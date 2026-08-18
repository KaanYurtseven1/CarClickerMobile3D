// ════════════════════════════════════════════════════════════════
// GarageSaveData.cs – DontDestroyOnLoad singleton that persists
// garage customisation state across scenes via PlayerPrefs.
//
// Public API used by GarageController (write) and
// MainSceneCarController (read).
// ════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using UnityEngine;

public class GarageSaveData : MonoBehaviour
{
    public static GarageSaveData Instance;

    private const string PrefsKey = "Save_GarageState";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    // ─── Serialisable data ───

    [Serializable]
    public class CarSaveEntry
    {
        public string carId;
        public int colorIndex;
        public int stickerIndex;
        public List<string> enabledParts = new List<string>();

        // ─── Ownership tracking ───
        public List<int> ownedColors = new List<int> { 0 };     // index 0 is free/default
        public List<int> ownedStickers = new List<int> { 0 };   // index 0 is free/default
        public List<string> ownedParts = new List<string>();
    }

    [Serializable]
    private class GarageStateWrapper
    {
        public int selectedCarIndex;
        public List<CarSaveEntry> cars = new List<CarSaveEntry>();
        public List<string> unlockedCarIds = new List<string> { DefaultUnlockedCarId };
    }

    /// <summary>Car ID that is always unlocked from the start.</summary>
    public const string DefaultUnlockedCarId = "Mazda";

    // ─── Runtime state ───

    private GarageStateWrapper _data = new GarageStateWrapper();
    private int _lastLoadFrame = -1; // Guard against redundant LoadFromPrefs in same frame

    public int SelectedCarIndex
    {
        get => _data.selectedCarIndex;
        set => _data.selectedCarIndex = value;
    }

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            LoadFromPrefs();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ══════════════════ Per-Car Accessors ══════════════════

    public CarSaveEntry GetStateForCar(string carId)
    {
        foreach (var e in _data.cars)
            if (e.carId == carId) return e;

        // First time – create blank entry
        var entry = new CarSaveEntry { carId = carId };
        _data.cars.Add(entry);
        return entry;
    }

    public void SetStateForCar(string carId, int colorIndex, int stickerIndex, HashSet<string> enabledParts)
    {
        var entry = GetStateForCar(carId);
        entry.colorIndex = colorIndex;
        entry.stickerIndex = stickerIndex;
        entry.enabledParts = new List<string>(enabledParts);
    }

    // ══════════════════ Ownership Helpers ══════════════════

    /// <summary>Returns true if the color at given index is owned for this car.</summary>
    public bool IsColorOwned(string carId, int colorIndex)
    {
        var entry = GetStateForCar(carId);
        return entry.ownedColors.Contains(colorIndex);
    }

    /// <summary>Marks a color index as owned.</summary>
    public void MarkColorOwned(string carId, int colorIndex)
    {
        var entry = GetStateForCar(carId);
        if (!entry.ownedColors.Contains(colorIndex))
            entry.ownedColors.Add(colorIndex);
    }

    /// <summary>Returns true if the sticker at given index is owned for this car.</summary>
    public bool IsStickerOwned(string carId, int stickerIndex)
    {
        var entry = GetStateForCar(carId);
        return entry.ownedStickers.Contains(stickerIndex);
    }

    /// <summary>Marks a sticker index as owned.</summary>
    public void MarkStickerOwned(string carId, int stickerIndex)
    {
        var entry = GetStateForCar(carId);
        if (!entry.ownedStickers.Contains(stickerIndex))
            entry.ownedStickers.Add(stickerIndex);
    }

    /// <summary>Returns true if the part is owned for this car.</summary>
    public bool IsPartOwned(string carId, string partKey)
    {
        var entry = GetStateForCar(carId);
        return entry.ownedParts.Contains(partKey);
    }

    /// <summary>Marks a part as owned.</summary>
    public void MarkPartOwned(string carId, string partKey)
    {
        var entry = GetStateForCar(carId);
        if (!entry.ownedParts.Contains(partKey))
            entry.ownedParts.Add(partKey);
    }

    // ══════════════════ Car Unlock Helpers ══════════════════

    /// <summary>Returns true if the car is unlocked (available for customisation).</summary>
    public bool IsCarUnlocked(string carId)
    {
        if (string.IsNullOrEmpty(carId)) return false;
        // Mazda is always unlocked regardless of save state
        if (carId == DefaultUnlockedCarId) return true;
        return _data.unlockedCarIds.Contains(carId);
    }

    /// <summary>Permanently marks a car as unlocked. Persists immediately.</summary>
    public void MarkCarUnlocked(string carId)
    {
        if (string.IsNullOrEmpty(carId)) return;
        if (_data.unlockedCarIds.Contains(carId)) return;
        _data.unlockedCarIds.Add(carId);
        SaveToPrefs();
        Debug.Log($"[GarageSaveData] Car '{carId}' unlocked.");
    }

    // ══════════════════ Persistence ══════════════════

    public void SaveToPrefs()
    {
        string json = JsonUtility.ToJson(_data);
        PlayerPrefs.SetString(PrefsKey, json);
        Debug.Log($"[GarageSaveData] Saved: {json}");
    }

    public void LoadFromPrefs()
    {
        // Skip if already loaded this frame (Awake + SaveSystem.LoadGame both call this)
        if (_lastLoadFrame == Time.frameCount) return;
        _lastLoadFrame = Time.frameCount;

        if (!PlayerPrefs.HasKey(PrefsKey))
        {
            _data = new GarageStateWrapper();
            Debug.Log("[GarageSaveData] No save found, starting fresh.");
            return;
        }

        string json = PlayerPrefs.GetString(PrefsKey);
        try
        {
            _data = JsonUtility.FromJson<GarageStateWrapper>(json);
            if (_data == null) _data = new GarageStateWrapper();

            // Migration: old saves have no unlockedCarIds — ensure default car is present.
            if (_data.unlockedCarIds == null)
                _data.unlockedCarIds = new List<string>();
            if (!_data.unlockedCarIds.Contains(DefaultUnlockedCarId))
                _data.unlockedCarIds.Add(DefaultUnlockedCarId);

            Debug.Log($"[GarageSaveData] Loaded: selectedCar={_data.selectedCarIndex}, cars={_data.cars.Count}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GarageSaveData] Failed to parse save: {ex.Message}. Starting fresh.");
            _data = new GarageStateWrapper();
        }
    }
}
