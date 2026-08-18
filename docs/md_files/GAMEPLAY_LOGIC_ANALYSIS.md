# Car Clicker Mobile 3D — Gameplay Logic Analysis

> **Read-only analysis.** No code changes proposed.

---

## A. Game Loop Summary (30-Second Pitch)

Car Clicker Mobile 3D is a **free-to-play idle/clicker game** built in Unity (URP). The player taps a car cruising on a scrolling road to earn money. Money buys **buildings** (28 tiers) that produce passive income (MPS). Eight collectible **cards** introduce layered mechanics: tap multipliers, nitro rain, magnet pulls, momentum combos, offline earnings, spend-based bonuses, and boost mode. **Radar traps** spawn on the road — miss them and **popularity rises**, eventually triggering a **Police Chase minigame** (tap-prompt sequence with money stakes). **Chests** appear on the road and drop money, nitro coins, and card copies via a weighted loot system. A **Garage** lets the player switch cars, apply skins, and toggle mod parts with no gameplay effect. There is **no real-money IAP** wired yet; nitro coins serve as hard currency.

**Core loop:** Tap → earn money → buy buildings → earn MPS → afford stronger buildings → unlock cards → cards amplify tapping/idle income → radar & police inject risk/reward → chests inject randomness & card progression → repeat.

---

## B. Core Systems Map

| System          | Primary Class                    | Singleton?        | DDOL?            | Role                                                                                                                |
| --------------- | -------------------------------- | ----------------- | ---------------- | ------------------------------------------------------------------------------------------------------------------- |
| Thin Shell      | `GameManager`                    | Yes               | Yes              | Placeholder manager; no logic                                                                                       |
| Economy         | `CurrencyManager`                | Yes               | Yes              | Owns `money`, `moneyPerTap`, `moneyPerSecond`, `nitroCoins`, boost multiplier, animated add, suppress/buffer system |
| Buildings       | `BuildingManager`                | Yes               | Yes              | 28 buildings, tiered cost curves, MPS recalculation, progression lock                                               |
| Cards           | `CardManager`                    | Yes               | Yes              | 8 card types, 8-segment upgrade model, effect delegation to dedicated controllers                                   |
| Chest Inventory | `ChestInventoryManager`          | Yes               | Yes              | Chest queue, unlock timer, pending-open handoff to `ChestOpenScene`                                                 |
| Chest Reveal    | `ChestOpenSceneController`       | Yes               | No (scene-local) | 7-tap reveal state machine, weighted card/segment selection, DOTween animations                                     |
| Boost Mode      | `BoostModeController`            | Yes               | Yes              | State machine (Locked→Charging→Ready→Active→Cooldown), nitro-charge gating                                          |
| Police Chase    | `PoliceCatchController`          | Yes               | Yes              | Chase minigame coroutine, tap prompts, money suppress/buffer, penalty/reward                                        |
| Police Trigger  | `PoliceCatchTrigger`             | Yes               | No               | Links radar-miss counter to chase start via popularity stage thresholds                                             |
| Popularity      | `PopularityManager`              | Yes               | Yes              | 0–100 scale, 6 stages, events                                                                                       |
| Radar           | `Radar` / `RadarSpawner`         | No / No           | No               | Spawns radar traps, miss → +popularity + photo event, tap → −popularity                                             |
| Input           | `TapInputRaycaster`              | No                | No               | Raycasts to Car/NitroCoin/Chest/Radar tags, multiplier chain, police isolation                                      |
| UI Panels       | `PanelManager`                   | Yes               | No               | Switches Clicker/ShopCards/Bank/TimeWarp/Ranking panels                                                             |
| UI Flow         | `UIFlowState` (static)           | N/A               | N/A              | Global flags: `IsContentPanelOpen`, `IsSpawnSuppressed`, `IsTapSuppressed`                                          |
| Save/Load       | `SaveSystem`                     | Yes               | Yes              | PlayerPrefs persistence, building key migration, ordered load with effect reapplication, `OnGameLoaded` event       |
| Upgrades        | `UpgradeButton`                  | No (per-instance) | No               | Tap/MPS/Global upgrade UI buttons with cost escalation                                                              |
| Nitro Coin      | `NitroCoin` / `NitroCoinSpawner` | No                | No               | Spawns on road, scrolls down, tapped for nitro currency, magnet-pullable                                            |
| Chest World     | `ChestSpawner` / `ChestMover`    | No                | No               | Spawns chests on road at intervals, scrolls down to despawn                                                         |
| Garage          | `GarageController`               | Yes               | No               | Car switching, skin/sticker/part customization, visual only                                                         |
| Garage Data     | `CarDataSO` / `GarageDatabaseSO` | N/A               | N/A              | ScriptableObject catalog of cars, 6 colors × 6 stickers = 36 generated materials per car, 18 mod parts              |

