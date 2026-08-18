// ════════════════════════════════════════════════════════════════
// PartStatData.cs – Static lookup for part stat bonuses and
//                   group-exclusive equip helpers.
//
// Part bonus data (hardcoded – universal for all cars):
//   Camurluk  → Durability
//   Egzoz     → Speed
//   Kaput     → Acceleration (higher tiers also add Durability)
//   Spoiler   → Speed (higher tiers also add Durability / Acceleration)
//
// Group rule: only ONE part per group can be equipped at a time.
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;

/// <summary>Stat bonus added by a single mod part.</summary>
public struct PartStatBonus
{
    public int durability;
    public int acceleration;
    public int speed;

    public PartStatBonus(int dur, int acc, int spd)
    {
        durability = dur;
        acceleration = acc;
        speed = spd;
    }
}

/// <summary>
/// Hardcoded stat bonuses for the 18 mod parts and group-prefix helpers.
/// </summary>
public static class PartStatData
{
    // ── Bonuses keyed by globalPartKey ──────────────────────────
    public static readonly Dictionary<string, PartStatBonus> Bonuses =
        new Dictionary<string, PartStatBonus>
    {
        // Camurluk → Durability only
        { "Camurluk_1", new PartStatBonus(1, 0, 0) },
        { "Camurluk_2", new PartStatBonus(2, 0, 0) },
        { "Camurluk_3", new PartStatBonus(3, 0, 0) },

        // Egzoz → Speed only
        { "Egzoz_1",    new PartStatBonus(0, 0, 1) },
        { "Egzoz_2",    new PartStatBonus(0, 0, 2) },
        { "Egzoz_3",    new PartStatBonus(0, 0, 3) },

        // Kaput → Acceleration; higher tiers also add Durability
        { "Kaput_1",    new PartStatBonus(0, 1, 0) },
        { "Kaput_2",    new PartStatBonus(0, 2, 0) },
        { "Kaput_3",    new PartStatBonus(0, 3, 0) },
        { "Kaput_4",    new PartStatBonus(1, 3, 0) },
        { "Kaput_5",    new PartStatBonus(1, 4, 0) },
        { "Kaput_6",    new PartStatBonus(2, 4, 0) },
        { "Kaput_7",    new PartStatBonus(2, 5, 0) },

        // Spoiler → Speed; higher tiers also add Durability and/or Acceleration
        { "Spoiler_1",  new PartStatBonus(0, 0, 1) },
        { "Spoiler_2",  new PartStatBonus(0, 0, 2) },
        { "Spoiler_3",  new PartStatBonus(1, 0, 2) },
        { "Spoiler_4",  new PartStatBonus(1, 0, 3) },
        { "Spoiler_5",  new PartStatBonus(2, 1, 3) },
    };

    // ── Group prefixes (one part per group at a time) ──────────
    private static readonly string[] GroupPrefixes =
        { "Camurluk_", "Egzoz_", "Kaput_", "Spoiler_" };

    /// <summary>
    /// Returns the group prefix for the given part key
    /// (e.g. <c>"Camurluk_"</c> for <c>"Camurluk_2"</c>),
    /// or <c>null</c> if the key doesn't belong to a known group.
    /// </summary>
    public static string GetGroupPrefix(string partKey)
    {
        if (string.IsNullOrEmpty(partKey)) return null;
        for (int i = 0; i < GroupPrefixes.Length; i++)
            if (partKey.StartsWith(GroupPrefixes[i]))
                return GroupPrefixes[i];
        return null;
    }
}
