using UnityEngine;
using UnityEngine.SceneManagement;
using System.Globalization;
using System.Collections;
using System;
using System.Collections.Generic;

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance;

    /// <summary>
    /// Event fired after game data (cards, economy, etc.) is fully loaded.
    /// Controllers should subscribe to refresh their state after load.
    /// </summary>
    public static event Action OnGameLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnGameLoaded = null; // Clear static event to prevent duplicate subscribers across domain-reload-off sessions
    }

    private CultureInfo culture = CultureInfo.InvariantCulture;

    // Guard against double-load (Start + OnSceneLoaded both firing on initial "Main" scene)
    private bool _hasLoadedThisSession = false;

    // ========== BUILDING SAVE-KEY MIGRATION ==========
    // Old saves used: "Save_Building_{EnumName}_Count" (string-based).
    // New saves use:  "Save_BuildingID_{(int)type}_Count" (integer-based, stable).
    //
    // OldEnumNameToId maps EVERY old enum-member string to its stable integer ID,
    // so we can read legacy PlayerPrefs keys and migrate them to the new format.
    // This table was extracted from git commit 9c34831.
    private static readonly Dictionary<string, int> OldEnumNameToId = new Dictionary<string, int>
    {
        { "AutoClicker",          0 },
        { "NeighborhoodTaxi",     1 },
        { "ParkingMeterNetwork",  2 },
        { "CarWashStation",       3 },
        { "PartsFactory",         4 },
        { "SportsGarage",         5 },
        { "LuxuryShowRoom",       6 },
        { "RideSharingFleet",     7 },
        { "LogisticsCompany",     8 },
        { "HighwayTollSystem",    9 },
        { "SmartTrafficNetwork",  10 },
        { "AutonomousTaxiHub",    11 },
        { "HyperloopCargoLine",   12 },
        { "EVGigafactory",        13 },
        { "NanoFuelLab",          14 },
        { "MolecularEngineLab",   15 },
        { "Virus_ProofCarOS",     16 },
        { "PrototypeWarpEngine",  17 },
        { "SynapticDrivingNetwork", 18 },
        { "HydrogenFuelNetwork",  19 },
        { "UraniumRaceTrack",     20 },
        { "PlutoniumEnginePlant", 21 },
        { "Crypto_TollChain",     22 },
        { "MoonColonyGarage",     23 },
        { "GalaxyHighway",        24 },
        { "Galaxy_XRacingLeague", 25 },
        { "CarHackAI",            26 },
        { "CarGodCore",           27 },
    };

    /// <summary>
    /// Generates the NEW (stable) save key for a building using its integer ID.
    /// </summary>
    private static string GetBuildingKey(BuildingType type)
    {
        return "Save_BuildingID_" + (int)type + "_Count";
    }

    /// <summary>
    /// Generates the OLD (legacy) save key from an enum-member string.
    /// </summary>
    private static string GetLegacyBuildingKey(string enumName)
    {
        return "Save_Building_" + enumName + "_Count";
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        _hasLoadedThisSession = false;
        StartCoroutine(LoadAfterStart());
    }

    private IEnumerator LoadAfterStart()
    {
        yield return null;
        if (!_hasLoadedThisSession)
        {
            LoadGame();
        }
        else
        {
            Debug.Log("[SaveSystem] LoadAfterStart skipped — already loaded by OnSceneLoaded.");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Main yeniden yüklenince save tekrar uygulansın
        if (scene.name == "Main")
        {
            StartCoroutine(LoadAfterScene());
        }
    }

    private IEnumerator LoadAfterScene()
    {
        yield return null;
        Time.timeScale = 1f;
        if (!_hasLoadedThisSession)
        {
            LoadGame();
        }
        else
        {
            Debug.Log("[SaveSystem] LoadAfterScene skipped — already loaded this session.");
        }
    }

    // ---------- SAVE ----------

    public void SaveGame()
    {
        // 1) Chest data her durumda kaydedilsin
        if (ChestInventoryManager.Instance != null)
        {
            ChestInventoryManager.Instance.SaveToPrefs();
        }

        // 2) Ekonomi / diğer sistemler varsa kaydet
        var cm = CurrencyManager.Instance;
        if (cm == null)
        {
            Debug.LogWarning("[SaveSystem] CurrencyManager.Instance is NULL, economy not saved (but chest save attempted).");
            PlayerPrefs.SetInt("HasSave", 1);
            PlayerPrefs.Save();
            return;
        }

        string moneyStr = cm.money.ToString(culture);
        PlayerPrefs.SetString("Save_Money", moneyStr);
        PlayerPrefs.SetString("Save_MPS", cm.moneyPerSecond.ToString(culture));
        PlayerPrefs.SetString("Save_MPT", cm.moneyPerTap.ToString(culture));

        PlayerPrefs.SetString("Save_TotalMoney", cm.totalMoneyEarned.ToString(culture));
        PlayerPrefs.SetInt("Save_NitroCoins", cm.nitroCoins);

        Debug.Log($"[SaveSystem] SAVE Money key=Save_Money value={moneyStr} (raw double={cm.money}) MPS={cm.moneyPerSecond} MPT={cm.moneyPerTap}");

        UpgradeButton[] upgrades = FindObjectsByType<UpgradeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var up in upgrades)
        {
            string key = "Save_Upgrade_" + up.upgradeType.ToString() + "_Level";
            PlayerPrefs.SetInt(key, up.GetCurrentLevel());
        }

        if (BuildingManager.Instance != null)
        {
            foreach (var b in BuildingManager.Instance.buildings)
            {
                // Use stable integer-based key for all future saves
                string key = GetBuildingKey(b.type);
                PlayerPrefs.SetInt(key, b.count);
            }
        }

        if (CardManager.Instance != null && CardManager.Instance.cards != null)
        {
            foreach (var card in CardManager.Instance.cards)
            {
                string prefix = "Save_Card_" + card.type.ToString() + "_";
                PlayerPrefs.SetInt(prefix + "Level", card.currentLevel);
                PlayerPrefs.SetInt(prefix + "Copies", card.copiesOwned);
            }
        }

        // NitroMagnet state
        if (NitroMagnetController.Instance != null)
        {
            NitroMagnetController.Instance.SaveState();
        }

        // Popularity
        if (PopularityManager.Instance != null)
        {
            PlayerPrefs.SetFloat("Save_Popularity01", PopularityManager.Instance.Popularity01);
        }

        PlayerPrefs.SetInt("HasSave", 1);
        PlayerPrefs.Save();

        Debug.Log("[SaveSystem] Game saved.");
    }

    // ---------- LOAD ----------

    public void LoadGame()
    {
        // 1) Chest inventory her durumda load edilsin (key yoksa zaten boş)
        if (ChestInventoryManager.Instance != null)
        {
            ChestInventoryManager.Instance.LoadFromPrefs();
        }

        // UI’yi chest durumuna göre güncelle (0 ise gizlenir)
        if (ChestShownUI.Instance != null)
        {
            // eski Refresh() değil! yeni fonksiyon
            ChestShownUI.Instance.RefreshVisibilityAndCount();
        }

        // 2) HasSave yoksa ekonomi vs zaten yoktur, burada çıkabiliriz
        if (!PlayerPrefs.HasKey("HasSave"))
        {
            Debug.Log("[SaveSystem] No save found, starting fresh (chests loaded if any).");
            return;
        }

        // 3) Ekonomi load
        var cm = CurrencyManager.Instance;
        if (cm == null)
        {
            Debug.LogWarning("[SaveSystem] CurrencyManager.Instance is NULL, economy not loaded (chests loaded).");
            return;
        }

        // ---- DIAGNOSTIC: log raw PlayerPrefs strings before parsing ----
        string rawMoney = PlayerPrefs.GetString("Save_Money", "");
        string rawMPS = PlayerPrefs.GetString("Save_MPS", "");
        string rawMPT = PlayerPrefs.GetString("Save_MPT", "");
        string rawTotal = PlayerPrefs.GetString("Save_TotalMoney", "");
        Debug.Log($"[SaveSystem] LOAD raw strings: Save_Money='{rawMoney}' Save_MPS='{rawMPS}' Save_MPT='{rawMPT}' Save_TotalMoney='{rawTotal}'");

        cm.money = GetDouble("Save_Money", cm.money);
        cm.moneyPerSecond = GetDouble("Save_MPS", cm.moneyPerSecond);
        cm.moneyPerTap = GetDouble("Save_MPT", cm.moneyPerTap);

        cm.totalMoneyEarned = GetDouble("Save_TotalMoney", cm.totalMoneyEarned);
        cm.nitroCoins = PlayerPrefs.GetInt("Save_NitroCoins", cm.nitroCoins);

        Debug.Log($"[SaveSystem] LOAD parsed: money={cm.money} MPS={cm.moneyPerSecond} MPT={cm.moneyPerTap} total={cm.totalMoneyEarned} nitro={cm.nitroCoins}");
        cm.ResetMpsAfterLoad();

        UpgradeButton[] upgrades = FindObjectsByType<UpgradeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var up in upgrades)
        {
            string key = "Save_Upgrade_" + up.upgradeType.ToString() + "_Level";
            int level = PlayerPrefs.GetInt(key, 0);
            up.LoadFromSave(level);
        }

        if (BuildingManager.Instance != null)
        {
            LoadBuildingsWithMigration();
        }

        // Re-apply upgrade effects after building recalc.
        // Building recalc resets MPS/MPT to building-only values;
        // this restores upgrade contributions. Additives first, then multiplicatives.
        {
            UpgradeButton[] upgradesReapply = FindObjectsByType<UpgradeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var up in upgradesReapply)
            {
                if (up.upgradeType != UpgradeType.Global)
                    up.ReapplyEffect();
            }
            foreach (var up in upgradesReapply)
            {
                if (up.upgradeType == UpgradeType.Global)
                    up.ReapplyEffect();
            }
        }

        if (CardManager.Instance != null && CardManager.Instance.cards != null)
        {
            foreach (var card in CardManager.Instance.cards)
            {
                string prefix = "Save_Card_" + card.type.ToString() + "_";
                card.currentLevel = PlayerPrefs.GetInt(prefix + "Level", card.currentLevel);
                card.copiesOwned = PlayerPrefs.GetInt(prefix + "Copies", card.copiesOwned);
            }

            CardManager.Instance.ReapplyAllCardEffects();
        }

        // NitroMagnet state
        if (NitroMagnetController.Instance != null)
        {
            NitroMagnetController.Instance.LoadState();
        }

        // Popularity
        if (PopularityManager.Instance != null)
        {
            float savedPop = PlayerPrefs.GetFloat("Save_Popularity01", 0f);
            PopularityManager.Instance.Set(savedPop);
        }

        // Safety: ensure police chase is not active on load
        // (we never save mid-chase state; force clean idle on every load)
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.suppressTopBarMoneyUpdates = false;
            CurrencyManager.Instance.bufferedEarnings = 0;
        }

        // Ensure TapInputRaycaster chase flag is cleared
        var tapInput = FindFirstObjectByType<TapInputRaycaster>();
        if (tapInput != null)
            tapInput.isPoliceChaseActive = false;

        Debug.Log($"[SaveSystem] Game loaded. Final money={cm.money:F2} MPS={cm.moneyPerSecond:F4} MPT={cm.moneyPerTap:F4}");

        _hasLoadedThisSession = true;

        // Start post-load money watcher (3-second diagnostic window)
        cm.StartLoadWatcher();

        // Notify subscribers that game data is now loaded
        OnGameLoaded?.Invoke();
    }

    private double GetDouble(string key, double defaultValue)
    {
        string s = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(s)) return defaultValue;

        if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, culture, out double v))
        {
            // Sanity check: reject obviously wrong values (e.g. > 1e15 when default < 1e6)
            if (v > 1e15 && defaultValue < 1e6)
            {
                Debug.LogError($"[SaveSystem] GetDouble REJECTED suspicious value for key='{key}': raw='{s}' parsed={v} default={defaultValue}. Using default.");
                return defaultValue;
            }
            return v;
        }

        Debug.LogWarning($"[SaveSystem] GetDouble PARSE FAILED for key='{key}': raw='{s}'. Using default={defaultValue}.");
        return defaultValue;
    }

    // ========== BUILDING LOAD + MIGRATION ==========

    /// <summary>
    /// Loads building counts using the NEW integer-based keys.
    /// Falls back to OLD string-based keys (legacy migration) if new keys don't exist.
    /// After all counts are loaded, recalculates MPS/tap income for economy integrity.
    /// </summary>
    private void LoadBuildingsWithMigration()
    {
        var bm = BuildingManager.Instance;
        if (bm == null || bm.buildings == null) return;

        bool migratedAny = false;
        int totalLoadedCount = 0;

        Debug.Log("[SaveSystem] LoadBuildingsWithMigration START — loading building counts from PlayerPrefs...");

        foreach (var b in bm.buildings)
        {
            if (b == null) continue;

            string newKey = GetBuildingKey(b.type);
            int count = 0;
            string source = "default(0)";

            if (PlayerPrefs.HasKey(newKey))
            {
                // New-format key exists — load it directly
                count = PlayerPrefs.GetInt(newKey, 0);
                source = $"new-key '{newKey}'";
            }
            else
            {
                // Try OLD-format keys for this building's integer ID
                // Check both: the CURRENT enum name (in case a mid-transition save exists)
                // and ALL known OLD enum names that map to this ID.
                count = TryLoadLegacyBuildingCount(b.type, out bool found);
                if (found)
                {
                    migratedAny = true;
                    // Persist under the new key immediately
                    PlayerPrefs.SetInt(newKey, count);
                    source = $"MIGRATED legacy key";
                    Debug.Log($"[SaveSystem] Migrated building {b.type} (ID {(int)b.type}): count={count}");
                }
                else
                {
                    source = "no key found";
                }
            }

            // Log every building load for diagnostics
            if (count > 0)
            {
                Debug.Log($"[SaveSystem] Building [{(int)b.type}] {b.displayName}: count={count} (source: {source}), baseProd={b.baseProduction:G6}");
            }

            totalLoadedCount += count;
            bm.SetBuildingCount(b.type, count);
        }

        Debug.Log($"[SaveSystem] LoadBuildingsWithMigration: total building units loaded = {totalLoadedCount}");

        if (migratedAny)
        {
            // Clean up old keys after successful migration
            CleanupLegacyBuildingKeys();
            PlayerPrefs.Save();
            Debug.Log("[SaveSystem] Building migration complete — old keys cleaned up.");
        }

        // Deterministic recalculation of MPS + tap income from building counts
        bm.RecalculateMPSFromBuildings();
    }

    /// <summary>
    /// Attempts to load a building count from any legacy (string-based) key
    /// that maps to the given BuildingType's integer ID.
    /// </summary>
    private int TryLoadLegacyBuildingCount(BuildingType type, out bool found)
    {
        found = false;
        int targetId = (int)type;

        // 1) Try the current enum name as a legacy key
        //    (covers saves made after rename but before this migration code shipped)
        string currentNameKey = GetLegacyBuildingKey(type.ToString());
        if (PlayerPrefs.HasKey(currentNameKey))
        {
            found = true;
            return PlayerPrefs.GetInt(currentNameKey, 0);
        }

        // 2) Try all old enum names that map to this integer ID
        foreach (var kvp in OldEnumNameToId)
        {
            if (kvp.Value == targetId)
            {
                string oldKey = GetLegacyBuildingKey(kvp.Key);
                if (PlayerPrefs.HasKey(oldKey))
                {
                    found = true;
                    return PlayerPrefs.GetInt(oldKey, 0);
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Removes all legacy building keys (both old and current enum-name keys)
    /// after data has been migrated to the new integer-based keys.
    /// </summary>
    private void CleanupLegacyBuildingKeys()
    {
        // Delete keys from OldEnumNameToId table
        foreach (var kvp in OldEnumNameToId)
        {
            string oldKey = GetLegacyBuildingKey(kvp.Key);
            if (PlayerPrefs.HasKey(oldKey))
            {
                PlayerPrefs.DeleteKey(oldKey);
            }
        }

        // Also delete keys using current enum names (mid-transition saves)
        if (BuildingManager.Instance != null && BuildingManager.Instance.buildings != null)
        {
            foreach (var b in BuildingManager.Instance.buildings)
            {
                if (b == null) continue;
                string key = GetLegacyBuildingKey(b.type.ToString());
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key);
                }
            }
        }
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveGame();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("DEVELOPER: Reset Save & Reload Scene")]
    public void DeveloperResetGame()
    {
        Debug.Log("[SaveSystem] DeveloperResetGame called. Deleting PlayerPrefs.");

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SaveSystem] DeveloperResetGame: Not in play mode, scene reload skipped.");
            return;
        }

        var current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }


}
