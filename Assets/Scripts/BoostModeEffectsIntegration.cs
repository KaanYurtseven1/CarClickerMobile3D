using UnityEngine;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// Phase 3.1 Integration: Connects BoostModeController events to visual effects and economy multiplier.
/// 
/// SETUP INSTRUCTIONS:
/// 1. Create an empty GameObject in your Main scene called "BoostModeEffectsIntegration"
/// 2. Attach this script to it
/// 3. Ensure your car prefab has tag "Car" assigned
/// 4. Ensure car has children named "BoostFX_L" and "BoostFX_R" (inactive by default)
/// 
/// The script persists across scenes (DontDestroyOnLoad) and automatically:
/// - Activates VFX when boost starts
/// - Applies 20x economy multiplier during boost
/// - Deactivates VFX and resets multiplier when boost ends
/// </summary>
public class BoostModeEffectsIntegration : MonoBehaviour
{
    public static BoostModeEffectsIntegration Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Boost Multiplier Settings")]
    [Tooltip("Economy multiplier during boost (MPS and MPT) - dynamically calculated from card level")]
    public float boostEconomyMultiplier = 20f;

    [Header("Road Speed Boost")]
    [Tooltip("Road scroll speed multiplier during boost. Higher = road scrolls faster = stronger speed feel.")]
    public float roadSpeedMultiplier = 2.5f;

    [Header("Debug")]
    [Tooltip("Enable verbose logging (Editor/Development builds only)")]
    public bool verboseLogs = true;

    // Cached references
    private GameObject _carObject;
    private GameObject _boostFXLeft;
    private GameObject _boostFXRight;

    // Warning flags to prevent log spam
    private bool _warnedMissingCar = false;
    private bool _warnedMissingVFX = false;
    private bool _warnedMissingController = false;

    // Track subscription state
    private bool _subscribedToBoostController = false;
    private bool _subscribedToSceneLoaded = false;

    // Track if boost is currently active (for scene reload handling)
    private bool _boostCurrentlyActive = false;

