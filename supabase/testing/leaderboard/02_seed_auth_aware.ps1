param(
    [Parameter(Mandatory = $true)]
    [string]$SupabaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$ServiceRoleKey,

    [int]$SeedCount = 70,
    [string]$TestPrefix = "TEST_",
    [string]$EmailPrefix = "leaderboard_test_",
    [string]$EmailDomain = "example.invalid",
    [long]$TopScore = 5000000,
    [long]$ScoreStep = 137,
    [switch]$PurgeExistingAuthUsers
)

$ErrorActionPreference = "Stop"

if ($SeedCount -lt 1) {
    throw "SeedCount must be >= 1."
}

$minScore = $TopScore - (($SeedCount - 1) * $ScoreStep)
if ($minScore -le 0) {
    throw "Score ladder becomes non-positive. Increase TopScore or reduce ScoreStep/SeedCount."
}

$adminHeaders = @{
    "apikey"        = $ServiceRoleKey
    "Authorization" = "Bearer $ServiceRoleKey"
    "Content-Type"  = "application/json"
}

$restUpsertHeaders = @{
    "apikey"                     = $ServiceRoleKey
    "Authorization"              = "Bearer $ServiceRoleKey"
    "Content-Type"               = "application/json"
    "Prefer"                     = "resolution=merge-duplicates"
}

$restPatchHeaders = @{
    "apikey"                     = $ServiceRoleKey
    "Authorization"              = "Bearer $ServiceRoleKey"
    "Content-Type"               = "application/json"
    "Prefer"                     = "return=representation"
}

function Get-ErrorResponseBody {
    param([System.Exception]$Exception)

    try {
        if ($Exception.Response -and $Exception.Response.GetResponseStream()) {
            $reader = New-Object System.IO.StreamReader($Exception.Response.GetResponseStream())
            $body = $reader.ReadToEnd()
            $reader.Dispose()
            return $body
        }
    }
    catch {
        return "<unable to read error response body>"
    }

    return "<no response body>"
}

function Invoke-SupabaseJson {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Headers,
        [object]$Body = $null,
        [string]$Context = ""
    )

    Write-Host "[HTTP] $Method $Uri | context=$Context" -ForegroundColor DarkCyan

    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $Headers
        }

        $json = $Body | ConvertTo-Json -Depth 8
        return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $Headers -Body $json
    }
    catch {
        $responseBody = Get-ErrorResponseBody -Exception $_.Exception
        Write-Host "[HTTP-ERROR] context=$Context" -ForegroundColor Red
        Write-Host "[HTTP-ERROR] endpoint=$Uri" -ForegroundColor Red
        Write-Host "[HTTP-ERROR] method=$Method" -ForegroundColor Red
        Write-Host "[HTTP-ERROR] response=$responseBody" -ForegroundColor Red
        throw
    }
}

Write-Host "[Seed] Starting auth-aware seeding..." -ForegroundColor Cyan
Write-Host "[Seed] SupabaseUrl=$SupabaseUrl"
Write-Host "[Seed] SeedCount=$SeedCount TestPrefix=$TestPrefix EmailPrefix=$EmailPrefix"

$existingSeedUsersByEmail = @{}

