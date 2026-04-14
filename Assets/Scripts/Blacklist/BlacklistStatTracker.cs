using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Singleton that owns lifetime cumulative counters for stats that don't
/// already exist in the project. Subscribes to existing gameplay events
/// and persists counters to PlayerPrefs.
///
/// Place on a DontDestroyOnLoad GameObject (same root as other managers).
/// </summary>
public class BlacklistStatTracker : MonoBehaviour
{
    public static BlacklistStatTracker Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    // ─── Lifetime counters (persisted) ───

    /// <summary>Total nitro coins collected from world objects (tap + magnet + rain).</summary>
    public int TotalWorldNitroCollected { get; private set; }

    /// <summary>Total radars defused (lifetime, never resets).</summary>
    public int TotalRadarsDefused { get; private set; }

    /// <summary>Total chests opened (lifetime).</summary>
    public int TotalChestsOpened { get; private set; }

    /// <summary>Total boost activations (lifetime).</summary>
    public int TotalBoostUses { get; private set; }

    /// <summary>Total successful police escapes (lifetime).</summary>
    public int TotalPoliceEscapes { get; private set; }

    /// <summary>Total nitro rain triggers (lifetime).</summary>
    public int TotalNitroRainTriggers { get; private set; }

    /// <summary>Total nitro coins collected specifically by the Nitro Magnet (lifetime).</summary>
    public int TotalMagnetCoinsCollected { get; private set; }

    /// <summary>Total Turbo Finger activations (lifetime).</summary>
    public int TotalTurboUses { get; private set; }

    // ─── PlayerPrefs keys ───

    private const string KEY_WORLD_NITRO = "BL_Stat_WorldNitro";
    private const string KEY_RADARS = "BL_Stat_RadarsDefused";
    private const string KEY_CHESTS = "BL_Stat_ChestsOpened";
    private const string KEY_BOOST = "BL_Stat_BoostUses";
    private const string KEY_POLICE_ESC = "BL_Stat_PoliceEscapes";
    private const string KEY_NITRO_RAIN = "BL_Stat_NitroRainTriggers";
    private const string KEY_MAGNET_COINS = "BL_Stat_MagnetCoins";
    private const string KEY_TURBO = "BL_Stat_TurboUses";

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

        LoadCounters();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    // ─── Event subscriptions ───

    private void SubscribeEvents()
    {
        // World nitro collected (fired from NitroCoin on tap or magnet collection)
        NitroCoin.OnWorldNitroCollected += HandleWorldNitroCollected;

        // Radar defused
        PopularityManager.OnRadarDefused += HandleRadarDefused;

        // Chest completed
        ChestSessionManager.OnChestCompleted += HandleChestCompleted;

        // Boost activated
        if (BoostModeController.Instance != null)
            BoostModeController.Instance.OnBoostStarted += HandleBoostStarted;

        // Police chase ended (check success flag)
        PoliceCatchController.OnChaseEnded += HandleChaseEnded;

        // Nitro rain triggered
        if (NitroRainController.Instance != null)
            NitroRainController.Instance.OnRainStarted += HandleNitroRainStarted;

        // Turbo finger activated
        if (TurboFingerController.Instance != null)
            TurboFingerController.Instance.OnActivated += HandleTurboActivated;

        // Magnet coin collected (fired from NitroMagnetController)
        NitroMagnetController.OnMagnetCoinCollected += HandleMagnetCoinCollected;
    }

    private void UnsubscribeEvents()
    {
        NitroCoin.OnWorldNitroCollected -= HandleWorldNitroCollected;
        PopularityManager.OnRadarDefused -= HandleRadarDefused;
        ChestSessionManager.OnChestCompleted -= HandleChestCompleted;

        if (BoostModeController.Instance != null)
            BoostModeController.Instance.OnBoostStarted -= HandleBoostStarted;

        PoliceCatchController.OnChaseEnded -= HandleChaseEnded;

        if (NitroRainController.Instance != null)
            NitroRainController.Instance.OnRainStarted -= HandleNitroRainStarted;

        if (TurboFingerController.Instance != null)
            TurboFingerController.Instance.OnActivated -= HandleTurboActivated;

        NitroMagnetController.OnMagnetCoinCollected -= HandleMagnetCoinCollected;
    }

