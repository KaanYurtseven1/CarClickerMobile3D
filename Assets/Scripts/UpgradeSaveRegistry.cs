using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// DDOL singleton that caches upgrade button levels so they can be
/// saved from any scene (Main, ChestOpenScene, etc.) without
/// needing FindObjectsByType&lt;UpgradeButton&gt;().
///
/// UpgradeButton calls Register() in its Start/LoadFromSave/OnClickUpgrade.
/// SaveSystem reads from this registry instead of searching the scene.
/// </summary>
public class UpgradeSaveRegistry : MonoBehaviour
{
    public static UpgradeSaveRegistry Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    // Key = UpgradeType.ToString(), Value = current level
    private readonly Dictionary<string, int> _cache = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called by UpgradeButton whenever its level changes (load, purchase, etc.).
    /// </summary>
    public void Register(string upgradeType, int level)
    {
        _cache[upgradeType] = level;
    }

    /// <summary>
    /// Returns the cached level for a given upgrade type, or 0 if not registered.
    /// </summary>
    public int GetLevel(string upgradeType)
    {
        return _cache.TryGetValue(upgradeType, out int level) ? level : 0;
    }

    /// <summary>
    /// Returns a snapshot of all registered upgrade types and their levels.
    /// Used by SaveSystem during SaveGame().
    /// </summary>
    public Dictionary<string, int> GetAll()
    {
        return new Dictionary<string, int>(_cache);
    }

    /// <summary>
    /// Returns true if the registry has any entries (i.e., upgrades have
    /// been registered at least once from the Main scene).
    /// </summary>
    public bool HasEntries => _cache.Count > 0;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
