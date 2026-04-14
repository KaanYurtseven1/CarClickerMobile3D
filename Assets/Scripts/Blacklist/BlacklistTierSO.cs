using UnityEngine;

/// <summary>
/// Data asset for one Blacklist tier. Create one asset per tier (6 total).
/// Assign missions, car image, and car name in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "BlacklistTier_", menuName = "Blacklist/Tier Definition")]
public class BlacklistTierSO : ScriptableObject
{
    [Header("Tier Identity")]
    [Tooltip("Tier number: 6 = easiest (first), 1 = hardest (last).")]
    [Range(1, 6)]
    public int tierIndex = 6;

    [Tooltip("Display title, e.g. 'BLACKLIST #6'.")]
    public string tierDisplayName = "BLACKLIST #6";

    [Header("Reward Car")]
    [Tooltip("Name of the car the player unlocks after completing this tier.")]
    public string carName;

    [Tooltip("Sprite shown in the panel. Can be null during development.")]
    public Sprite carImage;

    [Tooltip("Car data asset used by the showcase cinematic to spawn the correct car model " +
             "and apply the player's saved skin. Assign the matching CarDataSO here.")]
    public CarDataSO rewardCar;

    [Header("Missions (exactly 5)")]
    public BlacklistMissionDefinition[] missions = new BlacklistMissionDefinition[5];
}
