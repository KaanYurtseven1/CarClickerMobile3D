using UnityEngine;
using System;

/// <summary>
/// GarageManagerController manages the Garage Manager card effect.
/// 
/// MECHANIC (Spend Bar / Garage Charge):
/// 1. Track total money spent via OnMoneySpent(amount)
/// 2. When spentSinceLastTrigger >= requiredSpend (based on current MPS), trigger activates
/// 3. On activation: snapshot current MPS, compute bonusMps = snapshotMps * bonusMultiplier
/// 4. bonusMps is added to player's MPS for 60 seconds
/// 5. After 60s, bonusMps removed, enter 120s cooldown
/// 6. After cooldown, ready to trigger again
/// 
/// RequiredSpend = currentMps * spendSecondsEquivalent (scales with progression)
/// 
/// Level scaling (clamp >6 to L6):
/// Level 1: multiplier=10, spendSeconds=30
/// Level 2: multiplier=11, spendSeconds=28
/// Level 3: multiplier=12, spendSeconds=26
/// Level 4: multiplier=13, spendSeconds=24
/// Level 5: multiplier=14, spendSeconds=22
/// Level 6: multiplier=15, spendSeconds=20
/// 
/// State Machine: Ready -> Active -> Cooldown -> Ready
/// </summary>
public class GarageManagerController : MonoBehaviour
{
    public static GarageManagerController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    // ==================== CONFIGURATION ====================

    [Header("Duration Settings")]
    [Tooltip("How long the bonus MPS lasts (seconds)")]
    [SerializeField] private float activeDurationSeconds = 60f;

    [Tooltip("Cooldown after effect ends (seconds)")]
    [SerializeField] private float cooldownSeconds = 120f;

    [Header("Debug / Testing")]
    [Tooltip("Use level-based scaling (disable to use debugForceLevel)")]
    [SerializeField] private bool useLevelScaling = true;

    [Tooltip("Force a specific level for testing (0 = use real card level)")]
    [SerializeField] private int debugForceLevel = 0;

    [Tooltip("Enable verbose logging for spend progress")]
    [SerializeField] private bool verboseLogs = true;

    // Level-based configuration (index = level)
    // [0] = level 0 (locked/unused), [1] = level 1, etc.
    private static readonly float[] BonusMultipliers = { 0f, 15f, 17f, 19f, 21f, 23f, 25f };
    private static readonly float[] SpendSecondsEquivalents = { 0f, 25f, 22f, 19f, 16f, 14f, 12f };

    // ==================== STATE ====================

    public enum GarageManagerState
    {
        Ready,      // Accumulating spend toward threshold
        Active,     // Bonus MPS is active
        Cooldown    // Waiting for cooldown to end
    }

    private GarageManagerState _currentState = GarageManagerState.Ready;
    public GarageManagerState CurrentState => _currentState;

    // Spend tracking
    private double _spentSinceLastTrigger = 0.0;

    // Active state
    private double _snapshotMps = 0.0;
    private double _currentBonusMps = 0.0;
    private float _activeEndTime = 0f;

    // Cooldown state
    private float _cooldownEndTime = 0f;

    // Card locked warning (only log once)
    private bool _loggedLockedWarning = false;

    // ==================== PUBLIC PROPERTIES ====================

    /// <summary>
    /// Returns true if the bonus MPS is currently active.
    /// </summary>
    public bool IsActive => _currentState == GarageManagerState.Active;

    /// <summary>
    /// Returns true if currently on cooldown.
    /// </summary>
    public bool IsOnCooldown => _currentState == GarageManagerState.Cooldown;

    /// <summary>
    /// Returns true if ready to accumulate spend.
    /// </summary>
    public bool IsReady => _currentState == GarageManagerState.Ready;

    /// <summary>
    /// The current bonus MPS being added (0 when inactive).
    /// This is a SNAPSHOT value that does not change during the active period.
    /// </summary>
    public double CurrentBonusMps => IsActive ? _currentBonusMps : 0.0;

    /// <summary>
    /// Progress toward next activation (0.0 to 1.0).
    /// </summary>
    public float Progress01
    {
        get
        {
            double required = GetRequiredSpend();
            if (required <= 0.0) return 0f;
            return Mathf.Clamp01((float)(_spentSinceLastTrigger / required));
        }
    }

