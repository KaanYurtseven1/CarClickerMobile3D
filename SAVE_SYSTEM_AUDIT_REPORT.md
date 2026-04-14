# Save/Load System Diagnostic Audit Report

**Project:** CarClickerMobile3D  
**Date:** March 26, 2026  
**Scope:** Full project audit — save/load integrity, scene transitions, UI persistence, singleton architecture, data synchronization

---

## A. Executive Summary

### Overall Health: MODERATE — Functional but Fragile

The save/load system works for the core happy path (play → save → quit → resume), but has **significant structural weaknesses** that will surface during scene transitions, crash recovery, and edge-case flows.

**Main Risk Areas:**

1. **premiumCurrency (diamonds) is never saved** — complete data loss on restart
2. **MPS desync between displayed value, saved value, and actual passive income** — the three calculations use different formulas
3. **Multiple singletons leave stale static `Instance` references** after scene transitions — causing `MissingReferenceException` in race windows
4. **No atomic save** — partial PlayerPrefs writes on crash corrupt state
5. **Several gameplay-critical states are never persisted** — NitroRain progress, GarageManager bonus, momentum stacks, heat, car evolution stage, WorldScrollSpeed multiplier
6. **`FindObjectsByType<UpgradeButton>`** in save/load — upgrades can only be saved/loaded when Main scene is active

**Confidence Level:** HIGH — based on full source analysis of 80+ scripts across all systems.

---

## B. Systems Reviewed

### Core Save/Load

- `SaveSystem.cs` — central save/load orchestrator (PlayerPrefs)
- `CriticalManagerBootstrap.cs` — pre-scene manager bootstrapper
- `GameManager.cs` — empty DDOL singleton

### Economy

- `CurrencyManager.cs` — money, MPS, MPT, nitro, diamonds
- `BuildingManager.cs` + `BuildingDefinition.cs` + `BuildingType.cs` — building counts/production
- `UpgradeButton.cs` — upgrade levels and multiplicative effects
- `AutoIncome.cs` — deprecated, self-disabling

### Cards

- `CardManager.cs` — card levels, copies, effect application
- `CardDefinition.cs`, `CardType.cs`, `CardDropTuning.cs`
- `CardCollectionUI.cs`, `CardDetailPopupController.cs`, `CardSlotUI.cs`
- `ShopCardsTabs.cs`

### Chests

- `ChestInventoryManager.cs` — chest inventory with cross-scene handoff
- `ChestSessionManager.cs` — transactional chest opening flow
- `ChestOpeningSession.cs` — session data class
- `ChestOpenSceneController.cs` — chest open scene orchestrator
- `ChestPopupController.cs`, `ChestRewardRevealController.cs`
- `ChestShownUI.cs`, `ChestSlotUI.cs`, `ChestSpawner.cs`, `ChestMover.cs`

### Boost/Police/Radar/Cards Controllers

- `BoostModeController.cs` + `BoostModeControllerSaveData.cs`
- `BoostModeEffectsIntegration.cs`, `BoostModeCinematicController.cs`
- `BoostBarFeedbackController.cs`, `BoostPostProcessController.cs`
- `PoliceCatchController.cs`, `PoliceCatchTrigger.cs`
- `PoliceChaseFeedbackController.cs`, `PoliceCatchUIGuard.cs`
- `NitroMagnetController.cs`, `NitroRainController.cs`
- `NitroCoinSpawner.cs`, `NitroCoin.cs`, `NitroCoinGlowController.cs`
- `TurboFingerController.cs`, `MomentumController.cs`
- `PitStopCrewController.cs`, `SmallInvestmentController.cs`
- `PopularityManager.cs`, `RadarSpawner.cs`, `Radar.cs`, `RadarPopupController.cs`
- `AmbientHeatManager.cs`

### Garage

- `GarageSaveData.cs` — garage state persistence (DDOL)
- `GarageController.cs` — garage scene logic
- `GarageManagerController.cs` — spend-based MPS bonus (DDOL)
- `GarageSceneLoader.cs`, `GarageExitPopupController.cs`
- `MainSceneCarController.cs`
- `GarageBuyPopupController.cs`, `GarageFocusController.cs`
- `CarCustomizer.cs`, `ColorUIController.cs`, `StickerUIController.cs`, `PartsUIController.cs`

### UI/Panel System

- `UIManager.cs`, `CurrencyUI.cs`, `PopularityUI.cs`
- `PanelManager.cs`, `PanelTransitionManager.cs`
- `UIFlowState.cs`, `TopBarAnimator.cs`
- `BottomBarController.cs`, `BottomBarTabUI.cs`
- `DailyOffersController.cs`, `DailyOfferSlotUI.cs`

