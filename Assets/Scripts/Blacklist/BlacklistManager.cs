using System;
using UnityEngine;

/// <summary>
/// Core Blacklist progression manager. Tracks current tier, evaluates missions,
/// manages baseline snapshots, and advances progression.
///
/// Singleton — place on a persistent (DontDestroyOnLoad) GameObject.
/// Assign tier SOs in Inspector in order: index 0 = Blacklist #6, index 5 = Blacklist #1.
/// </summary>
public class BlacklistManager : MonoBehaviour
{
    public static BlacklistManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Tier Definitions (ordered: BL#6 first → BL#1 last)")]
    [Tooltip("Assign 6 BlacklistTierSO assets. Index 0 = BL#6, Index 5 = BL#1.")]
    [SerializeField] private BlacklistTierSO[] tierDefinitions;

    /// <summary>Fired when any mission progress changes. UI subscribes to refresh.</summary>
    public event Action OnProgressChanged;

    /// <summary>Fired when the active tier changes (including campaign complete).</summary>
    public event Action OnTierChanged;

    // ─── Runtime state ───

    private BlacklistSaveData _save;

    public BlacklistSaveData SaveData => _save;
    public bool CampaignComplete => _save.campaignComplete;

    /// <summary>Currently active tier SO. Null if campaign is complete.</summary>
    public BlacklistTierSO ActiveTier { get; private set; }

    // ─── Lifecycle ───

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        _save = BlacklistSaveData.LoadFromPrefs();
        RefreshActiveTier();
    }

    private void OnEnable()
    {
        SaveSystem.OnGameLoaded += OnGameLoaded;
    }

    private void OnDisable()
    {
        SaveSystem.OnGameLoaded -= OnGameLoaded;
    }

    private void OnGameLoaded()
    {
        int previousTier = _save.currentTierIndex;
        bool wasCampaignComplete = _save.campaignComplete;

        _save = BlacklistSaveData.LoadFromPrefs();
        RefreshActiveTier();

        // Ensure stat tracker has late-bound subscriptions
        if (BlacklistStatTracker.Instance != null)
            BlacklistStatTracker.Instance.LateSubscribe();

        // Only fire OnTierChanged if the tier actually changed — prevents false
        // force-submits in RankingService on every scene return.
        if (_save.currentTierIndex != previousTier || _save.campaignComplete != wasCampaignComplete)
        {
            Debug.Log($"[BlacklistManager] Tier changed during load: {previousTier} → {_save.currentTierIndex} (campaign={_save.campaignComplete})");
            OnTierChanged?.Invoke();
        }

        OnProgressChanged?.Invoke();
    }

    // ─── Tier resolution ───

    private void RefreshActiveTier()
    {
        ActiveTier = GetTierSO(_save.currentTierIndex);
    }

    /// <summary>Finds the tier SO matching the given tier index (6..1). Returns null if not found.</summary>
    public BlacklistTierSO GetTierSO(int tierIndex)
    {
        if (tierDefinitions == null) return null;
        foreach (var t in tierDefinitions)
        {
            if (t != null && t.tierIndex == tierIndex)
                return t;
        }
        return null;
    }

    /// <summary>Returns the tier index (6..1) whose reward car matches the given carId, or -1 if none.</summary>
    public int GetTierIndexForCar(string carId)
    {
        if (tierDefinitions == null || string.IsNullOrEmpty(carId)) return -1;
        foreach (var t in tierDefinitions)
        {
            if (t != null && t.rewardCar != null && t.rewardCar.carId == carId)
                return t.tierIndex;
        }
        return -1;
    }

    // ─── Mission progress evaluation ───

