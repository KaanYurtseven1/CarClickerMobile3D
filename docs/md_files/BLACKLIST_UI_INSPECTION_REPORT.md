# BLACKLIST UI INSPECTION REPORT

> **Project:** Car Clicker Mobile 3D  
> **Scene:** `Assets/Scenes/Main.unity`  
> **Prefab:** `Assets/Prefabs/Blacklist/MissionRow.prefab`  
> **Scripts:** `Assets/Scripts/Blacklist/` (11 files)

---

## A — Full UI Hierarchy Map

```
Canvas (Main.unity)
└── Panel_BlackList                          [Image, CanvasGroup]  ← starts hidden (m_IsActive: 0)
    ├── Header                               [Image]
    │   └── BlacklistTitle (1)               [TMP_Text]  "BLACKLIST #6"
    │
    └── ScrollView_Blacklist                 [ScrollRect, Image, Mask]
        └── Viewport                         [Image (Mask), RectMask2D]
            └── Content                      [VerticalLayoutGroup, ContentSizeFitter]
                └── Others (1)               [VerticalLayoutGroup (spacing 30, padding 40/40/30/200)]
                    ├── CarImage             [Image]
                    │   ├── CarName          [TMP_Text]
                    │   └── (child 1524049988)
                    │
                    ├── Missions             [Image, VerticalLayoutGroup (spacing 0)]
                    │   └── (MissionRow instances spawned at runtime)
                    │
                    └── TakeTheCarButton     [Button, Image, CanvasGroup]
                        └── (text child)     [TMP_Text]

RewardPopup (1)                              [CanvasGroup]  ← sibling of the above, inside Panel_BlackList
└── RewardBG                                 [Image]
    └── RewardImage                          [Image]  (popupPanel RectTransform)
        ├── TitleText                        [TMP_Text]
        ├── YouReceivedText                  [TMP_Text]
        ├── RewardText                       [TMP_Text]
        ├── RewardIcon                       [Image]
        └── CollectBtn                       [Button, Image]
            └── (text child)                 [TMP_Text]
```

### MissionRow.prefab (instantiated per mission, 5 per tier)

```
MissionRow                                   [Image]  ← ROOT
├── Image                                    [Image]   (mission icon)
├── MissionDesc                              [TMP_Text]
├── BarBG                                    [Image]
│   └── BarFill                              [Image, Filled]
├── ProgressText                             [TMP_Text]  "43K/50K"
├── BarBGComplete                            [Image]   (hidden by default)
│   └── BarFillFull                          [Image, Filled]
└── CompletedBtn                             [Button, Image]  (hidden by default)
    └── Text (TMP)                           [TMP_Text]  "Button"
```

### Bottom Bar Entry Point

```
BottomBar → Blacklist (tab button)           [Button, Image]
  └── OnClick → BottomBarController.OnTabButtonClicked(3)
```

---

## B — Element-by-Element Sprite & Style Audit

