# Building Purchase Economy - Test Checklist

## Setup

1. Enable `BuildingManager.enableEconomyDebugLogs` in Unity Inspector
2. Open Unity Console and clear logs
3. Start Play mode

## Test 1: Basic Purchase Flow (Any Building)

**Objective**: Verify money is spent correctly and cost increases exponentially

**Example Building**: EscapeDriver (assuming: baseCost=100, costMultiplier=1.15, baseProduction=2)

### Steps:

1. Set player money to **10,000** (use debug menu or script)
2. Note current building count (should be 0 if fresh)
3. Click buy button **3 times**

### Expected Results:

**Purchase 1** (count 0 → 1):

- Cost = 100 × 1.15^0 = **100.00**
- Money: 10,000 → 9,900
- MPS: 0 → 2
- Next cost = 100 × 1.15^1 = **115.00**

**Purchase 2** (count 1 → 2):

- Cost = 100 × 1.15^1 = **115.00**
- Money: 9,900 → 9,785
- MPS: 2 → 4
- Next cost = 100 × 1.15^2 = **132.25**

**Purchase 3** (count 2 → 3):

- Cost = 100 × 1.15^2 = **132.25**
- Money: 9,785 → 9,652.75
- MPS: 4 → 6
- Next cost = 100 × 1.15^3 = **152.09**

### Log Pattern to Verify:

```
[BUY_ATTEMPT] type=EscapeDriver | countBefore=0 | cost=100.00 | moneyBefore=10000.00 | canAfford=true
[BUY_RESULT] success=true | moneyAfter=9900.00 | countAfter=1 | mpsBefore=0.00 | mpsAfter=2.00 | mpsExpectedDelta=2.00 | mpsActualDelta=2.00
[NEXT_COST] type=EscapeDriver | countNow=1 | nextCost=115.00
```

### Pass Criteria:

- ✅ Money spent exactly equals calculated cost (tolerance ±0.01)
- ✅ MPS increases exactly by baseProduction each purchase
- ✅ Next cost matches formula: baseCost × multiplier^(new count)
- ✅ No [ECONOMY_BUG] warnings in logs

---

## Test 2: StreetDeals Special Behavior

**Objective**: Verify tap bonus increases alongside MPS

**Building**: StreetDeals (ID=0, tapBonusPerLevel should be 1.0)

### Steps:

1. Set player money to **10,000**
2. Note current moneyPerTap (base is 1.0)
3. Buy StreetDeals **2 times**

### Expected Results:

**Purchase 1**:

- MPS increases by baseProduction (e.g., 0.5 or 2, depends on Inspector value)
- **moneyPerTap increases by 1.0** (if tapBonusPerLevel=1.0)

**Purchase 2**:

- MPS increases again
- **moneyPerTap increases by another 1.0**

### Log Pattern:

```
[BUY_RESULT] ... mptExpectedDelta=1.00 | mptActualDelta=1.00 ...
```

### Pass Criteria:

- ✅ moneyPerTap increases by tapBonusPerLevel each purchase
- ✅ No mismatch between mptExpectedDelta and mptActualDelta

---

## Test 3: Exponential Cost Scaling (10 Purchases)

**Objective**: Verify cost doesn't plateau or break after many purchases

### Steps:

1. Set money to **1,000,000**
2. Pick one building
3. Buy it **10 times** rapidly
4. Export console logs

### Expected Behavior:

- Each purchase cost = baseCost × multiplier^countBefore
- Cost increases continuously (no plateau)
- Costs form geometric sequence: `[100, 115, 132.25, 152.09, ...]`

### Pass Criteria:

- ✅ All 10 purchases show correct exponential growth
- ✅ No cost calculated from countAfter (would cause off-by-one)

---

## Test 4: Cannot Afford

**Objective**: Verify purchase fails gracefully when insufficient funds

### Steps:

1. Set money to **50**
2. Try to buy a building that costs 100

### Expected Results:

- `[BUY_ATTEMPT]` log shows `canAfford=false`
- No `[BUY_RESULT]` log (purchase rejected)
- Money unchanged (still 50)
- Count unchanged

### Pass Criteria:

- ✅ No money deducted
- ✅ Count not incremented
- ✅ No MPS change

---

## Test 5: Max Count Limit

**Objective**: Verify purchases stop at maxCount

### Steps:

