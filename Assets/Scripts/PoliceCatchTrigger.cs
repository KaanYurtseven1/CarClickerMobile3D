using UnityEngine;
using System.Collections;

/// <summary>
/// Single-authority manager that links the Radar "photo taken" event to
/// the PoliceCatch minigame via the Popularity stage system.
///
/// DEFERRED START LOGIC:
///   • Each time a radar takes a photo (player missed it), radarCatchCounter increments.
///   • The current PopularityStage determines the threshold (catches needed).
///   • When radarCatchCounter >= threshold → pendingPoliceCatch is set to true.
///     The counter resets to 0 at this point.
///   • Police Chase does NOT start immediately. It waits until the radar popup
///     closes (RadarPopupController.OnRadarPopupClosed event).
///   • When the popup closes AND pendingPoliceCatch is true → PoliceCatchController.StartChase()
///     is called and the pending flag is cleared.
///   • If pendingPoliceCatch is already true, additional threshold reaches are ignored
///     (the counter was already reset; the pending flag is already set).
///
/// PERSISTENCE:
///   • radarCatchCounter is saved via PlayerPrefs key "Save_RadarCatchCounter".
///   • pendingPoliceCatch is saved via PlayerPrefs key "Save_PendingPoliceCatch" (0/1).
///   • On load, if pendingPoliceCatch is true and no radar popup is currently open,
///     Police Chase is deferred to the NEXT radar popup close (safest approach —
///     avoids surprise instant-start during loading). If a radar popup happens to be
///     open at load time the normal flow handles it.
///   • SaveSystem calls SaveState() / LoadState() during its save/load cycle.
///
/// Usage:
///   Place this component on a persistent GameObject (e.g. GameManager or its own object).
///   It auto-subscribes to PopularityManager.OnRadarPhotoTaken and
///   RadarPopupController.OnRadarPopupClosed.
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
    [Tooltip("Extra delay (seconds) after radar popup closes before starting Police Chase. " +
             "Gives a brief breathing room so the transition feels clean.")]
    [SerializeField] private float postPopupDelay = 0.3f;

    [Header("Debug")]
    [Tooltip("Enable verbose debug logs for popularity / catch / pending tracking.")]
    [SerializeField] private bool enableDebugLogs = false;

    // ==================== RUNTIME STATE ====================

    /// <summary>
    /// Number of radar photos taken since the last police chase (or game start).
    /// Persisted across sessions via SaveSystem.
    /// </summary>
    public int RadarCatchCounter => _radarCatchCounter;
    private int _radarCatchCounter = 0;

    /// <summary>
    /// True when the threshold has been reached but Police Chase has not yet started
    /// (waiting for radar popup to close). Persisted across sessions.
    /// </summary>
    public bool PendingPoliceCatch => _pendingPoliceCatch;
    private bool _pendingPoliceCatch = false;

    // Coroutine handle for the deferred start delay
    private Coroutine _deferredStartCoroutine;

    // ==================== PLAYERPREFS KEYS ====================

    private const string KEY_RADAR_CATCH_COUNTER = "Save_RadarCatchCounter";
    private const string KEY_PENDING_POLICE_CATCH = "Save_PendingPoliceCatch";

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
    }

    private void OnDisable()
    {
        PopularityManager.OnRadarPhotoTaken -= HandleRadarPhotoTaken;
        RadarPopupController.OnRadarPopupClosed -= HandleRadarPopupClosed;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ==================== CORE LOGIC ====================

    /// <summary>
    /// Called when a radar takes a photo (player missed it).
    /// Increments counter, checks threshold, sets pending flag if reached.
    /// Does NOT start Police Chase — that waits for popup close.
    /// </summary>
    private void HandleRadarPhotoTaken()
    {
        // If a chase is already pending or active, just increment the counter but don't re-trigger
        if (_pendingPoliceCatch)
        {
            // Counter was already reset when pending was set; new catches accumulate toward NEXT chase
            _radarCatchCounter++;
            if (enableDebugLogs)
            {
                Debug.Log($"[PoliceCatchTrigger] Radar photo while pending=true. " +
                          $"Counter now {_radarCatchCounter} (will count toward next chase).");
            }
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
        {
            Debug.Log($"[PoliceCatchTrigger] Radar photo! " +
                      $"popularity={Mathf.RoundToInt(popularity01 * 100f)} " +
                      $"stage={stage} " +
                      $"radarCatchCounter={_radarCatchCounter}/{threshold} " +
                      $"pending={_pendingPoliceCatch}");
        }

        // Check if threshold met
        if (_radarCatchCounter >= threshold)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PoliceCatchTrigger] THRESHOLD REACHED — setting pendingPoliceCatch=true. " +
                          $"(catches={_radarCatchCounter}, threshold={threshold}, stage={stage}). " +
                          $"Police Chase will start after radar popup closes.");
            }

            // Reset counter NOW (catches toward NEXT chase start fresh)
            _radarCatchCounter = 0;
            _pendingPoliceCatch = true;
        }
    }

    /// <summary>
    /// Called when the radar popup finishes closing.
    /// If a Police Chase is pending, this is the moment we start it.
    /// </summary>
    private void HandleRadarPopupClosed()
    {
        if (!_pendingPoliceCatch)
            return;

        if (enableDebugLogs)
        {
            Debug.Log("[PoliceCatchTrigger] Radar popup closed with pending Police Chase. " +
                      $"Starting chase after {postPopupDelay:F2}s delay...");
        }

        // Use a small delay so the transition feels smooth
        if (_deferredStartCoroutine != null)
            StopCoroutine(_deferredStartCoroutine);
        _deferredStartCoroutine = StartCoroutine(DeferredStartChase());
    }

    private IEnumerator DeferredStartChase()
    {
        if (postPopupDelay > 0f)
            yield return new WaitForSeconds(postPopupDelay);

        _deferredStartCoroutine = null;

        // Clear pending flag
        _pendingPoliceCatch = false;

        // Guard: don't start if a chase is already running
        if (PoliceCatchController.Instance != null)
        {
            if (!PoliceCatchController.Instance.IsChaseActive)
            {
                if (enableDebugLogs)
                    Debug.Log("[PoliceCatchTrigger] DEFERRED Police Chase starting NOW.");

                PoliceCatchController.Instance.StartChase();
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log("[PoliceCatchTrigger] Chase already active at deferred start — skipping.");
            }
        }
        else
        {
            Debug.LogWarning("[PoliceCatchTrigger] PoliceCatchController.Instance is null — cannot start chase.");
        }
    }

    /// <summary>
    /// Called by external systems (e.g. SaveSystem.OnGameLoaded) to try firing
    /// a pending Police Chase that was persisted from a previous session.
    ///
    /// RULE: If pending is true on load but no radar popup is currently open,
    /// we wait until the NEXT radar popup closes. This avoids a surprise
    /// instant-start during or right after loading.
    /// If a radar popup IS currently open, the normal OnRadarPopupClosed flow handles it.
    /// </summary>
    public void TryFirePendingPoliceCatch()
    {
        if (!_pendingPoliceCatch)
            return;

        // If radar popup is currently open, do nothing — the normal event flow
        // (OnRadarPopupClosed) will fire the chase when it closes.
        if (RadarPopupController.Instance != null && RadarPopupController.Instance.IsPopupOpen)
        {
            if (enableDebugLogs)
                Debug.Log("[PoliceCatchTrigger] TryFirePending: radar popup currently open — " +
                          "will start chase when it closes (normal flow).");
            return;
        }

        // No popup open. Per our deterministic rule: wait until NEXT radar popup closes.
        // The pending flag stays true and OnRadarPopupClosed will handle it.
        if (enableDebugLogs)
            Debug.Log("[PoliceCatchTrigger] TryFirePending: no popup open — " +
                      "pending flag remains true; chase will start after next radar popup closes.");
    }

    // ==================== THRESHOLD LOOKUP ====================

    /// <summary>
    /// Returns the configured threshold for the given stage.
    /// </summary>
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

        if (enableDebugLogs)
        {
            Debug.Log($"[PoliceCatchTrigger] SaveState: counter={_radarCatchCounter} " +
                      $"pending={_pendingPoliceCatch}");
        }
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

        if (enableDebugLogs)
        {
            Debug.Log($"[PoliceCatchTrigger] LoadState: counter={_radarCatchCounter} " +
                      $"pending={_pendingPoliceCatch}");
        }
    }

    /// <summary>
    /// Manually reset the radar catch counter and pending flag.
    /// </summary>
    public void ResetCounter()
    {
        _radarCatchCounter = 0;
        _pendingPoliceCatch = false;
        if (enableDebugLogs)
            Debug.Log("[PoliceCatchTrigger] Counter and pending flag reset to 0/false.");
    }

    // ==================== EDITOR TEST ====================

#if UNITY_EDITOR
    [ContextMenu("TEST: Simulate Radar Photo")]
    private void DebugSimulatePhoto()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PoliceCatchTrigger] Must be in Play Mode.");
            return;
        }
        HandleRadarPhotoTaken();
    }

    [ContextMenu("TEST: Simulate Popup Close")]
    private void DebugSimulatePopupClose()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PoliceCatchTrigger] Must be in Play Mode.");
            return;
        }
        HandleRadarPopupClosed();
    }

    [ContextMenu("TEST: Log State")]
    private void DebugLogState()
    {
        PopularityStage stage = PopularityStage.Stage1;
        float pop = 0f;
        if (PopularityManager.Instance != null)
        {
            stage = PopularityManager.Instance.GetCurrentStage();
            pop = PopularityManager.Instance.Popularity01;
        }
        int threshold = GetThresholdForStage(stage);
        Debug.Log($"[PoliceCatchTrigger] State: popularity={Mathf.RoundToInt(pop * 100f)} " +
                  $"stage={stage} catches={_radarCatchCounter}/{threshold} " +
                  $"pending={_pendingPoliceCatch}");
    }
#endif
}
