using UnityEngine;

/// <summary>
/// Central tuning constants for the card drop system.
/// All chest-related probabilities and decay parameters live here
/// so designers can adjust them in one place.
/// </summary>
public static class CardDropTuning
{
    // =====================================================================
    //  SEGMENTS PER UPGRADE (constant — always 8)
    // =====================================================================
    public const int SegmentsPerUpgrade = 8;

    // =====================================================================
    //  RARITY BASE WEIGHTS  (used when selecting WHICH card drops)
    //  Indices: 0 = Common, 1 = Rare, 2 = Epic, 3 = Legendary
    // =====================================================================
    public static readonly float[] RarityBaseWeights = { 50f, 30f, 15f, 5f };

    // =====================================================================
    //  LEVEL-BASED SELECTION DECAY
    //  weight(card) = rarityWeight * LevelDecay(card.level)
    //  LevelDecay(level) = max( DecayFloor, 1 / (1 + level * DecayFactor) )
    // =====================================================================

    /// <summary>Decay steepness. Higher = faster decay with level.</summary>
    public const float DecayFactor = 0.25f;

    /// <summary>Minimum floor so probability never reaches 0.</summary>
    public const float DecayFloor = 0.15f;

    /// <summary>
    /// Returns a 0..1 multiplier that decreases as card level rises.
    /// Never drops below DecayFloor.
    /// </summary>
    public static float LevelDecay(int level)
    {
        float raw = 1f / (1f + level * DecayFactor);
        return Mathf.Max(DecayFloor, raw);
    }

    // =====================================================================
    //  SEGMENT MULTIPLIER SYSTEM
    //  Chest grants {1, 2, 4, 8} segments for the selected card.
    //  Higher multipliers become rarer as card level increases.
    // =====================================================================

    /// <summary>Possible multiplier values.</summary>
    public static readonly int[] Multipliers = { 1, 2, 4, 8 };

    /// <summary>
    /// Base weights for each multiplier at level 0.
    /// Order matches Multipliers array: {1x, 2x, 4x, 8x}.
    /// </summary>
    public static readonly float[] MultiplierBaseWeights = { 30f, 35f, 25f, 10f };

    /// <summary>
    /// Per-level decay applied to each multiplier's weight.
    /// Higher values = that multiplier decays faster with level.
    /// 1x decays slowly (stays dominant), 8x decays fast (becomes very rare).
    /// </summary>
    public static readonly float[] MultiplierDecayFactors = { 0.02f, 0.12f, 0.25f, 0.40f };

    /// <summary>
    /// Minimum weight floor for each multiplier so it never hits 0.
    /// </summary>
    public static readonly float[] MultiplierFloors = { 5f, 2f, 1f, 0.5f };

    /// <summary>
    /// Returns a segment multiplier (1, 2, 4, or 8) using weighted random
    /// that shifts toward lower values as card level increases.
    /// </summary>
    public static int GetCardDropMultiplier(int cardLevel)
    {
        float totalWeight = 0f;
        int count = Multipliers.Length;
        float[] weights = new float[count];

        for (int i = 0; i < count; i++)
        {
            // Decay: weight = max(floor, baseWeight / (1 + level * decayFactor))
            float w = MultiplierBaseWeights[i] / (1f + cardLevel * MultiplierDecayFactors[i]);
            w = Mathf.Max(MultiplierFloors[i], w);
            weights[i] = w;
            totalWeight += w;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return Multipliers[i];
        }

        // Fallback (should never reach here)
        return 1;
    }
}
