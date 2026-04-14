# FULL ECONOMY + PROGRESSION + RESOURCE FLOW AUDIT

**Project:** CarClickerMobile3D  
**Date:** 2025  
**Scope:** Every gold source, gold sink, nitro coin source, nitro coin sink, timer, cooldown, spawn rate, placeholder value, cross-system interaction, retention concern, and balance gap in the entire project.  
**Status:** READ-ONLY ANALYSIS — NO CODE CHANGES

---

## A. EXECUTIVE SUMMARY

The game has **three currencies** (Gold, Nitro Coins, Premium/Diamonds), **28 buildings**, **8 card-based abilities**, **3 upgrade tracks**, a **chest gacha system**, a **6-tier blacklist campaign**, **7 garage cars** with cosmetics, **police chase** mechanics, and a **radar/popularity** pressure system. There is **no prestige/reset** mechanic.

### Critical Findings at a Glance

| #   | Finding                                                                                                                 | Severity       |
| --- | ----------------------------------------------------------------------------------------------------------------------- | -------------- |
| 1   | **No prestige system** — infinite progression with no reset loop                                                        | Design Gap     |
| 2   | **AdProvider is a dummy** — always auto-succeeds, no real ad SDK                                                        | Placeholder    |
| 3   | **Free Chest in Daily Offers is not implemented** — 1/3 chance of giving nothing                                        | Bug            |
| 4   | **Spoiler_5 costs 5 quadrillion gold** — sentinel value, not a real price                                               | Placeholder    |
| 5   | **Color #6 and Sticker #6 cost MAX_INT** (2.1B nitro) — unpurchasable sentinels                                         | Placeholder    |
| 6   | **PitStopCrew offline % conflict**: CardManager says 5%/level, Controller says 20-85%/level                             | Conflict       |
| 7   | **Building cost ratios are 4-6.67×** between tiers but validator expects 8-12×                                          | Balance Gap    |
| 8   | **Legendary card rarity weight = 0** in base card drops — completely disabled                                           | Design Choice? |
| 9   | **Tap income becomes irrelevant** mid-game — base moneyPerTap = 1.0 vs millions MPS                                     | Balance Gap    |
| 10  | **No diamond/premium currency sink** exists anywhere in the code                                                        | Missing System |
| 11  | **Blacklist tier missions** are Inspector-serialized — values not in code, cannot audit targets                         | Data Gap       |
| 12  | **Scene costMultiplier vs tiered override** — scene values (1.10-1.15) are silently replaced by tier system (1.15-1.25) | Confusing      |

---

## B. FULL GOLD SOURCES LIST

### B.1 Tapping (Primary Active Source)

| Component            | Formula                                                                                 | Source File          |
| -------------------- | --------------------------------------------------------------------------------------- | -------------------- |
| Base tap             | `moneyPerTap = 1.0 + buildingTapIncome`                                                 | CurrencyManager.cs   |
| Building tap bonus   | StreetDeals (ID 0): +1.0 per owned copy                                                 | BuildingManager.cs   |
| Upgrade tap bonus    | Tap upgrade: +1 per level, cost = 10 × 1.15^level                                       | UpgradeButton.cs     |
| Card bonus           | `turboFingerTapBonusCached` — currently always **0** (delegated to controller)          | CardManager.cs       |
| **Full tap formula** | `(moneyPerTap + cardTapBonus) × boostMultiplier × turboMultiplier × momentumMultiplier` | TapInputRaycaster.cs |

**Multiplier Stack (all multiplicative):**

| Multiplier         | Source                | Range                                    |
| ------------------ | --------------------- | ---------------------------------------- |
| boostMultiplier    | BoostModeController   | ×3 to ×20 (by level)                     |
| turboMultiplier    | TurboFingerController | ×2 to ×14 (by level)                     |
| momentumMultiplier | MomentumController    | ×1.15 to ×2.20 (by level, at max stacks) |

**Theoretical max single-tap multiplier:** ×20 × ×14 × ×2.20 = **×616** (all at L6, all active simultaneously).

> **📍 Where to change in Unity:**
>
> - `moneyPerTap` (base 1.0): Scene **Main** → GameObject **GameManager** → Component **CurrencyManager** → Field `moneyPerTap`
> - Building tap bonus (Street Deals): Scene **Main** → GameObject **GameManager** → Component **BuildingManager** → Field `buildings[0]` → `baseProduction`
> - Tap upgrade cost formula (`baseCost`, `costMultiplier`): **Code only** → `UpgradeButton.cs` — check if `[SerializeField]`; if present on the UpgradeButton prefab/scene instance
> - TurboFinger activation params: Scene **Main** → GameObject **TurboFingerCtrl** → Component **TurboFingerController** → Fields `tapWindowSeconds`, `tapsRequiredToActivate`, `activeDurationSeconds`, `cooldownDurationSeconds`
> - TurboFinger multipliers per level: **Code only** → `TurboFingerController.cs` → `LevelMultipliers` static readonly array
> - BoostMode Inspector params: Scene **Main** → GameObject **BoostModeController** → Component **BoostModeController** → Fields `boostDurationSeconds`, `cooldownSeconds`, `chargePerNitro`, `maxCharge`
> - BoostMode multipliers per level: **Code only** → `BoostModeController.cs` → `GetBoostParamsForLevel()` switch statement
> - Momentum Inspector params: Scene **Main** → GameObject **MomentumCtrl** → Component **MomentumController** → Fields `baseResetWindow`, `basePerStackBonus`, `baseStackCap`, `stackCapStep`

### B.2 Passive MPS (Primary Passive Source)

| Component            | Formula                                                                        | Source File                |
| -------------------- | ------------------------------------------------------------------------------ | -------------------------- |
| Base MPS             | `moneyPerSecond` (starts 0, +1 per MPS upgrade level)                          | CurrencyManager.cs         |
| Building MPS         | Σ(baseProduction × count) for each owned building                              | BuildingManager.cs         |
| Upgrade MPS bonus    | MPS upgrade: +1 per level, cost = 10 × 1.15^level                              | UpgradeButton.cs           |
| Global upgrade       | ×1.1^level (applies to both tap and MPS)                                       | UpgradeButton.cs           |
| GarageManager bonus  | ×10 to ×15 MPS for 60s when triggered                                          | GarageManagerController.cs |
| **Full MPS formula** | `(baseMps + garageBonusMps) × incomeBoostMultiplier × cardGlobalMpsMultiplier` | CurrencyManager.cs         |
| Tick rate            | Every frame; buffers fractional amounts, floors to integer when ≥1.0           | CurrencyManager.cs         |

> **📍 Where to change in Unity:**
>
> - `moneyPerSecond` (base): Scene **Main** → GameObject **GameManager** → Component **CurrencyManager** → Field `moneyPerSecond`
> - Building MPS values: Scene **Main** → GameObject **GameManager** → Component **BuildingManager** → Field `buildings[]` array → each entry's `baseProduction`
> - MPS upgrade cost formula: **Code only** → `UpgradeButton.cs` → `baseCost` / `costMultiplier`
> - Global upgrade multiplier (×1.1^level): **Code only** → `UpgradeButton.cs`
> - GarageManager MPS bonus: Scene **Main** → GameObject **GarageManagerCtrl** → Component **GarageManagerController** → Fields `activeDurationSeconds`, `cooldownSeconds`
> - GarageManager bonus multipliers per level: **Code only** → `GarageManagerController.cs` → `BonusMultipliers` static readonly array
> - `incomeBoostMultiplier`: Scene **Main** → GameObject **GameManager** → Component **CurrencyManager** → Field `incomeBoostMultiplier`

### B.3 Police Chase Escape Reward

| Stage | Money Reward              | Nitro Reward      | Source                   |
| ----- | ------------------------- | ----------------- | ------------------------ |
| All   | `floor(currentMoney / 8)` | See Nitro sources | PoliceCatchController.cs |

> **📍 Where to change in Unity:**
>
> - Chase reward formula (`currentMoney / 8`): **Code only** → `PoliceCatchController.cs` → `HandleChaseEnd()` method
> - `maxChaseDuration`: Scene **Main** → GameObject **GameManager** → Component **PoliceCatchController** → Field `maxChaseDuration`
> - `rewardCoinCount`: Scene **Main** → GameObject **GameManager** → Component **PoliceCatchController** → Field `rewardCoinCount`
> - Stage-scaled nitro rewards: **Code only** → `PoliceCatchController.cs` → `RewardCoinsByStage` hardcoded dictionary
> - `failMoneyMultiplier`: Scene **Main** → GameObject **GameManager** → Component **PoliceCatchController** → Field `failMoneyMultiplier`

### B.4 Chest Gold Rewards

| Chest Type | Min % of Balance | Max % of Balance | Source           |
| ---------- | ---------------- | ---------------- | ---------------- |
| Common     | 5%               | 15%              | ChestTypeDefs.cs |
| Rare       | 10%              | 25%              | ChestTypeDefs.cs |
| Legendary  | 20%              | 40%              | ChestTypeDefs.cs |

**Final value:** `RoundToClean10(currentMoney × Random(min%, max%))` — minimum 10 gold.

> **📍 Where to change in Unity:**
>
> - Chest gold min/max percentages: **Code only** → `ChestTypeDefs.cs` → `GetConfig()` static method (per chest type)
> - Rounding logic: **Code only** → `ChestTypeDefs.cs` → `RoundToClean10()` helper

### B.5 Blacklist Mission Rewards

Gold amounts are **Inspector-configured** per mission per tier (6 tiers × 5 missions). The `goldAmount` field exists on `BlacklistRewardDefinition` but actual values are serialized in the scene, not in code.

> **📍 Where to change in Unity:**
>
> - BlacklistTier mission definitions: Scene **Main** → GameObject **BlacklistManager** → Component **BlacklistManager** → Field `tierDefinitions[]` → each references a **BlacklistTier ScriptableObject** in `Assets/SO/` or `Assets/Prefabs/Blacklist/`
> - Individual tier assets: **Project Window** → `Assets/Prefabs/Blacklist/BlacklistTier_1.asset` through `BlacklistTier_5.asset` → edit mission targets and `goldAmount` rewards in Inspector

### B.6 Daily Offers (Free Slot)

| Reward Type | Range                    | Probability         |
| ----------- | ------------------------ | ------------------- |
| Gold        | `floor(Random(50, 200))` | 33%                 |
| NitroCoins  | `Random(1, 5)`           | 33%                 |
| Free Chest  | **NOT IMPLEMENTED**      | 33% → gives nothing |

> **📍 Where to change in Unity:**
>
> - `freeMoneyMin` / `freeMoneyMax`: Scene **Main** → GameObject **Section_DailyOffers** → Component **DailyOffersController** → Fields `freeMoneyMin` (50), `freeMoneyMax` (200)
> - `freeNitroMin` / `freeNitroMax`: Same GameObject → Fields `freeNitroMin` (1), `freeNitroMax` (5)
> - Free chest probability: **Code only** → `DailyOffersController.cs` → `GenerateFreeOffer()` method (hardcoded 1/3 split)

### B.7 PitStopCrew Offline Earnings

| Level | Efficiency | Max Hours | Formula                           |
| ----- | ---------- | --------- | --------------------------------- |
| 1     | 20%        | 2h        | `offlineSeconds × exitMPS × 0.20` |
| 2     | 30%        | 3h        | `offlineSeconds × exitMPS × 0.30` |
| 3     | 40%        | 4h        | `offlineSeconds × exitMPS × 0.40` |
| 4     | 55%        | 6h        | `offlineSeconds × exitMPS × 0.55` |
| 5     | 70%        | 8h        | `offlineSeconds × exitMPS × 0.70` |
| 6     | 85%        | 12h       | `offlineSeconds × exitMPS × 0.85` |

