/// <summary>
/// How a mission's progress is evaluated.
/// </summary>
public enum BlacklistMissionMode
{
    /// <summary>Progress = lifetimeCounter − baselineSnapshot at tier start.</summary>
    DeltaFromTierStart,

    /// <summary>Progress = current absolute game state (e.g. buildings owned).</summary>
    AbsoluteState
}
