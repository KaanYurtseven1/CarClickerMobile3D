using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Drives a single building row inside Content_ShopItems.
/// Auto-resolves child TMP references by container-name prefix:
///   Content_Name / Text  → building display name
///   Owned        / Text  → "OWN: {n}"
///   Cost         / Text  → "COST: 1,234,567" or "COST: LOCKED"
///   MPS          / Text  → "MPS: {total} (+{gain})"  (optional; if absent the line is skipped)
///
/// Progression lock: a building cannot be bought (and shows "COST: LOCKED")
/// unless the previous building type (by enum ID) has at least 1 owned.
///
/// Refreshes are event-driven (OnBuildingPurchased, SaveSystem.OnGameLoaded)
/// plus a lightweight 2 Hz dirty-check for cost/affordability changes.
/// </summary>
public class BuildingButton : MonoBehaviour
{
    [Header("Setup")]
    public BuildingType buildingType;

    [Tooltip("(Legacy) If assigned, the old single-label mode is used. " +
             "Leave null to use auto-resolved per-child TMP texts.")]
    public TextMeshProUGUI labelText;

    public Button button;

    [Header("Debug")]
    [Tooltip("Enable per-row economy debug logs (cost verification, MPS delta).")]
    public bool debugEconomyLogs = false;

    // ── Auto-resolved TMP references (found once on init) ──
    private TextMeshProUGUI _tmpName;
    private TextMeshProUGUI _tmpOwned;
    private TextMeshProUGUI _tmpCost;
    private TextMeshProUGUI _tmpMPS;    // may be null if no "MPS" child exists

    // ── Dirty-check state ──
    private double _lastCost = -1;
    private int    _lastOwned = -1;
    private double _lastMoney = -1;
    private bool   _lastLocked = false;

    // ── Lightweight timer (avoids per-frame GetCurrentCost math) ──
    private const float REFRESH_INTERVAL = 0.5f;   // 2 Hz
    private float _refreshTimer;

    private bool _initialized;
    private bool _diagnosticLogged;
    private bool _boundSuccessfully;
    private bool _subscribedToEvents;

    // ═══════════════════════ lifecycle ═══════════════════════

    private void OnEnable()
    {
        SubscribeEvents();
        if (_initialized) ForceRefresh();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Start()
    {
        // ── resolve Button reference ──
        if (button == null) button = GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError($"[BuildingButton] '{gameObject.name}' (type={buildingType}) — " +
                           "no Button component found. Disabling script.", this);
            enabled = false;
            return;
        }

        button.onClick.AddListener(OnClickBuy);

        // ── Auto-resolve TMP children ──
        ResolveChildTMPs();

        _initialized = true;
        SubscribeEvents();
        ForceRefresh();
    }

    private void Update()
    {
        if (!_initialized) return;

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = REFRESH_INTERVAL;
            TryRefreshData(false);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    // ═══════════════════════ event wiring ═══════════════════════

    private void SubscribeEvents()
    {
        if (_subscribedToEvents) return;

        if (BuildingManager.Instance != null)
            BuildingManager.Instance.OnBuildingPurchased += OnAnyBuildingPurchased;

        SaveSystem.OnGameLoaded += OnGameLoaded;
        _subscribedToEvents = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_subscribedToEvents) return;

        if (BuildingManager.Instance != null)
            BuildingManager.Instance.OnBuildingPurchased -= OnAnyBuildingPurchased;

        SaveSystem.OnGameLoaded -= OnGameLoaded;
        _subscribedToEvents = false;
    }

    private void OnAnyBuildingPurchased(BuildingType type, int newCount)
    {
        // Refresh this row regardless of which building was bought
        // (currency changed → affordability / cost display may differ;
        //  buying the previous building may unlock this one)
        ForceRefresh();
    }

    private void OnGameLoaded()
    {
        // Save just loaded → counts are fresh, rebuild display
        ForceRefresh();
    }

    // ═══════════════════════ child TMP resolution ═══════════════════════

