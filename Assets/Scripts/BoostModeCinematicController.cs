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
/// - When boost starts: Camera zooms in (orthographicSize), tilts, pushes forward, car shakes with power
/// - When boost ends: Everything smoothly returns to original state
/// - Creates a premium, racing-game turbo camera feel
/// - Works with ORTHOGRAPHIC cameras (no FOV changes)
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
    [Header("Camera Zoom Settings (Orthographic)")]
    [Tooltip("Target orthographic size during boost (smaller = zoomed in)")]
    public float targetOrthoSize = 3.5f;

    [Tooltip("Target camera X rotation (tilt) during boost in degrees")]
    public float targetTiltX = 25f;

    [Tooltip("How far camera pushes forward (local Z) during boost")]
    public float cameraPushDistance = 0.6f;

    [Tooltip("Time to ease camera into boost position (seconds)")]
    public float cameraInTime = 0.35f;

    [Tooltip("Time to ease camera back to original position (seconds)")]
    public float cameraOutTime = 1.0f;

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
    private float _defaultOrthoSize;
    private Vector3 _defaultLocalEuler;
    private bool _defaultsCached = false;
    private Vector3 _originalCarLocalPos;
    private Quaternion _originalCarLocalRot;

    // ==================== TWEEN REFERENCES ====================
    private Sequence _cameraInSequence;
    private Sequence _cameraOutSequence;
    private Tween _orthoInTween;
    private Tween _orthoOutTween;
    private Tween _tiltInTween;
    private Tween _tiltOutTween;
    private Tween _carShakePosTween;
    private Tween _carShakeRotTween;
    private Tween _carResetPosTween;
    private Tween _carResetRotTween;

    // ==================== STATE TRACKING ====================
    private bool _subscribedToBoostController = false;
    private bool _subscribedToSceneLoaded = false;
    private bool _boostActive = false;
    private bool _cinematicActive = false;
    private float _currentBoostDuration = 10f;

    // Warning flags
    private bool _warnedMissingCamera = false;
    private bool _warnedMissingCar = false;

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
                _defaultOrthoSize = _mainCamera.orthographicSize;
                _defaultLocalEuler = _cameraTransform.localEulerAngles;
                _defaultsCached = true;
            }
            _warnedMissingCamera = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[CinematicBoost] Camera cached: {_mainCamera.name}, OrthoSize={_defaultOrthoSize}, Tilt={_defaultLocalEuler.x}");
