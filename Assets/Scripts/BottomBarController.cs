using UnityEngine;
using UnityEngine.UI;

public class BottomBarController : MonoBehaviour
{
    public static BottomBarController Instance { get; private set; }

    public BottomBarTabUI[] tabs;
    public int defaultTabIndex = 2; // örn: Clicker tab'i başlangıç

    [Header("Ranking Tab Lock")]
    [Tooltip("Index of the Ranking tab in the tabs array (default 4).")]
    [SerializeField] private int rankingTabIndex = 4;

    private int currentIndex = -1;
    private Button _rankingButton;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (RankingService.Instance != null)
            RankingService.Instance.OnPlayerRanked -= OnPlayerBecameRanked;

        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Cache the Ranking tab button
        if (tabs != null && rankingTabIndex >= 0 && rankingTabIndex < tabs.Length && tabs[rankingTabIndex] != null)
            _rankingButton = tabs[rankingTabIndex].GetComponent<Button>();

        // Set initial ranking tab state based on current rank
        UpdateRankingTabInteractable();

        // Listen for the player becoming ranked
        if (RankingService.Instance != null)
            RankingService.Instance.OnPlayerRanked += OnPlayerBecameRanked;

        SetActiveTab(defaultTabIndex);
    }

    private void UpdateRankingTabInteractable()
    {
        if (_rankingButton == null) return;

        bool isRanked = RankingService.Instance != null && RankingService.Instance.PlayerRank > 0;
        _rankingButton.interactable = isRanked;
    }

    private void OnPlayerBecameRanked()
    {
        if (_rankingButton != null)
            _rankingButton.interactable = true;
    }

    public void SetActiveTab(int index)
    {
        if (tabs == null || tabs.Length == 0) return;
        if (index < 0 || index >= tabs.Length) return;

        // Block switching to the ranking tab if unranked
        if (index == rankingTabIndex)
        {
            bool isRanked = RankingService.Instance != null && RankingService.Instance.PlayerRank > 0;
            if (!isRanked)
            {
                Debug.Log("[BottomBar] Ranking tab blocked — player is not yet ranked.");
                return;
            }
        }

        currentIndex = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool selected = (i == index);
            if (tabs[i] != null)
                tabs[i].SetSelected(selected);
        }

        if (PanelTransitionManager.Instance != null)
            PanelTransitionManager.Instance.SwitchTo((BottomTab)index);
    }

    // Button OnClick'lerden çağırmak için
    public void OnTabButtonClicked(int index)
    {
        // U1: UI click SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayUIClick();

        SetActiveTab(index);
    }
}
