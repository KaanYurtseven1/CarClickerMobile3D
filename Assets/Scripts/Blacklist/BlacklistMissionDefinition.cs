using UnityEngine;

/// <summary>
/// One mission inside a blacklist tier. Serialised inside <see cref="BlacklistTierSO"/>.
/// </summary>
[System.Serializable]
public class BlacklistMissionDefinition
{
    [Tooltip("Which gameplay stat does this mission track?")]
    public BlacklistMissionType missionType;

    [Tooltip("Delta (earned from tier start) or Absolute (current state)?")]
    public BlacklistMissionMode mode;

    [Tooltip("Target value the player must reach to complete the mission.")]
    public double targetValue;

    [Tooltip("Display text shown in the mission row, e.g. 'Earn 50K gold'.")]
    public string description;

    [Tooltip("Icon shown next to the mission description.")]
    public Sprite icon;

    [Header("Reward")]
    [Tooltip("Reward granted when the player claims this mission.")]
    public BlacklistRewardDefinition reward;
}
