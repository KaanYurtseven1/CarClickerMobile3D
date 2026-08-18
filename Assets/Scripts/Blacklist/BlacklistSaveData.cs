using System;
using UnityEngine;

/// <summary>
/// Persistent save model for the entire Blacklist campaign.
/// Serialised via JsonUtility and stored in PlayerPrefs.
/// </summary>
[Serializable]
public class BlacklistSaveData
{
    private const string PREFS_KEY = "Save_BlacklistCampaign";

    // ─── Campaign state ───

    /// <summary>Currently active tier index (6 → 1). 0 = campaign complete.</summary>
    public int currentTierIndex = 6;

    /// <summary>True when all 6 tiers are finished.</summary>
    public bool campaignComplete;

    // ─── Per-mission state for the CURRENT tier ───

    /// <summary>
    /// State of each of the 5 missions in the current tier.
    /// 0 = Pending, 1 = Completed, 2 = Claimed.
    /// </summary>
    public int[] missionStates = new int[5];

    // ─── Baseline snapshot (taken when a tier becomes active) ───

    public double baselineGoldEarned;
    public int baselineWorldNitroCollected;
    public int baselineRadarsDefused;
    public int baselineChestsOpened;
    public int baselineBoostUses;
    public int baselinePoliceEscapes;
    public int baselineNitroRainTriggers;
    public int baselineMagnetCoinsCollected;
    public int baselineTurboUses;

    // ─── Mission state constants ───

    public const int STATE_PENDING = 0;
    public const int STATE_COMPLETED = 1;
    public const int STATE_CLAIMED = 2;

    // ─── Persistence ───

    public void SaveToPrefs()
    {
        string json = JsonUtility.ToJson(this);
        PlayerPrefs.SetString(PREFS_KEY, json);
    }

    public static BlacklistSaveData LoadFromPrefs()
    {
        if (!PlayerPrefs.HasKey(PREFS_KEY))
            return new BlacklistSaveData();

        string json = PlayerPrefs.GetString(PREFS_KEY, "");
        if (string.IsNullOrEmpty(json))
            return new BlacklistSaveData();

        try
        {
            var data = JsonUtility.FromJson<BlacklistSaveData>(json);
            if (data == null) return new BlacklistSaveData();

            // Ensure array is correct length
            if (data.missionStates == null || data.missionStates.Length != 5)
                data.missionStates = new int[5];

            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BlacklistSaveData] Failed to parse save: {e.Message}");
            return new BlacklistSaveData();
        }
    }
}