| #   | GameObject                     | Component    | Current State                                                                                                                                                                                              | Severity    |
| --- | ------------------------------ | ------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------- |
| 1   | **MissionRow** (prefab root)   | Image        | `m_Sprite: {fileID: 0}` — **NO SPRITE**, `m_Color: (1, 0, 0, 1)` — **SOLID RED**                                                                                                                           | 🔴 CRITICAL |
| 2   | **MissionRow → Image** (icon)  | Image        | `m_Sprite: {fileID: 0}` — null, white. Set dynamically from `BlacklistMissionDefinition.icon` — if SO field is empty, renders as white square                                                              | 🟡 MEDIUM   |
| 3   | **MissionRow → CompletedBtn**  | Button+Image | `m_Sprite: {fileID: 10905}` — **Unity built-in "Knob"**, `m_Color: (0, 1, 0.004, 1)` — **bright green**. SpriteState: ALL four states `{fileID: 0}` (null). Child text says **"Button"** (raw placeholder) | 🔴 CRITICAL |
| 4   | **MissionRow → BarBGComplete** | Image        | `m_Sprite: {fileID: 0}` — **NO SPRITE**, dark gray `(0.149, 0.149, 0.149)`                                                                                                                                 | 🟠 HIGH     |
| 5   | **Panel_BlackList** (root)     | Image        | `m_Sprite: {fileID: 10907}` — **Unity built-in "Background"**, `m_Color: (0, 0, 0, 0.588)` — semi-transparent black overlay                                                                                | 🟠 HIGH     |
| 6   | **CarImage**                   | Image        | `m_Sprite: {fileID: 0}` — **NO SPRITE**, white. Dynamically set from `BlacklistTierSO.carImage` — if SO has no carImage, shows blank white 960×400 rect                                                    | 🟡 MEDIUM   |
| 7   | **Missions** (container)       | Image        | `m_Sprite: {fileID: 0}` — **NO SPRITE**, white `(1, 1, 1, 1)`. Size 960×800 — **plain white background**                                                                                                   | 🟠 HIGH     |
| 8   | **TakeTheCarButton**           | Button+Image | `m_Sprite: {fileID: 10905}` — **Unity built-in "Knob"**, `m_Color: (0, 1, 0.027, 1)` — **bright green**. SpriteState: ALL four states `{fileID: 0}` (null). CanvasGroup alpha 0.4 when disabled            | 🔴 CRITICAL |
| 9   | **RewardImage** (popup frame)  | Image        | `m_Sprite: {fileID: 0}` — **NO SPRITE**, white. Size 800×700. This is the main popup card/frame — **completely blank**                                                                                     | 🔴 CRITICAL |
| 10  | **RewardIcon**                 | Image        | `m_Sprite: {fileID: 0}` — **NO SPRITE**, `m_Color: (1, 0, 0, 1)` — **SOLID RED**. Size 632×376. Dynamically set from `reward.rewardIcon` — if missing, shows huge red rect                                 | 🔴 CRITICAL |
| 11  | **RewardBG** (popup overlay)   | Image        | `m_Sprite: {fileID: 10907}` — **Unity built-in "Background"**, `m_Color: (0, 0, 0, 0.698)` — semi-transparent black                                                                                        | 🟠 HIGH     |
| 12  | **CollectBtn**                 | Button+Image | `m_Sprite: {fileID: 10905}` — **Unity built-in "Knob"**, white. SpriteState: ALL four states `{fileID: 0}` (null)                                                                                          | 🔴 CRITICAL |
| 13  | **BlacklistTitle (1)**         | TMP_Text     | `m_text: "BLACKLIST #6"`, font size in 200×50 area, black color. No background panel, no outline, no glow — **raw text on header**                                                                         | 🟡 MEDIUM   |
| 14  | **Header**                     | Image        | Has sprite assigned (`guid: 2fc45cbfc4efd85469115d3affd54045`) — ✅ OK                                                                                                                                     | ✅ OK       |
| 15  | **BarBG**                      | Image        | Has sprite assigned + dark gray color — ✅ OK                                                                                                                                                              | ✅ OK       |
| 16  | **BarFill**                    | Image        | Has sprite assigned, filled horizontal — ✅ OK                                                                                                                                                             | ✅ OK       |
| 17  | **BarFillFull**                | Image        | Has sprite assigned, filled — ✅ OK                                                                                                                                                                        | ✅ OK       |
| 18  | **Blacklist tab button**       | Button+Image | Has sprite assigned (`guid: 5dfe10cd6130890448f2cfa977eb2547`). SpriteState: ALL four states `{fileID: 0}` (null)                                                                                          | 🟡 MEDIUM   |

---

## C — Script ↔ UI Binding Cross-Reference

### BlacklistPanelController.cs → Scene

| Serialized Field        | Type                  | Bound To (Scene Object)      | Status                                                           |
| ----------------------- | --------------------- | ---------------------------- | ---------------------------------------------------------------- |
| `blacklistTitle`        | TMP_Text              | BlacklistTitle (1)           | ✅ Bound                                                         |
| `carImage`              | Image                 | CarImage                     | ✅ Bound (sprite set at runtime from `BlacklistTierSO.carImage`) |
| `carName`               | TMP_Text              | CarName (child of CarImage)  | ✅ Bound                                                         |
| `missionsContainer`     | Transform             | Missions                     | ✅ Bound                                                         |
| `missionRowPrefab`      | GameObject            | MissionRow.prefab            | ✅ Bound                                                         |
| `takeTheCarButton`      | Button                | TakeTheCarButton             | ✅ Bound                                                         |
| `takeTheCarCanvasGroup` | CanvasGroup           | TakeTheCarButton CanvasGroup | ✅ Bound                                                         |
| `takeTheCarImage`       | Image                 | TakeTheCarButton Image       | ✅ Bound                                                         |
| `rewardPopup`           | RewardPopupController | RewardPopup (1)              | ✅ Bound                                                         |

