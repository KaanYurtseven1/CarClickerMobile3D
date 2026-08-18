# Balance Changes Summary — Car Clicker Mobile 3D

All 15 balance changes implemented. Zero new gameplay systems. Public APIs preserved.

---

## A) Inspector-Only Changes Checklist

These values are exposed as `[SerializeField]` fields and can be fine-tuned in the Unity Inspector without recompiling.

| #   | Component / Script             | Field(s)                                          | Old Default | New Default   | Notes                                     |
| --- | ------------------------------ | ------------------------------------------------- | ----------- | ------------- | ----------------------------------------- |
| 1   | **NitroCoinSpawner**           | `minSpawnInterval`                                | 20          | **60**        | Reduce nitro coin flood                   |
| 1   | **NitroCoinSpawner**           | `maxSpawnInterval`                                | 45          | **90**        |                                           |
| 7   | **RadarSpawner**               | `minSpawnInterval`                                | _(new)_     | **8**         | Progression radar pacing                  |
| 7   | **RadarSpawner**               | `mpsForFastest`                                   | _(new)_     | **50000**     | MPS at which interval is fastest          |
| 7   | **RadarSpawner**               | `enableProgressionScaling`                        | _(new)_     | **true**      | Toggle progression logic                  |
| 11  | **DailyOffersController**      | `slot2PriceStep`                                  | _(new)_     | **5**         | Price increase per purchase               |
| 11  | **DailyOffersController**      | `slot3PriceStep`                                  | _(new)_     | **10**        |                                           |
| 11  | **DailyOffersController**      | `copiesStepPerPurchase`                           | _(new)_     | **1**         | Extra copies each buy                     |
| 14  | **ChestOpenSceneController**   | `chestGoldMPSMin`                                 | _(new)_     | **30**        | Min MPS multiplier for gold               |
| 14  | **ChestOpenSceneController**   | `chestGoldMPSMax`                                 | _(new)_     | **120**       | Max MPS multiplier for gold               |
| 15  | **CardDefinition** (each card) | `costMultiplierMid`                               | _(new)_     | **1.6**       | L7–12 upgrade cost curve                  |
| 15  | **CardDefinition** (each card) | `costMultiplierLate`                              | _(new)_     | **1.85**      | L13+ upgrade cost curve                   |
| 13  | **BuildingManager**            | _(Inspector baseCost on each BuildingDefinition)_ | varies      | **8–12× gap** | Use context menu "Validate BaseCost Gaps" |

---

## B) Code Changes Checklist

| #    | File                          | Change Description                                                                                                                                                                                       |
| ---- | ----------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | `NitroCoinSpawner.cs`         | Default intervals: 20→60 / 45→90                                                                                                                                                                         |
| 2    | `BoostModeController.cs`      | `GetBoostParamsForLevel()` → 4-tuple `(multiplier, cooldown, maxCharge, duration)`. New balanced table L1–L6. Legacy wrapper kept. `RefreshUnlockState()` sets `boostDurationSeconds` from level params. |
| 3    | `TurboFingerController.cs`    | `LevelMultipliers` array: `{1,5,10,20,50,100,200}` → `{1,2,3,5,7,10,14}`                                                                                                                                 |
| 4    | `BuildingManager.cs`          | New `GetTieredCostMultiplier()` — IDs 0–5: 1.15, 6–12: 1.17, 13–20: 1.20, 21–27: 1.25                                                                                                                    |
| 5+14 | `ChestOpenSceneController.cs` | Removed old money-multiplier system. Replaced with MPS-based gold: `floor(MPS × random(30,120))`. New serialized fields `chestGoldMPSMin`/`chestGoldMPSMax`.                                             |
| 6    | `PoliceCatchController.cs`    | New `GetStageScaledPenalty()` — Stage1: 0.90 → Stage6: 0.50 (replaces flat `failMoneyMultiplier`)                                                                                                        |
| 7    | `RadarSpawner.cs`             | New `GetProgressionScaledInterval()` — log10-based lerp from `spawnInterval` → `minSpawnInterval` based on MPS                                                                                           |
| 8    | `NitroRainController.cs`      | `RequiredCollects` — `{0,3,4,5,6,7,8}` → `{0,5,7,9,11,13,16}`                                                                                                                                            |
| 9    | `NitroMagnetController.cs`    | `tapsRequired` — `{30,40,50,55,60,70}` → `{40,50,60,70,80,90}`. `coinsToCollect` — `{3,4,5,7,9,12}` → `{2,3,4,5,7,9}`                                                                                    |
| 10   | `PoliceCatchController.cs`    | New `GetStageScaledRewardCoins()` — Stage1: 3 → Stage6: 25 nitro coins on chase success. Added `cm.AddNitroCoins()` call. `SpawnRewardCoins()` uses stage count.                                         |
| 11   | `DailyOffersController.cs`    | Per-purchase scaling: `scaledPrice = base + (totalPurchases × step)`. `scaledCopies = base + (totalPurchases × copiesStep)`. New PlayerPrefs keys for persistence.                                       |
| 12   | `ChestPopupController.cs`     | Open-now nitro cost: 3 → **15**                                                                                                                                                                          |
| 13   | `BuildingManager.cs`          | New `[ContextMenu("Validate BaseCost Gaps (8-12x)")]` validation method                                                                                                                                  |
| 15   | `CardDefinition.cs`           | `GetUpgradeCost()` now uses banded multipliers: L1–6 = `costMultiplier` (1.4), L7–12 = `costMultiplierMid` (1.6), L13+ = `costMultiplierLate` (1.85). Loop-based calculation with `Math.Floor`.          |

