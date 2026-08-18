using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controller for Panel_Ranking. Attach to the Panel_Ranking RectTransform.
/// Manages the leaderboard scroll view, header, and loading/empty states.
///
/// Lifecycle:
///   - OnEnable  → subscribes to events, triggers a fetch
///   - OnDisable → unsubscribes from events
///   - OnLeaderboardFetched → rebuilds the entry list
/// </summary>
public class RankingPanelController : MonoBehaviour
{
    // ─── Header ───

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI playerInfoText;

    // ─── Scroll Content ───

    [Header("Scroll View")]
    [Tooltip("The Content RectTransform inside the ScrollRect (entries are spawned here).")]
    [SerializeField] private RectTransform scrollContent;

    [Tooltip("The ScrollRect component on the scroll view.")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("Prefab for a single leaderboard row. Must have RankingEntryView component.")]
    [SerializeField] private RankingEntryView entryPrefab;

    // ─── State Overlays ───

    [Header("State Overlays")]
    [Tooltip("Shown while the leaderboard is loading.")]
    [SerializeField] private GameObject loadingOverlay;

    [Tooltip("Shown when there are no entries or the fetch failed.")]
    [SerializeField] private GameObject emptyOverlay;

    // ─── Refresh Button ───

    [Header("Refresh")]
    [SerializeField] private Button refreshButton;

    [Tooltip("Minimum seconds between manual refreshes.")]
    [SerializeField] private float refreshCooldown = 5f;

    // ─── Internal ───

    private readonly List<RankingEntryView> _spawnedEntries = new List<RankingEntryView>();
    private bool _waitingForFetch;
    private float _lastFetchTime = -999f;

    // ─── Lifecycle ───

    private void OnEnable()
    {
        if (RankingService.Instance != null)
        {
            RankingService.Instance.OnLeaderboardFetched += HandleLeaderboardFetched;
            RankingService.Instance.OnScoreSubmitted     += HandleScoreSubmitted;
        }

        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshClicked);