### MissionRowUI.cs → Prefab

| Serialized Field    | Type       | Bound To (Prefab Object)                 | Status                                                                  |
| ------------------- | ---------- | ---------------------------------------- | ----------------------------------------------------------------------- |
| `icon`              | Image      | MissionRow → Image                       | ✅ Bound (sprite set at runtime from `BlacklistMissionDefinition.icon`) |
| `missionDesc`       | TMP_Text   | MissionRow → MissionDesc                 | ✅ Bound                                                                |
| `barBG`             | GameObject | MissionRow → BarBG                       | ✅ Bound                                                                |
| `barFill`           | Image      | MissionRow → BarBG → BarFill             | ✅ Bound                                                                |
| `progressText`      | TMP_Text   | MissionRow → ProgressText                | ✅ Bound                                                                |
| `barBGComplete`     | GameObject | MissionRow → BarBGComplete               | ✅ Bound                                                                |
| `barFillFull`       | Image      | MissionRow → BarBGComplete → BarFillFull | ✅ Bound                                                                |
| `barBGCompleteText` | TMP_Text   | _(see note below)_                       | ⚠️ Field exists in code, no dedicated child found                       |
| `completedBtn`      | GameObject | MissionRow → CompletedBtn                | ✅ Bound                                                                |
| `completedBtnText`  | TMP_Text   | MissionRow → CompletedBtn → Text (TMP)   | ✅ Bound                                                                |

### RewardPopupController.cs → Scene

| Serialized Field   | Type          | Bound To (Scene Object)     | Status                                                    |
| ------------------ | ------------- | --------------------------- | --------------------------------------------------------- |
| `popupRoot`        | GameObject    | RewardPopup (1)             | ✅ Bound                                                  |
| `rewardIcon`       | Image         | RewardIcon                  | ✅ Bound (sprite set at runtime from `reward.rewardIcon`) |
| `titleText`        | TMP_Text      | TitleText                   | ✅ Bound                                                  |
| `youReceivedText`  | TMP_Text      | YouReceivedText             | ✅ Bound                                                  |
| `rewardText`       | TMP_Text      | RewardText                  | ✅ Bound                                                  |
| `collectButton`    | Button        | CollectBtn                  | ✅ Bound                                                  |
| `popupCanvasGroup` | CanvasGroup   | RewardPopup (1) CanvasGroup | ✅ Bound                                                  |
| `popupPanel`       | RectTransform | RewardImage                 | ✅ Bound                                                  |

### Dynamic Sprite Sources

These images receive their sprites at runtime from ScriptableObject data:

| Image                     | Runtime Source                         | Fallback if SO Field Empty                     |
| ------------------------- | -------------------------------------- | ---------------------------------------------- |
| CarImage                  | `BlacklistTierSO.carImage`             | **White 960×400 rect** (no null-check in code) |
| MissionRow → Image (icon) | `BlacklistMissionDefinition.icon`      | **White 100×100 square**                       |
| RewardIcon                | `BlacklistRewardDefinition.rewardIcon` | **Red 632×376 rect**                           |

---

## D — Missing / Incomplete Elements

### Missing UI Elements (not present in hierarchy at all)

| Element                                                       | Impact                                                              | Notes                                                                                    |
| ------------------------------------------------------------- | ------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| **Close / X button** on Panel_BlackList                       | No way to dismiss panel except re-tapping bottom bar                | Should have a close button in top-right corner                                           |
| **Tier progress indicator** (e.g., "Tier 3 of 6" or pip dots) | Player cannot see overall campaign progress at a glance             | Code tracks `currentTierIndex` — just needs a UI element                                 |
| **Lock / chain visuals** for future tiers                     | No sense of locked content ahead                                    | Tiers auto-advance; some teaser visuals would help                                       |
| **Reward preview** on mission rows                            | Player doesn't know what reward they'll get until claiming          | `BlacklistMissionDefinition` has `reward` data with `rewardDisplayText` and `rewardIcon` |
| **Section divider** between car area and missions             | CarImage and Missions stack directly with no visual separation      | The VerticalLayoutGroup `Others (1)` has spacing 30, but no decorative divider           |
| **CarName background/badge**                                  | Car name text floats on top of car image with no contrast treatment | TMP_Text directly on Image with no shadow/outline/backing panel                          |

