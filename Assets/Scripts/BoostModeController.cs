using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

public class BoostModeController : MonoBehaviour
{
    public static BoostModeController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    public enum BoostState
    {
        Locked,
        Charging,
        Ready,
        Active,
        Cooldown
    }

    [Header("Boost Settings")]
    public float boostDurationSeconds = 10f;
    [Tooltip("Cooldown in seconds (dynamically set by card level)")]
    public float cooldownSeconds = 30f;
    public bool hideBarDuringActive = false;

    private BoostState currentState = BoostState.Locked;
    private float stateTimer = 0f;
    private long lastStateTimestamp = 0;

    // Events
    public event Action<float> OnBoostStarted; // duration
    public event Action OnBoostEnded;
    public event Action<float> OnBoostCooldownStarted; // cooldown
    public event Action<int, int> OnBoostChargeChanged; // current, max
    public event Action<float> OnChargeChanged01; // normalized 0..1
    public event Action OnBoostReady;
    public event Action<BoostState> OnStateChanged;
    public event Action OnNitroChargeAccepted;

    // For persistence
    private const string SaveKey = "BoostModeControllerSave";

    [Header("UI References")]
    public Slider boostBarSlider;
    public GameObject boostBarRoot;

    [Header("Charge Settings")]
    public int chargePerNitro = 1;
    [Tooltip("Max charge (dynamically set by card level)")]
    public int maxCharge = 20;
    public bool verboseLogs = true;

    private int currentCharge = 0;
    private bool isUnlocked = false;
    private bool _sceneLoadedSubscribed = false;

    // Save/Load re-entrancy protection
    private bool _isLoading = false;

    // UI warning flag (log once)
    private bool _warnedMissingUI = false;

    // Save debouncing
    private bool _saveRequested = false;
    private float _lastSaveTime = 0f;
    private const float SaveDebounceInterval = 1f;

    public float Charge01 => maxCharge > 0 ? Mathf.Clamp01((float)currentCharge / maxCharge) : 0f;

    /// <summary>
    /// Returns true if boost is currently in the Active state.
    /// Used by BoostModeEffectsIntegration to check state on scene load.
    /// </summary>
    public bool IsBoostActive => currentState == BoostState.Active;

    /// <summary>
    /// Returns boost parameters (multiplier, cooldown, maxCharge) for the given card level.
    /// Level 1: 10x, 45s, 10 charge
    /// Level 2: 20x, 60s, 15 charge
    /// Level 3: 30x, 75s, 20 charge
    /// Level 4: 40x, 90s, 25 charge
    /// Level 5: 50x, 105s, 30 charge
    /// Level 6: 60x, 120s, 35 charge
    /// </summary>
    public static (float multiplier, float cooldown, int maxCharge) GetBoostParamsForLevel(int level)
    {
        if (level <= 0) return (1f, 30f, 10);

        // Clamp to max level 6
        level = Mathf.Clamp(level, 1, 6);

        float multiplier = level * 10f;
        float cooldown = 30f + (level * 15f);
        int maxCharge = 5 + (level * 5);

        return (multiplier, cooldown, maxCharge);
    }

    // ==================== SAVE DATA ====================

    [Serializable]
    private class BoostModeControllerSaveData
    {
        public bool isUnlocked;
        public int currentCharge;
        public BoostState currentState;
        public float remainingTime;
        public long lastTimestamp;
    }

    // ==================== UNITY LIFECYCLE ====================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SubscribeSceneLoaded();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeSceneLoaded();
        UnsubscribeFromSaveSystem();
    }

    private void OnDisable()
    {
        UnsubscribeSceneLoaded();
        UnsubscribeFromSaveSystem();
    }

    private void OnEnable()
    {
        SubscribeToSaveSystem();
    }

    private void Start()
    {
        TryBindUI();
        RefreshUnlockState();
        LoadState();
    }

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

    // =============== SAVE SYSTEM SUBSCRIPTION ===============

    private bool _saveSystemSubscribed = false;

    private void SubscribeToSaveSystem()
    {
        if (!_saveSystemSubscribed)
        {
            SaveSystem.OnGameLoaded += OnGameLoaded;
            _saveSystemSubscribed = true;
        }
    }

    private void UnsubscribeFromSaveSystem()
    {
        if (_saveSystemSubscribed)
        {
            SaveSystem.OnGameLoaded -= OnGameLoaded;
            _saveSystemSubscribed = false;
        }
    }

    /// <summary>
    /// Called when SaveSystem finishes loading game data.
    /// Refreshes unlock state since card levels are now loaded.
    /// </summary>
    private void OnGameLoaded()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log("[Boost] OnGameLoaded received. Refreshing unlock state...");
