using TMPro;
using UnityEngine;

/// <summary>
/// Debug-only UI binder that displays the current ranking profile display name (e.g. #R17).
/// Assign a TextMeshProUGUI in Inspector.
/// </summary>
public class DebugUserNameTextBinder : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI userNameText;
    [SerializeField] private string loadingText = "...";

    private void OnEnable()
    {
        if (RankingService.Instance != null)
        {
            RankingService.Instance.OnAuthCompleted += HandleAuthCompleted;
            RefreshText();
        }
        else
        {
            SetLoadingText();
        }
    }

    private void OnDisable()
    {
        if (RankingService.Instance != null)
            RankingService.Instance.OnAuthCompleted -= HandleAuthCompleted;
    }

    private void HandleAuthCompleted()
    {
        RefreshText();
    }

    private void RefreshText()
    {
        if (userNameText == null) return;

        if (RankingService.Instance != null && RankingService.Instance.IsAuthenticated)
        {
            string displayName = RankingService.Instance.DisplayName;
            userNameText.text = string.IsNullOrEmpty(displayName) ? loadingText : displayName;
            return;
        }

        SetLoadingText();
    }

    private void SetLoadingText()
    {
        if (userNameText != null)
            userNameText.text = loadingText;
    }
}