    /// <summary>
    /// Remaining seconds of active bonus (0 if not active).
    /// </summary>
    public float RemainingActiveTime => IsActive ? Mathf.Max(0f, _activeEndTime - Time.time) : 0f;

    /// <summary>
    /// Remaining seconds of cooldown (0 if not on cooldown).
    /// </summary>
    public float RemainingCooldownTime => IsOnCooldown ? Mathf.Max(0f, _cooldownEndTime - Time.time) : 0f;

    /// <summary>
    /// Current card level (0 if locked).
    /// </summary>
    public int CurrentCardLevel => GetEffectiveLevel();

    /// <summary>
    /// Current bonus multiplier based on level.
    /// </summary>
    public float CurrentBonusMultiplier => GetBonusMultiplierForLevel(GetEffectiveLevel());

    /// <summary>
    /// Current spend seconds equivalent based on level.
    /// </summary>
    public float CurrentSpendSecondsEquivalent => GetSpendSecondsForLevel(GetEffectiveLevel());

    /// <summary>
    /// Current required spend amount (currentMps * spendSecondsEquivalent).
    /// </summary>
    public double RequiredSpend => GetRequiredSpend();

    /// <summary>
    /// Current accumulated spend toward threshold.
    /// </summary>
    public double SpentSinceLastTrigger => _spentSinceLastTrigger;

    // ==================== EVENTS ====================

    /// <summary>
    /// Fired when GarageManager activates.
    /// Parameters: bonusMps, duration, level
    /// </summary>
    public event Action<double, float, int> OnActivated;

    /// <summary>
    /// Fired when active period ends and cooldown starts.
    /// </summary>
    public event Action OnEnded;

    /// <summary>
    /// Fired when cooldown ends and ready to trigger again.
    /// </summary>
    public event Action OnCooldownEnded;

    // ==================== UNITY LIFECYCLE ====================

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        switch (_currentState)
        {
            case GarageManagerState.Active:
                UpdateActiveState();
                break;

            case GarageManagerState.Cooldown:
                UpdateCooldownState();
                break;

            case GarageManagerState.Ready:
                // Nothing to update in Ready state (waiting for OnMoneySpent)
                break;
        }
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Called when the player spends money. Accumulates spend toward threshold.
    /// Call this from the authoritative spend point (e.g., CurrencyManager.TrySpendMoney).
    /// </summary>
    public void OnMoneySpent(double amount)
    {
        if (amount <= 0.0) return;

        int level = GetEffectiveLevel();

        // If card is locked, do nothing (log once)
        if (level <= 0)
        {
            if (!_loggedLockedWarning)
            {
                Debug.Log("[GarageManager] Card is locked (level 0). Spend tracking disabled.");
                _loggedLockedWarning = true;
            }
            return;
        }

        // Reset warning flag when card becomes unlocked
        _loggedLockedWarning = false;

        // Accumulate spend
        _spentSinceLastTrigger += amount;

        // Get required spend (clamp accumulator during Active/Cooldown)
        double required = GetRequiredSpend();

        if (_currentState != GarageManagerState.Ready)
        {
            // During Active or Cooldown, clamp to prevent instant retrigger
            if (_spentSinceLastTrigger > required)
            {
                _spentSinceLastTrigger = required;
            }

            if (verboseLogs)
            {
                float progress = required > 0 ? (float)(_spentSinceLastTrigger / required) * 100f : 0f;
                Debug.Log($"[GarageManager] Spend progress (clamped): spent={_spentSinceLastTrigger:F0} / required={required:F0} ({progress:F1}%) [State: {_currentState}]");
            }
            return;
        }

        // Log progress
        if (verboseLogs)
        {
            float progress = required > 0 ? (float)(_spentSinceLastTrigger / required) * 100f : 0f;
            Debug.Log($"[GarageManager] Spend progress: spent={_spentSinceLastTrigger:F0} / required={required:F0} ({progress:F1}%)");
        }

        // Check if threshold reached
        if (_spentSinceLastTrigger >= required && required > 0)
        {
            Activate(level);
        }
    }

