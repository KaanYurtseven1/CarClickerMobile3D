# ECONOMY TUNING GUIDE

Actionable changes only. Grouped by system. Every item tells you **what**, **where**, **current**, **recommended**, and **why**.

Legend: **Inspector** = change in Unity Inspector. **Code** = edit `.cs` file. **SO** = ScriptableObject asset.

---

## 1. GOLD — TAPPING

| #   | What                       | Where                                                                                        | Current           | Recommended         | Why                                                                               |
| --- | -------------------------- | -------------------------------------------------------------------------------------------- | ----------------- | ------------------- | --------------------------------------------------------------------------------- |
| 1   | Base money per tap         | **Inspector** → Main scene → `GameManager` → CurrencyManager → `moneyPerTap`                 | 1.0               | 5.0                 | Base 1 is irrelevant within minutes; 5 gives tapping a longer runway              |
| 2   | Tap upgrade +/level        | **Code** → `UpgradeButton.cs` → tap upgrade logic                                            | +1 per level      | +3 per level        | Keeps tap income closer to MPS growth                                             |
| 3   | TurboFinger multipliers    | **Code** → `TurboFingerController.cs` → `LevelMultipliers` array                             | {1,2,3,5,7,10,14} | {2,4,6,10,15,22,30} | Current L1=×1 is no boost at all; stronger curve keeps tapping exciting           |
| 4   | TurboFinger tap window     | **Inspector** → Main → `TurboFingerCtrl` → TurboFingerController → `tapWindowSeconds`        | 15                | 20                  | 50 taps in 15s is 3.3 TPS — tight for casual; 20s is more forgiving               |
| 5   | TurboFinger cooldown       | **Inspector** → Main → `TurboFingerCtrl` → TurboFingerController → `cooldownDurationSeconds` | 120               | 90                  | 2 min CD feels punishing; 90s keeps it active enough to matter                    |
| 6   | Momentum reset window (L1) | **Inspector** → Main → `MomentumCtrl` → MomentumController → `baseResetWindow`               | 0.8s              | 1.2s                | 0.8s punishes any pause; 1.2s lets casual players maintain stacks                 |
| 7   | Momentum stack cap (L1)    | **Inspector** → Main → `MomentumCtrl` → MomentumController → `baseStackCap`                  | 30                | 20                  | Reach max faster → feel the payoff sooner; compensate with higher per-stack bonus |
| 8   | Momentum per-stack bonus   | **Inspector** → Main → `MomentumCtrl` → MomentumController → `basePerStackBonus`             | 0.005 (×1.005)    | 0.015 (×1.015)      | At 20 stacks: ×1.35 instead of ×1.10 — actually noticeable                        |

---

## 2. GOLD — PASSIVE MPS

| #   | What                          | Where                                                                                    | Current           | Recommended       | Why                                                                      |
| --- | ----------------------------- | ---------------------------------------------------------------------------------------- | ----------------- | ----------------- | ------------------------------------------------------------------------ |
| 1   | MPS upgrade +/level           | **Code** → `UpgradeButton.cs` → MPS upgrade logic                                        | +1 per level      | +2 per level      | Flat +1 is trivial after first buildings                                 |
| 2   | Global upgrade multiplier     | **Code** → `UpgradeButton.cs` → global upgrade                                           | ×1.1 per level    | ×1.12 per level   | Slightly faster snowball; compounds meaningfully over 20+ levels         |
| 3   | GarageManager MPS multipliers | **Code** → `GarageManagerController.cs` → `BonusMultipliers` array                       | ×10–×15 by level  | ×15–×25 by level  | 60s of ×10 is good early but weak late; higher ceiling keeps it relevant |
| 4   | GarageManager cooldown        | **Inspector** → Main → `GarageManagerCtrl` → GarageManagerController → `cooldownSeconds` | 120               | 90                | 2 min → 1.5 min; faster cycles = more engagement                         |
| 5   | GarageManager spend threshold | **Code** → `GarageManagerController.cs` → `SpendSecondsEquivalents` array                | 30,28,26,24,22,20 | 25,22,19,16,14,12 | Easier to charge = more frequent use                                     |

