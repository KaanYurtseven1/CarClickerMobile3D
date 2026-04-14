using UnityEngine;
using System;
using System.Collections;
using System.Globalization;

/// <summary>
/// PitStopCrewController manages the PitStopCrew card effect: OFFLINE EARNINGS.
/// 
/// MECHANIC:
/// 1. On app exit/pause: Save lastQuitTimestamp and lastKnownMpsAtExit
/// 2. On next startup: Calculate offline earnings based on time away and exit MPS
/// 3. Earned amount = usedSeconds * exitMps * efficiency (level-based)
/// 4. Money increases with visible count-up animation
/// 
/// Level scaling (clamp >6 to L6):
/// Level 1: 20% efficiency, 2 hour cap
/// Level 2: 30% efficiency, 3 hour cap
/// Level 3: 40% efficiency, 4 hour cap
/// Level 4: 55% efficiency, 6 hour cap
/// Level 5: 70% efficiency, 8 hour cap
/// Level 6: 85% efficiency, 12 hour cap
/// </summary>
public class PitStopCrewController : MonoBehaviour
{
    public static PitStopCrewController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    // ==================== CONFIGURATION ====================

    [Header("Animation Settings")]
    [Tooltip("Duration of the count-up animation (seconds)")]
    [SerializeField] private float countUpDuration = 1.5f;

    [Tooltip("Minimum earned amount to trigger animation (skip tiny values)")]
    [SerializeField] private double minimumEarnedToAnimate = 1.0;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

#if UNITY_EDITOR
    [Header("DEBUG (Editor Only)")]
    [Tooltip("Enable debug simulation tools")]
    public bool debugEnabled = true;

    [Tooltip("Seconds to simulate being offline when using Debug/Simulate Return")]
    public int debugSimulateOfflineSeconds = 60;

    [Tooltip("If true, uses current live MPS as the exit snapshot; if false, uses stored PlayerPrefs value")]
    public bool debugUseCurrentMpsAsExitSnapshot = true;
#endif

    // PlayerPrefs keys
    private const string KEY_LAST_QUIT_TS = "PitStop_LastQuitTimestamp";
    private const string KEY_LAST_EXIT_MPS = "PitStop_LastExitMps";
    private const string KEY_OFFLINE_GRANTED_AT = "PitStop_OfflineGrantedAt";

    // Level-based configuration (index = level)
    // [0] = level 0 (locked/unused), [1] = level 1, etc.
    private static readonly float[] EfficiencyByLevel = { 0f, 0.25f, 0.30f, 0.40f, 0.55f, 0.70f, 0.85f };
    private static readonly float[] CapHoursByLevel = { 0f, 2f, 3f, 4f, 6f, 8f, 12f };

    // Culture for double parsing
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // State
    private bool _hasGrantedThisSession = false;
    private double _lastKnownMps = 0.0;

    // ==================== EVENTS ====================

    /// <summary>
    /// Fired when offline earnings are computed and about to be granted.
    /// Parameters: earned, offlineSeconds, usedSeconds, exitMps, level, efficiency
    /// </summary>
    public event Action<double, double, double, double, int, float> OnOfflineEarningsComputed;

    /// <summary>
    /// Fired when count-up animation completes.
    /// Parameters: earnedAmount
    /// </summary>
    public event Action<double> OnCountUpComplete;

    // Coroutine tracking to prevent multiple instances
    private Coroutine _grantCoroutine = null;

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

    private void Start()
    {
        // Delay offline earnings check until systems are ready
        _grantCoroutine = StartCoroutine(TryGrantOfflineEarningsOnStartup());
    }

    // Throttle MPS snapshot updates (no need for every frame)
    private float _mpsSnapshotTimer = 0f;
    private const float MpsSnapshotInterval = 0.5f; // Update twice per second

    private void Update()
    {
        // Throttle MPS snapshot updates (twice per second is sufficient)
        _mpsSnapshotTimer += Time.deltaTime;
        if (_mpsSnapshotTimer >= MpsSnapshotInterval)
        {
            _mpsSnapshotTimer = 0f;
            UpdateExitSnapshot(GetCurrentMps());
        }
    }

    private void OnApplicationQuit()
    {
        HandleAppPausedOrQuit();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            HandleAppPausedOrQuit();
        }
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Updates the MPS snapshot that will be saved on exit.
    /// Called automatically in Update, but can be called manually.
    /// </summary>
    public void UpdateExitSnapshot(double currentMps)
    {
        _lastKnownMps = currentMps;
    }

