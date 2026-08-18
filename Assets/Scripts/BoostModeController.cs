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
    private CanvasGroup boostBarCanvasGroup;

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
    /// Returns boost parameters (multiplier, duration, cooldown, maxCharge) for the given card level.
    /// Balanced tuning: moderate multipliers, duration scales up, cooldown scales DOWN.
    ///   L1: 3x mult, 6s dur, 60s cd, 5 charge
    ///   L2: 5x mult, 8s dur, 55s cd, 7 charge
    ///   L3: 8x mult, 10s dur, 48s cd, 9 charge
    ///   L4: 12x mult, 12s dur, 40s cd, 12 charge
    ///   L5: 16x mult, 14s dur, 32s cd, 15 charge
    ///   L6: 20x mult, 16s dur, 25s cd, 18 charge
    /// </summary>
    public static (float multiplier, float cooldown, int maxCharge, float duration) GetBoostParamsForLevel(int level)
    {
        if (level <= 0) return (1f, 60f, 4, 6f);

        // Clamp to max level 6
        level = Mathf.Clamp(level, 1, 6);

        // Tuning tables (index 0 = L1)
        float[] multipliers = { 3f, 5f, 8f, 12f, 16f, 20f };
        float[] durations = { 8f, 8f, 10f, 12f, 14f, 16f };
        float[] cooldowns = { 45f, 55f, 48f, 40f, 32f, 25f };
        int[] charges = { 4, 7, 9, 12, 15, 18 };

        int idx = level - 1;
        return (multipliers[idx], cooldowns[idx], charges[idx], durations[idx]);
    }

    /// <summary>
    /// Legacy 3-tuple overload for backward compatibility. Returns (multiplier, cooldown, maxCharge).
    /// </summary>
    public static (float multiplier, float cooldown, int maxCharge) GetBoostParamsForLevelLegacy(int level)
    {
        var p = GetBoostParamsForLevel(level);
        return (p.multiplier, p.cooldown, p.maxCharge);
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

            // DontDestroyOnLoad only works on ROOT GameObjects.
            // BoostModeController lives as a child of the Nitro prefab; detach first.
            if (transform.parent != null)
            {
                Debug.Log($"[Boost] Detaching from parent '{transform.parent.name}' for DontDestroyOnLoad.");
                transform.SetParent(null);
            }

            DontDestroyOnLoad(gameObject);
            SubscribeSceneLoaded();
            Debug.Log($"[Boost] Instance assigned (ID={GetInstanceID()}), DontDestroyOnLoad applied.");
        }
        else if (Instance != this)
        {
            Debug.Log($"[Boost] Duplicate detected (ID={GetInstanceID()}), destroying. Keeping ID={Instance.GetInstanceID()}");
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        FlushSave(); // Persist charge/timer before this instance is destroyed
        if (Instance == this) Instance = null;
        UnsubscribeSceneLoaded();
        UnsubscribeFromSaveSystem();
        UnsubscribeTopBarAnimator();
        UnsubscribeFromCards();
    }

    private void OnDisable()
    {
        UnsubscribeSceneLoaded();
        UnsubscribeFromSaveSystem();
        UnsubscribeTopBarAnimator();
        UnsubscribeFromCards();
    }

    private void OnEnable()
    {
        SubscribeToSaveSystem();
    }

    private void Start()
    {
        TryBindUI();
        SubscribeTopBarAnimator();
        SubscribeToCards();
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

    // =============== CARD CHANGE SUBSCRIPTION ===============

    private bool _cardsSubscribed = false;

    private void SubscribeToCards()
    {
        if (_cardsSubscribed) return;
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCardsChanged += OnCardsChanged;
            _cardsSubscribed = true;
        }
    }

    private void UnsubscribeFromCards()
    {
        if (!_cardsSubscribed) return;
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsChanged -= OnCardsChanged;
        _cardsSubscribed = false;
    }

    /// <summary>
    /// Called when CardManager detects card copies/level changes (e.g. chest reward, daily offer).
    /// This lets BoostModeController detect mid-scene card unlocks immediately.
    /// </summary>
    private void OnCardsChanged()
    {
        Debug.Log("[Boost] OnCardsChanged received — refreshing unlock state.");
        RefreshUnlockState();
    }

    /// <summary>
    /// Called when SaveSystem finishes loading game data.
    /// Refreshes unlock state since card levels are now loaded.
    /// </summary>
    private void OnGameLoaded()
    {
        Debug.Log("[Boost] OnGameLoaded received. Refreshing unlock state...");
        SubscribeToCards(); // Retry if CardManager wasn't available at Start
        RefreshUnlockState();
        UpdateUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _warnedMissingUI = false; // Reset warning for new scene

        // Clear stale UI refs from the previous scene so TryBindUI re-finds them.
        boostBarSlider = null;
        boostBarRoot = null;
        boostBarCanvasGroup = null;
        _topBarSubscribed = false; // TopBarAnimator is a new instance in the new scene

        TryBindUI();
        SubscribeTopBarAnimator();
        SubscribeToCards();
        RefreshUnlockState();
        Debug.Log($"[Boost] OnSceneLoaded('{scene.name}'): state={currentState}, unlocked={isUnlocked}, charge={currentCharge}/{maxCharge}, UI bound={(boostBarSlider != null)}");
    }

    private bool _topBarSubscribed;

    private void SubscribeTopBarAnimator()
    {
        if (_topBarSubscribed) return;
        if (TopBarAnimator.Instance != null)
        {
            TopBarAnimator.Instance.OnCompactChanged += OnTopBarCompactChanged;
            _topBarSubscribed = true;

            // Exclude boost bar from TopBarAnimator's hideGroups so only
            // BoostModeController controls its SetActive/alpha.
            if (boostBarCanvasGroup != null)
                TopBarAnimator.Instance.ExcludeFromCompact(boostBarCanvasGroup);
        }
    }

    private void UnsubscribeTopBarAnimator()
    {
        if (!_topBarSubscribed) return;
        if (TopBarAnimator.Instance != null)
        {
            TopBarAnimator.Instance.OnCompactChanged -= OnTopBarCompactChanged;
            if (boostBarCanvasGroup != null)
                TopBarAnimator.Instance.IncludeInCompact(boostBarCanvasGroup);
        }
        _topBarSubscribed = false;
    }

    /// <summary>
    /// After TopBarAnimator finishes its compact/expand transition,
    /// reassert boost bar visibility based on current boost state.
    /// </summary>
    private void OnTopBarCompactChanged(bool compact)
    {
        UpdateUI();
    }

    private void TryBindUI()
    {
        if (boostBarSlider == null)
        {
            // Slider_BoostBar starts INACTIVE in the scene. GameObject.Find only
            // returns active objects, so we use Transform.Find from the Canvas root
            // which works regardless of active state.
            GameObject sliderObj = null;
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                var t = canvas.transform.Find("TopBar/Slider_BoostBar");
                if (t != null) sliderObj = t.gameObject;
            }

            // Fallback: try the old global find in case hierarchy differs
            if (sliderObj == null)
                sliderObj = GameObject.Find("Canvas/TopBar/Slider_BoostBar");

            if (sliderObj != null)
            {
                boostBarSlider = sliderObj.GetComponent<Slider>();
                boostBarRoot = sliderObj;
                Debug.Log($"[Boost] Bound BoostBar slider via Transform.Find: {sliderObj.name}");
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
        if (boostBarRoot != null && boostBarCanvasGroup == null)
        {
            boostBarCanvasGroup = boostBarRoot.GetComponent<CanvasGroup>();
            // If we found a new CG and TopBarAnimator is already subscribed,
            // exclude it so TopBarAnimator doesn't control our bar.
            if (boostBarCanvasGroup != null && _topBarSubscribed && TopBarAnimator.Instance != null)
                TopBarAnimator.Instance.ExcludeFromCompact(boostBarCanvasGroup);
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
            boostDurationSeconds = parameters.duration;
        }

        bool wasUnlocked = isUnlocked;
        isUnlocked = level >= 1;
        if (isUnlocked && currentState == BoostState.Locked)
        {
            Debug.Log($"[Boost] Card JUST UNLOCKED! Level={level}, wasUnlocked={wasUnlocked}. Transitioning Locked -> Charging.");
            SetState(BoostState.Charging);
        }
        else if (!isUnlocked)
        {
            SetState(BoostState.Locked);
        }
        UpdateUI();
        Debug.Log($"[Boost] RefreshUnlockState: owned={isUnlocked}, state={currentState}, level={level}, " +
                  $"charge={currentCharge}/{maxCharge}, duration={boostDurationSeconds}s, cooldown={cooldownSeconds}s, " +
                  $"UI bound={(boostBarSlider != null)}");
    }

    public void OnNitroCollected(int amount)
    {
        if (!isUnlocked)
        {
            Debug.Log("[Boost] Ignored nitro: state=Locked (card not owned)");
            return;
        }
        if (currentState == BoostState.Active || currentState == BoostState.Cooldown)
        {
            Debug.Log($"[Boost] Ignored nitro: state={currentState}");
            return;
        }
        if (currentState == BoostState.Charging)
        {
            currentCharge += chargePerNitro * amount;
            if (currentCharge > maxCharge) currentCharge = maxCharge;
            if (amount > 0) OnNitroChargeAccepted?.Invoke();
            Debug.Log($"[Boost] Nitro +{amount} => charge {currentCharge}/{maxCharge} (fill={Charge01:F2})");
            UpdateUI();
            InvokeChargeEvents();
            RequestSave(); // Persist charge progress so it survives scene transitions
            if (currentCharge >= maxCharge)
            {
                Debug.Log("[Boost] Charge FULL — transitioning to Ready/Active");
                SetState(BoostState.Ready);
            }
        }
        else if (currentState == BoostState.Ready)
        {
            Debug.Log("[Boost] Ignored nitro: state=Ready");
        }
    }

    private void InvokeChargeEvents()
    {
        OnBoostChargeChanged?.Invoke(currentCharge, maxCharge);
        OnChargeChanged01?.Invoke(Charge01);
    }

    private void UpdateUI()
    {
        bool stateAllows = isUnlocked && (currentState == BoostState.Charging
                                        || currentState == BoostState.Ready
                                        || currentState == BoostState.Active);

        // Also hide when the TopBar is in compact mode (panel open / police chase)
        // so the bar doesn't float in a collapsed header.
        bool topBarVisible = TopBarAnimator.Instance == null || !TopBarAnimator.Instance.IsCompact;
        bool show = stateAllows && topBarVisible;

        if (boostBarSlider != null)
        {
            if (currentState == BoostState.Active && boostDurationSeconds > 0f)
                boostBarSlider.value = Mathf.Clamp01(stateTimer / boostDurationSeconds);
            else
                boostBarSlider.value = Charge01;
        }

        if (boostBarRoot != null)
        {
            boostBarRoot.SetActive(show);
            // Restore CanvasGroup alpha — safety net in case something else set it to 0.
            if (show && boostBarCanvasGroup != null)
            {
                boostBarCanvasGroup.alpha = 1f;
                boostBarCanvasGroup.interactable = true;
                boostBarCanvasGroup.blocksRaycasts = true;
            }
        }
    }

    private void Update()
    {
        if (currentState == BoostState.Active || currentState == BoostState.Cooldown)
        {
            stateTimer -= Time.deltaTime;

            // Keep the slider draining in real-time while boost is active
            if (currentState == BoostState.Active && boostBarSlider != null && boostDurationSeconds > 0f)
                boostBarSlider.value = Mathf.Clamp01(stateTimer / boostDurationSeconds);

            if (stateTimer <= 0f)
            {
                if (currentState == BoostState.Active)
                {
                    EndBoost();
                }
                else if (currentState == BoostState.Cooldown)
                {
                    Debug.Log("[Boost] Cooldown COMPLETE — transitioning to Charging. Bar will show.");
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
        var oldState = currentState;
        currentState = newState;
        OnStateChanged?.Invoke(newState);
        lastStateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        switch (newState)
        {
            case BoostState.Locked:
                currentCharge = 0;
                UpdateUI();
                Debug.Log("[Boost] State: Locked (card not owned). Bar hidden.");
                break;
            case BoostState.Charging:
                UpdateUI();
                Debug.Log($"[Boost] State: {oldState} -> Charging. Bar visible, charge={currentCharge}/{maxCharge}.");
                break;
            case BoostState.Ready:
                UpdateUI();
                Debug.Log($"[Boost] State: Charging -> Ready. Bar FULL. Auto-starting boost.");
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
                Debug.Log($"[Boost] State: Active. Turbo effect running for {boostDurationSeconds}s. Bar draining.");
                OnBoostStarted?.Invoke(boostDurationSeconds);
                break;
            case BoostState.Cooldown:
                stateTimer = cooldownSeconds;

                // Apply Blacklist boost-cooldown discount if available
                var blClaim = BlacklistRewardClaimData.LoadFromPrefs();
                if (blClaim.HasBoostDiscount)
                {
                    float mult = blClaim.ConsumeBoostDiscount();
                    stateTimer *= mult;
                    Debug.Log($"[Boost] Blacklist cooldown discount applied: x{mult}. Timer: {stateTimer:F1}s (base {cooldownSeconds}s).");
                }

                UpdateUI();
                Debug.Log($"[Boost] State: Active -> Cooldown ({stateTimer:F1}s). Bar HIDDEN.");
                OnBoostCooldownStarted?.Invoke(stateTimer);
                break;
        }
        RequestSave();
    }

    private void StartBoost()
    {
        SetState(BoostState.Active);
    }

    /// <summary>
    /// DEBUG ONLY: Immediately skips the cooldown timer, transitioning to Charging.
    /// No-op if the current state is not Cooldown.
    /// </summary>
    public void DebugSkipCooldown()
    {
        if (currentState != BoostState.Cooldown)
        {
            Debug.Log($"[Boost] DebugSkipCooldown: Not in Cooldown (state={currentState}). No-op.");
            return;
        }

        stateTimer = 0f;
        SetState(BoostState.Charging);
        Debug.Log("[Boost] DebugSkipCooldown: Cooldown skipped, now Charging.");
    }

    /// <summary>
    /// DEBUG ONLY: Immediately fills charge and triggers boost regardless of current state.
    /// Does not permanently affect save data — state resets on restart.
    /// </summary>
    public void DebugForceBoost()
    {
        if (!isUnlocked)
        {
            // Temporarily unlock so boost can fire
            isUnlocked = true;
        }

        // If already active, skip (let it finish naturally)
        if (currentState == BoostState.Active)
        {
            Debug.Log("[Boost] DebugForceBoost: Boost already active.");
            return;
        }

        // Ensure parameters are up to date
        RefreshUnlockState();

        // Jump directly to Active
        currentCharge = maxCharge;
        SetState(BoostState.Active);

        Debug.Log($"[Boost] DebugForceBoost: Boost activated (duration:{boostDurationSeconds}s)");
    }

    private void EndBoost()
    {
        Debug.Log("[Boost] BOOST ENDED — transitioning to Cooldown. Bar will hide.");
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
