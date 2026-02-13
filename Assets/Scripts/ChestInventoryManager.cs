using System;
using System.Collections.Generic;
using UnityEngine;

public class ChestInventoryManager : MonoBehaviour
{
    public static ChestInventoryManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [Serializable]
    public class ChestData
    {
        public string chestName;
        public double minReward;
        public double maxReward;

        public int cardReward;
        public int turboMin;
        public int turboMax;

        public float unlockDurationSeconds;

        public ChestState state;
        public float remainingTime;

        public bool skipUsed; // -20 mins bir kere

        public ChestData() { }
    }

    [Serializable]
    private class ChestSaveBlob
    {
        public List<ChestData> chests = new List<ChestData>();
        public int activeIndex = -1; // unlock olan chest index
    }

    [Header("Runtime")]
    [SerializeField] private List<ChestData> chests = new List<ChestData>();
    [SerializeField] private int activeUnlockIndex = -1;

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Tek aktif unlock slot
        if (activeUnlockIndex < 0 || activeUnlockIndex >= chests.Count) return;

        var cd = chests[activeUnlockIndex];
        if (cd.state != ChestState.Unlocking) return;

        cd.remainingTime -= Time.deltaTime;
        if (cd.remainingTime <= 0f)
        {
            cd.remainingTime = 0f;
            cd.state = ChestState.ReadyToOpen;
            chests[activeUnlockIndex] = cd;
            NotifyChanged();
            return;
        }

