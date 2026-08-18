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

        // Ranking tab requires BOTH gates to be open:
        //   1. RankingService says ranking is unlocked (existing gameplay gate).
        //   2. The player has completed the FIRST Garage tutorial visit
        //      (TutorialGate.BottomBarFullyUnlocked, mirrors fifteenDismissed).
        // Either one missing → tab stays locked. This must NOT bypass
        // RankingService — the tutorial gate is layered on top.
        bool rankingSystemUnlocked = RankingService.Instance != null && RankingService.Instance.IsRankingUnlocked;
        bool garageDone = TutorialGate.BottomBarFullyUnlocked;
        _rankingButton.interactable = rankingSystemUnlocked && garageDone;
    }

    private void OnPlayerBecameRanked()
    {
        // Re-evaluate via the gated path so the Garage-tutorial condition is
        // honoured even if the player gets ranked before completing the
        // Garage tutorial.
        UpdateRankingTabInteractable();
    }

    public void SetActiveTab(int index)
    {
        if (tabs == null || tabs.Length == 0) return;
        if (index < 0 || index >= tabs.Length) return;

        // Block switching to the ranking tab if unranked
        if (index == rankingTabIndex)
        {
            bool isUnlocked = RankingService.Instance != null && RankingService.Instance.IsRankingUnlocked;
            if (!isUnlocked)
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