#endif
        RefreshUnlockState();
        UpdateUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _warnedMissingUI = false; // Reset warning for new scene
        TryBindUI();
        RefreshUnlockState();
    }

    private void TryBindUI()
    {
        if (boostBarSlider == null)
        {
            var sliderObj = GameObject.Find("Canvas/TopBar/Slider_BoostBar");
            if (sliderObj != null)
            {
                boostBarSlider = sliderObj.GetComponent<Slider>();
                boostBarRoot = sliderObj;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (verboseLogs)
                    Debug.Log($"[Boost] Bound BoostBar slider: {sliderObj.name}");
#endif
            }
            else if (!_warnedMissingUI)
            {
                _warnedMissingUI = true;
                Debug.LogWarning("[Boost] Could not find 'Canvas/TopBar/Slider_BoostBar'. Boost bar UI will not display.");
            }
        }
        if (boostBarRoot == null && boostBarSlider != null)
        {
            boostBarRoot = boostBarSlider.gameObject;
        }
    }

    public void RefreshUnlockState()
    {
        int level = 0;
        if (CardManager.Instance != null)
        {
            var card = CardManager.Instance.GetCard(CardType.BoostMode);
            if (card != null)
                level = card.currentLevel;
        }

        // Apply level-based parameters
        if (level >= 1)
        {
            var parameters = GetBoostParamsForLevel(level);
            maxCharge = parameters.maxCharge;
            cooldownSeconds = parameters.cooldown;
        }

        isUnlocked = level >= 1;
        if (isUnlocked && currentState == BoostState.Locked)
        {
            SetState(BoostState.Charging);
        }
        else if (!isUnlocked)
        {
            SetState(BoostState.Locked);
        }
        if (boostBarRoot != null)
            boostBarRoot.SetActive(isUnlocked);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log($"[Boost] BoostBar visible: {isUnlocked} (BoostModeLevel={level}, MaxCharge={maxCharge}, Cooldown={cooldownSeconds}s)");
#endif
    }

    public void OnNitroCollected(int amount)
    {
        if (!isUnlocked)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log("[Boost] Ignored nitro: state=Locked");
#endif
            return;
        }
        if (currentState == BoostState.Active || currentState == BoostState.Cooldown)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log($"[Boost] Ignored nitro: state={currentState}");
#endif
            return;
        }
        if (currentState == BoostState.Charging)
        {
            currentCharge += chargePerNitro * amount;
            if (currentCharge > maxCharge) currentCharge = maxCharge;
            if (amount > 0) OnNitroChargeAccepted?.Invoke();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs)
                Debug.Log($"[Boost] Nitro +{amount} => charge {currentCharge}/{maxCharge}");
