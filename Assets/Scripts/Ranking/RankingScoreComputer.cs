using UnityEngine;
using System;

/// <summary>
/// Reads game state from all managers and computes the composite Racer Score.
/// Also provides the individual score components that the server needs for validation.
/// </summary>
public static class RankingScoreComputer
{
    // ─── Score Component Data ───

    [Serializable]
    public class ScoreComponents
    {
        public double totalMoneyEarned;
        public int    totalBuildingCount;
        public int    cardLevelSum;
        public int    highestBuildingTier;
        public int    blacklistTiersCompleted;
        public long   racerScore;
    }

    // ─── Public API ───

    /// <summary>
    /// Snapshot the current game state and compute the composite Racer Score.
    /// Returns all individual components so the server can re-verify.
    /// </summary>
    public static ScoreComponents Compute()
    {
        var c = new ScoreComponents();

        // 1. Total money earned (cumulative, never decreases)
        if (CurrencyManager.Instance != null)
            c.totalMoneyEarned = CurrencyManager.Instance.totalMoneyEarned;

        // 2. Total building count (sum of all building quantities)
        if (BuildingManager.Instance != null)
            c.totalBuildingCount = BuildingManager.Instance.GetTotalBuildingCount();

        // 3. Card level sum
        if (CardManager.Instance != null)
        {
            int sum = 0;
            foreach (var card in CardManager.Instance.cards)
                sum += card.currentLevel;
            c.cardLevelSum = sum;
        }

        // 4. Highest unlocked building tier (0-27)
        if (BuildingManager.Instance != null)
        {
            int highest = -1;
            foreach (var b in BuildingManager.Instance.buildings)
            {
                if (b.count > 0)
                {
                    int tier = (int)b.type;
                    if (tier > highest) highest = tier;
                }
            }
            c.highestBuildingTier = Mathf.Max(0, highest);
        }

        // 5. Blacklist tiers completed (tier index counts down from 6 to 1)
        if (BlacklistManager.Instance != null)
        {
            if (BlacklistManager.Instance.CampaignComplete)
                c.blacklistTiersCompleted = 6;
            else
                c.blacklistTiersCompleted = 6 - BlacklistManager.Instance.SaveData.currentTierIndex;
        }

        // Composite score formula
        c.racerScore = ComputeFormula(c);

        return c;
    }

    /// <summary>
    /// Pure formula — no game-state dependency. Also used by the server.
    /// floor( (totalMoneyEarned^0.85 * 0.0001)
    ///      + (totalBuildingCount * 50)
    ///      + (cardLevelSum * 200)
    ///      + (highestBuildingTier * 1000)
    ///      + (blacklistTiersCompleted * 5000) )
    /// </summary>
    public static long ComputeFormula(ScoreComponents c)
    {
        double score = Math.Pow(c.totalMoneyEarned, 0.85) * 0.0001
                     + c.totalBuildingCount * 50.0
                     + c.cardLevelSum * 200.0
                     + c.highestBuildingTier * 1000.0
                     + c.blacklistTiersCompleted * 5000.0;

        return (long)Math.Floor(score);
    }
}
