-- Leaderboard Test Package - Cleanup TEST_ seeded rows
-- Purpose: remove only fake seeded rows safely.
-- Safe defaults:
--   - Prefix-restricted deletion only.
--   - Dry run enabled by default.

set search_path = public;

do $$
declare
  -- ======== CONFIGURE THESE VALUES BEFORE RUNNING ========
  v_test_prefix text := 'TEST_';
  v_dry_run boolean := true; -- set to false to execute delete
  -- ======== END CONFIG ========

  v_target_profiles integer := 0;
  v_target_scores integer := 0;
  v_deleted_scores integer := 0;
  v_deleted_profiles integer := 0;
begin
  if to_regclass('public.player_profiles') is null then
    raise exception 'Missing table public.player_profiles';
  end if;

  if to_regclass('public.leaderboard_scores') is null then
    raise exception 'Missing table public.leaderboard_scores';
  end if;

  select count(*)
  into v_target_profiles
  from public.player_profiles
  where display_name like v_test_prefix || '%';

  select count(*)
  into v_target_scores
  from public.leaderboard_scores ls
  join public.player_profiles pp on pp.id = ls.player_id
  where pp.display_name like v_test_prefix || '%';

  raise notice 'Cleanup target preview => prefix=%, profiles=%, scores=%',
    v_test_prefix, v_target_profiles, v_target_scores;

  if v_dry_run then
    raise notice 'Dry run active. No rows deleted. Set v_dry_run=false to execute cleanup.';
    return;
  end if;

  delete from public.leaderboard_scores ls
  using public.player_profiles pp
  where ls.player_id = pp.id
    and pp.display_name like v_test_prefix || '%';
  get diagnostics v_deleted_scores = row_count;

  delete from public.player_profiles
  where display_name like v_test_prefix || '%';
  get diagnostics v_deleted_profiles = row_count;

  raise notice 'Cleanup executed => deleted scores=%, deleted profiles=%', v_deleted_scores, v_deleted_profiles;
end $$;

-- Post-clean verification
select count(*) as remaining_test_profiles
from public.player_profiles
where display_name like 'TEST_%';

select count(*) as remaining_test_scores
from public.leaderboard_scores ls
join public.player_profiles pp on pp.id = ls.player_id
where pp.display_name like 'TEST_%';