### Layout Issues

| Issue                             | Location                    | Details                                                                                                                                                                                      |
| --------------------------------- | --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Missions VLG spacing = 0**      | Missions container          | `m_Spacing: 0` — mission rows stack with zero gap. The parent `Others (1)` has spacing 30, but between _its_ children (CarImage, Missions, Button), not between mission rows inside Missions |
| **BlacklistTitle area too small** | BlacklistTitle (1): 200×50  | For a heading like "BLACKLIST #6", this is very cramped; on different tier names it may clip                                                                                                 |
| **Content bottom padding = 200**  | Content VerticalLayoutGroup | 200px bottom padding is very large — might be intentional for scroll overrun, but verify                                                                                                     |
| **MissionRow root size 900×134**  | MissionRow.prefab           | Width 900 inside a 960 container — 60px asymmetric gap (no horizontal centering anchor)                                                                                                      |

### Text Styling Issues

| Text Object               | Current Style                          | Issue                                                            |
| ------------------------- | -------------------------------------- | ---------------------------------------------------------------- |
| CompletedBtn → Text (TMP) | `m_text: "Button"`, font 24, dark gray | **Placeholder text** — should say "CLAIM" or similar             |
| BlacklistTitle (1)        | Black text, no outline/shadow/glow     | Flat text on header — lacks visual weight for a panel title      |
| ProgressText              | White text, anchored top-right         | Appears to overlap with BarBG area — verify vertical positioning |

---

## E — ScriptableObject Art Dependency Check

### BlacklistTierSO Assets (6 tiers)

Located at `Assets/Prefabs/Blacklist/`:

- `BlacklistTier_1.asset`
- `BlacklistTier_2.asset`
- `BlacklistTier_3.asset`
- `BlacklistTier_4.asset`
- `BlacklistTier_5.asset`
- `BlacklistTier_6.asset`

Each `BlacklistTierSO` has these art-dependent fields:

| Field                          | Type   | Used By                               | Critical?                         |
| ------------------------------ | ------ | ------------------------------------- | --------------------------------- |
| `carImage`                     | Sprite | `BlacklistPanelController` → CarImage | YES — blank white rect if empty   |
| `missions[].icon`              | Sprite | `MissionRowUI` → icon Image           | YES — blank white square if empty |
| `missions[].reward.rewardIcon` | Sprite | `RewardPopupController` → RewardIcon  | YES — solid red rect if empty     |

> **⚠️ Must verify in Unity Editor:** Open each of the 6 `BlacklistTier_*.asset` files and confirm that `carImage`, every mission's `icon`, and every mission's `reward.rewardIcon` are assigned. Any null field directly causes a visible placeholder in-game.

### Resources Folder Check

**No Blacklist-specific art found in Resources.** Searched:

- `Assets/Resources/Arts/` — Contains: bank3.png, diamondButtonBG.png, diamondButtonIcon.png, diamondPlusButton.png, GarageButtonPng.png, progress_fill.png
- `Assets/Resources/UI/` — Not found / empty
- `Assets/Prefabs/UI/` — No Blacklist art

Art assets referenced by GUIDs in scene/prefab (confirmed assigned):

- Header background: `guid: 2fc45cbfc4efd85469115d3affd54045`
- BarBG sprite: `guid: f4f9dd77eb0fe934db1b6c33bd100596`
- BarFill sprite: `guid: 4b6e56fc090b4964a8bd837d2cd00f57`
- BarFillFull sprite: `guid: 9d1b568828eb1b04388ac049510163bc`
- Blacklist tab icon: `guid: 5dfe10cd6130890448f2cfa977eb2547`

---

## PRIORITY FIX LIST (Ranked)

