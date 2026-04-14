using System;
using UnityEngine;

/// <summary>
/// Represents an active chest-opening transaction.
/// Persisted to PlayerPrefs as JSON for crash recovery.
/// Contains all data needed to safely commit or revert a chest opening.
/// </summary>
[Serializable]
public class ChestOpeningSession
{
    public string sessionId;

    /// <summary>Copy of the chest data at the time the session started.</summary>
    public ChestInventoryManager.ChestData chestData;

    // ── Committed reward values (serializable, no Sprites) ──
    public bool rewardsComputed;
    public double committedMoneyGained;
    public int committedNitroReward;
    public int committedCardType;   // CardType cast to int for JSON safety
    public int committedCardCopies;

    // ── Sticker reward ──
    public bool committedHasSticker;
    public string committedStickerCarId;
    public int committedStickerIndex;

    /// <summary>True once rewards have been applied to player data and saved.</summary>
    public bool rewardsCommitted;

    /// <summary>True once the player has completed the full reveal and exited.</summary>
    public bool revealCompleted;

    public long createdAtUtcTicks;
    public int sessionVersion;

    public const int CurrentVersion = 2;

    /// <summary>Maximum age (hours) before a session is considered stale and is force-cleaned.</summary>
    public const int StaleSessionHours = 48;

    public ChestOpeningSession() { }

    public static ChestOpeningSession Create(ChestInventoryManager.ChestData chest)
    {
        return new ChestOpeningSession
        {
            sessionId = Guid.NewGuid().ToString(),
            chestData = chest,
            rewardsComputed = false,
            rewardsCommitted = false,
            revealCompleted = false,
            createdAtUtcTicks = DateTime.UtcNow.Ticks,
            sessionVersion = CurrentVersion
        };
    }

    public bool IsStale()
    {
        var age = DateTime.UtcNow - new DateTime(createdAtUtcTicks, DateTimeKind.Utc);
        return age.TotalHours > StaleSessionHours;
    }
}
