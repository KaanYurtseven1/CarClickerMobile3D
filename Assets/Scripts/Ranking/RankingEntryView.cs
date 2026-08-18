using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// View component for a single leaderboard row.
/// Attach to the entry prefab. RankingPanelController populates it via Bind().
/// </summary>
public class RankingEntryView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Image backgroundImage;

    [Header("Colors")]
    [SerializeField] private Color normalColor    = new Color(0.12f, 0.12f, 0.18f, 0.85f);
    [SerializeField] private Color selfColor      = new Color(1f, 0.76f, 0.03f, 0.30f);
    [SerializeField] private Color topThreeColor  = new Color(0.93f, 0.42f, 0.07f, 0.18f);

    public void Bind(RankingDataModel.LeaderboardEntry entry, bool isSelf)
    {
        if (rankText  != null) rankText.text  = "#" + entry.rank;
        if (nameText  != null) nameText.text  = entry.display_name;
        if (scoreText != null) scoreText.text = FormatScore(entry.racer_score);

        if (backgroundImage != null)
        {
            if (isSelf)
                backgroundImage.color = selfColor;
            else if (entry.rank <= 3)
                backgroundImage.color = topThreeColor;
            else
                backgroundImage.color = normalColor;
        }
    }

    private static string FormatScore(long score)
    {
        if (score >= 1_000_000_000) return (score / 1_000_000_000.0).ToString("F1") + "B";
        if (score >= 1_000_000)     return (score / 1_000_000.0).ToString("F1") + "M";
        if (score >= 1_000)         return (score / 1_000.0).ToString("F1") + "K";
        return score.ToString("N0");
    }
}