    /// <summary>
    /// Call this when app is about to quit or pause.
    /// Saves timestamp and MPS snapshot.
    /// </summary>
    public void HandleAppPausedOrQuit()
    {
        long now = GetUnixTimestamp();

        PlayerPrefs.SetString(KEY_LAST_QUIT_TS, now.ToString());
        PlayerPrefs.SetString(KEY_LAST_EXIT_MPS, _lastKnownMps.ToString(Culture));
        // Note: PlayerPrefs.Save() is called by Unity automatically on application quit/pause
        // We only call it here to ensure data is flushed immediately
        PlayerPrefs.Save();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
        {
            Debug.Log($"[PitStopCrew] Exit snapshot saved: timestamp={now}, mps={_lastKnownMps:F2}");
        }
#endif
    }

    /// <summary>
    /// Attempts to grant offline earnings on startup.
    /// Should be called once after save data is loaded.
    /// </summary>
    public IEnumerator TryGrantOfflineEarningsOnStartup()
    {
        // Wait for systems to be ready
        yield return null; // 1 frame
        yield return new WaitUntil(() => CurrencyManager.Instance != null);
        yield return new WaitUntil(() => CardManager.Instance != null);
        yield return null; // Extra frame for CardManager to load card data

        // Prevent double grant in same session
        if (_hasGrantedThisSession)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log("[PitStopCrew] Already granted this session, skipping.");
#endif
            _grantCoroutine = null;
            yield break;
        }

        // Check if we have saved data
        if (!PlayerPrefs.HasKey(KEY_LAST_QUIT_TS) || !PlayerPrefs.HasKey(KEY_LAST_EXIT_MPS))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log("[PitStopCrew] No exit data found, skipping offline earnings.");
#endif
            _hasGrantedThisSession = true;
            _grantCoroutine = null;
            yield break;
        }

        // Get PitStopCrew card level
        int level = GetPitStopCrewLevel();
        if (level <= 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log("[PitStopCrew] Card is locked (level 0), no offline earnings.");
#endif
            _hasGrantedThisSession = true;
            _grantCoroutine = null;
            yield break;
        }

        // Read saved data
        long lastQuitTs = GetLongFromPrefs(KEY_LAST_QUIT_TS, 0);
        double exitMps = GetDoubleFromPrefs(KEY_LAST_EXIT_MPS, 0.0);

        if (exitMps <= 0.0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log("[PitStopCrew] Exit MPS was 0 or invalid, no offline earnings.");
#endif
            _hasGrantedThisSession = true;
            _grantCoroutine = null;
            yield break;
        }

        // Calculate offline time
        long now = GetUnixTimestamp();
        double offlineSeconds = Math.Max(0, now - lastQuitTs);

        // Get level-based parameters
        float efficiency = GetEfficiencyForLevel(level);
        float capHours = GetCapHoursForLevel(level);
        double capSeconds = capHours * 3600.0;

        // Clamp to reasonable max (cap * 10) to handle clock manipulation
        double maxReasonableSeconds = capSeconds * 10.0;
        if (offlineSeconds > maxReasonableSeconds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log($"[PitStopCrew] Offline time {offlineSeconds}s exceeds reasonable max {maxReasonableSeconds}s, clamping.");
#endif
            offlineSeconds = maxReasonableSeconds;
        }

        // Apply cap
        double usedSeconds = Math.Min(offlineSeconds, capSeconds);

        // Calculate earnings
        double earned = usedSeconds * exitMps * efficiency;

        // Log details
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
        {
            Debug.Log($"[PitStopCrew] Offline: away={offlineSeconds:F0}s used={usedSeconds:F0}s cap={capSeconds:F0}s exitMps={exitMps:F2} eff={efficiency:P0} earned={earned:F2}");
        }
