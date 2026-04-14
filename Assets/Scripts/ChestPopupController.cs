using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class ChestPopupController : MonoBehaviour
{
    public static ChestPopupController Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Root")]
    [SerializeField] private GameObject popupRoot;

    [Header("Outside Click")]
    [Tooltip("Full-screen transparent Image behind the popup panel. Clicking it closes the popup.")]
    [SerializeField] private Button outsideClickBlocker;

    [Header("Animation")]
    [Tooltip("The popup panel RectTransform that will scale in/out. If null, popupRoot's transform is used.")]
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private float animOpenDuration = 0.25f;
    [SerializeField] private float animCloseDuration = 0.18f;
    [SerializeField] private Ease animOpenEase = Ease.OutBack;
    [SerializeField] private Ease animCloseEase = Ease.InBack;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private GameObject timerTextObj;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("State Label")]
    [SerializeField] private GameObject openGetRewardTextObj;

    [Header("Buttons / Objects")]
    [SerializeField] private GameObject openNowObj;
    [SerializeField] private TextMeshProUGUI openNowCostText;
    [SerializeField] private GameObject startUnlockObj;
    [SerializeField] private GameObject halfTimeObj;     // Replaces skip20Obj
    [SerializeField] private GameObject openObj;

    [Header("Per-Type Visuals")]
    [SerializeField] private Image popupBackgroundImage;
    [SerializeField] private Image chestDisplayImage;

    [Header("Per-Type Background Sprites")]
    [SerializeField] private Sprite commonPopupBg;
    [SerializeField] private Sprite rarePopupBg;
    [SerializeField] private Sprite legendaryPopupBg;

    [Header("Per-Type Chest Sprites")]
    [SerializeField] private Sprite commonChestSprite;
    [SerializeField] private Sprite rareChestSprite;
    [SerializeField] private Sprite legendaryChestSprite;

    [Header("Start Unlock Button Visuals")]
    [SerializeField] private Image startUnlockButtonImage;
    [SerializeField] private Sprite commonStartUnlockSprite;
    [SerializeField] private Sprite rareStartUnlockSprite;
    [SerializeField] private Sprite legendaryStartUnlockSprite;
    [SerializeField] private bool useStartUnlockColors = false;
    [SerializeField] private Color commonStartUnlockColor = Color.white;
    [SerializeField] private Color rareStartUnlockColor = Color.white;
    [SerializeField] private Color legendaryStartUnlockColor = Color.white;

    public bool IsPopupOpen => popupRoot != null && popupRoot.activeSelf;

    /// <summary>Index of the chest currently shown in the popup.</summary>
    private int _selectedIndex = -1;

    /// <summary>Prevents input during open/close animations.</summary>
    private bool _isAnimating;

    /// <summary>CanvasGroup on popupRoot — resolved or added at runtime.</summary>
    private CanvasGroup _rootCanvasGroup;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        // Ensure CanvasGroup exists on popupRoot for fade animation
        if (popupRoot != null)
        {
            _rootCanvasGroup = popupRoot.GetComponent<CanvasGroup>();
            if (_rootCanvasGroup == null)
                _rootCanvasGroup = popupRoot.AddComponent<CanvasGroup>();

            popupRoot.SetActive(false);
        }

        // Wire outside-click blocker
        if (outsideClickBlocker != null)
        {
            outsideClickBlocker.onClick.RemoveAllListeners();
            outsideClickBlocker.onClick.AddListener(ClosePopup);
        }
    }

    private void Update()
    {
        if (IsPopupOpen && !_isAnimating) RefreshUI();
    }

    // ======= PUBLIC API =======

    /// <summary>Opens popup for a specific chest index.</summary>
    public void ShowPopupForChest(int chestIndex)
    {
        if (_isAnimating) return;

        Debug.Log($"[ChestPopup] ShowPopupForChest({chestIndex}) called");
        _selectedIndex = chestIndex;
        if (popupRoot == null)
        {
            Debug.LogError("[ChestPopup] popupRoot is NULL! Assign PopupRoot in the ChestPopupController inspector.");
            return;
        }

        // U4: Chest popup open SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayPopupAppear();

        popupRoot.SetActive(true);
        RefreshUI();
        PlayOpenAnimation();
    }

    /// <summary>Legacy: shows first available chest.</summary>
    public void ShowPopupFromInventory() => ShowPopupForChest(0);

    public void ClosePopup()
    {
        if (_isAnimating || !IsPopupOpen) return;
        PlayCloseAnimation();
    }

    /// <summary>Immediately hides the popup with no animation (for forced/internal use).</summary>
    private void ForceCloseImmediate()
    {
        KillPopupTweens();
        if (popupRoot != null) popupRoot.SetActive(false);
        _selectedIndex = -1;
        _isAnimating = false;
    }

    // ======= ANIMATION =======

    private void PlayOpenAnimation()
    {
        KillPopupTweens();
        _isAnimating = true;

        Transform target = popupPanel != null ? popupPanel : popupRoot.transform;

        // Start state: small + transparent
        target.localScale = Vector3.one * 0.5f;
        if (_rootCanvasGroup != null) _rootCanvasGroup.alpha = 0f;

        // Scale
        target.DOScale(Vector3.one, animOpenDuration)
              .SetEase(animOpenEase)
              .SetUpdate(true)
              .SetId(this);

        // Fade
        if (_rootCanvasGroup != null)
        {
            _rootCanvasGroup.DOFade(1f, animOpenDuration * 0.6f)
                            .SetUpdate(true)
                            .SetId(this);
        }

        // Done
        DOVirtual.DelayedCall(animOpenDuration, () => _isAnimating = false)
                 .SetUpdate(true)
                 .SetId(this);
    }

    private void PlayCloseAnimation()
    {
        KillPopupTweens();
        _isAnimating = true;

        Transform target = popupPanel != null ? popupPanel : popupRoot.transform;

        // Scale out
        target.DOScale(Vector3.one * 0.5f, animCloseDuration)
              .SetEase(animCloseEase)
              .SetUpdate(true)
              .SetId(this);

        // Fade out
        if (_rootCanvasGroup != null)
        {
            _rootCanvasGroup.DOFade(0f, animCloseDuration)
                            .SetUpdate(true)
                            .SetId(this);
        }

        // Deactivate after animation finishes
        DOVirtual.DelayedCall(animCloseDuration, () =>
        {
            if (popupRoot != null) popupRoot.SetActive(false);
            _selectedIndex = -1;
            _isAnimating = false;
        }).SetUpdate(true).SetId(this);
    }

    private void KillPopupTweens()
    {
        DOTween.Kill(this);
    }

    private void OnDestroy()
    {
        KillPopupTweens();
    }

    // ======= UI REFRESH =======

    private void RefreshUI()
    {
        if (ChestInventoryManager.Instance == null) return;
        var cd = ChestInventoryManager.Instance.GetChestAt(_selectedIndex);
        if (cd == null) { ClosePopup(); return; }

        if (titleText != null)
            titleText.text = ChestTypeConfig.GetDisplayName(cd.chestType);

        ApplyChestTypeVisuals(cd.chestType);

        // Hide everything first
        SetActive(openGetRewardTextObj, false);
        SetActive(timerTextObj, false);
        SetActive(openNowObj, false);
        SetActive(startUnlockObj, false);
        SetActive(halfTimeObj, false);
        SetActive(openObj, false);

        switch (cd.state)
        {
            case ChestState.Idle:
                SetActive(openGetRewardTextObj, true);
                SetActive(openNowObj, true);
                SetActive(startUnlockObj, true);
                UpdateOpenNowCost(cd.chestType);
                break;

            case ChestState.Unlocking:
                SetActive(timerTextObj, true);
                SetActive(halfTimeObj, true);
                SetActive(openNowObj, true);
                UpdateOpenNowCost(cd.chestType);

                // Disable half-time button if already used
                if (halfTimeObj != null)
                {
                    var btn = halfTimeObj.GetComponent<UnityEngine.UI.Button>();
                    if (btn != null) btn.interactable = !cd.halfTimeUsed;
                    var cg = halfTimeObj.GetComponent<CanvasGroup>();
                    if (cg == null) cg = halfTimeObj.AddComponent<CanvasGroup>();
                    cg.alpha = cd.halfTimeUsed ? 0.4f : 1f;
                }

                if (timerText != null)
                    timerText.text = FormatTime(cd.GetRemainingSeconds());
                break;

            case ChestState.ReadyToOpen:
                SetActive(openGetRewardTextObj, true);
                SetActive(openObj, true);
                break;
        }
    }

    private void UpdateOpenNowCost(ChestType type)
    {
        if (openNowCostText != null)
            openNowCostText.text = ChestTypeConfig.GetOpenNowCost(type).ToString();
    }

    // ======= PER-TYPE VISUALS =======

    private void ApplyChestTypeVisuals(ChestType type)
    {
        if (popupBackgroundImage != null)
        {
            Sprite bg = commonPopupBg;
            switch (type)
            {
                case ChestType.Rare: bg = rarePopupBg != null ? rarePopupBg : commonPopupBg; break;
                case ChestType.Legendary: bg = legendaryPopupBg != null ? legendaryPopupBg : commonPopupBg; break;
            }
            if (bg != null) popupBackgroundImage.sprite = bg;
        }

        if (chestDisplayImage != null)
        {
            Sprite icon = commonChestSprite;
            switch (type)
            {
                case ChestType.Rare: icon = rareChestSprite != null ? rareChestSprite : commonChestSprite; break;
                case ChestType.Legendary: icon = legendaryChestSprite != null ? legendaryChestSprite : commonChestSprite; break;
            }
            if (icon != null) chestDisplayImage.sprite = icon;
        }

        if (startUnlockButtonImage != null)
        {
            Sprite btnSprite = commonStartUnlockSprite;
            switch (type)
            {
                case ChestType.Rare: btnSprite = rareStartUnlockSprite != null ? rareStartUnlockSprite : commonStartUnlockSprite; break;
                case ChestType.Legendary: btnSprite = legendaryStartUnlockSprite != null ? legendaryStartUnlockSprite : commonStartUnlockSprite; break;
            }
            if (btnSprite != null) startUnlockButtonImage.sprite = btnSprite;

            if (useStartUnlockColors)
            {
                switch (type)
                {
                    case ChestType.Rare: startUnlockButtonImage.color = rareStartUnlockColor; break;
                    case ChestType.Legendary: startUnlockButtonImage.color = legendaryStartUnlockColor; break;
                    default: startUnlockButtonImage.color = commonStartUnlockColor; break;
                }
            }
        }
    }

    // ======= BUTTON HANDLERS =======

    public void OnStartUnlockPressed()
    {
        if (ChestInventoryManager.Instance == null) return;
        ChestInventoryManager.Instance.StartUnlock(_selectedIndex);
        RefreshUI();
        if (SaveSystem.Instance != null) SaveSystem.Instance.SaveGame();
    }

    public void OnHalfTimePressed()
    {
        if (ChestInventoryManager.Instance == null) return;
        var cd = ChestInventoryManager.Instance.GetChestAt(_selectedIndex);
        if (cd == null || cd.halfTimeUsed) return;

        AdProvider.ShowRewardedAd(
            onRewarded: () =>
            {
                ChestInventoryManager.Instance.ApplyHalfTime(_selectedIndex);
                RefreshUI();
                if (SaveSystem.Instance != null) SaveSystem.Instance.SaveGame();
            },
            onFailed: () => Debug.Log("[ChestPopup] Ad failed / cancelled.")
        );
    }

    public void OnOpenNowPressed()
    {
        if (ChestInventoryManager.Instance == null) return;
        bool ok = ChestInventoryManager.Instance.OpenNowByNitro(_selectedIndex);
        if (ok)
        {
            RefreshUI();
            if (SaveSystem.Instance != null) SaveSystem.Instance.SaveGame();
        }
        else
        {
            Debug.Log("[ChestPopup] OpenNow failed (not enough nitro or invalid state).");
        }
    }

    public void OnOpenPressed()
    {
        ChestInventoryManager.EnsureInstance();
        ChestSessionManager.EnsureInstance();
        if (ChestInventoryManager.Instance == null) return;

        var chestData = ChestInventoryManager.Instance.MarkChestAsOpening(_selectedIndex);
        if (chestData == null)
        {
            Debug.LogWarning("[ChestPopup] No eligible chest to open.");
            return;
        }

        if (ChestSessionManager.Instance == null)
        {
            Debug.LogError("[ChestPopup] ChestSessionManager is NULL!");
            ChestInventoryManager.Instance.RevertOpeningChestToReady();
            return;
        }

        ChestSessionManager.Instance.BeginSession(chestData);
        ChestInventoryManager.Instance.SetPendingOpenChest(chestData);

        if (ChestShownUI.Instance != null) ChestShownUI.Instance.RefreshSlots();
        if (SaveSystem.Instance != null) SaveSystem.Instance.SaveGame();

        SceneManager.LoadScene("ChestOpenScene");
    }

    // ======= HELPERS =======

    private void SetActive(GameObject go, bool active) { if (go != null) go.SetActive(active); }

    private string FormatTime(float seconds)
    {
        if (seconds < 0) seconds = 0;
        int s = Mathf.CeilToInt(seconds);
        int h = s / 3600;
        int m = (s % 3600) / 60;
        int r = s % 60;
        if (h > 0) return string.Format("{0}h {1:00}m", h, m);
        return string.Format("{0:00}m {1:00}s", m, r);
    }
}