        EnsureRefreshButtonAtBottom();
        UpdateHeaderInfo();
        RequestFetch();
    }

    private void OnDisable()
    {
        if (RankingService.Instance != null)
        {
            RankingService.Instance.OnLeaderboardFetched -= HandleLeaderboardFetched;
            RankingService.Instance.OnScoreSubmitted     -= HandleScoreSubmitted;
        }

        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(OnRefreshClicked);
    }

    // ─── Data Flow ───

    private void RequestFetch()
    {
        if (RankingService.Instance == null || !RankingService.Instance.IsAuthenticated)
        {
            ShowEmpty();
            return;
        }

        ShowLoading();
        _waitingForFetch = true;
        RankingService.Instance.FetchLeaderboard();
    }

    private void HandleLeaderboardFetched(RankingDataModel.LeaderboardResult result)
    {
        _waitingForFetch = false;
        _lastFetchTime = Time.unscaledTime;
        BuildEntryList(result);
        UpdateHeaderInfo();
    }

    private void HandleScoreSubmitted(long newScore)
    {
        UpdateHeaderInfo();
    }

    private void OnRefreshClicked()
    {
        if (Time.unscaledTime - _lastFetchTime < refreshCooldown)
        {
            Debug.Log("[RankingPanel] Refresh cooldown active, ignoring.");
            return;
        }
        RequestFetch();
    }

    // ─── Build UI ───

    private void BuildEntryList(RankingDataModel.LeaderboardResult result)
    {
        ClearEntries();

        if (result == null || result.entries == null || result.entries.Length == 0)
        {
            ShowEmpty();
            EnsureRefreshButtonAtBottom();
            return;
        }

        HideOverlays();

        string selfId = RankingService.Instance != null ? RankingService.Instance.UserId : "";

        for (int i = 0; i < result.entries.Length; i++)
        {
            var entry = result.entries[i];
            bool isSelf = entry.player_id == selfId;

            var view = Instantiate(entryPrefab, scrollContent);
            view.Bind(entry, isSelf);
            _spawnedEntries.Add(view);
        }

        EnsureRefreshButtonAtBottom();

        // Scroll to self after layout rebuilds
        if (result.selfIndex >= 0)
            StartCoroutine(ScrollToSelfAfterLayout(result.selfIndex));
    }

    private void EnsureRefreshButtonAtBottom()
    {
        if (refreshButton == null || scrollContent == null) return;

        var btnTransform = refreshButton.transform;

        if (btnTransform.parent != scrollContent)
            btnTransform.SetParent(scrollContent, false);

        btnTransform.SetAsLastSibling();
    }

    private void ClearEntries()
    {
        for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
        {
            if (_spawnedEntries[i] != null)
                Destroy(_spawnedEntries[i].gameObject);
        }
        _spawnedEntries.Clear();
    }

    // ─── Scroll To Self (layout-aware) ───

    private IEnumerator ScrollToSelfAfterLayout(int selfIndex)
    {
        if (scrollContent == null || scrollRect == null) yield break;
        if (selfIndex < 0 || selfIndex >= _spawnedEntries.Count) yield break;

        // Force layout rebuild so ContentSizeFitter updates content height
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);

        // Wait one frame for Unity to fully apply layout
        yield return null;

        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);

        RectTransform targetEntry = _spawnedEntries[selfIndex].GetComponent<RectTransform>();
        if (targetEntry == null) yield break;

        RectTransform viewport = scrollRect.viewport;
        if (viewport == null) yield break;

        float contentHeight = scrollContent.rect.height;
        float viewportHeight = viewport.rect.height;

        // No scrolling needed if content fits
        if (contentHeight <= viewportHeight)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            yield break;
        }

        // Entry's top edge position relative to content top
        // In a top-anchored VerticalLayoutGroup, anchoredPosition.y is negative (downward)
        float entryTopInContent = -targetEntry.anchoredPosition.y;
        float entryHeight = targetEntry.rect.height;

        // Center the entry in the viewport
        float targetScrollY = entryTopInContent - (viewportHeight / 2f) + (entryHeight / 2f);

        float maxScroll = contentHeight - viewportHeight;
        targetScrollY = Mathf.Clamp(targetScrollY, 0f, maxScroll);

        // Convert to normalized position (1 = top, 0 = bottom)
        float normalized = 1f - (targetScrollY / maxScroll);
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
    }

    // ─── Header ───

    private void UpdateHeaderInfo()
    {
        if (titleText != null)
            titleText.text = "GLOBAL RANKING";

        if (playerInfoText == null) return;

        if (RankingService.Instance == null || !RankingService.Instance.IsAuthenticated)
        {
            playerInfoText.text = "Connecting...";
            return;
        }

        string name  = RankingService.Instance.DisplayName;
        long   score = RankingService.Instance.LastSubmittedScore;
        int    rank  = RankingService.Instance.PlayerRank;

        if (rank > 0)
            playerInfoText.text = $"{name}  |  Rank #{rank}  |  {FormatScore(score)}";
        else
            playerInfoText.text = $"{name}  |  {FormatScore(score)}";
    }

    // ─── Overlays ───

    private void ShowLoading()
    {
        if (loadingOverlay != null) loadingOverlay.SetActive(true);
        if (emptyOverlay   != null) emptyOverlay.SetActive(false);
    }

    private void ShowEmpty()
    {
        if (loadingOverlay != null) loadingOverlay.SetActive(false);
        if (emptyOverlay   != null) emptyOverlay.SetActive(true);
    }

    private void HideOverlays()
    {
        if (loadingOverlay != null) loadingOverlay.SetActive(false);
        if (emptyOverlay   != null) emptyOverlay.SetActive(false);
    }

    // ─── Utility ───

    private static string FormatScore(long score)
    {
        if (score >= 1_000_000_000) return (score / 1_000_000_000.0).ToString("F1") + "B";
        if (score >= 1_000_000)     return (score / 1_000_000.0).ToString("F1") + "M";
        if (score >= 1_000)         return (score / 1_000.0).ToString("F1") + "K";
        return score.ToString("N0");
    }
}
