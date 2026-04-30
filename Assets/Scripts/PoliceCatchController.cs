using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Police Chase minigame controller — Distance-based tap chase.
///
/// Flow: Idle → Enter → Chase → Success/Fail → Exit → Idle
///
/// During the chase the police car continuously closes distance on the player.
/// Every tap on the player car increases the escape distance.
///   • Win  = escape distance reaches escapeThreshold (player got away).
///   • Lose = distance drops to 0 or below (police caught up and crashed).
///
/// TapInputRaycaster routes car taps to OnChaseTap() during chase
/// (tap isolation via isPoliceChaseActive flag — no economy, no cards).
/// </summary>
public class PoliceCatchController : MonoBehaviour
{
    public static PoliceCatchController Instance { get; private set; }

    /// <summary>
    /// Fired the instant the Chase phase begins (state transitions from Enter to Chase).
    /// PoliceChaseFeedbackController subscribes to start audiovisual feedback systems.
    /// </summary>
    public static event System.Action OnChaseStarted;

    /// <summary>
    /// Fired on every valid player tap during the chase phase.
    /// PoliceChaseFeedbackController subscribes to trigger per-tap haptic / pulse feedback.
    /// </summary>
    public static event System.Action OnChaseTapFeedback;

    /// <summary>
    /// Fired when a police chase fully ends (both success and failure paths),
    /// right before the state returns to Idle.
    /// AmbientHeatManager and any other listeners subscribe to this.
    /// </summary>
    public static event System.Action OnChaseEnded;

    /// <summary>
    /// True if the most recent chase ended with the player escaping.
    /// Set before OnChaseEnded fires; listeners can read this to know the outcome.
    /// </summary>
    public bool WasLastChaseSuccess { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnChaseStarted = null;
        OnChaseTapFeedback = null;
        OnChaseEnded = null;
    }

    // ==================== SERIALIZED ====================

    [Header("Scene References")]
    [SerializeField] private Transform playerCar;
    [SerializeField] private Transform policeCar;

    [Header("Chase Z Settings")]
    [Tooltip("Maximum chase duration (seconds). Surviving = escape.")]
    [SerializeField] private float maxChaseDuration = 12f;
    [Tooltip("Fallback flat police advance speed used only when progressiveDifficultyCurve has no keys.\n" +
             "Normally overridden by the progressive difficulty curve below.")]
    [SerializeField] private float policeBaseAdvancePerSecond = 0.73f;
    [Tooltip("Police advance speed over normalized chase progress (0 = chase start, 1 = chase end).\n" +
             "Y axis = actual advance-speed value at that point in the chase.\n" +
             "Default: rises from 0.60 at start to 0.85 at end — police gets faster the longer it drags on.\n" +
             "If this curve has zero keys, policeBaseAdvancePerSecond is used as a flat fallback.")]
    [SerializeField]
    private AnimationCurve progressiveDifficultyCurve =
        AnimationCurve.EaseInOut(0f, 0.60f, 1f, 0.85f);

    [Header("Police Car Enter Animation")]
    [Tooltip("World position where PoliceCar starts (off-screen, drifting in).")]
    [SerializeField] private Vector3 policeEnterStartPos = new Vector3(-6f, 0f, -3.5f);
    [Tooltip("Y-rotation at enter start (sideways drift).")]
    [SerializeField] private float policeEnterStartRotY = 90f;
    [Tooltip("World position PoliceCar arrives at after drift-in.")]
    [SerializeField] private Vector3 policeEnterEndPos = new Vector3(0f, 0f, -1f);
    [Tooltip("Y-rotation at enter end (facing forward).")]
    [SerializeField] private float policeEnterEndRotY = 0f;
    [Tooltip("Duration of the drift-in animation.")]
    [SerializeField] private float driftInDuration = 0.8f;

    [Header("Player Car Enter Animation")]
    [Tooltip("World position player car starts at when chase begins.")]
    [SerializeField] private Vector3 playerChaseStartPos = new Vector3(0f, 0f, 0f);
    [Tooltip("World position player car moves to during the chase (forward).")]
    [SerializeField] private Vector3 playerChaseEndPos = new Vector3(0f, 0f, 5f);
    [Tooltip("Duration of the player car forward slide.")]
    [SerializeField] private float playerSlideDuration = 0.6f;

    [Header("Police Car Exit")]
    [Tooltip("World position police car retreats to on exit.")]
    [SerializeField] private Vector3 policeHiddenLocalPos = new Vector3(0f, 0f, -8f);
    [Tooltip("Duration of police car enter/exit movement.")]
    [SerializeField] private float moveDuration = 0.6f;

    [Header("Police Sway (during chase)")]
    [Tooltip("Horizontal sway amplitude during chase.")]
    [SerializeField] private float swayAmount = 0.15f;
    [Tooltip("Duration of one sway cycle (left-right).")]
    [SerializeField] private float swayDuration = 0.8f;

    [Header("Crash Settings")]
    [Tooltip("Police Z threshold that triggers a crash into the player car.")]
    [SerializeField] private float crashZ = 2.3f;
    [Tooltip("Maximum backwards distance the police car can be pushed. Caps the player's safety zone.")]
    [SerializeField] private float minPoliceZ = -4f;

    [Header("Tap-Rate Balance Model")]
    [Tooltip("Rolling time window (seconds) for computing the player's current taps-per-second (TPS).\n" +
             "Shorter = more reactive to bursts; longer = more averaged. Recommended: 1.2–2.0s.")]
    [SerializeField] private float tapRateWindowSeconds = 1.5f;
    [Tooltip("Below this TPS, the player generates zero resistance — rare/accidental taps have no effect.")]
    [SerializeField] private float minimumEffectiveTPS = 2.0f;
    [Tooltip("Reference TPS used for debug zone labeling only (no direct gameplay effect).\n" +
             "At this rate the player should barely survive the full duration under pressure.")]
    [SerializeField] private float targetTpsReference = 5.5f;
    [Tooltip("Above this TPS, player resistance is fully capped. Prevents trivial spam winning.")]
    [SerializeField] private float maxEffectiveTPS = 9.0f;
    [Tooltip("Maximum backwards resistance force per second when TPS >= maxEffectiveTPS.\n" +
             "Net retreat speed at cap TPS = maxResistancePerSecond - policeBaseAdvancePerSecond.\n" +
             "Recommended: policeBaseAdvancePerSecond + 0.08 to 0.12 for modest cap pushback.")]
    [SerializeField] private float maxResistancePerSecond = 0.82f;
    [Tooltip("Extra police advance speed when the player has completely stopped tapping (TPS ≈ 0).\n" +
             "Creates the \"if I stop for even a moment, police surges\" panic feeling.")]
    [SerializeField] private float noTapAccelerationBonus = 0.25f;
    [Tooltip("Immediate per-tap Z pushback applied on each tap for tactile feel.\n" +
             "This is a small feedback-only kick — the real chase balance is driven by TPS.\n" +
             "Keep between 0.03 and 0.08.")]
    [SerializeField] private float tapMicroKickZ = 0.05f;

