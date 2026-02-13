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
    public Sprite icon;   // 👈 Kart ikonu (inspector’dan atacağız)

    public CardRarity rarity = CardRarity.Common;

    [Header("Level")]
    public int currentLevel = 0;
    public const int MaxLevelCap = 6; // Hard cap for all cards

    [Header("Copies (Chest drop)")]
    [Tooltip("Toplam sahip olunan kart kopyası")]
    public int copiesOwned = 0;

    [Header("Upgrade Cost")]
    [Tooltip("Level 0 → 1 için temel maliyet")]
    public double baseUpgradeCost = 10;

    [Tooltip("Her levelde maliyeti çarpan")]
    public double costMultiplier = 1.4;

    public double GetUpgradeCost()
    {
        return baseUpgradeCost * System.Math.Pow(costMultiplier, currentLevel);
    }

    /// <summary>
    /// Returns true if card is at max level (6).
    /// </summary>
    public bool IsMaxLevel => currentLevel >= MaxLevelCap;

    /// <summary>
    /// Returns copies needed to upgrade from current level to next.
    /// Formula: needCopies = currentLevel + 1
    /// Returns 0 if already at max level.
    /// </summary>
    public int GetCopiesRequiredForNextLevel()
    {
        if (IsMaxLevel) return 0;
        return currentLevel + 1;
    }

    /// <summary>
    /// Returns progress toward next upgrade as 0..1 float.
    /// Uses clamped value: shownHave = clamp(copiesOwned, 0, need)
    /// Returns 1.0 if at max level.
    /// </summary>
    public float GetUpgradeProgress01()
    {
        if (IsMaxLevel) return 1f;
        int need = GetCopiesRequiredForNextLevel();
        if (need <= 0) return 1f;
        int shownHave = Mathf.Clamp(copiesOwned, 0, need);
        return (float)shownHave / need;
    }

    /// <summary>
    /// Returns display text for upgrade progress.
    /// Format: "{shownHave}/{need}" where shownHave is clamped to [0, need].
    /// Returns "MAX" if at max level.
    /// </summary>
    public string GetUpgradeProgressText()
    {
        if (IsMaxLevel) return "MAX";
        int need = GetCopiesRequiredForNextLevel();
        int shownHave = Mathf.Clamp(copiesOwned, 0, need);
        return $"{shownHave}/{need}";
    }

    // Sadece yardımcı property, serialize edilmiyor
    public bool IsUnlocked => (currentLevel > 0 || copiesOwned > 0);
}