---

## C) Final Tuning Values Table

### Boost Mode (Change 2)

| Level | Multiplier | Duration (s) | Cooldown (s) | Max Charge |
| ----- | ---------- | ------------ | ------------ | ---------- |
| 1     | 3×         | 6            | 60           | 5          |
| 2     | 5×         | 8            | 55           | 8          |
| 3     | 8×         | 10           | 45           | 10         |
| 4     | 12×        | 12           | 35           | 13         |
| 5     | 16×        | 14           | 30           | 16         |
| 6     | 20×        | 16           | 25           | 18         |

### Turbo Finger Multipliers (Change 3)

| Level   | 0   | 1   | 2   | 3   | 4   | 5   | 6   |
| ------- | --- | --- | --- | --- | --- | --- | --- |
| Old     | 1   | 5   | 10  | 20  | 50  | 100 | 200 |
| **New** | 1   | 2   | 3   | 5   | 7   | 10  | 14  |

### Building Cost Multiplier Tiers (Change 4)

| Building IDs                               | Cost Multiplier |
| ------------------------------------------ | --------------- |
| 0–5 (StreetDeals → GasStation)             | 1.15            |
| 6–12 (ElectricCharging → TireShop)         | 1.17            |
| 13–20 (Showroom → DealershipHQ)            | 1.20            |
| 21–27 (MotorSportsArena → UltimateFactory) | 1.25            |

### Police Penalty Scaling (Change 6)

| Stage               | Penalty (money kept) |
| ------------------- | -------------------- |
| Stage1 (pop 0–17)   | 90%                  |
| Stage2 (pop 18–35)  | 85%                  |
| Stage3 (pop 36–53)  | 80%                  |
| Stage4 (pop 54–71)  | 72%                  |
| Stage5 (pop 72–89)  | 60%                  |
| Stage6 (pop 90–100) | 50%                  |

### Police Reward Scaling (Change 10)

| Stage  | Nitro Coins on Success |
| ------ | ---------------------- |
| Stage1 | 3                      |
| Stage2 | 5                      |
| Stage3 | 8                      |
| Stage4 | 12                     |
| Stage5 | 18                     |
| Stage6 | 25                     |

### Nitro Rain Thresholds (Change 8)

| Level   | 0   | 1   | 2   | 3   | 4   | 5   | 6   |
| ------- | --- | --- | --- | --- | --- | --- | --- |
| Old     | 0   | 3   | 4   | 5   | 6   | 7   | 8   |
| **New** | 0   | 5   | 7   | 9   | 11  | 13  | 16  |

### Nitro Magnet (Change 9)

| Level           | 1   | 2   | 3   | 4   | 5   | 6   |
| --------------- | --- | --- | --- | --- | --- | --- |
| Taps (old)      | 30  | 40  | 50  | 55  | 60  | 70  |
| **Taps (new)**  | 40  | 50  | 60  | 70  | 80  | 90  |
| Coins (old)     | 3   | 4   | 5   | 7   | 9   | 12  |
| **Coins (new)** | 2   | 3   | 4   | 5   | 7   | 9   |

### Card Upgrade Cost Bands (Change 15)

| Level Range | Multiplier per Level |
| ----------- | -------------------- |
| 1–6         | 1.4× (base)          |
| 7–12        | 1.6× (mid)           |
| 13+         | 1.85× (late)         |

Example costs (baseUpgradeCost = 10):

- L1→2: 10 × 1.4^1 = **14**
- L6→7: 10 × 1.4^6 = **75**
- L7→8: 75 × 1.6 = **120**
- L12→13: 75 × 1.6^6 = **1258**
- L13→14: 1258 × 1.85 = **2327**
- L18→19: cost ≈ **51,600**