---

## 3. GOLD — BUILDINGS

| #   | What                            | Where                                                                                        | Current               | Recommended                | Why                                                                                        |
| --- | ------------------------------- | -------------------------------------------------------------------------------------------- | --------------------- | -------------------------- | ------------------------------------------------------------------------------------------ |
| 1   | Tiered cost multiplier (T21–27) | **Code** → `BuildingManager.cs` → `GetEffectiveCostMultiplier()`                             | 1.25                  | 1.20                       | Late buildings already have astronomical base costs; 1.25× stacking makes them unreachable |
| 2   | Building 27 maxCount            | **Inspector** → Main → `GameManager` → BuildingManager → `buildings[27].maxCount`            | 999                   | 50                         | Endgame building with 999 cap is meaningless; 50 matches similar-tier buildings            |
| 3   | Validator expected cost ratio   | **Code** → `BuildingManager.cs` → `ValidateBaseCostGaps()`                                   | expects 8–12×         | change to 3–7×             | Actual ratios are 4–6.67×; validator should match reality                                  |
| 4   | Scene costMultiplier values     | **Inspector** → Main → `GameManager` → BuildingManager → `buildings[]` each `costMultiplier` | 1.10–1.15 (dead data) | Set to match tiered values | These are overridden at runtime — update them to avoid confusing designers                 |
| 5   | StreetDeals tapBonusPerLevel    | **Inspector** → Main → `GameManager` → BuildingManager → `buildings[0].tapBonusPerLevel`     | 0 (force-set to 1.0)  | Set to 1.0 in scene        | Remove the runtime override — just set the correct value in Inspector                      |

---

## 4. GOLD — POLICE CHASE

| #   | What                    | Where                                                                                  | Current                  | Recommended               | Why                                                        |
| --- | ----------------------- | -------------------------------------------------------------------------------------- | ------------------------ | ------------------------- | ---------------------------------------------------------- |
| 1   | Stage 6 fail multiplier | **Code** → `PoliceCatchController.cs` → `FailMultiplierByStage` dict                   | 0.50 (lose 50%)          | 0.70 (lose 30%)           | Losing half your gold is rage-quit territory; cap the pain |
| 2   | Stage 5 fail multiplier | **Code** → `PoliceCatchController.cs` → `FailMultiplierByStage` dict                   | 0.60 (lose 40%)          | 0.72 (lose 28%)           | Same reasoning — flatten the penalty curve at high stages  |
| 3   | Escape gold reward      | **Code** → `PoliceCatchController.cs` → `HandleChaseEnd()`                             | floor(money / 8) = 12.5% | floor(money / 6) = ~16.7% | Slightly more generous reward makes chases worth engaging  |
| 4   | Max chase duration      | **Inspector** → Main → `GameManager` → PoliceCatchController → `maxChaseDuration`      | 12s                      | 10s                       | Shorter chase = more intense, less tedious                 |
| 5   | Min time between chases | **Inspector** → Main → `GameManager` → PoliceCatchTrigger → `minimumTimeBetweenChases` | 15s                      | 20s                       | A bit more breathing room between events                   |

---

## 5. GOLD — DAILY OFFERS & OFFLINE

