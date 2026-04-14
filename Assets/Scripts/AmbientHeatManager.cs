using UnityEngine;

/// <summary>
/// AmbientHeatManager — Hidden pacing layer for the Police Catch system.
///
/// PURPOSE:
///   Even a skilled player who catches every radar should occasionally trigger a police chase.
///   This manager provides that inevitability via a hidden "heat" value that fills passively
///   over time and also reacts to radar events, without ever being shown to the player.
///
/// DESIGN PRINCIPLES:
///   • Heat is INVISIBLE to the player. Popularity bar remains the visible risk meter.
///   • Heat does NOT replace the Popularity / miss-threshold system.
///     Both systems can independently request a chase via PoliceCatchTrigger.RequestPoliceCatch().
///   • Heat fills slowly over time (passive tension).
///   • Radar misses spike heat significantly.
///   • Radar catches cool heat slightly.
///   • A chase ending (success or fail) drops heat significantly and starts a cooldown.
///   • Ticking pauses during police chase, chest popup, and (optionally) content panels.
///
/// INTEGRATION:
///   • Subscribes to PopularityManager.OnRadarPhotoTaken (radar miss → heat up)
///   • Subscribes to PopularityManager.OnRadarDefused    (radar catch → heat down)
///   • Subscribes to PoliceCatchController.OnChaseEnded  (chase end → heat drop + cooldown)
///   • Calls PoliceCatchTrigger.RequestPoliceCatch(AmbientHeatThreshold) when threshold crossed
///
/// PLACEMENT:
///   Add to the same persistent (DontDestroyOnLoad) GameObject as PoliceCatchTrigger.
///   No scene references are required — everything is driven by events and Inspector values.
/// </summary>
public class AmbientHeatManager : MonoBehaviour
{
    public static AmbientHeatManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    // ==================== HEAT TUNING ====================

    [Header("Heat Range")]
    [Tooltip("Heat value at game start (and after a full reset). Usually 0.")]
    [SerializeField] private float startHeat = 0f;

    [Tooltip("Maximum possible heat value. Heat is always clamped to [0, maxHeat].")]
    [SerializeField] private float maxHeat = 100f;

    [Tooltip("Heat level at which a police chase is requested via AmbientHeatThreshold.\n" +
             "Recommended: 60-80 % of maxHeat.")]
    [SerializeField] private float heatThreshold = 70f;

    [Header("Heat Rates")]
    [Tooltip("Heat added per second during normal gameplay (passive tension).\n" +
             "Default 0.20 → ~350 s (≈5.8 min) of passive play to reach threshold from 0.\n" +
             "Lower this to be gentler with good players.")]
    [SerializeField] private float passiveHeatPerSecond = 0.20f;

    [Tooltip("Heat added when the player misses a radar.\n" +
             "Default 6.0 → ~12 consecutive misses raise heat from 0 to threshold.")]
    [SerializeField] private float missHeatGain = 6f;

    [Tooltip("Heat removed when the player catches (taps) a radar.\n" +
             "Should be noticeably less than missHeatGain so misses matter.")]
    [SerializeField] private float catchHeatLoss = 2f;

    [Tooltip("Heat removed immediately when a police chase ends (success or failure).\n" +
             "This is the main reset. Large value = longer breathing room after a chase.")]
    [SerializeField] private float chaseEndHeatDrop = 45f;

    [Header("Post-Chase Cooldown")]
    [Tooltip("Seconds after a chase ends during which heat threshold cannot trigger another chase.\n" +
             "Note: PoliceCatchTrigger.minimumTimeBetweenChases also applies to all paths.\n" +
             "This cooldown is heat-path-specific and stacks on top.")]
    [SerializeField] private float postChaseCooldown = 30f;

    [Header("Pause Conditions")]
    [Tooltip("Freeze heat ticking while a police chase is active.")]
    [SerializeField] private bool pauseOnPoliceChase = true;

    [Tooltip("Freeze heat ticking while a chest popup is open.")]
    [SerializeField] private bool pauseOnChestPopup = true;

    [Tooltip("Freeze heat ticking while a UI content panel is open " +
             "(same suppression gate that pauses RadarSpawner).")]
    [SerializeField] private bool pauseOnContentPanel = true;

    [Tooltip("Freeze heat ticking while the radar snapshot popup is displayed.\n" +
             "Usually fine to leave ON (popup is brief); set OFF for more aggression.")]
    [SerializeField] private bool pauseOnRadarPopup = false;

    [Header("Debug")]
    [Tooltip("Log heat changes and threshold events to the Console.")]
    [SerializeField] private bool enableDebugLogs = false;

    // ==================== RUNTIME STATE (read-only via properties) ====================

    /// <summary>Current raw heat value.</summary>
    public float CurrentHeat => _heat;

