using UnityEngine;
using System;
using DG.Tweening;

/// <summary>
/// NitroRainController manages the Nitro Rain card effect.
/// 
/// MECHANIC:
/// 1. Player collects N nitro coins (N depends on NitroRain card level)
/// 2. After threshold reached, 30-second delay timer starts
/// 3. After delay ends, "Nitro Rain" begins for D seconds (D depends on level)
/// 4. During rain, NitroCoin prefabs spawn continuously at random positions
/// 5. After rain ends, counter resets and cycle can repeat
/// 
/// Level scaling:
/// Level 1: collect 3 -> rain 5s
/// Level 2: collect 4 -> rain 8s
/// Level 3: collect 5 -> rain 11s
/// Level 4: collect 6 -> rain 14s
/// Level 5: collect 7 -> rain 17s
/// Level 6+: collect 8 -> rain 20s
/// 
/// State Machine: Ready -> PendingDelay -> Raining -> Ready
/// </summary>
public class NitroRainController : MonoBehaviour
{
    public static NitroRainController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    // ==================== CONFIGURATION ====================

    [Header("Delay Settings")]
    [Tooltip("Seconds to wait after threshold reached before rain starts")]
    [SerializeField] private float delaySeconds = 30f;

    [Header("Spawn Settings")]
    [Tooltip("Reference to the NitroCoinSpawner for spawning during rain")]
    [SerializeField] private NitroCoinSpawner spawner;

    [Tooltip("Minimum seconds between spawns during rain")]
    [SerializeField] private float spawnIntervalMin = 0.2f;

    [Tooltip("Maximum seconds between spawns during rain")]
    [SerializeField] private float spawnIntervalMax = 0.35f;

    [Header("Spawn Animation")]
    [Tooltip("Duration of spawn scale animation")]
    [SerializeField] private float spawnAnimDuration = 0.2f;

    [Tooltip("How far above spawn point to start (for drop-in effect)")]
    [SerializeField] private float spawnDropHeight = 0.5f;

    [Header("Spawn Distribution")]
    [Tooltip("Number of spawn lanes to divide the X range into")]
    [SerializeField] private int spawnLaneCount = 5;

    [Header("Rain Pulse Animation")]
    [Tooltip("Enable subtle pulse animation on rain coins")]
    [SerializeField] private bool enableRainPulse = true;

    [Tooltip("Scale multiplier for pulse peak (1.05 = +5%)")]
    [SerializeField] private float pulseMultiplier = 1.05f;

    [Tooltip("Duration of one pulse cycle (up and down)")]
    [SerializeField] private float pulseDuration = 0.8f;

    [Tooltip("How many spawns before a lane can repeat (0 = pure random)")]
    [SerializeField] private int noRepeatWindow = 2;

    [Header("Robustness")]
    [Tooltip("Maximum seconds to extend rain duration while waiting for spawner")]
    [SerializeField] private float maxRainExtensionSeconds = 10f;

    // Level-based configuration (index = level)
    // [0] = level 0 (locked/unused), [1] = level 1, etc.
    // Duration scaling: L1=5s, L2=8s, L3=11s, L4=14s, L5=17s, L6=20s (+3s per level)
    private static readonly int[] RequiredCollects = { 0, 3, 4, 5, 6, 7, 8 };
    private static readonly float[] RainDurations = { 0f, 5f, 8f, 11f, 14f, 17f, 20f };

    // ==================== STATE ====================

    public enum NitroRainState
    {
        Ready,          // Counting nitro collections toward threshold
        PendingDelay,   // Threshold reached, waiting 30s before rain
        Raining         // Rain is active, spawning nitro coins
    }

    private NitroRainState _currentState = NitroRainState.Ready;
    public NitroRainState CurrentState => _currentState;

    // Collection tracking
    private int _collectedCount = 0;

    // Timers
    private float _stateEndTime = 0f;
    private float _nextSpawnTime = 0f;

    // Spawn distribution tracking
    private int[] _recentLanes;  // Circular buffer of recently used lanes
    private int _recentLaneIndex = 0;

    // Pre-allocated array for lane selection (avoids GC)
    private int[] _availableLanesBuffer;