### Card-Specific Controllers

| Card            | Controller                  | Mechanic Summary                                                                               |
| --------------- | --------------------------- | ---------------------------------------------------------------------------------------------- |
| TurboFinger     | `TurboFingerController`     | 50 taps in 15s rolling window → level-based MPT multiplier (2×–14×) for 30s → 120s cooldown    |
| GarageManager   | `GarageManagerController`   | Spend threshold (MPS × 20–30s equiv) → snapshot MPS × 10–15× bonus MPS for 60s → 120s cooldown |
| NitroRain       | `NitroRainController`       | Collect 5–16 nitro → 30s delay → rain nitro coins for 5–20s → cycle repeats                    |
| PitStopCrew     | `PitStopCrewController`     | Offline earnings: `exitMPS × offlineTime × efficiency(20%–85%)`, capped 2–12 hrs               |
| SmallInvestment | `SmallInvestmentController` | Refund 2%–12% of every money/nitro spend (loop-guarded via `IsApplyingRefund`)                 |
| Momentum        | `MomentumController`        | Consecutive taps build stacks (0.8–1.8s window), ×1.15–×2.20 at cap                            |
| NitroMagnet     | `NitroMagnetController`     | 40–90 taps → arm magnet → auto-collect 2–9 nitro coins in area → disarm                        |
| BoostMode       | `BoostModeController`       | Nitro charge → 3×–20× income multiplier for 6–16s → 25–60s cooldown                            |

---

## C. Main Flow Diagrams

### C1. Tap-to-Money Flow

```
Player Tap
  │
  ├─ TapInputRaycaster.HandleTap()
  │   ├─ GATE: UIFlowState.IsTapSuppressed? → block
  │   ├─ GATE: ChestPopupController.IsPopupOpen? → block
  │   ├─ GATE: RadarPopupController.IsPopupOpen? → block
  │   │
  │   ├─ Raycast → tag "Car"
  │   │   ├─ IF isPoliceChaseActive → PoliceCatchController.OnChaseTap() only
  │   │   │
  │   │   ├─ ELSE (normal tap):
  │   │   │   ├─ base = moneyPerTap + cardBonus
  │   │   │   ├─ × CurrencyManager.incomeBoostMultiplier (boost mode)
  │   │   │   ├─ × TurboFingerController.CurrentMultiplier
  │   │   │   ├─ × MomentumController.CurrentMultiplier
  │   │   │   ├─ CurrencyManager.AddMoney(final, "TAP", base, multiplier)
  │   │   │   │
  │   │   │   ├─ TurboFingerController.OnTap()     → builds rolling window
  │   │   │   ├─ MomentumController.RegisterClick() → builds stacks
  │   │   │   └─ NitroMagnetController.RegisterTap() → counts toward arm
  │   │
  │   ├─ Raycast → tag "NitroCoin"
  │   │   └─ CurrencyManager.AddNitroCoins(reward)
  │   │       ├─ CardManager.NotifyNitroCollected(amount)
  │   │       │   ├─ NitroRainController.OnNitroCollected()
  │   │       │   └─ BoostModeController.OnNitroCollected()
  │   │
  │   ├─ Raycast → tag "Chest"
  │   │   └─ ChestPopupController.ShowChestPopup()
  │   │       └─ (player confirms) → ChestInventoryManager.AddChestFromWorld()
  │   │
  │   └─ Raycast → tag "Radar"
  │       └─ Radar.OnTapped() → PopularityManager.AddPopularity(-0.01)
```