### Blacklist Campaign

- `BlacklistManager.cs`, `BlacklistSaveData.cs`, `BlacklistStatTracker.cs`
- `BlacklistPanelController.cs`, `BlacklistTierSO.cs`
- `BlacklistRewardClaimData.cs`, `BlacklistRewardDefinition.cs`
- `BlacklistMissionDefinition.cs`, `BlacklistMissionType.cs`, `BlacklistMissionMode.cs`
- `RewardPopupController.cs`, `MissionRowUI.cs`
- `FreeChestRewardHandler.cs`, `CardProgressRewardHandler.cs`
- `KaplamaPickerController.cs`

### Cinematic/Showcase

- `CarShowcaseDirector.cs`, `ShowcaseCarSpawner.cs`, `ShowcaseSkipButton.cs`
- `ShowcaseFadeController.cs`, `ShowcasePostProcessController.cs`, `ShowcaseCarNameReveal.cs`

### Other

- `SFXManager.cs`, `AdProvider.cs`, `StickerRewardHelper.cs`
- `WorldScrollSpeed.cs`, `WorldRewardCardController.cs`
- `TapInputRaycaster.cs`, `RoadLooper.cs`

### Scenes Analyzed

- `Main.unity` — primary gameplay scene
- `ChestOpenScene.unity` — chest opening flow
- `NewGarage.unity` — car customization
- `TakeTheCarScene.unity` — blacklist car cinematic

---

## C. Confirmed Good Behaviors

### Save System Core

1. **SaveSystem uses `DontDestroyOnLoad` with parent detach** — correct DDOL pattern
2. **`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`** properly resets statics for domain-reload-off Editor workflow
3. **`OnApplicationQuit` + `OnApplicationPause(true)`** both trigger save — good mobile practice
4. **`PlayerPrefs.Save()` is called at end of `SaveGame()`** — explicit flush
5. **`HasSave` guard** prevents loading garbage on first run
6. **Building key migration** from string-enum keys to integer keys is well-implemented and one-directional
7. **1-frame yield before LoadGame()** prevents Awake-order race conditions

### Chest System

8. **Transactional chest opening** with `ChestSessionManager` — commit-then-reveal pattern prevents reward loss
9. **Crash recovery** via `RecoverIfNeeded()` with committed/uncommitted/stale paths is thorough
10. **Multi-layer fallback** for chest data resolution (runtime → persisted session → legacy key → direct read)
11. **`CriticalManagerBootstrap`** pre-creates `ChestInventoryManager` and `ChestSessionManager` before any scene loads

### Economy

12. **Load order is correct**: buildings → non-Global upgrades → Global upgrades → cards
13. **`RecalculateMPSFromBuildings()`** does a full deterministic recalc instead of incremental — prevents drift
14. **Building count sanity clamps** protect against corrupt save data
15. **`GetDouble` sanity check** rejects values exceeding `1e15` when expected small — prevents save corruption propagation

### Garage

16. **`GarageSaveData` is DDOL** and persists customization state across all scene transitions
17. **`MainSceneCarController` re-applies garage state** on every Main scene load and on `SaveSystem.OnGameLoaded`
18. **`GarageExitPopupController` saves before loading Main** — no data loss on return

### Event System

19. **`ChestShownUI`** properly subscribes in `OnEnable`, unsubscribes in `OnDisable` to DDOL events
20. **`PoliceCatchController` static events** are cleared in `ResetStatics()` — no stale delegate chains across domain reloads
21. **`BoostModeController` re-binds UI** on every scene load via `TryBindUI()`

### Boost System

22. **BoostModeController saves via PlayerPrefs JSON** with offline time handling and state cascading
23. **Debounced save** (`SaveDebounceInterval = 1s`) prevents excessive writes during active boost
24. **Mutual exclusion with NitroRain** — rain pauses during boost, auto-resumes after

---

## D. Suspected Problems

### D1. premiumCurrency (Diamonds) Never Saved

- **What:** `CurrencyManager.premiumCurrency` (int) exists but `SaveSystem.SaveGame()` never reads or writes it. Only `nitroCoins` is persisted.
- **Where:** [SaveSystem.cs](Assets/Scripts/SaveSystem.cs) save/load methods; [CurrencyManager.cs](Assets/Scripts/CurrencyManager.cs)
- **Why it breaks:** If diamonds are ever granted (purchase, reward, ad), they vanish on app restart.
- **Status:** CONFIRMED — field exists, no save key references it
- **Player symptom:** Purchased/earned diamonds disappear after closing the app

