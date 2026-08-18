using System;

/// <summary>
/// Data structures for leaderboard entries returned from the server.
/// Used by RankingService and consumed by the UI (Phase 4).
/// </summary>
public static class RankingDataModel
{
    /// <summary>
    /// A single row in the leaderboard display.
    /// </summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public string player_id;
        public string display_name;
        public long   racer_score;
        public int    rank;
    }

    /// <summary>
    /// The full leaderboard result: a window of entries around the current player.
    /// </summary>
    [Serializable]
    public class LeaderboardResult
    {
        public LeaderboardEntry[] entries;
        public int selfIndex;      // index of the current player within entries[]
        public int selfRank;       // player's real global rank (0 = unranked)
        public int totalPlayers;   // actual total player count from the server
    }

    // ─── JSON wrappers for JsonUtility (cannot parse root arrays) ───

    [Serializable]
    internal class LeaderboardEntryArray
    {
        public LeaderboardEntry[] items;
    }

    [Serializable]
    internal class PlayerCountResult
    {
        public int count;
    }

    [Serializable]
    internal class PlayerCountArray
    {
        public PlayerCountResult[] items;
    }

    // ─── Windowed leaderboard RPC response wrapper ───

    [Serializable]
    internal class WindowedLeaderboardResponse
    {
        public LeaderboardEntry[] entries;
        public int total_players;
        public int self_rank;
    }

    [Serializable]
    internal class WindowedLeaderboardResponseWrapper
    {
        public WindowedLeaderboardResponse[] items;
    }
}