#endif
            UpdateUI();
            InvokeChargeEvents();
            if (currentCharge >= maxCharge)
            {
                SetState(BoostState.Ready);
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else if (currentState == BoostState.Ready)
        {
            if (verboseLogs) Debug.Log("[Boost] Ignored nitro: state=Ready");
        }
#endif
    }

    private void InvokeChargeEvents()
    {
        OnBoostChargeChanged?.Invoke(currentCharge, maxCharge);
        OnChargeChanged01?.Invoke(Charge01);
    }

    private void UpdateUI()
    {
        if (boostBarSlider != null)
        {
            boostBarSlider.value = Charge01;
            if (boostBarRoot != null)
            {
                bool show = isUnlocked && (currentState == BoostState.Charging || currentState == BoostState.Ready);
                if (hideBarDuringActive && currentState == BoostState.Active)
                    show = false;
                boostBarRoot.SetActive(show);
            }
        }
    }

    private void Update()
    {
        if (currentState == BoostState.Active || currentState == BoostState.Cooldown)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                if (currentState == BoostState.Active)
                {
                    EndBoost();
                }
                else if (currentState == BoostState.Cooldown)
                {
                    SetState(BoostState.Charging);
                }
            }
        }

        // Handle debounced saves
        if (_saveRequested && Time.time - _lastSaveTime >= SaveDebounceInterval)
        {
            FlushSave();
        }
    }

    private void SetState(BoostState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        OnStateChanged?.Invoke(newState);
        lastStateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        switch (newState)
        {
            case BoostState.Locked:
                currentCharge = 0;
                UpdateUI();
                break;
            case BoostState.Charging:
                UpdateUI();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (verboseLogs) Debug.Log("[Boost] State: Charging");
#endif
                break;
            case BoostState.Ready:
                UpdateUI();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (verboseLogs) Debug.Log("[Boost] State: Ready");
#endif
                // Fire OnBoostReady BEFORE auto-starting boost
                OnBoostReady?.Invoke();
                // Auto-start boost immediately
                StartBoost();
                break;
            case BoostState.Active:
                stateTimer = boostDurationSeconds;
                currentCharge = 0;
                UpdateUI();
                InvokeChargeEvents();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (verboseLogs) Debug.Log("[Boost] State: Active");
#endif
                OnBoostStarted?.Invoke(boostDurationSeconds);
                break;
            case BoostState.Cooldown:
                stateTimer = cooldownSeconds;
                UpdateUI();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (verboseLogs) Debug.Log("[Boost] State: Cooldown");
#endif
                OnBoostCooldownStarted?.Invoke(cooldownSeconds);
                break;
        }
        RequestSave();
    }

    private void StartBoost()
    {
        SetState(BoostState.Active);
    }

    private void EndBoost()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs) Debug.Log("[Boost] BOOST ENDED");
#endif
        OnBoostEnded?.Invoke();
        SetState(BoostState.Cooldown);
    }

    // ==================== SAVE/LOAD ====================

    private void RequestSave()
    {
        if (_isLoading) return;
        _saveRequested = true;
    }

    private void FlushSave()
    {
        if (_isLoading) return;
        if (!_saveRequested) return;

        _saveRequested = false;
        _lastSaveTime = Time.time;

        var data = new BoostModeControllerSaveData
        {
            isUnlocked = isUnlocked,
            currentCharge = currentCharge,
            currentState = currentState,
            remainingTime = stateTimer,
            lastTimestamp = lastStateTimestamp
        };
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        // Note: PlayerPrefs.Save() is called only on pause/quit, not here
    }

    private void LoadState()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return;
        var json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return;

        _isLoading = true;
        try
        {
            var data = JsonUtility.FromJson<BoostModeControllerSaveData>(json);
            isUnlocked = data.isUnlocked;
            currentCharge = data.currentCharge;
            currentState = data.currentState;
            stateTimer = data.remainingTime;
            lastStateTimestamp = data.lastTimestamp;

            // Handle offline time for Active/Cooldown
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float offlineSeconds = Mathf.Max(0, now - lastStateTimestamp);

            if (currentState == BoostState.Active || currentState == BoostState.Cooldown)
            {
                stateTimer -= offlineSeconds;
                if (stateTimer <= 0f)
                {
                    // Transition without calling SetState (which would try to save)
                    if (currentState == BoostState.Active)
                    {
                        // Boost ended while offline -> go to Cooldown
                        // Calculate remaining cooldown time
                        float timeIntoNextState = -stateTimer; // How much time past Active end
                        float remainingCooldown = cooldownSeconds - timeIntoNextState;

                        if (remainingCooldown > 0f)
                        {
                            currentState = BoostState.Cooldown;
                            stateTimer = remainingCooldown;
                        }
                        else
                        {
                            // Cooldown also finished
                            currentState = BoostState.Charging;
                            stateTimer = 0f;
                        }
                        currentCharge = 0;
                        OnBoostEnded?.Invoke();
                    }
                    else if (currentState == BoostState.Cooldown)
                    {
                        // Cooldown finished while offline
                        currentState = BoostState.Charging;
                        stateTimer = 0f;
                    }
                }
            }

            UpdateUI();
            lastStateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        finally
        {
            _isLoading = false;
        }

        // Now we can request a save to persist the corrected state
        RequestSave();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            FlushSave();
            PlayerPrefs.Save();
        }
    }

    private void OnApplicationQuit()
    {
        FlushSave();
        PlayerPrefs.Save();
    }
}
