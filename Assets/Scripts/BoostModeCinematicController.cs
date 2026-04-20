using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;

/// <summary>
/// BoostModeCinematicController - Premium cinematic boost animation using DOTween.
/// 
/// SETUP INSTRUCTIONS:
/// 1. Install DOTween from Asset Store or Package Manager (required dependency)
/// 2. Create an empty GameObject in your Main scene called "BoostModeCinematicController"
/// 3. Attach this script to it
/// 4. Ensure your car has the tag "Car" assigned
/// 5. (Optional) Adjust tunable parameters in Inspector for desired feel
/// 
/// WHAT IT DOES:
/// - When boost starts: Camera zooms in (fieldOfView), tilts, pushes forward, car shakes with power
/// - When boost ends: Everything smoothly returns to original state
/// - Creates a premium, racing-game turbo camera feel
/// - Works with PERSPECTIVE cameras (FOV-based zoom)
/// 
/// DEPENDENCIES:
/// - DOTween (DG.Tweening namespace)
/// - BoostModeController.Instance with OnBoostStarted/OnBoostEnded events
/// </summary>
public class BoostModeCinematicController : MonoBehaviour
{
    public static BoostModeCinematicController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    // ==================== CAMERA SETTINGS ====================
    [Header("Camera Zoom Settings (Perspective)")]
    [Tooltip("Target FOV during boost. LARGER = wider angle = feels faster. Default 75 for speed sensation.")]
    public float targetFOV = 75f;

    [Tooltip("Extra FOV added for the initial punch overshoot (settles back to targetFOV). Creates impact kick.")]
    public float fovPunchOvershoot = 8f;

    [Tooltip("Target camera X rotation (tilt) during boost. Higher = looks more downward = ground-hugging speed feel.")]
    public float targetTiltX = 47f;

    [Tooltip("How far camera pulls BACK (world Z-) during boost — shows more road ahead.")]
    public float cameraPushDistance = 1.5f;

    [Tooltip("Vertical camera offset during boost. NEGATIVE = lifts camera UP (recommended for centering car). Positive = dips down (pushes car above center).")]
    public float cameraDipY = -2.3f;

    [Tooltip("Time to slam camera into boost position (seconds). Shorter = more punch.")]
    public float cameraInTime = 0.20f;

    [Tooltip("Time to ease camera back to original position (seconds).")]
    public float cameraOutTime = 0.85f;

    // ==================== CAR SHAKE SETTINGS ====================
    [Header("Car Shake Settings")]
    [Tooltip("Position shake amplitude (subtle = 0.02-0.05)")]
    public float shakePositionStrength = 0.03f;

    [Tooltip("Rotation shake amplitude in degrees (subtle = 0.5-2.0)")]
    public float shakeRotationStrength = 1.2f;

    [Tooltip("Shake vibrato (higher = faster oscillation, 15-30 recommended)")]
    public int shakeVibrato = 20;

    [Tooltip("Shake randomness (0 = rhythmic, 90 = chaotic, 20-40 recommended)")]
    public float shakeRandomness = 30f;

    [Header("Debug")]
    public bool verboseLogs = true;

    // ==================== CACHED REFERENCES ====================
    private Camera _mainCamera;
    private Transform _cameraTransform;
    private GameObject _carObject;
    private Transform _carTransform;

    // ==================== ORIGINAL STATE STORAGE ====================
    private Vector3 _originalCameraLocalPos;
    private Quaternion _originalCameraLocalRot;
    private float _defaultFOV;
    private Vector3 _defaultLocalEuler;
    private bool _defaultsCached = false;
    private Vector3 _originalCarLocalPos;
    private Quaternion _originalCarLocalRot;

    // ==================== TWEEN REFERENCES ====================
    private Sequence _cameraInSequence;
    private Sequence _cameraOutSequence;
    private Tween _fovInTween;
    private Tween _fovOutTween;
    private Tween _tiltInTween;
    private Tween _tiltOutTween;
    private Tween _carShakePosTween;
    private Tween _carShakeRotTween;
    private Tween _carResetPosTween;
    private Tween _carResetRotTween;

    // Short-loop shake duration (keeps pre-computation cheap)
    private const float ShakeLoopDuration = 1f;

