using UnityEngine;
using System.Collections;

/// <summary>
/// Reason why a police chase was requested.
/// Used for debug tracing and future branching logic.
/// </summary>
public enum PoliceCatchReason
{
    /// <summary>Player missed enough radars given their current Popularity Stage.</summary>
    RadarMissThreshold,

    /// <summary>AmbientHeatManager's hidden pressure crossed its configured threshold.</summary>
    AmbientHeatThreshold
}

/// <summary>
/// Single-authority manager that handles ALL police chase request routing.
///
/// DUAL TRIGGER PATHS:
///   1. RadarMissThreshold — player misses enough radars for their Popularity Stage.
///   2. AmbientHeatThreshold — hidden AmbientHeatManager pressure crosses its threshold.
///
/// Both paths funnel into the unified RequestPoliceCatch() method, which owns:
///   - duplicate/race-condition guards
///   - cooldown enforcement
///   - popup-safe deferred start
///   - the single call to PoliceCatchController.StartChase()
///
/// DEFERRED START:
///   Chase does NOT start immediately on request. It waits for the current radar
///   popup to close (if open), then applies a brief postPopupDelay.
///   If no popup is open, DeferredStartChase runs immediately.
///
/// PERSISTENCE:
///   radarCatchCounter and pendingPoliceCatch are saved to PlayerPrefs.
///   SaveSystem calls SaveState() / LoadState().
///
/// Usage:
///   Place this on a persistent GameObject (e.g. GameManager).
/// </summary>
public class PoliceCatchTrigger : MonoBehaviour
{
    public static PoliceCatchTrigger Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    // ==================== CONFIGURATION ====================

    [Header("Thresholds (catches needed per stage)")]
    [Tooltip("Stage1 [0,18) — safest.")]
    [SerializeField] private int thresholdStage1 = 13;
    [Tooltip("Stage2 [18,36).")]
    [SerializeField] private int thresholdStage2 = 11;
    [Tooltip("Stage3 [36,54).")]
    [SerializeField] private int thresholdStage3 = 9;
    [Tooltip("Stage4 [54,72).")]
    [SerializeField] private int thresholdStage4 = 7;
    [Tooltip("Stage5 [72,90).")]
    [SerializeField] private int thresholdStage5 = 5;
    [Tooltip("Stage6 [90,100] — most dangerous.")]
    [SerializeField] private int thresholdStage6 = 3;

    [Header("Deferred Start")]
    [Tooltip("Extra delay (seconds) after radar popup closes before starting Police Chase.")]
    [SerializeField] private float postPopupDelay = 0.3f;

    [Header("Cooldown")]
    [Tooltip("Minimum time (seconds) between any two police chases. " +
             "Prevents back-to-back chases from both trigger paths. 0 = no limit.")]
    [SerializeField] private float minimumTimeBetweenChases = 15f;

    [Header("Debug")]
    [Tooltip("Enable verbose debug logs for all trigger paths, pending state, and cooldown.")]
    [SerializeField] private bool enableDebugLogs = false;

    // ==================== RUNTIME STATE ====================

    /// <summary>Number of radar misses since the last miss-threshold chase request.</summary>
    public int RadarCatchCounter => _radarCatchCounter;
    private int _radarCatchCounter = 0;

    /// <summary>True when a chase has been requested and is waiting to start.</summary>
    public bool PendingPoliceCatch => _pendingPoliceCatch;
    private bool _pendingPoliceCatch = false;

    /// <summary>
    /// True when a chase has been requested and is waiting to start.
    /// Used externally (e.g. AmbientHeatManager) to avoid duplicate requests.
    /// </summary>
    public bool HasPendingRequest => _pendingPoliceCatch;

    /// <summary>The reason the current (or last) chase was requested.</summary>
    public PoliceCatchReason LastTriggerReason => _lastTriggerReason;
    private PoliceCatchReason _lastTriggerReason = PoliceCatchReason.RadarMissThreshold;

    /// <summary>Time.time when the last chase fully ended. Used for cooldown.</summary>
    public float TimeSinceLastChase => Time.time - _lastChaseEndTime;
    private float _lastChaseEndTime = -9999f;

    // Coroutine handle for the deferred start delay
    private Coroutine _deferredStartCoroutine;

    // ==================== PLAYERPREFS KEYS ====================