        chests[activeUnlockIndex] = cd;
    }

    // -------- PUBLIC API --------

    public int GetUnopenedCount()
    {
        // Opened olanları listeden zaten kaldıracağız; bu yüzden Count yeterli.
        return chests.Count;
    }

    public bool HasAnyChest() => chests.Count > 0;

    public bool HasActiveUnlock()
    {
        return activeUnlockIndex >= 0 &&
               activeUnlockIndex < chests.Count &&
               (chests[activeUnlockIndex].state == ChestState.Unlocking ||
                chests[activeUnlockIndex].state == ChestState.ReadyToOpen);
    }

    public ChestData GetChestToShowInPopup()
    {
        // Aktif unlock varsa onu göster
        if (HasActiveUnlock())
            return chests[activeUnlockIndex];

        // Yoksa en eski chest
        if (chests.Count > 0)
            return chests[0];

        return null;
    }

    public ChestData GetActiveChest()
    {
        if (!HasActiveUnlock()) return null;
        return chests[activeUnlockIndex];
    }

    public void AddChestFromWorld(Chest worldChest)
    {
        if (worldChest == null) return;

        ChestData cd = new ChestData
        {
            chestName = worldChest.chestName,
            minReward = worldChest.minReward,
            maxReward = worldChest.maxReward,
            cardReward = worldChest.cardReward,
            turboMin = worldChest.turboMin,
            turboMax = worldChest.turboMax,
            unlockDurationSeconds = worldChest.unlockDurationSeconds,

            state = ChestState.Idle,
            remainingTime = worldChest.unlockDurationSeconds,
            skipUsed = false
        };

        chests.Add(cd);
        NotifyChanged();
    }

    /// <summary>
    /// Aktif unlock yoksa en eski chest'i unlock'a sokar.
    /// </summary>
    public bool StartUnlockOldest()
    {
        if (chests.Count == 0) return false;
        if (HasActiveUnlock()) return false;

        var cd = chests[0];
        if (cd.state != ChestState.Idle) return false;

        cd.state = ChestState.Unlocking;
        cd.remainingTime = cd.unlockDurationSeconds;
        chests[0] = cd;

        activeUnlockIndex = 0;
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Reklam sonrası -20 dk uygular (1 kere).
    /// </summary>
    public bool ApplySkip20Minutes()
    {
        if (!HasActiveUnlock()) return false;

        var cd = chests[activeUnlockIndex];
        if (cd.state != ChestState.Unlocking) return false;
        if (cd.skipUsed) return false;

        cd.skipUsed = true;
        cd.remainingTime -= 20f * 60f;
        if (cd.remainingTime <= 0f)
        {
            cd.remainingTime = 0f;
            cd.state = ChestState.ReadyToOpen;
        }

        chests[activeUnlockIndex] = cd;
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Hazırsa açar, ödülü döndürür ve chest'i listeden kaldırır.
    /// </summary>
    public double OpenActiveChestAndGetReward()
    {
        if (!HasActiveUnlock()) return 0;

        var cd = chests[activeUnlockIndex];
        if (cd.state != ChestState.ReadyToOpen) return 0;

        double reward = UnityEngine.Random.Range((float)cd.minReward, (float)cd.maxReward);

        // ödülü ver
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddMoney(reward);

        // active chest'i kaldır
        chests.RemoveAt(activeUnlockIndex);
        activeUnlockIndex = -1;

        // Eğer queue’da chest kaldıysa, onları kaydırmış olduk. (şimdilik unlock otomatik başlamaz)
        NotifyChanged();

        return reward;
    }

    // -------- SAVE / LOAD --------
    // PlayerPrefs key'leri SaveSystem ile uyumlu kullanacağız.
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
        Debug.Log($"[ChestInventoryManager] SetPendingOpenChest: {chest.chestName}, cardReward={chest.cardReward}, minReward={chest.minReward}, maxReward={chest.maxReward}");
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
            Debug.Log($"[ChestInventoryManager] GetPendingOpenChest: {chest?.chestName}, cardReward={chest?.cardReward}");
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
            chests = chests,
            activeIndex = activeUnlockIndex
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
            activeUnlockIndex = blob != null ? blob.activeIndex : -1;

            // güvenlik
            if (activeUnlockIndex < 0 || activeUnlockIndex >= chests.Count)
                activeUnlockIndex = -1;

            NotifyChanged();
        }
        catch
        {
            chests = new List<ChestData>();
            activeUnlockIndex = -1;
            NotifyChanged();
        }
    }

    private void NotifyChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    public bool OpenNowByNitro(int costNitro = 3)
    {
        // Chest yoksa
        if (chests == null || chests.Count == 0) return false;

        // Para kontrol
        if (CurrencyManager.Instance == null) return false;
        if (!CurrencyManager.Instance.TrySpendNitroCoins(costNitro)) return false;

        // Hangi chest üzerinde işlem yapacağız?
        int idx = -1;

        // Aktif unlock varsa onu target al
        if (HasActiveUnlock())
        {
            idx = activeUnlockIndex;
        }
        else
        {
            // aktif unlock yoksa en eski chest (index 0)
            idx = 0;
            activeUnlockIndex = 0; // Open butonu "active" üzerinden açtığı için bunu set ediyoruz
        }

        if (idx < 0 || idx >= chests.Count) return false;

        var cd = chests[idx];

        // Zaten açılabiliyorsa boşa coin harcamayalım (istersen true döndürüp kesmeyi engelleriz)
        if (cd.state == ChestState.ReadyToOpen)
        {
            // Coin harcamasın diye geri iade etmek istersen:
            CurrencyManager.Instance.AddNitroCoins(costNitro);
            return false;
        }

        // Direkt hazır hale getir
        cd.state = ChestState.ReadyToOpen;
        cd.remainingTime = 0f;

        chests[idx] = cd;

        NotifyChanged();
        return true;
    }

    public bool ConsumeActiveReadyChest()
    {
        if (!HasActiveUnlock()) return false;

        var cd = chests[activeUnlockIndex];
        if (cd.state != ChestState.ReadyToOpen) return false;

        chests.RemoveAt(activeUnlockIndex);
        activeUnlockIndex = -1;

        NotifyChanged();
        return true;
    }

    public bool ConsumeReadyChestForOpening()
    {
        if (!HasActiveUnlock()) return false;

        var cd = chests[activeUnlockIndex];
        if (cd.state != ChestState.ReadyToOpen) return false;

        // chest’i tüket (artık “opened say”)
        chests.RemoveAt(activeUnlockIndex);
        activeUnlockIndex = -1;

        NotifyChanged();
        return true;
    }
    public bool ConsumeActiveChest_NoReward()
    {
        if (!HasActiveUnlock()) return false;

        var cd = chests[activeUnlockIndex];
        if (cd.state != ChestState.ReadyToOpen) return false;

        // chest'i listeden kaldır
        chests.RemoveAt(activeUnlockIndex);
        activeUnlockIndex = -1;

        NotifyChanged();
        return true;
    }


}