| #   | What                       | Where                                                                                                  | Current                        | Recommended                              | Why                                                                     |
| --- | -------------------------- | ------------------------------------------------------------------------------------------------------ | ------------------------------ | ---------------------------------------- | ----------------------------------------------------------------------- |
| 1   | ~~Free gold reward~~ ✅    | **Inspector** → Main → `Section_DailyOffers` → DailyOffersController → `freeMoneyMin` / `freeMoneyMax` | ~~50 / 200~~ → 1–3% of balance | ✅ DONE — scales with player money       | Implemented: `currentMoney * Random(0.01, 0.03)` min 50                 |
| 2   | ~~Free gold scaling~~ ✅   | **Code** → `DailyOffersController.cs` → `GrantFreeReward()`                                            | ~~Hardcoded Random(50,200)~~   | ✅ DONE                                  | Now uses `Math.Max(freeMoneyMin, currentMoney * Random(0.01f, 0.03f))`  |
| 3   | Free nitro reward          | **Inspector** → Main → `Section_DailyOffers` → DailyOffersController → `freeNitroMin` / `freeNitroMax` | 1 / 5                          | 3 / 10                                   | 1–5 NC is paltry; 3–10 feels worth clicking                             |
| 4   | ~~Free chest (BROKEN)~~ ✅ | **Code** → `DailyOffersController.cs` → `OpenFreeCommonChest()`                                        | ~~Placeholder (gave nothing)~~ | ✅ DONE — opens Common chest via session | Now uses same ChestSessionManager pipeline as Blacklist free chests     |
| 5   | PitStopCrew L1 efficiency  | **Code** → `PitStopCrewController.cs` → `EfficiencyByLevel[]`                                          | 20%                            | 25%                                      | 20% of MPS for 2h offline is low — bump slightly for better return feel |

---

## 6. GOLD — MISC SINKS

| #   | What                     | Where                                                                         | Current               | Recommended                             | Why                                                                          |
| --- | ------------------------ | ----------------------------------------------------------------------------- | --------------------- | --------------------------------------- | ---------------------------------------------------------------------------- |
| 1   | Garage part prices (all) | **SO** → `Assets/SO/Garage/GarageShopConfig.asset` → part price arrays        | 500–5,000             | 5,000–50,000                            | Current total (31K gold) is trivial; 10× makes garage a real mid-game sink   |
| 2   | Spoiler_5 sentinel       | **SO** → `Assets/SO/Garage/GarageShopConfig.asset` → spoiler array last entry | 5,000,000,000,000,000 | Remove or set real price (e.g. 100,000) | 5 quadrillion is a placeholder — decide if this part exists or not           |
| 3   | Card copies per upgrade  | **Code** → `CardDefinition.cs` → `CopiesNeeded`                               | 8                     | 5                                       | 8 copies per level is a grind wall; 5 is still gated but less frustrating    |
| 4   | Upgrade base cost        | **Code** → `UpgradeButton.cs` → `baseCost`                                    | 10                    | 25                                      | 10 gold is too cheap even at game start; 25 feels like a real first purchase |

---

## 7. NITRO COINS — SOURCES

| #   | What                             | Where                                                                                                  | Current        | Recommended     | Why                                                                 |
| --- | -------------------------------- | ------------------------------------------------------------------------------------------------------ | -------------- | --------------- | ------------------------------------------------------------------- |
| 1   | World spawn interval             | **Inspector** → Main → `NitroCoinSpawner` → NitroCoinSpawner → `minSpawnInterval` / `maxSpawnInterval` | 10 / 15        | 8 / 12          | Faster nitro flow early; reduces early-game scarcity                |
| 2   | Nitro coin per pickup            | **Inspector** (Prefab) → `Assets/Prefabs/NitroCoin.prefab` → NitroCoin → `rewardAmount`                | 1              | 1 (keep)        | Fine as-is; rain and magnet provide bulk                            |
| 3   | NitroRain delay                  | **Inspector** → Main → `NitroRainCtrl` → NitroRainController → `delaySeconds`                          | 30             | 20              | 30s wait after threshold feels too long; 20s keeps momentum         |
| 4   | NitroRain required collects L1   | **Code** → `NitroRainController.cs` → `RequiredCollects[]`                                             | 5              | 4               | Easier first rain trigger for new players                           |
| 5   | NitroMagnet cooldown base        | **Inspector** → Main → `NitroMagnetCtrl` → NitroMagnetController → `cooldownBase`                      | 60             | 45              | 60s base CD is long; 45s is more engaging                           |
| 6   | NitroMagnet rain CD multiplier   | **Inspector** → Main → `NitroMagnetCtrl` → NitroMagnetController → `rainCooldownMultiplier`            | 2.0            | 1.5             | ×2 during rain (up to 420s at L6) is too harsh; 1.5 keeps it usable |
| 7   | Police escape nitro (all stages) | **Code** → `PoliceCatchController.cs` → `RewardCoinsByStage`                                           | 3,5,8,12,18,25 | 5,8,12,18,25,35 | Bump all by ~2; makes police escapes a meaningful nitro source      |