| Priority | Location                         | What to Fix                                                                                                                            | Effort |
| :------: | -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- | :----: |
|  **P0**  | MissionRow.prefab root Image     | Replace solid RED + null sprite with proper row background art                                                                         | Small  |
|  **P0**  | CompletedBtn (MissionRow)        | Replace Unity Knob + green with styled "CLAIM" button sprite; fix text from "Button" to "CLAIM"; add SpriteState for pressed/disabled  | Small  |
|  **P0**  | TakeTheCarButton (scene)         | Replace Unity Knob + green with proper CTA button sprite; add SpriteState; style text                                                  | Small  |
|  **P0**  | RewardImage (popup frame)        | Assign popup card/frame background sprite (currently blank white 800×700)                                                              | Small  |
|  **P0**  | RewardIcon                       | Change placeholder red color to white `(1,1,1,1)` so dynamic sprite renders correctly; verify all SO `rewardIcon` fields are populated | Small  |
|  **P0**  | CollectBtn (popup)               | Replace Unity Knob with styled button sprite; add SpriteState                                                                          | Small  |
|  **P1**  | Panel_BlackList background       | Replace Unity built-in "Background" sprite with custom dark overlay or panel art                                                       | Small  |
|  **P1**  | RewardBG                         | Replace Unity built-in "Background" sprite with custom overlay                                                                         | Small  |
|  **P1**  | Missions container               | Replace null sprite + white color with either (a) transparent or (b) styled panel background                                           | Small  |
|  **P1**  | Missions VLG spacing             | Change `m_Spacing` from 0 to ~10-20 so rows don't stack flush                                                                          |  Tiny  |
|  **P1**  | BarBGComplete                    | Assign sprite (currently null, dark gray placeholder)                                                                                  | Small  |
|  **P2**  | CarImage fallback                | Add null-check in `BlacklistPanelController.BuildUI()` for missing `carImage` sprite                                                   | Small  |
|  **P2**  | BlacklistTitle styling           | Add TMP outline/shadow or place on a sub-panel for visual weight                                                                       | Small  |
|  **P2**  | Blacklist tab button SpriteState | Add Highlighted/Pressed/Selected/Disabled sprites                                                                                      | Small  |
|  **P2**  | Add Close/X button               | Create close button on Panel_BlackList (top-right corner)                                                                              | Medium |
|  **P3**  | Add tier progress indicator      | Show "Tier X / 6" or dot pips above or below title                                                                                     | Medium |
|  **P3**  | Add reward preview on rows       | Show small reward icon/text on each mission row                                                                                        | Medium |
|  **P3**  | Add section divider              | Visual separator between CarImage area and Missions                                                                                    | Small  |
|  **P3**  | CarName contrast                 | Add shadow/outline/backing panel to CarName TMP                                                                                        |  Tiny  |

---

## UNITY EDITOR STEP-BY-STEP CHECKLIST

Use this checklist to verify and fix each item directly in the Unity Editor.

### Phase 1 — Critical Placeholders (P0)

- [ ] **MissionRow.prefab → MissionRow (root)**
  1. Open `Assets/Prefabs/Blacklist/MissionRow.prefab`
  2. Select root `MissionRow` object
  3. Image component → Sprite: assign row background art (e.g., rounded rect 9-slice)
  4. Image component → Color: set to `(1, 1, 1, 1)` white (let sprite define appearance)
  5. Save prefab

- [ ] **MissionRow.prefab → CompletedBtn**
  1. Select `CompletedBtn` child
  2. Image → Sprite: assign styled button sprite (green CTA or themed)
  3. Image → Color: set to white `(1, 1, 1, 1)`
  4. Button → SpriteState: assign Highlighted, Pressed, Disabled sprites
  5. Select `CompletedBtn → Text (TMP)` child
  6. Change `text` from `"Button"` to `"CLAIM"`
  7. Style font: increase size, add outline, set color to white
  8. Save prefab

- [ ] **Main.unity → TakeTheCarButton**
  1. In Hierarchy: `Panel_BlackList → ScrollView_Blacklist → … → TakeTheCarButton`
  2. Image → Sprite: assign prominent CTA button sprite
  3. Image → Color: white `(1, 1, 1, 1)`
  4. Button → SpriteState: assign all four state sprites
  5. Select child text: style appropriately ("TAKE THE CAR" or localized text)