> **📍 Where to change in Unity:**
>
> - PitStopCrew efficiency per level: **Code only** → `PitStopCrewController.cs` → `EfficiencyByLevel[]` static readonly array
> - PitStopCrew cap hours per level: **Code only** → `PitStopCrewController.cs` → `CapHoursByLevel[]` static readonly array
> - `countUpDuration` (animation): Scene **Main** → GameObject **PitStopCrewCtrl** → Component **PitStopCrewController** → Field `countUpDuration`

### B.8 SmallInvestment Refund (Indirect Source)

Returns a percentage of **every spend** back to the player:

| Level | Refund % |
| ----- | -------- |
| 1     | 2%       |
| 2     | 4%       |
| 3     | 6%       |
| 4     | 8%       |
| 5     | 10%      |
| 6     | 12%      |

Applies to both gold AND nitro coin spends. Effectively reduces all costs by the refund percentage.

> **📍 Where to change in Unity:**
>
> - `basePercent` (2%): Scene **Main** → GameObject **SmallInvestmentCtrl** → Component **SmallInvestmentController** → Field `basePercent`
> - `stepPercent` (2% per level): Same GameObject → Field `stepPercent`
> - `maxRefundPercent` (12%): Same GameObject → Field `maxRefundPercent`

---

## C. FULL GOLD SINKS LIST

### C.1 Building Purchases (Primary Sink)

**28 buildings with exponential scaling:**

| ID  | Name                        | baseCost                   | baseProduction | Tier Mult | maxCount |
| --- | --------------------------- | -------------------------- | -------------- | --------- | -------- |
| 0   | Street Deals                | 15                         | 1              | 1.15      | 999      |
| 1   | Escape Driver               | 100                        | 2              | 1.15      | 999      |
| 2   | Parking Control Network     | 500                        | 5              | 1.15      | 999      |
| 3   | Custom Garage               | 2,000                      | 10             | 1.15      | 999      |
| 4   | Underground Parts Garage    | 10,000                     | 25             | 1.15      | 500      |
| 5   | Street Racing Crew          | 50,000                     | 60             | 1.15      | 500      |
| 6   | Exclusive Dealer            | 250,000                    | 150            | 1.17      | 400      |
| 7   | Night Racing Fleet          | 1,000,000                  | 400            | 1.17      | 400      |
| 8   | Shadow Logistics            | 5,000,000                  | 1,000          | 1.17      | 300      |
| 9   | Highway Influence System    | 25,000,000                 | 2,500          | 1.17      | 300      |
| 10  | Traffic Override System     | 100,000,000                | 6,000          | 1.17      | 250      |
| 11  | Pursuit Disruption System   | 500,000,000                | 15,000         | 1.17      | 250      |
| 12  | Advanced Nitro Lab          | 2,500,000,000              | 40,000         | 1.17      | 200      |
| 13  | Performance Engineering Hub | 10,000,000,000             | 100,000        | 1.20      | 200      |
| 14  | Prototype Vehicle Center    | 50,000,000,000             | 250,000        | 1.20      | 150      |
| 15  | Extreme Engine Lab          | 250,000,000,000            | 600,000        | 1.20      | 150      |
| 16  | Neural Driver Interface     | 1,000,000,000,000          | 1,500,000      | 1.20      | 125      |
| 17  | Alternative Fuel Network    | 5,000,000,000,000          | 3,500,000      | 1.20      | 125      |
| 18  | Elite Race District         | 25,000,000,000,000         | 8,000,000      | 1.20      | 100      |
| 19  | W World Blacklist League    | 100,000,000,000,000        | 20,000,000     | 1.20      | 100      |
| 20  | Ultimate Driver AI          | 500,000,000,000,000        | 50,000,000     | 1.20      | 100      |
| 21  | Legendary Speed Core        | 2,500,000,000,000,000      | 120,000,000    | 1.25      | 75       |
| 22  | Global Racing Authority     | 10,000,000,000,000,000     | 300,000,000    | 1.25      | 75       |
| 23  | Myth Garage                 | 50,000,000,000,000,000     | 750,000,000    | 1.25      | 50       |
| 24  | Eternal Speedway            | 250,000,000,000,000,000    | 2,000,000,000  | 1.25      | 50       |
| 25  | Legend Core                 | 1,000,000,000,000,000,000  | 5,000,000,000  | 1.25      | 50       |
| 26  | Racing Dominion             | 5,000,000,000,000,000,000  | 12,000,000,000 | 1.25      | 25       |
| 27  | Car God Protocol            | 25,000,000,000,000,000,000 | 30,000,000,000 | 1.25      | 999      |

**Cost formula:** `baseCost × tierMultiplier^currentCount`

**Cost ratio between consecutive buildings:** 4× to 6.67× (consistently 4-5× except building 0→1 which is 6.67×).

> **📍 Where to change in Unity:**
>
> - All 28 building definitions (`baseCost`, `baseProduction`, `maxCount`): Scene **Main** → GameObject **GameManager** → Component **BuildingManager** → Field `buildings[]` array → expand each element in Inspector
> - Per-building `costMultiplier` in scene (1.10–1.15): Same array → each building's `costMultiplier` field — **WARNING:** these scene values are overridden at runtime by the tiered system in code
> - Tiered cost multiplier override (1.15–1.25): **Code only** → `BuildingManager.cs` → `GetEffectiveCostMultiplier()` method
> - `enableEconomyDebugLogs`: Same Component → Field `enableEconomyDebugLogs`

### C.2 Upgrade Purchases

| Type   | Effect per Level  | Cost Formula    | Source           |
| ------ | ----------------- | --------------- | ---------------- |
| Tap    | +1 moneyPerTap    | 10 × 1.15^level | UpgradeButton.cs |
| MPS    | +1 moneyPerSecond | 10 × 1.15^level | UpgradeButton.cs |
| Global | ×1.1 both         | 10 × 1.15^level | UpgradeButton.cs |

> **📍 Where to change in Unity:**
>
> - Upgrade base cost (10) and cost multiplier (1.15): **Code only** → `UpgradeButton.cs` → fields `baseCost`, `costMultiplier` — check if `[SerializeField]`; if so, editable on the UpgradeButton instances in scene
> - Per-level effects (+1 tap, +1 MPS, ×1.1 global): **Code only** → `UpgradeButton.cs`

### C.3 Card Upgrades

| Level Range | Cost Multiplier | Formula                    |
| ----------- | --------------- | -------------------------- |
| 1–6         | ×1.4 per level  | `baseCost(10) × 1.4^level` |
| 7–12        | ×1.6 per level  | `cost × 1.6` (compound)    |
| 13+         | ×1.85 per level | `cost × 1.85` (compound)   |

**Also requires 8 card copies** (segments) per upgrade.

> **📍 Where to change in Unity:**
>
> - Card upgrade gold cost tiers (×1.4 / ×1.6 / ×1.85): **Code only** → `CardDefinition.cs` → `GetUpgradeCost()` method (hardcoded tier multipliers)
> - Copies required per upgrade (8): **Code only** → `CardDefinition.cs` → `CopiesNeeded` constant
> - Card definitions (rarity, type): Scene **Main** → GameObject **CardManager** → Component **CardManager** → Field `cards[]` array

### C.4 Garage Part Purchases (Gold)

| Part Category     | Price Range                                        | Count         |
| ----------------- | -------------------------------------------------- | ------------- |
| Camurluk (Fender) | 500 – 2,000                                        | 3             |
| Egzoz (Exhaust)   | 500 – 2,000                                        | 3             |
| Kaput (Hood)      | 500 – 5,000                                        | 7             |
| Spoiler           | 500 – 3,000 (+ Spoiler_5 = 5 quadrillion sentinel) | 5 (4 buyable) |

**Total per car:** 31,750 gold (excluding Spoiler_5 sentinel)  
**Total all 7 cars:** 222,250 gold (excluding sentinels)

> **📍 Where to change in Unity:**
>
> - All part prices: **Project Window** → `Assets/SO/Garage/GarageShopConfig.asset` → open in Inspector → part price arrays per category (Camurluk, Egzoz, Kaput, Spoiler)
> - Sentinel values (Spoiler_5 = 5 quadrillion): Same asset → last entry in Spoiler prices array
> - Per-car data: **Project Window** → `Assets/SO/Garage/CarData_Bmw.asset`, `CarData_Bugatti.asset`, etc.
> - Global garage config: **Project Window** → `Assets/SO/Garage/GarageDatabase.asset`

### C.5 Police Chase Fail Penalty (Gold Loss)

| Stage | Multiplier | Money Lost |
| ----- | ---------- | ---------- |
| 1     | 0.90       | 10%        |
| 2     | 0.85       | 15%        |
| 3     | 0.80       | 20%        |
| 4     | 0.72       | 28%        |
| 5     | 0.60       | 40%        |
| 6     | 0.50       | **50%**    |

> **📍 Where to change in Unity:**
>
> - `failMoneyMultiplier`: Scene **Main** → GameObject **GameManager** → Component **PoliceCatchController** → Field `failMoneyMultiplier` (base value)
> - Stage-scaled fail multipliers: **Code only** → `PoliceCatchController.cs` → `FailMultiplierByStage` hardcoded dictionary
> - Fail popularity gain per stage: **Code only** → `PoliceCatchController.cs` → `FailPopGainByStage` hardcoded dictionary

### C.6 GarageManager Activation Cost

**Spend requirement:** `currentMPS × spendSecondsEquivalent(level)` must be spent before activation.

| Level | Seconds of MPS to Spend |
| ----- | ----------------------- |
| 1     | 30s worth               |
| 2     | 28s worth               |
| 3     | 26s worth               |
| 4     | 24s worth               |
| 5     | 22s worth               |
| 6     | 20s worth               |

This is an indirect sink — you must spend naturally (buildings, upgrades, etc.) to charge the ability.

> **📍 Where to change in Unity:**
>
> - Spend seconds equivalents per level: **Code only** → `GarageManagerController.cs` → `SpendSecondsEquivalents` static readonly array
> - `activeDurationSeconds` (60s): Scene **Main** → GameObject **GarageManagerCtrl** → Component **GarageManagerController** → Field `activeDurationSeconds`
> - `cooldownSeconds` (120s): Same GameObject → Field `cooldownSeconds`
> - `useLevelScaling`: Same GameObject → Field `useLevelScaling`

---

## D. FULL NITRO COIN SOURCES LIST

### D.1 World Nitro Coin Spawns

| Parameter        | Value              | Source              |
| ---------------- | ------------------ | ------------------- |
| Spawn interval   | 60–90s (random)    | NitroCoinSpawner.cs |
| Coins per pickup | **1**              | NitroCoin.cs        |
| Effective rate   | ~0.8–1.0 coins/min | Calculated          |

> **📍 Where to change in Unity:**
>
> - Spawn interval: Scene **Main** → GameObject **NitroCoinSpawner** → Component **NitroCoinSpawner** → Fields `minSpawnInterval` (10), `maxSpawnInterval` (15)
> - Spawn position range: Same GameObject → Fields `minX`, `maxX`
> - Coins per pickup (`rewardAmount`): **Prefab** → `Assets/Prefabs/NitroCoin.prefab` → Component **NitroCoin** → Field `rewardAmount`
> - Coin movement speed: Same Prefab → Field `speed`

### D.2 Nitro Rain

| Level | Required Collects | Rain Duration | Est. Coins/Rain |
| ----- | ----------------- | ------------- | --------------- |
| 1     | 5                 | 5s            | ~18             |
| 2     | 7                 | 8s            | ~29             |
| 3     | 9                 | 11s           | ~40             |
| 4     | 11                | 14s           | ~51             |
| 5     | 13                | 17s           | ~62             |
| 6     | 16                | 20s           | ~73             |

**Spawn interval during rain:** 0.2–0.35s per coin. **30s delay** after collect threshold reached.