### C2. Passive Income Flow (CurrencyManager.Update)

```
Every Frame:
  │
  ├─ Boost Timer: if boostEndTime reached → reset incomeBoostMultiplier to 1
  │
  ├─ Passive MPS Income:
  │   ├─ raw = moneyPerSecond * Time.deltaTime
  │   ├─ × incomeBoostMultiplier (boost mode)
  │   ├─ + GarageManagerController.Instance.CurrentBonusMps * Time.deltaTime
  │   ├─ passiveBuffer += raw
  │   ├─ IF passiveBuffer >= 1.0 → AddMoney(floor(buffer), "PASSIVE")
  │   │                            buffer -= floor(buffer)
  │
  ├─ MPS Measurement Window (every 1s snapshot for UI)
  │
  └─ AnimatedMoney coroutines (AddMoneyAnimated)
```

### C3. Building Purchase Flow

```
Player taps BuildingButton
  │
  └─ BuildingManager.TryBuyBuilding(type)
      ├─ IsBuildingLocked(type)? → check previous building count >= 1
      ├─ cost = GetCurrentCost(type) = baseCost × tierMultiplier^count
      │   Tiers: IDs 0-5 → 1.15, 6-12 → 1.17, 13-20 → 1.20, 21-27 → 1.25
      ├─ CurrencyManager.TrySpendMoney(cost) → fires OnMoneySpent
      │   └─ SmallInvestmentController.HandleMoneySpent(cost) → refunds 2-12%
      │   └─ GarageManagerController.OnMoneySpent(cost) → accumulates spend
      ├─ building.currentCount++ (clamped to 500 early / 100 late)
      ├─ IF type == StreetDeals (ID=0): CurrencyManager.moneyPerTap += tapBonusPerLevel
      ├─ RecalculateMPSFromBuildings() → deterministic sum of all building MPS
      ├─ Fire OnBuildingPurchased(type, newCount) event
      └─ SaveSystem.SaveGame()
```

### C4. Radar → Police Chase Flow

```
RadarSpawner.Update()
  ├─ timer-based spawn (scaled by log10(MPS))
  ├─ Guards: max 1 alive, not during chase/popup
  └─ Instantiate Radar with random L/R side

Radar.Update()
  ├─ Scrolls -Z at road speed
  ├─ IF tapped → Radar.OnTapped()
  │   └─ PopularityManager.AddPopularity(-0.01, "RadarDefuse")
  │       + shake/vanish animation → Destroy
  │
  └─ IF reaches despawnZ → Radar.OnMissed()
      ├─ PopularityManager.AddPopularity(+0.01, "RadarMiss")
      ├─ PopularityManager.NotifyRadarPhotoTaken()
      │   └─ static event OnRadarPhotoTaken
      └─ RadarPopupController.ShowSnapshot(side)

PoliceCatchTrigger.HandleRadarPhotoTaken()
  ├─ radarCatchCounter++
  ├─ threshold = GetThresholdForStage(currentStage)
  │   Stage1=13, Stage2=11, Stage3=9, Stage4=7, Stage5=5, Stage6=3
  ├─ IF counter >= threshold:
  │   ├─ radarCatchCounter = 0
  │   └─ pendingPoliceCatch = true
  │
  └─ When RadarPopupController.OnRadarPopupClosed fires AND pendingPoliceCatch:
      └─ PoliceCatchController.StartChase()

PoliceCatchController Chase Sequence:
  Enter → PromptLoop (10 rounds, "Nx" = tap N times in N×0.5s) → Success/Fail
  ├─ Sets TapInputRaycaster.isPoliceChaseActive = true (isolates taps)
  ├─ Enables CurrencyManager.suppressTopBarMoneyUpdates (buffers MPS income)
  ├─ SUCCESS: stage-scaled nitro (3-25) + money/8 bonus
  └─ FAIL: stage-scaled penalty (keep 50%-90% of money)
      After exit → CommitBufferedEarnings()
```