#endif
        }
        else if (!_warnedMissingCamera)
        {
            Debug.LogWarning("[CinematicBoost] No camera found. Cinematic effects disabled.");
            _warnedMissingCamera = true;
        }

        // Cache car
        _carObject = GameObject.FindGameObjectWithTag("Car");
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

        // Re-cache if references were lost
        if (_cameraTransform == null || _carTransform == null)
        {
            CacheReferences();
        }

        // === CAMERA ZOOM + TILT SEQUENCE (Orthographic) ===
        if (_cameraTransform != null && _mainCamera != null)
        {
            // Store current position as original (in case we're mid-transition)
            _originalCameraLocalPos = _cameraTransform.localPosition;

            // Calculate target position (push forward in local Z)
            Vector3 targetPos = _originalCameraLocalPos + Vector3.forward * cameraPushDistance;

            // Create camera push sequence
            _cameraInSequence = DOTween.Sequence();
            _cameraInSequence.Append(
                _cameraTransform.DOLocalMove(targetPos, cameraInTime)
                    .SetEase(Ease.OutQuart)
            );
            _cameraInSequence.SetUpdate(true); // Ignore timescale

            // Orthographic size tween (zoom in)
            _orthoInTween = DOTween.To(
                () => _mainCamera.orthographicSize,
                x => _mainCamera.orthographicSize = x,
                targetOrthoSize,
                cameraInTime
            ).SetEase(Ease.OutQuart).SetUpdate(true);

            // Camera tilt tween (rotation X)
            Vector3 targetEuler = _cameraTransform.localEulerAngles;
            targetEuler.x = targetTiltX;
            _tiltInTween = _cameraTransform.DOLocalRotate(targetEuler, cameraInTime)
                .SetEase(Ease.OutQuart)
                .SetUpdate(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[CinematicBoost] Tweening to OrthoSize={targetOrthoSize}, TiltX={targetTiltX}");
#endif
        }

        // === CAR SHAKE SEQUENCE ===
        if (_carTransform != null)
        {
            // Store original car position
            _originalCarLocalPos = _carTransform.localPosition;
            _originalCarLocalRot = _carTransform.localRotation;

            // Position shake - rhythmic power feeling
            // Loop indefinitely until boost ends
            _carShakePosTween = _carTransform.DOShakePosition(
                duration: 1f, // Shake cycle duration
                strength: new Vector3(shakePositionStrength, shakePositionStrength * 0.5f, shakePositionStrength * 0.3f),
                vibrato: shakeVibrato,
                randomness: shakeRandomness,
                snapping: false,
                fadeOut: false // Don't fade - we control ending manually
            )
            .SetLoops(-1, LoopType.Restart) // Infinite loop
            .SetUpdate(true);

            // Rotation shake - subtle engine rumble
            _carShakeRotTween = _carTransform.DOShakeRotation(
                duration: 1f,
                strength: new Vector3(shakeRotationStrength * 0.3f, shakeRotationStrength * 0.2f, shakeRotationStrength),
                vibrato: shakeVibrato,
                randomness: shakeRandomness * 0.7f,
                fadeOut: false
            )
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);
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

            Vector3 targetPos = _originalCameraLocalPos + Vector3.forward * cameraPushDistance;
            _cameraTransform.localPosition = targetPos;

            // Apply orthographic size
            _mainCamera.orthographicSize = targetOrthoSize;

            // Apply tilt
            Vector3 euler = _cameraTransform.localEulerAngles;
            euler.x = targetTiltX;
            _cameraTransform.localEulerAngles = euler;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[CinematicBoost] Instant apply: OrthoSize={targetOrthoSize}, TiltX={targetTiltX}");
#endif
        }

        // Start car shake immediately
        if (_carTransform != null)
        {
            _originalCarLocalPos = _carTransform.localPosition;
            _originalCarLocalRot = _carTransform.localRotation;

            _carShakePosTween = _carTransform.DOShakePosition(
                duration: 1f,
                strength: new Vector3(shakePositionStrength, shakePositionStrength * 0.5f, shakePositionStrength * 0.3f),
                vibrato: shakeVibrato,
                randomness: shakeRandomness,
                snapping: false,
                fadeOut: false
            )
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);

            _carShakeRotTween = _carTransform.DOShakeRotation(
                duration: 1f,
                strength: new Vector3(shakeRotationStrength * 0.3f, shakeRotationStrength * 0.2f, shakeRotationStrength),
                vibrato: shakeVibrato,
                randomness: shakeRandomness * 0.7f,
                fadeOut: false
            )
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);
        }
    }

    private void EndCinematicSequence()
    {
        // Kill active shake and in-sequence tweens
        KillInProgressTweens();

        _cinematicActive = false;

        // === CAMERA RETURN SEQUENCE (Orthographic) ===
        if (_cameraTransform != null && _mainCamera != null)
        {
            _cameraOutSequence = DOTween.Sequence();
            _cameraOutSequence.Append(
                _cameraTransform.DOLocalMove(_originalCameraLocalPos, cameraOutTime)
                    .SetEase(Ease.InOutCubic)
            );
            _cameraOutSequence.SetUpdate(true);

            // Orthographic size return
            _orthoOutTween = DOTween.To(
                () => _mainCamera.orthographicSize,
                x => _mainCamera.orthographicSize = x,
                _defaultOrthoSize,
                cameraOutTime
            ).SetEase(Ease.InOutCubic).SetUpdate(true);

            // Tilt return
            _tiltOutTween = _cameraTransform.DOLocalRotate(_defaultLocalEuler, cameraOutTime)
                .SetEase(Ease.InOutCubic)
                .SetUpdate(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[CinematicBoost] Returning to OrthoSize={_defaultOrthoSize}, Tilt={_defaultLocalEuler.x}");
#endif
        }

        // === CAR RETURN TO ORIGINAL ===
        if (_carTransform != null)
        {
            // Smoothly return car to original position/rotation
            _carResetPosTween = _carTransform.DOLocalMove(_originalCarLocalPos, cameraOutTime * 0.6f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            _carResetRotTween = _carTransform.DOLocalRotateQuaternion(_originalCarLocalRot, cameraOutTime * 0.6f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
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

        if (_orthoInTween != null && _orthoInTween.IsActive())
        {
            _orthoInTween.Kill();
            _orthoInTween = null;
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

        if (_orthoOutTween != null && _orthoOutTween.IsActive())
        {
            _orthoOutTween.Kill();
            _orthoOutTween = null;
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
            _mainCamera.orthographicSize = _defaultOrthoSize;
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
    /// If resetDefaults is true, will also re-cache camera defaults (orthoSize, tilt).
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