    private const string KEY_RADAR_CATCH_COUNTER = "Save_RadarCatchCounter";
    private const string KEY_PENDING_POLICE_CATCH = "Save_PendingPoliceCatch";
    private const string KEY_LAST_CHASE_END_UTC = "Save_PoliceCooldownEndUTC";

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
    }

    private void OnEnable()
    {
        PopularityManager.OnRadarPhotoTaken += HandleRadarPhotoTaken;
        RadarPopupController.OnRadarPopupClosed += HandleRadarPopupClosed;
        PoliceCatchController.OnChaseEnded += HandleChaseEnded;
    }

    private void OnDisable()
    {
        PopularityManager.OnRadarPhotoTaken -= HandleRadarPhotoTaken;
        RadarPopupController.OnRadarPopupClosed -= HandleRadarPopupClosed;
        PoliceCatchController.OnChaseEnded -= HandleChaseEnded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ==================== UNIFIED REQUEST PIPELINE ====================

    /// <summary>
    /// THE single entry point for all police chase requests.
    ///
    /// Safe to call from any trigger source (radar miss threshold, ambient heat, etc.).
    /// Handles all guards internally:
    ///   • ignores if a chase is already active
    ///   • ignores if a chase is already pending
    ///   • ignores if the cooldown has not expired
    ///
    /// If the radar popup is currently open, the chase defers until it closes.
    /// If no popup is open, the deferred coroutine starts immediately.
    /// </summary>
    /// <param name="reason">The source that triggered this request (for debugging).</param>
    public void RequestPoliceCatch(PoliceCatchReason reason)
    {
        // Guard: a chase is already running
        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
        {
            if (enableDebugLogs)
                Debug.Log($"[PoliceCatchTrigger] RequestPoliceCatch({reason}) IGNORED — chase already active.");
            return;
        }

        // Guard: a chase is already pending
        if (_pendingPoliceCatch)
        {
            if (enableDebugLogs)
                Debug.Log($"[PoliceCatchTrigger] RequestPoliceCatch({reason}) IGNORED — chase already pending (reason={_lastTriggerReason}).");
            return;
        }

        // Guard: minimum time between chases (cooldown)
        if (minimumTimeBetweenChases > 0f && Time.time - _lastChaseEndTime < minimumTimeBetweenChases)
        {
            float remaining = minimumTimeBetweenChases - (Time.time - _lastChaseEndTime);
            if (enableDebugLogs)
                Debug.Log($"[PoliceCatchTrigger] RequestPoliceCatch({reason}) IGNORED — cooldown active ({remaining:F1}s remaining).");
            return;
        }

        // All guards passed — register the pending request
        _pendingPoliceCatch = true;
        _lastTriggerReason = reason;

        if (enableDebugLogs)
            Debug.Log($"[PoliceCatchTrigger] RequestPoliceCatch({reason}) ACCEPTED — pending=true.");

        // If the radar popup is currently open, wait for it to close.
        // HandleRadarPopupClosed will start DeferredStartChase when closed.
        if (RadarPopupController.Instance != null && RadarPopupController.Instance.IsPopupOpen)
        {
            if (enableDebugLogs)
                Debug.Log("[PoliceCatchTrigger] Popup is open — deferring until popup closes.");
            return;
        }

        // No popup currently open — start the deferred coroutine immediately
        if (_deferredStartCoroutine != null)
            StopCoroutine(_deferredStartCoroutine);
        _deferredStartCoroutine = StartCoroutine(DeferredStartChase());
    }

    // ==================== PRIVATE EVENT HANDLERS ====================

    /// <summary>
    /// Called each time a radar takes a photo (player missed it).
    /// Counts misses against the stage-based threshold.
    /// Calls RequestPoliceCatch when threshold is crossed.
    /// </summary>
    private void HandleRadarPhotoTaken()
    {
        // If a chase is already pending or active, accumulate toward the NEXT chase only
        if (_pendingPoliceCatch ||
            (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive))
        {
            _radarCatchCounter++;
            if (enableDebugLogs)
                Debug.Log($"[PoliceCatchTrigger] Radar photo while chase pending/active. " +
                          $"Counter now {_radarCatchCounter} (toward NEXT chase).");
            return;
        }

        _radarCatchCounter++;

        // Read current stage and threshold
        PopularityStage stage = PopularityStage.Stage1;
        float popularity01 = 0f;
        if (PopularityManager.Instance != null)
        {
            stage = PopularityManager.Instance.GetCurrentStage();
            popularity01 = PopularityManager.Instance.Popularity01;
        }

        int threshold = GetThresholdForStage(stage);

        if (enableDebugLogs)
            Debug.Log($"[PoliceCatchTrigger] Radar miss — " +
                      $"popularity={Mathf.RoundToInt(popularity01 * 100f)} " +
                      $"stage={stage} " +
                      $"counter={_radarCatchCounter}/{threshold}");

        if (_radarCatchCounter >= threshold)
        {
            if (enableDebugLogs)
                Debug.Log($"[PoliceCatchTrigger] RadarMissThreshold reached " +
                          $"({_radarCatchCounter}/{threshold}, stage={stage}).");

            // Reset counter before requesting — next misses count toward the chase after this one
            _radarCatchCounter = 0;

            RequestPoliceCatch(PoliceCatchReason.RadarMissThreshold);
        }
    }

    /// <summary>
    /// Called when the radar popup finishes closing.
    /// If a chase was requested while the popup was open, start it now.
    /// </summary>
    private void HandleRadarPopupClosed()
    {
        if (!_pendingPoliceCatch) return;

        if (enableDebugLogs)
            Debug.Log($"[PoliceCatchTrigger] Popup closed with pending chase (reason={_lastTriggerReason}). " +
                      $"Starting after {postPopupDelay:F2}s delay.");

        if (_deferredStartCoroutine != null)
            StopCoroutine(_deferredStartCoroutine);
        _deferredStartCoroutine = StartCoroutine(DeferredStartChase());
    }

    /// <summary>
    /// Called when a police chase fully ends (via PoliceCatchController.OnChaseEnded).
    /// Records the end time for cooldown tracking.
    /// </summary>
    private void HandleChaseEnded()
    {
        _lastChaseEndTime = Time.time;

        if (enableDebugLogs)
            Debug.Log($"[PoliceCatchTrigger] Chase ended. Cooldown reset (minimumTimeBetweenChases={minimumTimeBetweenChases}s).");
    }

    // ==================== DEFERRED START COROUTINE ====================

    private IEnumerator DeferredStartChase()
    {
        if (postPopupDelay > 0f)
            yield return new WaitForSeconds(postPopupDelay);

        _deferredStartCoroutine = null;

        // Clear pending flag
        _pendingPoliceCatch = false;

        if (PoliceCatchController.Instance != null)
        {
            if (!PoliceCatchController.Instance.IsChaseActive)
            {
                if (enableDebugLogs)
                    Debug.Log($"[PoliceCatchTrigger] Starting chase now (reason={_lastTriggerReason}).");
                PoliceCatchController.Instance.StartChase();
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log("[PoliceCatchTrigger] Chase already active at deferred start time — skipping.");
            }
        }
        else
        {
            Debug.LogWarning("[PoliceCatchTrigger] PoliceCatchController.Instance is null — cannot start chase.");
        }
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Called by external systems (e.g. SaveSystem.OnGameLoaded) to try firing
    /// a pending Police Chase that was loaded from a previous session.
    ///
    /// Per our deterministic safe rule: if no popup is currently open, the chase
    /// defers until the NEXT radar popup closes. This avoids a surprise instant-start
    /// during or immediately after loading.
    /// </summary>
    public void TryFirePendingPoliceCatch()
    {
        if (!_pendingPoliceCatch) return;

        // If popup is currently open the normal OnRadarPopupClosed flow handles it
        if (RadarPopupController.Instance != null && RadarPopupController.Instance.IsPopupOpen)
        {
            if (enableDebugLogs)
                Debug.Log("[PoliceCatchTrigger] TryFirePending: popup open — waiting for popup close.");
            return;
        }

        // No popup open — keep pending flag; wait for NEXT radar popup close
        if (enableDebugLogs)
            Debug.Log("[PoliceCatchTrigger] TryFirePending: no popup open — " +
                      "pending remains true; chase will start after next popup closes.");
    }

    // ==================== THRESHOLD LOOKUP ====================

    /// <summary>Returns the configured miss threshold for the given popularity stage.</summary>
    public int GetThresholdForStage(PopularityStage stage)
    {
        switch (stage)
        {
            case PopularityStage.Stage1: return thresholdStage1;
            case PopularityStage.Stage2: return thresholdStage2;
            case PopularityStage.Stage3: return thresholdStage3;
            case PopularityStage.Stage4: return thresholdStage4;
            case PopularityStage.Stage5: return thresholdStage5;
            case PopularityStage.Stage6: return thresholdStage6;
            default: return thresholdStage1;
        }
    }

    // ==================== SAVE / LOAD ====================

    /// <summary>
    /// Saves radarCatchCounter and pendingPoliceCatch to PlayerPrefs.
    /// Called by SaveSystem.SaveGame().
    /// </summary>
    public void SaveState()
    {
        PlayerPrefs.SetInt(KEY_RADAR_CATCH_COUNTER, _radarCatchCounter);
        PlayerPrefs.SetInt(KEY_PENDING_POLICE_CATCH, _pendingPoliceCatch ? 1 : 0);

        // Persist cooldown end as UTC so offline time counts
        if (_lastChaseEndTime > 0f)
        {
            float elapsed = Time.time - _lastChaseEndTime;
            float cooldownLeft = minimumTimeBetweenChases - elapsed;
            if (cooldownLeft > 0f)
            {
                long utcEnd = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)System.Math.Ceiling(cooldownLeft);
                PlayerPrefs.SetString(KEY_LAST_CHASE_END_UTC, utcEnd.ToString());
            }
            else
            {
                PlayerPrefs.SetString(KEY_LAST_CHASE_END_UTC, "0");
            }
        }
        else
        {
            PlayerPrefs.SetString(KEY_LAST_CHASE_END_UTC, "0");
        }

        if (enableDebugLogs)
            Debug.Log($"[PoliceCatchTrigger] SaveState: counter={_radarCatchCounter} pending={_pendingPoliceCatch}");
    }

    /// <summary>
    /// Loads radarCatchCounter and pendingPoliceCatch from PlayerPrefs.
    /// Called by SaveSystem.LoadGame().
    /// Does NOT auto-fire pending chase — caller should invoke TryFirePendingPoliceCatch()
    /// after all systems are initialized.
    /// </summary>
    public void LoadState()
    {
        _radarCatchCounter = PlayerPrefs.GetInt(KEY_RADAR_CATCH_COUNTER, 0);
        _pendingPoliceCatch = PlayerPrefs.GetInt(KEY_PENDING_POLICE_CATCH, 0) == 1;

        // Restore cooldown from UTC timestamp
        string utcStr = PlayerPrefs.GetString(KEY_LAST_CHASE_END_UTC, "0");
        if (long.TryParse(utcStr, out long utcEnd) && utcEnd > 0)
        {
            long nowUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long remaining = utcEnd - nowUtc;
            if (remaining > 0)
            {
                // Cooldown still active: set _lastChaseEndTime so Time.time - _lastChaseEndTime < minimumTimeBetweenChases
                _lastChaseEndTime = Time.time - (minimumTimeBetweenChases - remaining);
            }
            else
            {
                _lastChaseEndTime = -9999f; // Cooldown expired offline
            }
        }

        if (enableDebugLogs)
            Debug.Log($"[PoliceCatchTrigger] LoadState: counter={_radarCatchCounter} pending={_pendingPoliceCatch}");
    }

    /// <summary>Manually resets the radar catch counter and clears any pending flag.</summary>
    public void ResetCounter()
    {
        _radarCatchCounter = 0;
        _pendingPoliceCatch = false;
        if (enableDebugLogs)
            Debug.Log("[PoliceCatchTrigger] Counter and pending flag reset.");
    }

    // ==================== EDITOR TEST ====================

#if UNITY_EDITOR
    [ContextMenu("TEST: Simulate Radar Photo")]
    private void DebugSimulatePhoto()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[PoliceCatchTrigger] Must be in Play Mode."); return; }
        HandleRadarPhotoTaken();
    }

    [ContextMenu("TEST: Simulate Popup Close")]
    private void DebugSimulatePopupClose()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[PoliceCatchTrigger] Must be in Play Mode."); return; }
        HandleRadarPopupClosed();
    }

    [ContextMenu("TEST: Request Chase (RadarMissThreshold)")]
    private void DebugRequestMissChase()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[PoliceCatchTrigger] Must be in Play Mode."); return; }
        RequestPoliceCatch(PoliceCatchReason.RadarMissThreshold);
    }

    [ContextMenu("TEST: Request Chase (AmbientHeatThreshold)")]
    private void DebugRequestHeatChase()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[PoliceCatchTrigger] Must be in Play Mode."); return; }
        RequestPoliceCatch(PoliceCatchReason.AmbientHeatThreshold);
    }

    [ContextMenu("TEST: Log State")]
    private void DebugLogState()
    {
        PopularityStage stage = PopularityStage.Stage1;
        float pop = 0f;
        if (PopularityManager.Instance != null)
        {
            stage = PopularityManager.Instance.GetCurrentStage();
            pop   = PopularityManager.Instance.Popularity01;
        }
        int threshold = GetThresholdForStage(stage);
        Debug.Log($"[PoliceCatchTrigger] State:\n" +
                  $"  popularity     = {Mathf.RoundToInt(pop * 100f)}\n" +
                  $"  stage          = {stage}\n" +
                  $"  missCounter    = {_radarCatchCounter}/{threshold}\n" +
                  $"  pending        = {_pendingPoliceCatch}\n" +
                  $"  lastReason     = {_lastTriggerReason}\n" +
                  $"  timeSinceChase = {TimeSinceLastChase:F1}s\n" +
                  $"  cooldown       = {minimumTimeBetweenChases}s");
    }
#endif
}