### C5. Chest Flow

```
ChestSpawner → spawns chest on road (20-40s interval, frozen during UI)
  │
  └─ Player taps chest → ChestPopupController shows popup
      └─ Confirm → ChestInventoryManager.AddChestFromWorld(Chest)
          ├─ Adds ChestData to queue (Idle state)
          ├─ StartUnlockOldest() → Unlocking (timer countdown)
          └─ Timer expires → ReadyToOpen
              │
              ├─ Path A: Open normally → SetPendingOpenChest() writes to PlayerPrefs
              │   └─ SceneManager.LoadScene("ChestOpenScene")
              │       └─ ChestOpenSceneController.Start()
              │           ├─ ReadAndClearPendingChest()
              │           ├─ ComputeRewards():
              │           │   ├─ Money = floor(MPS × random(30,120))
              │           │   ├─ Nitro = random from NitroAmounts[]
              │           │   └─ Card = PickWeightedCard()
              │           │       ├─ weight = RarityBaseWeights[rarity] × LevelDecay(level)
              │           │       ├─ Segments = CardDropTuning.GetCardDropMultiplier(level)
              │           │       │   Possible: {1, 2, 4, 8} with level-decaying weights
              │           │       └─ First card obtain → auto-unlock to L1
              │           ├─ 7-tap reveal: Intro → 3-tap open → Money → Nitro → Card → Summary → Exit
              │           └─ GrantChestRewards() → CurrencyManager + CardManager
              │               → SceneManager.LoadScene("Main")
              │
              └─ Path B: OpenNowByNitro(15) → skip timer, same open flow
```

### C6. Card Upgrade Flow

```
CardManager.TryUpgradeCard(type)
  ├─ CardDefinition.currentCopies >= SegmentsPerUpgrade (8)?
  ├─ CurrencyManager.TrySpendMoney(upgradeCost)?
  ├─ currentCopies -= 8
  ├─ currentLevel++
  ├─ ApplyCardEffect(type, level) → dispatches to dedicated controller:
  │   ├─ TurboFinger → TurboFingerController (level stored; multiplier table lookup)
  │   ├─ GarageManager → GarageManagerController (level scales bonus & threshold)
  │   ├─ NitroRain → NitroRainController (level scales duration & collect count)
  │   ├─ PitStopCrew → PitStopCrewController (level scales efficiency & cap)
  │   ├─ SmallInvestment → SmallInvestmentController (level scales refund %)
  │   ├─ Momentum → MomentumController (level scales window, stack cap, per-stack bonus)
  │   ├─ NitroMagnet → NitroMagnetController (level scales taps & quota)
  │   └─ BoostMode → BoostModeController (level scales multiplier, duration, cooldown, charge cost)
  ├─ Fire OnCardsChanged event
  └─ SaveSystem.SaveGame()
```

---

## D. Data Model & State

### D1. Runtime Singletons (DontDestroyOnLoad)