> **📍 Where to change in Unity:**
>
> - Rain delay, spawn intervals: Scene **Main** → GameObject **NitroRainCtrl** → Component **NitroRainController** → Fields `delaySeconds` (30), `spawnIntervalMin` (0.2), `spawnIntervalMax` (0.35), `maxRainExtensionSeconds` (10)
> - Required collects per level: **Code only** → `NitroRainController.cs` → `RequiredCollects[]` static readonly array
> - Rain durations per level: **Code only** → `NitroRainController.cs` → `RainDurations[]` static readonly array
> - Spawn lane count per level: **Code only** → `NitroRainController.cs` → `SpawnLaneCounts[]` static readonly array

### D.3 Nitro Magnet Auto-Collect

| Level | Taps to Arm | Coins Collected | Cooldown |
| ----- | ----------- | --------------- | -------- |
| 1     | 40          | 2               | 60s      |
| 2     | 50          | 3               | 90s      |
| 3     | 60          | 4               | 120s     |
| 4     | 70          | 5               | 150s     |
| 5     | 80          | 7               | 180s     |
| 6     | 90          | 9               | 210s     |

**Rain overlap:** cooldown ×2.

> **📍 Where to change in Unity:**
>
> - Magnet Inspector params: Scene **Main** → GameObject **NitroMagnetCtrl** → Component **NitroMagnetController** → Fields `pullSpeed` (15), `collectDistance` (0.5), `maxArmedDuration` (30), `cooldownBase` (60), `cooldownPerLevel` (30), `rainCooldownMultiplier` (2), `coinZThreshold` (42)
> - Taps to arm per level: **Code only** → `NitroMagnetController.cs` → `TapsToArm[]` static readonly array
> - Coins to collect per level: **Code only** → `NitroMagnetController.cs` → `CoinsToCollect[]` static readonly array

### D.4 Police Escape Reward (Nitro)

| Stage | Reward Coins |
| ----- | ------------ |
| 1     | 3            |
| 2     | 5            |
| 3     | 8            |
| 4     | 12           |
| 5     | 18           |
| 6     | 25           |

> **📍 Where to change in Unity:**
>
> - Nitro reward coins per stage: **Code only** → `PoliceCatchController.cs` → `RewardCoinsByStage` hardcoded dictionary
> - `rewardCoinCount` (base): Scene **Main** → GameObject **GameManager** → Component **PoliceCatchController** → Field `rewardCoinCount`

### D.5 Chest Nitro Rewards

| Chest Type | Min % of Balance | Max % of Balance | Minimum |
| ---------- | ---------------- | ---------------- | ------- |
| Common     | 5%               | 20%              | 5       |
| Rare       | 10%              | 30%              | 5       |
| Legendary  | 20%              | 40%              | 5       |

**Rounding:** `max(5, round(raw / 5) * 5)` — always multiples of 5.

> **📍 Where to change in Unity:**
>
> - Chest nitro min/max percentages: **Code only** → `ChestTypeDefs.cs` → `GetConfig()` static method (per chest type `nitroPercentMin` / `nitroPercentMax`)
> - Minimum nitro reward (5): **Code only** → `ChestTypeDefs.cs` → rounding logic

### D.6 Blacklist Mission Rewards

`nitroAmount` field exists per mission reward. Values are Inspector-configured.

> **📍 Where to change in Unity:**
>
> - Blacklist mission nitro rewards: Scene **Main** → GameObject **BlacklistManager** → Component **BlacklistManager** → Field `tierDefinitions[]` → each BlacklistTier SO asset → mission rewards → `nitroAmount`
> - Tier SO assets: **Project Window** → `Assets/Prefabs/Blacklist/BlacklistTier_1.asset` through `BlacklistTier_5.asset`

### D.7 Daily Offers (Free Slot)

| Reward     | Range | Probability |
| ---------- | ----- | ----------- |
| NitroCoins | 1–5   | 33%         |

> **📍 Where to change in Unity:**
>
> - Free nitro range: Scene **Main** → GameObject **Section_DailyOffers** → Component **DailyOffersController** → Fields `freeNitroMin` (1), `freeNitroMax` (5)

### D.8 SmallInvestment Nitro Refund

Same percentage as gold refund (2-12% by level) applied to all nitro spends.

> **📍 Where to change in Unity:**
>
> - Same as B.8: Scene **Main** → GameObject **SmallInvestmentCtrl** → Component **SmallInvestmentController** → Fields `basePercent`, `stepPercent`, `maxRefundPercent`

---

## E. FULL NITRO COIN SINKS LIST

### E.1 Chest Open Now (Skip Timer)

| Chest Type | Nitro Cost |
| ---------- | ---------- |
| Common     | 15         |
| Rare       | 50         |
| Legendary  | 100        |

> **📍 Where to change in Unity:**
>
> - Chest open-now nitro costs: **Code only** → `ChestTypeDefs.cs` → `GetConfig()` static method → `openNowCost` per chest type

### E.2 Garage Color Purchases

| Color Index | Nitro Cost                           |
| ----------- | ------------------------------------ |
| 0 (Default) | 0 (free)                             |
| 1           | 50                                   |
| 2           | 50                                   |
| 3           | 75                                   |
| 4           | 75                                   |
| 5           | **2,147,483,647** (MAX_INT — locked) |

**Per car:** 250 nitro for colors 1-4 (color 5 is locked)  
**All 7 cars:** 1,750 nitro

> **📍 Where to change in Unity:**
>
> - Color prices per index: **Project Window** → `Assets/SO/Garage/GarageShopConfig.asset` → Inspector → color price array
> - MAX_INT sentinel (color 5): Same asset → last entry in color array

### E.3 Garage Sticker Purchases

| Sticker Index | Nitro Cost                           |
| ------------- | ------------------------------------ |
| 0 (Default)   | 0 (free)                             |
| 1 (98)        | 30                                   |
| 2 (Alev)      | 40                                   |
| 3 (Asim)      | 50                                   |
| 4 (Bir)       | 60                                   |
| 5 (Ejder)     | **2,147,483,647** (MAX_INT — locked) |

**Per car:** 180 nitro for stickers 1-4 (sticker 5 is locked)  
**All 7 cars:** 1,260 nitro

> **📍 Where to change in Unity:**
>
> - Sticker prices per index: **Project Window** → `Assets/SO/Garage/GarageShopConfig.asset` → Inspector → sticker price array
> - MAX_INT sentinel (sticker 5): Same asset → last entry in sticker array

### E.4 BoostMode Charge (Indirect)

Collecting nitro coins charges the boost bar (1 charge per nitro collected). This is an **opportunity cost** — nitro goes into the boost bar instead of being "saved". Charge is NOT consumed from the nitro balance; it's a parallel counter.

> **📍 Where to change in Unity:**
>
> - `chargePerNitro`: Scene **Main** → GameObject **BoostModeController** → Component **BoostModeController** → Field `chargePerNitro` (1)
> - `maxCharge`: Same GameObject → Field `maxCharge` (20)

### E.5 Daily Offers Card Purchases

| Slot   | Base Price | Per-Purchase Scaling    | Currency    |
| ------ | ---------- | ----------------------- | ----------- |
| Slot 2 | 15 NC      | +5 per total purchases  | Nitro Coins |
| Slot 3 | 30 NC      | +10 per total purchases | Nitro Coins |

Grants `5 + totalPurchases` card copies of the lowest-level card.

> **📍 Where to change in Unity:**
>
> - `slot2Price` (15 NC): Scene **Main** → GameObject **Section_DailyOffers** → Component **DailyOffersController** → Field `slot2Price`
> - `slot3Price` (30 NC): Same GameObject → Field `slot3Price`
> - `copiesPerPurchase` (5): Same GameObject → Field `copiesPerPurchase`
> - Per-purchase scaling formula (+5 / +10): **Code only** → `DailyOffersController.cs`

---

## F. FULL TIMER / COOLDOWN / SPAWN / PACING LIST

### F.1 Chest Timers

| Timer                   | Duration              | Source                   |
| ----------------------- | --------------------- | ------------------------ |
| Common chest unlock     | 1,200s (20 min)       | ChestTypeDefs.cs         |
| Rare chest unlock       | 2,400s (40 min)       | ChestTypeDefs.cs         |
| Legendary chest unlock  | 3,600s (60 min)       | ChestTypeDefs.cs         |
| Half-time ad reward     | 50% of remaining time | ChestInventoryManager.cs |
| Chest spawn interval    | 20–40s (random)       | ChestSpawner.cs          |
| Max chest slots         | 5                     | ChestInventoryManager.cs |
| Simultaneous unlocks    | 1                     | ChestInventoryManager.cs |
| Session stale threshold | 48h                   | ChestSessionManager.cs   |

> **📍 Where to change in Unity:**
>
> - Unlock durations (20/40/60 min): **Code only** → `ChestTypeDefs.cs` → `GetConfig()` → `unlockDurationSeconds` per type
> - Open-now nitro costs (15/50/100): **Code only** → `ChestTypeDefs.cs` → `GetConfig()` → `openNowCost`
> - Half-time ad reduction: **Code only** → `ChestInventoryManager.cs`
> - Spawn interval: Scene **Main** → GameObject **ChestSpawner** → Component **ChestSpawner** → Fields `minSpawnInterval` (5), `maxSpawnInterval` (15)
> - Max slots (5): **Code only** → `ChestInventoryManager.cs` → hardcoded constant
> - Session stale threshold: **Code only** → `ChestSessionManager.cs`
> - Debug logging: Scene **Main** → GameObject **ChestInventoryManager** → Component **ChestInventoryManager** → Field `debugLogs`

### F.2 Ability Timers

| Ability       | Activation                   | Duration                  | Cooldown                         | Source                     |
| ------------- | ---------------------------- | ------------------------- | -------------------------------- | -------------------------- |
| TurboFinger   | 50 taps in 15s window        | 30s                       | 120s                             | TurboFingerController.cs   |
| BoostMode     | Fill charge bar (5-18 nitro) | 6-16s (by level)          | 25-60s (by level)                | BoostModeController.cs     |
| GarageManager | Spend threshold met          | 60s                       | 120s                             | GarageManagerController.cs |
| Momentum      | Continuous tapping           | Passive (resets on pause) | N/A (0.8-1.8s reset window)      | MomentumController.cs      |
| NitroRain     | Collect 5-16 nitro           | 5-20s rain (by level)     | 30s delay before rain            | NitroRainController.cs     |
| NitroMagnet   | 40-90 taps + armed           | Up to 30s armed           | 60-210s (by level, ×2 with rain) | NitroMagnetController.cs   |

> **📍 Where to change in Unity:**
>
> - TurboFinger: Scene **Main** → GameObject **TurboFingerCtrl** → Component **TurboFingerController** → Fields `tapWindowSeconds` (15), `tapsRequiredToActivate` (50), `activeDurationSeconds` (30), `cooldownDurationSeconds` (120)
> - BoostMode: Scene **Main** → GameObject **BoostModeController** → Component **BoostModeController** → Fields `boostDurationSeconds` (10), `cooldownSeconds` (30)
> - BoostMode per-level params (charge thresholds, durations, multipliers): **Code only** → `BoostModeController.cs` → `GetBoostParamsForLevel()` switch
> - GarageManager: Scene **Main** → GameObject **GarageManagerCtrl** → Component **GarageManagerController** → Fields `activeDurationSeconds` (60), `cooldownSeconds` (120)
> - Momentum: Scene **Main** → GameObject **MomentumCtrl** → Component **MomentumController** → Fields `baseResetWindow` (0.8), `resetWindowStep`, `basePerStackBonus` (0.005), `baseStackCap` (30), `stackCapStep` (10)
> - NitroRain delay: Scene **Main** → GameObject **NitroRainCtrl** → Component **NitroRainController** → Field `delaySeconds` (30)
> - NitroRain per-level collects/duration: **Code only** → `NitroRainController.cs` → `RequiredCollects[]` / `RainDurations[]`
> - NitroMagnet: Scene **Main** → GameObject **NitroMagnetCtrl** → Component **NitroMagnetController** → Fields `maxArmedDuration` (30), `cooldownBase` (60), `cooldownPerLevel` (30), `rainCooldownMultiplier` (2)
> - NitroMagnet per-level taps/coins: **Code only** → `NitroMagnetController.cs` → `TapsToArm[]` / `CoinsToCollect[]`