#endif

        // Mark as granted and update timestamp to prevent re-grant
        _hasGrantedThisSession = true;
        PlayerPrefs.SetString(KEY_OFFLINE_GRANTED_AT, now.ToString());
        PlayerPrefs.SetString(KEY_LAST_QUIT_TS, now.ToString()); // Update to now
        PlayerPrefs.Save();

        // Fire event
        OnOfflineEarningsComputed?.Invoke(earned, offlineSeconds, usedSeconds, exitMps, level, efficiency);

        // F5: Offline earnings SFX
        if (earned > 0 && SFXManager.Instance != null)
            SFXManager.Instance.PlayPitStopEarnings();

        // Grant earnings with animation (if above minimum)
        if (earned >= minimumEarnedToAnimate)
        {
            if (CurrencyManager.Instance != null)
            {
                // Mark offline earnings so MPS display ignores the payout
                CurrencyManager.Instance.MarkOfflineEarnings(earned);
                CurrencyManager.Instance.AddMoneyAnimated(earned, countUpDuration, "Offline Earnings");
                // Fire completion event after animation duration
                StartCoroutine(FireCountUpCompleteAfterDelay(earned, countUpDuration));
            }
        }
        else if (earned > 0)
        {
            // Tiny amount, just add instantly
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.MarkOfflineEarnings(earned);
                CurrencyManager.Instance.AddMoney(earned);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log($"[PitStopCrew] Earned {earned:F2} (below animation threshold, added instantly).");
#endif
            OnCountUpComplete?.Invoke(earned);
        }

        _grantCoroutine = null;
    }

    private IEnumerator FireCountUpCompleteAfterDelay(double earned, float delay)
    {
        yield return new WaitForSeconds(delay);
        OnCountUpComplete?.Invoke(earned);
    }

    /// <summary>
    /// Force recalculate and grant offline earnings (for testing).
    /// </summary>
    [ContextMenu("Force Grant Offline Earnings")]
    public void ForceGrantOfflineEarnings()
    {
        // Stop any existing grant coroutine to prevent overlap
        if (_grantCoroutine != null)
        {
            StopCoroutine(_grantCoroutine);
            _grantCoroutine = null;
        }
        _hasGrantedThisSession = false;
        _grantCoroutine = StartCoroutine(TryGrantOfflineEarningsOnStartup());
    }

    // ==================== LEVEL HELPERS ====================

    private int GetPitStopCrewLevel()
    {
        if (CardManager.Instance == null) return 0;

        CardDefinition card = CardManager.Instance.GetCard(CardType.PitStopCrew);
        if (card == null) return 0;

        return Mathf.Clamp(card.currentLevel, 0, 6);
    }

    private float GetEfficiencyForLevel(int level)
    {
        if (level <= 0) return 0f;
        int index = Mathf.Clamp(level, 0, EfficiencyByLevel.Length - 1);
        return EfficiencyByLevel[index];
    }

    private float GetCapHoursForLevel(int level)
    {
        if (level <= 0) return 0f;
        int index = Mathf.Clamp(level, 0, CapHoursByLevel.Length - 1);
        return CapHoursByLevel[index];
    }

    // ==================== MPS HELPERS ====================

    private double GetCurrentMps()
    {
        if (CurrencyManager.Instance == null) return 0.0;

        // Use the base moneyPerSecond as the snapshot
        // This represents the player's "passive income rate" without temporary buffs
        return CurrencyManager.Instance.moneyPerSecond;
    }

    // ==================== UTILITY ====================

    private long GetUnixTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private long GetLongFromPrefs(string key, long defaultValue)
    {
        string s = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(s)) return defaultValue;
        if (long.TryParse(s, out long v)) return v;
        return defaultValue;
    }

    private double GetDoubleFromPrefs(string key, double defaultValue)
    {
        string s = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(s)) return defaultValue;
        if (double.TryParse(s, NumberStyles.Float, Culture, out double v)) return v;
        return defaultValue;
    }

    // ==================== PUBLIC PROPERTIES ====================

    /// <summary>
    /// Current PitStopCrew card level (0 if locked).
    /// </summary>
    public int CurrentCardLevel => GetPitStopCrewLevel();

    /// <summary>
    /// Efficiency for current card level.
    /// </summary>
    public float CurrentEfficiency => GetEfficiencyForLevel(GetPitStopCrewLevel());

    /// <summary>
    /// Cap hours for current card level.
    /// </summary>
    public float CurrentCapHours => GetCapHoursForLevel(GetPitStopCrewLevel());

    /// <summary>
    /// Last known MPS that will be saved on exit.
    /// </summary>
    public double LastKnownMps => _lastKnownMps;

    // ==================== DEBUG (EDITOR ONLY) ====================