---

## 8. NITRO COINS — SINKS

| #   | What                        | Where                                                                                      | Current        | Recommended                                      | Why                                                                                     |
| --- | --------------------------- | ------------------------------------------------------------------------------------------ | -------------- | ------------------------------------------------ | --------------------------------------------------------------------------------------- |
| 1   | Common chest skip cost      | **Code** → `ChestTypeDefs.cs` → `GetConfig()` → `openNowCost`                              | 15 NC          | 10 NC                                            | Lower barrier to spend nitro = better sink velocity                                     |
| 2   | Color #6 sentinel           | **SO** → `Assets/SO/Garage/GarageShopConfig.asset` → color array last                      | 2,147,483,647  | Remove slot or set 150 NC                        | MAX_INT placeholder — decide on real price or remove                                    |
| 3   | Sticker #6 sentinel         | **SO** → `Assets/SO/Garage/GarageShopConfig.asset` → sticker array last                    | 2,147,483,647  | Remove slot or set 120 NC                        | Same — placeholder                                                                      |
| 4   | Daily card pack Slot 2      | **Inspector** → Main → `Section_DailyOffers` → DailyOffersController → `slot2Price`        | 15 NC          | 12 NC                                            | Slightly cheaper = more purchases = better NC drain                                     |
| 5   | Daily card pack copies      | **Inspector** → Main → `Section_DailyOffers` → DailyOffersController → `copiesPerPurchase` | 5              | 3                                                | With card copies per upgrade dropping to 5, reduce pack value to keep progression paced |
| 6   | **NEW: NC temp boost shop** | **Code** — new system needed                                                               | Does not exist | Add 50/100/200 NC options for 5/15/30 min ×2 MPS | Late-game nitro has no purpose; this creates an ongoing sink                            |

---

## 9. BOOST MODE

| #   | What                    | Where                                                                                  | Current | Recommended | Why                                                |
| --- | ----------------------- | -------------------------------------------------------------------------------------- | ------- | ----------- | -------------------------------------------------- |
| 1   | L1 charge threshold     | **Code** → `BoostModeController.cs` → `GetBoostParamsForLevel()` switch                | 5 nitro | 4 nitro     | Easier first activation                            |
| 2   | L1 duration             | **Code** → `BoostModeController.cs` → same switch                                      | 6s      | 8s          | 6s is too brief to feel impactful                  |
| 3   | L1 cooldown             | **Code** → `BoostModeController.cs` → same switch                                      | 60s     | 45s         | Faster cycle                                       |
| 4   | Inspector base cooldown | **Inspector** → Main → `BoostModeController` → BoostModeController → `cooldownSeconds` | 30      | 30 (keep)   | Scene value is fine; per-level switch overrides it |
| 5   | Inspector maxCharge     | **Inspector** → Main → `BoostModeController` → BoostModeController → `maxCharge`       | 20      | 20 (keep)   | Used as fallback; fine                             |

---

## 10. SMALL INVESTMENT

