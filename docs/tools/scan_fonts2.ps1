$root = "c:\Users\kaanyurt7\Desktop\Car_Clicker\CarClickerMobile3D"
Set-Location $root

$fontMap = @{
    "fe75d96226daefa49b53ffc633e65bd0" = "BigStem-Regular.ttf"
    "cdd1332859c262b4fb023b31f9548ae1" = "Big Stem Oblique.ttf"
    "2a4e0a7b22cc78349b1449da0667c945" = "BigStem-Regular SDF"
    "07fe05e9d49688640a74cdeab2ac57c6" = "Big Stem Oblique SDF"
    "fda2d78545fef6c4c8589594ad06b701" = "BebasNeue-Regular SDF"
    "8289c01dc9db514499bb618c08dc9cf0" = "BebasNeue-Regular.ttf"
    "8f586378b4e144a9851e7b34d9b748ee" = "LiberationSans SDF"
    "2e498d1c8094910479dc3e1b768306a4" = "LiberationSans SDF - Fallback"
}

$bigStemGuids = @(
    "fe75d96226daefa49b53ffc633e65bd0",
    "cdd1332859c262b4fb023b31f9548ae1",
    "2a4e0a7b22cc78349b1449da0667c945",
    "07fe05e9d49688640a74cdeab2ac57c6"
)

$tmpUGUI = "f4688fdb7df04437aeb418b961361dc5"
$tmpWorld = "b9839c2d141782e41b2b87a2a1ef6f78"
$uiText = "5f7201a12d95ffc409449d95f23cf332"

$allFiles = @(
    "Assets\Scenes\Main.unity",
    "Assets\Scenes\ChestOpenScene.unity",
    "Assets\Scenes\NewGarage.unity",
    "Assets\Scenes\TakeTheCarScene.unity",
    "Assets\Scenes\TestScene.unity",
    "Assets\_Recovery\0.unity",
    "Assets\Prefabs\Blacklist\MissionRow.prefab",
    "Assets\Prefabs\UI\CardSlot\CardSlot.prefab",
    "Assets\Prefabs\ChestSlotPrefab.prefab",
    "Assets\Prefabs\FloatingText.prefab",
    "Assets\Prefabs\SummarySlotPrefab.prefab",
    "Assets\Prefabs\WorldCardPrefab_TMP.prefab",
    "Assets\Prefabs\WorldRewardCardPrefab_TMP.prefab"
)

$allResults = New-Object System.Collections.ArrayList

