-- Leaderboard Test Package - Seed deterministic TEST_ players
-- Purpose: Seed fake leaderboard rows for window/rank verification.
-- Safe defaults:
--   - Uses TEST_ prefix only.
--   - Never touches non-TEST profiles.
--   - Does NOT modify auth identities.
--   - Aborts by default if player_profiles.id -> auth.users FK exists.

set search_path = public;

do $$
declare
  -- ======== CONFIGURE THESE VALUES BEFORE RUNNING ========
  v_test_prefix text := 'TEST_';
  v_seed_count integer := 70; -- Suggested values: 1, 10, 50, 70, 1000

  -- Score distribution: deterministic descending ladder.
  -- TEST_001 gets top score, TEST_002 slightly lower, etc.
  v_top_score bigint := 5000000;
  v_score_step bigint := 137;

  -- Safety controls
  v_purge_existing_test_prefix_first boolean := true;
  v_abort_if_profiles_fk_to_auth_users boolean := true;

  -- Optional: keep this false unless you intentionally want to reseed while preserving old TEST_ rows.
  v_upsert_existing_test_rows boolean := true;
  -- ======== END CONFIG ========

  has_profiles_auth_fk boolean;
  has_profile_sequential_id boolean;
  has_score_total_money boolean;
  has_score_total_buildings boolean;
  has_score_card_sum boolean;
  has_score_highest_tier boolean;
  has_score_blacklist_tiers boolean;
  has_score_updated_at boolean;

  v_profile_insert_cols text;
  v_profile_select_cols text;
  v_profile_update_set text;

  v_score_insert_cols text;
  v_score_select_cols text;
  v_score_update_set text;

  v_deleted_scores integer := 0;
  v_deleted_profiles integer := 0;
  v_seeded_profiles integer := 0;
  v_seeded_scores integer := 0;