    [Header("Debug — Tap-Rate (Play Mode Inspector)")]
    [Tooltip("Enable to update debug readout fields below in real-time during Play Mode.")]
    [SerializeField] private bool enableChaseDebugLogs = false;
    [SerializeField] private float _debugCurrentTPS = 0f;
    [SerializeField] private float _debugNormalizedTPS = 0f;
    [SerializeField] private float _debugNetVelocity = 0f;
    [SerializeField] private string _debugZoneLabel = "\u2014";

    [Header("Visual Smoothing")]
    [Tooltip("SmoothDamp time for the police car visual Z when retreating (after tap). Higher = smoother gap-opening feel.")]
    [SerializeField] private float retreatSmoothTime = 0.35f;
    [Tooltip("SmoothDamp time for the police car visual Z when approaching. Lower = more threatening.")]
    [SerializeField] private float approachSmoothTime = 0.12f;

    [Header("Roadblock Animation (Post-Catch)")]
    [Tooltip("Final world position the police car drifts to when blocking the player.")]
    [SerializeField] private Vector3 roadblockTargetPos = new Vector3(0f, 0f, 13f);
    [Tooltip("Final Y rotation of the police car (angled across the road).")]
    [SerializeField] private float roadblockTargetRotY = 45f;
    [Tooltip("Total duration of the full overtake + cut-in animation.")]
    [SerializeField] private float roadblockDriftDuration = 1.6f;
    [Tooltip("Duration over which the road and player animator slow to a stop.")]
    [SerializeField] private float roadblockSlowdownDuration = 1.5f;
    [Tooltip("How far the police car swings out on X during the side overtake (negative = left).")]
    [SerializeField] private float overtakeSideOffset = -2.8f;
    [Tooltip("Slight Y rotation while passing alongside (steering into the overtake lane).")]
    [SerializeField] private float overtakeSteerAngle = -12f;

    [Header("Caught Reset (Black Screen + Resume)")]
    [Tooltip("CanvasGroup on a full-screen black Image used for the caught fade transition.\n" +
             "Create: UI > Image, color black, stretch to fill, add CanvasGroup, start alpha=0 & disabled.")]
    [SerializeField] private CanvasGroup blackScreenOverlay;
    [Tooltip("Duration of the fade-to-black transition.")]
    [SerializeField] private float fadeToBlackDuration = 0.5f;
    [Tooltip("How long the screen stays fully black before fading back.")]
    [SerializeField] private float blackHoldDuration = 0.6f;
    [Tooltip("Duration of the fade-from-black transition.")]
    [SerializeField] private float fadeFromBlackDuration = 0.6f;
    [Tooltip("Z position CarRoot starts at after the black screen (behind normal pos).")]
    [SerializeField] private float caughtResumeStartZ = -11f;
    [Tooltip("Z position CarRoot slides to (normal gameplay position).")]
    [SerializeField] private float caughtResumeEndZ = 0f;
    [Tooltip("Duration of the CarRoot slide-in after the black screen lifts.")]
    [SerializeField] private float caughtResumeSlideDuration = 1.2f;

    [Header("Reward (Success)")]
    [Tooltip("Base number of nitro coins spawned on success. Actual scales with popularity stage.")]
    [SerializeField] private int rewardCoinCount = 10;
    [Tooltip("Interval between reward coin spawns (seconds).")]
    [SerializeField] private float rewardCoinInterval = 0.12f;
    [SerializeField] private NitroCoinSpawner rewardSpawner;

    /// <summary>
    /// Returns nitro coin reward count scaled by popularity stage.
    ///   Stage1: 3   Stage2: 5   Stage3: 8
    ///   Stage4: 12  Stage5: 18  Stage6: 25
    /// </summary>
    private int GetStageScaledRewardCoins()
    {
        if (PopularityManager.Instance == null) return rewardCoinCount;
        PopularityStage stage = PopularityManager.Instance.GetCurrentStage();
        switch (stage)
        {
            case PopularityStage.Stage1: return 5;
            case PopularityStage.Stage2: return 8;
            case PopularityStage.Stage3: return 12;
            case PopularityStage.Stage4: return 18;
            case PopularityStage.Stage5: return 25;
            case PopularityStage.Stage6: return 35;
            default: return rewardCoinCount;
        }
    }

    [Header("Penalty (Fail)")]
    [Tooltip("Base money multiplier on fail (0.75 = lose 25%). Actual value scales by popularity stage.")]
    [SerializeField] private float failMoneyMultiplier = 0.75f;

    [Header("Penalty (Fail) — Popularity Gain")]
    [Tooltip("Normalized popularity increase when the player is caught (FAIL path only).\n" +
             "6 elements: index 0 = Stage1 … index 5 = Stage6.\n" +
             "Applied via PopularityManager.AddPopularityNormalized('PoliceCatch.Fail').\n" +
             "Set element to 0 to disable the increase for a specific stage.")]
    [SerializeField]
    private float[] failPopularityGainPerStage = new float[]
    {
        0.02f, // Stage1 — safest, smallest consequence
        0.03f, // Stage2
        0.04f, // Stage3
        0.05f, // Stage4
        0.06f, // Stage5
        0.08f  // Stage6 — most dangerous, largest consequence
    };