| Instance                  | Key Mutable State                                                                                                                                                                                    |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CurrencyManager`         | `_money` (double), `moneyPerTap`, `moneyPerSecond`, `nitroCoins`, `incomeBoostMultiplier`, `totalMoneyEarned`, `passiveBuffer`, `bufferedEarnings`, `suppressTopBarMoneyUpdates`, `IsApplyingRefund` |
| `BuildingManager`         | `buildings[]` array of `BuildingDefinition` (each has `currentCount`, `baseCost`, `baseMPS`, `tapBonusPerLevel`)                                                                                     |
| `CardManager`             | `cards[]` array of `CardDefinition` (each has `currentLevel`, `currentCopies`, `cardType`, `rarity`)                                                                                                 |
| `ChestInventoryManager`   | `List<ChestData>` queue; each has `state` (Idle/Unlocking/ReadyToOpen/Opened), `remainingTime`, `skipUsed`                                                                                           |
| `BoostModeController`     | `_state` enum, `currentCharge`, `maxCharge`, boost params per level, cooldown/active timers                                                                                                          |
| `PoliceCatchController`   | `_chasePhase` enum, prompt list, chase active flag, scene references (rebindable)                                                                                                                    |
| `PopularityManager`       | `_popularity01` (float 0–1), stage enum                                                                                                                                                              |
| `PoliceCatchTrigger`      | `_radarCatchCounter`, `_pendingPoliceCatch`                                                                                                                                                          |
| `SaveSystem`              | No significant state; orchestrates load/save order                                                                                                                                                   |
| `GarageManagerController` | `_currentState`, `_spentSinceLastTrigger`, `_currentBonusMps`, timers                                                                                                                                |
| `NitroRainController`     | `_currentState`, `_collectedCount`, spawn timers, boost coordination queue                                                                                                                           |
| `PitStopCrewController`   | `_lastKnownMps`, `_hasGrantedThisSession`                                                                                                                                                            |

### D2. Scene-Local Components

| Component                  | Lives In                  | Notes                                      |
| -------------------------- | ------------------------- | ------------------------------------------ |
| `TapInputRaycaster`        | Main scene                | Raycasts, routes taps                      |
| `PanelManager`             | Main scene                | Panel switching                            |
| `RadarSpawner`             | Main scene                | Timer + spawn                              |
| `NitroCoinSpawner`         | Main scene                | Timer + spawn                              |
| `ChestSpawner`             | Main scene                | Coroutine spawn loop                       |
| `Radar`                    | Main scene (instantiated) | Per-instance scroll + miss/tap             |
| `NitroCoin`                | Main scene (instantiated) | Per-instance scroll + magnet state machine |
| `ChestOpenSceneController` | ChestOpenScene            | 7-tap reveal flow                          |
| `GarageController`         | Garage panel              | Car switching, customization               |
| `UpgradeButton`            | Main scene UI             | Per-button upgrade flow                    |

### D3. Persistence Format (PlayerPrefs)

| Key Pattern                   | Type            | Example                             |
| ----------------------------- | --------------- | ----------------------------------- |
| `Save_Money`                  | string (double) | "12345.0"                           |
| `Save_MPS`                    | string (double) | "50.5"                              |
| `Save_MPT`                    | string (double) | "3.0"                               |
| `Save_TotalMoney`             | string (double) | "99999.0"                           |
| `Save_NitroCoins`             | int             | 42                                  |
| `Save_BuildingID_{id}_Count`  | int             | building count per integer ID       |
| `Save_Card_{TypeName}_Level`  | int             | card level                          |
| `Save_Card_{TypeName}_Copies` | int             | card copies (segments)              |
| `Save_Popularity01`           | float           | 0.0–1.0                             |
| `Save_ChestBlob`              | string (JSON)   | serialized chest queue              |
| `Save_PendingOpenChest`       | string          | chest name for cross-scene hand-off |
| `Save_BoostState`             | string (JSON)   | boost controller state + timers     |
| `Save_RadarCatchCounter`      | int             | catches since last police chase     |
| `Save_PendingPoliceCatch`     | int (0/1)       | pending chase flag                  |
| `Save_NitroMagnet_*`          | int/int/int/int | tap count, armed, quota, collected  |
| `PitStop_LastQuitTimestamp`   | string (long)   | Unix epoch                          |
| `PitStop_LastExitMps`         | string (double) | MPS snapshot at quit                |
| `Save_Upgrade_{name}_Level`   | int             | upgrade button level                |

### D4. Static Data

| Asset                           | Description                                                                                                                                           |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CardDropTuning` (static class) | `SegmentsPerUpgrade=8`, rarity weights [50,30,15,5], `DecayFactor=0.25`, `DecayFloor=0.15`, segment multipliers {1,2,4,8} with level-decaying weights |
| `BuildingType` (enum)           | IDs 0–27 mapping to building names                                                                                                                    |
| `CardType` (enum)               | 8 card types                                                                                                                                          |
| `GarageDatabaseSO`              | List of `CarDataSO` assets, 18 global part keys, 6 default sticker keys                                                                               |
| `CarDataSO`                     | Per-car: identity, base stats (0–15), 6 colors, 6 stickers, 18 parts, 36 generated materials                                                          |