### D2. Stale Singleton Instance References After Scene Death

- **What:** Multiple scene-scoped singletons set `static Instance` in Awake but never null it in `OnDestroy` or `ResetStatics`
- **Where:**
  - `PanelTransitionManager` — no OnDestroy cleanup, no ResetStatics
  - `BottomBarController` — no OnDestroy cleanup, no ResetStatics, no duplicate guard
  - `ShopCardsTabs` — no OnDestroy cleanup, no ResetStatics, no duplicate guard
  - `CardCollectionUI` — no ResetStatics, no duplicate guard
  - `CardDetailPopupController` — OnDestroy doesn't clear Instance, no ResetStatics
  - `MomentumController` — no ResetStatics
  - `TurboFingerController` — no ResetStatics, no OnDestroy
  - `SmallInvestmentController` — no ResetStatics
- **Why it breaks:** After Main → Garage → Main, or Main → ChestOpenScene → Main, `Instance` points to a destroyed Unity object. Any DDOL manager checking `X.Instance != null` gets `true` (C# null check passes for destroyed Unity objects), then accessing a member throws `MissingReferenceException`.
- **Status:** CONFIRMED — code inspection verified missing cleanup
- **Player symptom:** Intermittent errors on scene return; panels not opening; UI not responding

### D3. GetAutoMPS() vs Update() MPS Formula Inconsistency

- **What:** Three different formulas compute MPS across the codebase:
  - **`Update()` passive income:** `(moneyPerSecond + garageBonus) * incomeBoostMultiplier` — no card multiplier
  - **`GetAutoMPS()`:** `moneyPerSecond * incomeBoostMultiplier * cardGlobalMpsMultiplier` — no garageBonus
  - **`SaveSystem` saves/loads:** raw `moneyPerSecond` field — which has upgrade multipliers baked in but not card or boost
- **Where:** [CurrencyManager.cs](Assets/Scripts/CurrencyManager.cs) lines 187 and 532
- **Why it breaks:** The money actually earned per second differs from what `GetAutoMPS()` returns, which differs from the saved/loaded value. After a save/load cycle, MPS can shift.
- **Status:** CONFIRMED (though `cardGlobalMpsMultiplier` currently always returns 1.0 since `garageManagerPercentCached` is forced to 0 — this is a **dormant bug** that will activate if any card starts providing a global MPS multiplier)
- **Player symptom:** Currently masked; will manifest as wrong MPS display whenever card multiplier is enabled

### D4. UpgradeButton Data Only Accessible in Main Scene

- **What:** `SaveSystem.SaveGame()` calls `FindObjectsByType<UpgradeButton>()` to save upgrade levels. `LoadGame()` does the same to load them. UpgradeButtons only exist in the Main scene.
- **Where:** [SaveSystem.cs](Assets/Scripts/SaveSystem.cs) save/load methods
- **Why it breaks:** If `SaveGame()` is called from a non-Main scene (e.g., `OnApplicationPause` while in Garage, ChestOpenScene, or TakeTheCarScene), **all upgrade data is written as level 0** because no UpgradeButton objects exist. This overwrites real upgrade levels with zeros.
- **Status:** CONFIRMED — `FindObjectsByType` returns empty array in non-Main scenes
- **Player symptom:** **CRITICAL** — all upgrade levels reset to 0 after backgrounding the app in Garage/Chest scenes. The player returns to Main with no upgrades.

### D5. No Atomic Save — Partial Write on Crash

- **What:** `SaveGame()` writes 50+ PlayerPrefs keys sequentially. If the app crashes or is killed mid-write, some keys are updated and others are not.
- **Where:** [SaveSystem.cs](Assets/Scripts/SaveSystem.cs) `SaveGame()` method
- **Why it breaks:** Economy values could be inconsistent (e.g., money updated but building counts not, or vice versa).
- **Status:** HIGH-RISK SUSPICION — depends on OS/Unity's PlayerPrefs batch behavior
- **Player symptom:** Money/building/card desync after a force-kill during auto-save

### D6. BlacklistRewardClaimData Race Condition — Multiple Independent Loaders

- **What:** `BlacklistRewardClaimData.LoadFromPrefs()` is called independently by `RewardPopupController` (caches it), `FreeChestRewardHandler`, `KaplamaPickerController`, and `CardProgressRewardHandler`. Each creates its own instance from PlayerPrefs. If two callers read → modify → save, last-write-wins loses data.
- **Where:** All Blacklist reward handlers
- **Why it breaks:** During multi-reward claim flow (chests + cards + kaplama), one handler's save can overwrite another's pending count decrement.
- **Status:** HIGH-RISK SUSPICION — depends on flow timing
- **Player symptom:** Free chests or card progress rewards silently lost or duplicated

### D7. NitroMagnetController Cooldown Doesn't Account for Offline Time

- **What:** `SaveState()` saves `_cooldownEndTime - Time.time` as remaining seconds. On reload, it restores `Time.time + remaining`. App closure time is ignored.
- **Where:** [NitroMagnetController.cs](Assets/Scripts/NitroMagnetController.cs) `SaveState()`/`LoadState()`
- **Why it breaks:** Close app with 50s cooldown → reopen 10 minutes later → cooldown is still 50s instead of already elapsed.
- **Status:** CONFIRMED
- **Player symptom:** Magnet cooldown doesn't progress while app is closed (inconsistent with BoostMode which does handle offline time)

### D8. PoliceCatchTrigger.\_lastChaseEndTime Not Persisted

- **What:** The cooldown timer between police chases (`_lastChaseEndTime`) is only in memory. Scene change or app restart resets it to 0.
- **Where:** [PoliceCatchTrigger.cs](Assets/Scripts/PoliceCatchTrigger.cs)
- **Why it breaks:** Player could trigger back-to-back police chases by switching scenes.
- **Status:** CONFIRMED
- **Player symptom:** Rapid consecutive police chases after returning from Garage

### D9. GameManager Missing Parent Detach Before DDOL

- **What:** `GameManager.Awake()` calls `DontDestroyOnLoad(gameObject)` without first detaching from parent. If it's a child of another object in the scene, DDOL silently fails.
- **Where:** [GameManager.cs](Assets/Scripts/GameManager.cs)
- **Why it breaks:** GameManager gets destroyed on every scene change instead of persisting.
- **Status:** CONFIRMED — no `transform.SetParent(null)` call exists (while SaveSystem, CurrencyManager, GarageSaveData all have it)
- **Player symptom:** Currently no impact because GameManager has zero functionality, but if logic is added to it, it won't survive scenes when parented.

### D10. GarageSaveData Double-Load

- **What:** `GarageSaveData.Awake()` loads from PlayerPrefs, then `SaveSystem.LoadGame()` loads it again 1 frame later.
- **Where:** [GarageSaveData.cs](Assets/Scripts/Garage/GarageSaveData.cs) `Awake()`; [SaveSystem.cs](Assets/Scripts/SaveSystem.cs) `LoadGame()`
- **Why it breaks:** Any runtime modification to garage data between Awake and LoadGame is overwritten by the second load. The window is 1 frame — small but real.
- **Status:** CONFIRMED — architecturally redundant
- **Player symptom:** Extremely unlikely to cause visible issues, but could theoretically reset a car unlock if `MarkCarUnlocked()` fires in the 1-frame window.

### D11. CardType Enum Uses Implicit Integer Values

- **What:** `CardType` enum members have no explicit `= N` assignments. `ChestOpeningSession.committedCardType` stores the int value in PlayerPrefs.
- **Where:** [CardType.cs](Assets/Scripts/CardType.cs), [ChestOpeningSession.cs](Assets/Scripts/ChestOpeningSession.cs)
- **Why it breaks:** If a developer inserts or reorders enum members, all persisted sessions and card data map to wrong card types.
- **Status:** HIGH-RISK SUSPICION — no issue today, one enum change triggers it
- **Player symptom:** Wrong cards granted from recovered chest sessions

### D12. Daily Offers Free Chest Reward is a Placeholder

- **What:** `DailyOffersController.ClaimFreeReward()` with `FreeRewardType.FreeChest` only logs a message — no chest is actually granted.
- **Where:** [DailyOffersController.cs](Assets/Scripts/DailyOffersController.cs)
- **Why it breaks:** Players see a free chest offer but receive nothing when claiming it.
- **Status:** CONFIRMED
- **Player symptom:** Free chest claim button does nothing

### D13. GarageManagerController Bonus MPS May Be Dropped on Scene Reload

- **What:** `GarageManagerController` is DDOL and tracks an active MPS bonus with `Time.time` timers. When returning to Main, `BuildingManager.RecalculateMPSFromBuildings()` **resets** `moneyPerSecond` to building-only totals. The GarageManager bonus is only added in `CurrencyManager.Update()` via `GarageManagerController.Instance.CurrentBonusMps` — but `GetAutoMPS()` and the saved `moneyPerSecond` don't include it.
- **Where:** [GarageManagerController.cs](Assets/Scripts/GarageManagerController.cs); [CurrencyManager.cs](Assets/Scripts/CurrencyManager.cs)
- **Why it breaks:** The displayed MPS (via `displayedMps` which uses `totalMoneyEarned` delta) will be correct, but `GetAutoMPS()` won't reflect the garage bonus. PitStopCrew's offline earnings calculation uses `_lastMps` snapshot from `Update()` which DOES include garage bonus — so offline earnings may be higher during a garage bonus window. Inconsistent behavior.
- **Status:** CONFIRMED — architectural inconsistency
- **Player symptom:** Minor — MPS display may not match actual income during GarageManager bonus periods

### D14. Garage Scene Save on App Kill

- **What:** `GarageController.PersistCurrentState()` writes to the DDOL `GarageSaveData` in memory. Disk save only happens on explicit "Exit" button press. If the app is killed while in Garage, the in-memory state is lost because `SaveSystem.OnApplicationPause` saves but `GarageSaveData.SaveToPrefs()` may save stale data if `PersistCurrentState()` hasn't been called recently.
- **Where:** [GarageController.cs](Assets/Scripts/Garage/GarageController.cs), [GarageSaveData.cs](Assets/Scripts/Garage/GarageSaveData.cs)
- **Why it breaks:** GarageController calls `PersistCurrentState()` on every change, so `GarageSaveData` should always have current data in memory. `SaveSystem.SaveGame()` calls `GarageSaveData.SaveToPrefs()` which flushes to PlayerPrefs. On app pause in Garage scene, `SaveSystem.OnApplicationPause` triggers `SaveGame()`. This flow appears safe.
- **Status:** LOW-RISK — the chain appears complete, but the upgrade data loss (D4) still occurs since we're not in Main scene.
- **Player symptom:** Garage changes preserved correctly; economy data at risk (see D4).

---

## E. Missing Save Coverage

| Data                                                                         | Location                               | Currently Saved? | Impact                                                                                 |
| ---------------------------------------------------------------------------- | -------------------------------------- | :--------------: | -------------------------------------------------------------------------------------- |
| `premiumCurrency` (diamonds)                                                 | `CurrencyManager`                      |      **NO**      | **CRITICAL** — complete loss on restart                                                |
| NitroRain progress (`_collectedCount`, state, queued rain level)             | `NitroRainController`                  |      **NO**      | Medium — rain progress lost on restart                                                 |
| GarageManager bonus state (`_spentSinceLastTrigger`, `_currentState`, timer) | `GarageManagerController`              |      **NO**      | Medium — active bonus lost on restart                                                  |
| Momentum stacks                                                              | `MomentumController`                   |      **NO**      | Low — ephemeral by design, but stacks lost on scene change                             |
| Ambient heat level                                                           | `AmbientHeatManager`                   |      **NO**      | Low — invisible mechanic, but balance shifts on restart                                |
| Car evolution stage                                                          | `CarEvolution`                         |      **NO**      | Low — visual-only stage resets to 0 on scene reload                                    |
| WorldScrollSpeed multiplier                                                  | `WorldScrollSpeed`                     |      **NO**      | Medium — if boost changes speed and scene changes mid-boost, speed could stay elevated |
| `passiveBuffer` (fractional MPS accumulation)                                | `CurrencyManager`                      |      **NO**      | Trivial — sub-1.0 money amount lost                                                    |
| TurboFinger active/cooldown state                                            | `TurboFingerController`                |      **NO**      | Low — buff lost on scene change                                                        |
| SmallInvestment spend tracking                                               | `SmallInvestmentController`            |      **NO**      | Low — reactive system, no accumulated state                                            |
| DailyOffers slot configuration (already saved independently)                 | `DailyOffersController`                |       YES        | —                                                                                      |
| Police chase cooldown timer                                                  | `PoliceCatchTrigger._lastChaseEndTime` |      **NO**      | Medium — rapid chases after scene return                                               |

---

## F. Scene Transition / UI Rebuild Risks

### F1. Main → ChestOpenScene → Main

- **UpgradeButton data at risk** if `OnApplicationPause` fires in ChestOpenScene (D4)
- All scene-local singletons (PanelTransitionManager, BottomBarController, ShopCardsTabs, etc.) have stale `Instance` references during the transition gap
- `SaveSystem.LoadGame()` fires on return, properly rebuilding all state
- Chest session recovery handles crash between scenes correctly
- **Risk Level:** MEDIUM (due to D4)

### F2. Main → NewGarage → Main

- Same UpgradeButton risk (D4) if app pauses in Garage
- `GarageExitPopupController` correctly saves before returning
- `MainSceneCarController` correctly re-applies car visuals on return
- `UIFlowState.IsContentPanelOpen` may carry over (cleared by PanelTransitionManager.Awake)
- **Risk Level:** MEDIUM (due to D4)

### F3. Main → TakeTheCarScene → Main

- `ShowcaseCarSpawner.PendingCarId` is a **static** field — survives scene transitions
- Car showcase → cinematic → returns to Main → `SaveSystem.LoadGame()` rebuilds
- `BlacklistManager` (DDOL) persists tier advancement
- **Risk Level:** LOW

### F4. UI Elements After Scene Return

| UI Component              | Rebuild Method                                              | Risk                                 |
| ------------------------- | ----------------------------------------------------------- | ------------------------------------ |
| CurrencyUI                | Fresh instance in scene, reads DDOL CurrencyManager         | SAFE                                 |
| PopularityUI              | Subscribes to DDOL PopularityManager in OnEnable            | SAFE (if PopularityManager is ready) |
| ChestShownUI              | Subscribes to DDOL ChestInventoryManager in OnEnable        | SAFE                                 |
| BuildingButton[]          | Subscribe to DDOL BuildingManager + SaveSystem.OnGameLoaded | SAFE                                 |
| UpgradeButton[]           | Loaded from SaveSystem.LoadGame() via FindObjectsByType     | SAFE on entry, **AT RISK on save**   |
| BottomBarController       | Stale Instance between scenes                               | **STALE**                            |
| PanelTransitionManager    | Stale Instance between scenes                               | **STALE**                            |
| ShopCardsTabs             | Stale Instance between scenes                               | **STALE**                            |
| CardDetailPopupController | Stale Instance after scene death                            | **STALE**                            |
| CardCollectionUI          | No ResetStatics, no duplicate guard                         | **MODERATE**                         |
| BoostModeController UI    | Re-binds via TryBindUI() on scene load                      | SAFE                                 |
| DailyOffersController     | Loads own state from PlayerPrefs in OnEnable                | SAFE                                 |

### F5. DOTween Animation Interruption

- All boost-related controllers kill tweens in `OnDestroy`/`OnDisable` — SAFE
- `ChestRewardRevealController` has `SnapKillAllTweens()` — SAFE
- `PanelTransitionManager` kills sequence in `OnDestroy` — SAFE
- Some per-character typewriter animations may leave TMP in modified state if killed mid-animation — MINOR

---

## G. Initialization / Load Order Risks

### G1. Singleton Lifecycle Matrix

| Manager                      |        DDOL?         | ResetStatics? | OnDestroy clears Instance? |     Parent Detach?      |
| ---------------------------- | :------------------: | :-----------: | :------------------------: | :---------------------: |
| SaveSystem                   |         YES          |      YES      |            YES             |           YES           |
| CurrencyManager              |         YES          |      YES      |            YES             |           YES           |
| BuildingManager              |         YES          |      YES      |            YES             |       _(verify)_        |
| CardManager                  |         YES          |      YES      |            YES             |           YES           |
| GarageSaveData               |         YES          |      YES      |            YES             |           YES           |
| GarageManagerController      |         YES          |      YES      |            YES             |       _(verify)_        |
| PopularityManager            |         YES          |      YES      |            YES             | _(no detach code seen)_ |
| WorldScrollSpeed             |         YES          |      YES      |            YES             |       _(verify)_        |
| BoostModeController          |         YES          |      YES      |            YES             |           YES           |
| BoostModeEffectsIntegration  |         YES          |      YES      |            YES             |           YES           |
| BoostModeCinematicController |         YES          |      YES      |            YES             |       _(verify)_        |
| PitStopCrewController        |         YES          |      YES      |            YES             |       _(verify)_        |
| NitroRainController          |         YES          |      YES      |            YES             |       _(verify)_        |
| SFXManager                   |         YES          |      YES      |            YES             |       _(verify)_        |
| ChestInventoryManager        |         YES          |      YES      |            YES             |           YES           |
| ChestSessionManager          |         YES          |      YES      |            YES             |           YES           |
| BlacklistManager             |         YES          |      YES      |            YES             |           YES           |
| BlacklistStatTracker         |         YES          |      YES      |            YES             |       _(verify)_        |
| FreeChestRewardHandler       |         YES          |      YES      |            YES             |       _(verify)_        |
| GameManager                  |         YES          |      YES      |            YES             |         **NO**          |
|                              |                      |               |                            |                         |
| PanelTransitionManager       |          NO          |    **NO**     |           **NO**           |           N/A           |
| BottomBarController          |          NO          |    **NO**     |           **NO**           |           N/A           |
| ShopCardsTabs                |          NO          |    **NO**     |           **NO**           |           N/A           |
| CardCollectionUI             |          NO          |    **NO**     |  NO(Awake unconditional)   |           N/A           |
| CardDetailPopupController    |          NO          |    **NO**     |           **NO**           |           N/A           |
| MomentumController           |          NO          |    **NO**     |             NO             |           N/A           |
| TurboFingerController        |          NO          |    **NO**     |             NO             |           N/A           |
| SmallInvestmentController    |          NO          |    **NO**     |             NO             |           N/A           |
| ChestShownUI                 |          NO          |      YES      |            YES             |           N/A           |
| ChestPopupController         |          NO          |      YES      |        _(partial)_         |           N/A           |
| CurrencyUI                   |          NO          |      YES      |            YES             |           N/A           |
| TopBarAnimator               |          NO          |      YES      |            YES             |           N/A           |
| RadarPopupController         |          NO          |      YES      |            YES             |           N/A           |
| PoliceCatchController        |          NO          |      YES      |            YES             |           N/A           |
| PoliceCatchTrigger           |          NO          |      YES      |            YES             |           N/A           |
| RewardPopupController        |          NO          |      YES      |            YES             |           N/A           |
| KaplamaPickerController      |          NO          |      YES      |            YES             |           N/A           |
| AmbientHeatManager           | NO(relies on parent) |      YES      |            YES             |           N/A           |
| NitroMagnetController        |          NO          |      YES      |            YES             |           N/A           |

### G2. Execution Order

```
1. [SubsystemRegistration] → All ResetStatics() → Instance = null everywhere
2. [BeforeSceneLoad] → CriticalManagerBootstrap creates ChestInventoryManager, ChestSessionManager
3. Scene Awake() → All managers initialize (ORDER NOT GUARANTEED):
   - DDOL singletons: check existing Instance, set DDOL
   - GarageSaveData.Awake() calls LoadFromPrefs() immediately
   - BuildingManager.Awake() calls ResetAllBuildingCounts() (zeroes all counts)
4. Scene OnEnable() → UI components subscribe to DDOL events
5. Scene Start() → UI components do initial data read
   - SaveSystem.Start() → yield 1 frame → LoadGame()
6. LoadGame() → Full state restoration (see D. section for order)
7. OnGameLoaded event → Late subscribers activate
```

### G3. Awake Order Dependency Risks

- **BuildingManager.Awake()** zeros all building counts. If `SaveSystem.LoadGame()` ran before `BuildingManager.Awake()` (impossible since Load waits 1 frame), counts would be zeroed after load. **Currently safe** due to SaveSystem's yield.
- **GarageSaveData.Awake()** loads immediately. `SaveSystem.LoadGame()` loads again 1 frame later. The double-load is redundant but doesn't break anything.
- **BoostModeController.LoadState()** runs in `Start()` or on `OnGameLoaded`. If it runs before `CurrencyManager` is ready, boost multiplier can't be applied. The `OnGameLoaded` pattern mitigates this.

### G4. Critical Bootstrap Gap

`CriticalManagerBootstrap` only creates `ChestInventoryManager` and `ChestSessionManager`. All other managers must exist in the scene. If you enter Play Mode from a non-Main scene (e.g., testing Garage), most managers won't exist:

- No `SaveSystem` → no save/load
- No `CurrencyManager` → no economy
- No `BuildingManager` → no buildings
- No `CardManager` → no cards
- No `BoostModeController` → no boost

---

## H. Recommended Fix Priority

### CRITICAL (Fix Immediately)

| #   | Issue                                                      | Ref |
| --- | ---------------------------------------------------------- | --- |
| 1   | **UpgradeButton save in non-Main scenes writes all zeros** | D4  |
| 2   | **premiumCurrency never saved**                            | D1  |

### HIGH (Fix Soon)

| #   | Issue                                                   | Ref |
| --- | ------------------------------------------------------- | --- |
| 3   | Stale singleton Instance references (8 classes)         | D2  |
| 4   | BlacklistRewardClaimData race condition (multi-handler) | D6  |
| 5   | CardType enum no explicit int values                    | D11 |
| 6   | No atomic save mechanism                                | D5  |
| 7   | GetAutoMPS() vs Update() MPS formula inconsistency      | D3  |

### MEDIUM (Plan for Next Sprint)

| #   | Issue                                                           | Ref                |
| --- | --------------------------------------------------------------- | ------------------ |
| 8   | NitroMagnetController cooldown ignores offline time             | D7                 |
| 9   | NitroRain progress not saved                                    | E                  |
| 10  | GarageManager bonus state not saved                             | E                  |
| 11  | Police chase cooldown not persisted                             | D8                 |
| 12  | Daily Offers free chest is placeholder                          | D12                |
| 13  | GarageManager bonus MPS not in GetAutoMPS()                     | D13                |
| 14  | Expand CriticalManagerBootstrap to cover all essential managers | G4                 |
| 15  | BlacklistStatTracker.SaveCounters() missing PlayerPrefs.Save()  | Blacklist analysis |
| 16  | BlacklistSaveData.SaveToPrefs() missing PlayerPrefs.Save()      | Blacklist analysis |

### LOW (Nice to Have)

| #   | Issue                                                           | Ref         |
| --- | --------------------------------------------------------------- | ----------- |
| 17  | GameManager missing parent detach for DDOL                      | D9          |
| 18  | GarageSaveData double-load                                      | D10         |
| 19  | CarEvolution stage not persisted                                | E           |
| 20  | AmbientHeatManager heat not persisted                           | E           |
| 21  | TapInputRaycaster static diagnostics without ResetStatics       | F           |
| 22  | PanelManager / PanelTransitionManager potential dual-management | UI analysis |
| 23  | AutoIncome.\_warnedOnce static leak                             | Minor       |

---

## I. Suggested Next Step Plan

### Phase 1: Critical Data Safety (Immediate)

1. **Fix UpgradeButton save data loss (D4)**
   - Option A: Cache upgrade levels in a DDOL structure (e.g., dictionary on SaveSystem or BuildingManager) so save works from any scene
   - Option B: Guard `SaveGame()` to skip upgrade writes when not in Main scene (preserving last-known values)
   - Recommended: **Option A** — caching is more robust
2. **Add premiumCurrency save/load (D1)**
   - Add `PlayerPrefs.SetInt("Save_PremiumCurrency", ...)` in SaveGame
   - Add corresponding GetInt in LoadGame

### Phase 2: Singleton Cleanup (High Priority)

3. **Add `OnDestroy` + `ResetStatics` to all 8 broken singletons**
   - PanelTransitionManager, BottomBarController, ShopCardsTabs, CardCollectionUI, CardDetailPopupController, MomentumController, TurboFingerController, SmallInvestmentController
   - Template: `OnDestroy() { if (Instance == this) Instance = null; }` + `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] static void ResetStatics() { Instance = null; }`

### Phase 3: Save Integrity Improvements

4. **Add explicit int values to CardType enum** (`TurboFinger = 0, NitroRain = 1, ...`)
5. **Centralize BlacklistRewardClaimData** — make it singleton or pass by reference instead of loading from prefs independently
6. **Consider atomic save** — serialize entire game state to a single JSON blob, write to temp file, rename (file-level atomicity), with PlayerPrefs as backup

### Phase 4: State Persistence Gaps

7. **Persist NitroRain state** — save collection count and state to PlayerPrefs
8. **Persist GarageManager bonus state** — save spend accumulator and active/cooldown timers
9. **Fix NitroMagnet cooldown offline handling** — use UTC timestamps instead of Time.time offsets
10. **Persist police chase cooldown** — save `_lastChaseEndTime` as UTC timestamp

### Phase 5: MPS Calculation Unification

11. **Unify GetAutoMPS() and Update() formulas** — create a single `ComputeEffectiveMPS()` method used everywhere
12. **Include GarageManager bonus in GetAutoMPS()** if appropriate
13. **Decide on card MPS multiplier architecture** — either bake into moneyPerSecond or always multiply dynamically, not both

### Phase 6: Architecture Hardening

14. **Expand CriticalManagerBootstrap** to cover all essential DDOL managers (SaveSystem, CurrencyManager, BuildingManager, CardManager at minimum)
15. **Add `PlayerPrefs.Save()` calls** to BlacklistSaveData, BlacklistStatTracker, and any subsystem that writes to PlayerPrefs independently
16. **Implement Daily Offers free chest reward** (currently placeholder)

---

_End of audit report. No code was modified during this analysis._
