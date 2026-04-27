# Execution Guide When profiles_auth_fk = present

Use this guide when `01_precheck.sql` reports `profiles_auth_fk = present`.

Direct `02_seed_test_players.sql` is **not** valid for this schema because profile rows are tied to auth users.

## What to use instead

- `02_seed_auth_aware.ps1` (creates auth users through Supabase Admin API, then updates profile names and score rows)
- `05_auth_aware_quick_verify.sql` (DB checks)
- `04_score_adjust_helpers.sql` (move your real player rank region)
- `03_cleanup_auth_aware.ps1` (safe cleanup for auth-aware seeded users)

## Required inputs before running

- `SUPABASE_URL` (example: `https://xxxx.supabase.co`)
- `SUPABASE_SERVICE_ROLE_KEY` (staging/dev only)

## Recommended command sequence (PowerShell)

```powershell
cd supabase/testing/leaderboard

# Seed 70 users
./02_seed_auth_aware.ps1 `
  -SupabaseUrl "https://YOUR_PROJECT_REF.supabase.co" `
  -ServiceRoleKey "YOUR_SERVICE_ROLE_KEY" `
  -SeedCount 70

# Verify in SQL editor with 05_auth_aware_quick_verify.sql

# Cleanup preview
./03_cleanup_auth_aware.ps1 `
  -SupabaseUrl "https://YOUR_PROJECT_REF.supabase.co" `
  -ServiceRoleKey "YOUR_SERVICE_ROLE_KEY"

# Cleanup execute
./03_cleanup_auth_aware.ps1 `
  -SupabaseUrl "https://YOUR_PROJECT_REF.supabase.co" `
  -ServiceRoleKey "YOUR_SERVICE_ROLE_KEY" `
  -Execute
```

## Safety notes

- Use staging/dev project only.
- Keep prefixes unchanged unless you intentionally isolate another test batch.
- Never commit service role keys.
- Cleanup script is dry-run unless `-Execute` is passed.