---

## E. Event & Update Mechanisms

### E1. C# Events (delegate / Action)

| Publisher                   | Event                                                                                                                     | Subscribers                                                      |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| `CurrencyManager`           | `OnMoneySpent(double)`                                                                                                    | `SmallInvestmentController`, `GarageManagerController`           |
| `CurrencyManager`           | `OnNitroCoinsSpent(int)`                                                                                                  | `SmallInvestmentController`                                      |
| `BuildingManager`           | `OnBuildingPurchased(type, count)`                                                                                        | UI refreshes                                                     |
| `CardManager`               | `OnCardsChanged`                                                                                                          | UI refreshes                                                     |
| `PopularityManager`         | `OnPopularityChanged(float)`                                                                                              | UI updates                                                       |
| `PopularityManager`         | `OnRadarPhotoTaken` (static)                                                                                              | `PoliceCatchTrigger.HandleRadarPhotoTaken()`                     |
| `RadarPopupController`      | `OnRadarPopupClosed` (static)                                                                                             | `PoliceCatchTrigger.HandleRadarPopupClosed()`                    |
| `BoostModeController`       | `OnBoostStarted` / `OnBoostEnded` / `OnBoostCooldownStarted` / `OnBoostChargeChanged` / `OnBoostReady` / `OnStateChanged` | `NitroRainController` (mutual exclusion), UI                     |
| `TurboFingerController`     | `OnActivated` / `OnEffectEnded` / `OnCooldownEnded`                                                                       | UI                                                               |
| `NitroRainController`       | `OnDelayStarted` / `OnRainStarted` / `OnRainEnded`                                                                        | UI                                                               |
| `GarageManagerController`   | `OnActivated` / `OnEnded` / `OnCooldownEnded`                                                                             | UI                                                               |
| `MomentumController`        | `OnMomentumChanged(stacks, multiplier)`                                                                                   | UI                                                               |
| `SmallInvestmentController` | `OnRefundApplied(amount, currencyType)`                                                                                   | UI                                                               |
| `PitStopCrewController`     | `OnOfflineEarningsComputed` / `OnCountUpComplete`                                                                         | UI                                                               |
| `SaveSystem`                | `OnGameLoaded` (static)                                                                                                   | `SmallInvestmentController.LateSubscribe()`, general system init |

### E2. Update-Driven Loops

| Class                     | What Runs in Update                                                     |
| ------------------------- | ----------------------------------------------------------------------- |
| `CurrencyManager`         | Boost timer countdown, passive MPS accumulation, MPS measurement window |
| `TapInputRaycaster`       | Input polling (New Input System / legacy fallback), raycast routing     |
| `RadarSpawner`            | Spawn timer (with `UIFlowState.IsSpawnSuppressed` gate)                 |
| `NitroCoinSpawner`        | Spawn timer (same gate)                                                 |
| `Radar`                   | Z-axis movement + despawn check                                         |
| `NitroCoin`               | Magnet state machine (None/Drift/Pull) or normal Z-movement             |
| `TurboFingerController`   | Active → check duration end → Cooldown; Cooldown → check end → Ready    |
| `BoostModeController`     | State timer management (Active/Cooldown transitions)                    |
| `GarageManagerController` | Active → end check → Cooldown; Cooldown → end check → Ready             |
| `NitroMagnetController`   | Arm timeout check                                                       |
| `MomentumController`      | Stack inactivity reset (window exceeded → stacks = 0)                   |
| `PitStopCrewController`   | MPS snapshot update (throttled 2×/sec)                                  |
| `ChestInventoryManager`   | Unlock timer countdown for Unlocking chests                             |
| `UpgradeButton`           | Afford-check for button interactability                                 |

### E3. Coroutine-Based Sequences

