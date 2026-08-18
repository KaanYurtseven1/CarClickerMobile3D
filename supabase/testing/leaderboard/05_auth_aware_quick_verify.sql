-- Auth-aware path quick verification SQL
-- Run after 02_seed_auth_aware.ps1 and after cleanup.

set search_path = public;

-- 1) Count seeded test profiles
select count(*) as test_profiles
from public.player_profiles
where display_name like 'TEST_%';

-- 2) Count seeded test scores
select count(*) as test_scores
from public.leaderboard_scores ls
join public.player_profiles pp on pp.id = ls.player_id
where pp.display_name like 'TEST_%';

-- 3) Show top 20 seeded players by score
select
  pp.display_name,
  ls.player_id,
  ls.racer_score,
  public.get_player_rank(ls.player_id) as rank
from public.leaderboard_scores ls
join public.player_profiles pp on pp.id = ls.player_id
where pp.display_name like 'TEST_%'
order by ls.racer_score desc, ls.player_id asc
limit 20;

-- 4) Optional: verify your real player rank/window (replace UUID first)
-- select public.get_player_rank('00000000-0000-0000-0000-000000000000'::uuid) as my_rank;
-- select * from public.get_leaderboard_window('00000000-0000-0000-0000-000000000000'::uuid, 50);
