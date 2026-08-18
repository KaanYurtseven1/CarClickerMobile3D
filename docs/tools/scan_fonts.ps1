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

# TMP_Text GUIDs (TextMeshProUGUI and TextMeshPro)
$tmpUGUI = "f4688fdb7df04437aeb418b961361dc5"  # TMP UGUI
$tmpWorld = "b9839c2d141782e41b2b87a2a1ef6f78" # TMP world (3D)

# Unity UI Text GUID
$uiText = "5f7201a12d95ffc409449d95f23cf332" # UnityEngine.UI.Text

# All scenes + relevant prefabs
$scenes = @(
    "Assets\Scenes\Main.unity",
    "Assets\Scenes\ChestOpenScene.unity",
    "Assets\Scenes\NewGarage.unity",
    "Assets\Scenes\TakeTheCarScene.unity",
    "Assets\Scenes\TestScene.unity",
    "Assets\_Recovery\0.unity"
)

$prefabs = @(
    "Assets\Prefabs\Blacklist\MissionRow.prefab",
    "Assets\Prefabs\UI\CardSlot\CardSlot.prefab",
    "Assets\Prefabs\ChestSlotPrefab.prefab",
    "Assets\Prefabs\FloatingText.prefab",
    "Assets\Prefabs\SummarySlotPrefab.prefab",
    "Assets\Prefabs\WorldCardPrefab_TMP.prefab",
    "Assets\Prefabs\WorldRewardCardPrefab_TMP.prefab"
)