    // For spawner rebind logic
    private bool _sceneLoadedSubscribed = false;
    private bool _hasLoggedSpawnerWarningThisRain = false;
    private float _lastSpawnerRebindAttemptTime = 0f;
    private const float SpawnerRebindInterval = 1.5f; // seconds

    // Rain extension tracking
    private float _rainExtensionAccumulated = 0f;

    // ==================== BOOST COORDINATION ====================
    // When Boost Mode is Active, rain must not run. If rain was triggered
    // while boost is active, _rainQueuedLevel stores the card level so
    // rain can auto-start once boost finishes.
    private int _rainQueuedLevel = 0;
    private bool _subscribedToBoost = false;

    // ==================== PUBLIC PROPERTIES ====================

    /// <summary>
    /// Returns true if currently raining (spawning nitro coins).
    /// </summary>
    public bool IsRaining => _currentState == NitroRainState.Raining;

    /// <summary>
    /// Returns true if in the 30s delay period before rain.
    /// </summary>
    public bool IsPendingDelay => _currentState == NitroRainState.PendingDelay;

    /// <summary>
    /// Returns true if ready and counting collections.
    /// </summary>
    public bool IsReady => _currentState == NitroRainState.Ready;

    /// <summary>
    /// Remaining seconds in the delay period (0 if not pending).
    /// </summary>
    public float RemainingDelayTime => IsPendingDelay ? Mathf.Max(0f, _stateEndTime - Time.time) : 0f;

    /// <summary>
    /// Remaining seconds of rain (0 if not raining).
    /// </summary>
    public float RemainingRainTime => IsRaining ? Mathf.Max(0f, _stateEndTime - Time.time) : 0f;

    /// <summary>
    /// Current collected count toward threshold.
    /// </summary>
    public int CollectedCount => _collectedCount;

    /// <summary>
    /// Required collections for current card level.
    /// </summary>
    public int RequiredCount => GetRequiredCollectsForLevel(GetNitroRainCardLevel());

    /// <summary>
    /// Progress toward threshold (0..1).
    /// </summary>
    public float CurrentProgress
    {
        get
        {
            int required = RequiredCount;
            if (required <= 0) return 0f;
            return Mathf.Clamp01((float)_collectedCount / required);
        }
    }

    /// <summary>
    /// Current NitroRain card level (0 if locked).
    /// </summary>
    public int CurrentCardLevel => GetNitroRainCardLevel();

    // ==================== EVENTS ====================

    /// <summary>
    /// Fired when threshold is reached and delay starts.
    /// Parameters: delayDuration
    /// </summary>
    public event Action<float> OnDelayStarted;

    /// <summary>
    /// Fired when rain begins.
    /// Parameters: rainDuration, cardLevel
    /// </summary>
    public event Action<float, int> OnRainStarted;

    /// <summary>
    /// Fired when rain ends.
    /// </summary>
    public event Action OnRainEnded;

    // ==================== UNITY LIFECYCLE ====================