    /// <summary>
    /// Force activation (for testing/debugging).
    /// </summary>
    public void ForceActivate()
    {
        int level = GetEffectiveLevel();
        if (level <= 0)
        {
            Debug.LogWarning("[GarageManager] Cannot force activate: card is locked.");
            return;
        }
        Activate(level);
    }

    /// <summary>
    /// Force end active state (for testing/debugging).
    /// </summary>
    public void ForceEndActive()
    {
        if (_currentState == GarageManagerState.Active)
        {
            EndActive();
        }
    }

    // ==================== STATE TRANSITIONS ====================

    private void Activate(int level)
    {
        // Snapshot current MPS
        _snapshotMps = GetCurrentMps();

        // Compute bonus
        float multiplier = GetBonusMultiplierForLevel(level);
        _currentBonusMps = _snapshotMps * multiplier;

        // Reset spend accumulator
        _spentSinceLastTrigger = 0.0;

        // Transition to Active
        _currentState = GarageManagerState.Active;
        _activeEndTime = Time.time + activeDurationSeconds;

        Debug.Log($"[GarageManager] ACTIVATED L{level}: snapshotMps={_snapshotMps:F2}, multiplier={multiplier}, bonusMps={_currentBonusMps:F2}, duration={activeDurationSeconds}s");

        // F3: Garage Manager card activate SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayGarageManagerActivate();

        OnActivated?.Invoke(_currentBonusMps, activeDurationSeconds, level);
    }

    private void EndActive()
    {
        _currentBonusMps = 0.0;
        _snapshotMps = 0.0;

        // Transition to Cooldown
        _currentState = GarageManagerState.Cooldown;
        _cooldownEndTime = Time.time + cooldownSeconds;

        Debug.Log($"[GarageManager] ENDED. Cooldown started: {cooldownSeconds}s");

        OnEnded?.Invoke();
    }

    private void EndCooldown()
    {
        _currentState = GarageManagerState.Ready;

        Debug.Log("[GarageManager] Cooldown ended. Ready.");

        OnCooldownEnded?.Invoke();
    }

    // ==================== UPDATE HELPERS ====================

    private void UpdateActiveState()
    {
        if (Time.time >= _activeEndTime)
        {
            EndActive();
        }
    }

    private void UpdateCooldownState()
    {
        if (Time.time >= _cooldownEndTime)
        {
            EndCooldown();
        }
    }

    // ==================== LEVEL HELPERS ====================

    private int GetEffectiveLevel()
    {
        if (!useLevelScaling && debugForceLevel > 0)
        {
            return Mathf.Clamp(debugForceLevel, 1, 6);
        }

        if (CardManager.Instance == null) return 0;

        CardDefinition card = CardManager.Instance.GetCard(CardType.GarageManager);
        if (card == null) return 0;

        return Mathf.Clamp(card.currentLevel, 0, 6);
    }

    private float GetBonusMultiplierForLevel(int level)
    {
        if (level <= 0) return 0f;
        int index = Mathf.Clamp(level, 0, BonusMultipliers.Length - 1);
        return BonusMultipliers[index];
    }

    private float GetSpendSecondsForLevel(int level)
    {
        if (level <= 0) return float.MaxValue;
        int index = Mathf.Clamp(level, 0, SpendSecondsEquivalents.Length - 1);
        return SpendSecondsEquivalents[index];
    }

    // ==================== MPS HELPERS ====================

    private double GetCurrentMps()
    {
        if (CurrencyManager.Instance == null) return 0.0;

        // Get base MPS from CurrencyManager
        double baseMps = CurrencyManager.Instance.moneyPerSecond;

        // We don't include the GarageManager bonus in snapshot to avoid recursion
        // Just use the raw moneyPerSecond value
        return baseMps;
    }

    private double GetRequiredSpend()
    {
        int level = GetEffectiveLevel();
        if (level <= 0) return double.MaxValue;

        double currentMps = GetCurrentMps();
        if (currentMps <= 0.0)
        {
            // Avoid division by zero / infinite progress
            // Return a large number so progress is 0
            return double.MaxValue;
        }

        float spendSeconds = GetSpendSecondsForLevel(level);
        return currentMps * spendSeconds;
    }

    // ==================== SAVE / LOAD ====================