begin
  if v_seed_count < 1 then
    raise exception 'v_seed_count must be >= 1. Current: %', v_seed_count;
  end if;

  if v_top_score <= 0 or v_score_step <= 0 then
    raise exception 'v_top_score and v_score_step must both be > 0.';
  end if;

  if v_top_score - ((v_seed_count - 1)::bigint * v_score_step) <= 0 then
    raise exception 'Score ladder would produce non-positive scores. Increase v_top_score or reduce v_score_step/count.';
  end if;

  if to_regclass('public.player_profiles') is null then
    raise exception 'Missing table public.player_profiles';
  end if;

  if to_regclass('public.leaderboard_scores') is null then
    raise exception 'Missing table public.leaderboard_scores';
  end if;

  select exists (
    select 1
    from pg_constraint con
    join pg_class child_tbl on child_tbl.oid = con.conrelid
    join pg_namespace child_ns on child_ns.oid = child_tbl.relnamespace
    join pg_class parent_tbl on parent_tbl.oid = con.confrelid
    join pg_namespace parent_ns on parent_ns.oid = parent_tbl.relnamespace
    where con.contype = 'f'
      and child_ns.nspname = 'public'
      and child_tbl.relname = 'player_profiles'
      and parent_ns.nspname = 'auth'
      and parent_tbl.relname = 'users'
  ) into has_profiles_auth_fk;

  if has_profiles_auth_fk and v_abort_if_profiles_fk_to_auth_users then
    raise exception
      'Seed aborted for safety: public.player_profiles.id references auth.users.id. This script does not create auth users. Either disable v_abort_if_profiles_fk_to_auth_users after review, or seed through an auth-aware path.';
  end if;

  select exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'player_profiles' and column_name = 'sequential_id'
  ) into has_profile_sequential_id;

  select exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'leaderboard_scores' and column_name = 'total_money_earned'
  ) into has_score_total_money;

  select exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'leaderboard_scores' and column_name = 'total_building_count'
  ) into has_score_total_buildings;

  select exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'leaderboard_scores' and column_name = 'card_level_sum'
  ) into has_score_card_sum;

  select exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'leaderboard_scores' and column_name = 'highest_building_tier'
  ) into has_score_highest_tier;

  select exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'leaderboard_scores' and column_name = 'blacklist_tiers_completed'
  ) into has_score_blacklist_tiers;

  select exists (
    select 1 from information_schema.columns
    where table_schema = 'public' and table_name = 'leaderboard_scores' and column_name = 'updated_at'
  ) into has_score_updated_at;

  create temporary table if not exists tmp_leaderboard_seed_rows (
    player_id uuid primary key,
    display_name text not null,
    sequential_id bigint not null,
    racer_score bigint not null,
    total_money_earned numeric,
    total_building_count integer,
    card_level_sum integer,
    highest_building_tier integer,
    blacklist_tiers_completed integer,
    updated_at timestamptz not null
  ) on commit drop;

  truncate table tmp_leaderboard_seed_rows;

  insert into tmp_leaderboard_seed_rows (
    player_id,
    display_name,
    sequential_id,
    racer_score,
    total_money_earned,
    total_building_count,
    card_level_sum,
    highest_building_tier,
    blacklist_tiers_completed,
    updated_at
  )
  select
    (
      substr(md5(v_test_prefix || lpad(gs.i::text, 6, '0')), 1, 8) || '-' ||
      substr(md5(v_test_prefix || lpad(gs.i::text, 6, '0')), 9, 4) || '-' ||
      substr(md5(v_test_prefix || lpad(gs.i::text, 6, '0')), 13, 4) || '-' ||
      substr(md5(v_test_prefix || lpad(gs.i::text, 6, '0')), 17, 4) || '-' ||
      substr(md5(v_test_prefix || lpad(gs.i::text, 6, '0')), 21, 12)
    )::uuid as player_id,
    v_test_prefix || lpad(gs.i::text, 3, '0') as display_name,
    900000000 + gs.i as sequential_id,
    v_top_score - ((gs.i - 1)::bigint * v_score_step) as racer_score,
    1e12 + ((v_seed_count - gs.i + 1) * 1e9) as total_money_earned,
    500 + (v_seed_count - gs.i) as total_building_count,
    100 + ((v_seed_count - gs.i) / 2) as card_level_sum,
    27 as highest_building_tier,
    6 as blacklist_tiers_completed,
    now() - make_interval(secs => gs.i)
  from generate_series(1, v_seed_count) as gs(i);

  if v_purge_existing_test_prefix_first then
    delete from public.leaderboard_scores ls
    using public.player_profiles pp
    where ls.player_id = pp.id
      and pp.display_name like v_test_prefix || '%';
    get diagnostics v_deleted_scores = row_count;

    delete from public.player_profiles
    where display_name like v_test_prefix || '%';
    get diagnostics v_deleted_profiles = row_count;
  end if;

  v_profile_insert_cols := 'id, display_name';
  v_profile_select_cols := 's.player_id, s.display_name';
  v_profile_update_set := 'display_name = excluded.display_name';

  if has_profile_sequential_id then
    v_profile_insert_cols := v_profile_insert_cols || ', sequential_id';
    v_profile_select_cols := v_profile_select_cols || ', s.sequential_id';
    v_profile_update_set := v_profile_update_set || ', sequential_id = excluded.sequential_id';
  end if;

  execute format(
    'insert into public.player_profiles (%s)
     select %s
     from tmp_leaderboard_seed_rows s
     on conflict (id) do %s',
    v_profile_insert_cols,
    v_profile_select_cols,
    case when v_upsert_existing_test_rows then 'update set ' || v_profile_update_set else 'nothing' end
  );
  get diagnostics v_seeded_profiles = row_count;

  v_score_insert_cols := 'player_id, racer_score';
  v_score_select_cols := 's.player_id, s.racer_score';
  v_score_update_set := 'racer_score = excluded.racer_score';

  if has_score_total_money then
    v_score_insert_cols := v_score_insert_cols || ', total_money_earned';
    v_score_select_cols := v_score_select_cols || ', s.total_money_earned';
    v_score_update_set := v_score_update_set || ', total_money_earned = excluded.total_money_earned';
  end if;

  if has_score_total_buildings then
    v_score_insert_cols := v_score_insert_cols || ', total_building_count';
    v_score_select_cols := v_score_select_cols || ', s.total_building_count';
    v_score_update_set := v_score_update_set || ', total_building_count = excluded.total_building_count';
  end if;

  if has_score_card_sum then
    v_score_insert_cols := v_score_insert_cols || ', card_level_sum';
    v_score_select_cols := v_score_select_cols || ', s.card_level_sum';
    v_score_update_set := v_score_update_set || ', card_level_sum = excluded.card_level_sum';
  end if;

  if has_score_highest_tier then
    v_score_insert_cols := v_score_insert_cols || ', highest_building_tier';
    v_score_select_cols := v_score_select_cols || ', s.highest_building_tier';
    v_score_update_set := v_score_update_set || ', highest_building_tier = excluded.highest_building_tier';
  end if;

  if has_score_blacklist_tiers then
    v_score_insert_cols := v_score_insert_cols || ', blacklist_tiers_completed';
    v_score_select_cols := v_score_select_cols || ', s.blacklist_tiers_completed';
    v_score_update_set := v_score_update_set || ', blacklist_tiers_completed = excluded.blacklist_tiers_completed';
  end if;

  if has_score_updated_at then
    v_score_insert_cols := v_score_insert_cols || ', updated_at';
    v_score_select_cols := v_score_select_cols || ', s.updated_at';
    v_score_update_set := v_score_update_set || ', updated_at = excluded.updated_at';
  end if;

  execute format(
    'insert into public.leaderboard_scores (%s)
     select %s
     from tmp_leaderboard_seed_rows s
     on conflict (player_id) do update set %s',
    v_score_insert_cols,
    v_score_select_cols,
    v_score_update_set
  );
  get diagnostics v_seeded_scores = row_count;

  raise notice 'Seed completed. test_prefix=%, seed_count=%', v_test_prefix, v_seed_count;
  raise notice 'Deleted existing TEST rows first: profiles=%, scores=%', v_deleted_profiles, v_deleted_scores;
  raise notice 'Inserted/Upserted: profiles=%, scores=%', v_seeded_profiles, v_seeded_scores;

  raise notice 'Top sample:';
  perform 1;
end $$;

-- Quick verification: deterministic top 15 after seeding
select
  ls.player_id,
  pp.display_name,
  ls.racer_score
from public.leaderboard_scores ls
join public.player_profiles pp on pp.id = ls.player_id
where pp.display_name like 'TEST_%'
order by ls.racer_score desc, ls.updated_at asc, ls.player_id asc
limit 15;

-- Quick verification: count of seeded TEST rows
select
  count(*) as seeded_test_profiles
from public.player_profiles
where display_name like 'TEST_%';
