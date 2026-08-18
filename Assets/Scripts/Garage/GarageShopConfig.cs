// ════════════════════════════════════════════════════════════════
// GarageShopConfig.cs – Inspector-editable pricing for all
// purchasable garage items.  Attach to any GO in NewGarage or
// create as asset via  Assets ▸ Create ▸ Garage ▸ Shop Config.
//
// • Color costs  → NITRO COIN
// • Sticker costs → NITRO COIN
// • Part costs   → GOLD
//
// Prices are set per-INDEX (shared across all cars).
// Index 0 for colors/stickers is always FREE (the default).
// ════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GarageShopConfig", menuName = "Garage/Shop Config")]
public class GarageShopConfig : ScriptableObject
{
    // ─────────────────── Color Prices (Nitro Coin) ───────────────────
    [Header("─── Color Prices (Nitro Coin) ───")]
    [Tooltip("6 entries, one per color index.  Index 0 = default (free).")]
    public List<int> colorCosts = new List<int> { 0, 50, 50, 75, 75, 100 };

    // ─────────────────── Sticker Prices (Nitro Coin) ───────────────────
    [Header("─── Sticker / Kaplama Prices (Nitro Coin) ───")]
    [Tooltip("6 entries, one per sticker index.  Index 0 = default (free).")]
    public List<int> stickerCosts = new List<int> { 0, 30, 40, 50, 60, 80 };

    // ─────────────────── Part Prices (Gold) ───────────────────
    [Header("─── Mod Part Prices (Gold) ───")]
    [Tooltip("18 entries, one per part key index (same order as GarageDatabaseSO.globalPartKeys).")]
    public List<double> partCosts = new List<double>
    {
        // Camurluk 1-3
        500, 1000, 2000,
        // Egzoz 1-3
        500, 1000, 2000,
        // Kaput 1-7
        500, 750, 1000, 1500, 2000, 3000, 5000,
        // Spoiler 1-5
        500, 1000, 2000, 3000, 5000
    };

    // ══════════════════ Accessors ══════════════════

    /// <summary>Nitro cost for the given color index.</summary>
    public int GetColorCost(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= colorCosts.Count) return 0;
        return colorCosts[colorIndex];
    }

    /// <summary>Nitro cost for the given sticker index.</summary>
    public int GetStickerCost(int stickerIndex)
    {
        if (stickerIndex < 0 || stickerIndex >= stickerCosts.Count) return 0;
        return stickerCosts[stickerIndex];
    }

    /// <summary>Gold cost for the given part key index.</summary>
    public double GetPartCost(int partIndex)
    {
        if (partIndex < 0 || partIndex >= partCosts.Count) return 0;
        return partCosts[partIndex];
    }

    /// <summary>Gold cost for the given part key string.</summary>
    public double GetPartCostByKey(string partKey, List<string> globalPartKeys)
    {
        if (globalPartKeys == null) return 0;
        int idx = globalPartKeys.IndexOf(partKey);
        return GetPartCost(idx);
    }
}