    private void Awake()
    {
        // Singleton setup
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

    private void OnDisable()
    {
        UnsubscribeSceneLoaded();
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeSceneLoaded();
        UnsubscribeFromBoost();
    }

    private void SubscribeSceneLoaded()
    {
        if (!_sceneLoadedSubscribed)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneLoadedSubscribed = true;
        }
    }
    private void UnsubscribeSceneLoaded()
    {
        if (_sceneLoadedSubscribed)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            _sceneLoadedSubscribed = false;
        }
    }
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Defer spawner lookup to avoid FindObjectOfType during scene load
        _hasLoggedSpawnerWarningThisRain = false;
        _lastSpawnerRebindAttemptTime = 0f;
    }

    // ==================== SPAWNER REBIND ====================
    /// <summary>
    /// Ensures NitroCoinSpawner reference is valid. Returns true if found and enabled.
    /// Uses cached reference when possible to avoid expensive FindObjectOfType.
    /// </summary>
    private bool EnsureSpawnerReference(bool logSuccess)
    {
        if (spawner != null && spawner.isActiveAndEnabled)
            return true;

        // Only search if we don't have a valid reference
        var found = FindObjectOfType<NitroCoinSpawner>(true);
        if (found != null && found.isActiveAndEnabled)
        {
            spawner = found;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logSuccess)
                Debug.Log($"[NitroRain] NitroCoinSpawner rebound: {spawner.name}");
#endif
            return true;
        }
        return false;
    }

    private void Start()
    {
        // Try to auto-find spawner if not assigned
        EnsureSpawnerReference(true);
        // Initialize spawn distribution tracking
        InitializeSpawnLanes();
        // Subscribe to BoostModeController events for mutual exclusion
        TrySubscribeToBoost();
    }

    private void InitializeSpawnLanes()
    {
        int laneCount = Mathf.Max(1, spawnLaneCount);

        if (noRepeatWindow > 0)
        {
            _recentLanes = new int[noRepeatWindow];
            for (int i = 0; i < _recentLanes.Length; i++)
            {
                _recentLanes[i] = -1; // -1 means "no lane used yet"
            }
            _recentLaneIndex = 0;
        }

        // Pre-allocate buffer for lane selection (avoids GC during spawning)
        _availableLanesBuffer = new int[laneCount];
    }

    private void Update()
    {
        // Lazy-bind to BoostModeController if it wasn't ready at Start()
        if (!_subscribedToBoost)
            TrySubscribeToBoost();

        float now = Time.time;

        // Robust spawner rebind during rain
        if (IsRaining && (spawner == null || !spawner.isActiveAndEnabled))
        {
            if (!_hasLoggedSpawnerWarningThisRain)
            {
                Debug.LogWarning("[NitroRain] Cannot spawn: NitroCoinSpawner reference is missing!");
                _hasLoggedSpawnerWarningThisRain = true;
            }
            // Try to rebind every SpawnerRebindInterval seconds
            if (now - _lastSpawnerRebindAttemptTime > SpawnerRebindInterval)
            {
                if (EnsureSpawnerReference(true))
                {
                    _hasLoggedSpawnerWarningThisRain = false; // Reset warning for next loss
                }
                _lastSpawnerRebindAttemptTime = now;
            }
            // Extend rain duration until spawner is found (with cap)
            if (spawner == null || !spawner.isActiveAndEnabled)
            {
                if (_rainExtensionAccumulated < maxRainExtensionSeconds)
                {
                    float extensionThisFrame = Time.deltaTime;
                    _stateEndTime += extensionThisFrame;
                    _rainExtensionAccumulated += extensionThisFrame;
                }
                // If max extension reached, rain will end naturally
            }
        }

        switch (_currentState)
        {
            case NitroRainState.Ready:
                // Nothing to update, waiting for OnNitroCollected calls
                break;

            case NitroRainState.PendingDelay:
                // Check if delay has ended
                if (now >= _stateEndTime)
                {
                    // Gate: if Boost Mode is active, queue rain instead of starting it
                    if (IsBoostBlocking())
                    {
                        int lvl = GetNitroRainCardLevel();
                        _rainQueuedLevel = lvl >= 1 ? lvl : 1;
                        _currentState = NitroRainState.Ready;
                        _collectedCount = 0;
                        _stateEndTime = 0f;
                        Debug.Log($"[NitroRain] Delay ended but Boost active — rain queued at level {_rainQueuedLevel}.");
                    }
                    else
                    {
                        StartRain();
                    }
                }
                break;

            case NitroRainState.Raining:
                // Spawn nitro coins at intervals
                if (now >= _nextSpawnTime)
                {
                    SpawnRainNitroCoin();
                    ScheduleNextSpawn();
                }

                // Check if rain has ended
                if (now >= _stateEndTime)
                {
                    EndRain();
                }
                break;
        }
    }

    // ==================== PUBLIC METHODS ====================

    /// <summary>
    /// Called when player collects nitro coins.
    /// Should be called from CardManager.NotifyNitroCollected().
    /// </summary>
    public void OnNitroCollected(int amount = 1)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int level = GetNitroRainCardLevel();
        int required = GetRequiredCollectsForLevel(level);
        Debug.Log($"[NitroRain] OnNitroCollected({amount}) | State: {_currentState} | CardLevel: {level} | Collected: {_collectedCount}/{required} | Will process: {_currentState == NitroRainState.Ready && level >= 1}");