    private const string SaveKey_GM_State = "Save_GarageManager_State";
    private const string SaveKey_GM_Spent = "Save_GarageManager_Spent";
    private const string SaveKey_GM_EndUTC = "Save_GarageManager_EndUTC";
    private const string SaveKey_GM_BonusMps = "Save_GarageManager_BonusMps";

    /// <summary>Saves GarageManager state to PlayerPrefs. Called by SaveSystem.</summary>
    public void SaveState()
    {
        PlayerPrefs.SetInt(SaveKey_GM_State, (int)_currentState);
        PlayerPrefs.SetString(SaveKey_GM_Spent, _spentSinceLastTrigger.ToString(System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(SaveKey_GM_BonusMps, _currentBonusMps.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Save end time as UTC
        float endTime = _currentState == GarageManagerState.Active ? _activeEndTime :
                         _currentState == GarageManagerState.Cooldown ? _cooldownEndTime : 0f;
        if (endTime > Time.time)
        {
            float remaining = endTime - Time.time;
            long utcEnd = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)System.Math.Ceiling(remaining);
            PlayerPrefs.SetString(SaveKey_GM_EndUTC, utcEnd.ToString());
        }
        else
        {
            PlayerPrefs.SetString(SaveKey_GM_EndUTC, "0");
        }
    }

    /// <summary>Loads GarageManager state from PlayerPrefs. Called by SaveSystem.</summary>
    public void LoadState()
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        string spentStr = PlayerPrefs.GetString(SaveKey_GM_Spent, "0");
        double.TryParse(spentStr, System.Globalization.NumberStyles.Any, culture, out double spent);
        _spentSinceLastTrigger = spent;

        string bonusStr = PlayerPrefs.GetString(SaveKey_GM_BonusMps, "0");
        double.TryParse(bonusStr, System.Globalization.NumberStyles.Any, culture, out double bonus);

        int savedState = PlayerPrefs.GetInt(SaveKey_GM_State, 0);
        string endStr = PlayerPrefs.GetString(SaveKey_GM_EndUTC, "0");
        long.TryParse(endStr, out long utcEnd);
        long nowUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long remaining = utcEnd > 0 ? utcEnd - nowUtc : 0;

        if (savedState == (int)GarageManagerState.Active && remaining > 0)
        {
            _currentState = GarageManagerState.Active;
            _currentBonusMps = bonus;
            _activeEndTime = Time.time + remaining;
        }
        else if (savedState == (int)GarageManagerState.Active && remaining <= 0)
        {
            // Active expired offline — transition to cooldown
            _currentState = GarageManagerState.Cooldown;
            _currentBonusMps = 0;
            _cooldownEndTime = Time.time + cooldownSeconds;
        }
        else if (savedState == (int)GarageManagerState.Cooldown && remaining > 0)
        {
            _currentState = GarageManagerState.Cooldown;
            _currentBonusMps = 0;
            _cooldownEndTime = Time.time + remaining;
        }
        else
        {
            // Ready or expired cooldown
            _currentState = GarageManagerState.Ready;
            _currentBonusMps = 0;
        }

        Debug.Log($"[GarageManager] LoadState: state={_currentState} spent={_spentSinceLastTrigger:F0} bonusMps={_currentBonusMps:F2} remaining={remaining}s");
    }

    // ==================== DEBUG ====================

#if UNITY_EDITOR
    private void OnGUI()
    {
        // Uncomment for debug overlay
        /*
        GUILayout.BeginArea(new Rect(10, 280, 350, 150));
        GUILayout.Label($"GarageManager State: {_currentState}");
        GUILayout.Label($"Card Level: {CurrentCardLevel}");
        GUILayout.Label($"Progress: {_spentSinceLastTrigger:F0} / {RequiredSpend:F0} ({Progress01 * 100:F1}%)");
        GUILayout.Label($"Multiplier: x{CurrentBonusMultiplier}, SpendSecs: {CurrentSpendSecondsEquivalent}");
        if (IsActive)
        {
            GUILayout.Label($"Active - Bonus MPS: {CurrentBonusMps:F2}, Time Left: {RemainingActiveTime:F1}s");
        }
        if (IsOnCooldown)
        {
            GUILayout.Label($"Cooldown Left: {RemainingCooldownTime:F1}s");
        }
        GUILayout.EndArea();
        */
    }
#endif

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