### F.3 Spawn Rates

| Spawner    | Min Interval | Max Interval | Max Active        | Source              |
| ---------- | ------------ | ------------ | ----------------- | ------------------- |
| NitroCoins | 60s          | 90s          | ∞                 | NitroCoinSpawner.cs |
| Chests     | 20s          | 40s          | 5 (inventory cap) | ChestSpawner.cs     |
| Radars     | 8s           | 20s          | 1                 | RadarSpawner.cs     |

> **📍 Where to change in Unity:**
>
> - NitroCoin spawner: Scene **Main** → GameObject **NitroCoinSpawner** → Component **NitroCoinSpawner** → Fields `minSpawnInterval` (10), `maxSpawnInterval` (15)
> - Chest spawner: Scene **Main** → GameObject **ChestSpawner** → Component **ChestSpawner** → Fields `minSpawnInterval` (5), `maxSpawnInterval` (15), `minX`, `maxX`, `fixedY`
> - Radar spawner: Scene **Main** → GameObject **RadarSpawner** → Component **RadarSpawner** → Fields `minSpawnInterval` (8), `maxSpawnInterval` (20), `maxAliveRadars` (1)

### F.4 Heat / Police Pacing

| Parameter                          | Value                 | Source                   |
| ---------------------------------- | --------------------- | ------------------------ |
| Passive heat fill                  | 0.20/s                | AmbientHeatManager.cs    |
| Chase trigger threshold            | 70 heat               | AmbientHeatManager.cs    |
| Time to threshold (no radars)      | ~350s (5.8 min)       | Calculated               |
| Radar miss heat gain               | +6                    | AmbientHeatManager.cs    |
| Radar catch heat loss              | -2                    | AmbientHeatManager.cs    |
| Chase end heat drop                | -45                   | AmbientHeatManager.cs    |
| Post-chase cooldown                | 30s                   | AmbientHeatManager.cs    |
| Pre-chase heat reduction           | -42 (threshold × 0.6) | AmbientHeatManager.cs    |
| Max chase duration                 | 12s                   | PoliceCatchController.cs |
| Min time between chases            | 15s                   | PoliceCatchTrigger.cs    |
| Radar misses to trigger (by stage) | 13, 11, 9, 7, 5, 3    | PoliceCatchTrigger.cs    |

> **📍 Where to change in Unity:**
>
> - Heat params: Scene **Main** → GameObject **GameManager** → Component **AmbientHeatManager** → Fields `maxHeat` (100), `heatThreshold` (70), `passiveHeatPerSecond` (0.2), `missHeatGain` (6), `catchHeatLoss` (2), `chaseEndHeatDrop` (45), `postChaseCooldown` (30)
> - Chase duration: Scene **Main** → GameObject **GameManager** → Component **PoliceCatchController** → Field `maxChaseDuration` (12)
> - Min time between chases: Scene **Main** → GameObject **GameManager** → Component **PoliceCatchTrigger** → Field `minimumTimeBetweenChases` (15)
> - Radar miss thresholds per stage: Scene **Main** → GameObject **GameManager** → Component **PoliceCatchTrigger** → Field `thresholds[]` array (13, 11, 9, 7, 5, 3)
> - Radar speed / popularity effect: **Prefab** → `Assets/Prefabs/Radar_Test.prefab` → Component **Radar** → Fields `moveSpeed`, `popularityDelta`

### F.5 Other Timers

| Timer                   | Value | Source                      |
| ----------------------- | ----- | --------------------------- |
| Daily offers refresh    | 24h   | DailyOffersController.cs    |
| Blacklist panel refresh | 0.5s  | BlacklistPanelController.cs |
| MPS measurement window  | 1s    | CurrencyManager.cs          |
| MPS snapshot (offline)  | 0.5s  | PitStopCrewController.cs    |
| Radar display popup     | 2s    | RadarPopupController.cs     |

> **📍 Where to change in Unity:**
>
> - Daily offers refresh: **Code only** → `DailyOffersController.cs` → 24h check logic
> - Blacklist panel refresh: Scene **Main** → GameObject **Panel_BlackList** → Component **BlacklistPanelController** → Field `refreshInterval` (0.5)
> - Radar popup display: Scene **Main** → GameObject **RadarPopup** → Component **RadarPopupController** → Fields `displayDuration` (1.5), `animIn` (0.18), `animOut` (0.18), `zoomScale` (0.92)
> - PitStopCrew MPS snapshot: **Code only** → `PitStopCrewController.cs`

---

## G. CURRENT ECONOMY VALUES TABLE

### G.1 Building Cost/Production Progression

| ID  | baseCost        | Production     | Cost/Prod Ratio | Payback (1 unit) |
| --- | --------------- | -------------- | --------------- | ---------------- |
| 0   | 15              | 1              | 15.0            | 15s              |
| 1   | 100             | 2              | 50.0            | 50s              |
| 2   | 500             | 5              | 100.0           | 100s             |
| 3   | 2,000           | 10             | 200.0           | 200s             |
| 4   | 10,000          | 25             | 400.0           | 400s             |
| 5   | 50,000          | 60             | 833.3           | 833s             |
| 6   | 250,000         | 150            | 1,666.7         | 1,667s           |
| 7   | 1,000,000       | 400            | 2,500.0         | 2,500s           |
| 8   | 5,000,000       | 1,000          | 5,000.0         | 5,000s           |
| 9   | 25,000,000      | 2,500          | 10,000.0        | 10,000s          |
| 10  | 100,000,000     | 6,000          | 16,666.7        | 16,667s          |
| 11  | 500,000,000     | 15,000         | 33,333.3        | 33,333s          |
| 12  | 2,500,000,000   | 40,000         | 62,500.0        | 62,500s          |
| 13  | 10,000,000,000  | 100,000        | 100,000.0       | 100,000s         |
| 14  | 50,000,000,000  | 250,000        | 200,000.0       | 200,000s         |
| 15  | 250,000,000,000 | 600,000        | 416,666.7       | 416,667s         |
| 16  | 1e12            | 1,500,000      | 666,666.7       | ~7.7 days        |
| 17  | 5e12            | 3,500,000      | 1,428,571.4     | ~16.5 days       |
| 18  | 2.5e13          | 8,000,000      | 3,125,000.0     | ~36 days         |
| 19  | 1e14            | 20,000,000     | 5,000,000.0     | ~58 days         |
| 20  | 5e14            | 50,000,000     | 10,000,000.0    | ~116 days        |
| 21  | 2.5e15          | 120,000,000    | 20,833,333.3    | ~241 days        |
| 22  | 1e16            | 300,000,000    | 33,333,333.3    | ~386 days        |
| 23  | 5e16            | 750,000,000    | 66,666,666.7    | ~2.1 years       |
| 24  | 2.5e17          | 2,000,000,000  | 125,000,000.0   | ~4.0 years       |
| 25  | 1e18            | 5,000,000,000  | 200,000,000.0   | ~6.3 years       |
| 26  | 5e18            | 12,000,000,000 | 416,666,666.7   | ~13.2 years      |
| 27  | 2.5e19          | 30,000,000,000 | 833,333,333.3   | ~26.4 years      |

**NOTE:** Payback times above assume only that building's production with no other income. Real payback would be faster due to cumulative MPS from all buildings + multipliers. However, the exponential cost growth means later buildings become progressively harder to reach. **Buildings 21+ have payback ratios that become astronomical without multiplier stacking.**

> **📍 Where to change in Unity:**
>
> - Every row in this table: Scene **Main** → GameObject **GameManager** → Component **BuildingManager** → Field `buildings[]` array → expand element by ID → `baseCost`, `baseProduction`, `costMultiplier`, `maxCount`
> - Tiered cost multiplier override: **Code only** → `BuildingManager.cs` → `GetEffectiveCostMultiplier()` — overrides the scene `costMultiplier` with tiered values (1.15/1.17/1.20/1.25)

### G.2 Card Upgrade Costs (Gold)

| Level | Cost (cumulative formula) | Copies Needed |
| ----- | ------------------------- | ------------- |
| 1     | Free (auto-unlock)        | First copy    |
| 2     | 14                        | 8             |
| 3     | 19                        | 8             |
| 4     | 27                        | 8             |
| 5     | 38                        | 8             |
| 6     | 54                        | 8             |
| 7     | 86                        | 8             |
| 8     | 138                       | 8             |
| 9     | 221                       | 8             |
| 10    | 353                       | 8             |
| 11    | 565                       | 8             |
| 12    | 904                       | 8             |
| 13    | 1,672                     | 8             |
| 14    | 3,093                     | 8             |
| 15    | 5,722                     | 8             |

### G.3 Card Ability Effects Summary

| Card            | L1 Effect                        | L6 Effect                          | Activation                           |
| --------------- | -------------------------------- | ---------------------------------- | ------------------------------------ |
| TurboFinger     | ×2 tap income 30s                | ×14 tap income 30s                 | 50 taps in 15s, 120s CD              |
| NitroRain       | 5 collects → 5s rain (~18 coins) | 16 collects → 20s rain (~73 coins) | Auto after threshold, 30s delay      |
| BoostMode       | ×3 all income 6s                 | ×20 all income 16s                 | Fill 5-18 nitro charge, 25-60s CD    |
| Momentum        | ×1.15 at 30 stacks               | ×2.20 at 80 stacks                 | Continuous tapping, 0.8-1.8s reset   |
| PitStopCrew     | 20% MPS offline, 2h cap          | 85% MPS offline, 12h cap           | Automatic on app reopen              |
| SmallInvestment | 2% refund all spends             | 12% refund all spends              | Automatic on any purchase            |
| GarageManager   | ×10 MPS for 60s                  | ×15 MPS for 60s                    | Spend 30-20s of MPS worth, 120s CD   |
| NitroMagnet     | 40 taps → collect 2 coins        | 90 taps → collect 9 coins          | Auto after tap threshold, 60-210s CD |

> **📍 Where to change in Unity:**
>
> - See individual ability sections above (B.1, D.2, D.3, F.2) for per-ability Inspector fields and code-only locations.
> - All ability level scaling is **code only** in static arrays/switch statements within each controller.
> - Activation params (tap windows, cooldowns, durations) are Inspector-editable on each controller’s GameObject in Main scene.

### G.4 BoostMode Detailed Parameters

| Level | Multiplier | Duration | Cooldown | Max Charge |
| ----- | ---------- | -------- | -------- | ---------- |
| 1     | ×3         | 6s       | 60s      | 5          |
| 2     | ×5         | 8s       | 55s      | 7          |
| 3     | ×8         | 10s      | 48s      | 9          |
| 4     | ×12        | 12s      | 40s      | 12         |
| 5     | ×16        | 14s      | 32s      | 15         |
| 6     | ×20        | 16s      | 25s      | 18         |

> **📍 Where to change in Unity:**
>
> - All values in this table (multiplier, duration, cooldown, max charge per level): **Code only** → `BoostModeController.cs` → `GetBoostParamsForLevel()` switch statement
> - Base Inspector fields: Scene **Main** → GameObject **BoostModeController** → Component **BoostModeController** → `boostDurationSeconds`, `cooldownSeconds`, `chargePerNitro`, `maxCharge`

### G.5 Popularity Stage Effects

