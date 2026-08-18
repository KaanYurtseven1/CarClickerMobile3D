using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popularity bar UI — fill amount + color mapped to 6 popularity stages.
///
/// Stage colors (from PopularityManager.StageColors):
///   Stage1 [0,18)  #2E7F18 (green)
///   Stage2 [18,36) #45731E
///   Stage3 [36,54) #675E24
///   Stage4 [54,72) #8D472B
///   Stage5 [72,90) #B13433
///   Stage6 [90,100] #C82538 (red)
///
/// Within each stage the color smoothly lerps toward the next stage's color.
/// </summary>
public class PopularityUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;

    // Stage boundary thresholds on the 0..1 scale
    private static readonly float[] StageBoundaries = { 0f, 0.18f, 0.36f, 0.54f, 0.72f, 0.90f, 1.0f };

    private bool _subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    /// <summary>
    /// Retry subscription in Start in case PopularityManager.Awake hasn't run yet during OnEnable.
    /// </summary>
    private void Start()
    {
        TrySubscribe();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (fillImage == null)
            Debug.LogWarning("[PopularityUI] fillImage is null — popularity bar will not display.");
#endif
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;
        if (PopularityManager.Instance == null) return;

        PopularityManager.Instance.OnPopularityChanged += UpdateBar;
        UpdateBar(PopularityManager.Instance.Popularity01);
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (PopularityManager.Instance != null)
            PopularityManager.Instance.OnPopularityChanged -= UpdateBar;
        _subscribed = false;
    }

    private void UpdateBar(float value01)
    {
        if (fillImage == null) return;

        fillImage.fillAmount = value01;
        fillImage.color = GetStageColor(value01);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PopularityUI] Bar updated: {value01:P0} stage={PopularityManager.GetStageForValue(value01)}");
#endif
    }

    /// <summary>
    /// Returns a smoothly interpolated color based on the 6 stage colors.
    /// Within each stage band the color lerps toward the next stage's color.
    /// </summary>
    private static Color GetStageColor(float value01)
    {
        Color[] colors = PopularityManager.StageColors;
        if (colors == null || colors.Length == 0)
            return Color.white;

        // Find which band we're in (0..5)
        for (int i = 0; i < colors.Length - 1; i++)
        {
            float lo = StageBoundaries[i];
            float hi = StageBoundaries[i + 1];
            if (value01 < hi || i == colors.Length - 2)
            {
                float t = Mathf.InverseLerp(lo, hi, value01);
                return Color.Lerp(colors[i], colors[i + 1], t);
            }
        }

        return colors[colors.Length - 1];
    }
}