    // ==================== STATE TRACKING ====================
    private bool _subscribedToBoostController = false;
    private bool _subscribedToSceneLoaded = false;
    private bool _boostActive = false;
    private bool _cinematicActive = false;
    private float _currentBoostDuration = 10f;

    // Warning flags
    private bool _warnedMissingCamera = false;
    private bool _warnedMissingCar = false;

    // Explicit car reference passed via RefreshCarReference, consumed by CacheReferences
    private Transform _explicitCar = null;

    // ==================== UNITY LIFECYCLE ====================

    private void Awake()
    {
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

        // Initialize DOTween if not already
        DOTween.Init();
    }

    private void Start()
    {
        SubscribeToSceneLoaded();
        CacheReferences();
        TrySubscribeToBoostController();
        CheckAndApplyActiveBoost();
    }

    private void OnEnable()
    {
        TrySubscribeToBoostController();
        SubscribeToSceneLoaded();
    }

    private void OnDisable()
    {
        KillAllTweens();
        UnsubscribeFromBoostController();
        UnsubscribeFromSceneLoaded();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        KillAllTweens();
        UnsubscribeFromBoostController();
        UnsubscribeFromSceneLoaded();
    }

    // Retry subscription if controller wasn't ready
    private void LateUpdate()
    {
        if (!_subscribedToBoostController)
        {
            TrySubscribeToBoostController();
        }
    }

    // ==================== SCENE MANAGEMENT ====================

