using UnityEngine;
using System;
using System.Collections.Generic;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    /// <summary>
    /// Fired after a building is purchased. UI can subscribe to avoid polling.
    /// Parameters: BuildingType, newCount
    /// </summary>
    public event Action<BuildingType, int> OnBuildingPurchased;

    /// <summary>
    /// DEBUG: Enable detailed economy validation logs for building purchases.
    /// Toggle this in Inspector to trace money flow, cost progression, and MPS changes.
    /// </summary>
    [Header("Debug")]
    public bool enableEconomyDebugLogs = false;

    public BuildingDefinition[] buildings; // Inspector'dan dolduracagiz

    // Dictionary cache for O(1) lookup instead of O(n) array search
    private Dictionary<BuildingType, BuildingDefinition> _buildingLookup;
    private bool _lookupInitialized = false;

    // Maximum reasonable count for any single building during early/mid game.
    // Prevents scene-serialized stale counts or corrupt saves from inflating MPS.
    // Late-game buildings (baseCost > 1e15) are capped more aggressively.
    private const int EARLY_BUILDING_MAX_SANITY = 500;
    private const int LATE_BUILDING_MAX_SANITY = 100;
    private const double LATE_BUILDING_COST_THRESHOLD = 1e15;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // CRITICAL: Reset all building counts to 0 on initialization.
        // The scene file may have non-zero counts baked into serialized data
        // (e.g. from Inspector edits). These are NOT gameplay state — they are
        // design-time artifacts. Real counts come from SaveSystem.LoadGame().
        ResetAllBuildingCounts();

        InitializeLookup();
    }

    /// <summary>
    /// Zeroes all building counts. Called in Awake before any save data is loaded,
    /// to prevent scene-serialized stale counts from polluting runtime state.
    /// </summary>
    private void ResetAllBuildingCounts()
    {
        if (buildings == null) return;
        foreach (var b in buildings)
        {
            if (b != null && b.count != 0)
            {
                Debug.LogWarning($"[BuildingManager] Resetting scene-baked count for '{b.displayName}' (type={b.type}): was {b.count}, now 0");
                b.count = 0;
            }
        }
    }

    /// <summary>
    /// Builds dictionary from buildings array for fast lookup.
    /// Called once on Awake and can be called again if buildings array changes.
    /// </summary>
    public void InitializeLookup()
    {
        _buildingLookup = new Dictionary<BuildingType, BuildingDefinition>();
        if (buildings != null)
        {
            foreach (var b in buildings)
            {
                if (b != null && !_buildingLookup.ContainsKey(b.type))
                {
                    _buildingLookup[b.type] = b;
                }
            }
        }
        // Defensive default: StreetDeals tap bonus (was hardcoded, now data-driven).
        // If designer hasn't set it in Inspector, default to 1.0 to preserve original behavior.
        if (_buildingLookup.TryGetValue(BuildingType.StreetDeals, out var sd) && sd.tapBonusPerLevel == 0)
        {
            sd.tapBonusPerLevel = 1.0;
        }

        _lookupInitialized = true;
    }

    public BuildingDefinition GetBuilding(BuildingType type)
    {
        // Use dictionary lookup if available (O(1)), fallback to linear search (O(n))
        if (_lookupInitialized && _buildingLookup != null)
        {
            if (_buildingLookup.TryGetValue(type, out var building))
                return building;
        }
        else
        {
            // Fallback for calls before Awake
            foreach (var b in buildings)
            {
                if (b.type == type)
                    return b;
            }
        }

        Debug.LogError("[BuildingManager] Building not found for type: " + type);
        return null;
    }

    public double GetCurrentCost(BuildingType type)
    {
        BuildingDefinition b = GetBuilding(type);
        if (b == null) return 0;

        // cost = baseCost * costMultiplier^count
        double cost = b.baseCost * Math.Pow(b.costMultiplier, b.count);
        return cost;
    }

    public bool TryBuyBuilding(BuildingType type)
    {
        if (CurrencyManager.Instance == null) return false;

        BuildingDefinition b = GetBuilding(type);
        if (b == null) return false;

        // Capture state BEFORE purchase for validation
        int countBefore = b.count;
        double moneyBefore = CurrencyManager.Instance.money;
        double mpsBefore = CurrencyManager.Instance.moneyPerSecond;
        double mptBefore = CurrencyManager.Instance.moneyPerTap;

        // Max level check
        if (b.maxCount > 0 && b.count >= b.maxCount)
        {
            Debug.Log("[BuildingManager] Max level reached for: " + b.displayName);
            return false;
        }

        double cost = GetCurrentCost(type);
        bool canAfford = moneyBefore >= cost;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (enableEconomyDebugLogs)
        {
            Debug.Log($"[BUY_ATTEMPT] type={type} | countBefore={countBefore} | cost={cost:F2} | moneyBefore={moneyBefore:F2} | canAfford={canAfford}");
        }
#endif

        // Can afford?
        if (!CurrencyManager.Instance.TrySpendMoney(cost))
        {
            return false;
        }

        // Purchase successful -> level +1
        b.count++;

        // Notify CardManager for purchase-based activations
        if (CardManager.Instance != null)
        {
            CardManager.Instance.NotifyPurchase();
        }

        // MPS increase per level (general rule)
        double mpsIncrease = b.baseProduction;
        CurrencyManager.Instance.IncreaseMPS(mpsIncrease);

        // Data-driven tap income bonus (e.g., StreetDeals gives +tapBonusPerLevel per level)
        double tapIncrease = 0;
        if (b.tapBonusPerLevel > 0)
        {
            tapIncrease = b.tapBonusPerLevel;
            CurrencyManager.Instance.IncreaseTapIncome(tapIncrease);
        }

        // Capture state AFTER purchase for validation
        double moneyAfter = CurrencyManager.Instance.money;
        double mpsAfter = CurrencyManager.Instance.moneyPerSecond;
        double mptAfter = CurrencyManager.Instance.moneyPerTap;
        double moneySpent = moneyBefore - moneyAfter;
        double mpsActualDelta = mpsAfter - mpsBefore;
        double mptActualDelta = mptAfter - mptBefore;

        // Calculate NEXT cost (for the newly incremented count)
        double nextCost = GetCurrentCost(type);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (enableEconomyDebugLogs)
        {
            Debug.Log($"[BUY_RESULT] success=true | moneyAfter={moneyAfter:F2} | countAfter={b.count} | " +
                      $"mpsBefore={mpsBefore:F2} | mpsAfter={mpsAfter:F2} | mpsExpectedDelta={mpsIncrease:F2} | mpsActualDelta={mpsActualDelta:F2} | " +
                      $"mptExpectedDelta={tapIncrease:F2} | mptActualDelta={mptActualDelta:F2} | moneySpent={moneySpent:F2}");
            Debug.Log($"[NEXT_COST] type={type} | countNow={b.count} | nextCost={nextCost:F2}");

            // Validation checks
            double costTolerance = 0.01;
            double mpsTolerance = 0.01;
            if (Math.Abs(moneySpent - cost) > costTolerance)
            {
                Debug.LogWarning($"[ECONOMY_BUG] Money spent mismatch! Expected={cost:F2}, Actual={moneySpent:F2}, Diff={Math.Abs(moneySpent - cost):F6}");
            }
            if (Math.Abs(mpsActualDelta - mpsIncrease) > mpsTolerance)
            {
                Debug.LogWarning($"[ECONOMY_BUG] MPS delta mismatch! Expected={mpsIncrease:F2}, Actual={mpsActualDelta:F2}, Diff={Math.Abs(mpsActualDelta - mpsIncrease):F6}");
            }
            if (tapIncrease > 0 && Math.Abs(mptActualDelta - tapIncrease) > mpsTolerance)
            {
                Debug.LogWarning($"[ECONOMY_BUG] MPT delta mismatch! Expected={tapIncrease:F2}, Actual={mptActualDelta:F2}, Diff={Math.Abs(mptActualDelta - tapIncrease):F6}");
            }
        }
#endif

        // Fire event for UI updates
        OnBuildingPurchased?.Invoke(type, b.count);

        return true;
    }

    // ---- SaveSystem: set building count ----
    public void SetBuildingCount(BuildingType type, int newCount)
    {
        BuildingDefinition b = GetBuilding(type);
        if (b == null) return;

        int hardMax = b.maxCount > 0 ? b.maxCount : 999;
        newCount = Mathf.Clamp(newCount, 0, hardMax);

        // Sanity clamp: flag suspiciously high counts that could blow up MPS
        int sanityMax = b.baseCost >= LATE_BUILDING_COST_THRESHOLD
                        ? LATE_BUILDING_MAX_SANITY
                        : EARLY_BUILDING_MAX_SANITY;
        if (newCount > sanityMax)
        {
            Debug.LogWarning($"[BuildingManager] SANITY CLAMP: '{b.displayName}' (type={b.type}) count {newCount} exceeds sanity max {sanityMax}. " +
                             $"baseCost={b.baseCost:G4}, baseProduction={b.baseProduction:G4}. Clamping to {sanityMax}.");
            newCount = sanityMax;
        }

        b.count = newCount;

        // NOTE: MPS is NOT recalculated here to avoid double-counting.
        // Call RecalculateMPSFromBuildings() once after all counts are loaded.
    }

    /// <summary>
    /// Recalculates total MPS contribution from all buildings.
    /// Should be called ONCE after loading all building counts.
    /// Returns the calculated MPS value.
    /// </summary>
    public double CalculateTotalMPSFromBuildings()
    {
        double totalMPS = 0;
        if (buildings == null) return totalMPS;

        foreach (var b in buildings)
        {
            if (b != null && b.count > 0)
            {
                totalMPS += b.baseProduction * b.count;
            }
        }
        return totalMPS;
    }

    /// <summary>
    /// Recalculates MPS and tap income from building counts and updates CurrencyManager.
    /// Call this after loading save data to ensure economy consistency.
    /// Applies data-driven tapBonusPerLevel for all buildings that define it.
    /// Includes detailed per-building diagnostic breakdown.
    /// </summary>
    public void RecalculateMPSFromBuildings()
    {
        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning("[BuildingManager] Cannot recalculate MPS: CurrencyManager is null");
            return;
        }

        double totalMPS = 0;
        double tapBonus = 0;
        string topBuildingName = "(none)";
        double topBuildingMPS = 0;
        int totalBuildingCount = 0;

        // ---- Diagnostic breakdown ----
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[BuildingManager] === MPS RECALC BREAKDOWN ===");

        if (buildings != null)
        {
            foreach (var b in buildings)
            {
                if (b == null) continue;

                double contribution = b.baseProduction * b.count;
                totalMPS += contribution;
                totalBuildingCount += b.count;

                if (b.count > 0 && b.tapBonusPerLevel > 0)
                {
                    tapBonus += b.count * b.tapBonusPerLevel;
                }

                // Track biggest contributor
                if (contribution > topBuildingMPS)
                {
                    topBuildingMPS = contribution;
                    topBuildingName = b.displayName;
                }

                // Log every building with count > 0
                if (b.count > 0)
                {
                    sb.AppendLine($"  [{(int)b.type}] {b.displayName}: count={b.count}, baseProd={b.baseProduction:G6}, contrib={contribution:G6}");
                }
            }
        }

        sb.AppendLine($"  TOTAL: buildings={totalBuildingCount}, MPS={totalMPS:G10}, tapBonus={tapBonus:G6}");
        sb.AppendLine($"  TOP CONTRIBUTOR: {topBuildingName} → {topBuildingMPS:G10} MPS");
        sb.Append("[BuildingManager] === END BREAKDOWN ===");
        Debug.Log(sb.ToString());

        // Sanity check: if total MPS looks astronomically wrong for the total building count,
        // log a big red warning (but still apply — the per-building clamp in SetBuildingCount
        // should have already prevented truly absurd values).
        if (totalBuildingCount > 0 && totalMPS / totalBuildingCount > 1e10)
        {
            Debug.LogWarning($"[BuildingManager] WARNING: Average MPS per building = {totalMPS / totalBuildingCount:G6}. " +
                             $"Top contributor: {topBuildingName} ({topBuildingMPS:G6}). If this looks wrong, check building definitions and save data.");
        }

        CurrencyManager.Instance.SetBuildingMPS(totalMPS);
        CurrencyManager.Instance.SetBuildingTapIncome(tapBonus);

        Debug.Log($"[BuildingManager] Recalculated from buildings: MPS={totalMPS:G10}, TapBonus={tapBonus:G6}, TotalCount={totalBuildingCount}");
    }

    /// <summary>
    /// Gets the total count of all buildings owned.
    /// </summary>
    public int GetTotalBuildingCount()
    {
        int total = 0;
        if (buildings == null) return total;

        foreach (var b in buildings)
        {
            if (b != null)
                total += b.count;
        }
        return total;
    }

    /// <summary>
    /// Returns true if the player owns at least one StreetDeals building.
    /// (StreetDeals is the successor of the old AutoClicker building.)
    /// </summary>
    public bool HasStreetDeals()
    {
        BuildingDefinition sd = GetBuilding(BuildingType.StreetDeals);
        return sd != null && sd.count > 0;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
