using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Picks and grants a random unowned sticker across the player's unlocked cars.
/// Used by the chest reward system for the 4th reward (Rare/Legendary chests).
/// </summary>
public static class StickerRewardHelper
{
    public struct StickerReward
    {
        public string carId;
        public int stickerIndex;
        public string carDisplayName;
    }

    /// <summary>
    /// Attempts to find a random unowned sticker from any unlocked car.
    /// Returns false if all stickers for all unlocked cars are already owned.
    /// </summary>
    public static bool TryPickRandomSticker(out StickerReward reward)
    {
        // Fallback: try Resources.Load (works only if asset is in a Resources/ folder)
        var db = Resources.Load<GarageDatabaseSO>("GarageDatabase");
        return TryPickRandomSticker(db, out reward);
    }

    /// <summary>
    /// Overload that accepts an explicit database reference (preferred).
    /// </summary>
    public static bool TryPickRandomSticker(GarageDatabaseSO db, out StickerReward reward)
    {
        reward = default;

        if (GarageSaveData.Instance == null)
        {
            Debug.LogWarning("[StickerReward] GarageSaveData.Instance is NULL — cannot search stickers.");
            return false;
        }

        if (db == null || db.cars == null || db.cars.Count == 0)
        {
            Debug.LogWarning("[StickerReward] GarageDatabaseSO is null or empty! " +
                "Make sure garageDatabase is assigned on ChestOpenSceneController.");
            return false;
        }

        Debug.Log($"[StickerReward] Searching {db.cars.Count} car(s) for unowned stickers...");

        var candidates = new List<StickerReward>();

        for (int c = 0; c < db.cars.Count; c++)
        {
            var carData = db.cars[c];
            if (carData == null) continue;

            bool unlocked = GarageSaveData.Instance.IsCarUnlocked(carData.carId);
            if (!unlocked)
            {
                Debug.Log($"[StickerReward]   Car '{carData.carId}' — LOCKED, skipping.");
                continue;
            }

            // Each car has up to 6 stickers; index 0 is free/default (always owned)
            int stickerCount = carData.stickerKeys != null ? carData.stickerKeys.Count : 6;
            int unownedForCar = 0;
            for (int s = 1; s < stickerCount; s++)
            {
                if (!GarageSaveData.Instance.IsStickerOwned(carData.carId, s))
                {
                    unownedForCar++;
                    candidates.Add(new StickerReward
                    {
                        carId = carData.carId,
                        stickerIndex = s,
                        carDisplayName = carData.displayCarName
                    });
                }
            }
            Debug.Log($"[StickerReward]   Car '{carData.carId}' — unlocked, {stickerCount} sticker slots, {unownedForCar} unowned.");
        }

        Debug.Log($"[StickerReward] Total candidates: {candidates.Count}");

        if (candidates.Count == 0) return false;

        reward = candidates[Random.Range(0, candidates.Count)];
        Debug.Log($"[StickerReward] Picked: car='{reward.carId}' stickerIdx={reward.stickerIndex} name='{reward.carDisplayName}'");
        return true;
    }

    /// <summary>Marks the sticker as owned in GarageSaveData and persists.</summary>
    public static void GrantSticker(StickerReward reward)
    {
        if (GarageSaveData.Instance == null) return;

        GarageSaveData.Instance.MarkStickerOwned(reward.carId, reward.stickerIndex);
        GarageSaveData.Instance.SaveToPrefs();
        Debug.Log($"[StickerReward] Granted sticker #{reward.stickerIndex} for car '{reward.carId}'.");
    }
}