    private void SubscribeToSceneLoaded()
    {
        if (!_subscribedToSceneLoaded)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribedToSceneLoaded = true;
        }
    }

    private void UnsubscribeFromSceneLoaded()
    {
        if (_subscribedToSceneLoaded)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _subscribedToSceneLoaded = false;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset warning flags
        _warnedMissingCamera = false;
        _warnedMissingCar = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log($"[CinematicBoost] Scene loaded: {scene.name}. Re-caching references...");
#endif

        // Kill any active tweens before re-caching
        KillAllTweens();
        _cinematicActive = false;

        // Reset defaults cache for new scene's camera
        _defaultsCached = false;

        // Re-cache references for new scene
        CacheReferences();

        // If boost was active, re-apply cinematic immediately
        CheckAndApplyActiveBoost();
    }

    // ==================== BOOST CONTROLLER SUBSCRIPTION ====================

    private void TrySubscribeToBoostController()
    {
        if (_subscribedToBoostController) return;

        if (BoostModeController.Instance != null)
        {
            BoostModeController.Instance.OnBoostStarted += HandleBoostStarted;
            BoostModeController.Instance.OnBoostEnded += HandleBoostEnded;
            _subscribedToBoostController = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log("[CinematicBoost] Subscribed to BoostModeController events.");
#endif
        }
    }

    private void UnsubscribeFromBoostController()
    {
        if (!_subscribedToBoostController) return;

        if (BoostModeController.Instance != null)
        {
            BoostModeController.Instance.OnBoostStarted -= HandleBoostStarted;
            BoostModeController.Instance.OnBoostEnded -= HandleBoostEnded;
        }
        _subscribedToBoostController = false;
    }

    // ==================== REFERENCE CACHING ====================

    private void CacheReferences()
    {
        // Cache camera
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();
        }

        if (_mainCamera != null)
        {
            _cameraTransform = _mainCamera.transform;
            _originalCameraLocalPos = _cameraTransform.localPosition;
            _originalCameraLocalRot = _cameraTransform.localRotation;

            // Only cache defaults if not already cached (prevents overwriting during active boost/scene reload)
            if (!_defaultsCached)
            {
                _defaultFOV = _mainCamera.fieldOfView;
                _defaultLocalEuler = _cameraTransform.localEulerAngles;
                _defaultsCached = true;
            }
            _warnedMissingCamera = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[CinematicBoost] Camera cached: {_mainCamera.name}, FOV={_defaultFOV}, Tilt={_defaultLocalEuler.x}");
#endif
        }
        else if (!_warnedMissingCamera)
        {
            Debug.LogWarning("[CinematicBoost] No camera found. Cinematic effects disabled.");
            _warnedMissingCamera = true;
        }

        // Cache car (use explicit reference if provided, otherwise tag search)
        if (_explicitCar != null)
        {
            _carObject = _explicitCar.gameObject;
            _explicitCar = null; // consumed
        }
        else
        {
            _carObject = GameObject.FindGameObjectWithTag("Car");
        }
        if (_carObject != null)
        {
            _carTransform = _carObject.transform;
            _originalCarLocalPos = _carTransform.localPosition;
            _originalCarLocalRot = _carTransform.localRotation;
            _warnedMissingCar = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[CinematicBoost] Car cached: {_carObject.name}");
#endif
        }
        else if (!_warnedMissingCar)
        {
            Debug.LogWarning("[CinematicBoost] No GameObject with tag 'Car' found. Car shake disabled.");
            _warnedMissingCar = true;
        }
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Forces a re-cache of the car reference. Call after switching the active car.
    /// </summary>
    public void RefreshCarReference(Transform activeCar = null)
    {
        _warnedMissingCar = false;
        _explicitCar = activeCar;
        CacheReferences();

        // If boost is active, re-apply cinematic to the new car
        if (_cinematicActive && _carTransform != null)
        {
            _originalCarLocalPos = _carTransform.localPosition;
            _originalCarLocalRot = _carTransform.localRotation;
        }
    }

    /// <summary>
    /// Kills the car shake/rotation tweens and resets the car model
    /// to its original local transform. Called by PoliceCatchController
    /// before the chase slide so the shake doesn't desync child objects.
    /// </summary>
    public void SuspendCarShake()
    {
        Debug.Log($"[BoostCinematicDebug] SuspendCarShake CALLED: _carTransform={(_carTransform != null ? _carTransform.name : "NULL")} shakeActive={(_carShakePosTween != null && _carShakePosTween.IsActive())} _originalCarLocalPos={_originalCarLocalPos}");
        if (_carTransform != null)
            Debug.Log($"[BoostCinematicDebug] SuspendCarShake: BEFORE restore — carModel.local={_carTransform.localPosition}");

        if (_carShakePosTween != null && _carShakePosTween.IsActive())
        {
            _carShakePosTween.Kill();
            _carShakePosTween = null;
        }
        if (_carShakeRotTween != null && _carShakeRotTween.IsActive())
        {
            _carShakeRotTween.Kill();
            _carShakeRotTween = null;
        }

        // Reset to pre-shake state
        if (_carTransform != null)
        {
            _carTransform.localPosition = _originalCarLocalPos;
            _carTransform.localRotation = _originalCarLocalRot;
            Debug.Log($"[BoostCinematicDebug] SuspendCarShake: AFTER restore — carModel.local={_carTransform.localPosition} (restored to {_originalCarLocalPos})");
        }
    }

    /// <summary>
    /// Restarts car shake if a boost is still active. Called by
    /// PoliceCatchController after the chase ends.
    /// </summary>
    public void ResumeCarShakeIfActive()
    {
        if (!_boostActive || !_cinematicActive || _carTransform == null) return;

        // Refresh cached original in case the car moved (e.g., chase reset)
        _originalCarLocalPos = _carTransform.localPosition;
        _originalCarLocalRot = _carTransform.localRotation;

        // Use a safe fallback duration for the remaining shake
        float remaining = Mathf.Max(_currentBoostDuration, 5f);

        int loops = Mathf.CeilToInt(remaining / ShakeLoopDuration);

        _carShakePosTween = _carTransform.DOShakePosition(
            duration: ShakeLoopDuration,
            strength: new Vector3(shakePositionStrength, shakePositionStrength * 0.5f, shakePositionStrength * 0.3f),
            vibrato: shakeVibrato,
            randomness: shakeRandomness,
            snapping: false,
            fadeOut: false
        ).SetLoops(loops, LoopType.Restart).SetUpdate(true);

        _carShakeRotTween = _carTransform.DOShakeRotation(
            duration: ShakeLoopDuration,
            strength: new Vector3(shakeRotationStrength * 0.3f, shakeRotationStrength * 0.2f, shakeRotationStrength),
            vibrato: shakeVibrato,
            randomness: shakeRandomness * 0.7f,
            fadeOut: false
        ).SetLoops(loops, LoopType.Restart).SetUpdate(true);
    }

    // ==================== STATE RECOVERY ====================

    private void CheckAndApplyActiveBoost()
    {
        if (BoostModeController.Instance == null) return;

        bool isActive = BoostModeController.Instance.IsBoostActive;

        if (isActive && !_cinematicActive)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log("[CinematicBoost] Detected active boost on scene load. Applying cinematic immediately.");
#endif

            _boostActive = true;
            // Apply cinematic state instantly (no ease-in since boost already active)
            ApplyCinematicStateInstant();
        }
    }

    // ==================== BOOST EVENT HANDLERS ====================

    private void HandleBoostStarted(float duration)
    {
        if (_boostActive) return; // Prevent double-activation

        _boostActive = true;
        _currentBoostDuration = duration;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log($"[CinematicBoost] Boost started! Duration: {duration}s. Beginning cinematic sequence...");
#endif

        StartCinematicSequence(duration);
    }

    private void HandleBoostEnded()
    {
        if (!_boostActive) return;

        _boostActive = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log("[CinematicBoost] Boost ended. Returning to normal...");
#endif

        EndCinematicSequence();
    }

    // ==================== CINEMATIC SEQUENCES ====================

    private void StartCinematicSequence(float duration)
    {
        // Kill any existing tweens to prevent stacking
        KillAllTweens();

        _cinematicActive = true;

        // Only re-cache if references were lost (avoid Find calls every boost start)
        if (_cameraTransform == null || _mainCamera == null || _carTransform == null)
        {
            CacheReferences();
        }

        // === CAMERA ZOOM + TILT SEQUENCE (Perspective FOV) ===
        if (_cameraTransform != null && _mainCamera != null)
        {
            // Store current position as original (in case we're mid-transition)
            _originalCameraLocalPos = _cameraTransform.localPosition;

            // Pull back (Z-) + dip down (Y-) — ground-level speed sensation
            Vector3 targetPos = _originalCameraLocalPos + new Vector3(0, -cameraDipY, -cameraPushDistance);

            // Create camera move sequence with impact jolt appended after the move
            // NOTE: DOShakePosition must NOT run concurrently with DOLocalMove on the
            // same transform — it captures the starting position and resets to it on
            // completion, causing a visible snap. Instead we append a brief DOPunchPosition
            // AFTER the move finishes so the captured origin equals the final targetPos.
            _cameraInSequence = DOTween.Sequence();
            _cameraInSequence.Append(
                _cameraTransform.DOLocalMove(targetPos, cameraInTime)
                    .SetEase(Ease.OutBack)
            );
            _cameraInSequence.Append(
                _cameraTransform.DOPunchPosition(new Vector3(0.07f, 0.07f, 0f), 0.15f, 15, 0.5f)
            );
            _cameraInSequence.SetUpdate(true);

            // FOV punch: briefly overshoot then settle — wide angle = speed sensation
            float fovPunch = targetFOV + fovPunchOvershoot;
            _fovInTween = DOTween.Sequence()
                .Append(DOTween.To(() => _mainCamera.fieldOfView, x => _mainCamera.fieldOfView = x, fovPunch, cameraInTime * 0.35f).SetEase(Ease.OutQuart))
                .Append(DOTween.To(() => _mainCamera.fieldOfView, x => _mainCamera.fieldOfView = x, targetFOV, cameraInTime * 0.65f).SetEase(Ease.OutSine))
                .SetUpdate(true);

            // Camera tilt tween (rotation X)
            Vector3 targetEuler = _cameraTransform.localEulerAngles;
            targetEuler.x = targetTiltX;
            _tiltInTween = _cameraTransform.DOLocalRotate(targetEuler, cameraInTime)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[CinematicBoost] Tweening to FOV={targetFOV} (punch {fovPunch}), TiltX={targetTiltX}");
#endif
        }

        // === CAR SHAKE SEQUENCE ===
        if (_carTransform != null)
        {
            // Store original car position
            _originalCarLocalPos = _carTransform.localPosition;
            _originalCarLocalRot = _carTransform.localRotation;

            // Use short-duration looping shakes instead of one long shake.
            // DOTween pre-computes the entire shake path on creation; a 16s shake
            // with vibrato=20 generates hundreds of keyframes in one frame (= hitch).
            // A 1s looping shake computes ~20 keyframes and loops seamlessly.
            int loops = Mathf.CeilToInt(duration / ShakeLoopDuration);

            _carShakePosTween = _carTransform.DOShakePosition(
                duration: ShakeLoopDuration,
                strength: new Vector3(shakePositionStrength, shakePositionStrength * 0.5f, shakePositionStrength * 0.3f),
                vibrato: shakeVibrato,
                randomness: shakeRandomness,
                snapping: false,
                fadeOut: false
            ).SetLoops(loops, LoopType.Restart).SetUpdate(true);

            // Rotation shake - engine rumble, looped
            _carShakeRotTween = _carTransform.DOShakeRotation(
                duration: ShakeLoopDuration,
                strength: new Vector3(shakeRotationStrength * 0.3f, shakeRotationStrength * 0.2f, shakeRotationStrength),
                vibrato: shakeVibrato,
                randomness: shakeRandomness * 0.7f,
                fadeOut: false
            ).SetLoops(loops, LoopType.Restart).SetUpdate(true);
        }
    }

    private void ApplyCinematicStateInstant()
    {
        // Apply cinematic state without animation (for scene reload during boost)
        _cinematicActive = true;

        if (_cameraTransform == null || _carTransform == null)
        {
            CacheReferences();
        }

        // Camera instant push + zoom + tilt
        if (_cameraTransform != null && _mainCamera != null)
        {
            _originalCameraLocalPos = _cameraTransform.localPosition;

            Vector3 targetPos = _originalCameraLocalPos + new Vector3(0, -cameraDipY, -cameraPushDistance);
            _cameraTransform.localPosition = targetPos;

            // Apply field of view (wide angle = speed)
            _mainCamera.fieldOfView = targetFOV;

            // Apply tilt
            Vector3 euler = _cameraTransform.localEulerAngles;
            euler.x = targetTiltX;
            _cameraTransform.localEulerAngles = euler;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[CinematicBoost] Instant apply: FOV={targetFOV}, TiltX={targetTiltX}");
#endif
        }

        // Start car shake immediately (use remaining boost time or safe fallback)
        if (_carTransform != null)
        {
            _originalCarLocalPos = _carTransform.localPosition;
            _originalCarLocalRot = _carTransform.localRotation;

            float shakeDuration = Mathf.Max(_currentBoostDuration, 5f);
            int loops = Mathf.CeilToInt(shakeDuration / ShakeLoopDuration);

            _carShakePosTween = _carTransform.DOShakePosition(
                duration: ShakeLoopDuration,
                strength: new Vector3(shakePositionStrength, shakePositionStrength * 0.5f, shakePositionStrength * 0.3f),
                vibrato: shakeVibrato,
                randomness: shakeRandomness,
                snapping: false,
                fadeOut: false
            ).SetLoops(loops, LoopType.Restart).SetUpdate(true);

            _carShakeRotTween = _carTransform.DOShakeRotation(
                duration: ShakeLoopDuration,
                strength: new Vector3(shakeRotationStrength * 0.3f, shakeRotationStrength * 0.2f, shakeRotationStrength),
                vibrato: shakeVibrato,
                randomness: shakeRandomness * 0.7f,
                fadeOut: false
            ).SetLoops(loops, LoopType.Restart).SetUpdate(true);
        }
    }

    private void EndCinematicSequence()
    {
        // ── DEBUG: Detect if this fires during police chase ──
        bool chaseActive = PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive;
        Debug.Log($"[BoostCinematicDebug] EndCinematicSequence CALLED! chaseActive={chaseActive} _boostActive={_boostActive} _cinematicActive={_cinematicActive}");
        if (chaseActive)
            Debug.LogWarning("[BoostCinematicDebug] ⚠ EndCinematicSequence firing DURING POLICE CHASE! This will create DOLocalMove on car model!");

        // Kill active shake and in-sequence tweens
        KillInProgressTweens();

        _cinematicActive = false;

        // === CAMERA RETURN SEQUENCE (Perspective FOV) ===
        if (_cameraTransform != null && _mainCamera != null)
        {
            _cameraOutSequence = DOTween.Sequence();
            _cameraOutSequence.Append(
                _cameraTransform.DOLocalMove(_originalCameraLocalPos, cameraOutTime)
                    .SetEase(Ease.OutCubic)
            );
            _cameraOutSequence.SetUpdate(true);

            // Field of view return
            _fovOutTween = DOTween.To(
                () => _mainCamera.fieldOfView,
                x => _mainCamera.fieldOfView = x,
                _defaultFOV,
                cameraOutTime
            ).SetEase(Ease.OutCubic).SetUpdate(true);

            // Tilt return
            _tiltOutTween = _cameraTransform.DOLocalRotate(_defaultLocalEuler, cameraOutTime)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[CinematicBoost] Returning to FOV={_defaultFOV}, Tilt={_defaultLocalEuler.x}");
#endif
        }

        // === CAR RETURN TO ORIGINAL ===
        // Skip car DOLocalMove when police chase is active — the chase
        // controls CarRoot position and the car model must stay stable.
        if (_carTransform != null && !chaseActive)
        {
            // Smoothly return car to original position/rotation
            _carResetPosTween = _carTransform.DOLocalMove(_originalCarLocalPos, cameraOutTime * 0.6f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            _carResetRotTween = _carTransform.DOLocalRotateQuaternion(_originalCarLocalRot, cameraOutTime * 0.6f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
        else if (_carTransform != null && chaseActive)
        {
            Debug.Log($"[BoostCinematicDebug] EndCinematicSequence: SKIPPED car DOLocalMove — chase is active");
        }
    }

    // ==================== TWEEN CLEANUP ====================

    private void KillInProgressTweens()
    {
        // Kill shake tweens
        if (_carShakePosTween != null && _carShakePosTween.IsActive())
        {
            _carShakePosTween.Kill();
            _carShakePosTween = null;
        }

        if (_carShakeRotTween != null && _carShakeRotTween.IsActive())
        {
            _carShakeRotTween.Kill();
            _carShakeRotTween = null;
        }

        // Kill camera in sequence
        if (_cameraInSequence != null && _cameraInSequence.IsActive())
        {
            _cameraInSequence.Kill();
            _cameraInSequence = null;
        }

        if (_fovInTween != null && _fovInTween.IsActive())
        {
            _fovInTween.Kill();
            _fovInTween = null;
        }

        if (_tiltInTween != null && _tiltInTween.IsActive())
        {
            _tiltInTween.Kill();
            _tiltInTween = null;
        }
    }

    private void KillAllTweens()
    {
        KillInProgressTweens();

        // Kill return tweens
        if (_cameraOutSequence != null && _cameraOutSequence.IsActive())
        {
            _cameraOutSequence.Kill();
            _cameraOutSequence = null;
        }

        if (_fovOutTween != null && _fovOutTween.IsActive())
        {
            _fovOutTween.Kill();
            _fovOutTween = null;
        }

        if (_tiltOutTween != null && _tiltOutTween.IsActive())
        {
            _tiltOutTween.Kill();
            _tiltOutTween = null;
        }

        if (_carResetPosTween != null && _carResetPosTween.IsActive())
        {
            _carResetPosTween.Kill();
            _carResetPosTween = null;
        }

        if (_carResetRotTween != null && _carResetRotTween.IsActive())
        {
            _carResetRotTween.Kill();
            _carResetRotTween = null;
        }

        // Restore original values immediately if references exist
        if (_cameraTransform != null)
        {
            _cameraTransform.localPosition = _originalCameraLocalPos;
            _cameraTransform.localRotation = _originalCameraLocalRot;
        }

        if (_mainCamera != null && _defaultsCached)
        {
            _mainCamera.fieldOfView = _defaultFOV;
            _cameraTransform.localEulerAngles = _defaultLocalEuler;
        }

        if (_carTransform != null)
        {
            _carTransform.localPosition = _originalCarLocalPos;
            _carTransform.localRotation = _originalCarLocalRot;
        }
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Forces a refresh of camera and car references. Call if objects are spawned dynamically.
    /// If resetDefaults is true, will also re-cache camera defaults (FOV, tilt).
    /// </summary>
    public void RefreshReferences(bool resetDefaults = false)
    {
        _warnedMissingCamera = false;
        _warnedMissingCar = false;

        if (resetDefaults)
        {
            _defaultsCached = false;
        }

        CacheReferences();
    }

    /// <summary>
    /// Immediately stops all cinematic effects and restores original state.
    /// </summary>
    public void ForceStopCinematic()
    {
        _boostActive = false;
        KillAllTweens();
        _cinematicActive = false;
    }
}