| Stage | Range  | Radar Miss Threshold | Police Escape Reward | Fail Money Multiplier | Fail Pop Gain |
| ----- | ------ | -------------------- | -------------------- | --------------------- | ------------- |
| 1     | 0-17   | 13 misses            | 3 NC                 | ×0.90 (−10%)          | +2            |
| 2     | 18-35  | 11 misses            | 5 NC                 | ×0.85 (−15%)          | +3            |
| 3     | 36-53  | 9 misses             | 8 NC                 | ×0.80 (−20%)          | +4            |
| 4     | 54-71  | 7 misses             | 12 NC                | ×0.72 (−28%)          | +5            |
| 5     | 72-89  | 5 misses             | 18 NC                | ×0.60 (−40%)          | +6            |
| 6     | 90-100 | 3 misses             | 25 NC                | ×0.50 (−50%)          | +8            |

> **📍 Where to change in Unity:**
>
> - Stage thresholds (0-17, 18-35, etc.): **Code only** → `PopularityManager.cs` → `StageThresholds` static readonly array
> - Radar miss thresholds: Scene **Main** → GameObject **GameManager** → Component **PoliceCatchTrigger** → Field `thresholds[]`
> - Police escape nitro rewards: **Code only** → `PoliceCatchController.cs` → `RewardCoinsByStage`
> - Fail money multipliers: **Code only** → `PoliceCatchController.cs` → `FailMultiplierByStage`
> - Fail pop gain: **Code only** → `PoliceCatchController.cs` → `FailPopGainByStage`
> - Popularity debug: Scene **Main** → GameObject **GameManager** → Component **PopularityManager** → Field `enableDebug`

### G.6 Chest Spawn Weight Progression

| Money Level | Common% | Rare% | Legendary% |
| ----------- | ------- | ----- | ---------- |
| 0           | 75      | 20    | 5          |
| 500K        | 61.7    | 28.3  | 10         |
| 1M          | 55      | 32.5  | 12.5       |
| 10M         | 38.6    | 42.7  | 18.6       |
| ∞           | 35      | 45    | 20         |

> **📍 Where to change in Unity:**
>
> - Chest spawn weight progression by money level: **Code only** → `ChestTypeDefs.cs` → spawn weight interpolation tables

### G.7 Card Drop Rarity Weights by Chest Type

| Chest Type | Common | Rare | Epic | Legendary |
| ---------- | ------ | ---- | ---- | --------- |
| Common     | 50     | 30   | 15   | **0**     |
| Rare       | 30     | 35   | 25   | 10        |
| Legendary  | **0**  | 20   | 50   | 30        |

> **📍 Where to change in Unity:**
>
> - All card rarity weights per chest type: **Code only** → `ChestTypeDefs.cs` → `GetConfig()` → `cardRarityWeights` dictionary per chest type
> - Individual card rarity assignments: **Code only** → `CardDropTuning.cs` → `CardConfigs` static dictionary

### G.8 Garage Total Sink Summary

| Category       | Per Car | 7 Cars Total | Currency |
| -------------- | ------- | ------------ | -------- |
| Colors (1-4)   | 250     | 1,750        | Nitro    |
| Stickers (1-4) | 180     | 1,260        | Nitro    |
| Parts (all 18) | 31,750  | 222,250      | Gold     |

> **📍 Where to change in Unity:**
>
> - All garage pricing: **Project Window** → `Assets/SO/Garage/GarageShopConfig.asset` → Inspector → arrays for color prices, sticker prices, part prices per category
> - Per-car data: **Project Window** → `Assets/SO/Garage/CarData_Bmw.asset` / `CarData_Bugatti.asset` / `CarData_Dodge.asset` / `CarData_Mazda.asset` / `CarData_Nardo.asset` / `CarData_Pagani.asset` / `CarData_Vw.asset`
> - Global garage database: **Project Window** → `Assets/SO/Garage/GarageDatabase.asset`

---

## H. PLACEHOLDER / TEMPORARY / SUSPICIOUS VALUES

### H.1 Confirmed Placeholders

| Item                         | Value                      | Location                    | Issue                                                     |
| ---------------------------- | -------------------------- | --------------------------- | --------------------------------------------------------- |
| **AdProvider**               | Always auto-succeed        | AdProvider.cs               | Dummy — no real ad SDK integrated                         |
| **Daily Offer Free Chest**   | 1/3 probability            | DailyOffersController.cs    | Selected but **never granted** — no implementation        |
| **Color index 5**            | 2,147,483,647 NC           | GarageShopConfig.asset      | MAX_INT sentinel = locked/unpurchasable                   |
| **Sticker index 5**          | 2,147,483,647 NC           | GarageShopConfig.asset      | MAX_INT sentinel = locked/unpurchasable                   |
| **Spoiler_5**                | 5,000,000,000,000,000 Gold | GarageShopConfig.asset      | 5 quadrillion sentinel = locked/unpurchasable             |
| **TurboFinger doc comments** | "x5/x10/x20/x50/x100/x200" | TurboFingerController.cs L8 | Comment says one thing, code array is `{1,2,3,5,7,10,14}` |

> **📍 Where to change in Unity:**
>
> - AdProvider dummy: **Code only** → `AdProvider.cs` → replace with real ad SDK
> - Daily offer free chest (unimplemented): **Code only** → `DailyOffersController.cs` → `GenerateFreeOffer()` method
> - Color index 5 / Sticker index 5 sentinels (MAX_INT): **Project Window** → `Assets/SO/Garage/GarageShopConfig.asset` → last entries in color/sticker price arrays
> - Spoiler_5 sentinel (5 quadrillion): Same asset → last entry in spoiler part prices
> - TurboFinger comments vs code mismatch: **Code only** → `TurboFingerController.cs` → line 8 (comments) vs line 35 (`LevelMultipliers` array)

### H.2 Suspicious Values

| Item                                  | Value                                             | Location                                            | Concern                                                          |
| ------------------------------------- | ------------------------------------------------- | --------------------------------------------------- | ---------------------------------------------------------------- |
| **StreetDeals tapBonusPerLevel**      | 0 in scene, force-set to 1.0 at runtime           | BuildingManager.cs                                  | Why not set correctly in scene?                                  |
| **Scene costMultipliers** (1.10-1.15) | Overridden by tiered system (1.15-1.25)           | BuildingManager.cs                                  | Scene values are dead data — confusing for designers             |
| **PitStopCrew efficiency conflict**   | CardManager: 5%/level vs Controller: 20-85%/level | CardManager.cs L289 vs PitStopCrewController.cs L50 | Controller takes priority but CardManager has wrong formula      |
| **Building 27 maxCount = 999**        | Most expensive building has highest max           | Scene data                                          | Intentional endgame? Or copy-paste from defaults?                |
| **Base moneyPerTap = 1.0**            | Never changes from 1.0 base                       | CurrencyManager.cs                                  | Trivial compared to any MPS; tapping becomes irrelevant fast     |
| **Upgrade base cost = 10**            | All 3 upgrade types share same base               | UpgradeButton.cs                                    | Too cheap? — though exponential scaling kicks in quickly         |
| **freeMoneyMin/Max = 50/200**         | Daily free gold                                   | DailyOffersController.cs                            | Static values — become insignificant within minutes of play      |
| **ChestOpenScene inspector fields**   | chestGoldPercent Min/Max declared but unused      | ChestOpenSceneController.cs                         | Dead fields — rewards come from ChestTypeConfig                  |
| **ChestRewardRevealController pools** | MoneyMultipliers, NitroAmounts, RarityWeights     | ChestRewardRevealController.cs L33                  | UNUSED legacy arrays (current system is %-based)                 |
| **ValidateBaseCostGaps**              | Expects 8-12× building ratios                     | BuildingManager.cs                                  | Actual ratios are 4-6.67× — validation would flag every building |

> **📍 Where to change in Unity:**
>
> - `tapBonusPerLevel` (force-set to 1.0): Scene **Main** → GameObject **GameManager** → Component **BuildingManager** → Field `buildings[0]` → `tapBonusPerLevel` (scene value is 0, overridden in `Awake()`)
> - Scene costMultipliers (dead data): Scene **Main** → GameObject **GameManager** → Component **BuildingManager** → Field `buildings[]` → each `costMultiplier` — overridden by tiered system in code
> - PitStopCrew conflict: **Code only** → `CardManager.cs` L289 (wrong formula) vs `PitStopCrewController.cs` L50 (correct)
> - Building 27 maxCount: Scene **Main** → GameObject **GameManager** → Component **BuildingManager** → `buildings[27].maxCount` (currently 999)
> - `moneyPerTap` base (1.0): Scene **Main** → GameObject **GameManager** → Component **CurrencyManager** → Field `moneyPerTap`
> - Upgrade base cost (10): **Code only** → `UpgradeButton.cs` → `baseCost` field
> - `freeMoneyMin`/`freeMoneyMax` (50/200): Scene **Main** → GameObject **Section_DailyOffers** → Component **DailyOffersController** → Fields `freeMoneyMin`, `freeMoneyMax`
> - Unused ChestOpenScene fields: **Code only** → `ChestOpenSceneController.cs` → dead fields `chestGoldPercentMin` / `chestGoldPercentMax`
> - Unused ChestRewardRevealController pools: **Code only** → `ChestRewardRevealController.cs` L33-34
> - ValidateBaseCostGaps expected ratios: **Code only** → `BuildingManager.cs` → `ValidateBaseCostGaps()` method

### H.3 Dead/Unused Code

| Item                                           | Location                              | Status                                                       |
| ---------------------------------------------- | ------------------------------------- | ------------------------------------------------------------ |
| `turboFingerTapBonusCached`                    | CardManager.cs                        | Always 0 — effect fully delegated to TurboFingerController   |
| `garageManagerPercentCached`                   | CardManager.cs                        | Always 0 — effect fully delegated to GarageManagerController |
| `TurboFingerStreakWindow/Required/Cooldown`    | CardManager.cs L39-42                 | Legacy constants — TurboFingerController has its own values  |
| `turboFingerTapBonusPerLevel` (1.0)            | CardManager.cs L23                    | Inspector field, never used                                  |
| `garageManagerPercentPerLevel` (0.03)          | CardManager.cs L28                    | Inspector field, never used                                  |
| All `ChestRewardRevealController` static pools | ChestRewardRevealController.cs L33-34 | Replaced by %-based system                                   |

> **📍 Where to change in Unity:**
>
> - All dead/unused code listed above is **code only** — must be removed or fixed in the respective `.cs` script files. None of these are Inspector-editable.

---

## I. SYSTEM INTERACTION MAP

### I.1 Tap Income Chain

```
Player Tap
  → TurboFingerController.OnTap()     [register for streak activation]
  → MomentumController.RegisterClick() [build combo stacks]
  → NitroMagnetController.RegisterTap()[count toward arm threshold]
  → Calculate finalAmount:
      base = moneyPerTap + cardTapBonus
      × boostMultiplier (BoostMode)
      × turboMultiplier (TurboFinger)
      × momentumMultiplier (Momentum)
  → CurrencyManager.AddMoney(finalAmount)
  → BlacklistStatTracker records earnings
```

### I.2 Passive Income Chain

```
Every Frame:
  MPS = (baseMps + garageBonusMps) × incomeBoostMultiplier × cardGlobalMpsMultiplier
  buffer += MPS × deltaTime
  if buffer >= 1.0:
      CurrencyManager.AddMoney(floor(buffer))
      buffer -= floor(buffer)
```

### I.3 Nitro Coin Flow

```
NitroCoinSpawner (60-90s) → Spawn NitroCoin prefab on road
  Player taps coin → NitroCoin.Collect()
    → CurrencyManager.AddNitroCoins(1)
    → NitroRainController.RegisterCollect()  [count toward rain threshold]
    → BoostModeController.AddCharge(1)       [charge boost bar]
    → BlacklistStatTracker.RecordWorldNitro()

NitroRain triggered:
  → 30s delay
  → Rain spawns coins at 0.2-0.35s intervals for 5-20s
  → Each coin collected → same flow as above

NitroMagnet armed:
  → Auto-pulls 2-9 nearby coins from road
  → Each → CurrencyManager.AddNitroCoins(1)
```