    // ==================== UNITY LIFECYCLE ====================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // DontDestroyOnLoad only works on ROOT GameObjects.
            // BoostModeEffectsIntegration lives under VFX > VFX & Effects; detach first.
            if (transform.parent != null)
            {
                Debug.Log($"[BoostIntegration] Detaching from parent '{transform.parent.name}' for DontDestroyOnLoad.");
                transform.SetParent(null);
            }

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
        SubscribeToSceneLoaded();
        TrySubscribeToBoostController();
        CacheCarAndVFX();
        CheckAndApplyActiveBoost();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeFromAll();
    }

    private void OnDisable()
    {
        UnsubscribeFromAll();
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
        // Reset warning flags for new scene
        _warnedMissingCar = false;
        _warnedMissingVFX = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log($"[BoostIntegration] Scene loaded: {scene.name}. Rebinding references...");
#endif

        // Re-cache references for new scene
        CacheCarAndVFX();

        // If boost was active when we loaded, ensure VFX is on
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
            _warnedMissingController = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log("[BoostIntegration] Subscribed to BoostModeController events.");
#endif
        }
        else if (!_warnedMissingController)
        {
            Debug.LogWarning("[BoostIntegration] BoostModeController.Instance is null. Will retry on LateUpdate.");
            _warnedMissingController = true;
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

    private void UnsubscribeFromAll()
    {
        UnsubscribeFromBoostController();
        UnsubscribeFromSceneLoaded();
    }

    // Retry subscription if controller wasn't ready at Start
    private void LateUpdate()
    {
        if (!_subscribedToBoostController)
        {
            TrySubscribeToBoostController();
        }
    }

    // ==================== CAR & VFX CACHING ====================

    private void CacheCarAndVFX(Transform explicitCar = null)
    {
        // Use explicit car if provided, otherwise find by tag
        if (explicitCar != null)
        {
            _carObject = explicitCar.gameObject;
        }
        else
        {
            _carObject = GameObject.FindGameObjectWithTag("Car");
        }

        if (_carObject == null)
        {
            if (!_warnedMissingCar)
            {
                Debug.LogWarning("[BoostIntegration] No GameObject with tag 'Car' found. VFX will not activate.");
                _warnedMissingCar = true;
            }
            _boostFXLeft = null;
            _boostFXRight = null;
            return;
        }

        _warnedMissingCar = false;

        // Find VFX children by prefix (handles Unity suffixed names like "BoostFX_L (1)")
        Transform leftVFX = FindChildByPrefix(_carObject.transform, "BoostFX_L");
        Transform rightVFX = FindChildByPrefix(_carObject.transform, "BoostFX_R");

        _boostFXLeft = leftVFX != null ? leftVFX.gameObject : null;
        _boostFXRight = rightVFX != null ? rightVFX.gameObject : null;

        if (_boostFXLeft == null || _boostFXRight == null)
        {
            if (!_warnedMissingVFX)
            {
                Debug.LogWarning($"[BoostIntegration] VFX not found under car '{_carObject.name}'. " +
                    $"BoostFX_L: {(_boostFXLeft != null ? "Found" : "Missing")}, " +
                    $"BoostFX_R: {(_boostFXRight != null ? "Found" : "Missing")}");
                _warnedMissingVFX = true;
            }
        }
        else
        {
            _warnedMissingVFX = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[BoostIntegration] VFX cached: BoostFX_L={_boostFXLeft.name}, BoostFX_R={_boostFXRight.name}");
#endif
        }
    }

    /// <summary>
    /// Recursively searches for a child transform by name.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;

        // Direct child check
        Transform found = parent.Find(childName);
        if (found != null) return found;

        // Recursive search through all children
        foreach (Transform child in parent)
        {
            found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// Searches for a child whose name equals the prefix or starts with "prefix " (e.g. "BoostFX_L (1)").
    /// Checks direct children first, then recurses.
    /// </summary>
    private static Transform FindChildByPrefix(Transform parent, string prefix)
    {
        if (parent == null) return null;

        // Direct children first (most common case)
        foreach (Transform child in parent)
        {
            if (child.name == prefix || child.name.StartsWith(prefix + " "))
                return child;
        }

        // Recursive fallback
        foreach (Transform child in parent)
        {
            Transform found = FindChildByPrefix(child, prefix);
            if (found != null) return found;
        }

        return null;
    }

    // ==================== BOOST EVENT HANDLERS ====================

    private void HandleBoostStarted(float duration)
    {
        _boostCurrentlyActive = true;

        // Get current boost level and calculate multiplier dynamically
        int boostLevel = CardManager.Instance != null ? CardManager.Instance.GetCardLevel(CardType.BoostMode) : 1;
        var parameters = BoostModeController.GetBoostParamsForLevel(boostLevel);
        float actualMultiplier = parameters.multiplier;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log($"[BoostIntegration] Boost STARTED! Duration: {duration}s, Level: {boostLevel}, Multiplier: x{actualMultiplier}");
#endif

        // Activate VFX
        SetVFXActive(true);

        // Apply economy multiplier (use calculated value, not inspector value)
        ApplyEconomyMultiplier(actualMultiplier);

        // Speed up road scroll
        ApplyRoadSpeedMultiplier(roadSpeedMultiplier);
    }

    private void HandleBoostEnded()
    {
        _boostCurrentlyActive = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log("[BoostIntegration] Boost ENDED. Restoring multiplier to x1.");
#endif

        // Deactivate VFX
        SetVFXActive(false);

        // Reset economy multiplier
        ApplyEconomyMultiplier(1f);

        // Reset road scroll speed
        ApplyRoadSpeedMultiplier(1f);
    }

    // ==================== VFX CONTROL ====================

    private void SetVFXActive(bool active)
    {
        // Re-cache if car reference was lost (e.g., car respawned)
        if (_carObject == null)
        {
            CacheCarAndVFX();
        }

        if (_boostFXLeft != null)
        {
            _boostFXLeft.SetActive(active);
        }

        if (_boostFXRight != null)
        {
            _boostFXRight.SetActive(active);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs && (_boostFXLeft != null || _boostFXRight != null))
            Debug.Log($"[BoostIntegration] VFX SetActive({active})");
#endif
    }

    // ==================== ECONOMY MULTIPLIER ====================

    private void ApplyEconomyMultiplier(float multiplier)
    {
        if (CurrencyManager.Instance != null)
        {
            // Use the new method that bypasses the timer system
            CurrencyManager.Instance.SetBoostMultiplier(multiplier);
        }
        else
        {
            Debug.LogWarning("[BoostIntegration] CurrencyManager.Instance is null. Cannot apply multiplier.");
        }
    }

    // ==================== ROAD SPEED ====================

    private void ApplyRoadSpeedMultiplier(float multiplier)
    {
        if (WorldScrollSpeed.Instance != null)
        {
            WorldScrollSpeed.Instance.SpeedMultiplier = multiplier;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[BoostIntegration] Road speed multiplier set to x{multiplier} (effective: {WorldScrollSpeed.Instance.EffectiveSpeed:F1})");
#endif
        }
    }

    // ==================== STATE RECOVERY ====================

    /// <summary>
    /// Checks if boost is currently active (e.g., restored from save) and applies effects.
    /// </summary>
    private void CheckAndApplyActiveBoost()
    {
        if (BoostModeController.Instance == null) return;

        // Check if boost is in Active state (restored from persistence)
        bool isActive = BoostModeController.Instance.IsBoostActive;

        if (isActive && !_boostCurrentlyActive)
        {
            // Get current boost level and calculate multiplier dynamically
            int boostLevel = CardManager.Instance != null ? CardManager.Instance.GetCardLevel(CardType.BoostMode) : 1;
            var parameters = BoostModeController.GetBoostParamsForLevel(boostLevel);
            float actualMultiplier = parameters.multiplier;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[BoostIntegration] Detected active boost on scene load. Applying effects. Level: {boostLevel}, Multiplier: x{actualMultiplier}");
#endif

            _boostCurrentlyActive = true;
            SetVFXActive(true);
            ApplyEconomyMultiplier(actualMultiplier);
            ApplyRoadSpeedMultiplier(roadSpeedMultiplier);
        }
        else if (!isActive && _boostCurrentlyActive)
        {
            // Boost ended while we were away
            _boostCurrentlyActive = false;
            SetVFXActive(false);
            ApplyEconomyMultiplier(1f);
            ApplyRoadSpeedMultiplier(1f);
        }
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Forces a refresh of car and VFX references. Call this if the car is spawned dynamically.
    /// </summary>
    public void RefreshCarReference(Transform activeCar = null)
    {
        _warnedMissingCar = false;
        _warnedMissingVFX = false;
        CacheCarAndVFX(activeCar);

        // If boost is active, ensure VFX is on for the new car
        if (_boostCurrentlyActive)
        {
            SetVFXActive(true);
        }
    }
}