function Load-ExistingSeedUsers {
    $page = 1
    while ($true) {
        $listUrl = "$SupabaseUrl/auth/v1/admin/users?page=$page&per_page=200"
        $resp = Invoke-SupabaseJson -Method "GET" -Uri $listUrl -Headers $adminHeaders -Context "load-existing-users page=$page"
        $users = @()
        if ($resp.users) { $users = $resp.users }
        elseif ($resp -is [System.Array]) { $users = $resp }

        if (-not $users -or $users.Count -eq 0) {
            break
        }

        foreach ($u in $users) {
            $email = "$($u.email)"
            if ($email.StartsWith($EmailPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $existingSeedUsersByEmail[$email.ToLowerInvariant()] = "$($u.id)"
            }
        }

        $page++
    }
}

Load-ExistingSeedUsers
Write-Host "[Seed] Existing seeded auth users found: $($existingSeedUsersByEmail.Count)" -ForegroundColor Yellow

if ($PurgeExistingAuthUsers.IsPresent) {
    Write-Host "[Seed] Purging existing seeded auth users first..." -ForegroundColor Yellow

    $page = 1
    $deletedCount = 0
    while ($true) {
        $listUrl = "$SupabaseUrl/auth/v1/admin/users?page=$page&per_page=200"
        $resp = Invoke-SupabaseJson -Method "GET" -Uri $listUrl -Headers $adminHeaders -Context "purge-list page=$page"
        $users = @()
        if ($resp.users) { $users = $resp.users }
        elseif ($resp -is [System.Array]) { $users = $resp }

        if (-not $users -or $users.Count -eq 0) {
            break
        }

        foreach ($u in $users) {
            $email = "$($u.email)"
            if ($email.StartsWith($EmailPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $deleteUrl = "$SupabaseUrl/auth/v1/admin/users/$($u.id)"
                Invoke-SupabaseJson -Method "DELETE" -Uri $deleteUrl -Headers $adminHeaders -Context "purge-delete email=$email" | Out-Null
                $deletedCount++
            }
        }

        $page++
    }

    Write-Host "[Seed] Purged seeded auth users: $deletedCount" -ForegroundColor Yellow

    $existingSeedUsersByEmail.Clear()
}

$created = New-Object System.Collections.Generic.List[object]

for ($i = 1; $i -le $SeedCount; $i++) {
    $idx3 = $i.ToString("000")
    $idx6 = $i.ToString("000000")

    $email = "$EmailPrefix$idx6@$EmailDomain"
    $displayName = "$TestPrefix$idx3"
    $score = $TopScore - (($i - 1) * $ScoreStep)

    $createBody = @{
        email = $email
        password = "Test!1234$idx3"
        email_confirm = $true
        user_metadata = @{
            seed_tag = "LEADERBOARD_TEST"
            seed_index = $i
            display_name = $displayName
        }
    }

    try {
        $emailKey = $email.ToLowerInvariant()
        $playerId = $null

        if ($existingSeedUsersByEmail.ContainsKey($emailKey)) {
            $playerId = $existingSeedUsersByEmail[$emailKey]
            Write-Host "[Seed] Reusing existing auth user for index=$i email=$email user_id=$playerId" -ForegroundColor Yellow
        }
        else {
            $createUrl = "$SupabaseUrl/auth/v1/admin/users"
            $createdUser = Invoke-SupabaseJson -Method "POST" -Uri $createUrl -Headers $adminHeaders -Body $createBody -Context "create-user index=$i email=$email"
            $playerId = "$($createdUser.id)"
            if ([string]::IsNullOrWhiteSpace($playerId)) {
                throw "Failed to create user for $displayName (empty id)."
            }

            $existingSeedUsersByEmail[$emailKey] = $playerId
        }

        $profilePatchUrl = "$SupabaseUrl/rest/v1/player_profiles?id=eq.$playerId"
        $profilePatchBody = @{
            display_name = $displayName
        }
        $profileResult = Invoke-SupabaseJson -Method "PATCH" -Uri $profilePatchUrl -Headers $restPatchHeaders -Body $profilePatchBody -Context "patch-profile index=$i email=$email user_id=$playerId"

        if (-not $profileResult -or $profileResult.Count -eq 0) {
            throw "Profile row update failed for user $playerId. Check profile-creation trigger or RLS/service role access."
        }

        $scoreUpsertUrl = "$SupabaseUrl/rest/v1/leaderboard_scores?on_conflict=player_id"
        $scoreUpsertBody = @(
            @{
                player_id = $playerId
                racer_score = $score
            }
        )
        Invoke-SupabaseJson -Method "POST" -Uri $scoreUpsertUrl -Headers $restUpsertHeaders -Body $scoreUpsertBody -Context "upsert-score index=$i email=$email user_id=$playerId" | Out-Null
    }
    catch {
        throw "Seed failed at index=$i email=$email display_name=$displayName. Error: $($_.Exception.Message)"
    }

    $created.Add([pscustomobject]@{
        player_id = $playerId
        email = $email
        display_name = $displayName
        racer_score = $score
    }) | Out-Null

    if (($i % 25) -eq 0 -or $i -eq $SeedCount) {
        Write-Host "[Seed] Progress: $i / $SeedCount"
    }
}

$created | Sort-Object racer_score -Descending | Select-Object -First 10 | Format-Table -AutoSize

Write-Host "[Seed] Completed. Created users: $($created.Count)" -ForegroundColor Green
Write-Host "[Seed] Suggested next SQL verification:" -ForegroundColor Cyan
Write-Host "select count(*) from public.player_profiles where display_name like '$TestPrefix%';"
Write-Host "select count(*) from public.leaderboard_scores ls join public.player_profiles pp on pp.id = ls.player_id where pp.display_name like '$TestPrefix%';"