### I.4 Chest Flow

```
ChestSpawner (20-40s) → Spawn chest on screen
  Player taps → ChestPopupController shows popup
  → "Start Unlock" → timer begins (20/40/60 min)
  → When timer done OR "Open Now" (15/50/100 NC) → ReadyToOpen
  → Player opens → Load ChestOpenScene
    → Compute rewards:
        Gold: currentMoney × Random(min%, max%) → round
        Nitro: currentNitro × Random(min%, max%) → round to 5s, min 5
        Card: weighted random (rarity × chest type × level decay)
        Segments: 1/2/4/8 (weighted, decays at higher card levels)
        Sticker: Rare/Legendary chests only, random unowned
    → Grant rewards → Return to Main scene
```

### I.5 Police Chase Flow

```
AmbientHeatManager:
  heat += 0.20/s passively
  heat += 6 per radar miss
  heat -= 2 per radar tap
  When heat >= 70: trigger pre-chase, reduce heat by 42

PoliceCatchTrigger:
  Count radar misses per stage
  When misses >= threshold(stage): trigger police chase

Police Chase:
  12s max duration
  Player taps to resist (TPS 2-9 range)
  Success: +floor(money/8) gold + stage-scaled nitro coins
  Fail: lose 10-50% money + gain 2-8 popularity
  After: heat -= 45, cooldown 30s
  Min 15s between chases
```

### I.6 Card Progression Loop

```
Chests → Card copies (segments)
  8 copies + gold cost → Level up card
  Higher level → Better ability effect
  BUT: higher level → lower drop weight (LevelDecay)
  AND: higher level → lower segment multiplier chance

Card levels feed into:
  TurboFinger: affects tap multiplier
  Momentum: affects tap multiplier
  BoostMode: affects all-income multiplier
  NitroRain: affects nitro generation
  NitroMagnet: affects nitro collection
  PitStopCrew: affects offline earnings
  SmallInvestment: affects cost reduction
  GarageManager: affects MPS bonus
```

### I.7 Blacklist Campaign Flow

```
6 Tiers (BL#6 → BL#1), 5 missions each
  → Complete mission targets (earn gold, collect nitro, etc.)
  → Claim rewards: gold, nitro, popularity/heat reset, free chests,
     boost discounts, card copies, sticker picks, endgame cosmetics
  → All 5 missions claimed → Advance to next tier
  → Tier advance triggers cinematic car showcase
  → Campaign complete after BL#1
  → Reward car unlocked in garage
```

### I.8 Spending Interaction (SmallInvestment)

```
Any TrySpendMoney() or TrySpendNitroCoins():
  → Fires OnMoneySpent / OnNitroCoinsSpent event
  → SmallInvestmentController calculates refund (2-12%)
  → Adds refund back via AddMoney/AddNitroCoins
  → IsApplyingRefund guard prevents infinite loop
```

> **📍 Where to change GameObjects for all system interactions:**
>
> - All flows above reference components on these GameObjects in Scene **Main**:
>   - **GameManager**: CurrencyManager, BuildingManager, PopularityManager, PoliceCatchTrigger, PoliceCatchController, AmbientHeatManager
>   - **TurboFingerCtrl**: TurboFingerController
>   - **MomentumCtrl**: MomentumController
>   - **NitroMagnetCtrl**: NitroMagnetController
>   - **BoostModeController**: BoostModeController
>   - **NitroRainCtrl**: NitroRainController
>   - **NitroCoinSpawner**: NitroCoinSpawner
>   - **ChestSpawner**: ChestSpawner
>   - **ChestInventoryManager**: ChestInventoryManager
>   - **SmallInvestmentCtrl**: SmallInvestmentController
>   - **GarageManagerCtrl**: GarageManagerController
>   - **PitStopCrewCtrl**: PitStopCrewController
>   - **BlacklistManager**: BlacklistManager
>   - **CardManager**: CardManager
>   - **Section_DailyOffers**: DailyOffersController
>   - **RadarSpawner**: RadarSpawner
>   - **RadarPopup**: RadarPopupController

---

## J. RETENTION / LONG-TERM ENGAGEMENT ANALYSIS

### J.1 Session Loop (Minutes 0–30)

| Time      | Activity                       | Income Source              | Feeling                           |
| --------- | ------------------------------ | -------------------------- | --------------------------------- |
| 0-2 min   | Tapping for gold               | Base tap (1 gold/tap)      | Slow but satisfying               |
| 2-5 min   | Buy first buildings            | MPS begins                 | Progression acceleration          |
| 5-10 min  | Unlock cards from first chests | TurboFinger/Momentum start | Multiplier excitement             |
| 10-20 min | Chase/radar events start       | Nitro coins flowing        | Variety, risk/reward              |
| 20-30 min | Multiple buildings producing   | MPS dominates tap          | **Tap starts feeling irrelevant** |

### J.2 Early-Game Concerns

1. **Tap income obsolescence:** Base tap produces 1 gold. Within minutes of buying buildings, MPS vastly outpaces tapping. By building #3 (Custom Garage, 10 MPS), tapping at 5 TPS = 5 gold/s vs 10+ MPS. Tapping only remains relevant through multiplier stacking (TurboFinger × BoostMode × Momentum).

2. **Daily offers become trivial:** Free gold is 50-200. By the time daily offers matter (next day), the player likely has thousands or millions of gold. The 50-200 gold offer becomes meaningless very quickly.

3. **Nitro coin scarcity early game:** Only 1 coin per 60-90s from spawner. First rain requires 5 collects (~5-7 minutes of collecting). Chest unlock skip costs 15 NC for Common — affordable after ~15 minutes of play.

### J.3 Mid-Game Concerns (Days 1-7)

1. **Building wall progression:** Buildings 8-12 (5M to 2.5B gold) create significant walls. Players without BoostMode or GarageManager active will face multi-hour waits per building.

2. **Card level bottleneck:** 8 copies per upgrade, chests spawn every 20-40s, unlock takes 20-60 min. Effective card copy rate is ~1-4 per hour (depending on chest type and segment multiplier roll). Reaching card level 6 requires 40+ copies = 10+ hours of chest farming.

3. **Garage parts are cheap relative to buildings:** All 18 parts cost 31,750 gold total. By the time players unlock the garage scene, they likely have millions of gold. **Garage parts are not a meaningful sink.**

### J.4 Late-Game Concerns (Weeks+)

1. **Exponential cost wall:** Building 16+ costs exceed 1 trillion gold. Without multiplier stacking, reaching these buildings requires days of passive MPS accumulation. **Current MPS multiplier sources may be insufficient for late-game progression velocity.**

2. **No prestige/reset system:** Once players hit a wall (buildings 20+), there's no mechanism to reset and scale faster. This is the single largest retention gap for an idle game.

3. **Blacklist campaign is one-shot:** 6 tiers × 5 missions = 30 missions total, then done. No repeatable endgame content after campaign completion.

4. **Endgame gold sinks disappear:** After all buildings are maxed, card upgrades are the only remaining gold sink. Card costs scale infinitely but very slowly compared to income.

5. **Nitro coin sink exhaustion:** After buying all garage cosmetics (~3,010 NC total across 7 cars) and not needing to skip chest timers, nitro coins accumulate with no purpose.

### J.5 Offline Earnings Analysis

| Card Level | MPS Example | 2h Offline     | 8h Offline      | 12h Offline     |
| ---------- | ----------- | -------------- | --------------- | --------------- |
| L1 (20%)   | 1,000       | 1,440,000      | N/A (2h cap)    | N/A             |
| L3 (40%)   | 100,000     | 288,000,000    | 1,152,000,000   | N/A (4h cap)    |
| L6 (85%)   | 10,000,000  | 61,200,000,000 | 244,800,000,000 | 367,200,000,000 |

**Observation:** At high PitStopCrew levels with strong MPS, overnight earnings can be substantial. This is the primary overnight retention hook.

---

## K. MISSING ECONOMY PIECES / Balance GAPS / WEAK LOOPS

### K.1 Critical Missing Systems

| #   | Missing System                       | Impact                                                                          | Priority |
| --- | ------------------------------------ | ------------------------------------------------------------------------------- | -------- |
| 1   | **Prestige/Reset mechanic**          | No way to accelerate past late-game walls; players will quit at building ~16-20 | Critical |
| 2   | **Premium currency (diamond) sinks** | Diamonds exist in CurrencyManager but have ZERO uses anywhere in code           | Critical |
| 3   | **Real ad integration**              | AdProvider is a dummy; half-time chest reward is free; no monetization          | Critical |
| 4   | **Daily offer free chest**           | 33% chance of the free slot giving nothing (unimplemented reward type)          | High     |
| 5   | **Repeatable endgame content**       | Blacklist campaign is one-shot; no seasonal/weekly events                       | High     |

### K.2 Balance Gaps

| #   | Gap                                    | Detail                                                                             | Impact |
| --- | -------------------------------------- | ---------------------------------------------------------------------------------- | ------ |
| 1   | **Tap income irrelevance**             | Base tap = 1 gold; even with all multipliers (×616), it's tiny vs MPS              | Medium |
| 2   | **Daily offer gold scaling**           | Fixed 50-200 gold; trivial after first hour                                        | Low    |
| 3   | **Garage parts too cheap**             | 31,750 total vs millions/billions in economy; not a meaningful sink                | Medium |
| 4   | **Late building payback**              | Buildings 21-27 have payback ratios of millions to hundreds of millions of seconds | High   |
| 5   | **Building cost ratio inconsistency**  | Validator expects 8-12×, actual is 4-6.67×; neither is clearly "correct"           | Low    |
| 6   | **Police fail at Stage 6**             | Losing 50% of gold is devastating; may feel punishing rather than challenging      | Medium |
| 7   | **Nitro coin inflation**               | After garage purchases complete, nitro accumulates with limited sinks              | Medium |
| 8   | **Card copy bottleneck**               | 8 copies per upgrade with low drop rates creates frustrating gating                | Medium |
| 9   | **SmallInvestment is weak**            | 2-12% refund is barely noticeable; doesn't fundamentally change spending           | Low    |
| 10  | **Momentum requires constant tapping** | 0.8-1.8s reset window means any pause resets stacks; punishes natural play         | Medium |

### K.3 Conflicting Values

| #   | Conflict                                                                       | Files                                             | Resolution                                                 |
| --- | ------------------------------------------------------------------------------ | ------------------------------------------------- | ---------------------------------------------------------- |
| 1   | PitStopCrew offline %: CardManager says 5%/level, Controller says 20-85%/level | CardManager.cs L289, PitStopCrewController.cs L50 | Controller wins at runtime; CardManager value is dead code |
| 2   | TurboFinger comments say "x5/x10/x20..." but code is `{1,2,3,5,7,10,14}`       | TurboFingerController.cs L8 vs L35                | Code wins; comments are stale                              |
| 3   | Scene costMultiplier per building vs tiered override                           | Scene data vs BuildingManager.cs                  | Tiered system wins; scene values are dead data             |

### K.4 Weak Feedback Loops

| Loop                             | Issue                                                                                            |
| -------------------------------- | ------------------------------------------------------------------------------------------------ |
| **Tap → Gold → Buildings → MPS** | Tap income becomes negligible; loop breaks when MPS dominates                                    |
| **Nitro → Boost → Gold**         | Boost is powerful (×3-20) but short (6-16s) with long cooldown (25-60s); net effect is diluted   |
| **Chests → Cards → Abilities**   | Slow card progression (8 copies/upgrade) makes new ability levels feel far apart                 |
| **Radar tap → Popularity down**  | -1 per tap vs +1 per miss = net neutral if you catch half; heat system dominates pacing instead  |
| **Police escape → Gold bonus**   | floor(money/8) is generous (~12.5% income bump) but chase frequency is limited by heat cooldowns |

---

## L. STRUCTURED OUTPUT: NEXT-STEPS BALANCING ROADMAP

