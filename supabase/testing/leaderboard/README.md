# Leaderboard Staging Test Package

This package is designed for SQL-first leaderboard validation without adding in-game fake runtime features.

## Files

- `01_precheck.sql`
  - Validates required tables, required columns, required RPC/functions, relevant constraints and indexes.
  - Hard-fails if critical required objects are missing.
- `02_seed_test_players.sql`
  - Seeds deterministic fake players with `TEST_` prefix and matching leaderboard rows.
  - Supports configurable counts (`1`, `10`, `50`, `70`, `1000`) via `v_seed_count`.
  - Default-safe behavior: prefix-scoped only, no real user deletion, no auth table mutation.
- `03_cleanup_test_players.sql`
  - Prefix-scoped cleanup for seeded fake rows only.
  - Starts as dry-run (`v_dry_run=true`) to prevent accidental deletes.
- `04_score_adjust_helpers.sql`
  - Helpers to place your real player near top/middle/bottom score regions and verify rank/window.

## Important schema note

If `player_profiles.id` has a foreign key to `auth.users.id`, direct profile seeding can fail unless matching auth rows exist.

- `01_precheck.sql` reports this as `profiles_auth_fk`.
- `02_seed_test_players.sql` aborts by default when this FK is detected (`v_abort_if_profiles_fk_to_auth_users=true`).

This is intentional for safety and to avoid breaking auth architecture.

## Suggested run order

1. Run `01_precheck.sql`.
2. If precheck passes and FK guard allows direct seeding, run `02_seed_test_players.sql` with desired `v_seed_count`.
3. Run game-side leaderboard checks in Unity.
4. Use `04_score_adjust_helpers.sql` to move your real player to top/middle/bottom and re-check window behavior.
5. Run `03_cleanup_test_players.sql` (dry-run first, then execute).

## Placeholder values to edit

- `02_seed_test_players.sql`
  - `v_seed_count`
  - `v_test_prefix` (default `TEST_`)
  - Optional ladder settings: `v_top_score`, `v_score_step`
- `03_cleanup_test_players.sql`
  - `v_test_prefix`
  - `v_dry_run` (`false` only when ready)
- `04_score_adjust_helpers.sql`
  - Replace `00000000-0000-0000-0000-000000000000` with your real player UUID
  - Set `v_target_region` = `TOP` / `MIDDLE` / `BOTTOM`

## Verification checklist

After seeding:

- `select count(*) from public.player_profiles where display_name like 'TEST_%';`
- `select count(*) from public.leaderboard_scores ls join public.player_profiles pp on pp.id = ls.player_id where pp.display_name like 'TEST_%';`
- Unity leaderboard panel:
  - window size behavior for 1, 10, 50, 70, 1000
  - self row highlight
  - scroll-to-self centering
  - total players + self rank values

After cleanup:

- Ensure both test counts return `0`.
- Verify your real profile/rank still exists.