    /// <summary>
    /// Returns the current progress value for a mission in the active tier.
    /// For delta missions: raw = lifetimeValue − baseline.
    /// For absolute missions: raw = current state.
    /// Clamped to [0, target].
    /// </summary>
    public double GetMissionProgress(int missionIndex)
    {
        if (ActiveTier == null || missionIndex < 0 || missionIndex >= ActiveTier.missions.Length)
            return 0;

        var mission = ActiveTier.missions[missionIndex];
        if (mission == null) return 0;

        var tracker = BlacklistStatTracker.Instance;
        if (tracker == null) return 0;

        double raw;

        if (mission.mode == BlacklistMissionMode.DeltaFromTierStart)
        {
            double lifetime = tracker.GetLifetimeValue(mission.missionType);
            double baseline = GetBaselineValue(mission.missionType);
            raw = lifetime - baseline;
        }
        else // AbsoluteState
        {
            raw = tracker.GetLifetimeValue(mission.missionType);
        }

        return Math.Max(0, Math.Min(raw, mission.targetValue));
    }

    /// <summary>Returns the target value for a mission in the active tier.</summary>
    public double GetMissionTarget(int missionIndex)
    {
        if (ActiveTier == null || missionIndex < 0 || missionIndex >= ActiveTier.missions.Length)
            return 1;
        return ActiveTier.missions[missionIndex].targetValue;
    }

    /// <summary>Returns true if a specific mission is complete.</summary>
    public bool IsMissionComplete(int missionIndex)
    {
        if (_save.missionStates[missionIndex] >= BlacklistSaveData.STATE_COMPLETED)
            return true;

        double progress = GetMissionProgress(missionIndex);
        double target = GetMissionTarget(missionIndex);
        if (progress >= target)
        {
            _save.missionStates[missionIndex] = BlacklistSaveData.STATE_COMPLETED;
            Save();
            return true;
        }
        return false;
    }

    /// <summary>Returns true if all 5 missions in the current tier are complete.</summary>
    public bool AreAllMissionsComplete()
    {
        if (ActiveTier == null) return false;
        for (int i = 0; i < 5; i++)
        {
            if (!IsMissionComplete(i))
                return false;
        }
        return true;
    }

    /// <summary>Returns the mission state (Pending/Completed/Claimed).</summary>
    public int GetMissionState(int missionIndex)
    {
        if (missionIndex < 0 || missionIndex >= 5) return 0;
        // Auto-check for completion even if not yet marked
        IsMissionComplete(missionIndex);
        return _save.missionStates[missionIndex];
    }

    // ─── Tier advancement ───