### Daily Offer Scaling (Change 11)

| Purchase # | Slot 2 Price | Slot 3 Price | Copies    |
| ---------- | ------------ | ------------ | --------- |
| 1st        | 15           | 30           | 5         |
| 2nd        | 20           | 40           | 6         |
| 3rd        | 25           | 50           | 7         |
| 4th        | 30           | 60           | 8         |
| nth        | 15 + 5(n-1)  | 30 + 10(n-1) | 5 + (n-1) |

---

## D) Quality Checks

### Edge Cases Handled

- **Zero MPS at game start**: Chest gold = `floor(0 × rand) = 0`. Safe — first chests won't grant money until player has MPS.
- **Police at Stage1**: Penalty is only 10% loss (generous for new players).
- **Boost 4-tuple backward compat**: `GetBoostParamsForLevelLegacy()` wrapper exists. Named tuple fields preserved.
- **DailyOffer totalPurchases overflow**: Linear growth (5/10 per purchase), 100 buys = 515/1030 nitro cost — self-limiting by economy.
- **Card cost at very high levels**: L30 cost ≈ baseUpgradeCost × 1.4^6 × 1.6^6 × 1.85^18 ≈ 10 × 7.53 × 16.78 × 2,614,337 = **~3.3 billion** — appropriate for infinite progression.
- **Radar scaling with 0 MPS**: `log10(0)` is guarded — returns base `spawnInterval` unmodified.
- **Building tier fallback**: IDs > 27 fall back to per-building `costMultiplier` field.
- **PlayerPrefs for daily purchases**: `slot2TotalPurchases`/`slot3TotalPurchases` persist across daily resets intentionally (escalating economy). Card _selection_ still resets daily.

### Manual Test Steps

1. **NitroCoin Spawner**: Play for 5 min → nitro coins should appear every ~60–90s (not every 20s).
2. **Boost Mode**: Activate boost at L1 → should be 3× for 6s, 60s cooldown, 5 charges.
3. **Turbo Finger**: Tap 50× in 15s → L1 multiplier should show 2× (not 5×).
4. **Building Costs**: Buy building ID=7 → cost should grow at 1.17× per purchase.
5. **Chest Gold**: Open chest with MPS=100 → gold reward should be 3,000–12,000.
6. **Police Fail (Stage 3)**: Fail police event → should lose 20% money.
7. **Police Success (Stage 4)**: Succeed → should get 12 nitro coins + money/8.
8. **Radar Pacing**: With MPS=50,000 → radar interval should approach 8s.
9. **Nitro Rain**: At card L1, collect 5 nitro coins in sequence → rain triggers (was 3).
10. **Nitro Magnet**: At card L1, need 40 taps to arm (was 30), grants 2 coins (was 3).
11. **Daily Offer**: Buy slot 2 twice in a row → 2nd purchase should cost 20 (was 15).
12. **Chest Open-Now**: Open-now button should require 15 nitro (was 3).
13. **BaseCost Gaps**: Select BuildingManager → right-click → "Validate BaseCost Gaps (8-12x)" → fix any warnings.
14. **Card Upgrade**: Upgrade a card to L7 → verify cost jump from 1.4× to 1.6× band.
15. **DailyOffer persistence**: Buy slot 3, restart app → totalPurchases should persist, next price = 40.

### Files Modified (12 total)

1. `Assets/Scripts/NitroCoinSpawner.cs`
2. `Assets/Scripts/BoostModeController.cs`
3. `Assets/Scripts/TurboFingerController.cs`
4. `Assets/Scripts/BuildingManager.cs`
5. `Assets/Scripts/ChestOpenSceneController.cs`
6. `Assets/Scripts/PoliceCatchController.cs`
7. `Assets/Scripts/RadarSpawner.cs`
8. `Assets/Scripts/NitroRainController.cs`
9. `Assets/Scripts/NitroMagnetController.cs`
10. `Assets/Scripts/DailyOffersController.cs`
11. `Assets/Scripts/ChestPopupController.cs`
12. `Assets/Scripts/CardDefinition.cs`

### Files NOT Modified (no changes needed)

- `Chest.cs` — world chest object unchanged; gold is computed in ChestOpenSceneController
- `CurrencyManager.cs` — read-only usage, no changes
- `PopularityManager.cs` — data source only
- `BoostModeEffectsIntegration.cs` — accesses `.multiplier` by name, still valid with 4-tuple
- `Radar.cs` — individual radar behavior unchanged
- `ChestRewardPackage.cs` — static arrays unchanged
- `CardDropTuning.cs` — segment logic unchanged
