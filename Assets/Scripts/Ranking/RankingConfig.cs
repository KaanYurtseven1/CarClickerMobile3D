using UnityEngine;

/// <summary>
/// ScriptableObject that holds Supabase configuration for the ranking system.
/// Lives in Assets/Resources/ so RankingService can load it at runtime via Resources.Load.
///
/// Create: Right-click in Project → Create → Ranking → RankingConfig
/// Then fill in the Supabase URL and Anon Key fields.
/// The asset MUST be at: Assets/Resources/RankingConfig.asset
/// </summary>
[CreateAssetMenu(fileName = "RankingConfig", menuName = "Ranking/RankingConfig")]
public class RankingConfig : ScriptableObject
{
    [Header("Supabase Configuration")]
    [Tooltip("Your Supabase project URL, e.g. https://abcdefgh.supabase.co")]
    public string supabaseUrl = "";

    [Tooltip("Your Supabase anon (public) API key. This is NOT a secret.")]
    public string supabaseAnonKey = "";

    [Header("Score Submission")]
    [Tooltip("Seconds between automatic score submissions. 0 = manual only.")]
    public float autoSubmitInterval = 120f;

    [Tooltip("Minimum score change required to trigger a submission.")]
    public long minimumScoreDelta = 10;

    [Tooltip("Minimum seconds between event-triggered submissions.")]
    public float eventSubmitCooldown = 10f;
}