- [ ] **Main.unity → RewardPopup → RewardBG → RewardImage**
  1. Select `RewardImage` (the 800×700 popup frame)
  2. Image → Sprite: assign popup card/frame art (9-slice recommended)
  3. Image → Color: white `(1, 1, 1, 1)`

- [ ] **Main.unity → RewardPopup → RewardBG → RewardImage → RewardIcon**
  1. Select `RewardIcon`
  2. Image → Color: change from `(1, 0, 0, 1)` RED to `(1, 1, 1, 1)` WHITE
  3. (Sprite is set dynamically; the red tint will corrupt the runtime sprite)

- [ ] **Main.unity → RewardPopup → RewardBG → RewardImage → CollectBtn**
  1. Select `CollectBtn`
  2. Image → Sprite: assign styled button sprite
  3. Image → Color: ensure white or appropriate tint
  4. Button → SpriteState: assign all four state sprites
  5. Style child text as needed

### Phase 2 — Visual Polish (P1)

- [ ] **Main.unity → Panel_BlackList** (root)
  1. Image → Sprite: replace built-in "Background" with custom dark overlay or panel background
  2. Alternatively: set sprite to None and keep color as dark overlay (if design intends simple overlay)

- [ ] **Main.unity → RewardPopup → RewardBG**
  1. Image → Sprite: replace built-in "Background" with custom overlay or leave as intentional dim

- [ ] **Main.unity → Missions container**
  1. Select `Missions` (`Panel_BlackList → … → Others (1) → Missions`)
  2. Image → Color: change from white `(1,1,1,1)` to transparent `(1,1,1,0)` OR assign a styled panel sprite
  3. VerticalLayoutGroup → Spacing: change from `0` to `10-20` pixels

- [ ] **MissionRow.prefab → BarBGComplete**
  1. Image → Sprite: assign same or similar sprite as BarBG (currently BarBG has a sprite, BarBGComplete does not)

### Phase 3 — Nice-to-Have (P2–P3)

- [ ] **Verify all 6 BlacklistTier SOs**
  1. Open each `Assets/Prefabs/Blacklist/BlacklistTier_1.asset` through `_6.asset`
  2. Confirm `carImage` sprite is assigned (not None)
  3. For each of 5 missions: confirm `icon` sprite assigned
  4. For each of 5 missions: confirm `reward.rewardIcon` sprite assigned
  5. Any null fields → assign placeholder art or final art

- [ ] **Blacklist tab button SpriteState**
  1. In Hierarchy: `BottomBar → Blacklist`
  2. Button → SpriteState: assign Highlighted, Pressed, Selected, Disabled sprites

- [ ] **Add Close button to Panel_BlackList**
  1. Create UI Button as child of Panel_BlackList (or Header)
  2. Position top-right, assign X icon sprite
  3. Wire OnClick to hide Panel_BlackList

- [ ] **Add tier progress indicator**
  1. Add TMP_Text or Image pips as child of Header
  2. Bind to `BlacklistManager.Instance.currentTierIndex` in `BlacklistPanelController`

- [ ] **BlacklistTitle styling**
  1. Select `BlacklistTitle (1)` TMP
  2. Consider: increase RectTransform width, add outline (Material Preset or TMP settings), or add a sub-panel behind it

- [ ] **CarName contrast**
  1. Select `CarImage → CarName`
  2. Add TMP outline or drop shadow for readability over car image

---

## Summary Statistics

| Category                                                        | Count                        |
| --------------------------------------------------------------- | ---------------------------- |
| Total UI elements audited                                       | 18                           |
| ✅ OK (proper sprite assigned)                                  | 5                            |
| 🔴 CRITICAL (null sprite or Unity built-in + placeholder color) | 6                            |
| 🟠 HIGH (Unity built-in sprite or null sprite, cosmetic impact) | 4                            |
| 🟡 MEDIUM (dynamic sprite with no fallback, or minor styling)   | 3                            |
| Missing UI elements (not in hierarchy at all)                   | 6                            |
| Layout issues found                                             | 4                            |
| Text styling issues found                                       | 3                            |
| ScriptableObject fields requiring art verification              | 36 (6 tiers × 6 fields each) |
