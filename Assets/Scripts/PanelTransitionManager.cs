using UnityEngine;
using DG.Tweening;

public enum BottomTab
{
    Bank = 0,
    ShopAndCards = 1,
    Clicker = 2,
    TimeWarp = 3,
    Ranking = 4
}

public class PanelTransitionManager : MonoBehaviour
{
    public static PanelTransitionManager Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private GameObject clickerRoot;
    [SerializeField] private TopBarAnimator topBarAnimator;

    [Header("Panels (Match BottomTab order except Clicker)")]
    [SerializeField] private RectTransform panelBank;
    [SerializeField] private RectTransform panelShopCards;
    [SerializeField] private RectTransform panelTimeWarp;
    [SerializeField] private RectTransform panelRanking;

    [Header("Panel Animation")]
    [SerializeField] private float openDuration = 0.30f;
    [SerializeField] private float closeDuration = 0.25f;
    [SerializeField] private Ease openEase = Ease.OutCubic;
    [SerializeField] private Ease closeEase = Ease.InCubic;

    private BottomTab currentTab = BottomTab.Clicker;
    private RectTransform currentPanel; // null => clicker
    private Sequence seq;
    private bool isTransitioning = false;

    private void OnDestroy()
    {
        // Kill main sequence to prevent callbacks after destroy
        if (seq != null)
        {
            seq.Kill();
            seq = null;
        }
        isTransitioning = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // First state: Clicker
        SetPanelActive(panelBank, false);
        SetPanelActive(panelShopCards, false);
        SetPanelActive(panelTimeWarp, false);
        SetPanelActive(panelRanking, false);

        if (clickerRoot != null) clickerRoot.SetActive(true);

        if (topBarAnimator != null)
            topBarAnimator.SetCompact(false); // normal

        // Ensure suppression is OFF at scene start (Clicker is default)
        UIFlowState.IsContentPanelOpen = false;
    }

    public void SwitchTo(BottomTab targetTab)
    {
        if (isTransitioning) return;
        if (targetTab == currentTab) return;

        RectTransform targetPanel = GetPanel(targetTab);
        bool isGoingToClicker = (targetTab == BottomTab.Clicker);
        bool isCurrentlyPanelOpen = (currentPanel != null);

        // ── UI-flow suppression: set IMMEDIATELY so spawners/taps freeze
        //    even while the panel-open animation is playing. ──
        bool suppress = !isGoingToClicker;
        UIFlowState.IsContentPanelOpen = suppress;
        Debug.Log($"[PanelTransitionManager] SwitchTo {targetTab} — UIFlowState.IsContentPanelOpen = {suppress}");

        if (seq != null && seq.IsActive()) seq.Kill();

        // Create new sequence with SetUpdate(true) for UI (ignores timeScale)
        seq = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);
        isTransitioning = true;

        // --- CASE 1: Panel -> Clicker (close current, no open) ---
        if (isCurrentlyPanelOpen && isGoingToClicker)
        {
            RectTransform panelToClose = currentPanel;
            seq.Append(ClosePanelTween(panelToClose));
            seq.AppendCallback(() =>
            {
                SetPanelActive(panelToClose, false);
                if (topBarAnimator != null) topBarAnimator.SetCompact(false);
                if (clickerRoot != null) clickerRoot.SetActive(true);
            });
            seq.OnComplete(() =>
            {
                currentPanel = null;
                currentTab = BottomTab.Clicker;
                isTransitioning = false;
            });
            seq.Play();
            return;
        }

        // --- CASE 2: Panel -> Panel (close current, then open target) ---
        if (isCurrentlyPanelOpen && !isGoingToClicker)
        {
            RectTransform panelToClose = currentPanel;
            // 1) Close current panel (animate down)
            seq.Append(ClosePanelTween(panelToClose));
            seq.AppendCallback(() =>
            {
                SetPanelActive(panelToClose, false);
            });
            // 2) Then open target panel (animate up)
            seq.Append(OpenPanelTween(targetPanel));
            seq.OnComplete(() =>
            {
                currentPanel = targetPanel;
                currentTab = targetTab;
                isTransitioning = false;
            });
            seq.Play();
            return;
        }

        // --- CASE 3: Clicker -> Panel (just open target) ---
        // 1) Compact topbar
        seq.AppendCallback(() =>
        {
            if (topBarAnimator != null) topBarAnimator.SetCompact(true);
        });
        // 2) Open target panel
        seq.Append(OpenPanelTween(targetPanel));
        seq.OnComplete(() =>
        {
            currentPanel = targetPanel;
            currentTab = targetTab;
            isTransitioning = false;
        });
        seq.Play();
    }

    // ---------- Panel Tweens ----------

    private Tween OpenPanelTween(RectTransform panel)
    {
        if (panel == null) return null;

        // Kill any existing tweens on this panel to prevent fighting
        panel.DOKill();

        SetPanelActive(panel, true);

        float h = GetPanelHeight(panel);
        panel.anchoredPosition = new Vector2(0f, -h);

        CanvasGroup cg = GetOrAddCanvasGroup(panel);
        cg.DOKill(); // Kill any existing fade tweens
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        Sequence s = DOTween.Sequence()
            .SetLink(panel.gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);
        s.Join(panel.DOAnchorPosY(0f, openDuration).SetEase(openEase));
        s.Join(cg.DOFade(1f, openDuration * 0.9f).SetEase(Ease.OutQuad));
        s.OnComplete(() =>
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        });
        return s;
    }

    private Tween ClosePanelTween(RectTransform panel)
    {
        if (panel == null) return null;

        // Kill any existing tweens on this panel to prevent fighting
        panel.DOKill();

        float h = GetPanelHeight(panel);

        CanvasGroup cg = GetOrAddCanvasGroup(panel);
        cg.DOKill(); // Kill any existing fade tweens
        cg.interactable = false;
        cg.blocksRaycasts = false;

        Sequence s = DOTween.Sequence()
            .SetLink(panel.gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);
        s.Join(panel.DOAnchorPosY(-h, closeDuration).SetEase(closeEase));
        s.Join(cg.DOFade(0f, closeDuration * 0.9f).SetEase(Ease.InQuad));
        // NOTE: SetPanelActive(false) is now handled by the caller after this tween completes
        return s;
    }

    // ---------- Helpers ----------

    private RectTransform GetPanel(BottomTab tab)
    {
        switch (tab)
        {
            case BottomTab.Bank: return panelBank;
            case BottomTab.ShopAndCards: return panelShopCards;
            case BottomTab.TimeWarp: return panelTimeWarp;
            case BottomTab.Ranking: return panelRanking;
            case BottomTab.Clicker: return null;
            default: return null;
        }
    }

    private void SetPanelActive(RectTransform panel, bool active)
    {
        if (panel != null) panel.gameObject.SetActive(active);
    }

    private CanvasGroup GetOrAddCanvasGroup(RectTransform rt)
    {
        if (rt == null) return null;
        var cg = rt.GetComponent<CanvasGroup>();
        if (!cg) cg = rt.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    private float GetPanelHeight(RectTransform rt)
    {
        float h = rt.rect.height;
        if (h <= 1f) h = Screen.height;
        return h;
    }
}