    /// <summary>
    /// Walks immediate children looking for containers whose names start with
    /// "Content_Name", "Owned", "Cost", or "MPS". Grabs the first
    /// TextMeshProUGUI found in that child (typically a grandchild named "Text").
    /// </summary>
    private void ResolveChildTMPs()
    {
        foreach (Transform child in transform)
        {
            string n = child.name;

            TextMeshProUGUI tmp = child.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp == null) continue;

            // Match by prefix — handles "Owned (3)", "Cost (12)", "Content_Name (7)", etc.
            if (n.StartsWith("Content_Name"))   { _tmpName  = tmp; continue; }
            if (n.StartsWith("Owned"))          { _tmpOwned = tmp; continue; }
            if (n.StartsWith("Cost"))           { _tmpCost  = tmp; continue; }
            if (n.StartsWith("MPS"))            { _tmpMPS   = tmp; continue; }
        }
    }

    // ═══════════════════════ refresh logic ═══════════════════════

    private void ForceRefresh() => TryRefreshData(true);

    private void TryRefreshData(bool forceLabel)
    {
        // ── 1. Singletons ready? ──
        if (BuildingManager.Instance == null || CurrencyManager.Instance == null)
        {
            if (!_diagnosticLogged && forceLabel)
            {
                _diagnosticLogged = true;
                Debug.Log($"[BuildingButton] '{gameObject.name}' (type={buildingType}) — " +
                          $"waiting for managers.", this);
            }
            return;
        }

        // ── 2. Building definition? ──
        BuildingDefinition b = BuildingManager.Instance.GetBuilding(buildingType);
        if (b == null)
        {
            if (!_diagnosticLogged)
            {
                _diagnosticLogged = true;
                Debug.LogWarning($"[BuildingButton] '{gameObject.name}' — " +
                                 $"GetBuilding({buildingType}) returned NULL.", this);
            }
            return;
        }

        // ── 3. First-bind log ──
        if (!_boundSuccessfully)
        {
            _boundSuccessfully = true;
            _diagnosticLogged = true;
            Debug.Log($"[BuildingButton] '{gameObject.name}' bound OK — type={buildingType}, " +
                      $"name='{b.displayName}', tmpName={_tmpName != null}, tmpOwned={_tmpOwned != null}, " +
                      $"tmpCost={_tmpCost != null}, tmpMPS={_tmpMPS != null}", this);
        }

        // ── 4. Read current values ──
        double cost   = BuildingManager.Instance.GetCurrentCost(buildingType);
        double money  = CurrencyManager.Instance.money;
        int owned     = b.count;
        bool isLocked = BuildingManager.Instance.IsBuildingLocked(buildingType);

        // ── 5. Button interactable ──
        bool canAfford = money >= cost;
        bool atMax     = b.maxCount > 0 && owned >= b.maxCount;
        button.interactable = !isLocked && canAfford && !atMax;

        // ── 6. Dirty check — skip label work if nothing changed ──
        bool dirty = forceLabel
                     || owned != _lastOwned
                     || isLocked != _lastLocked
                     || System.Math.Abs(cost - _lastCost) > 0.5
                     || System.Math.Abs(money - _lastMoney) > 0.5;

        if (!dirty) return;

        _lastCost   = cost;
        _lastOwned  = owned;
        _lastMoney  = money;
        _lastLocked = isLocked;

        // ── 7. Update labels ──
        UpdateLabels(b, cost, owned, atMax, isLocked);
    }

    // ═══════════════════════ buy ═══════════════════════

    private void OnClickBuy()
    {
        if (BuildingManager.Instance == null) return;

        // ── Debug: capture state BEFORE purchase ──
        double moneyBefore = 0;
        double mpsBefore   = 0;
        double costForLog  = 0;
        if (debugEconomyLogs && CurrencyManager.Instance != null)
        {
            moneyBefore = CurrencyManager.Instance.money;
            mpsBefore   = CurrencyManager.Instance.moneyPerSecond;
            costForLog  = BuildingManager.Instance.GetCurrentCost(buildingType);
            Debug.Log($"[BuildingButton:DEBUG] PRE-BUY type={buildingType} | " +
                      $"displayedCost={FormatCostFull(costForLog)} | rawCost={costForLog:F2} | " +
                      $"money={moneyBefore:F2} | MPS={mpsBefore:F2}");
        }

        if (BuildingManager.Instance.TryBuyBuilding(buildingType))
        {
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlayBuildingBuy();

            // ── Debug: verify state AFTER purchase ──
            if (debugEconomyLogs && CurrencyManager.Instance != null)
            {
                double moneyAfter = CurrencyManager.Instance.money;
                double mpsAfter   = CurrencyManager.Instance.moneyPerSecond;
                double spent      = moneyBefore - moneyAfter;
                double mpsDelta   = mpsAfter - mpsBefore;

                BuildingDefinition b = BuildingManager.Instance.GetBuilding(buildingType);
                double expectedMpsDelta = b != null ? b.baseProduction : 0;

                Debug.Log($"[BuildingButton:DEBUG] POST-BUY type={buildingType} | " +
                          $"moneyAfter={moneyAfter:F2} | moneySpent={spent:F2} (expected={costForLog:F2}) | " +
                          $"mpsAfter={mpsAfter:F2} | mpsDelta={mpsDelta:F2} (expected={expectedMpsDelta:F2})");

                // Sanity assertions
                if (System.Math.Abs(spent - costForLog) > 0.02)
                    Debug.LogWarning($"[BuildingButton:DEBUG] MISMATCH — money deducted ({spent:F2}) != displayed cost ({costForLog:F2})!");
                if (System.Math.Abs(mpsDelta - expectedMpsDelta) > 0.02)
                    Debug.LogWarning($"[BuildingButton:DEBUG] MISMATCH — MPS delta ({mpsDelta:F2}) != baseProduction ({expectedMpsDelta:F2})!");
            }

            // Immediate refresh (event also fires, but this is instant for the buyer)
            ForceRefresh();
        }
    }

    // ═══════════════════════ label rendering ═══════════════════════

    private void UpdateLabels(BuildingDefinition b, double cost, int owned, bool atMax, bool isLocked)
    {
        // ── Compute MPS display values (all double → long, NO float/int casts) ──
        //   totalMpsRaw = baseProduction * ownedCount   (current total from this building)
        //   gainMpsRaw  = baseProduction                (what 1 more purchase adds)
        // This is DISPLAY ONLY — underlying economy math is unchanged.
        double totalMpsRaw = b.baseProduction * (double)owned;
        double gainMpsRaw  = b.baseProduction;

        long totalMps = SafeCeilToLong(totalMpsRaw);
        long gainMps  = SafeCeilToLong(gainMpsRaw);

        // Temporary debug log so we can verify large-value buildings in the console
        Debug.Log($"[MPS DEBUG] {b.displayName} baseProduction={gainMpsRaw} gain={gainMps}");

        // ── Per-child TMP mode (preferred) ──
        bool hasChildTMPs = (_tmpOwned != null || _tmpCost != null);

        if (hasChildTMPs)
        {
            // Name
            if (_tmpName != null)
                _tmpName.text = b.displayName;

            // Owned
            if (_tmpOwned != null)
            {
                if (atMax)
                    _tmpOwned.text = $"OWN: {owned} (MAX)";
                else
                    _tmpOwned.text = $"OWN: {owned}";
            }

            // MPS line: "MPS: {total} (+{gain})" — formatted with thousands separators
            // No MPT shown. Uses "+" not "x".
            if (_tmpMPS != null)
            {
                string gainStr  = gainMps.ToString("N0");
                string totalStr = totalMps.ToString("N0");
                if (owned > 0)
                    _tmpMPS.text = $"MPS: {totalStr} (+{gainStr})";
                else
                    _tmpMPS.text = $"MPS: (+{gainStr})";
            }

            // Cost
            if (_tmpCost != null)
            {
                if (isLocked)
                    _tmpCost.text = "COST: LOCKED";
                else if (atMax)
                    _tmpCost.text = "MAX";
                else
                    _tmpCost.text = $"COST: {FormatCostFull(cost)}";
            }
        }

        // ── Legacy single-label fallback (labelText assigned in Inspector) ──
        if (labelText != null)
        {
            string gainStr  = gainMps.ToString("N0");
            string totalStr = totalMps.ToString("N0");
            string mpsLine = owned > 0
                ? $"MPS: {totalStr} (+{gainStr})"
                : $"MPS: (+{gainStr})";

            if (isLocked)
            {
                labelText.text = $"{b.displayName}\nOWN: {owned}\n{mpsLine}\nCOST: LOCKED";
            }
            else if (atMax)
            {
                labelText.text = $"{b.displayName}\nOWN: {owned} (MAX)\n{mpsLine}\nMAX";
            }
            else
            {
                labelText.text = $"{b.displayName}\nOWN: {owned}\n{mpsLine}\nCOST: {FormatCostFull(cost)}";
            }
        }
    }

    // ═══════════════════════ helpers ═══════════════════════

    /// <summary>
    /// Ceil double → long with NO float or int intermediaries.
    /// Zero or negative → 0.  Positive but < 1 → 1.
    /// Safe for values up to ~9.2e18 (long.MaxValue).
    /// </summary>
    private static long SafeCeilToLong(double raw)
    {
        if (raw <= 0) return 0;
        if (raw < 1)  return 1;
        return (long)System.Math.Ceiling(raw);
    }

    /// <summary>
    /// Formats cost as a full integer with thousands separators.
    /// No abbreviations (no K/M/B/T suffixes).
    /// Example: 2400000000 → "2,400,000,000"
    /// Uses Math.Ceiling so the player always sees a cost >= the actual deduction.
    /// </summary>
    private static string FormatCostFull(double value)
    {
        // Ceil to ensure displayed cost is never less than actual deduction
        double ceiled = System.Math.Ceiling(value);

        // For extremely large values (> long.MaxValue ≈ 9.2e18), fall back to
        // decimal which handles up to ±7.9e28.
        if (ceiled > 9_000_000_000_000_000_000d || ceiled < -9_000_000_000_000_000_000d)
        {
            try
            {
                decimal d = (decimal)ceiled;
                return d.ToString("N0");  // "N0" = number with thousand separators, 0 decimal places
            }
            catch
            {
                // Absolute fallback for values beyond decimal range
                return ceiled.ToString("F0");
            }
        }

        long rounded = (long)ceiled;
        return rounded.ToString("N0");
    }
}
