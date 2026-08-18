using UnityEngine;

[System.Serializable]
public class BuildingDefinition
{
    public BuildingType type;
    public string displayName;

    [Header("Economy")]
    public double baseCost = 100;
    public double costMultiplier = 1.15;
    public double baseProduction = 2; // her bina başına MPS artışı
    public double tapBonusPerLevel = 0; // per-level tap income bonus (e.g., 1.0 for StreetDeals)

    [Header("Limit")]
    public int maxCount = 999;

    [Header("Runtime (read-only in Inspector)")]
    public int count; // kaç tane aldık
}
