using System;
using System.Collections.Generic;
using UnityEngine;

public class ChestInventoryManager : MonoBehaviour
{
    public static ChestInventoryManager Instance;

    // ── Debug Instrumentation ─────────────────────────────────────────
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void DLog(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[ChestInvMgr][{name}#{GetInstanceID()}] t={Time.time:F2} rt={Time.realtimeSinceStartup:F2} f={Time.frameCount} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} | {msg}");
    }
    // ──────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [Serializable]
    public class ChestData
    {
        public ChestType chestType;
        public string chestName;
        public float unlockDurationSeconds;
        public ChestState state;
        public long unlockEndUtcTicks;  // UTC ticks when unlock finishes
        public bool halfTimeUsed;

        public ChestData() { }

        /// <summary>Remaining seconds based on real-time UTC clock.</summary>
        public float GetRemainingSeconds()
        {
            if (state != ChestState.Unlocking) return 0f;
            if (unlockEndUtcTicks <= 0) return unlockDurationSeconds;
            var remaining = new DateTime(unlockEndUtcTicks, DateTimeKind.Utc) - DateTime.UtcNow;
            return Mathf.Max(0f, (float)remaining.TotalSeconds);
        }
    }

    [Serializable]
    private class ChestSaveBlob
    {
        public List<ChestData> chests = new List<ChestData>();
    }

    [Header("Runtime")]
    [SerializeField] private List<ChestData> chests = new List<ChestData>();

    public const int MaxChestSlots = 5;

    public event Action OnInventoryChanged;

    private void Awake()
    {
        DLog($"Awake ENTRY — Instance={(Instance != null ? Instance.name + "#" + Instance.GetInstanceID() : "NULL")} this={name}#{GetInstanceID()}");

        if (Instance != null && Instance != this)
        {
            DLog($"Awake — duplicate detected, destroying self");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        DLog($"Awake — Instance assigned to {name}#{GetInstanceID()}, DontDestroyOnLoad applied");
    }

    /// <summary>
    /// Returns the existing Instance, or creates one on-demand if missing.
    /// Use this in cross-scene code where timing cannot be guaranteed.
    /// </summary>
    public static ChestInventoryManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        // Check for a surviving DDOL object whose static ref was cleared by domain reload
        Instance = FindObjectOfType<ChestInventoryManager>();
        if (Instance != null)
        {
            Debug.Log($"[ChestInvMgr] EnsureInstance — found orphaned instance {Instance.name}#{Instance.GetInstanceID()}, re-assigned.");
            return Instance;
        }

        // Last resort: create from scratch (Bootstrap should have handled this)
        var go = new GameObject("[Auto] ChestInventoryManager");
        Instance = go.AddComponent<ChestInventoryManager>();
        Debug.Log("[ChestInvMgr] EnsureInstance — created new instance from scratch.");
        return Instance;
    }

    private void OnEnable()
    {
        DLog($"OnEnable — this={name}#{GetInstanceID()}");
    }

    private void Start()
    {
        DLog($"Start — chests.Count={chests.Count}");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Check all unlocking chests for UTC-based completion
        for (int i = 0; i < chests.Count; i++)
        {
            var cd = chests[i];
            if (cd.state != ChestState.Unlocking) continue;
            if (cd.GetRemainingSeconds() <= 0f)
            {
                cd.state = ChestState.ReadyToOpen;
                cd.unlockEndUtcTicks = 0;
                NotifyChanged();
            }
        }
    }

    // ═══════════════ PUBLIC API ═══════════════

    public bool IsInventoryFull => chests.Count >= MaxChestSlots;

    public int ChestCount => chests.Count;

    public int GetUnopenedCount()
    {
        int count = 0;
        for (int i = 0; i < chests.Count; i++)
        {
            var s = chests[i].state;
            if (s != ChestState.OpeningInProgress)
                count++;
        }
        return count;
    }

    public bool HasAnyChest() => GetUnopenedCount() > 0;

    public bool HasActiveUnlock()
    {
        for (int i = 0; i < chests.Count; i++)
            if (chests[i].state == ChestState.Unlocking) return true;
        return false;
    }

    /// <summary>Returns chest data at the given inventory index, or null.</summary>
    public ChestData GetChestAt(int index)
    {
        if (index < 0 || index >= chests.Count) return null;
        return chests[index];
    }

    /// <summary>Returns a read-only view of all chests.</summary>
    public IReadOnlyList<ChestData> GetAllChests() => chests;

    public ChestData GetChestToShowInPopup()
    {
        return chests.Count > 0 ? chests[0] : null;
    }

    public void AddChestFromWorld(Chest worldChest)
    {
        if (worldChest == null) return;
        if (IsInventoryFull)
        {
            Debug.Log("[ChestInvMgr] Inventory full — chest rejected.");
            return;
        }

        ChestType type = worldChest.chestType;
        ChestData cd = new ChestData
        {
            chestType = type,
            chestName = ChestTypeConfig.GetDisplayName(type),
            unlockDurationSeconds = ChestTypeConfig.GetUnlockDuration(type),
            state = ChestState.Idle,
            unlockEndUtcTicks = 0,
            halfTimeUsed = false
        };

        chests.Add(cd);
        NotifyChanged();
        DLog($"Added chest: {cd.chestName} ({type}), count={chests.Count}");
    }

    /// <summary>
    /// Aktif unlock yoksa en eski Idle chest'i unlock'a sokar.
    /// Skips OpeningInProgress and Completed chests.
    /// </summary>
    // ═══════════════ UNLOCK / TIMER ═══════════════

    /// <summary>Starts the unlock timer for the chest at the given index. One at a time.</summary>
    public bool StartUnlock(int index)
    {
        if (index < 0 || index >= chests.Count) return false;
        if (HasActiveUnlock()) return false;
        var cd = chests[index];
        if (cd.state != ChestState.Idle) return false;

        cd.state = ChestState.Unlocking;
        cd.unlockEndUtcTicks = (DateTime.UtcNow + TimeSpan.FromSeconds(cd.unlockDurationSeconds)).Ticks;
        NotifyChanged();
        DLog($"Started unlock: index={index} duration={cd.unlockDurationSeconds}s");
        return true;
    }

    /// <summary>Legacy: starts the oldest idle chest.</summary>
    public bool StartUnlockOldest()
    {
        for (int i = 0; i < chests.Count; i++)
            if (chests[i].state == ChestState.Idle) return StartUnlock(i);
        return false;
    }

    /// <summary>
    /// Reklam sonrası -20 dk uygular (1 kere).
    /// </summary>
    /// <summary>Halves the remaining unlock time (ad reward). Once per chest.</summary>
    public bool ApplyHalfTime(int index)
    {
        if (index < 0 || index >= chests.Count) return false;
        var cd = chests[index];
        if (cd.state != ChestState.Unlocking) return false;
        if (cd.halfTimeUsed) return false;

        cd.halfTimeUsed = true;
        float remaining = cd.GetRemainingSeconds();
        float newRemaining = remaining * 0.5f;
        cd.unlockEndUtcTicks = (DateTime.UtcNow + TimeSpan.FromSeconds(newRemaining)).Ticks;
        if (newRemaining <= 0f)
        {
            cd.state = ChestState.ReadyToOpen;
            cd.unlockEndUtcTicks = 0;
        }
        NotifyChanged();
        DLog($"HalfTime applied: index={index} remaining={remaining:F0} -> {newRemaining:F0}");
        return true;
    }

    /// <summary>Legacy compat for old skip flow.</summary>
    public bool ApplySkip20Minutes()
    {
        int idx = FindFirstUnlockingIndex();
        if (idx < 0) return false;
        return ApplyHalfTime(idx);
    }

    /// <summary>Instantly completes unlock by spending nitro coins (per-type cost).</summary>
    public bool OpenNowByNitro(int index)
    {
        if (index < 0 || index >= chests.Count) return false;
        var cd = chests[index];
        if (cd.state == ChestState.ReadyToOpen || cd.state == ChestState.OpeningInProgress)
            return false;

        int cost = ChestTypeConfig.GetOpenNowCost(cd.chestType);
        if (CurrencyManager.Instance == null) return false;
        if (!CurrencyManager.Instance.TrySpendNitroCoins(cost)) return false;

        cd.state = ChestState.ReadyToOpen;
        cd.unlockEndUtcTicks = 0;
        NotifyChanged();
        DLog($"OpenNow: index={index} cost={cost} nitro");
        return true;
    }

    private int FindFirstUnlockingIndex()
    {
        for (int i = 0; i < chests.Count; i++)
            if (chests[i].state == ChestState.Unlocking) return i;
        return -1;
    }


    // -------- SAVE / LOAD --------
    private const string KEY_CHEST_BLOB = "Save_ChestBlob";
    private const string KEY_PENDING_OPEN_CHEST = "Save_PendingOpenChest";
    /// <summary>
    /// Stores a chest to be opened in the next scene (ChestOpenScene).
    /// Call this BEFORE consuming the chest and loading the scene.
    /// </summary>
    public void SetPendingOpenChest(ChestData chest)
    {
        if (chest == null)
        {
            PlayerPrefs.DeleteKey(KEY_PENDING_OPEN_CHEST);
            Debug.Log("[ChestInventoryManager] Cleared pending open chest.");
            return;
        }

        string json = JsonUtility.ToJson(chest);
        PlayerPrefs.SetString(KEY_PENDING_OPEN_CHEST, json);
        PlayerPrefs.Save();
        Debug.Log($"[ChestInventoryManager] SetPendingOpenChest: {chest.chestName}");
    }

    /// <summary>
    /// Retrieves the chest stored for opening. Returns null if none.
    /// </summary>
    public ChestData GetPendingOpenChest()
    {
        if (!PlayerPrefs.HasKey(KEY_PENDING_OPEN_CHEST))
        {
            Debug.Log("[ChestInventoryManager] GetPendingOpenChest: No pending chest found.");
            return null;
        }

        string json = PlayerPrefs.GetString(KEY_PENDING_OPEN_CHEST, "");
        if (string.IsNullOrEmpty(json))
        {
            Debug.Log("[ChestInventoryManager] GetPendingOpenChest: Empty JSON.");
            return null;
        }

        try
        {
            var chest = JsonUtility.FromJson<ChestData>(json);
            Debug.Log($"[ChestInventoryManager] GetPendingOpenChest: {chest?.chestName}");
            return chest;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChestInventoryManager] GetPendingOpenChest parse error: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clears the pending open chest after rewards have been granted.
    /// </summary>
    public void ClearPendingOpenChest()
    {
        PlayerPrefs.DeleteKey(KEY_PENDING_OPEN_CHEST);
        Debug.Log("[ChestInventoryManager] ClearPendingOpenChest: Cleared.");
    }

    public void SaveToPrefs()
    {
        ChestSaveBlob blob = new ChestSaveBlob
        {
            chests = chests
        };

        string json = JsonUtility.ToJson(blob);
        PlayerPrefs.SetString(KEY_CHEST_BLOB, json);
    }

    public void LoadFromPrefs()
    {
        if (!PlayerPrefs.HasKey(KEY_CHEST_BLOB))
        {
            NotifyChanged();
            return;
        }

        string json = PlayerPrefs.GetString(KEY_CHEST_BLOB, "");
        if (string.IsNullOrEmpty(json))
        {
            NotifyChanged();
            return;
        }

        try
        {
            var blob = JsonUtility.FromJson<ChestSaveBlob>(json);
            chests = blob != null && blob.chests != null ? blob.chests : new List<ChestData>();

            // Apply offline time: check all unlocking chests
            for (int i = 0; i < chests.Count; i++)
            {
                var cd = chests[i];
                if (cd.state == ChestState.Unlocking && cd.GetRemainingSeconds() <= 0f)
                {
                    cd.state = ChestState.ReadyToOpen;
                    cd.unlockEndUtcTicks = 0;
                }
            }

            NotifyChanged();
        }
        catch
        {
            chests = new List<ChestData>();
            NotifyChanged();
        }
    }

    private void NotifyChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    // ═══════════════ SESSION-BASED OPENING ═══════════════

    /// <summary>
    /// Transitions the chest at given index to OpeningInProgress.
    /// Returns the chest data, or null if ineligible.
    /// </summary>
    public ChestData MarkChestAsOpening(int index)
    {
        if (index < 0 || index >= chests.Count) return null;
        var cd = chests[index];
        if (cd.state != ChestState.ReadyToOpen) return null;

        cd.state = ChestState.OpeningInProgress;
        NotifyChanged();
        Debug.Log($"[ChestInvMgr] Chest '{cd.chestName}' marked as OpeningInProgress (index={index}).");
        return cd;
    }

    /// <summary>Legacy: marks the first ReadyToOpen chest as opening.</summary>
    public ChestData MarkChestAsOpening()
    {
        for (int i = 0; i < chests.Count; i++)
        {
            if (chests[i].state == ChestState.ReadyToOpen)
                return MarkChestAsOpening(i);
        }
        return null;
    }

    /// <summary>
    /// Removes the first chest in OpeningInProgress state from the inventory.
    /// Called by ChestSessionManager after rewards are committed.
    /// </summary>
    public bool RemoveOpeningChest()
    {
        for (int i = 0; i < chests.Count; i++)
        {
            if (chests[i].state == ChestState.OpeningInProgress)
            {
                Debug.Log($"[ChestInvMgr] Removing OpeningInProgress chest at index {i}.");
                chests.RemoveAt(i);
                NotifyChanged();
                return true;
            }
        }
        Debug.LogWarning("[ChestInvMgr] RemoveOpeningChest: no OpeningInProgress chest found.");
        return false;
    }

    /// <summary>
    /// Reverts the first OpeningInProgress chest back to ReadyToOpen.
    /// Used for crash recovery when rewards were NOT committed.
    /// </summary>
    public bool RevertOpeningChestToReady()
    {
        for (int i = 0; i < chests.Count; i++)
        {
            if (chests[i].state == ChestState.OpeningInProgress)
            {
                var cd = chests[i];
                cd.state = ChestState.ReadyToOpen;
                NotifyChanged();
                Debug.Log($"[ChestInvMgr] Reverted chest to ReadyToOpen (index={i}).");
                return true;
            }
        }
        return false;
    }
}
