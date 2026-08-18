using System;
using UnityEngine;

/// <summary>
/// Persistent state for pending blacklist rewards and modifiers.
/// Stored in PlayerPrefs via JsonUtility.
///
/// Covers:
///   - Pending free chests (chest chain)
///   - Pending card progress choice
///   - Pending free kaplama choice
///   - Boost cooldown discount modifier
/// </summary>
[Serializable]
public class BlacklistRewardClaimData
{
    private const string PREFS_KEY = "Save_BlacklistRewardClaim";

    // ─── Pending free chests ───
    public int pendingFreeChests;

    // ─── Pending card progress ───
    public int pendingCardProgressAmount;

    // ─── Pending free kaplama ───
    public int pendingFreeKaplamaCount;

    // ─── Boost cooldown discount ───
    public int boostDiscountRemainingUses;
    public float boostDiscountMultiplier;

    // ─── Persistence ───

    public void SaveToPrefs()
    {
        string json = JsonUtility.ToJson(this);
        PlayerPrefs.SetString(PREFS_KEY, json);
    }

    public static BlacklistRewardClaimData LoadFromPrefs()
    {
        if (!PlayerPrefs.HasKey(PREFS_KEY))
            return new BlacklistRewardClaimData();

        string json = PlayerPrefs.GetString(PREFS_KEY, "");
        if (string.IsNullOrEmpty(json))
            return new BlacklistRewardClaimData();

        try
        {
            var data = JsonUtility.FromJson<BlacklistRewardClaimData>(json);
            return data ?? new BlacklistRewardClaimData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BlacklistRewardClaimData] Failed to parse: {e.Message}");
            return new BlacklistRewardClaimData();
        }
    }

    // ─── Boost discount helpers ───

    /// <summary>
    /// Adds boost cooldown discount uses. Stacks on existing remaining uses.
    /// </summary>
    public void AddBoostDiscount(int uses, float multiplier)
    {
        boostDiscountRemainingUses += uses;
        boostDiscountMultiplier = multiplier;
        SaveToPrefs();
    }

    /// <summary>
    /// Consumes one boost discount use. Returns the multiplier to apply, or 1.0 if no discount.
    /// </summary>
    public float ConsumeBoostDiscount()
    {
        if (boostDiscountRemainingUses <= 0)
            return 1f;

        boostDiscountRemainingUses--;
        float mult = boostDiscountMultiplier;

        if (boostDiscountRemainingUses <= 0)
        {
            boostDiscountMultiplier = 0f;
        }

        SaveToPrefs();
        Debug.Log($"[BlacklistRewardClaimData] Boost discount consumed. Remaining: {boostDiscountRemainingUses}, multiplier: {mult}");
        return mult;
    }

    /// <summary>Returns true if there is a pending card progress reward.</summary>
    public bool HasPendingCardProgress => pendingCardProgressAmount > 0;

    /// <summary>Returns true if there is a pending kaplama reward.</summary>
    public bool HasPendingKaplama => pendingFreeKaplamaCount > 0;

    /// <summary>Returns true if there are pending free chests.</summary>
    public bool HasPendingFreeChests => pendingFreeChests > 0;

    /// <summary>Returns true if a boost discount is active.</summary>
    public bool HasBoostDiscount => boostDiscountRemainingUses > 0;
}