| Class                   | Coroutine                            | Purpose                                                                        |
| ----------------------- | ------------------------------------ | ------------------------------------------------------------------------------ |
| `PoliceCatchController` | `RunChaseSequence()`                 | Full chase flow: enter animation → prompt loop → success/fail → exit animation |
| `ChestSpawner`          | `SpawnLoop()`                        | Infinite loop with manual timer (freezable)                                    |
| `PitStopCrewController` | `TryGrantOfflineEarningsOnStartup()` | Waits for systems ready → computes offline earnings                            |
| `NitroMagnetController` | `BoundsCheckLoop()`                  | 0.1s periodic check for coins entering magnet area                             |
| `CurrencyManager`       | `AnimateMoneyAddition()`             | Count-up animation over duration                                               |
| `PoliceCatchTrigger`    | `DeferredStartChase()`               | Small delay after popup close before chase starts                              |

---

## F. Player Progression & Balance Hooks

### F1. Income Multiplier Stack (Car Tap)

The final tap reward is computed in `TapInputRaycaster`:

```
finalTapReward = (moneyPerTap + cardBonus)
                  × incomeBoostMultiplier       ← BoostModeController (3×–20×)
                  × turboMultiplier              ← TurboFingerController (2×–14×)
                  × momentumMultiplier           ← MomentumController (1×–2.2×)
```

**Theoretical max burst:** At max card levels with all actives rolling: `base × 20 × 14 × 2.2 = base × 616`. This is time-gated by activation conditions, cooldowns, and the short uptime windows.

### F2. Building Cost Curves

Cost grows exponentially: `baseCost × tierMultiplier^count`

| Building IDs | Tier Multiplier | Growth Speed |
| ------------ | --------------- | ------------ |
| 0–5          | 1.15            | Gentle       |
| 6–12         | 1.17            | Moderate     |
| 13–20        | 1.20            | Steep        |
| 21–27        | 1.25            | Very steep   |

Progression lock: building N requires building N−1 to have count ≥ 1. This creates a forced linear unlock order.

### F3. Card Drop Economy

- **8 segments per upgrade** (constant via `CardDropTuning.SegmentsPerUpgrade`)
- **Rarity weights:** Common=50, Rare=30, Epic=15, Legendary=5
- **Level decay on selection:** `weight × max(0.15, 1/(1 + level × 0.25))` — higher-level cards are picked less frequently
- **Segment multiplier per drop:** {1, 2, 4, 8} with weights that decay by level — at high levels, ×8 drops become very rare
- **Card auto-unlock:** First copy of any card immediately sets level to 1 (unlocks the effect)
- **Infinite level cap:** Cards can upgrade indefinitely; parameter arrays are clamped to max index (typically level 6)

### F4. Popularity → Danger Escalation

| Stage  | Popularity Range | Radar Catches to Police | Police Fail Penalty (money kept) | Police Success Nitro |
| ------ | ---------------- | ----------------------- | -------------------------------- | -------------------- |
| Stage1 | 0–17             | 13                      | 90%                              | 3                    |
| Stage2 | 18–35            | 11                      | 83%                              | 7                    |
| Stage3 | 36–53            | 9                       | 76%                              | 12                   |
| Stage4 | 54–71            | 7                       | 68%                              | 17                   |
| Stage5 | 72–89            | 5                       | 60%                              | 22                   |
| Stage6 | 90–100           | 3                       | 50%                              | 25                   |

Each missed radar adds +1 popularity (0.01 normalized). Each tapped radar subtracts −1. Higher popularity = more frequent and more dangerous police chases, but higher rewards on success.

### F5. Boost Mode Gating

Nitro coins → charge → boost. Level-based params:

| Level | Multiplier | Duration | Cooldown | Charge Cost |
| ----- | ---------- | -------- | -------- | ----------- |
| 1     | 3×         | 6s       | 60s      | 5           |
| 2     | 5×         | 8s       | 50s      | 7           |
| 3     | 8×         | 10s      | 45s      | 9           |
| 4     | 12×        | 12s      | 35s      | 12          |
| 5     | 16×        | 14s      | 30s      | 15          |
| 6     | 20×        | 16s      | 25s      | 18          |

Auto-starts when Ready (no manual activation needed after charging).

### F6. Cross-System Interactions