#if UNITY_EDITOR
    /// <summary>
    /// Saves the current exit snapshot (timestamp + MPS) as if the app is about to quit.
    /// Use this to set up the "before" state for testing offline earnings.
    /// </summary>
    [ContextMenu("DEBUG/1. Save Exit Snapshot Now")]
    private void Debug_SaveExitSnapshotNow()
    {
        if (!debugEnabled)
        {
            Debug.LogWarning("[PitStopCrew][DEBUG] Debug is disabled. Enable debugEnabled first.");
            return;
        }

        long now = GetUnixTimestamp();
        double mps = GetCurrentMps();

        PlayerPrefs.SetString(KEY_LAST_QUIT_TS, now.ToString());
        PlayerPrefs.SetString(KEY_LAST_EXIT_MPS, mps.ToString(Culture));
        PlayerPrefs.Save();

        Debug.Log($"[PitStopCrew][DEBUG] Exit snapshot SAVED:\n" +
                  $"  Timestamp: {now} (Unix seconds)\n" +
                  $"  Exit MPS: {mps:F2}\n" +
                  $"  Card Level: {GetPitStopCrewLevel()}\n" +
                  $"  Efficiency: {GetEfficiencyForLevel(GetPitStopCrewLevel()):P0}\n" +
                  $"  Cap Hours: {GetCapHoursForLevel(GetPitStopCrewLevel())}h");
    }

    /// <summary>
    /// Simulates returning to the app after debugSimulateOfflineSeconds.
    /// Calculates and grants offline earnings using the same production path.
    /// </summary>
    [ContextMenu("DEBUG/2. Simulate Return After X Seconds")]
    private void Debug_SimulateReturnAfterX()
    {
        if (!debugEnabled)
        {
            Debug.LogWarning("[PitStopCrew][DEBUG] Debug is disabled. Enable debugEnabled first.");
            return;
        }

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PitStopCrew][DEBUG] Must be in Play Mode to simulate return.");
            return;
        }

        StartCoroutine(Debug_SimulateReturnCoroutine());
    }

    private IEnumerator Debug_SimulateReturnCoroutine()
    {
        // Wait for systems
        yield return null;
        if (CurrencyManager.Instance == null || CardManager.Instance == null)
        {
            Debug.LogError("[PitStopCrew][DEBUG] CurrencyManager or CardManager not ready.");
            yield break;
        }

        // Get card level
        int level = GetPitStopCrewLevel();
        if (level <= 0)
        {
            Debug.LogWarning("[PitStopCrew][DEBUG] Card is locked (level 0). Unlock PitStopCrew card first.");
            yield break;
        }

        // Determine exit MPS
        double exitMps;
        if (debugUseCurrentMpsAsExitSnapshot)
        {
            exitMps = GetCurrentMps();
            Debug.Log($"[PitStopCrew][DEBUG] Using CURRENT MPS as exit snapshot: {exitMps:F2}");
        }
        else
        {
            exitMps = GetDoubleFromPrefs(KEY_LAST_EXIT_MPS, 0.0);
            if (exitMps <= 0)
            {
                Debug.LogWarning("[PitStopCrew][DEBUG] No stored exit MPS found. Use 'Save Exit Snapshot Now' first, or enable debugUseCurrentMpsAsExitSnapshot.");
                yield break;
            }
            Debug.Log($"[PitStopCrew][DEBUG] Using STORED MPS from PlayerPrefs: {exitMps:F2}");
        }

        // Simulate offline time
        double offlineSeconds = debugSimulateOfflineSeconds;

        // Get level-based parameters (same as production)
        float efficiency = GetEfficiencyForLevel(level);
        float capHours = GetCapHoursForLevel(level);
        double capSeconds = capHours * 3600.0;

        // Apply cap
        double usedSeconds = Math.Min(offlineSeconds, capSeconds);

        // Calculate earnings (SAME formula as production)
        double earned = usedSeconds * exitMps * efficiency;

        // Log detailed summary
        Debug.Log($"[PitStopCrew][DEBUG] ========== SIMULATION RESULT ==========\n" +
                  $"  Simulated offline: {offlineSeconds}s ({offlineSeconds / 60:F1} min)\n" +
                  $"  Cap: {capSeconds}s ({capHours}h)\n" +
                  $"  Used: {usedSeconds}s\n" +
                  $"  Exit MPS: {exitMps:F2}\n" +
                  $"  Card Level: {level}\n" +
                  $"  Efficiency: {efficiency:P0}\n" +
                  $"  EARNED: ${earned:F2}\n" +
                  $"  ===========================================");

        // Fire event (same as production)
        OnOfflineEarningsComputed?.Invoke(earned, offlineSeconds, usedSeconds, exitMps, level, efficiency);

        // Grant with animation (same as production)
        if (earned >= minimumEarnedToAnimate)
        {
            Debug.Log($"[PitStopCrew][DEBUG] CountUp START: +{earned:F2} over {countUpDuration}s");
            CurrencyManager.Instance.AddMoneyAnimated(earned, countUpDuration, "DEBUG Offline Earnings");
            
            // Wait for animation to complete
            yield return new WaitForSeconds(countUpDuration + 0.1f);
            Debug.Log($"[PitStopCrew][DEBUG] CountUp END. Current money: {CurrencyManager.Instance.money:F2}");
        }
        else if (earned > 0)
        {
            CurrencyManager.Instance.AddMoney(earned);
            Debug.Log($"[PitStopCrew][DEBUG] Added {earned:F2} instantly (below animation threshold).");
        }
        else
        {
            Debug.Log("[PitStopCrew][DEBUG] Earned amount is 0 or negative. Nothing granted.");
        }

        // Update stored timestamp to prevent real grant on next startup
        long now = GetUnixTimestamp();
        PlayerPrefs.SetString(KEY_LAST_QUIT_TS, now.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Quick test: Save snapshot, then immediately simulate 5 minutes offline.
    /// </summary>
    [ContextMenu("DEBUG/3. Quick Test (5 min offline)")]
    private void Debug_QuickTest5Min()
    {
        if (!debugEnabled)
        {
            Debug.LogWarning("[PitStopCrew][DEBUG] Debug is disabled. Enable debugEnabled first.");
            return;
        }

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PitStopCrew][DEBUG] Must be in Play Mode.");
            return;
        }

        int originalSeconds = debugSimulateOfflineSeconds;
        debugSimulateOfflineSeconds = 300; // 5 minutes
        debugUseCurrentMpsAsExitSnapshot = true;

        Debug.Log("[PitStopCrew][DEBUG] Quick Test: Simulating 5 minutes offline with current MPS...");
        StartCoroutine(Debug_SimulateReturnCoroutine());

        debugSimulateOfflineSeconds = originalSeconds; // Restore
    }

    /// <summary>
    /// Quick test: Simulate 2 hours offline.
    /// </summary>
    [ContextMenu("DEBUG/4. Quick Test (2 hours offline)")]
    private void Debug_QuickTest2Hours()
    {
        if (!debugEnabled)
        {
            Debug.LogWarning("[PitStopCrew][DEBUG] Debug is disabled. Enable debugEnabled first.");
            return;
        }

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PitStopCrew][DEBUG] Must be in Play Mode.");
            return;
        }

        int originalSeconds = debugSimulateOfflineSeconds;
        debugSimulateOfflineSeconds = 7200; // 2 hours
        debugUseCurrentMpsAsExitSnapshot = true;

        Debug.Log("[PitStopCrew][DEBUG] Quick Test: Simulating 2 hours offline with current MPS...");
        StartCoroutine(Debug_SimulateReturnCoroutine());

        debugSimulateOfflineSeconds = originalSeconds; // Restore
    }

    /// <summary>
    /// Clears all PitStopCrew PlayerPrefs data.
    /// </summary>
    [ContextMenu("DEBUG/Clear PitStopCrew PlayerPrefs")]
    private void Debug_ClearPlayerPrefs()
    {
        PlayerPrefs.DeleteKey(KEY_LAST_QUIT_TS);
        PlayerPrefs.DeleteKey(KEY_LAST_EXIT_MPS);
        PlayerPrefs.DeleteKey(KEY_OFFLINE_GRANTED_AT);
        PlayerPrefs.Save();
        _hasGrantedThisSession = false;
        Debug.Log("[PitStopCrew][DEBUG] All PitStopCrew PlayerPrefs cleared.");
    }

    /// <summary>
    /// Shows current debug state in console.
    /// </summary>
    [ContextMenu("DEBUG/Show Current State")]
    private void Debug_ShowCurrentState()
    {
        long storedTs = GetLongFromPrefs(KEY_LAST_QUIT_TS, 0);
        double storedMps = GetDoubleFromPrefs(KEY_LAST_EXIT_MPS, 0);
        long grantedAt = GetLongFromPrefs(KEY_OFFLINE_GRANTED_AT, 0);

        Debug.Log($"[PitStopCrew][DEBUG] ========== CURRENT STATE ==========\n" +
                  $"  Card Level: {GetPitStopCrewLevel()}\n" +
                  $"  Efficiency: {CurrentEfficiency:P0}\n" +
                  $"  Cap Hours: {CurrentCapHours}h\n" +
                  $"  ---\n" +
                  $"  Live MPS: {GetCurrentMps():F2}\n" +
                  $"  Last Known MPS (runtime): {_lastKnownMps:F2}\n" +
                  $"  ---\n" +
                  $"  Stored Exit Timestamp: {storedTs}\n" +
                  $"  Stored Exit MPS: {storedMps:F2}\n" +
                  $"  Last Granted At: {grantedAt}\n" +
                  $"  Has Granted This Session: {_hasGrantedThisSession}\n" +
                  $"  ---\n" +
                  $"  Debug Simulate Seconds: {debugSimulateOfflineSeconds}\n" +
                  $"  =====================================");
    }
#endif

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
