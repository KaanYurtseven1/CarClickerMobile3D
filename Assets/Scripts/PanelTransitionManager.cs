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
    private BottomTab pendingTab;          // the tab we are transitioning TO
    private RectTransform currentPanel;    // null => clicker
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
        if (Instance == this) Instance = null;
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
        // If already on this tab and not mid-transition, nothing to do
        if (targetTab == currentTab && !isTransitioning) return;
        // If mid-transition toward the same tab, let it finish
        if (isTransitioning && targetTab == pendingTab) return;

        // U2/U3: Panel transition SFX
        if (SFXManager.Instance != null)
        {
            if (targetTab == BottomTab.Clicker)
                SFXManager.Instance.PlayPanelClose();
            else
                SFXManager.Instance.PlayPanelOpen();
        }

        // ── Kill any running sequence ──
        if (seq != null && seq.IsActive()) seq.Kill();
        seq = null;

        // Determine who is visually on-screen right now.
        // During a transition the *old* panel may still be animating out while
        // the *pending* panel is animating in. After seq.Kill() both freeze
        // wherever they are. We need to close anything that is still visible.
        RectTransform oldPanel = currentPanel;          // panel that *was* settled before any transition
        RectTransform interruptedPanel = null;          // panel that was mid-open when interrupted

        if (isTransitioning)
        {
            RectTransform pendingPanel = GetPanel(pendingTab);
            // The pending panel was in the process of opening — it's the one to close
            if (pendingPanel != null && pendingPanel.gameObject.activeSelf)
                interruptedPanel = pendingPanel;

            // The old panel that was closing may also still be visible
            if (oldPanel != null && oldPanel != interruptedPanel && oldPanel.gameObject.activeSelf)
            {
                // Snap the old one closed immediately (it was already leaving)
                oldPanel.DOKill();
                CanvasGroup oldCg = GetOrAddCanvasGroup(oldPanel);
                oldCg.DOKill();
                SetPanelActive(oldPanel, false);
            }
        }

        isTransitioning = false;

        // The panel we need to animate closed is whichever one is still on-screen
        RectTransform panelToClose = interruptedPanel ?? oldPanel;

        RectTransform targetPanel = GetPanel(targetTab);
        bool isGoingToClicker = (targetTab == BottomTab.Clicker);

        // Guard: if the target panel reference is missing, abort to avoid corrupted state
        if (!isGoingToClicker && targetPanel == null)
        {
            Debug.LogError($"[PanelTransitionManager] panelRanking or target panel for {targetTab} is not assigned in Inspector! Aborting switch.");
            isTransitioning = false;
            return;
        }

        // ── UI-flow suppression ──
        bool suppress = !isGoingToClicker;
        UIFlowState.IsContentPanelOpen = suppress;
        Debug.Log($"[PanelTransitionManager] SwitchTo {targetTab} — UIFlowState.IsContentPanelOpen = {suppress}");

        // ── Build new sequence ──
        seq = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);
        isTransitioning = true;
        pendingTab = targetTab;

        // --- Step 1: Close whatever is currently visible (from its CURRENT position) ---
        bool hasPanelToClose = (panelToClose != null && panelToClose.gameObject.activeSelf);

        if (hasPanelToClose)
        {
            RectTransform closingRef = panelToClose; // capture for lambda
            seq.Append(ClosePanelFromCurrentState(closingRef));
            seq.AppendCallback(() =>
            {
                SetPanelActive(closingRef, false);
                // If going to Clicker, un-compact topbar and show clicker
                if (isGoingToClicker)
                {
                    if (topBarAnimator != null) topBarAnimator.SetCompact(false);
                    if (clickerRoot != null) clickerRoot.SetActive(true);
                }
            });
        }

        // --- Step 2: Open target panel (unless going to Clicker) ---
        if (!isGoingToClicker && targetPanel != null)
        {
            // Hide clicker root when opening a panel
            if (!hasPanelToClose)
            {
                // Clicker -> Panel: compact the topbar first
                seq.AppendCallback(() =>
                {
                    if (topBarAnimator != null) topBarAnimator.SetCompact(true);
                });
            }
            seq.Append(OpenPanelTween(targetPanel));
        }

        // --- Finalize ---
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

    /// <summary>
    /// Closes a panel from whatever position/alpha it is currently at.
    /// Duration is proportional to how far it still needs to travel,
    /// so a barely-open panel closes almost instantly.
    /// </summary>
    private Tween ClosePanelFromCurrentState(RectTransform panel)
    {
        if (panel == null) return null;

        panel.DOKill();

        float h = GetPanelHeight(panel);
        float targetY = -h;
        float currentY = panel.anchoredPosition.y;

        // How far along the open range (0 = fully closed, 1 = fully open)
        float openRatio = Mathf.Clamp01(1f - Mathf.Abs(currentY) / Mathf.Max(h, 1f));

        // Scale duration proportionally (at least a tiny amount to avoid zero-length tween)
        float duration = Mathf.Max(closeDuration * openRatio, 0.05f);

        CanvasGroup cg = GetOrAddCanvasGroup(panel);
        cg.DOKill();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        Sequence s = DOTween.Sequence()
            .SetLink(panel.gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);
        s.Join(panel.DOAnchorPosY(targetY, duration).SetEase(closeEase));
        s.Join(cg.DOFade(0f, duration * 0.9f).SetEase(Ease.InQuad));
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
