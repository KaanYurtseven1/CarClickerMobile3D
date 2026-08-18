-- Leaderboard Test Package - Precheck
-- Purpose: Validate schema/function assumptions before seeding fake leaderboard data.
-- Safe by default: read-only checks only.

set search_path = public;

-- 1) Required tables
select
  'table_exists' as check_type,
  tbl,
  case when to_regclass(tbl) is not null then 'ok' else 'missing' end as status
from (values
  ('public.player_profiles'),
  ('public.leaderboard_scores')
) as t(tbl)
order by tbl;

-- 2) Required columns
with required_columns as (
  select * from (values
    ('public', 'player_profiles', 'id'),
    ('public', 'player_profiles', 'display_name'),
    ('public', 'leaderboard_scores', 'player_id'),
    ('public', 'leaderboard_scores', 'racer_score')
  ) as v(table_schema, table_name, column_name)
)
select
  'column_exists' as check_type,
  rc.table_schema,
  rc.table_name,
  rc.column_name,
  case when c.column_name is not null then 'ok' else 'missing' end as status,
  c.data_type
from required_columns rc
left join information_schema.columns c
  on c.table_schema = rc.table_schema
 and c.table_name = rc.table_name
 and c.column_name = rc.column_name
order by rc.table_name, rc.column_name;

-- 3) Required RPC/functions used by current architecture
select
  'function_exists' as check_type,
  fn.signature,
  case when to_regprocedure(fn.signature) is not null then 'ok' else 'missing' end as status
from (values
  ('public.get_player_rank(uuid)'),
  ('public.get_leaderboard_window(uuid,integer)'),
  ('public.reset_player_ranking_progress(uuid)')
) as fn(signature)
order by fn.signature;

-- 4) Constraints relevant to leaderboard integrity
select
  'constraint' as check_type,
  n.nspname as schema_name,
  cls.relname as table_name,
  con.conname as constraint_name,
  case con.contype
    when 'p' then 'primary_key'
    when 'f' then 'foreign_key'
    when 'u' then 'unique'
    when 'c' then 'check'
    else con.contype::text
  end as constraint_type,
  pg_get_constraintdef(con.oid) as definition
from pg_constraint con
join pg_class cls on cls.oid = con.conrelid
join pg_namespace n on n.oid = cls.relnamespace
where n.nspname = 'public'
  and cls.relname in ('player_profiles', 'leaderboard_scores')
order by cls.relname, constraint_type, con.conname;

-- 5) Ranking-related indexes (performance + deterministic ordering helpers)
select
  'index' as check_type,
  schemaname,
  tablename,
  indexname,
  indexdef
from pg_indexes
where schemaname = 'public'
  and tablename in ('player_profiles', 'leaderboard_scores')
  and (
    indexdef ilike '%racer_score%'
    or indexdef ilike '%player_id%'
    or indexdef ilike '%updated_at%'
    or indexname ilike '%leaderboard%'
    or indexname ilike '%score%'
  )
order by tablename, indexname;

-- 6) Critical guard: detect if player_profiles.id references auth.users.id.
-- If true, direct profile seeding without creating auth users may fail.
select
  'profiles_auth_fk' as check_type,
  case when exists (
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
  ) then 'present' else 'not_present' end as status;

-- 7) Hard fail if minimum required objects are missing.
do $$
declare
  missing text[] := array[]::text[];
begin
  if to_regclass('public.player_profiles') is null then
    missing := array_append(missing, 'public.player_profiles table');
  end if;

  if to_regclass('public.leaderboard_scores') is null then
    missing := array_append(missing, 'public.leaderboard_scores table');
  end if;

  if to_regprocedure('public.get_player_rank(uuid)') is null then
    missing := array_append(missing, 'public.get_player_rank(uuid) function');
  end if;

  if to_regprocedure('public.get_leaderboard_window(uuid,integer)') is null then
    missing := array_append(missing, 'public.get_leaderboard_window(uuid,integer) function');
  end if;

  if array_length(missing, 1) is not null then
    raise exception 'Precheck failed. Missing required objects: %', array_to_string(missing, ', ');
  end if;

  raise notice 'Precheck passed for required tables/functions.';
end $$;