1. In Inspector, set a building's maxCount to **3**
2. Give player enough money
3. Try to buy it **5 times**

### Expected Results:

- First 3 purchases succeed
- 4th and 5th purchases show "Max level reached" log
- Count stops at 3

### Pass Criteria:

- ✅ No purchases beyond maxCount
- ✅ No money lost on failed attempts

---

## Test 6: UI Consistency

**Objective**: Verify displayed cost matches actual cost

### Steps:

1. Note building button's displayed cost before purchase
2. Buy the building
3. Check new displayed cost

### Pass Criteria:

- ✅ Displayed cost (from UI) matches logged cost in [BUY_ATTEMPT]
- ✅ After purchase, displayed cost updates to [NEXT_COST] value
- ✅ "Owned: X" count updates immediately

---

## Test 7: Multiple Buildings Interaction

**Objective**: Verify MPS is cumulative across all buildings

### Steps:

1. Buy 1× StreetDeals (baseProduction=2) → MPS should be 2
2. Buy 1× EscapeDriver (baseProduction=5) → MPS should be 7
3. Buy another StreetDeals → MPS should be 9

### Pass Criteria:

- ✅ MPS = sum of (count × baseProduction) for all buildings
- ✅ Each purchase adds only its own baseProduction

---

## Common Bugs Checklist

### ❌ BUG: Cost calculated from countAfter

**Symptom**: First purchase costs multiplier × baseCost instead of baseCost  
**Cause**: Calling GetCurrentCost(type) after count++  
**Status**: ✅ VERIFIED CORRECT - cost calculated before increment

### ❌ BUG: MPS overwritten instead of accumulated

**Symptom**: Buying a building resets MPS to baseProduction  
**Cause**: Using = instead of += in IncreaseMPS  
**Status**: ✅ VERIFIED CORRECT - uses += operator

### ❌ BUG: UI shows stale cost

**Symptom**: Button shows wrong cost after purchase  
**Cause**: Not calling RefreshData after successful purchase  
**Status**: ✅ VERIFIED CORRECT - RefreshData(true) called

### ❌ BUG: Double-counting on event handlers

**Symptom**: MPS increases by 2× baseProduction  
**Cause**: Multiple listeners on OnBuildingPurchased incorrectly adding MPS  
**Status**: ⚠️ REVIEW NEEDED - check if any external code subscribes and calls IncreaseMPS

### ❌ BUG: Floating-point precision loss

**Symptom**: After many purchases, money/cost slightly off  
**Cause**: Using float instead of double  
**Status**: ✅ VERIFIED CORRECT - all economy values use double

### ❌ BUG: Save/load MPS desync

**Symptom**: After loading, MPS doesn't match building totals  
**Cause**: RecalculateMPSFromBuildings overwrites MPS without re-adding upgrades  
**Status**: ✅ RECENTLY FIXED - upgrade reapply logic added

---

## How to Read Logs

### Successful Purchase:

```
[BUY_ATTEMPT] type=X | countBefore=5 | cost=201.14 | moneyBefore=10000.00 | canAfford=true
[BUY_RESULT] success=true | moneyAfter=9798.86 | countAfter=6 | mpsBefore=50.00 | mpsAfter=52.00 | mpsExpectedDelta=2.00 | mpsActualDelta=2.00 | ...
[NEXT_COST] type=X | countNow=6 | nextCost=231.31
```

✅ All deltas match expectations = PASS

### Detected Bug:

```
[ECONOMY_BUG] Money spent mismatch! Expected=100.00, Actual=99.99, Diff=0.01
```

❌ Investigate cause of mismatch

---

## Manual Validation Formula

For any building purchase `n` (where count goes from `c` to `c+1`):

```
expectedCost = baseCost × costMultiplier^c
expectedMoneyAfter = moneyBefore - expectedCost
expectedMpsAfter = mpsBefore + baseProduction
expectedMptAfter = mptBefore + tapBonusPerLevel  // if tapBonusPerLevel > 0
expectedNextCost = baseCost × costMultiplier^(c+1)
```

Use these formulas to verify logs manually if automated checks fail.

---

## Quick Test via Unity Console

```csharp
// Paste in Unity immediate window (if available) or create a test button
CurrencyManager.Instance.money = 1000000;
BuildingManager.Instance.enableEconomyDebugLogs = true;
// Then click buy buttons and watch console
```