1. **Boost ↔ NitroRain mutual exclusion:** Rain cannot run while Boost is Active; queued and auto-starts when Boost ends.
2. **Police Chase ↔ Spawners:** Radar, nitro, and chest spawners all gate on `PoliceCatchController.IsChaseActive`.
3. **Police Chase ↔ CurrencyManager suppress:** During chase, `suppressTopBarMoneyUpdates = true` buffers all MPS income. Committed or flushed after chase exit.
4. **SmallInvestment ↔ CurrencyManager loop guard:** `IsApplyingRefund` flag prevents refund money adds from re-firing `OnMoneySpent`.
5. **GarageManagerController bonus MPS** is added directly in `CurrencyManager.Update()` via `GarageManagerController.Instance.CurrentBonusMps`.
6. **UpgradeButton.ReapplyEffect()** is called during load, after `BuildingManager.RecalculateMPSFromBuildings()`, to additively restore upgrade contributions.

---

## G. Onboarding Reading Order

For a new engineer jumping into this codebase, read the files in this order:

### Phase 1: Core Loop (understand money flow)

1. **`UIFlowState.cs`** — 40 lines. Static flags that gate everything.
2. **`CurrencyManager.cs`** — Central economy. Read `AddMoney()`, `TrySpendMoney()`, the Update loop, and the suppress/buffer system.
3. **`TapInputRaycaster.cs`** — Entry point for all player input. Follow the raycast → reward chain.
4. **`BuildingManager.cs`** — How buildings produce MPS and their cost curves.
5. **`UpgradeButton.cs`** — Simple Tap/MPS/Global upgrades.

### Phase 2: Persistence & Boot

6. **`SaveSystem.cs`** — Read `LoadGame()` carefully. The load order (chests → economy → upgrades → buildings → cards → popularity → police) is critical. Note the `OnGameLoaded` event.

### Phase 3: Card System

7. **`CardManager.cs`** — Card definitions, 8-segment model, `ApplyCardEffect()` dispatch.
8. **`CardDropTuning.cs`** — Rarity weights, decay formulas, segment multipliers.
9. **Read each card controller** in any order:
   - `TurboFingerController.cs` — Tap multiplier with state machine
   - `MomentumController.cs` — Combo stacking
   - `SmallInvestmentController.cs` — Spend refund
   - `GarageManagerController.cs` — Spend-threshold bonus MPS
   - `NitroRainController.cs` — Nitro coin rain cycle
   - `NitroMagnetController.cs` — Magnet area pull
   - `PitStopCrewController.cs` — Offline earnings
   - `BoostModeController.cs` — Boost mode state machine

### Phase 4: Risk/Reward Systems

10. **`PopularityManager.cs`** — 6-stage danger scale.
11. **`Radar.cs`** + **`RadarSpawner.cs`** — Road hazard spawning and miss/tap handling.
12. **`PoliceCatchTrigger.cs`** — Radar counter → pending police chase link.
13. **`PoliceCatchController.cs`** — Chase minigame coroutine (long file; focus on `RunChaseSequence()`).

### Phase 5: Chest System

14. **`ChestInventoryManager.cs`** — Queue, unlock timer, cross-scene handoff.
15. **`ChestOpenSceneController.cs`** — 7-tap reveal, `ComputeRewards()`, `PickWeightedCard()`.
16. **`ChestSpawner.cs`** + **`ChestMover.cs`** — World spawning.

### Phase 6: Cosmetics & Garage

17. **`GarageDatabaseSO.cs`** + **`CarDataSO.cs`** — Data model.
18. **`GarageController.cs`** — Car switching and customization.
19. **`CarCustomizer.cs`** — Skin/part application.

### Phase 7: World Objects & Spawners

20. **`NitroCoinSpawner.cs`** + **`NitroCoin.cs`** — Nitro coin lifecycle.
21. **`PanelManager.cs`** — UI panel switching.
22. **`GameManager.cs`** — Thin shell (read last, it's nearly empty).

---

_Analysis generated from codebase read on the current state of the repository. No modifications made._