    /// <summary>
    /// Re-subscribe to instance events that may not have been available at Awake.
    /// Called after SaveSystem.OnGameLoaded.
    /// </summary>
    public void LateSubscribe()
    {
        // These are instance events — the singleton might not exist at our Awake time.
        if (BoostModeController.Instance != null)
        {
            BoostModeController.Instance.OnBoostStarted -= HandleBoostStarted;
            BoostModeController.Instance.OnBoostStarted += HandleBoostStarted;
        }
        if (NitroRainController.Instance != null)
        {
            NitroRainController.Instance.OnRainStarted -= HandleNitroRainStarted;
            NitroRainController.Instance.OnRainStarted += HandleNitroRainStarted;
        }
        if (TurboFingerController.Instance != null)
        {
            TurboFingerController.Instance.OnActivated -= HandleTurboActivated;
            TurboFingerController.Instance.OnActivated += HandleTurboActivated;
        }
    }

    // ─── Event handlers ───

    private void HandleWorldNitroCollected(int amount)
    {
        TotalWorldNitroCollected += amount;
        SaveCounters();
    }

    private void HandleRadarDefused()
    {
        TotalRadarsDefused++;
        SaveCounters();
    }

    private void HandleChestCompleted()
    {
        TotalChestsOpened++;
        SaveCounters();
    }

    private void HandleBoostStarted(float _duration)
    {
        TotalBoostUses++;
        SaveCounters();
    }

