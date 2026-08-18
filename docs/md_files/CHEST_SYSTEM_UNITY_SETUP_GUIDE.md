# CHEST SYSTEM — COMPLETE UNITY EDITOR SETUP GUIDE

> This guide covers **every** inspector field, prefab, hierarchy object, button wire,
> DontDestroyOnLoad requirement, and scene dependency introduced or changed by the
> chest-system refactor. Follow it section by section; the **Recommended Setup Order**
> at the end tells you the optimal sequence.

---

## TABLE OF CONTENTS

1. [Scripts That Need Unity Setup vs Scripts That Don't](#1-scripts-that-need-unity-setup-vs-scripts-that-dont)
2. [Main Scene — Full Hierarchy Map](#2-main-scene--full-hierarchy-map)
3. [ChestShownPlace (ChestShownUI) Setup](#3-chestshownplace-chestshownui-setup)
4. [Chest Shown Slot Prefab (ChestSlotUI) Setup](#4-chest-shown-slot-prefab-chestslotui-setup)
5. [Chest Popup (ChestPopupController) Setup](#5-chest-popup-chestpopupcontroller-setup)
6. [Button Wiring — Popup & Slots](#6-button-wiring--popup--slots)
7. [ChestOpenScene — Full Hierarchy Map](#7-chestopenscene--full-hierarchy-map)
8. [ChestOpenSceneController — Inspector Fields](#8-chestopenscenecontroller--inspector-fields)
9. [ChestRewardRevealController — Inspector Fields](#9-chestrewardrevealcontroller--inspector-fields)
10. [Chest Prefab Assignment (World + Scene)](#10-chest-prefab-assignment-world--scene)
11. [DontDestroyOnLoad / Singleton / Root Requirements](#11-dontdestroyonload--singleton--root-requirements)
12. [Tags / Layers / Colliders / Prefabs Checklist](#12-tags--layers--colliders--prefabs-checklist)
13. [Full Inspector Checklist — Every SerializedField](#13-full-inspector-checklist--every-serializedfield)
14. [Common Setup Mistakes and Symptoms](#14-common-setup-mistakes-and-symptoms)
15. [Recommended Setup Order](#15-recommended-setup-order)

---

## 1. Scripts That Need Unity Setup vs Scripts That Don't

### Scripts that NEED manual Inspector / Hierarchy setup

| Script                          | Lives In                       | Attach To                   | Has SerializedFields?                  |
| ------------------------------- | ------------------------------ | --------------------------- | -------------------------------------- |
| **ChestShownUI**                | Main scene                     | `ChestShownPlace` UI object | YES — 3 fields                         |
| **ChestSlotUI**                 | Prefab                         | `ChestSlotPrefab`           | YES — 6 fields                         |
| **ChestPopupController**        | Main scene                     | `ChestPopup` UI panel       | YES — 10 fields                        |
| **ChestSpawner**                | Main scene                     | Existing `ChestSpawner` GO  | YES — 3+ fields                        |
| **Chest**                       | World prefab(s)                | Each chest prefab root      | YES — 2 fields                         |
| **ChestOpenSceneController**    | ChestOpenScene                 | Scene controller GO         | YES — 15+ fields                       |
| **ChestRewardRevealController** | ChestOpenScene                 | `RewardRevealRoot` GO       | YES — 20+ fields                       |
| **ChestInventoryManager**       | Main scene (DontDestroyOnLoad) | Empty root GO               | NO serialized refs (only debug toggle) |
| **ChestSessionManager**         | Main scene (DontDestroyOnLoad) | Empty root GO               | NO serialized refs                     |
| **FreeChestRewardHandler**      | Main scene (DontDestroyOnLoad) | Empty root GO               | NO serialized refs                     |

### Scripts that need NO Unity setup at all (pure C#)

| Script                     | Reason                                                                                |
| -------------------------- | ------------------------------------------------------------------------------------- |
| **ChestTypeDefs.cs**       | Enums (`ChestType`, `ChestState`) + static class `ChestTypeConfig` — no MonoBehaviour |
| **ChestOpeningSession.cs** | Pure serializable data class — no MonoBehaviour                                       |
| **AdProvider.cs**          | Static utility class — no MonoBehaviour                                               |
| **StickerRewardHelper.cs** | Static utility class — loads `Resources/GarageDatabase` at runtime                    |

> **StickerRewardHelper** has ONE implicit dependency: there must be a `GarageDatabaseSO`
> asset at the path `Assets/Resources/GarageDatabase.asset`. If this asset does not
> exist, sticker rewards will silently fail (the helper returns `null`). You do NOT need
> to wire anything in the inspector — it uses `Resources.Load<GarageDatabaseSO>("GarageDatabase")`.

---

## 2. Main Scene — Full Hierarchy Map

Below is the required hierarchy in your **Main** scene. Items marked **[NEW]** must be
created; items marked **[EXISTING]** should already be there.

```
Main Scene
│
├── Canvas (your main UI canvas) [EXISTING]
│   ├── ChestShownPlace [EXISTING or NEW]
│   │   ├── (ChestShownUI component attached)
│   │   ├── CanvasGroup (component — auto-created if missing, but add one for clarity)
│   │   └── VerticalLayoutGroup (recommended for slot auto-layout)
│   │
│   └── ChestPopup [EXISTING or NEW]
│       ├── (ChestPopupController component attached)
│       ├── PopupRoot [child GO — the actual panel that is SetActive toggled]
│       │   ├── TitleText (TextMeshProUGUI)
│       │   ├── TimerTextObj (GO with TextMeshProUGUI child "TimerText")
│       │   ├── OpenGetRewardText (GO — label "Open & Get Your Reward!")
│       │   ├── StartUnlockButton (GO with Button component)
│       │   ├── HalfTimeButton (GO with Button + CanvasGroup components)
│       │   ├── OpenNowButton (GO with Button + child TextMeshProUGUI for cost)
│       │   └── OpenButton (GO with Button component)
│       └── (optional: dim overlay behind PopupRoot)
│
├── ChestSpawner [EXISTING]
│   └── (ChestSpawner component — update prefab refs)
│       ├── SpawnTop (Transform child or reference)
│       └── SpawnBottom (Transform child or reference)
│
├── ChestInventoryManager [NEW — empty root GO]
│   └── (ChestInventoryManager component)
│
├── ChestSessionManager [NEW — empty root GO]
│   └── (ChestSessionManager component)
│
└── FreeChestRewardHandler [NEW — empty root GO]
    └── (FreeChestRewardHandler component)
```

> **CRITICAL**: `ChestInventoryManager`, `ChestSessionManager`, and `FreeChestRewardHandler`
> must be **ROOT** GameObjects (no parent). They call `DontDestroyOnLoad(gameObject)` in
> `Awake()`, and Unity requires DontDestroyOnLoad objects to be root-level.

---

## 3. ChestShownPlace (ChestShownUI) Setup

### 3.1 Find or Create the GameObject

1. In the **Main** scene Hierarchy, look for your existing chest-display panel (it was
   previously called `ChestShownPlace` or similar).
2. If it doesn't exist, create one:
   - Right-click Canvas → **UI > Empty** → rename to `ChestShownPlace`.
   - Add a **VerticalLayoutGroup** component (recommended but optional — controls how
     slot prefabs stack). Set spacing, padding, child alignment as desired.

### 3.2 Attach ChestShownUI Component

1. Select `ChestShownPlace` in Hierarchy.
2. **Add Component** → search for `ChestShownUI` → attach.

### 3.3 Wire Inspector Fields

| Field              | Type          | What to Drag                                                                                    | Notes                                                                                                                                |
| ------------------ | ------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| **Slot Container** | `Transform`   | Drag `ChestShownPlace` itself (or a child `Transform` if you want slots inside a sub-container) | This is the parent transform where `ChestSlotUI` prefabs are instantiated as children.                                               |
| **Slot Prefab**    | `GameObject`  | Drag your `ChestSlotPrefab` from the **Project** window (see Section 4 to create it)            | Must have `ChestSlotUI` component on its root.                                                                                       |
| **Canvas Group**   | `CanvasGroup` | Drag the `CanvasGroup` component on `ChestShownPlace`                                           | If left empty (`None`), the script auto-adds one at runtime. Better to add it manually so you can tweak alpha/interactable defaults. |

### 3.4 Additional Component Setup

| Component                          | Setting                                                | Why                            |
| ---------------------------------- | ------------------------------------------------------ | ------------------------------ |
| **CanvasGroup**                    | Alpha = 1, Interactable = true, Blocks Raycasts = true | Police chase fading uses this. |
| **VerticalLayoutGroup** (optional) | Child Alignment = Upper Center, Spacing = 8-12         | Slots stack top-to-bottom.     |
| **Content Size Fitter** (optional) | Vertical Fit = Preferred Size                          | Auto-shrinks when fewer slots. |

---

## 4. Chest Shown Slot Prefab (ChestSlotUI) Setup

This prefab represents ONE chest slot in the side list. `ChestShownUI` instantiates one
per chest in the inventory.

### 4.1 Create the Prefab

1. In Main scene, right-click Canvas → **UI > Empty** → rename to `ChestSlotPrefab`.
2. Build its internal layout (example hierarchy):

```
ChestSlotPrefab (root — has ChestSlotUI + Button)
├── ChestIcon (UI > Image)
└── StatusText (UI > TextMeshPro - Text (UI))
```

3. **Add Component** to root → `ChestSlotUI`.
4. **Add Component** to root → `Button` (Unity UI Button).
5. Drag to Project window to save as a **Prefab**.
6. **Delete** the instance from the scene (it's instantiated at runtime by ChestShownUI).

### 4.2 Wire Inspector Fields (on the Prefab)

Open the prefab (double-click in Project window) and wire:

| Field              | Type       | What to Drag                                       | Notes                                                                                                                      |
| ------------------ | ---------- | -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| **Chest Icon**     | `Image`    | Drag the `ChestIcon` Image child                   | Displays the chest type icon.                                                                                              |
| **Status Text**    | `TMP_Text` | Drag the `StatusText` TMP component                | Shows "New" / countdown / "Ready!"                                                                                         |
| **Slot Button**    | `Button`   | Drag the `Button` component on the root            | Click listeners are added by code (`slotButton.onClick.AddListener`). **Do NOT add any OnClick entries in the inspector.** |
| **Common Icon**    | `Sprite`   | Drag your Common chest icon sprite from Project    | Shown when `chestType == Common`.                                                                                          |
| **Rare Icon**      | `Sprite`   | Drag your Rare chest icon sprite from Project      | Shown when `chestType == Rare`. Falls back to Common if left empty.                                                        |
| **Legendary Icon** | `Sprite`   | Drag your Legendary chest icon sprite from Project | Shown when `chestType == Legendary`. Falls back to Common if left empty.                                                   |

### 4.3 Visual Design Tips

- Recommended root size: 200×60 to 280×80 (adjust to your UI scale).
- `ChestIcon` on the left, `StatusText` on the right.
- `Button` navigation mode: **None** (to prevent joystick/keyboard issues on mobile).
- The prefab does NOT need a `CanvasGroup` — fading is done on the parent `ChestShownPlace`.

---

## 5. Chest Popup (ChestPopupController) Setup

### 5.1 Find or Create the Popup

1. In **Main** scene, look for your existing chest popup panel.
2. If it doesn't exist, create one under Canvas → **UI > Empty** → rename to `ChestPopup`.
3. Create a child `PopupRoot` — this is the panel that gets `SetActive(true/false)`.

### 5.2 Attach ChestPopupController

1. Select `ChestPopup` in Hierarchy.
2. **Add Component** → `ChestPopupController`.

### 5.3 Build the Popup Hierarchy

```
ChestPopup (root — ChestPopupController component here)
└── PopupRoot (the panel — SetActive toggled)
    ├── Background / DimOverlay (optional)
    ├── TitleText (TextMeshProUGUI — "Common Chest" / "Rare Chest" / etc.)
    ├── TimerTextObj (empty GO — shown only during Unlocking state)
    │   └── TimerText (TextMeshProUGUI — "12:34")
    ├── OpenGetRewardText (GO — shows "Open & Get Your Reward!")
    ├── OpenNowButton (GO with Button — "Open Now" + cost label)
    │   └── OpenNowCostText (TextMeshProUGUI — "15" / "50" / "100")
    ├── StartUnlockButton (GO with Button — "Start Unlock")
    ├── HalfTimeButton (GO with Button + CanvasGroup — "Watch Ad / Half Time")
    └── OpenButton (GO with Button — "Open" — shown when timer done)
```

### 5.4 Wire Inspector Fields

Select `ChestPopup` and fill every slot:

| Field                        | Type              | What to Drag                                    | Notes                                                                                                                                                                                                                |
| ---------------------------- | ----------------- | ----------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Popup Root**               | `GameObject`      | `PopupRoot` child                               | Entire panel toggled via `SetActive`.                                                                                                                                                                                |
| **Title Text**               | `TextMeshProUGUI` | `TitleText` TMP component                       | Auto-filled with "Common Chest" / "Rare Chest" / "Legendary Chest".                                                                                                                                                  |
| **Timer Text Obj**           | `GameObject`      | `TimerTextObj` GO                               | Parent shown/hidden as a unit.                                                                                                                                                                                       |
| **Timer Text**               | `TextMeshProUGUI` | `TimerText` TMP component inside `TimerTextObj` | Shows countdown "MM:SS".                                                                                                                                                                                             |
| **Open Get Reward Text Obj** | `GameObject`      | `OpenGetRewardText` GO                          | "Open & Get Your Reward" label. Shown in Idle + ReadyToOpen states.                                                                                                                                                  |
| **Open Now Obj**             | `GameObject`      | `OpenNowButton` GO                              | Shown in Idle + Unlocking states.                                                                                                                                                                                    |
| **Open Now Cost Text**       | `TextMeshProUGUI` | `OpenNowCostText` TMP inside `OpenNowButton`    | Auto-filled: 15 (Common), 50 (Rare), 100 (Legendary).                                                                                                                                                                |
| **Start Unlock Obj**         | `GameObject`      | `StartUnlockButton` GO                          | Shown only in Idle state.                                                                                                                                                                                            |
| **Half Time Obj**            | `GameObject`      | `HalfTimeButton` GO                             | Shown only in Unlocking state. **MUST have a `Button` component** (code reads `GetComponent<Button>()` to set `interactable`). Add a `CanvasGroup` too (code uses it for alpha dimming when half-time already used). |
| **Open Obj**                 | `GameObject`      | `OpenButton` GO                                 | Shown only in ReadyToOpen state.                                                                                                                                                                                     |

### 5.5 State Visibility Logic (what shows when)

| Chest State                             | Visible Elements                                               |
| --------------------------------------- | -------------------------------------------------------------- |
| **Idle** (new chest, not yet unlocking) | `openGetRewardTextObj` ✓ — `openNowObj` ✓ — `startUnlockObj` ✓ |
| **Unlocking** (timer running)           | `timerTextObj` ✓ — `halfTimeObj` ✓ — `openNowObj` ✓            |
| **ReadyToOpen** (timer done)            | `openGetRewardTextObj` ✓ — `openObj` ✓                         |

All elements not listed for a state are `SetActive(false)`.

---

## 6. Button Wiring — Popup & Slots

### 6.1 ChestPopupController — Button OnClick Events

The popup has **5 public methods** that must be wired to buttons. For each button, select
it in the Hierarchy, scroll to the **Button > On Click ()** section, and add an entry:

| Button GO                            | Method to Wire         | Object to Drag                                         | Full Signature                                |
| ------------------------------------ | ---------------------- | ------------------------------------------------------ | --------------------------------------------- |
| **StartUnlockButton**                | `OnStartUnlockPressed` | Drag `ChestPopup` (the GO with `ChestPopupController`) | `ChestPopupController.OnStartUnlockPressed()` |
| **HalfTimeButton**                   | `OnHalfTimePressed`    | Drag `ChestPopup`                                      | `ChestPopupController.OnHalfTimePressed()`    |
| **OpenNowButton**                    | `OnOpenNowPressed`     | Drag `ChestPopup`                                      | `ChestPopupController.OnOpenNowPressed()`     |
| **OpenButton**                       | `OnOpenPressed`        | Drag `ChestPopup`                                      | `ChestPopupController.OnOpenPressed()`        |
| **Close/X Button** (if you have one) | `ClosePopup`           | Drag `ChestPopup`                                      | `ChestPopupController.ClosePopup()`           |

#### Step-by-step for each button:

1. Select the button GO (e.g., `StartUnlockButton`) in Hierarchy.
2. In Inspector, find the **Button** component → scroll to **On Click ()**.
3. Click the **+** button to add a new entry.
4. Drag `ChestPopup` (the GO that has `ChestPopupController`) into the **Object** slot.
5. In the dropdown, navigate to: **ChestPopupController** → pick the method (e.g., `OnStartUnlockPressed`).
6. Repeat for all 5 buttons.

### 6.2 ChestSlotUI — Slot Buttons (NO manual wiring needed)

The `ChestSlotUI.Initialize()` method calls `slotButton.onClick.AddListener(...)` in code.
**Do NOT add any OnClick entries in the inspector** for the slot prefab's Button component.
The code handles it automatically.

### 6.3 Dim Overlay / Close Button

If your popup has a dim overlay or background that should close the popup when tapped:

- Add a **Button** component to the overlay.
- Wire On Click → `ChestPopupController.ClosePopup()`.

---

## 7. ChestOpenScene — Full Hierarchy Map

```
ChestOpenScene
│
├── Main Camera [EXISTING]
│
├── ChestOpenController [EXISTING or NEW]
│   └── (ChestOpenSceneController component)
│
├── SpawnPoint [EXISTING or NEW — empty Transform]
│   └── (Position this where the 3D chest should appear)
│
├── CardParkAnchor [EXISTING or NEW — empty Transform]
│   └── (Position where reward cards fly to and park)
│
├── RewardRevealRoot [EXISTING or NEW]
│   └── (ChestRewardRevealController component)
│       ├── WorldTitle (3D TextMeshPro)
│       ├── WorldSubtitle (3D TextMeshPro)
│       ├── WorldValue (3D TextMeshPro)
│       ├── ProgressRoot (GO — inactive by default)
│       │   ├── BarBGParent (Transform with 8 SpriteRenderer children)
│       │   ├── BarFillParent (Transform with 8 SpriteRenderer children)
│       │   ├── ProgressLevelText (3D TextMeshPro)
│       │   └── ProgressCopiesText (3D TextMeshPro)
│       └── SummaryRoot (GO — inactive by default)
│           ├── SummaryTitleText (3D TextMeshPro)
│           └── SummarySlotsContainer (empty Transform — slots generated here at runtime)
│
├── Directional Light [EXISTING]
│
└── (optional: Dim Canvas overlay for fade effects)
```

> **IMPORTANT**: `ProgressRoot` and `SummaryRoot` must start **inactive** (unchecked in
> Hierarchy). The controller activates them at the right time.

---

## 8. ChestOpenSceneController — Inspector Fields

Select the `ChestOpenController` GameObject in ChestOpenScene and fill every field:

### 8.1 References

| Field                      | Type         | What to Drag                                  | Notes                                                                      |
| -------------------------- | ------------ | --------------------------------------------- | -------------------------------------------------------------------------- |
| **Cam**                    | `Camera`     | `Main Camera`                                 | Auto-found via `Camera.main` if left empty. Wire it explicitly for safety. |
| **Spawn Point**            | `Transform`  | `SpawnPoint` empty GO                         | World position where the 3D chest will be instantiated.                    |
| **Chest Prefab**           | `GameObject` | Your legacy/default chest prefab from Project | **Fallback** — used only if per-type prefabs are all null.                 |
| **Common Chest Prefab**    | `GameObject` | Common chest 3D prefab from Project           | **NEW FIELD.** If null, falls back to `chestPrefab`.                       |
| **Rare Chest Prefab**      | `GameObject` | Rare chest 3D prefab from Project             | **NEW FIELD.** If null, falls back to `chestPrefab`.                       |
| **Legendary Chest Prefab** | `GameObject` | Legendary chest 3D prefab from Project        | **NEW FIELD.** If null, falls back to `chestPrefab`.                       |

> **Quick start**: If you only have ONE chest model, drag it into all 4 prefab slots
> (`chestPrefab`, `commonChestPrefab`, `rareChestPrefab`, `legendaryChestPrefab`).
> The system works fine — you can add unique models later.

### 8.2 Lid Settings

| Field                      | Type        | Default               | Notes                                                                                                             |
| -------------------------- | ----------- | --------------------- | ----------------------------------------------------------------------------------------------------------------- |
| **Lid Bone**               | `Transform` | Leave empty initially | Auto-searched in the prefab hierarchy using `lidTransformName` / `lidSearchPath`. Wire only if auto-search fails. |
| **Lid Transform Name**     | `string`    | `"Cube.004"`          | Name of the lid bone in the chest model. Check your FBX.                                                          |
| **Lid Search Path**        | `string`    | `"Empty/Cube.004"`    | Deeper path searched if direct name fails.                                                                        |
| **Lid Closed Local Euler** | `Vector3`   | `(0, 0, 0)`           | The rotation of the lid when closed.                                                                              |
| **Lid Open Local Euler**   | `Vector3`   | `(-110, 0, 0)`        | The rotation of the lid when open. Adjust for your model.                                                         |

### 8.3 Runtime Pivot Fix

| Field                     | Default          | Notes                                          |
| ------------------------- | ---------------- | ---------------------------------------------- |
| **Use Runtime Pivot Fix** | `false`          | Enable only if lid rotates around wrong point. |
| **Pivot Offset**          | `(0, 0.5, -0.5)` | Offset applied to create virtual pivot.        |

### 8.4 Money/Nitro Percentages (LEGACY — now ignored)

| Field                           | Default       | Notes                                                                                                                                                               |
| ------------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Chest Gold Percent Min/Max**  | `0.05 / 0.15` | **These values are NO LONGER USED.** `ChestTypeConfig` drives all reward scaling now (Common=5-15%, Rare=10-25%, Legendary=20-40%). You can leave them at defaults. |
| **Chest Nitro Percent Min/Max** | `0.05 / 0.20` | **Same — no longer used.**                                                                                                                                          |

### 8.5 Reward Reveal

| Field                     | Type                          | What to Drag                                         | Notes                                                                                                                                                             |
| ------------------------- | ----------------------------- | ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Reveal Controller**     | `ChestRewardRevealController` | Drag `RewardRevealRoot` GO (which has the component) | **MANDATORY.** NullRef if missing.                                                                                                                                |
| **World Card Prefab**     | `GameObject`                  | `WorldRewardCardPrefab_TMP` from Project             | **MANDATORY.** The 3D card that flies out of the chest. Must have `WorldRewardCardController` component.                                                          |
| **Park Anchor**           | `Transform`                   | `CardParkAnchor` empty GO                            | Where cards park after flying out. If null, a position is computed automatically (but explicit is better).                                                        |
| **Card Mouth Child Name** | `string`                      | `"CardMouthAnchor"`                                  | Name of the child transform on the chest prefab where cards emerge from. If your chest prefab doesn't have this child, cards will emerge from the chest's center. |

### 8.6 Tap & Animation Settings

| Field                    | Default      | Notes                                       |
| ------------------------ | ------------ | ------------------------------------------- |
| **Taps To Open**         | `3`          | Number of taps before lid opens.            |
| **Chest Layer Mask**     | `Everything` | Raycast filter for tap detection.           |
| **Intro Move Time**      | `0.55`       | Duration of intro drop animation.           |
| **Intro Start Y Offset** | `0.55`       | How high above spawnPoint the chest starts. |
| **Intro Start Scale**    | `0.25`       | Starting scale for intro (scales up to 1).  |
| **Tap Jump Power**       | `0.25`       | How high chest jumps on each tap.           |
| **Tap Jump Duration**    | `0.22`       | Duration of tap hop.                        |
| **Lid Open Duration**    | `0.35`       | Duration of lid-open tween.                 |
| **Lid Ease**             | `OutCubic`   | DOTween ease for lid opening.               |

### 8.7 Debug

| Field          | Default | Notes                                                  |
| -------------- | ------- | ------------------------------------------------------ |
| **Debug Logs** | `false` | Enable for verbose console logging during development. |

---

## 9. ChestRewardRevealController — Inspector Fields

Select `RewardRevealRoot` in **ChestOpenScene** and fill every field:

### 9.1 World-Space Info Texts

| Field              | Type               | What to Drag                                          |
| ------------------ | ------------------ | ----------------------------------------------------- |
| **World Title**    | `TextMeshPro` (3D) | The 3D TMP showing reward name (e.g., "Gold Reward"). |
| **World Subtitle** | `TextMeshPro` (3D) | Subtitle text below title.                            |
| **World Value**    | `TextMeshPro` (3D) | Shows the reward value (e.g., "+$5,000").             |

> These are **3D TextMeshPro** objects (not UGUI). Create via:
> **GameObject > 3D Object > Text - TextMeshPro**.

### 9.2 Card Progress Bar (World-Space)

| Field                    | Type                | What to Drag                                          | Notes                                                                                       |
| ------------------------ | ------------------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| **Progress Root**        | `GameObject`        | Root GO of the progress bar cluster                   | **Must start inactive** (unchecked). Only shown during real card reveal.                    |
| **Bar BG Parent**        | `Transform`         | Parent of 8 SpriteRenderer children (background bars) | If `bgSegments` array is empty, children are auto-resolved from this parent.                |
| **Bar Fill Parent**      | `Transform`         | Parent of 8 SpriteRenderer children (fill bars)       | Same auto-resolve behavior.                                                                 |
| **BG Segments**          | `SpriteRenderer[8]` | The 8 background bar SpriteRenderers                  | **Optional** — auto-resolved from `barBGParent` children if array is empty or wrong length. |
| **Fill Segments**        | `SpriteRenderer[8]` | The 8 fill bar SpriteRenderers, LEFT to RIGHT         | **Optional** — auto-resolved from `barFillParent` children. INDEX 0 = leftmost segment.     |
| **Progress Level Text**  | `TextMeshPro` (3D)  | TMP showing the card level/name on the progress bar.  |
| **Progress Copies Text** | `TextMeshPro` (3D)  | TMP showing "3/8" card copies count.                  |

> **Auto-resolve shortcut**: If you set `barBGParent` and `barFillParent` correctly and
> leave the arrays empty (size 0), the segments are automatically resolved from children
> at Awake. Just make sure the children are ordered left-to-right in the hierarchy.

### 9.3 Summary (World-Space) — Dynamic Prefab System

| Field                       | Type               | What to Drag                                             | Notes                                                                                  |
| --------------------------- | ------------------ | -------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| **Summary Root**            | `GameObject`       | Root GO of summary panel                                 | **Must start inactive.**                                                               |
| **Summary Title Text**      | `TextMeshPro` (3D) | Title text in summary (e.g., "Chest Rewards").           |
| **Summary Slot Prefab**     | `GameObject`       | `SummarySlotPrefab` from Project                         | **NEW.** Must have `SummarySlotUI` component. See below.                               |
| **Summary Slots Container** | `Transform`        | A child of `SummaryRoot` (e.g., `SummarySlotsContainer`) | **NEW.** Slot instances are spawned here. Add `HorizontalLayoutGroup` for auto-layout. |

> **The old `summaryCard1-4` / `summaryOverlay1-4` fields are REMOVED.** The system now
> dynamically generates `SummarySlotPrefab` instances based on actual reward count.
> 3 rewards = 3 slots, 4 rewards = 4 slots.

#### SummarySlotPrefab Setup

1. Create a new empty 3D GameObject, name it `SummarySlotPrefab`.
2. Add a child with a `SpriteRenderer` component (this displays the reward icon).
3. Add `SummarySlotUI` component to the root.
4. Wire the `SpriteRenderer` child into the `Reward Image` field.
5. Save as a prefab in `Assets/Prefabs/`.

#### SummarySlotsContainer Setup

Inside `SummaryRoot`, create an empty child called `SummarySlotsContainer`.
This keeps generated slot instances separate from `SummaryTitleText`.
Slots are instantiated as children of this container at runtime.

### 9.5 Reward Icon Sprites

| Field              | Type     | What to Drag                     | Notes                                                                         |
| ------------------ | -------- | -------------------------------- | ----------------------------------------------------------------------------- |
| **Money Sprite**   | `Sprite` | Your money/coin icon sprite      | Used for summary card 1 icon.                                                 |
| **Nitro Sprite**   | `Sprite` | Your nitro/lightning icon sprite | Used for summary card 2 icon.                                                 |
| **Sticker Sprite** | `Sprite` | Your sticker icon sprite         | **NEW.** Used for summary card 4. Can be left empty if you don't wire slot 4. |

### 9.6 Animation Tuning

| Field                     | Default | Notes                                                |
| ------------------------- | ------- | ---------------------------------------------------- |
| **Crossfade Duration**    | `0.20`  | How fast reward texts crossfade between revelations. |
| **Summary Fade Duration** | `0.30`  | How fast the summary panel fades in.                 |

### 9.7 Debug

| Field          | Default | Notes                               |
| -------------- | ------- | ----------------------------------- |
| **Debug Logs** | `false` | Enable for verbose console logging. |

---

## 10. Chest Prefab Assignment (World + Scene)

You need chest prefabs in **two places**: the Main scene (world spawner) and ChestOpenScene
(opening ceremony).

### 10.1 World Chest Prefabs (Main Scene → ChestSpawner)

These are the 3D chest models that fall from the sky during driving gameplay.

| ChestSpawner Field         | Prefab                         | Required?                                                       |
| -------------------------- | ------------------------------ | --------------------------------------------------------------- |
| **Common Chest Prefab**    | Your common chest 3D prefab    | **YES — mandatory.** Also serves as fallback for missing types. |
| **Rare Chest Prefab**      | Your rare chest 3D prefab      | No — falls back to Common if null.                              |
| **Legendary Chest Prefab** | Your legendary chest 3D prefab | No — falls back to Common if null.                              |

Each world chest prefab **MUST** have:

- **`Chest`** component on the root (`Assets/Scripts/Chest.cs`)
- At least one **`Collider`** (Box/Sphere/Mesh) for tap detection
- At least one **`Renderer`** (MeshRenderer or SkinnedMeshRenderer) for visibility checks
- (Optional) A `TapVanishAnimator` component if you want a collect animation

> The `Chest.chestType` field on the prefab doesn't matter — `ChestSpawner` sets it at
> runtime via `c.chestType = pickedType;`.

### 10.2 Scene Chest Prefabs (ChestOpenScene → ChestOpenSceneController)

These are the 3D chest models used in the opening ceremony scene.

| ChestOpenSceneController Field | Prefab                                | Required?                         |
| ------------------------------ | ------------------------------------- | --------------------------------- |
| **Chest Prefab**               | Legacy/default chest prefab           | Only needed as fallback.          |
| **Common Chest Prefab**        | Same or different model for Common    | No — falls back to `chestPrefab`. |
| **Rare Chest Prefab**          | Same or different model for Rare      | No — falls back to `chestPrefab`. |
| **Legendary Chest Prefab**     | Same or different model for Legendary | No — falls back to `chestPrefab`. |

> **Quick start**: Drag your single chest prefab into all 4 slots in both scenes.
> Visual differentiation can come later.

### 10.3 WorldRewardCardPrefab

The `worldCardPrefab` field on `ChestOpenSceneController` must reference a prefab that has
a `WorldRewardCardController` component. This is the 3D card that flies out of the chest
and parks at `parkAnchor`.

- This prefab should already exist from the original chest system.
- It is typically in **Assets/Prefabs/** or similar.
- Search your Project for `WorldRewardCardPrefab_TMP` if unsure.

---

## 11. DontDestroyOnLoad / Singleton / Root Requirements

### 11.1 DontDestroyOnLoad Managers

These three managers **MUST** be root-level GameObjects in the **Main** scene:

| Manager                    | GO Name (suggested)      | What It Does                                      |
| -------------------------- | ------------------------ | ------------------------------------------------- |
| **ChestInventoryManager**  | `ChestInventoryManager`  | Manages 5-slot chest inventory, timers, save/load |
| **ChestSessionManager**    | `ChestSessionManager`    | Bridges chest data between Main → ChestOpenScene  |
| **FreeChestRewardHandler** | `FreeChestRewardHandler` | Handles blacklist free-chest chains               |

### 11.2 Setup Instructions (for each)

1. **Create** an empty GameObject at the scene root (NOT inside Canvas or any other parent).
2. **Rename** it to the manager name.
3. **Add Component** → attach the script.
4. **Verify**: In the Inspector, Transform should show Position (0,0,0), and the parent
   should be "None" (root object).

### 11.3 Why Root-Level?

Unity's `DontDestroyOnLoad()` only works on root GameObjects. If the object has a parent,
Unity throws a warning and it gets destroyed on scene load anyway.

The scripts contain a safety check:

```csharp
if (transform.parent != null)
    transform.SetParent(null);
DontDestroyOnLoad(gameObject);
```

But it's cleaner to make them root from the start.

### 11.4 EnsureInstance() Bootstrap

`ChestInventoryManager` and `ChestSessionManager` have an `EnsureInstance()` static method
tagged with `[RuntimeInitializeOnLoadMethod]`. This means **if you forget to place them in
the scene**, they will auto-create themselves when first accessed.

However, **do not rely on this** — place them explicitly so you can see them in the Hierarchy
and set the debug toggle.

### 11.5 Re-Entry Protection

All three managers have duplicate-destruction logic in `Awake()`:

```csharp
if (Instance != null && Instance != this) { Destroy(gameObject); return; }
```

This means: if you accidentally have two `ChestInventoryManager` objects (e.g., from a scene
reload), the second one self-destructs. This is correct behavior — not a bug.

---

## 12. Tags / Layers / Colliders / Prefabs Checklist

### 12.1 Tags

No custom tags are required by the chest system. Chests are detected via Collider raycasts,
not `CompareTag()`.

### 12.2 Layers

| Layer                   | Where Used          | Required Action                                                                                                                                                                                                     |
| ----------------------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Default or custom layer | World chest prefabs | The `ChestOpenSceneController.chestLayerMask` field controls which layers are raycast for tap detection. Default is `Everything (~0)`. If your chests are on a specific layer, make sure it's included in the mask. |

### 12.3 Colliders (World Chest Prefabs)

Each world chest prefab (`commonChestPrefab`, `rareChestPrefab`, `legendaryChestPrefab`)
**MUST** have at least one `Collider` component:

- **`BoxCollider`** (recommended, simplest)
- Size it to cover the chest mesh.
- `Is Trigger` can be either true or false — the `Chest.cs` script uses both `OnMouseDown()`
  and raycasting; a trigger collider works for both.

### 12.4 Colliders (ChestOpenScene Chest Prefabs)

The ChestOpenScene prefabs also need colliders for tap detection:

- `ChestOpenSceneController` uses `Physics.Raycast()` with `chestLayerMask`.
- Make sure your chest model has a `Collider` (Box, Sphere, or Mesh).

### 12.5 Required Prefabs Summary

| Prefab                        | Location (suggested)                | Components Required                                          |
| ----------------------------- | ----------------------------------- | ------------------------------------------------------------ |
| **ChestSlotPrefab**           | `Assets/Prefabs/UI/ChestSlotPrefab` | `ChestSlotUI`, `Button`, `Image` (child), `TMP_Text` (child) |
| **Common Chest (world)**      | `Assets/Prefabs/CommonChest`        | `Chest`, `Collider`, `Renderer(s)`                           |
| **Rare Chest (world)**        | `Assets/Prefabs/RareChest`          | `Chest`, `Collider`, `Renderer(s)`                           |
| **Legendary Chest (world)**   | `Assets/Prefabs/LegendaryChest`     | `Chest`, `Collider`, `Renderer(s)`                           |
| **Common Chest (scene)**      | Same or different prefab            | `Collider`, `Renderer(s)` — NO `Chest` component needed      |
| **Rare Chest (scene)**        | Same or different prefab            | Same                                                         |
| **Legendary Chest (scene)**   | Same or different prefab            | Same                                                         |
| **WorldRewardCardPrefab_TMP** | Existing in project                 | `WorldRewardCardController`                                  |

> **You can use the SAME prefab for both world and scene** — just make sure it has both
> a `Chest` component (for world spawning) and a `Collider` (for scene tap detection).

### 12.6 Resources Folder Assets

| Asset Path                              | Type               | Used By                                          |
| --------------------------------------- | ------------------ | ------------------------------------------------ |
| `Assets/Resources/GarageDatabase.asset` | `GarageDatabaseSO` | `StickerRewardHelper.cs` `Resources.Load()` call |

If this asset doesn't exist, sticker rewards silently fail (returns null, no crash).

---

## 13. Full Inspector Checklist — Every SerializedField

Use this as a final validation pass. Go through each script and confirm every field is wired.

### ChestShownUI (Main scene — ChestShownPlace)

- [ ] `slotContainer` → Transform (ChestShownPlace itself or child container)
- [ ] `slotPrefab` → ChestSlotPrefab from Project
- [ ] `canvasGroup` → CanvasGroup on ChestShownPlace (or leave empty for auto-create)

### ChestSlotUI (Prefab — ChestSlotPrefab)

- [ ] `chestIcon` → Image child
- [ ] `statusText` → TMP_Text child
- [ ] `slotButton` → Button on root
- [ ] `commonIcon` → Common chest Sprite
- [ ] `rareIcon` → Rare chest Sprite (or leave empty = fallback to common)
- [ ] `legendaryIcon` → Legendary chest Sprite (or leave empty = fallback to common)

### ChestPopupController (Main scene — ChestPopup)

- [ ] `popupRoot` → PopupRoot child GO
- [ ] `titleText` → TextMeshProUGUI for chest name
- [ ] `timerTextObj` → Timer container GO
- [ ] `timerText` → TextMeshProUGUI for countdown
- [ ] `openGetRewardTextObj` → "Open & Get Reward" label GO
- [ ] `openNowObj` → Open Now button GO
- [ ] `openNowCostText` → TextMeshProUGUI inside Open Now button
- [ ] `startUnlockObj` → Start Unlock button GO
- [ ] `halfTimeObj` → Half Time button GO (needs Button + CanvasGroup components)
- [ ] `openObj` → Open button GO

### ChestSpawner (Main scene)

- [ ] `commonChestPrefab` → Common chest world prefab (MANDATORY)
- [ ] `rareChestPrefab` → Rare chest world prefab (or leave empty = fallback)
- [ ] `legendaryChestPrefab` → Legendary chest world prefab (or leave empty = fallback)
- [ ] `spawnTop` → Top spawn anchor Transform
- [ ] `spawnBottom` → Bottom spawn anchor Transform

### ChestInventoryManager (Main scene — root GO)

- [ ] Exists as root-level GO with component attached
- [ ] `debugLogs` → false (or true for testing)

### ChestSessionManager (Main scene — root GO)

- [ ] Exists as root-level GO with component attached

### FreeChestRewardHandler (Main scene — root GO in Blacklist folder structure)

- [ ] Exists as root-level GO with component attached

### ChestOpenSceneController (ChestOpenScene)

- [ ] `cam` → Main Camera (or leave empty for auto)
- [ ] `spawnPoint` → SpawnPoint Transform
- [ ] `chestPrefab` → Legacy/default chest prefab
- [ ] `commonChestPrefab` → Common scene prefab (NEW)
- [ ] `rareChestPrefab` → Rare scene prefab (NEW)
- [ ] `legendaryChestPrefab` → Legendary scene prefab (NEW)
- [ ] `lidBone` → Leave empty (auto-searched) OR wire explicitly
- [ ] `revealController` → ChestRewardRevealController on RewardRevealRoot (MANDATORY)
- [ ] `worldCardPrefab` → WorldRewardCardPrefab_TMP (MANDATORY)
- [ ] `parkAnchor` → CardParkAnchor Transform (recommended)

### ChestRewardRevealController (ChestOpenScene — RewardRevealRoot)

- [ ] `worldTitle` → 3D TextMeshPro
- [ ] `worldSubtitle` → 3D TextMeshPro
- [ ] `worldValue` → 3D TextMeshPro
- [ ] `progressRoot` → Progress bar root GO (start INACTIVE)
- [ ] `barBGParent` → Parent of 8 BG SpriteRenderers
- [ ] `barFillParent` → Parent of 8 Fill SpriteRenderers
- [ ] `bgSegments[8]` → (optional if barBGParent is set)
- [ ] `fillSegments[8]` → (optional if barFillParent is set)
- [ ] `progressLevelText` → 3D TMP
- [ ] `progressCopiesText` → 3D TMP
- [ ] `summaryRoot` → Summary root GO (start INACTIVE)
- [ ] `summaryTitleText` → 3D TMP
- [ ] `summarySlotPrefab` → SummarySlotPrefab with SummarySlotUI component (NEW)
- [ ] `summarySlotsContainer` → Child Transform inside SummaryRoot for generated slots (NEW)
- [ ] `moneySprite` → Money icon Sprite
- [ ] `nitroSprite` → Nitro icon Sprite
- [ ] `stickerSprite` → Sticker icon Sprite (OPTIONAL)

---

## 14. Common Setup Mistakes and Symptoms

### Mistake 1: DontDestroyOnLoad manager is a child object

- **Symptom**: Manager disappears after scene change. Chest inventory resets. Console shows
  "DontDestroyOnLoad only works for root GameObjects" warning.
- **Fix**: Make `ChestInventoryManager`, `ChestSessionManager`, and `FreeChestRewardHandler`
  root-level GameObjects (no parent in Hierarchy).

### Mistake 2: `popupRoot` not assigned on ChestPopupController

- **Symptom**: Tapping a chest slot does nothing visible. No errors in console (code
  null-checks `popupRoot`).
- **Fix**: Drag the correct child panel into the `Popup Root` field.

### Mistake 3: Button GOs missing Button component

- **Symptom**: Tapping buttons does nothing. No console error.
- **Fix**: `halfTimeObj` MUST have a `Button` component. The code calls
  `halfTimeObj.GetComponent<Button>()` to toggle `interactable`. Same for all other
  button GOs.

### Mistake 4: `halfTimeObj` missing CanvasGroup

- **Symptom**: Half-time button doesn't dim after being used. No crash, just visual issue.
- **Fix**: Add a `CanvasGroup` component to the `HalfTimeButton` GO. The code sets
  `cg.alpha = 0.4f` when already used.

### Mistake 5: `revealController` not assigned on ChestOpenSceneController

- **Symptom**: `NullReferenceException` in `ChestOpenSceneController` when chest opens.
  Rewards don't display.
- **Fix**: Drag `RewardRevealRoot` (with `ChestRewardRevealController` component) into
  the `Reveal Controller` field.

### Mistake 6: `worldCardPrefab` not assigned on ChestOpenSceneController

- **Symptom**: `NullReferenceException` when chest lid opens and cards try to spawn.
- **Fix**: Drag `WorldRewardCardPrefab_TMP` prefab into `World Card Prefab` field.

### Mistake 7: Chest prefab has no Collider

- **Symptom**: Tapping the chest in ChestOpenScene does nothing. World chests can't be
  collected. No errors.
- **Fix**: Add a `BoxCollider` or `SphereCollider` to every chest prefab.

### Mistake 8: `slotPrefab` not assigned on ChestShownUI

- **Symptom**: Chest inventory shows nothing in the side panel. Console shows
  "[ChestShownUI] slotPrefab is null!" error.
- **Fix**: Drag `ChestSlotPrefab` from Project into the `Slot Prefab` field.

### Mistake 9: ProgressRoot or SummaryRoot starts active

- **Symptom**: Progress bar or summary is visible at scene start before any chest is opened.
  May also cause layout glitches.
- **Fix**: Select `ProgressRoot` and `SummaryRoot` in Hierarchy → uncheck the checkbox
  next to the name (set inactive).

### Mistake 10: GarageDatabase.asset not in Resources folder

- **Symptom**: Rare/Legendary chests give money + nitro + card but sticker is null.
  Console may show "StickerRewardHelper: GarageDatabase not found" log.
- **Fix**: Ensure `GarageDatabase.asset` exists at `Assets/Resources/GarageDatabase.asset`.
  The file MUST be in a folder named exactly `Resources`.

### Mistake 11: OnClick wired to wrong method or wrong object

- **Symptom**: Button does something unexpected (e.g., pressing "Start Unlock" opens the
  chest immediately, or nothing happens).
- **Fix**: Double-check each button's On Click () event:
  - Object = the GO with `ChestPopupController` (NOT the button itself).
  - Method = the exact public method name (see Section 6).

### Mistake 12: ChestSlotPrefab Button has inspector OnClick entries

- **Symptom**: Tapping a slot triggers both the code-added listener AND the inspector
  listener, causing double behavior.
- **Fix**: The `ChestSlotUI` slot prefab's Button should have **zero** On Click () entries
  in the inspector. All listeners are added in code.

### Mistake 13: Two ChestInventoryManager instances in scene

- **Symptom**: One gets destroyed in Awake. If timing is unlucky, references may break
  for one frame.
- **Fix**: Ensure only ONE instance of each DontDestroyOnLoad manager exists in the scene.
  Search Hierarchy for duplicates.

### Mistake 14: ChestOpenScene not in Build Settings

- **Symptom**: `SceneManager.LoadScene("ChestOpenScene")` throws "Scene not found" error.
- **Fix**: **File > Build Settings** → drag `ChestOpenScene` into the scene list.
  Make sure both "Main" and "ChestOpenScene" are in the list with checkmarks enabled.

### Mistake 15: Forgetting to clear old PlayerPrefs after refactor

- **Symptom**: Old chest data format causes JSON parse errors. Console shows
  "JsonUtility error" or chests appear with wrong state.
- **Fix**: In code, `ChestInventoryManager` handles migration gracefully. But if you see
  persistent issues during testing, call `PlayerPrefs.DeleteAll()` once (or use
  **Edit > Clear All PlayerPrefs** in Unity Editor) to reset.

---

## 15. Recommended Setup Order

Follow this order to avoid dependency issues:

### Phase 1: Prefabs (do these first — other things reference them)

1. **Create/update World Chest Prefabs** (Common, Rare, Legendary)
   - Add `Chest` component, `Collider`, verify `Renderer` exists.
   - Save as prefabs in Project.

2. **Create ChestSlotPrefab**
   - Build UI hierarchy (Image + TMP_Text + Button).
   - Add `ChestSlotUI` component, wire all 6 fields.
   - Save as prefab, delete scene instance.

3. **Verify WorldRewardCardPrefab_TMP exists** in your Project
   - Must have `WorldRewardCardController` component.

### Phase 2: Main Scene — Managers (create the singletons)

4. **Create `ChestInventoryManager` root GO**
   - Empty GO at scene root → Add Component → `ChestInventoryManager`.

5. **Create `ChestSessionManager` root GO**
   - Same process.

6. **Create `FreeChestRewardHandler` root GO**
   - Same process.

### Phase 3: Main Scene — UI Components

7. **Set up ChestShownPlace (`ChestShownUI`)**
   - Find/create the GO under Canvas.
   - Add `ChestShownUI` component.
   - Wire: `slotContainer`, `slotPrefab` (from step 2), `canvasGroup`.
   - Add VerticalLayoutGroup if desired.

8. **Set up ChestPopup (`ChestPopupController`)**
   - Find/create the popup under Canvas.
   - Build full hierarchy (PopupRoot + all children).
   - Add `ChestPopupController` component.
   - Wire all 10 inspector fields.
   - Wire all 5 button OnClick events (Section 6).

9. **Update ChestSpawner**
   - Select existing `ChestSpawner` GO.
   - Wire `commonChestPrefab`, `rareChestPrefab`, `legendaryChestPrefab`.
   - Verify `spawnTop` and `spawnBottom` are still assigned.

### Phase 4: ChestOpenScene

10. **Set up SpawnPoint + CardParkAnchor**
    - Create empty GameObjects, position them in 3D space where you want the chest
      and parked cards.

11. **Set up ChestOpenSceneController**
    - Wire all fields per Section 8.
    - Drag per-type prefabs into the 3 new slots + legacy slot.

12. **Set up ChestRewardRevealController**
    - Wire all fields per Section 9.
    - Create `SummarySlotPrefab` (SpriteRenderer child + `SummarySlotUI` component).
    - Create `SummarySlotsContainer` child inside `SummaryRoot`.
    - Wire `summarySlotPrefab` and `summarySlotsContainer` in inspector.
    - **Delete old SummaryCard1..4 / SummaryOverlay1..4 objects** from the hierarchy.
    - Ensure `ProgressRoot` and `SummaryRoot` start **inactive**.

### Phase 5: Build Settings + Verification

13. **Build Settings**
    - **File > Build Settings** → verify both `Main` and `ChestOpenScene` are listed
      and enabled.

14. **Resources check**
    - Verify `Assets/Resources/GarageDatabase.asset` exists.

15. **Play Test**
    - Enter Play mode in Main scene.
    - Collect a chest from the road → verify it appears in the side panel.
    - Tap the slot → verify popup opens with correct state.
    - Start Unlock → verify timer appears.
    - Wait for timer (or use Open Now) → verify scene transition to ChestOpenScene.
    - Tap chest 3 times → verify lid opens, cards fly out.
    - Tap through all rewards → verify summary shows.
    - Final tap → verify return to Main scene.
    - Check console for any `NullReferenceException` or warnings.

---

## QUICK REFERENCE: Build Scenes Order

| Index | Scene Name                | Notes                                                                  |
| ----- | ------------------------- | ---------------------------------------------------------------------- |
| 0     | Main (or your boot scene) | Must be first if it's the entry point                                  |
| 1+    | ChestOpenScene            | Must be in list for `SceneManager.LoadScene("ChestOpenScene")` to work |

---

## QUICK REFERENCE: Static Utilities (no setup needed)

| Class                 | File                     | Notes                                                             |
| --------------------- | ------------------------ | ----------------------------------------------------------------- |
| `ChestTypeConfig`     | `ChestTypeDefs.cs`       | All tuning constants. Edit the C# file to change values.          |
| `AdProvider`          | `AdProvider.cs`          | Dummy ad provider. Replace `ShowRewardedAd()` with your real SDK. |
| `StickerRewardHelper` | `StickerRewardHelper.cs` | Only dependency: `Resources/GarageDatabase`.                      |
| `ChestOpeningSession` | `ChestOpeningSession.cs` | Pure data. No setup.                                              |

---

**End of setup guide. Follow the sections in order, check every box in Section 13,
and review Section 14 if anything doesn't work.**