### L.1 Immediate Fixes (Bugs / Broken Systems)

| #   | Fix                                                                                                                 | Effort  |
| --- | ------------------------------------------------------------------------------------------------------------------- | ------- |
| 1   | **Implement free chest in Daily Offers** — currently 33% chance of no reward                                        | Small   |
| 2   | **Fix PitStopCrew formula in CardManager.cs** — change from `0.05f * level` to match Controller's EfficiencyByLevel | Small   |
| 3   | **Remove dead code** — unused ChestRewardRevealController pools, CardManager legacy constants                       | Small   |
| 4   | **Fix TurboFinger doc comments** — update to match actual `{1,2,3,5,7,10,14}` array                                 | Trivial |
| 5   | **Clean up scene costMultiplier values** — set to match tiered system or remove the field from BuildingDefinition   | Small   |

### L.2 Design Additions (Missing Core Systems)

| #   | System                              | Impact                                                            | Effort |
| --- | ----------------------------------- | ----------------------------------------------------------------- | ------ |
| 1   | **Prestige/Reset system**           | Biggest retention impact; enables infinite replayability          | Large  |
| 2   | **Premium currency shop and sinks** | Monetization pathway; gems for QoL, cosmetics, or acceleration    | Large  |
| 3   | **Real ad integration**             | Replace AdProvider dummy with actual SDK                          | Medium |
| 4   | **Repeatable events**               | Weekly challenges, seasonal content after blacklist completion    | Large  |
| 5   | **Tap income scaling**              | Make tapping remain relevant alongside MPS (e.g., tap = % of MPS) | Medium |

### L.3 Balance Tuning Priorities

| #   | Area                        | Current                   | Suggested Direction                                                  |
| --- | --------------------------- | ------------------------- | -------------------------------------------------------------------- |
| 1   | Daily offer gold            | 50-200 fixed              | Scale with player progress (e.g., % of current money)                |
| 2   | Garage part prices          | 500-5,000 gold            | Scale with building tier (e.g., 10× current for late-game relevance) |
| 3   | Card copies per upgrade     | 8 fixed                   | Consider reducing to 5-6, or add more card copy sources              |
| 4   | Police Stage 6 penalty      | -50% money                | Cap at -30% or provide a "insurance" card/mechanic                   |
| 5   | Momentum reset window       | 0.8s (L1)                 | Start at 1.2s+ to be more forgiving for casual players               |
| 6   | Late building accessibility | >1T gold for building 16+ | Prestige multipliers or milestone cost reductions                    |
| 7   | Nitro coin late sinks       | Only garage cosmetics     | Add utility sinks (e.g., temporary MPS boosts, card copy packs)      |
| 8   | Chest unlock times          | 20-60 min                 | Consider 10-30 min for better session fit                            |
| 9   | NitroMagnet cooldown        | 60-210s (×2 with rain)    | Rain penalty may be too harsh at 420s for L6                         |
| 10  | SmallInvestment refund      | 2-12%                     | Increase to 5-20% to make it feel impactful                          |

### L.4 Sentinel/Placeholder Resolution

| #   | Item                      | Action Needed                                                     |
| --- | ------------------------- | ----------------------------------------------------------------- |
| 1   | Color #6 (MAX_INT)        | Decide: remove slot, set real price, or mark as premium-only      |
| 2   | Sticker #6 (MAX_INT)      | Same as above                                                     |
| 3   | Spoiler_5 (5 quadrillion) | Same — currently the stat bonuses exist but the part is unbuyable |
| 4   | AdProvider dummy          | Replace with real SDK before launch                               |
| 5   | Free chest daily offer    | Implement or replace with a different reward type                 |

---

## APPENDIX A: ALL PLAYERPREFS SAVE KEYS

| Key Pattern                   | Type            | System                                                                 |
| ----------------------------- | --------------- | ---------------------------------------------------------------------- |
| `HasSave`                     | int             | SaveSystem                                                             |
| `Save_Money`                  | string (double) | CurrencyManager                                                        |
| `Save_MPS`                    | string (double) | CurrencyManager                                                        |
| `Save_MPT`                    | string (double) | CurrencyManager                                                        |
| `Save_TotalMoney`             | string (double) | CurrencyManager                                                        |
| `Save_NitroCoins`             | int             | CurrencyManager                                                        |
| `Save_PremiumCurrency`        | int             | CurrencyManager                                                        |
| `Save_Upgrade_{Type}_Level`   | int             | UpgradeSaveRegistry                                                    |
| `Save_BuildingID_{id}_Count`  | int             | BuildingManager                                                        |
| `Save_Building_{name}_Count`  | int             | BuildingManager (legacy)                                               |
| `Save_Card_{Type}_Level`      | int             | CardManager                                                            |
| `Save_Card_{Type}_Copies`     | int             | CardManager                                                            |
| `Save_Popularity01`           | float           | PopularityManager                                                      |
| `Save_ChestBlob`              | string (JSON)   | ChestInventoryManager                                                  |
| `Save_PendingOpenChest`       | string (JSON)   | ChestInventoryManager                                                  |
| `Save_BlacklistCampaign`      | string (JSON)   | BlacklistManager                                                       |
| `Save_GarageState`            | string (JSON)   | GarageSaveData                                                         |
| `BL_Stat_WorldNitro`          | int             | BlacklistStatTracker                                                   |
| `BL_Stat_RadarsDefused`       | int             | BlacklistStatTracker                                                   |
| `BL_Stat_ChestsOpened`        | int             | BlacklistStatTracker                                                   |
| `BL_Stat_BoostUses`           | int             | BlacklistStatTracker                                                   |
| `BL_Stat_PoliceEscapes`       | int             | BlacklistStatTracker                                                   |
| `BL_Stat_NitroRainTriggers`   | int             | BlacklistStatTracker                                                   |
| `BL_Stat_MagnetCoins`         | int             | BlacklistStatTracker                                                   |
| `BL_Stat_TurboUses`           | int             | BlacklistStatTracker                                                   |
| Various controller state keys | string (JSON)   | NitroMagnet, PoliceCatchTrigger, NitroRain, GarageManager, AmbientHeat |

---

## APPENDIX B: CURRENCY FLOW DIAGRAM (TEXT)

```
                    ┌─────────────────────────────────────────────┐
                    │                 GOLD SOURCES                 │
                    ├─────────────────────────────────────────────┤
                    │ • Tapping (base + cards × boost × turbo     │
                    │          × momentum)                         │
                    │ • Passive MPS (buildings + upgrades          │
                    │              × garageManager × boost)        │
                    │ • Chest rewards (5-40% of balance)           │
                    │ • Police escape (+12.5% of balance)          │
                    │ • Blacklist mission rewards                  │
                    │ • Daily offers (50-200 gold)                 │
                    │ • PitStopCrew offline earnings               │
                    │ • SmallInvestment refund (2-12%)             │
                    └──────────────────┬──────────────────────────┘
                                       │
                                       ▼
                    ┌─────────────────────────────────────────────┐
                    │              GOLD BALANCE                    │
                    └──────────────────┬──────────────────────────┘
                                       │
                                       ▼
                    ┌─────────────────────────────────────────────┐
                    │                  GOLD SINKS                  │
                    ├─────────────────────────────────────────────┤
                    │ • Buildings (15 → 2.5e19 base, exponential) │
                    │ • Upgrades (10 × 1.15^level)                │
                    │ • Card upgrades (10 × 1.4-1.85^level)       │
                    │ • Garage parts (500-5,000 per part)          │
                    │ • Police fail penalty (-10% to -50%)         │
                    │ • GarageManager activation (spend threshold) │
                    └─────────────────────────────────────────────┘

                    ┌─────────────────────────────────────────────┐
                    │              NITRO COIN SOURCES              │
                    ├─────────────────────────────────────────────┤
                    │ • World spawns (1 NC per 60-90s)            │
                    │ • NitroRain (18-73 coins per rain event)    │
                    │ • NitroMagnet (2-9 coins per activation)    │
                    │ • Police escape (3-25 NC by stage)          │
                    │ • Chest rewards (5-40% of NC balance)       │
                    │ • Blacklist rewards                          │
                    │ • Daily offers (1-5 NC)                     │
                    │ • SmallInvestment refund (2-12% of NC spent)│
                    └──────────────────┬──────────────────────────┘
                                       │
                                       ▼
                    ┌─────────────────────────────────────────────┐
                    │            NITRO COIN BALANCE                │
                    └──────────────────┬──────────────────────────┘
                                       │
                                       ▼
                    ┌─────────────────────────────────────────────┐
                    │              NITRO COIN SINKS                │
                    ├─────────────────────────────────────────────┤
                    │ • Chest skip timer (15/50/100 NC)           │
                    │ • Garage colors (50-75 NC each)             │
                    │ • Garage stickers (30-60 NC each)           │
                    │ • Daily offers card packs (15-30+ NC)       │
                    │ • BoostMode charge (opportunity cost only)   │
                    └─────────────────────────────────────────────┘

                    ┌─────────────────────────────────────────────┐
                    │         PREMIUM CURRENCY (DIAMONDS)          │
                    ├─────────────────────────────────────────────┤
                    │ Sources: Save_PremiumCurrency exists         │
                    │ Sinks: *** NONE ***                          │
                    │ Status: COMPLETELY UNUSED                    │
                    └─────────────────────────────────────────────┘
```

---

## APPENDIX C: CARD SEGMENT DROP PROBABILITY DEEP-DIVE

### Base Card Rarity Drop Weights (Common Chest)

| Card            | Rarity | Weight | At L0 (decay=1.0) | At L3 (decay=0.36) | At L6 (decay=0.19) |
| --------------- | ------ | ------ | ----------------- | ------------------ | ------------------ |
| TurboFinger     | Common | 50     | 50.0              | 18.0               | 9.5                |
| NitroRain       | Common | 50     | 50.0              | 18.0               | 9.5                |
| PitStopCrew     | Rare   | 30     | 30.0              | 10.8               | 5.7                |
| BoostMode       | Rare   | 30     | 30.0              | 10.8               | 5.7                |
| SmallInvestment | Common | 50     | 50.0              | 18.0               | 9.5                |
| Momentum        | Common | 50     | 50.0              | 18.0               | 9.5                |
| NitroMagnet     | Epic   | 15     | 15.0              | 5.4                | 2.85               |
| GarageManager   | Epic   | 15     | 15.0              | 5.4                | 2.85               |

**Level Decay Formula:** `max(0.15, 1 / (1 + level × 0.25))`

The system naturally pushes players toward leveling underleveled cards first, creating a "catch-up" mechanic.

### Segment Multiplier Drop Rates

At **card level 0**:
| Multiplier | Weight | %chance |
|------------|--------|---------|
| ×1 | 30 | 30% |
| ×2 | 35 | 35% |
| ×4 | 25 | 25% |
| ×8 | 10 | 10% |

Expected segments per chest (L0): `1×0.30 + 2×0.35 + 4×0.25 + 8×0.10 = 2.80`

At **card level 5** (approximate):
| Multiplier | Weight | Decayed |
|------------|--------|---------|
| ×1 | 30 → 26.7 | 41% |
| ×2 | 35 → 18.6 | 29% |
| ×4 | 25 → 5.7 | 9% |
| ×8 | 10 → 1.7 | 3% |

Expected segments (L5): ~1.5

**Implication:** It takes roughly twice as many chests to upgrade a L5 card vs a L0 card.

> **📍 Where to change in Unity:**
>
> - All card drop rarity weights: **Code only** → `CardDropTuning.cs` → `CardConfigs` static dictionary (rarity, weight per card)
> - Level decay formula: **Code only** → `CardDropTuning.cs` → `LevelDecay()` method
> - Segment multiplier weights: **Code only** → `CardDropTuning.cs` → segment multiplier arrays
> - Chest-type rarity weights: **Code only** → `ChestTypeDefs.cs` → `GetConfig()` → `cardRarityWeights`