| #   | What             | Where                                                                                         | Current | Recommended | Why                                                       |
| --- | ---------------- | --------------------------------------------------------------------------------------------- | ------- | ----------- | --------------------------------------------------------- |
| 1   | Base refund %    | **Inspector** → Main → `SmallInvestmentCtrl` → SmallInvestmentController → `basePercent`      | 2       | 5           | 2% is invisible; 5% is noticeable                         |
| 2   | Step % per level | **Inspector** → Main → `SmallInvestmentCtrl` → SmallInvestmentController → `stepPercent`      | 2       | 3           | Scaling: 5→8→11→14→17→20% at L6 instead of 2→4→6→8→10→12% |
| 3   | Max refund %     | **Inspector** → Main → `SmallInvestmentCtrl` → SmallInvestmentController → `maxRefundPercent` | 12      | 20          | L6 at 20% feels rewarding; old 12% was barely noticeable  |

---

## 11. CHESTS

| #   | What                                | Where                                                                                          | Current        | Recommended    | Why                                                                                        |
| --- | ----------------------------------- | ---------------------------------------------------------------------------------------------- | -------------- | -------------- | ------------------------------------------------------------------------------------------ |
| 1   | Common unlock time                  | **Code** → `ChestTypeDefs.cs` → `GetConfig()` → `unlockDurationSeconds`                        | 1200s (20 min) | 600s (10 min)  | 20 min is too long for a common chest; 10 min fits casual sessions                         |
| 2   | Rare unlock time                    | **Code** → same                                                                                | 2400s (40 min) | 1500s (25 min) | Faster cycle → more engagement                                                             |
| 3   | Legendary unlock time               | **Code** → same                                                                                | 3600s (60 min) | 2400s (40 min) | Still feels premium but not punishing                                                      |
| 4   | Chest spawn interval                | **Inspector** → Main → `ChestSpawner` → ChestSpawner → `minSpawnInterval` / `maxSpawnInterval` | 5 / 15         | 10 / 25        | Slightly slower spawning avoids inventory overflow and makes each chest feel more valuable |
| 5   | Legendary base weight (commonChest) | **Code** → `ChestTypeDefs.cs` → card rarity weights for Common chest                           | 0 (disabled)   | 3              | Zero legendary drops from common chests is harsh; 3% gives hope                            |
| 6   | Max chest slots                     | **Code** → `ChestInventoryManager.cs` → hardcoded                                              | 5              | 6              | One extra slot reduces "waste" chest spawns                                                |

---

## 12. HEAT & RADAR PACING

| #   | What                     | Where                                                                              | Current | Recommended | Why                                                                   |
| --- | ------------------------ | ---------------------------------------------------------------------------------- | ------- | ----------- | --------------------------------------------------------------------- |
| 1   | Passive heat rate        | **Inspector** → Main → `GameManager` → AmbientHeatManager → `passiveHeatPerSecond` | 0.2     | 0.15        | 350s to threshold → ~467s; slightly more relaxed pacing               |
| 2   | Radar miss heat          | **Inspector** → Main → `GameManager` → AmbientHeatManager → `missHeatGain`         | 6       | 5           | Slightly less punishing per miss                                      |
| 3   | Chase end heat drop      | **Inspector** → Main → `GameManager` → AmbientHeatManager → `chaseEndHeatDrop`     | 45      | 50          | More reset after chase = longer breather                              |
| 4   | Radar spawn max interval | **Inspector** → Main → `RadarSpawner` → RadarSpawner → `maxSpawnInterval`          | 20      | 18          | Slightly more frequent radars = more gameplay, more nitro opportunity |
| 5   | Post-chase cooldown      | **Inspector** → Main → `GameManager` → AmbientHeatManager → `postChaseCooldown`    | 30      | 35          | A bit more breathing room                                             |

---

## 13. GARAGE (COSMETICS)