    private void HandleChaseEnded()
    {
        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.WasLastChaseSuccess)
        {
            TotalPoliceEscapes++;
            SaveCounters();
        }
    }

    private void HandleNitroRainStarted(float _duration, int _level)
    {
        TotalNitroRainTriggers++;
        SaveCounters();
    }

    private void HandleMagnetCoinCollected(int amount)
    {
        TotalMagnetCoinsCollected += amount;
        SaveCounters();
    }

    private void HandleTurboActivated()
    {
        TotalTurboUses++;
        SaveCounters();
    }

    // ─── Query API (used by BlacklistManager) ───

    /// <summary>
    /// Returns the current raw lifetime counter value for a given mission type.
    /// For absolute missions this returns the live game state directly.
    /// </summary>
    public double GetLifetimeValue(BlacklistMissionType type)
    {
        switch (type)
        {
            case BlacklistMissionType.EarnGold:
                return CurrencyManager.Instance != null ? CurrencyManager.Instance.totalMoneyEarned : 0;

            case BlacklistMissionType.CollectWorldNitro:
                return TotalWorldNitroCollected;

            case BlacklistMissionType.DefuseRadars:
                return TotalRadarsDefused;

            case BlacklistMissionType.OwnBuildings:
                return GetUniqueBuildingsOwned();

            case BlacklistMissionType.OpenChests:
                return TotalChestsOpened;

            case BlacklistMissionType.UseBoost:
                return TotalBoostUses;

            case BlacklistMissionType.EscapePolice:
                return TotalPoliceEscapes;

            case BlacklistMissionType.UpgradeAnyCardToLevel:
                return GetHighestCardLevel();

            case BlacklistMissionType.BuyGarageParts:
                return GetTotalGaragePartsOwned();

            case BlacklistMissionType.TriggerNitroRain:
                return TotalNitroRainTriggers;

            case BlacklistMissionType.NitroMagnetCollect:
                return TotalMagnetCoinsCollected;

            case BlacklistMissionType.UseTurbo:
                return TotalTurboUses;

            case BlacklistMissionType.ReachTotalCardLevel:
                return GetSumOfAllCardLevels();

            default:
                Debug.LogWarning($"[BlacklistStatTracker] Unknown mission type: {type}");
                return 0;
        }
    }

    // ─── Absolute state helpers ───

    private int GetUniqueBuildingsOwned()
    {
        if (BuildingManager.Instance == null || BuildingManager.Instance.buildings == null)
            return 0;
        return BuildingManager.Instance.buildings.Count(b => b.count > 0);
    }

    private int GetHighestCardLevel()
    {
        if (CardManager.Instance == null || CardManager.Instance.cards == null)
            return 0;
        int max = 0;
        foreach (var c in CardManager.Instance.cards)
            if (c.currentLevel > max) max = c.currentLevel;
        return max;
    }

    private int GetSumOfAllCardLevels()
    {
        if (CardManager.Instance == null || CardManager.Instance.cards == null)
            return 0;
        int sum = 0;
        foreach (var c in CardManager.Instance.cards)
            sum += c.currentLevel;
        return sum;
    }

    private int GetTotalGaragePartsOwned()
    {
        if (GarageSaveData.Instance == null)
            return 0;

        // Sum ownedParts across all cars.
        // We access the internal data through the public API.
        int total = 0;
        // GarageSaveData stores per-car entries. We enumerate known car IDs.
        // Since GarageSaveData creates entries on GetStateForCar, we need the
        // list of carIds that exist. We use the same IDs the garage system uses.
        var carIds = GetKnownCarIds();
        foreach (var carId in carIds)
        {
            var entry = GarageSaveData.Instance.GetStateForCar(carId);
            total += entry.ownedParts.Count;
        }
        return total;
    }

    /// <summary>
    /// Returns known car IDs used by the garage system.
    /// These match the CarDataSO asset names used in the project.
    /// </summary>
    private static readonly string[] _knownCarIds = new string[]
    {
        "Bmv", "Bugatti", "Dodge", "Mazda", "Nardo", "Pagani", "Vw"
    };

    private string[] GetKnownCarIds()
    {
        return _knownCarIds;
    }

    /// <summary>
    /// Public accessor for known car IDs. Used by reward granting (endgame cosmetics).
    /// </summary>
    public static string[] GetAllCarIds()
    {
        return _knownCarIds;
    }

    // ─── Persistence ───

    public void SaveCounters()
    {
        PlayerPrefs.SetInt(KEY_WORLD_NITRO, TotalWorldNitroCollected);
        PlayerPrefs.SetInt(KEY_RADARS, TotalRadarsDefused);
        PlayerPrefs.SetInt(KEY_CHESTS, TotalChestsOpened);
        PlayerPrefs.SetInt(KEY_BOOST, TotalBoostUses);
        PlayerPrefs.SetInt(KEY_POLICE_ESC, TotalPoliceEscapes);
        PlayerPrefs.SetInt(KEY_NITRO_RAIN, TotalNitroRainTriggers);
        PlayerPrefs.SetInt(KEY_MAGNET_COINS, TotalMagnetCoinsCollected);
        PlayerPrefs.SetInt(KEY_TURBO, TotalTurboUses);
    }

    public void LoadCounters()
    {
        TotalWorldNitroCollected = PlayerPrefs.GetInt(KEY_WORLD_NITRO, 0);
        TotalRadarsDefused = PlayerPrefs.GetInt(KEY_RADARS, 0);
        TotalChestsOpened = PlayerPrefs.GetInt(KEY_CHESTS, 0);
        TotalBoostUses = PlayerPrefs.GetInt(KEY_BOOST, 0);
        TotalPoliceEscapes = PlayerPrefs.GetInt(KEY_POLICE_ESC, 0);
        TotalNitroRainTriggers = PlayerPrefs.GetInt(KEY_NITRO_RAIN, 0);
        TotalMagnetCoinsCollected = PlayerPrefs.GetInt(KEY_MAGNET_COINS, 0);
        TotalTurboUses = PlayerPrefs.GetInt(KEY_TURBO, 0);
    }
}