    /// <summary>Heat as a normalized 0–1 fraction of maxHeat. Useful for debug displays.</summary>
    public float NormalizedHeat => maxHeat > 0f ? Mathf.Clamp01(_heat / maxHeat) : 0f;

    /// <summary>True while the post-chase cooldown is preventing heat-triggered chases.</summary>
    public bool IsInPostChaseCooldown => _postChaseCooldownTimer > 0f;

    /// <summary>Remaining seconds of the post-chase cooldown (0 when not in cooldown).</summary>
    public float PostChaseCooldownRemaining => Mathf.Max(0f, _postChaseCooldownTimer);

    private float _heat;
    private float _postChaseCooldownTimer;

    // ==================== LIFECYCLE ====================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _heat = startHeat;
    }

    private void OnEnable()
    {
        PopularityManager.OnRadarPhotoTaken += OnRadarMissed;
        PopularityManager.OnRadarDefused += OnRadarCaught;
        PoliceCatchController.OnChaseEnded += OnChaseEnded;
    }

    private void OnDisable()
    {
        PopularityManager.OnRadarPhotoTaken -= OnRadarMissed;
        PopularityManager.OnRadarDefused -= OnRadarCaught;
        PoliceCatchController.OnChaseEnded -= OnChaseEnded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ==================== UPDATE ====================

    private void Update()
    {
        // Tick the post-chase cooldown down every frame
        if (_postChaseCooldownTimer > 0f)
            _postChaseCooldownTimer -= Time.deltaTime;

        // Only tick passive heat when gameplay is in an active state
        if (!ShouldHeatTick()) return;

        // Passive heat gain
        _heat = Mathf.Clamp(_heat + passiveHeatPerSecond * Time.deltaTime, 0f, maxHeat);

        // Check if threshold is crossed — attempt to request a chase
        if (_heat >= heatThreshold)
            TryRequestHeatChase();
    }

    // ==================== PAUSE GATE ====================

    private bool ShouldHeatTick()
    {
        if (pauseOnPoliceChase &&
            PoliceCatchController.Instance != null &&
            PoliceCatchController.Instance.IsChaseActive)
            return false;

        if (pauseOnChestPopup &&
            ChestPopupController.Instance != null &&
            ChestPopupController.Instance.IsPopupOpen)
            return false;

        if (pauseOnRadarPopup &&
            RadarPopupController.Instance != null &&
            RadarPopupController.Instance.IsPopupOpen)
            return false;

        if (pauseOnContentPanel && UIFlowState.IsSpawnSuppressed)
            return false;

        return true;
    }

    // ==================== THRESHOLD CHECK ====================

    private void TryRequestHeatChase()
    {
        // Guard: own post-chase cooldown
        if (_postChaseCooldownTimer > 0f)
        {
            if (enableDebugLogs)
                Debug.Log($"[AmbientHeat] Heat={_heat:F1} crossed threshold={heatThreshold} " +
                          $"but in post-chase cooldown ({_postChaseCooldownTimer:F1}s remaining). Waiting.");
            return;
        }

        // Guard: a request is already pending or a chase is already running
        // (PoliceCatchTrigger.RequestPoliceCatch handles this too, but checking here
        //  prevents spamming the log and avoids the immediate heat-drop below).
        if (PoliceCatchTrigger.Instance != null && PoliceCatchTrigger.Instance.HasPendingRequest)
        {
            if (enableDebugLogs)
                Debug.Log($"[AmbientHeat] Heat={_heat:F1} crossed threshold but chase pending. Waiting.");
            return;
        }

        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
        {
            if (enableDebugLogs)
                Debug.Log($"[AmbientHeat] Heat={_heat:F1} crossed threshold but chase already active. Waiting.");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[AmbientHeat] Heat threshold reached ({_heat:F1}/{heatThreshold}). " +
                      $"Requesting AmbientHeatThreshold police chase.");

        // Partially reset heat immediately so threshold is not crossed every frame
        // while the pending chase waits for the popup to close.
        // The real big drop happens in OnChaseEnded.
        _heat = Mathf.Clamp(_heat - heatThreshold * 0.6f, 0f, maxHeat);

        PoliceCatchTrigger.Instance?.RequestPoliceCatch(PoliceCatchReason.AmbientHeatThreshold);
    }

    // ==================== EVENT HANDLERS ====================

    /// <summary>Called when the player misses a radar (OnRadarPhotoTaken fires).</summary>
    private void OnRadarMissed()
    {
        float before = _heat;
        _heat = Mathf.Clamp(_heat + missHeatGain, 0f, maxHeat);

        if (enableDebugLogs)
            Debug.Log($"[AmbientHeat] Radar MISSED. Heat: {before:F1} → {_heat:F1} (+{missHeatGain})");
    }

    /// <summary>Called when the player catches (taps) a radar (OnRadarDefused fires).</summary>
    private void OnRadarCaught()
    {
        float before = _heat;
        _heat = Mathf.Clamp(_heat - catchHeatLoss, 0f, maxHeat);

        if (enableDebugLogs)
            Debug.Log($"[AmbientHeat] Radar CAUGHT. Heat: {before:F1} → {_heat:F1} (-{catchHeatLoss})");
    }

    /// <summary>
    /// Called when a police chase fully ends (OnChaseEnded fires from PoliceCatchController).
    /// Applies the significant post-chase heat drop and starts the cooldown timer.
    /// </summary>
    private void OnChaseEnded()
    {
        float before = _heat;
        _heat = Mathf.Clamp(_heat - chaseEndHeatDrop, 0f, maxHeat);
        _postChaseCooldownTimer = postChaseCooldown;

        if (enableDebugLogs)
            Debug.Log($"[AmbientHeat] Chase ENDED. Heat: {before:F1} → {_heat:F1} (-{chaseEndHeatDrop}). " +
                      $"Post-chase cooldown: {postChaseCooldown}s");
    }

    // ==================== SAVE / LOAD ====================

    private const string SaveKey_AH_Heat = "Save_AmbientHeat_Heat";
    private const string SaveKey_AH_CooldownUTC = "Save_AmbientHeat_CooldownEndUTC";

    /// <summary>Saves heat state to PlayerPrefs. Called by SaveSystem.</summary>
    public void SaveState()
    {
        PlayerPrefs.SetFloat(SaveKey_AH_Heat, _heat);

        if (_postChaseCooldownTimer > 0f)
        {
            long utcEnd = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)System.Math.Ceiling(_postChaseCooldownTimer);
            PlayerPrefs.SetString(SaveKey_AH_CooldownUTC, utcEnd.ToString());
        }
        else
        {
            PlayerPrefs.SetString(SaveKey_AH_CooldownUTC, "0");
        }
    }

    /// <summary>Loads heat state from PlayerPrefs. Called by SaveSystem.</summary>
    public void LoadState()
    {
        _heat = PlayerPrefs.GetFloat(SaveKey_AH_Heat, startHeat);

        string endStr = PlayerPrefs.GetString(SaveKey_AH_CooldownUTC, "0");
        if (long.TryParse(endStr, out long utcEnd) && utcEnd > 0)
        {
            long nowUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long remaining = utcEnd - nowUtc;
            _postChaseCooldownTimer = remaining > 0 ? remaining : 0f;
        }

        if (enableDebugLogs)
            Debug.Log($"[AmbientHeat] LoadState: heat={_heat:F1} cooldown={_postChaseCooldownTimer:F1}s");
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Manually resets heat to startHeat and clears the cooldown.
    /// Useful for save resets or debug purposes.
    /// </summary>
    public void ResetHeat()
    {
        _heat = startHeat;
        _postChaseCooldownTimer = 0f;

        if (enableDebugLogs)
            Debug.Log("[AmbientHeat] Heat manually reset.");
    }

    // ==================== EDITOR TEST ====================

#if UNITY_EDITOR
    [ContextMenu("TEST: Simulate Radar Miss (heat up)")]
    private void DebugRadarMiss()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[AmbientHeat] Must be in Play Mode."); return; }
        OnRadarMissed();
    }

    [ContextMenu("TEST: Simulate Radar Catch (heat down)")]
    private void DebugRadarCatch()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[AmbientHeat] Must be in Play Mode."); return; }
        OnRadarCaught();
    }

    [ContextMenu("TEST: Set heat to threshold")]
    private void DebugSetHeatToThreshold()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[AmbientHeat] Must be in Play Mode."); return; }
        _heat = heatThreshold;
        Debug.Log($"[AmbientHeat] Heat manually set to threshold ({heatThreshold}).");
    }

    [ContextMenu("TEST: Simulate Chase Ended")]
    private void DebugChaseEnded()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[AmbientHeat] Must be in Play Mode."); return; }
        OnChaseEnded();
    }

    [ContextMenu("TEST: Log Heat State")]
    private void DebugLogState()
    {
        Debug.Log($"[AmbientHeat] State:\n" +
                  $"  heat            = {_heat:F2} / {maxHeat} ({NormalizedHeat * 100f:F1}%)\n" +
                  $"  threshold       = {heatThreshold}\n" +
                  $"  cooldown        = {_postChaseCooldownTimer:F1}s remaining\n" +
                  $"  passiveRate     = {passiveHeatPerSecond}/s\n" +
                  $"  missGain        = {missHeatGain}\n" +
                  $"  catchLoss       = {catchHeatLoss}\n" +
                  $"  chaseEndDrop    = {chaseEndHeatDrop}");
    }
#endif
}
