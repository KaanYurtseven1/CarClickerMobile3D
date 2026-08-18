using UnityEngine;

/// <summary>
/// Compound reward data for a single blacklist mission.
/// Supports mixing multiple reward components (gold + nitro + reset etc.).
/// Serialised inside <see cref="BlacklistMissionDefinition"/>.
/// </summary>
[System.Serializable]
public class BlacklistRewardDefinition
{
    [Header("Gold")]
    [Tooltip("Gold amount to add. 0 = no gold reward.")]
    public double goldAmount;

    [Header("Nitro")]
    [Tooltip("Nitro coins to add. 0 = no nitro reward.")]
    public int nitroAmount;

    [Header("Resets")]
    public bool popularityReset;
    public bool heatReset;

    [Header("Free Chests")]
    [Tooltip("Number of free chests to give. 0 = none.")]
    public int freeChestCount;

    [Header("Boost Cooldown Discount")]
    [Tooltip("Number of boost uses that get the discount. 0 = none.")]
    public int boostDiscountUses;
    [Tooltip("Cooldown multiplier. 0.5 = 50% reduction (half cooldown).")]
    [Range(0f, 1f)]
    public float boostDiscountMultiplier;

    [Header("Card Progress (player chooses)")]
    [Tooltip("Card copies to add to a player-chosen card. 0 = none.")]
    public int cardProgressAmount;

    [Header("Free Kaplama")]
    [Tooltip("Number of free kaplama/stickers to choose. 0 = none.")]
    public int freeKaplamaCount;

    [Header("Endgame Cosmetics")]
    [Tooltip("If true, unlocks all colors + stickers for every car except the last.")]
    public bool unlockAllCosmeticsForOtherCars;

    [Header("Display")]
    [Tooltip("Icon shown in the RewardPopup. Can be null.")]
    public Sprite rewardIcon;
    [Tooltip("Text shown in the RewardPopup, e.g. '+10,000 Gold'.")]
    public string rewardDisplayText;

    /// <summary>Returns true if this reward requires a deferred player choice (card/kaplama/chest).</summary>
    public bool HasDeferredChoice =>
        freeChestCount > 0 || cardProgressAmount > 0 || freeKaplamaCount > 0;
}
