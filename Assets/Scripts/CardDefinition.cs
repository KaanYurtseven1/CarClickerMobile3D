using UnityEngine;

public enum CardRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

[System.Serializable]
public class CardDefinition
{
    public CardType type;
    public string displayName;

    [Header("Visual")]
    [Tooltip("Small icon used in card collection UI and card slot thumbnails.")]
    public Sprite icon;

    [Tooltip("Full card artwork / background sprite shown in the world reward card reveal."
           + " If left empty, falls back to the icon sprite.")]
    public Sprite cardArtSprite;

    public CardRarity rarity = CardRarity.Common;

    [Header("Level (infinite - no cap)")]
    public int currentLevel = 0;
    // MaxLevelCap REMOVED - cards can now level up forever.

    [Header("Segments (formerly Copies)")]
    [Tooltip("Segment balance toward next upgrade. 8 segments = 1 upgrade.")]
    public int copiesOwned = 0;
    // NOTE: Field kept as 'copiesOwned' for SaveSystem compatibility.
    // Semantically this is now "segmentBalance".

    /// <summary>Convenience alias - reads/writes copiesOwned.</summary>
    public int segmentBalance
    {
        get => copiesOwned;
        set => copiesOwned = value;
    }

    [Header("Upgrade Cost")]
    [Tooltip("Base cost for level 0 to 1 upgrade")]
    public double baseUpgradeCost = 10;

    [Tooltip("Cost multiplier applied per level")]
    public double costMultiplier = 1.4;

    /// <summary>
    /// Returns the money cost to upgrade from currentLevel to currentLevel+1.
    /// Uses double to safely handle high levels without overflow.
    /// </summary>
    public double GetUpgradeCost()
    {
        return baseUpgradeCost * System.Math.Pow(costMultiplier, currentLevel);
    }

    /// <summary>
    /// Segments needed for the next upgrade - always 8.
    /// </summary>
    public int GetCopiesRequiredForNextLevel()
    {
        return CardDropTuning.SegmentsPerUpgrade;
    }

    /// <summary>
    /// Returns progress toward next upgrade as 0..1 float.
    /// Clamped: min(segmentBalance, 8) / 8.
    /// </summary>
    public float GetUpgradeProgress01()
    {
        int shown = Mathf.Clamp(copiesOwned, 0, CardDropTuning.SegmentsPerUpgrade);
        return (float)shown / CardDropTuning.SegmentsPerUpgrade;
    }

    /// <summary>
    /// Returns the number of filled segments (0..8) for UI display.
    /// </summary>
    public int GetFilledSegments()
    {
        return Mathf.Clamp(copiesOwned, 0, CardDropTuning.SegmentsPerUpgrade);
    }

    /// <summary>
    /// Returns display text for upgrade progress: "X/8".
    /// No "MAX" state - levels are infinite.
    /// </summary>
    public string GetUpgradeProgressText()
    {
        int shown = Mathf.Clamp(copiesOwned, 0, CardDropTuning.SegmentsPerUpgrade);
        return $"{shown}/{CardDropTuning.SegmentsPerUpgrade}";
    }

    /// <summary>
    /// Returns true if the card has been unlocked (level > 0 or has segments).
    /// </summary>
    public bool IsUnlocked => (currentLevel > 0 || copiesOwned > 0);

    /// <summary>
    /// Returns true if the player can afford the upgrade (segments >= 8).
    /// Cost check is separate - this only checks segment balance.
    /// </summary>
    public bool HasEnoughSegments => copiesOwned >= CardDropTuning.SegmentsPerUpgrade;
}