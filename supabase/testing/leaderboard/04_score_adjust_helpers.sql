-- Leaderboard Test Package - Real player score positioning helpers
-- Purpose: move your real player to top/middle/bottom regions for window validation.
-- Safety: requires explicit player UUID and never targets TEST_ profiles.

set search_path = public;

-- Helper A: identify your current player row quickly.
-- Replace :your_player_uuid with your real UUID.
select
  pp.id,
  pp.display_name,
  ls.racer_score,
  public.get_player_rank(pp.id) as current_rank
from public.player_profiles pp
left join public.leaderboard_scores ls on ls.player_id = pp.id
where pp.id = '00000000-0000-0000-0000-000000000000'::uuid;

-- Helper B: set your real player near TOP/MIDDLE/BOTTOM.
-- Edit v_player_id and v_target_region.
-- v_target_region allowed values: TOP, MIDDLE, BOTTOM

do $$
declare
  -- ======== CONFIGURE THESE VALUES BEFORE RUNNING ========
  v_player_id uuid := '00000000-0000-0000-0000-000000000000'::uuid;
  v_target_region text := 'MIDDLE'; -- TOP | MIDDLE | BOTTOM
  v_guard_test_prefix text := 'TEST_';
  -- ======== END CONFIG ========

  v_is_test_profile boolean;
  v_total integer;
  v_target_score bigint;
  v_current_score bigint;
  v_top_score bigint;
  v_bottom_score bigint;
  v_middle_anchor bigint;
  v_has_score_row boolean;
begin
  if to_regclass('public.player_profiles') is null or to_regclass('public.leaderboard_scores') is null then
    raise exception 'Missing required leaderboard tables.';
  end if;

  select (pp.display_name like v_guard_test_prefix || '%')
  into v_is_test_profile
  from public.player_profiles pp
  where pp.id = v_player_id;

  if v_is_test_profile is null then
    raise exception 'Player ID % not found in player_profiles.', v_player_id;
  end if;

  if v_is_test_profile then
    raise exception 'Refusing to reposition TEST_ profile. Use a real player UUID.';
  end if;

  select exists (
    select 1
    from public.leaderboard_scores ls
    where ls.player_id = v_player_id
  ) into v_has_score_row;

  if not v_has_score_row then
    raise exception
      'No leaderboard_scores row exists for player %. Submit score once from app before using this helper.',
      v_player_id;
  end if;

  select racer_score into v_current_score
  from public.leaderboard_scores
  where player_id = v_player_id;

  select count(*), coalesce(max(racer_score), 1), coalesce(min(racer_score), 1)
  into v_total, v_top_score, v_bottom_score
  from public.leaderboard_scores;

  if upper(v_target_region) = 'TOP' then
    v_target_score := v_top_score + 1000;
  elsif upper(v_target_region) = 'BOTTOM' then
    v_target_score := greatest(1, v_bottom_score - 1);
  elsif upper(v_target_region) = 'MIDDLE' then
    -- Approximate middle by taking score around median rank and nudging nearby.
    select s.racer_score
    into v_middle_anchor
    from (
      select racer_score, row_number() over (order by racer_score desc, updated_at asc nulls last, player_id asc) as rn
      from public.leaderboard_scores
    ) s
    where s.rn = greatest(1, (v_total / 2));

    v_target_score := greatest(1, coalesce(v_middle_anchor, v_current_score) + 3);
  else
    raise exception 'Invalid v_target_region: %. Allowed: TOP, MIDDLE, BOTTOM.', v_target_region;
  end if;

  update public.leaderboard_scores
  set racer_score = v_target_score
  where player_id = v_player_id;

  raise notice 'Player repositioned. player_id=%, old_score=%, new_score=%, target_region=%',
    v_player_id, v_current_score, v_target_score, upper(v_target_region);
end $$;

-- Helper C: verify rank and sample window from DB side
-- Replace UUID before running.
select public.get_player_rank('00000000-0000-0000-0000-000000000000'::uuid) as new_rank;

select *
from public.get_leaderboard_window('00000000-0000-0000-0000-000000000000'::uuid, 50);