function Scan-File($filePath) {
    $shortName = $filePath -replace [regex]::Escape($root + "\"), ""
    $lines = Get-Content (Join-Path $root $filePath)
    $total = $lines.Count

    # Build object map: fileID -> name, fileID -> parent, fileID -> gameObject
    $names = @{}
    $parents = @{}  # Transform fileID -> parent Transform fileID
    $goToTransform = @{} # GameObject fileID -> Transform/RectTransform fileID
    $transformToGO = @{} # Transform fileID -> GameObject fileID
    $goComponents = @{} # GameObject fileID -> list of component fileIDs

    # First pass: collect all objects
    $currentObjType = ""
    $currentObjID = ""
    $currentGO = ""

    for ($i = 0; $i -lt $total; $i++) {
        $line = $lines[$i]

        # Detect new YAML object
        if ($line -match "^--- !u!(\d+) &(\d+)") {
            $currentObjType = $Matches[1]
            $currentObjID = $Matches[2]
            $currentGO = ""
            continue
        }

        if ($line -match "^\s+m_Name: (.+)$") {
            $names[$currentObjID] = $Matches[1]
        }

        if ($line -match "^\s+m_GameObject: \{fileID: (\d+)\}") {
            $currentGO = $Matches[1]
            $transformToGO[$currentObjID] = $currentGO
        }

        # Transform/RectTransform parent
        if ($line -match "^\s+m_Father: \{fileID: (\d+)\}") {
            $parents[$currentObjID] = $Matches[1]
        }

        # GameObject -> components
        if ($line -match "^\s+- component: \{fileID: (\d+)\}") {
            $compID = $Matches[1]
            if (-not $goComponents.ContainsKey($currentObjID)) {
                $goComponents[$currentObjID] = @()
            }
            $goComponents[$currentObjID] += $compID
        }

        # Map GO -> its transform (type 4=Transform, 224=RectTransform)
        if (($currentObjType -eq "4" -or $currentObjType -eq "224") -and $line -match "^\s+m_GameObject: \{fileID: (\d+)\}") {
            $goToTransform[$Matches[1]] = $currentObjID
        }
    }

    # Build hierarchy path for a given Transform fileID
    function Get-HierarchyPath($transformID) {
        $path = @()
        $tid = $transformID
        $safety = 0
        while ($tid -and $tid -ne "0" -and $safety -lt 50) {
            $goID = $transformToGO[$tid]
            if ($goID -and $names[$goID]) {
                $path = @($names[$goID]) + $path
            } elseif ($names[$tid]) {
                $path = @($names[$tid]) + $path
            }
            $tid = $parents[$tid]
            $safety++
        }
        return ($path -join "/")
    }

    # Second pass: find text components
    $results = @()
    $currentObjType = ""
    $currentObjID = ""
    $inTextComponent = $false
    $textGO = ""
    $textCompType = ""
    $fontGuid = ""
    $fontRef = ""
    $isActive = $true

    for ($i = 0; $i -lt $total; $i++) {
        $line = $lines[$i]

        if ($line -match "^--- !u!(\d+) &(\d+)") {
            # Save previous text component
            if ($inTextComponent -and $textGO) {
                $transformID = $goToTransform[$textGO]
                $hierPath = if ($transformID) { Get-HierarchyPath $transformID } else { "(unknown)" }
                $fontName = if ($fontGuid -and $fontMap.ContainsKey($fontGuid)) { $fontMap[$fontGuid] } elseif ($fontGuid) { "UNKNOWN(guid:$fontGuid)" } else { "NO_FONT_REF" }
                $isBigStem = $bigStemGuids -contains $fontGuid

                $results += [PSCustomObject]@{
                    File = $shortName
                    Hierarchy = $hierPath
                    CompType = $textCompType
                    FontName = $fontName
                    FontGUID = $fontGuid
                    IsBigStem = $isBigStem
                }
            }

            $currentObjType = $Matches[1]
            $currentObjID = $Matches[2]
            $inTextComponent = $false
            $textGO = ""
            $textCompType = ""
            $fontGuid = ""
            continue
        }

        # Detect MonoBehaviour type via m_Script guid
        if ($currentObjType -eq "114" -and $line -match "m_Script: \{fileID: \d+, guid: ([a-f0-9]+)") {
            $scriptGuid = $Matches[1]
            if ($scriptGuid -eq $tmpUGUI) {
                $inTextComponent = $true
                $textCompType = "TextMeshProUGUI"
            } elseif ($scriptGuid -eq $tmpWorld) {
                $inTextComponent = $true
                $textCompType = "TextMeshPro (3D)"
            } elseif ($scriptGuid -eq $uiText) {
                $inTextComponent = $true
                $textCompType = "UI.Text"
            }
        }

        if ($inTextComponent) {
            if ($line -match "^\s+m_GameObject: \{fileID: (\d+)\}") {
                $textGO = $Matches[1]
            }
            # TMP font asset reference
            if ($line -match "m_fontAsset: \{fileID: \d+, guid: ([a-f0-9]+)") {
                $fontGuid = $Matches[1]
            }
            # Legacy UI.Text font reference
            if ($line -match "m_Font: \{fileID: \d+, guid: ([a-f0-9]+)") {
                $fontGuid = $Matches[1]
            }
        }
    }

    # Catch last component
    if ($inTextComponent -and $textGO) {
        $transformID = $goToTransform[$textGO]
        $hierPath = if ($transformID) { Get-HierarchyPath $transformID } else { "(unknown)" }
        $fontName = if ($fontGuid -and $fontMap.ContainsKey($fontGuid)) { $fontMap[$fontGuid] } elseif ($fontGuid) { "UNKNOWN(guid:$fontGuid)" } else { "NO_FONT_REF" }
        $isBigStem = $bigStemGuids -contains $fontGuid

        $results += [PSCustomObject]@{
            File = $shortName
            Hierarchy = $hierPath
            CompType = $textCompType
            FontName = $fontName
            FontGUID = $fontGuid
            IsBigStem = $isBigStem
        }
    }

    return $results
}

$allResults = @()

Write-Output "=== SCANNING SCENES ==="
foreach ($s in $scenes) {
    $full = Join-Path $root $s
    if (Test-Path $full) {
        Write-Output "  Scanning: $s"
        $allResults += Scan-File $s
    } else {
        Write-Output "  SKIP (not found): $s"
    }
}

Write-Output ""
Write-Output "=== SCANNING PREFABS ==="
foreach ($p in $prefabs) {
    $full = Join-Path $root $p
    if (Test-Path $full) {
        Write-Output "  Scanning: $p"
        $allResults += Scan-File $p
    } else {
        Write-Output "  SKIP (not found): $p"
    }
}

Write-Output ""
Write-Output "=========================================="
Write-Output "TOTAL TEXT ELEMENTS FOUND: $($allResults.Count)"
Write-Output "=========================================="

$correct = $allResults | Where-Object { $_.IsBigStem -eq $true }
$incorrect = $allResults | Where-Object { $_.IsBigStem -eq $false }

Write-Output ""
Write-Output "--- CORRECT FONTS (BigStem) ---"
Write-Output "Count: $($correct.Count)"
foreach ($r in $correct) {
    Write-Output "  [$($r.CompType)] $($r.File) -> $($r.Hierarchy) | Font: $($r.FontName)"
}

Write-Output ""
Write-Output "--- INCORRECT/NON-BIGSTEM FONTS ---"
Write-Output "Count: $($incorrect.Count)"
foreach ($r in $incorrect) {
    Write-Output "  [$($r.CompType)] $($r.File) -> $($r.Hierarchy) | Font: $($r.FontName) | GUID: $($r.FontGUID)"
}