| #   | What                   | Where                                              | Current       | Recommended            | Why                                                    |
| --- | ---------------------- | -------------------------------------------------- | ------------- | ---------------------- | ------------------------------------------------------ |
| 1   | All part prices (gold) | **SO** → `Assets/SO/Garage/GarageShopConfig.asset` | 500–5,000     | 5,000–50,000           | See #6.1 — current values are not a meaningful sink    |
| 2   | Color prices (nitro)   | **SO** → same asset → color array                  | 0,50,50,75,75 | 0,60,80,120,180        | Higher prices = better NC drain + sense of progression |
| 3   | Sticker prices (nitro) | **SO** → same asset → sticker array                | 0,30,40,50,60 | 0,40,60,90,130         | Same reasoning                                         |
| 4   | Spoiler_5              | **SO** → same asset → spoiler array last           | 5 quadrillion | 100,000 gold or remove | Placeholder — needs a real decision                    |
| 5   | Color #6 / Sticker #6  | **SO** → same asset → last entries                 | MAX_INT       | 300 NC or remove slot  | Placeholder — needs a real decision                    |

---

## 14. BLACKLIST / CARDS

| #   | What                               | Where                                                              | Current         | Recommended                                               | Why                                                                               |
| --- | ---------------------------------- | ------------------------------------------------------------------ | --------------- | --------------------------------------------------------- | --------------------------------------------------------------------------------- |
| 1   | Card copies per upgrade            | **Code** → `CardDefinition.cs` → `CopiesNeeded`                    | 8               | 5                                                         | 8 per level is a steep grind; 5 by 15 levels = 75 total still requires investment |
| 2   | Card upgrade cost tier 1 mult      | **Code** → `CardDefinition.cs` → `GetUpgradeCost()`                | ×1.4 per level  | ×1.35 per level                                           | Slightly slower scaling = more affordable mid-levels                              |
| 3   | Blacklist tier rewards             | **SO** → `Assets/Prefabs/Blacklist/BlacklistTier_1-5.asset` (each) | Inspector-set   | Review in editor — ensure rewards scale with tier         | Cannot audit from code; verify gold/nitro amounts in Inspector                    |
| 4   | PitStopCrew formula in CardManager | **Code** → `CardManager.cs` line ~289                              | `0.05f * level` | Delete or match `PitStopCrewController.EfficiencyByLevel` | Dead code — causes confusion; Controller values are correct                       |

---

## 15. BUG FIXES (non-tuning, but required)

| #   | What                          | Where                                                                     | Current                                                            | Fix                                                              |
| --- | ----------------------------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------ | ---------------------------------------------------------------- |
| 1   | ~~Free chest daily offer~~ ✅ | **Code** → `DailyOffersController.cs` → `OpenFreeCommonChest()`           | ~~33% chance grants nothing~~                                      | ✅ DONE — opens Common chest via ChestSessionManager             |
| 2   | AdProvider dummy              | **Code** → `AdProvider.cs`                                                | Always auto-succeeds                                               | Replace with real ad SDK before launch                           |
| 3   | TurboFinger comment mismatch  | **Code** → `TurboFingerController.cs` line ~8                             | Says "x5/x10/x20/x50/x100/x200"                                    | Update to match actual array `{1,2,3,5,7,10,14}` (or new values) |
| 4   | Dead code: CardManager legacy | **Code** → `CardManager.cs`                                               | `turboFingerTapBonusCached`, `garageManagerPercentCached` always 0 | Remove dead fields and constants                                 |
| 5   | Dead code: ChestRewardReveal  | **Code** → `ChestRewardRevealController.cs` lines ~33-34                  | Unused `MoneyMultipliers`, `NitroAmounts` arrays                   | Remove — replaced by %-based system                              |
| 6   | Dead scene costMultiplier     | **Inspector** → Main → BuildingManager → each building's `costMultiplier` | 1.10–1.15 (overridden)                                             | Set to match tiered values or remove field                       |

---

## QUICK REFERENCE: INSPECTOR CHANGES ONLY

All changes you can make **without touching code**, directly in Unity Inspector:

| GameObject          | Component                 | Field                           | Current | Recommended |
| ------------------- | ------------------------- | ------------------------------- | ------- | ----------- |
| GameManager         | CurrencyManager           | `moneyPerTap`                   | 1.0     | 5.0         |
| TurboFingerCtrl     | TurboFingerController     | `tapWindowSeconds`              | 15      | 20          |
| TurboFingerCtrl     | TurboFingerController     | `cooldownDurationSeconds`       | 120     | 90          |
| MomentumCtrl        | MomentumController        | `baseResetWindow`               | 0.8     | 1.2         |
| MomentumCtrl        | MomentumController        | `baseStackCap`                  | 30      | 20          |
| MomentumCtrl        | MomentumController        | `basePerStackBonus`             | 0.005   | 0.015       |
| GarageManagerCtrl   | GarageManagerController   | `cooldownSeconds`               | 120     | 90          |
| SmallInvestmentCtrl | SmallInvestmentController | `basePercent`                   | 2       | 5           |
| SmallInvestmentCtrl | SmallInvestmentController | `stepPercent`                   | 2       | 3           |
| SmallInvestmentCtrl | SmallInvestmentController | `maxRefundPercent`              | 12      | 20          |
| GameManager         | PoliceCatchController     | `maxChaseDuration`              | 12      | 10          |
| GameManager         | PoliceCatchTrigger        | `minimumTimeBetweenChases`      | 15      | 20          |
| GameManager         | AmbientHeatManager        | `passiveHeatPerSecond`          | 0.2     | 0.15        |
| GameManager         | AmbientHeatManager        | `missHeatGain`                  | 6       | 5           |
| GameManager         | AmbientHeatManager        | `chaseEndHeatDrop`              | 45      | 50          |
| GameManager         | AmbientHeatManager        | `postChaseCooldown`             | 30      | 35          |
| NitroCoinSpawner    | NitroCoinSpawner          | `minSpawnInterval`              | 10      | 8           |
| NitroCoinSpawner    | NitroCoinSpawner          | `maxSpawnInterval`              | 15      | 12          |
| NitroRainCtrl       | NitroRainController       | `delaySeconds`                  | 30      | 20          |
| NitroMagnetCtrl     | NitroMagnetController     | `cooldownBase`                  | 60      | 45          |
| NitroMagnetCtrl     | NitroMagnetController     | `rainCooldownMultiplier`        | 2.0     | 1.5         |
| ChestSpawner        | ChestSpawner              | `minSpawnInterval`              | 5       | 10          |
| ChestSpawner        | ChestSpawner              | `maxSpawnInterval`              | 15      | 25          |
| RadarSpawner        | RadarSpawner              | `maxSpawnInterval`              | 20      | 18          |
| Section_DailyOffers | DailyOffersController     | `freeNitroMin`                  | 1       | 3           |
| Section_DailyOffers | DailyOffersController     | `freeNitroMax`                  | 5       | 10          |
| Section_DailyOffers | DailyOffersController     | `slot2Price`                    | 15      | 12          |
| Section_DailyOffers | DailyOffersController     | `copiesPerPurchase`             | 5       | 3           |
| GameManager         | BuildingManager           | `buildings[27].maxCount`        | 999     | 50          |
| GameManager         | BuildingManager           | `buildings[0].tapBonusPerLevel` | 0       | 1.0         |

**ScriptableObject changes** (Project Window):

| Asset                                     | Field          | Current           | Recommended         |
| ----------------------------------------- | -------------- | ----------------- | ------------------- |
| `Assets/SO/Garage/GarageShopConfig.asset` | Part prices    | 500–5,000         | 5,000–50,000        |
| Same                                      | Color prices   | 0,50,50,75,75,MAX | 0,60,80,120,180,300 |
| Same                                      | Sticker prices | 0,30,40,50,60,MAX | 0,40,60,90,130,120  |
| Same                                      | Spoiler_5      | 5 quadrillion     | 100,000 or remove   |

---

**Total: 80+ actionable items across 15 categories.**