#endif

        // Only count in Ready state
        if (_currentState != NitroRainState.Ready)
        {
            return;
        }

        // Check if NitroRain card is unlocked
        int levelCheck = GetNitroRainCardLevel();
        if (levelCheck < 1)
        {
            return;
        }

        // Add to collected count
        _collectedCount += amount;

        int requiredCheck = GetRequiredCollectsForLevel(levelCheck);

        // Check if threshold reached
        if (_collectedCount >= requiredCheck)
        {
            StartDelay(levelCheck);
        }
    }

    /// <summary>
    /// Force start rain (for testing).
    /// </summary>
    public void ForceStartRain()
    {
        int level = GetNitroRainCardLevel();
        if (level < 1) level = 1; // Default to level 1 for testing
        StartRain(level);
    }

    /// <summary>
    /// Reset to Ready state (for testing).
    /// </summary>
    public void Reset()
    {
        _currentState = NitroRainState.Ready;
        _collectedCount = 0;
        _stateEndTime = 0f;
        _nextSpawnTime = 0f;
        _rainExtensionAccumulated = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[NitroRain] Reset to Ready state.");
#endif
    }

    // ==================== PRIVATE METHODS ====================

    private int GetNitroRainCardLevel()
    {
        if (CardManager.Instance == null)
            return 0;

        CardDefinition card = CardManager.Instance.GetCard(CardType.NitroRain);
        return card != null ? card.currentLevel : 0;
    }

    private int GetRequiredCollectsForLevel(int level)
    {
        if (level <= 0) return int.MaxValue; // Can never reach if locked
        int index = Mathf.Clamp(level, 0, RequiredCollects.Length - 1);
        return RequiredCollects[index];
    }

    private float GetRainDurationForLevel(int level)
    {
        if (level <= 0) return 0f;
        int index = Mathf.Clamp(level, 0, RainDurations.Length - 1);
        return RainDurations[index];
    }

    private void StartDelay(int level)
    {
        // Gate: if Boost Mode is active, queue rain instead of starting delay
        if (IsBoostBlocking())
        {
            _rainQueuedLevel = level;
            _currentState = NitroRainState.Ready;
            _collectedCount = 0; // threshold was met, reset counter
            Debug.Log($"[NitroRain] Boost active — rain queued at level {level} (will start after boost).");
            return;
        }

        _currentState = NitroRainState.PendingDelay;
        _stateEndTime = Time.time + delaySeconds;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int required = GetRequiredCollectsForLevel(level);
        Debug.Log($"[NitroRain] Threshold reached (collected {_collectedCount}/{required}). Starting {delaySeconds}s delay.");
#endif

        OnDelayStarted?.Invoke(delaySeconds);
    }

    private void StartRain(int level = -1)
    {
        if (level < 1)
        {
            level = GetNitroRainCardLevel();
        }

        float duration = GetRainDurationForLevel(level);

        _currentState = NitroRainState.Raining;
        _stateEndTime = Time.time + duration;
        _rainExtensionAccumulated = 0f; // Reset extension tracker

        // Schedule first spawn immediately
        _nextSpawnTime = Time.time;

        // Reset spawn lane tracking for fresh distribution
        if (_recentLanes != null)
        {
            for (int i = 0; i < _recentLanes.Length; i++)
            {
                _recentLanes[i] = -1;
            }
            _recentLaneIndex = 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NitroRain] RAIN STARTED for {duration}s (Level {level}).");
#endif

        OnRainStarted?.Invoke(duration, level);
    }

    private void EndRain()
    {
        _currentState = NitroRainState.Ready;
        _collectedCount = 0;
        _stateEndTime = 0f;
        _nextSpawnTime = 0f;
        _rainExtensionAccumulated = 0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[NitroRain] RAIN ENDED. Resetting progress.");
#endif

        OnRainEnded?.Invoke();
    }

    private void SpawnRainNitroCoin()
    {
        // Guard: police chase active — skip rain spawns during minigame
        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
            return;

        if (spawner == null || !spawner.isActiveAndEnabled)
        {
            // Warning is handled in Update, do not spam here
            return;
        }

        if (spawner.nitroCoinPrefab == null || spawner.spawnTop == null)
        {
            Debug.LogWarning("[NitroRain] Cannot spawn: Spawner prefab or spawnTop is null!");
            return;
        }

        // Get spawn position using lane-based distribution
        float x = GetDistributedSpawnX();
        float y = spawner.spawnTop.position.y + spawnDropHeight;
        float z = spawner.spawnTop.position.z;
        Vector3 startPos = new Vector3(x, y, z);
        Vector3 targetPos = new Vector3(x, spawner.spawnTop.position.y, z);

        // Instantiate at start position with scale 0
        GameObject obj = Instantiate(spawner.nitroCoinPrefab, startPos, Quaternion.identity);
        obj.transform.localScale = Vector3.zero;

        // Set despawn Z from spawner
        NitroCoin coin = obj.GetComponent<NitroCoin>();
        if (coin != null && spawner.spawnBottom != null)
        {
            coin.despawnZ = spawner.spawnBottom.position.z;
        }

        // Store the prefab's original scale to use as baseline
        Vector3 originalScale = spawner.nitroCoinPrefab.transform.localScale;

        // Animate spawn: scale up + drop down with ease-out
        Sequence spawnSeq = DOTween.Sequence().SetLink(obj, LinkBehaviour.KillOnDestroy);
        spawnSeq.Append(obj.transform.DOScale(originalScale, spawnAnimDuration).SetEase(Ease.OutBack));
        spawnSeq.Join(obj.transform.DOMove(targetPos, spawnAnimDuration).SetEase(Ease.OutQuad));

        // After spawn animation, start subtle pulse if enabled
        if (enableRainPulse)
        {
            spawnSeq.OnComplete(() => ApplyRainPulse(obj.transform, originalScale));
        }
    }

    /// <summary>
    /// Applies a subtle looping pulse animation to a rain coin.
    /// The pulse oscillates between originalScale and originalScale * pulseMultiplier.
    /// </summary>
    private void ApplyRainPulse(Transform coinTf, Vector3 originalScale)
    {
        if (coinTf == null) return;

        Vector3 pulseScale = originalScale * pulseMultiplier;

        // Create pulse tween with Yoyo loop, tied to the object's lifetime
        coinTf.DOScale(pulseScale, pulseDuration * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(coinTf.gameObject, LinkBehaviour.KillOnDestroy);
    }

    /// <summary>
    /// Gets an X position using lane-based distribution to avoid consecutive spawns at the same spot.
    /// Uses pre-allocated buffer to avoid GC allocations.
    /// </summary>
    private float GetDistributedSpawnX()
    {
        float rangeWidth = spawner.maxX - spawner.minX;
        int laneCount = Mathf.Max(1, spawnLaneCount);

        // If no lanes or no repeat prevention, use pure random
        if (laneCount <= 1 || noRepeatWindow <= 0 || _recentLanes == null)
        {
            return UnityEngine.Random.Range(spawner.minX, spawner.maxX);
        }

        // Ensure buffer is valid
        if (_availableLanesBuffer == null || _availableLanesBuffer.Length < laneCount)
        {
            _availableLanesBuffer = new int[laneCount];
        }

        // Build list of available lanes (not recently used) - using pre-allocated buffer
        int availableCount = 0;

        for (int lane = 0; lane < laneCount; lane++)
        {
            bool isRecent = false;
            for (int i = 0; i < _recentLanes.Length; i++)
            {
                if (_recentLanes[i] == lane)
                {
                    isRecent = true;
                    break;
                }
            }
            if (!isRecent)
            {
                _availableLanesBuffer[availableCount++] = lane;
            }
        }

        // Pick from available lanes, or fallback to any lane if all are recent
        int chosenLane;
        if (availableCount > 0)
        {
            chosenLane = _availableLanesBuffer[UnityEngine.Random.Range(0, availableCount)];
        }
        else
        {
            chosenLane = UnityEngine.Random.Range(0, laneCount);
        }

        // Record this lane as recently used
        _recentLanes[_recentLaneIndex] = chosenLane;
        _recentLaneIndex = (_recentLaneIndex + 1) % _recentLanes.Length;

        // Calculate X position within the chosen lane (with slight randomness within lane)
        float laneWidth = rangeWidth / laneCount;
        float laneStart = spawner.minX + (chosenLane * laneWidth);
        float laneEnd = laneStart + laneWidth;

        // Add slight randomness within the lane (80% of lane width, centered)
        float innerMargin = laneWidth * 0.1f;
        return UnityEngine.Random.Range(laneStart + innerMargin, laneEnd - innerMargin);
    }

    private void ScheduleNextSpawn()
    {
        float interval = UnityEngine.Random.Range(spawnIntervalMin, spawnIntervalMax);
        _nextSpawnTime = Time.time + interval;
    }

    // ==================== BOOST COORDINATION ====================

    /// <summary>
    /// Returns true if BoostModeController is in the Active state,
    /// meaning rain must not run concurrently.
    /// </summary>
    private bool IsBoostBlocking()
    {
        return BoostModeController.Instance != null && BoostModeController.Instance.IsBoostActive;
    }

    private void TrySubscribeToBoost()
    {
        if (_subscribedToBoost) return;
        if (BoostModeController.Instance == null) return;

        BoostModeController.Instance.OnBoostStarted += HandleBoostStarted;
        BoostModeController.Instance.OnBoostEnded += HandleBoostEnded;
        _subscribedToBoost = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[NitroRain] Subscribed to BoostModeController events for mutual exclusion.");
#endif
    }

    private void UnsubscribeFromBoost()
    {
        if (!_subscribedToBoost) return;
        if (BoostModeController.Instance != null)
        {
            BoostModeController.Instance.OnBoostStarted -= HandleBoostStarted;
            BoostModeController.Instance.OnBoostEnded -= HandleBoostEnded;
        }
        _subscribedToBoost = false;
    }

    /// <summary>
    /// Called when Boost Mode becomes Active.
    /// If rain is currently running or pending, interrupt and queue it.
    /// </summary>
    private void HandleBoostStarted(float duration)
    {
        if (_currentState == NitroRainState.Raining)
        {
            int level = GetNitroRainCardLevel();
            _rainQueuedLevel = level >= 1 ? level : 1;

            // Interrupt rain: reset state without going through full EndRain
            // so the OnRainEnded event fires (VFX/UI can react)
            _currentState = NitroRainState.Ready;
            _collectedCount = 0;
            _stateEndTime = 0f;
            _nextSpawnTime = 0f;
            _rainExtensionAccumulated = 0f;
            OnRainEnded?.Invoke();

            Debug.Log($"[NitroRain] Rain INTERRUPTED by Boost Mode. Queued at level {_rainQueuedLevel}.");
        }
        else if (_currentState == NitroRainState.PendingDelay)
        {
            int level = GetNitroRainCardLevel();
            _rainQueuedLevel = level >= 1 ? level : 1;

            // Cancel delay
            _currentState = NitroRainState.Ready;
            _collectedCount = 0;
            _stateEndTime = 0f;

            Debug.Log($"[NitroRain] Delay CANCELLED by Boost Mode. Queued at level {_rainQueuedLevel}.");
        }
    }

    /// <summary>
    /// Called when Boost Mode finishes (transitions out of Active).
    /// If rain was queued, start it now.
    /// </summary>
    private void HandleBoostEnded()
    {
        if (_rainQueuedLevel > 0)
        {
            int level = _rainQueuedLevel;
            _rainQueuedLevel = 0;
            Debug.Log($"[NitroRain] Boost ended — starting queued rain at level {level}.");
            StartRain(level);
        }
    }

    // ==================== DEBUG ====================

#if UNITY_EDITOR
    private void OnGUI()
    {
        // Uncomment for debug overlay
        /*
        GUILayout.BeginArea(new Rect(10, 160, 300, 120));
        GUILayout.Label($"NitroRain State: {_currentState}");
        GUILayout.Label($"Card Level: {CurrentCardLevel}");
        GUILayout.Label($"Progress: {CollectedCount}/{RequiredCount}");
        if (IsPendingDelay)
            GUILayout.Label($"Delay Left: {RemainingDelayTime:F1}s");
        if (IsRaining)
            GUILayout.Label($"Rain Left: {RemainingRainTime:F1}s");
        GUILayout.EndArea();
        */
    }
#endif
}