foreach ($relFile in $allFiles) {
    $fullPath = Join-Path $root $relFile
    if (-not (Test-Path $fullPath)) {
        Write-Output "SKIP: $relFile"
        continue
    }
    Write-Output "SCAN: $relFile"

    $content = [System.IO.File]::ReadAllText($fullPath)
    $lines = $content -split "`n"
    $total = $lines.Count

    # Pass 1: build name map and hierarchy
    $names = @{}
    $goForComponent = @{} # component fileID -> GO fileID
    $transformForGO = @{} # GO fileID -> transform fileID
    $parentOfTransform = @{} # transform fileID -> parent transform fileID
    $goActiveState = @{} # GO fileID -> bool

    $curType = ""
    $curID = ""
    $curGOref = ""

    for ($i = 0; $i -lt $total; $i++) {
        $l = $lines[$i].Trim()

        if ($l.StartsWith("--- !u!")) {
            if ($l -match "^--- !u!(\d+) &(\d+)") {
                $curType = $Matches[1]
                $curID = $Matches[2]
                $curGOref = ""
            }
            continue
        }

        if ($l.StartsWith("m_Name:") -and $curType -eq "1") {
            $n = $l.Substring(8).Trim()
            $names[$curID] = $n
        }

        if ($l.StartsWith("m_IsActive:") -and $curType -eq "1") {
            $goActiveState[$curID] = ($l -replace "m_IsActive:\s*", "").Trim()
        }

        if ($l.StartsWith("m_GameObject:") -and $l -match "fileID: (\d+)") {
            $curGOref = $Matches[1]
            $goForComponent[$curID] = $curGOref
            if ($curType -eq "4" -or $curType -eq "224") {
                $transformForGO[$curGOref] = $curID
            }
        }

        if ($l.StartsWith("m_Father:") -and $l -match "fileID: (\d+)") {
            $parentOfTransform[$curID] = $Matches[1]
        }

        if ($l.StartsWith("- component:") -and $l -match "fileID: (\d+)") {
            $goForComponent[$Matches[1]] = $curID
        }
    }

    # Pass 2: find text components
    $curType = ""
    $curID = ""
    $inTMP = $false
    $compType = ""
    $curGO2 = ""
    $curFontGuid = ""
    $curText = ""

    for ($i = 0; $i -lt $total; $i++) {
        $l = $lines[$i].Trim()

        if ($l.StartsWith("--- !u!")) {
            # Flush prev
            if ($inTMP -and $curGO2) {
                # Build hierarchy
                $path = @()
                $tID = $transformForGO[$curGO2]
                $safety = 0
                while ($tID -and $tID -ne "0" -and $safety -lt 50) {
                    $gID = $goForComponent[$tID]
                    if ($gID -and $names[$gID]) { $path = ,($names[$gID]) + $path }
                    $tID = $parentOfTransform[$tID]
                    $safety++
                }
                $hierStr = $path -join "/"
                $fName = if ($curFontGuid -and $fontMap.ContainsKey($curFontGuid)) { $fontMap[$curFontGuid] } elseif ($curFontGuid) { "UNKNOWN($curFontGuid)" } else { "NO_FONT_REF" }
                $isBig = $bigStemGuids -contains $curFontGuid
                $active = if ($goActiveState[$curGO2]) { $goActiveState[$curGO2] } else { "?" }
                [void]$allResults.Add([PSCustomObject]@{
                    File = $relFile; Hierarchy = $hierStr; CompType = $compType
                    FontName = $fName; FontGUID = $curFontGuid; IsBigStem = $isBig
                    TextContent = $curText; Active = $active
                })
            }

            $inTMP = $false; $compType = ""; $curGO2 = ""; $curFontGuid = ""; $curText = ""
            if ($l -match "^--- !u!(\d+) &(\d+)") {
                $curType = $Matches[1]; $curID = $Matches[2]
            }
            continue
        }

        if ($curType -eq "114" -and $l.StartsWith("m_Script:") -and $l -match "guid: ([a-f0-9]+)") {
            $sg = $Matches[1]
            if ($sg -eq $tmpUGUI) { $inTMP = $true; $compType = "TextMeshProUGUI" }
            elseif ($sg -eq $tmpWorld) { $inTMP = $true; $compType = "TextMeshPro3D" }
            elseif ($sg -eq $uiText) { $inTMP = $true; $compType = "UI.Text" }
        }

        if ($inTMP) {
            if ($l.StartsWith("m_GameObject:") -and $l -match "fileID: (\d+)") { $curGO2 = $Matches[1] }
            if ($l.StartsWith("m_fontAsset:") -and $l -match "guid: ([a-f0-9]+)") { $curFontGuid = $Matches[1] }
            if ($l.StartsWith("m_Font:") -and $l -match "guid: ([a-f0-9]+)") { $curFontGuid = $Matches[1] }
            if ($l.StartsWith("m_text:")) { $curText = $l.Substring(7).Trim() }
        }
    }

    # Flush last
    if ($inTMP -and $curGO2) {
        $path = @()
        $tID = $transformForGO[$curGO2]
        $safety = 0
        while ($tID -and $tID -ne "0" -and $safety -lt 50) {
            $gID = $goForComponent[$tID]
            if ($gID -and $names[$gID]) { $path = ,($names[$gID]) + $path }
            $tID = $parentOfTransform[$tID]
            $safety++
        }
        $hierStr = $path -join "/"
        $fName = if ($curFontGuid -and $fontMap.ContainsKey($curFontGuid)) { $fontMap[$curFontGuid] } elseif ($curFontGuid) { "UNKNOWN($curFontGuid)" } else { "NO_FONT_REF" }
        $isBig = $bigStemGuids -contains $curFontGuid
        $active = if ($goActiveState[$curGO2]) { $goActiveState[$curGO2] } else { "?" }
        [void]$allResults.Add([PSCustomObject]@{
            File = $relFile; Hierarchy = $hierStr; CompType = $compType
            FontName = $fName; FontGUID = $curFontGuid; IsBigStem = $isBig
            TextContent = $curText; Active = $active
        })
    }
}

Write-Output ""
Write-Output "=========================================="
Write-Output "TOTAL TEXT ELEMENTS: $($allResults.Count)"
Write-Output "=========================================="

$correct = @($allResults | Where-Object { $_.IsBigStem })
$incorrect = @($allResults | Where-Object { -not $_.IsBigStem })

Write-Output ""
Write-Output "=== CORRECT (BigStem) === Count: $($correct.Count)"
foreach ($r in $correct) {
    Write-Output "  [$($r.CompType)] $($r.File) | $($r.Hierarchy) | Font=$($r.FontName) | Text='$($r.TextContent)' | Active=$($r.Active)"
}

Write-Output ""
Write-Output "=== NON-BIGSTEM === Count: $($incorrect.Count)"
foreach ($r in $incorrect) {
    Write-Output "  [$($r.CompType)] $($r.File) | $($r.Hierarchy) | Font=$($r.FontName) | GUID=$($r.FontGUID) | Text='$($r.TextContent)' | Active=$($r.Active)"
}