    /// <summary>
    /// Returns a stage-scaled fail money multiplier.
    ///   Stage1: 0.90 (lose 10%)   Stage2: 0.85 (lose 15%)
    ///   Stage3: 0.80 (lose 20%)   Stage4: 0.72 (lose 28%)
    ///   Stage5: 0.60 (lose 40%)   Stage6: 0.50 (lose 50%)
    /// </summary>
    private float GetStageScaledPenalty()
    {
        if (PopularityManager.Instance == null) return failMoneyMultiplier;
        PopularityStage stage = PopularityManager.Instance.GetCurrentStage();
        switch (stage)
        {
            case PopularityStage.Stage1: return 0.90f;
            case PopularityStage.Stage2: return 0.85f;
            case PopularityStage.Stage3: return 0.80f;
            case PopularityStage.Stage4: return 0.72f;
            case PopularityStage.Stage5: return 0.72f;
            case PopularityStage.Stage6: return 0.70f;
            default: return failMoneyMultiplier;
        }
    }

    /// <summary>
    /// Returns the normalized popularity gain applied on police-catch FAIL for the given stage.
    /// Stage1 → index 0 … Stage6 → index 5.
    /// Returns 0 if the array is unset or too short.
    /// </summary>
    private float GetFailPopularityGain(PopularityStage stage)
    {
        if (failPopularityGainPerStage == null || failPopularityGainPerStage.Length == 0)
            return 0f;
        int idx = Mathf.Clamp((int)stage - 1, 0, failPopularityGainPerStage.Length - 1);
        return failPopularityGainPerStage[idx];
    }

    // ==================== RUNTIME STATE ====================

    /// <summary>True while the minigame is active (any state except Idle).</summary>
    public bool IsChaseActive => _state != ChaseState.Idle;

    private enum ChaseState { Idle, Enter, Chase, Success, Fail, Exit }
    private ChaseState _state = ChaseState.Idle;

    // Chase Z state
    private float _policeChaseZ;      // logical target Z (responds instantly to taps + approach)
    private float _policeVisualZ;     // smoothed Z applied to the police car transform
    private float _policeZVelocity;   // SmoothDamp velocity
    private float _chaseTimer;

    // Tap-rate tracking: rolling window of tap timestamps for TPS computation
    private readonly List<float> _tapTimestamps = new List<float>(64);

    // ── Public read-only danger / progress properties ──
    // PoliceChaseFeedbackController reads these every frame to drive audiovisual intensity.

    /// <summary>
    /// Normalized fraction of how close the police car is to causing a crash.
    /// 0 = police at starting distance (safe).  1 = police at crashZ (crash imminent).
    /// Only meaningful during the Chase state; always 0 otherwise.
    /// </summary>
    public float DangerFraction => (_state == ChaseState.Chase && crashZ > 0f)
        ? Mathf.Clamp01(_policeChaseZ / crashZ)
        : 0f;

    /// <summary>
    /// Normalized time progress through the current chase (0 = just started, 1 = full duration elapsed).
    /// Only meaningful during the Chase state; always 0 otherwise.
    /// </summary>
    public float ChaseProgress => (_state == ChaseState.Chase && maxChaseDuration > 0f)
        ? Mathf.Clamp01(_chaseTimer / maxChaseDuration)
        : 0f;

    // Road speed restore after catch
    private float _savedSpeedMultiplier = 1f;
    private float _savedAnimatorSpeed = 1f;

    // Car original positions (for restore)
    private Vector3 _playerCarOriginalLocal;
    private Quaternion _playerCarOriginalRot;
    private Vector3 _policeCarOriginalLocal;
    private Quaternion _policeCarOriginalRot;

    // Car model ("Car"-tagged child) cached transform — used to restore
    // after killing child tweens so MagnetAnchor stays aligned.
    private Transform _carModelTransform;
    private Vector3 _carModelOriginalLocal;
    private Quaternion _carModelOriginalRot;

    // Reference to TapInputRaycaster for chase flag
    private TapInputRaycaster _tapInput;

