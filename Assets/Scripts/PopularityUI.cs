using UnityEngine;
using UnityEngine.UI;

public class PopularityUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;

    private static readonly Color LowColor = new Color(0.2f, 0.8f, 0.2f);   // green
    private static readonly Color HighColor = new Color(0.9f, 0.15f, 0.15f); // red

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
        fillImage.color = Color.Lerp(LowColor, HighColor, value01);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PopularityUI] Bar updated: {value01:P0}");
#endif
    }
}