    /// <summary>
    /// Advances to the next blacklist tier. Call when player presses TakeTheCarButton.
    /// Takes a new baseline snapshot for the next tier's delta missions.
    /// </summary>
    public void AdvanceToNextTier()
    {
        if (_save.campaignComplete) return;

        int nextTier = _save.currentTierIndex - 1; // 6 → 5 → 4 → ... → 1

        if (nextTier < 1)
        {
            // Campaign is complete
            _save.campaignComplete = true;
            _save.currentTierIndex = 1; // Stay visually on #1
            Save();
            RefreshActiveTier();
            OnTierChanged?.Invoke();
            return;
        }

        _save.currentTierIndex = nextTier;

        // Reset mission states for new tier
        for (int i = 0; i < 5; i++)
            _save.missionStates[i] = BlacklistSaveData.STATE_PENDING;

        // Take baseline snapshot
        TakeBaselineSnapshot();

        Save();
        RefreshActiveTier();

        // Goal complete / tier advance SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayGoalComplete();

        OnTierChanged?.Invoke();
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Snapshot current lifetime counters as the baseline for the new tier.
    /// Called automatically when a tier becomes active.
    /// </summary>
    public void TakeBaselineSnapshot()
    {
        var tracker = BlacklistStatTracker.Instance;
        if (tracker == null)
        {
            Debug.LogWarning("[BlacklistManager] Cannot take snapshot — BlacklistStatTracker not found.");
            return;
        }

        _save.baselineGoldEarned = CurrencyManager.Instance != null ? CurrencyManager.Instance.totalMoneyEarned : 0;
        _save.baselineWorldNitroCollected = tracker.TotalWorldNitroCollected;
        _save.baselineRadarsDefused = tracker.TotalRadarsDefused;
        _save.baselineChestsOpened = tracker.TotalChestsOpened;
        _save.baselineBoostUses = tracker.TotalBoostUses;
        _save.baselinePoliceEscapes = tracker.TotalPoliceEscapes;
        _save.baselineNitroRainTriggers = tracker.TotalNitroRainTriggers;
        _save.baselineMagnetCoinsCollected = tracker.TotalMagnetCoinsCollected;
        _save.baselineTurboUses = tracker.TotalTurboUses;
    }

    /// <summary>
    /// If the save has no baseline yet (first play), take initial snapshot.
    /// Called once after LoadGame if tier is 6 and baselines are all zero.
    /// </summary>
    public void EnsureInitialBaseline()
    {
        if (_save.currentTierIndex == 6
            && _save.baselineGoldEarned == 0
            && _save.baselineWorldNitroCollected == 0
            && _save.baselineRadarsDefused == 0
            && _save.baselineChestsOpened == 0
            && _save.baselineBoostUses == 0
            && _save.baselinePoliceEscapes == 0
            && _save.baselineNitroRainTriggers == 0
            && _save.baselineMagnetCoinsCollected == 0
            && _save.baselineTurboUses == 0
            && !PlayerPrefs.HasKey("Save_BlacklistCampaign"))
        {
            TakeBaselineSnapshot();
            Save();
        }
    }

    // ─── Baseline value lookups ───

    private double GetBaselineValue(BlacklistMissionType type)
    {
        switch (type)
        {
            case BlacklistMissionType.EarnGold: return _save.baselineGoldEarned;
            case BlacklistMissionType.CollectWorldNitro: return _save.baselineWorldNitroCollected;
            case BlacklistMissionType.DefuseRadars: return _save.baselineRadarsDefused;
            case BlacklistMissionType.OpenChests: return _save.baselineChestsOpened;
            case BlacklistMissionType.UseBoost: return _save.baselineBoostUses;
            case BlacklistMissionType.EscapePolice: return _save.baselinePoliceEscapes;
            case BlacklistMissionType.TriggerNitroRain: return _save.baselineNitroRainTriggers;
            case BlacklistMissionType.NitroMagnetCollect: return _save.baselineMagnetCoinsCollected;
            case BlacklistMissionType.UseTurbo: return _save.baselineTurboUses;
            default: return 0;
        }
    }

    // ─── Save helper ───

    public void Save()
    {
        _save.SaveToPrefs();
    }

    // ─── Periodic progress check (called from panel controller) ───

    /// <summary>
    /// Evaluates all missions and fires OnProgressChanged if any state changed.
    /// Call this periodically (e.g. every 0.5s) from the UI controller.
    /// </summary>
    public void EvaluateAll()
    {
        if (ActiveTier == null) return;

        bool anyChanged = false;
        for (int i = 0; i < 5; i++)
        {
            int oldState = _save.missionStates[i];
            IsMissionComplete(i); // may promote to COMPLETED
            if (_save.missionStates[i] != oldState)
                anyChanged = true;
        }

        if (anyChanged)
        {
            Save();
            OnProgressChanged?.Invoke();
        }
    }

    // ─── DEBUG / TESTING ───
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    /// <summary>
    /// [DEBUG] Completes the next PENDING mission in the current tier.
    /// Uses the same state transition as the real system (STATE_PENDING → STATE_COMPLETED),
    /// saves to PlayerPrefs, and fires OnProgressChanged so UI updates normally.
    /// Returns the index of the mission that was completed, or -1 if none remain.
    /// </summary>
    [ContextMenu("Complete Next Mission")]
    public int DebugCompleteNextMission()
    {
        if (ActiveTier == null)
        {
            Debug.LogWarning("[Blacklist-DEBUG] No active tier (campaign complete?).");
            return -1;
        }

        for (int i = 0; i < _save.missionStates.Length; i++)
        {
            if (_save.missionStates[i] == BlacklistSaveData.STATE_PENDING)
            {
                _save.missionStates[i] = BlacklistSaveData.STATE_COMPLETED;
                Save();
                OnProgressChanged?.Invoke();
                Debug.Log($"[Blacklist-DEBUG] Mission {i} forced to COMPLETED (tier {_save.currentTierIndex}).");
                return i;
            }
        }

        Debug.Log("[Blacklist-DEBUG] All missions already completed.");
        return -1;
    }

#endif
}
