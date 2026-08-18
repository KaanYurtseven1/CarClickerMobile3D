// ════════════════════════════════════════════════════════════════
// ChestTypeDefs.cs — Shared enums and per-type configuration
// for the entire chest system. Add new types or tweak values here.
// ════════════════════════════════════════════════════════════════
using UnityEngine;

public enum ChestState
{
    Idle,
    Unlocking,
    ReadyToOpen,
    OpeningInProgress
}

public enum ChestType
{
    Common = 0,
    Rare = 1,
    Legendary = 2
}

/// <summary>
/// Central configuration for all chest-type-specific tuning.
/// Designers adjust values here — no other file needs changes.
/// </summary>
public static class ChestTypeConfig
{
    // ═══════════════ UNLOCK DURATIONS (seconds) ═══════════════

    public static float GetUnlockDuration(ChestType type)
    {
        switch (type)
        {
            case ChestType.Common: return 10f * 60f;   // 10 min
            case ChestType.Rare: return 30f * 60f;   // 30 min
            case ChestType.Legendary: return 60f * 60f;   // 60 min
            default: return 20f * 60f;
        }
    }

    // ═══════════════ OPEN NOW NITRO COST ═══════════════

    public static int GetOpenNowCost(ChestType type)
    {
        switch (type)
        {
            case ChestType.Common: return 10;
            case ChestType.Rare: return 50;
            case ChestType.Legendary: return 100;
            default: return 15;
        }
    }

    // ═══════════════ DISPLAY NAMES ═══════════════

    public static string GetDisplayName(ChestType type)
    {
        switch (type)
        {
            case ChestType.Common: return "Common Chest";
            case ChestType.Rare: return "Rare Chest";
            case ChestType.Legendary: return "Legendary Chest";
            default: return "Chest";
        }
    }

    // ═══════════════ REWARD SCALING ═══════════════

    /// <summary>Money reward: percentage of current balance.</summary>
    public static void GetMoneyPercentRange(ChestType type, out float min, out float max)
    {
        switch (type)
        {
            case ChestType.Rare: min = 0.10f; max = 0.25f; return;
            case ChestType.Legendary: min = 0.20f; max = 0.40f; return;
            default: min = 0.05f; max = 0.15f; return;
        }
    }

    /// <summary>Nitro reward: percentage of current balance.</summary>
    public static void GetNitroPercentRange(ChestType type, out float min, out float max)
    {
        switch (type)
        {
            case ChestType.Rare: min = 0.10f; max = 0.30f; return;
            case ChestType.Legendary: min = 0.20f; max = 0.40f; return;
            default: min = 0.05f; max = 0.20f; return;
        }
    }

    /// <summary>
    /// Card rarity weights per chest type.
    /// Indices: 0=Common, 1=Rare, 2=Epic, 3=Legendary.
    /// Legendary chests guarantee Epic+ (weight 0 for Common/Rare cards).
    /// </summary>
    public static float[] GetCardRarityWeights(ChestType type)
    {
        switch (type)
        {
            case ChestType.Rare: return new float[] { 30f, 35f, 25f, 10f };
            case ChestType.Legendary: return new float[] { 0f, 20f, 50f, 30f };
            default: return new float[] { 50f, 30f, 15f, 0f };
        }
    }

    /// <summary>True if this chest type can award a sticker as 4th reward.</summary>
    public static bool CanHaveStickerReward(ChestType type)
    {
        return type == ChestType.Rare || type == ChestType.Legendary;
    }

    // ═══════════════ SPAWN WEIGHTS ═══════════════

    public const float BaseWeightCommon = 75f;
    public const float BaseWeightRare = 20f;
    public const float BaseWeightLegendary = 5f;
    public const float MinCommonWeight = 25f;

    /// <summary>
    /// Returns spawn weights adjusted by player money progression.
    /// As money grows, better chest types become more likely.
    /// Common never drops below MinCommonWeight.
    /// </summary>
    public static void GetSpawnWeights(double playerMoney,
        out float common, out float rare, out float legendary)
    {
        // progression: 0 at $0 → approaches 1 as money → ∞
        const double milestone = 1_000_000.0;
        float p = (float)(1.0 - 1.0 / (1.0 + playerMoney / milestone));
        p = Mathf.Clamp01(p);

        common = Mathf.Max(MinCommonWeight, BaseWeightCommon - p * 40f);
        rare = BaseWeightRare + p * 25f;
        legendary = BaseWeightLegendary + p * 15f;
    }
}
