param(
    [Parameter(Mandatory = $true)]
    [string]$SupabaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$ServiceRoleKey,

    [string]$TestPrefix = "TEST_",
    [string]$EmailPrefix = "leaderboard_test_",
    [switch]$Execute
)

$ErrorActionPreference = "Stop"

$adminHeaders = @{
    "apikey"        = $ServiceRoleKey
    "Authorization" = "Bearer $ServiceRoleKey"
    "Content-Type"  = "application/json"
}

$restHeaders = @{
    "apikey"        = $ServiceRoleKey
    "Authorization" = "Bearer $ServiceRoleKey"
    "Content-Type"  = "application/json"
}

function Invoke-SupabaseJson {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Headers,
        [object]$Body = $null
    )

    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $Headers
    }

    $json = $Body | ConvertTo-Json -Depth 8
    return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $Headers -Body $json
}

Write-Host "[Cleanup] Starting auth-aware cleanup preview..." -ForegroundColor Cyan

# 1) Find auth users by email prefix
$targetAuthUsers = New-Object System.Collections.Generic.List[object]
$page = 1
while ($true) {
    $listUrl = "$SupabaseUrl/auth/v1/admin/users?page=$page&per_page=200"
    $resp = Invoke-SupabaseJson -Method "GET" -Uri $listUrl -Headers $adminHeaders

    $users = @()
    if ($resp.users) { $users = $resp.users }
    elseif ($resp -is [System.Array]) { $users = $resp }

    if (-not $users -or $users.Count -eq 0) {
        break
    }

    foreach ($u in $users) {
        $email = "$($u.email)"
        if ($email.StartsWith($EmailPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $targetAuthUsers.Add([pscustomobject]@{
                id = "$($u.id)"
                email = $email
            }) | Out-Null
        }
    }

    $page++
}

# 2) Find profile rows by TEST_ prefix
$encodedLike = [System.Web.HttpUtility]::UrlEncode("$TestPrefix*")
$profilesUrl = "$SupabaseUrl/rest/v1/player_profiles?select=id,display_name&display_name=like.$encodedLike"
$targetProfiles = Invoke-SupabaseJson -Method "GET" -Uri $profilesUrl -Headers $restHeaders
if ($null -eq $targetProfiles) { $targetProfiles = @() }
if ($targetProfiles -isnot [System.Array]) { $targetProfiles = @($targetProfiles) }

Write-Host "[Cleanup] Preview counts:" -ForegroundColor Yellow
Write-Host "  Auth users with email prefix '$EmailPrefix': $($targetAuthUsers.Count)"
Write-Host "  Profiles with display_name prefix '$TestPrefix': $($targetProfiles.Count)"

if (-not $Execute.IsPresent) {
    Write-Host "[Cleanup] Dry run only. Re-run with -Execute to delete." -ForegroundColor Yellow
    return
}

Write-Host "[Cleanup] Executing delete..." -ForegroundColor Red

# Delete auth users first (expected to cascade profile/score depending schema)
$deletedAuth = 0
foreach ($u in $targetAuthUsers) {
    $deleteUrl = "$SupabaseUrl/auth/v1/admin/users/$($u.id)"
    Invoke-SupabaseJson -Method "DELETE" -Uri $deleteUrl -Headers $adminHeaders | Out-Null
    $deletedAuth++
}

# Fallback cleanup for any profile rows that still remain by TEST_ prefix
$remainingProfiles = Invoke-SupabaseJson -Method "GET" -Uri $profilesUrl -Headers $restHeaders
if ($null -eq $remainingProfiles) { $remainingProfiles = @() }
if ($remainingProfiles -isnot [System.Array]) { $remainingProfiles = @($remainingProfiles) }

$deletedScoresFallback = 0
$deletedProfilesFallback = 0

foreach ($p in $remainingProfiles) {
    $pid = "$($p.id)"

    $deleteScoreUrl = "$SupabaseUrl/rest/v1/leaderboard_scores?player_id=eq.$pid"
    Invoke-SupabaseJson -Method "DELETE" -Uri $deleteScoreUrl -Headers $restHeaders | Out-Null
    $deletedScoresFallback++

    $deleteProfileUrl = "$SupabaseUrl/rest/v1/player_profiles?id=eq.$pid"
    Invoke-SupabaseJson -Method "DELETE" -Uri $deleteProfileUrl -Headers $restHeaders | Out-Null
    $deletedProfilesFallback++
}

Write-Host "[Cleanup] Completed." -ForegroundColor Green
Write-Host "  Deleted auth users: $deletedAuth"
Write-Host "  Fallback deleted scores: $deletedScoresFallback"
Write-Host "  Fallback deleted profiles: $deletedProfilesFallback"

Write-Host "[Cleanup] Verify in SQL editor:" -ForegroundColor Cyan
Write-Host "select count(*) from public.player_profiles where display_name like '$TestPrefix%';"
Write-Host "select count(*) from public.leaderboard_scores ls join public.player_profiles pp on pp.id = ls.player_id where pp.display_name like '$TestPrefix%';"