---

## APPENDIX D: CONSOLIDATED UNITY EDITOR CHANGE MAP

This table provides a single-reference lookup for every tunable economy value. **Inspector** = editable in Unity Inspector without code changes. **Code Only** = requires editing the `.cs` script file.

### D.1 Inspector-Editable Values (Scene: Main)

| Economy Value                            | GameObject            | Component                 | Inspector Field(s)                                                                                                                 | Current Value(s)           |
| ---------------------------------------- | --------------------- | ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- | -------------------------- |
| Base money per tap                       | GameManager           | CurrencyManager           | `moneyPerTap`                                                                                                                      | 1.0                        |
| Base MPS                                 | GameManager           | CurrencyManager           | `moneyPerSecond`                                                                                                                   | 1                          |
| Income boost multiplier                  | GameManager           | CurrencyManager           | `incomeBoostMultiplier`                                                                                                            | 1.0                        |
| Starting nitro coins                     | GameManager           | CurrencyManager           | `nitroCoins`                                                                                                                       | 1000                       |
| All 28 buildings (cost, production, max) | GameManager           | BuildingManager           | `buildings[]` array                                                                                                                | See Section G.1            |
| Economy debug logs                       | GameManager           | BuildingManager           | `enableEconomyDebugLogs`                                                                                                           | Off                        |
| Heat system params                       | GameManager           | AmbientHeatManager        | `maxHeat`, `heatThreshold`, `passiveHeatPerSecond`, `missHeatGain`, `catchHeatLoss`, `chaseEndHeatDrop`, `postChaseCooldown`       | 100, 70, 0.2, 6, 2, 45, 30 |
| Chase duration                           | GameManager           | PoliceCatchController     | `maxChaseDuration`                                                                                                                 | 12                         |
| Police fail multiplier (base)            | GameManager           | PoliceCatchController     | `failMoneyMultiplier`                                                                                                              | 0.75                       |
| Police reward coins (base)               | GameManager           | PoliceCatchController     | `rewardCoinCount`                                                                                                                  | 10                         |
| Police advance speed                     | GameManager           | PoliceCatchController     | `policeBaseAdvancePerSecond`                                                                                                       | 0.73                       |
| Time between chases                      | GameManager           | PoliceCatchTrigger        | `minimumTimeBetweenChases`                                                                                                         | 15                         |
| Radar miss thresholds                    | GameManager           | PoliceCatchTrigger        | `thresholds[]`                                                                                                                     | 13, 11, 9, 7, 5, 3         |
| Popularity debug                         | GameManager           | PopularityManager         | `enableDebug`                                                                                                                      | Off                        |
| TurboFinger activation                   | TurboFingerCtrl       | TurboFingerController     | `tapWindowSeconds`, `tapsRequiredToActivate`, `activeDurationSeconds`, `cooldownDurationSeconds`                                   | 15, 50, 30, 120            |
| BoostMode timers                         | BoostModeController   | BoostModeController       | `boostDurationSeconds`, `cooldownSeconds`, `chargePerNitro`, `maxCharge`                                                           | 10, 30, 1, 20              |
| Momentum params                          | MomentumCtrl          | MomentumController        | `baseResetWindow`, `basePerStackBonus`, `baseStackCap`, `stackCapStep`                                                             | 0.8, 0.005, 30, 10         |
| NitroRain timing                         | NitroRainCtrl         | NitroRainController       | `delaySeconds`, `spawnIntervalMin`, `spawnIntervalMax`, `maxRainExtensionSeconds`                                                  | 30, 0.2, 0.35, 10          |
| NitroMagnet params                       | NitroMagnetCtrl       | NitroMagnetController     | `pullSpeed`, `collectDistance`, `maxArmedDuration`, `cooldownBase`, `cooldownPerLevel`, `rainCooldownMultiplier`, `coinZThreshold` | 15, 0.5, 30, 60, 30, 2, 42 |
| GarageManager timers                     | GarageManagerCtrl     | GarageManagerController   | `activeDurationSeconds`, `cooldownSeconds`, `useLevelScaling`                                                                      | 60, 120, On                |
| SmallInvestment refund                   | SmallInvestmentCtrl   | SmallInvestmentController | `basePercent`, `stepPercent`, `maxRefundPercent`                                                                                   | 2, 2, 12                   |
| PitStopCrew animation                    | PitStopCrewCtrl       | PitStopCrewController     | `countUpDuration`                                                                                                                  | 1.5                        |
| Daily offers pricing                     | Section_DailyOffers   | DailyOffersController     | `slot2Price`, `slot3Price`, `copiesPerPurchase`, `freeMoneyMin`, `freeMoneyMax`, `freeNitroMin`, `freeNitroMax`                    | 15, 30, 5, 50, 200, 1, 5   |
| NitroCoin spawn rate                     | NitroCoinSpawner      | NitroCoinSpawner          | `minSpawnInterval`, `maxSpawnInterval`, `minX`, `maxX`                                                                             | 10, 15, -2, 2              |
| Chest spawn rate                         | ChestSpawner          | ChestSpawner              | `minSpawnInterval`, `maxSpawnInterval`, `minX`, `maxX`, `fixedY`                                                                   | 5, 15, -1.61, 2.9, 0.35    |
| Radar spawn rate                         | RadarSpawner          | RadarSpawner              | `minSpawnInterval`, `maxSpawnInterval`, `maxAliveRadars`                                                                           | 8, 20, 1                   |
| Radar popup display                      | RadarPopup            | RadarPopupController      | `displayDuration`, `animIn`, `animOut`, `zoomScale`                                                                                | 1.5, 0.18, 0.18, 0.92      |
| Blacklist panel refresh                  | Panel_BlackList       | BlacklistPanelController  | `refreshInterval`                                                                                                                  | 0.5                        |
| Chest debug logging                      | ChestInventoryManager | ChestInventoryManager     | `debugLogs`                                                                                                                        | Off                        |
| Blacklist tier SO refs                   | BlacklistManager      | BlacklistManager          | `tierDefinitions[]`                                                                                                                | 6 SO references            |

### D.2 Inspector-Editable Values (ScriptableObject Assets)

| Economy Value           | Asset Path                                         | Editable Fields                                     |
| ----------------------- | -------------------------------------------------- | --------------------------------------------------- |
| Garage color prices     | `Assets/SO/Garage/GarageShopConfig.asset`          | Color price array (0, 50, 50, 75, 75, MAX_INT)      |
| Garage sticker prices   | `Assets/SO/Garage/GarageShopConfig.asset`          | Sticker price array (0, 30, 40, 50, 60, MAX_INT)    |
| Garage part prices      | `Assets/SO/Garage/GarageShopConfig.asset`          | Part price arrays per category                      |
| Per-car base data       | `Assets/SO/Garage/CarData_*.asset` (7 files)       | Car stats, model references                         |
| Global garage data      | `Assets/SO/Garage/GarageDatabase.asset`            | Part keys, sticker keys                             |
| Blacklist tier missions | `Assets/Prefabs/Blacklist/BlacklistTier_1-5.asset` | Mission targets, reward amounts (gold, nitro, etc.) |
| Card popup themes       | `Assets/SO/Theme_*.asset` (8 files)                | Visual theme data per card type                     |

### D.3 Inspector-Editable Values (Prefabs)

| Economy Value               | Prefab Path                        | Component | Field(s)                       |
| --------------------------- | ---------------------------------- | --------- | ------------------------------ |
| Nitro coin reward & speed   | `Assets/Prefabs/NitroCoin.prefab`  | NitroCoin | `rewardAmount`, `speed`        |
| Radar movement & popularity | `Assets/Prefabs/Radar_Test.prefab` | Radar     | `moveSpeed`, `popularityDelta` |

### D.4 Code-Only Values (Require Script Edits)

| Economy Value                                           | Script File                  | Location in Code                                             |
| ------------------------------------------------------- | ---------------------------- | ------------------------------------------------------------ |
| TurboFinger multipliers per level                       | `TurboFingerController.cs`   | `LevelMultipliers` static readonly array                     |
| BoostMode params per level (mult, duration, CD, charge) | `BoostModeController.cs`     | `GetBoostParamsForLevel()` switch statement                  |
| NitroRain required collects per level                   | `NitroRainController.cs`     | `RequiredCollects[]` static readonly array                   |
| NitroRain durations per level                           | `NitroRainController.cs`     | `RainDurations[]` static readonly array                      |
| NitroMagnet taps to arm per level                       | `NitroMagnetController.cs`   | `TapsToArm[]` static readonly array                          |
| NitroMagnet coins to collect per level                  | `NitroMagnetController.cs`   | `CoinsToCollect[]` static readonly array                     |
| PitStopCrew efficiency per level                        | `PitStopCrewController.cs`   | `EfficiencyByLevel[]` static readonly array                  |
| PitStopCrew cap hours per level                         | `PitStopCrewController.cs`   | `CapHoursByLevel[]` static readonly array                    |
| GarageManager bonus multipliers per level               | `GarageManagerController.cs` | `BonusMultipliers[]` static readonly array                   |
| GarageManager spend seconds per level                   | `GarageManagerController.cs` | `SpendSecondsEquivalents[]` static readonly array            |
| Police escape reward coins per stage                    | `PoliceCatchController.cs`   | `RewardCoinsByStage` hardcoded dictionary                    |
| Police fail multiplier per stage                        | `PoliceCatchController.cs`   | `FailMultiplierByStage` hardcoded dictionary                 |
| Police fail popularity gain per stage                   | `PoliceCatchController.cs`   | `FailPopGainByStage` hardcoded dictionary                    |
| Popularity stage thresholds                             | `PopularityManager.cs`       | `StageThresholds` static readonly array                      |
| Momentum level scaling                                  | `MomentumController.cs`      | Level config methods                                         |
| Card upgrade cost tiers (×1.4/×1.6/×1.85)               | `CardDefinition.cs`          | `GetUpgradeCost()` method                                    |
| Card copies per upgrade (8)                             | `CardDefinition.cs`          | `CopiesNeeded` constant                                      |
| Upgrade base cost & multiplier                          | `UpgradeButton.cs`           | `baseCost` (10), `costMultiplier` (1.15)                     |
| Building tiered cost multiplier override                | `BuildingManager.cs`         | `GetEffectiveCostMultiplier()` method                        |
| Chest unlock durations (20/40/60 min)                   | `ChestTypeDefs.cs`           | `GetConfig()` → `unlockDurationSeconds`                      |
| Chest open-now costs (15/50/100 NC)                     | `ChestTypeDefs.cs`           | `GetConfig()` → `openNowCost`                                |
| Chest gold/nitro reward percentages                     | `ChestTypeDefs.cs`           | `GetConfig()` → `moneyPercentMin/Max`, `nitroPercentMin/Max` |
| Chest spawn weight progression                          | `ChestTypeDefs.cs`           | Spawn weight interpolation tables                            |
| Card rarity weights per chest type                      | `ChestTypeDefs.cs`           | `GetConfig()` → `cardRarityWeights`                          |
| Card drop rarity/weight configs                         | `CardDropTuning.cs`          | `CardConfigs` static dictionary                              |
| Card level decay formula                                | `CardDropTuning.cs`          | `LevelDecay()` method                                        |
| Segment multiplier drop weights                         | `CardDropTuning.cs`          | Segment multiplier arrays                                    |
| Daily offers refresh (24h)                              | `DailyOffersController.cs`   | Time check logic                                             |
| Daily offers free reward split (1/3)                    | `DailyOffersController.cs`   | `GenerateFreeOffer()` method                                 |

---

**END OF AUDIT**

_This report covers every script, ScriptableObject, and Inspector-configured value that affects the game's economy. Blacklist tier mission targets and rewards are Inspector-serialized on the scene's BlacklistManager component and cannot be extracted from code alone — those values require in-Editor inspection._