    // Scene-loaded subscription guard
    private bool _sceneLoadedSubscribed = false;



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
        SubscribeSceneLoaded();
    }

    private void Start()
    {
        _tapInput = FindFirstObjectByType<TapInputRaycaster>();
        if (_tapInput == null)
            Debug.LogWarning("[PoliceCatch] TapInputRaycaster not found in scene.");

        // Hide police car at start
        if (policeCar != null)
        {
            policeCar.localPosition = policeHiddenLocalPos;
            policeCar.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        UnsubscribeSceneLoaded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnsubscribeSceneLoaded();

        if (_tapInput != null)
            _tapInput.isPoliceChaseActive = false;

        StopSway();
        if (playerCar != null) playerCar.DOKill();
        if (policeCar != null) policeCar.DOKill();
    }

    // ==================== SCENE LOAD ====================

    private void SubscribeSceneLoaded()
    {
        if (!_sceneLoadedSubscribed)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneLoadedSubscribed = true;
        }
    }

    private void UnsubscribeSceneLoaded()
    {
        if (_sceneLoadedSubscribed)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _sceneLoadedSubscribed = false;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[PoliceCatch] OnSceneLoaded({scene.name}) — state={_state}");

        if (scene.name == "Main")
            RebindSceneReferences();
    }

    private void RebindSceneReferences()
    {
        // --- TapInputRaycaster ---
        if (_tapInput == null)
        {
            _tapInput = FindFirstObjectByType<TapInputRaycaster>();
            if (_tapInput != null)
                Debug.Log("[PoliceCatch] RebindSceneReferences: TapInputRaycaster re-acquired.");
        }

        // --- playerCar ---
        if (playerCar == null)
        {
            GameObject carObj = GameObject.FindWithTag("Car");
            if (carObj != null)
            {
                playerCar = carObj.transform;
                Debug.Log("[PoliceCatch] RebindSceneReferences: playerCar re-acquired via tag.");
            }
        }

        // --- policeCar ---
        if (policeCar == null)
        {
            GameObject policeObj = GameObject.Find("PoliceCar");
            if (policeObj != null)
            {
                policeCar = policeObj.transform;
                policeCar.localPosition = policeHiddenLocalPos;
                policeCar.gameObject.SetActive(false);
                Debug.Log("[PoliceCatch] RebindSceneReferences: policeCar re-acquired by name.");
            }
        }

        // Ensure tap isolation is off when idle
        if (_state == ChaseState.Idle && _tapInput != null)
            _tapInput.isPoliceChaseActive = false;
    }

    private void Update()
    {
        if (_state != ChaseState.Chase) return;

        // Tutorial freeze: pause chase progression while a tutorial popup is on-screen.
        if (TutorialGate.GameplayFrozen) return;

        float dt = Time.deltaTime;
        _chaseTimer += dt;

        // ── TPS CALCULATION ──
        // Prune tap timestamps that have fallen outside the rolling window.
        float windowStart = Time.time - tapRateWindowSeconds;
        while (_tapTimestamps.Count > 0 && _tapTimestamps[0] < windowStart)
            _tapTimestamps.RemoveAt(0);

        // Current taps-per-second: counts taps inside the rolling window.
        float currentTPS = _tapTimestamps.Count / tapRateWindowSeconds;

        // ── NET POLICE VELOCITY ──
        // Normalize TPS to [0..1] across the effective difficulty range.
        //   0 = at or below minimumEffectiveTPS (no resistance)
        //   1 = at or above maxEffectiveTPS (full resistance, capped)
        float normalizedTPS = Mathf.InverseLerp(minimumEffectiveTPS, maxEffectiveTPS, currentTPS);

        // Player resistance: how much the player's tapping pushes back against police advance.
        float playerResistance = normalizedTPS * maxResistancePerSecond;

        // No-tap panic bonus: extra police speed when the player stops tapping entirely.
        // Triggers instantly when TPS drops near zero, creating a "don't stop!" urgency.
        float noTapBonus = (currentTPS < 0.1f) ? noTapAccelerationBonus : 0f;

        // Net velocity: positive = police advancing toward player, negative = retreating.
        //   Low TPS  → large positive net (police gains fast)
        //   ~target TPS → small positive net (tense, barely stable)
        //   High TPS → negative net (player pushing police back)
        //
        // Progressive difficulty: evaluate the curve at current chase progress so the
        // police gets systematically faster the longer the chase drags on.
        // Falls back to flat policeBaseAdvancePerSecond if the curve has no keys.
        float baseAdvance = (progressiveDifficultyCurve != null && progressiveDifficultyCurve.length > 0)
            ? progressiveDifficultyCurve.Evaluate(Mathf.Clamp01(_chaseTimer / maxChaseDuration))
            : policeBaseAdvancePerSecond;
        float netVelocity = baseAdvance + noTapBonus - playerResistance;

        // Apply net velocity to logical chase Z.
        _policeChaseZ += netVelocity * dt;

        // Clamp: player cannot push police further back than minPoliceZ.
        _policeChaseZ = Mathf.Max(_policeChaseZ, minPoliceZ);

        // ── VISUAL SMOOTHING ──
        // Approach faster (threatening), retreat slower (satisfying tap-back feel).
        float smoothTime = (netVelocity > 0f) ? approachSmoothTime : retreatSmoothTime;
        _policeVisualZ = Mathf.SmoothDamp(_policeVisualZ, _policeChaseZ, ref _policeZVelocity, smoothTime);

        if (policeCar != null)
        {
            Vector3 pos = policeCar.localPosition;
            pos.z = _policeVisualZ;
            policeCar.localPosition = pos;
        }

        // ── DEBUG READOUT ──
        if (enableChaseDebugLogs)
        {
            _debugCurrentTPS = currentTPS;
            _debugNormalizedTPS = normalizedTPS;
            _debugNetVelocity = netVelocity;

            if (currentTPS < minimumEffectiveTPS)
                _debugZoneLabel = "DANGER (below min TPS)";
            else if (currentTPS < targetTpsReference)
                _debugZoneLabel = "LOW (losing ground)";
            else if (currentTPS < maxEffectiveTPS * 0.78f)
                _debugZoneLabel = "TARGET (barely surviving)";
            else
                _debugZoneLabel = "HIGH (pushing back)";
        }

        // ── WIN/LOSE CHECKS ──
        // Crash check: use logical Z (not visual) for precise gameplay timing.
        if (_policeChaseZ >= crashZ)
        {
            _state = ChaseState.Fail;
            return;
        }

        // Escape check: survived the full chase duration.
        if (_chaseTimer >= maxChaseDuration)
        {
            _state = ChaseState.Success;
            return;
        }
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Called by TapInputRaycaster when the player taps the car during police chase.
    ///
    /// Does two things:
    ///   1. Records the tap timestamp into the rolling window for TPS calculation.
    ///      This is what actually drives the chase balance (Update uses TPS to compute net velocity).
    ///   2. Applies a small immediate micro-kick for tactile feel (tapMicroKickZ).
    ///      This is intentionally tiny — the real balance is TPS-driven, not tap-counted.
    ///
    /// Isolated from economy (no money, no cards).
    /// </summary>
    public void OnChaseTap()
    {
        if (_state != ChaseState.Chase) return;

        // Record tap for rolling TPS calculation.
        _tapTimestamps.Add(Time.time);

        // Notify feedback controller for haptic / pulse feedback on each valid chase tap.
        OnChaseTapFeedback?.Invoke();

        // Micro-kick: small immediate pushback for tactile feedback.
        // Real chase balance is TPS-driven in Update; this is purely a responsive feel cue.
        if (tapMicroKickZ > 0f)
        {
            _policeChaseZ -= tapMicroKickZ;
            _policeChaseZ = Mathf.Max(_policeChaseZ, minPoliceZ);
        }
    }

    /// <summary>
    /// Start a police chase. Called by PoliceCatchTrigger.
    /// </summary>
    public void StartChase()
    {
        if (_state != ChaseState.Idle)
        {
            Debug.LogWarning("[PoliceCatch] Chase already in progress.");
            return;
        }

        StartCoroutine(ChaseSequence());
    }

    // ==================== CHASE SEQUENCE ====================

    private IEnumerator ChaseSequence()
    {
        // ── ENTER ──
        _state = ChaseState.Enter;
        _chaseTimer = 0f;

        // Enable tap isolation
        if (_tapInput != null)
            _tapInput.isPoliceChaseActive = true;

        // Save car positions for restore
        if (playerCar != null)
        {
            _playerCarOriginalLocal = playerCar.localPosition;
            _playerCarOriginalRot = playerCar.localRotation;
        }
        if (policeCar != null)
        {
            _policeCarOriginalLocal = policeCar.localPosition;
            _policeCarOriginalRot = policeCar.localRotation;
            policeCar.gameObject.SetActive(true);
        }

        // Cache car model's local transform BEFORE any tweens are killed,
        // so we can restore it accurately (no hardcoded zero/identity).
        Debug.Log($"[PoliceChaseDebug] PRE-CacheCarModel: playerCar.local={playerCar?.localPosition} carModel(tag)={GameObject.FindGameObjectWithTag("Car")?.transform.localPosition}");
        CacheCarModelTransform();
        Debug.Log($"[PoliceChaseDebug] POST-CacheCarModel: cached=({_carModelOriginalLocal}) carModelRef={((_carModelTransform != null) ? _carModelTransform.name : "NULL")} actualLocal={_carModelTransform?.localPosition}");

        // Animate police drift-in + player car forward slide
        Debug.Log("[PoliceChaseDebug] >>> AnimateEnter START");
        yield return StartCoroutine(AnimateEnter());
        Debug.Log($"[PoliceChaseDebug] <<< AnimateEnter END: playerCar.local={playerCar?.localPosition} carModel.local={_carModelTransform?.localPosition}");

        // Initialize chase Z from drift-end position
        _policeChaseZ = policeCar != null ? policeCar.localPosition.z : -1f;
        _policeVisualZ = _policeChaseZ;
        _policeZVelocity = 0f;

        // Reset tap-rate tracking so previous session's taps don't bleed into this chase.
        _tapTimestamps.Clear();
        _debugCurrentTPS = 0f;
        _debugNormalizedTPS = 0f;
        _debugNetVelocity = 0f;
        _debugZoneLabel = "\u2014";

        // Hide top bar
        if (TopBarAnimator.Instance != null)
            TopBarAnimator.Instance.HideAnimated();

        // ── CHASE (distance-based tap loop) ──
        // Notify PoliceChaseFeedbackController (and any other listener) that the chase
        // phase is about to begin so audiovisual feedback can start immediately.
        OnChaseStarted?.Invoke();
        _state = ChaseState.Chase;

        // Wait until Update() sets Success or Fail
        while (_state == ChaseState.Chase)
            yield return null;

        bool isFail = (_state == ChaseState.Fail);
        bool isSuccess = (_state == ChaseState.Success);
        WasLastChaseSuccess = isSuccess;
        double penaltyBefore = 0;
        double penaltyAfter = 0;

        // Play roadblock animation if police caught the player
        if (isFail)
            yield return StartCoroutine(AnimateRoadblock());

        // ── RESULT ──
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.suppressTopBarMoneyUpdates = true;
            CurrencyManager.Instance.bufferedEarnings = 0;
        }

        if (isSuccess)
        {
            Debug.Log("[PoliceCatch] SUCCESS — player escaped!");
            yield return new WaitForSeconds(0.5f);
        }
        else if (isFail)
        {
            Debug.Log("[PoliceCatch] FAIL — caught by police!");

            // Apply popularity gain on fail — being caught raises public heat / wanted level.
            // Stage is read at the MOMENT of failure (before any further state change).
            // Applied once via the existing PopularityManager API; does not affect money penalty.
            if (PopularityManager.Instance != null)
            {
                PopularityStage failStage = PopularityManager.Instance.GetCurrentStage();
                float popularityGain = GetFailPopularityGain(failStage);
                if (popularityGain > 0f)
                {
                    PopularityManager.Instance.AddPopularityNormalized(popularityGain, "PoliceCatch.Fail");
                    Debug.Log($"[PoliceCatch] FAIL popularity gain: +{popularityGain:F3} normalized (stage={failStage})");
                }
            }

            if (CurrencyManager.Instance != null)
            {
                var cm = CurrencyManager.Instance;
                penaltyBefore = cm.money;
                float stagePenalty = GetStageScaledPenalty();
                cm.money = penaltyBefore * stagePenalty;
                penaltyAfter = cm.money;
                Debug.Log($"[PoliceCatch] FAIL penalty applied: before={penaltyBefore:F0} after={penaltyAfter:F0} stagePenalty={stagePenalty}");
            }
            yield return new WaitForSeconds(0.5f);
        }

        // ── EXIT ──
        _state = ChaseState.Exit;

        if (isFail)
        {
            // Caught path: black screen → silent reset → resume slide-in
            yield return StartCoroutine(AnimateCaughtReset());
        }
        else
        {
            // Success / normal path: original exit animation
            yield return StartCoroutine(AnimateExit());
        }

        // Disable police car after exit
        if (policeCar != null)
            policeCar.gameObject.SetActive(false);

        // Disable tap isolation
        if (_tapInput != null)
            _tapInput.isPoliceChaseActive = false;

        // Show TopBar
        Debug.Log("[PoliceCatch] Showing TopBar after exit");
        if (TopBarAnimator.Instance != null)
            TopBarAnimator.Instance.ShowAnimated();

        yield return new WaitForSeconds(0.35f);

        // ── Post-exit money feedback ──
        if (isFail && CurrencyManager.Instance != null)
        {
            var cm = CurrencyManager.Instance;

            bool penaltyAnimDone = false;
            if (CurrencyUI.Instance != null)
            {
                CurrencyUI.Instance.PlayPenaltyAnimation(penaltyBefore, penaltyAfter, 1f, () =>
                {
                    penaltyAnimDone = true;
                });
            }
            else
            {
                penaltyAnimDone = true;
            }
            while (!penaltyAnimDone)
                yield return null;

            double buffered = cm.bufferedEarnings;
            Debug.Log($"[PoliceCatch] FAIL before={penaltyBefore:F0} afterPenalty={penaltyAfter:F0} buffered={buffered:F0}");

            // Always call PlayBufferedEarningsApplyAnimation when CurrencyUI is available,
            // even when buffered == 0. Its internal skip-path is the ONLY code that restores
            // textMPSLine/textMPTLine alpha (set to 0 by PlayPenaltyAnimation) and clears
            // _isPenaltyAnimating. Skipping it leaves the UI permanently stuck.
            if (CurrencyUI.Instance != null)
            {
                bool buffAnimDone = false;
                CurrencyUI.Instance.PlayBufferedEarningsApplyAnimation(penaltyAfter, buffered, () =>
                {
                    cm.CommitBufferedEarnings();
                    buffAnimDone = true;
                });
                while (!buffAnimDone)
                    yield return null;
            }
            else
            {
                cm.CommitBufferedEarnings();
            }

            Debug.Log($"[PoliceCatch] FAIL complete, suppress OFF, money now={cm.money:F0}");
        }
        else if (isSuccess && CurrencyManager.Instance != null)
        {
            var cm = CurrencyManager.Instance;
            double before = cm.money;

            int stageRewardCoins = GetStageScaledRewardCoins();
            cm.AddNitroCoins(stageRewardCoins);
            Debug.Log($"[PoliceCatch] SUCCESS NitroCoins reward: +{stageRewardCoins}");

            double reward = System.Math.Floor(before / 6.0);
            cm.money += reward;
            cm.totalMoneyEarned += reward;
            double after = cm.money;

            bool rewardAnimDone = false;
            if (CurrencyUI.Instance != null)
            {
                CurrencyUI.Instance.PlaySuccessRewardAnimation(before, after, 0.6f, () =>
                {
                    rewardAnimDone = true;
                });
            }
            else
            {
                rewardAnimDone = true;
            }
            while (!rewardAnimDone)
                yield return null;

            double buffered = cm.bufferedEarnings;
            Debug.Log($"[PoliceCatch] SUCCESS reward={reward:F0} before={before:F0} afterReward={after:F0} buffered={buffered:F0}");

            // Always call PlayBufferedEarningsApplyAnimation when CurrencyUI is available,
            // even when buffered == 0. Its internal skip-path is the ONLY code that restores
            // textMPSLine/textMPTLine alpha (set to 0 by PlaySuccessRewardAnimation) and clears
            // _isPenaltyAnimating. Skipping it leaves the UI permanently stuck.
            if (CurrencyUI.Instance != null)
            {
                bool buffAnimDone = false;
                CurrencyUI.Instance.PlayBufferedEarningsApplyAnimation(after, buffered, () =>
                {
                    cm.CommitBufferedEarnings();
                    buffAnimDone = true;
                });
                while (!buffAnimDone)
                    yield return null;
            }
            else
            {
                cm.CommitBufferedEarnings();
            }

            Debug.Log($"[PoliceCatch] SUCCESS complete, suppress OFF, money now={cm.money:F0}");
        }
        else
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.suppressTopBarMoneyUpdates = false;
                CurrencyManager.Instance.bufferedEarnings = 0;
            }
        }

        _state = ChaseState.Idle;
        Debug.Log("[PoliceCatch] Chase ended, returning to Idle.");

        // Re-enable car effects suspended during chase (boost shake, etc.)
        ResumeCarEffectsAfterChase();

        // Notify subscribers (e.g. AmbientHeatManager) that this chase has fully ended
        OnChaseEnded?.Invoke();
    }

    // ==================== ANIMATION (DOTween) ====================

    private IEnumerator AnimateEnter()
    {
        Debug.Log($"[PoliceChaseDebug] AnimateEnter: BEFORE DOKill — carModel.local={_carModelTransform?.localPosition} isTweening={(_carModelTransform != null ? DG.Tweening.DOTween.IsTweening(_carModelTransform).ToString() : "N/A")}");
        if (playerCar != null) playerCar.DOKill();
        if (policeCar != null) policeCar.DOKill();
        Debug.Log($"[PoliceChaseDebug] AnimateEnter: AFTER DOKill(playerCar) — carModel.local={_carModelTransform?.localPosition}");

        // Kill DOTween on all car children (boost cinematic shake, VFX tweens, etc.)
        // so they don't fight the chase slide or desync MagnetAnchor / Plasma Sphere.
        KillCarChildTweens();
        Debug.Log($"[PoliceChaseDebug] AnimateEnter: AFTER KillCarChildTweens — carModel.local={_carModelTransform?.localPosition}");

        Sequence seq = DOTween.Sequence();

        // ── Player car slides forward ──
        if (playerCar != null)
        {
            playerCar.localPosition = playerChaseStartPos;
            seq.Join(playerCar.DOLocalMove(playerChaseEndPos, playerSlideDuration)
                .SetEase(Ease.OutCubic));
        }

        // ── Police car drift-in ──
        if (policeCar != null)
        {
            policeCar.position = policeEnterStartPos;
            policeCar.rotation = Quaternion.Euler(0f, policeEnterStartRotY, 0f);

            // Position: arc-like drift from side to center
            seq.Join(policeCar.DOMove(policeEnterEndPos, driftInDuration)
                .SetEase(Ease.OutQuart));

            // Rotation: drift settle from sideways to forward
            seq.Join(policeCar.DORotate(new Vector3(0f, policeEnterEndRotY, 0f), driftInDuration)
                .SetEase(Ease.OutBack, 1.2f));
        }

        yield return seq.WaitForCompletion();

        // Start police sway during chase
        StartSway();
    }

    /// <summary>
    /// New post-catch animation: police overtakes and drifts in front of the player,
    /// blocking the road. Road and player animator gradually slow to a stop.
    /// </summary>
    private IEnumerator AnimateRoadblock()
    {
        StopSway();
        if (policeCar != null) policeCar.DOKill();
        if (playerCar != null) playerCar.DOKill();

        // ── 1. Save current speeds so we can restore later ──
        if (WorldScrollSpeed.Instance != null)
            _savedSpeedMultiplier = WorldScrollSpeed.Instance.SpeedMultiplier;

        Animator playerAnimator = playerCar != null ? playerCar.GetComponentInChildren<Animator>() : null;
        if (playerAnimator != null)
            _savedAnimatorSpeed = playerAnimator.speed;

        // ── 2. Police overtake: smooth curved path from behind → wide → in front ──
        //    Uses DOPath (CatmullRom) for one continuous spline so there are
        //    no hard kinks between waypoints. Rotation is driven by a separate
        //    smooth tween that blends steer-out → straight → block-angle.
        if (policeCar != null)
        {
            Vector3 startPos = policeCar.localPosition;

            // Build a smooth spline through key waypoints
            Vector3 pullOutPos = new Vector3(
                overtakeSideOffset,
                startPos.y,
                Mathf.Lerp(startPos.z, roadblockTargetPos.z, 0.22f));

            Vector3 alongsidePos = new Vector3(
                overtakeSideOffset * 0.85f,
                startPos.y,
                Mathf.Lerp(startPos.z, roadblockTargetPos.z, 0.55f));

            Vector3 aheadPos = new Vector3(
                overtakeSideOffset * 0.4f,
                roadblockTargetPos.y,
                roadblockTargetPos.z - 1.2f);

            Vector3[] path = new Vector3[]
            {
                pullOutPos,
                alongsidePos,
                aheadPos,
                roadblockTargetPos
            };

            // One smooth spline for the entire overtake path
            policeCar.DOLocalPath(path, roadblockDriftDuration, PathType.CatmullRom)
                .SetEase(Ease.InOutSine);

            // Smooth rotation: steer-out → straighten → settle into block angle
            // Uses a sequence but with long, overlapping-feel durations and soft eases
            float totalDur = roadblockDriftDuration;
            Sequence rotSeq = DOTween.Sequence();

            // Steer into overtake lane (first 30%)
            rotSeq.Append(policeCar.DOLocalRotate(
                new Vector3(0f, overtakeSteerAngle, 0f), totalDur * 0.30f)
                .SetEase(Ease.InOutSine));

            // Straighten while surging past (next 35%)
            rotSeq.Append(policeCar.DOLocalRotate(
                new Vector3(0f, 0f, 0f), totalDur * 0.35f)
                .SetEase(Ease.InOutSine));

            // Ease into the final blocking angle (last 35%)
            rotSeq.Append(policeCar.DOLocalRotate(
                new Vector3(0f, roadblockTargetRotY, 0f), totalDur * 0.35f)
                .SetEase(Ease.InOutQuad));

            // Runs in parallel with the slowdown loop below — do NOT yield here
        }

        // ── 3. Gradually slow road + player animator over the slowdown duration ──
        float elapsed = 0f;
        float startMultiplier = _savedSpeedMultiplier;
        float startAnimSpeed = playerAnimator != null ? _savedAnimatorSpeed : 1f;

        while (elapsed < roadblockSlowdownDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / roadblockSlowdownDuration);
            // Smooth deceleration curve
            float factor = 1f - Mathf.SmoothStep(0f, 1f, t);

            if (WorldScrollSpeed.Instance != null)
                WorldScrollSpeed.Instance.SpeedMultiplier = startMultiplier * factor;

            if (playerAnimator != null)
                playerAnimator.speed = startAnimSpeed * factor;

            yield return null;
        }

        // Ensure fully stopped
        if (WorldScrollSpeed.Instance != null)
            WorldScrollSpeed.Instance.SpeedMultiplier = 0f;
        if (playerAnimator != null)
            playerAnimator.speed = 0f;

        // ── 4. Hold the roadblock pose briefly ──
        yield return new WaitForSeconds(0.6f);
    }

    /// <summary>
    /// Caught-reset sequence: fade to black → silently reset all positions/speeds →
    /// fade from black → CarRoot slides smoothly from Z=-11 to Z=0 (resume feel).
    /// </summary>
    private IEnumerator AnimateCaughtReset()
    {
        StopSway();
        if (playerCar != null) playerCar.DOKill();
        if (policeCar != null) policeCar.DOKill();

        // ── 1. Fade to black ──
        if (blackScreenOverlay != null)
        {
            blackScreenOverlay.gameObject.SetActive(true);
            blackScreenOverlay.alpha = 0f;
            yield return blackScreenOverlay.DOFade(1f, fadeToBlackDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .WaitForCompletion();
        }

        // ── 2. While fully black: silently reset everything ──

        // Restore road speed
        if (WorldScrollSpeed.Instance != null)
            WorldScrollSpeed.Instance.SpeedMultiplier = _savedSpeedMultiplier;

        // Restore player animator speed
        Animator playerAnimator = playerCar != null ? playerCar.GetComponentInChildren<Animator>() : null;
        if (playerAnimator != null)
            playerAnimator.speed = _savedAnimatorSpeed;

        // Hide police car
        if (policeCar != null)
        {
            policeCar.localPosition = policeHiddenLocalPos;
            policeCar.localRotation = Quaternion.identity;
            policeCar.gameObject.SetActive(false);
        }

        // Place CarRoot (playerCar) at the resume-start Z
        if (playerCar != null)
        {
            Vector3 resetPos = _playerCarOriginalLocal;
            resetPos.z = caughtResumeStartZ;
            playerCar.localPosition = resetPos;
            playerCar.localRotation = _playerCarOriginalRot;
        }

        // Hold black
        yield return new WaitForSeconds(blackHoldDuration);

        // ── 3. Fade from black ──
        if (blackScreenOverlay != null)
        {
            yield return blackScreenOverlay.DOFade(0f, fadeFromBlackDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .WaitForCompletion();
            blackScreenOverlay.gameObject.SetActive(false);
        }

        // ── 4. CarRoot slides smoothly from Z=-11 to Z=0 (resume feel) ──
        if (playerCar != null)
        {
            Vector3 targetPos = _playerCarOriginalLocal;
            targetPos.z = caughtResumeEndZ;
            yield return playerCar.DOLocalMoveZ(targetPos.z, caughtResumeSlideDuration)
                .SetEase(Ease.OutSine)
                .WaitForCompletion();

            // Snap to exact original position
            playerCar.localPosition = _playerCarOriginalLocal;
            playerCar.localRotation = _playerCarOriginalRot;
        }

        Debug.Log("[PoliceCatch] Caught reset complete — resumed at original position.");
    }

    private IEnumerator AnimateExit()
    {
        StopSway();
        if (playerCar != null) playerCar.DOKill();
        if (policeCar != null) policeCar.DOKill();

        // Restore road movement if it was frozen during roadblock
        if (WorldScrollSpeed.Instance != null && WorldScrollSpeed.Instance.SpeedMultiplier == 0f)
            WorldScrollSpeed.Instance.SpeedMultiplier = _savedSpeedMultiplier;

        // Restore player animator speed if it was slowed/stopped
        Animator playerAnimator = playerCar != null ? playerCar.GetComponentInChildren<Animator>() : null;
        if (playerAnimator != null && playerAnimator.speed < _savedAnimatorSpeed)
            playerAnimator.speed = _savedAnimatorSpeed;

        Sequence seq = DOTween.Sequence();

        // Police car retreats
        if (policeCar != null)
        {
            seq.Append(policeCar.DOLocalMove(policeHiddenLocalPos, moveDuration).SetEase(Ease.InCubic));
            seq.Join(policeCar.DOLocalRotate(Vector3.zero, moveDuration * 0.5f).SetEase(Ease.InSine));
        }

        // Player car returns to original position
        if (playerCar != null)
        {
            seq.Append(playerCar.DOLocalMove(_playerCarOriginalLocal, playerSlideDuration)
                .SetEase(Ease.InOutSine));
            seq.Join(playerCar.DOLocalRotateQuaternion(_playerCarOriginalRot, playerSlideDuration * 0.5f)
                .SetEase(Ease.InSine));
        }

        yield return seq.WaitForCompletion();
    }

    /// <summary>
    /// Caches the "Car"-tagged child model's local transform so we can
    /// restore it precisely after killing child tweens. Must be called
    /// BEFORE SuspendCarShake / DOKill on children.
    /// </summary>
    private void CacheCarModelTransform()
    {
        _carModelTransform = null;
        if (playerCar == null) return;

        GameObject carModel = GameObject.FindGameObjectWithTag("Car");
        if (carModel != null && carModel.transform.parent == playerCar)
        {
            _carModelTransform = carModel.transform;
            _carModelOriginalLocal = _carModelTransform.localPosition;
            _carModelOriginalRot = _carModelTransform.localRotation;
        }
    }

    /// <summary>
    /// Kills all DOTween tweens running on descendants of playerCar
    /// (e.g., BoostModeCinematicController shake on the car model,
    /// NitroMagnet VFX DOScale on Plasma Sphere, etc.).
    /// Restores the car model to its cached original local transform
    /// so MagnetAnchor and Plasma Sphere stay perfectly aligned with CarRoot.
    /// </summary>
    private void KillCarChildTweens()
    {
        if (playerCar == null) return;

        Debug.Log($"[PoliceChaseDebug] KillCarChildTweens: BEFORE SuspendCarShake — carModel.local={_carModelTransform?.localPosition}");

        // Suspend boost cinematic car shake cleanly (restores its own cached original)
        if (BoostModeCinematicController.Instance != null)
            BoostModeCinematicController.Instance.SuspendCarShake();

        Debug.Log($"[PoliceChaseDebug] KillCarChildTweens: AFTER SuspendCarShake — carModel.local={_carModelTransform?.localPosition}");

        // Kill any remaining DOTween on all descendants (covers VFX, scale tweens, etc.)
        int killedCount = 0;
        foreach (var t in playerCar.GetComponentsInChildren<Transform>(true))
        {
            if (t == playerCar) continue;
            if (DG.Tweening.DOTween.IsTweening(t))
            {
                Debug.Log($"[PoliceChaseDebug] KillCarChildTweens: killing tween on '{t.name}' (path={GetDebugPath(t)})");
                killedCount++;
            }
            t.DOKill();
        }
        Debug.Log($"[PoliceChaseDebug] KillCarChildTweens: killed {killedCount} child tweens");
        Debug.Log($"[PoliceChaseDebug] KillCarChildTweens: AFTER child DOKill — carModel.local={_carModelTransform?.localPosition}");

        // Restore car model to its cached pre-tween local transform
        if (_carModelTransform != null)
        {
            Debug.Log($"[PoliceChaseDebug] KillCarChildTweens: RESTORING carModel from {_carModelTransform.localPosition} to cached {_carModelOriginalLocal}");
            _carModelTransform.localPosition = _carModelOriginalLocal;
            _carModelTransform.localRotation = _carModelOriginalRot;
            Debug.Log($"[PoliceChaseDebug] KillCarChildTweens: AFTER restore — carModel.local={_carModelTransform.localPosition}");
        }

    }

    private static string GetDebugPath(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        int depth = 0;
        while (p != null && depth < 8)
        {
            path = p.name + "/" + path;
            p = p.parent;
            depth++;
        }
        return path;
    }

    /// <summary>
    /// Re-enables car effects (e.g., boost shake) that were suspended
    /// when the chase started. Called after the chase fully ends.
    /// </summary>
    private void ResumeCarEffectsAfterChase()
    {
        if (BoostModeCinematicController.Instance != null)
            BoostModeCinematicController.Instance.ResumeCarShakeIfActive();
    }

    private void StartSway()
    {
        if (policeCar == null || swayAmount <= 0f) return;

        float baseX = policeCar.localPosition.x;
        policeCar.DOLocalMoveX(baseX + swayAmount, swayDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetId("PoliceSway");
    }

    private void StopSway()
    {
        DOTween.Kill("PoliceSway");
    }

    // ==================== REWARD ====================

    private IEnumerator SpawnRewardCoins()
    {
        if (rewardSpawner == null || rewardSpawner.nitroCoinPrefab == null)
        {
            Debug.LogWarning("[PoliceCatch] rewardSpawner or its prefab is null — skipping reward.");
            yield break;
        }

        int coinsToSpawn = GetStageScaledRewardCoins();

        for (int i = 0; i < coinsToSpawn; i++)
        {
            float x = Random.Range(rewardSpawner.minX, rewardSpawner.maxX);
            Vector3 pos = new Vector3(x, rewardSpawner.spawnTop.position.y, rewardSpawner.spawnTop.position.z);

            GameObject obj = Object.Instantiate(rewardSpawner.nitroCoinPrefab, pos, Quaternion.identity);
            NitroCoin coin = obj.GetComponent<NitroCoin>();
            if (coin != null)
                coin.despawnZ = rewardSpawner.spawnBottom.position.z;

            yield return new WaitForSeconds(rewardCoinInterval);
        }
    }

    // ==================== EDITOR TEST ====================

#if UNITY_EDITOR
    [ContextMenu("TEST: Start Police Chase")]
    private void DebugStartChase()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PoliceCatch] Must be in Play Mode.");
            return;
        }
        StartChase();
    }
#endif
}
