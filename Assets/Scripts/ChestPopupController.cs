using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    [Header("Open Button (Tutorial Free Override)")]
    [Tooltip("TextMeshPro label inside the Open button. When the chest is one of the first 3 free tutorial Common Chests, this is overridden to 'Open (Free)'.")]
    [SerializeField] private TextMeshProUGUI openButtonText;

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

    /// <summary>Cached original text on the Open button so non-tutorial chests render the designer-authored label.</summary>
    private string _defaultOpenButtonText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        // Cache the original Open button label for non-tutorial chests.
        if (openButtonText != null)
            _defaultOpenButtonText = openButtonText.text;

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

        // [DIAG] Raycast debug: every frame the popup is open, if any pointer was
        // pressed this frame, dump the top UI hits so we can see what is
        // swallowing the click. TEMPORARY — remove once the bug is identified.
        if (IsPopupOpen && WasAnyPointerPressedThisFrame(out Vector2 screenPos))
            LogPopupRaycastUnderPointer(screenPos);
    }

    /// <summary>[DIAG TEMP] Returns true if a mouse/touch was pressed this frame, with screen pos.</summary>
    private static bool WasAnyPointerPressedThisFrame(out Vector2 screenPos)
    {
        screenPos = default;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        { screenPos = Mouse.current.position.ReadValue(); return true; }
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        { screenPos = Touchscreen.current.primaryTouch.position.ReadValue(); return true; }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0)) { screenPos = Input.mousePosition; return true; }
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == UnityEngine.TouchPhase.Began) { screenPos = t.position; return true; }
        }
#endif
        return false;
    }

    /// <summary>[DIAG TEMP] EventSystem.RaycastAll under pointer; log top 10 hits + raycast-relevant components.</summary>
    private static void LogPopupRaycastUnderPointer(Vector2 screenPos)
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[ChestPopup][Raycast] EventSystem.current is NULL — no UI input possible.");
            return;
        }
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, hits);
        Debug.Log($"[ChestPopup][Raycast] pointer={screenPos} hitCount={hits.Count}");
        int n = Mathf.Min(10, hits.Count);
        for (int i = 0; i < n; i++)
        {
            var go = hits[i].gameObject;
            string btn = go.GetComponent<Button>() != null ? "Button" : "-";
            var img = go.GetComponent<Image>();
            string imgRT = img != null ? img.raycastTarget.ToString() : "<no Image>";
            var rawImg = go.GetComponent<RawImage>();
            string rawRT = rawImg != null ? rawImg.raycastTarget.ToString() : "<no RawImage>";
            var cg = go.GetComponentInParent<CanvasGroup>();
            string cgs = cg != null ? $"cg(blocks={cg.blocksRaycasts} interactable={cg.interactable} alpha={cg.alpha} on={cg.gameObject.name})" : "cg=<none>";
            Debug.Log($"[ChestPopup][Raycast] hit[{i}]={GetHierarchyPath(go.transform)} comp={btn} imgRT={imgRT} rawRT={rawRT} {cgs}");
        }
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "<null>";
        var sb = new System.Text.StringBuilder(t.name);
        var p = t.parent;
        int safety = 0;
        while (p != null && safety++ < 10) { sb.Insert(0, p.name + "/"); p = p.parent; }
        return sb.ToString();
    }

    // ======= PUBLIC API =======

    /// <summary>Opens popup for a specific chest index.</summary>
    public void ShowPopupForChest(int chestIndex)
    {
        if (_isAnimating) return;

        Debug.Log($"[ChestPopup][Show] ShowPopupForChest({chestIndex}) called");
        _selectedIndex = chestIndex;
        if (popupRoot == null)
        {
            Debug.LogError("[ChestPopup][Show] popupRoot is NULL! Assign PopupRoot in the ChestPopupController inspector.");
            return;
        }

        // Diagnostic: report the exact ChestData the popup is about to render.
        ChestInventoryManager.ChestData diag = null;
        if (ChestInventoryManager.Instance != null)
            diag = ChestInventoryManager.Instance.GetChestAt(chestIndex);
        bool firstTutShown = TutorialSaveData.Load().firstTutorialPopupShown;
        if (diag != null)
            Debug.Log($"[ChestPopup][Show] chest idx={chestIndex} type={diag.chestType} state={diag.state} isTutorialFreeChest={diag.isTutorialFreeChest} firstTutorialPopupShown={firstTutShown}");
        else
            Debug.LogWarning($"[ChestPopup][Show] ChestData NULL at index {chestIndex} firstTutorialPopupShown={firstTutShown}");

        ApplyTutorialFreeChestOverrides(chestIndex);

        // [DIAG] Pre-RefreshUI snapshot of all click-relevant state.
        LogPopupClickState("PRE-Refresh");

        // U4: Chest popup open SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayPopupAppear();

        popupRoot.SetActive(true);
        RefreshUI();
        PlayOpenAnimation();

        // [DIAG] Post-Refresh snapshot. PlayOpenAnimation initially sets canvasgroup
        // alpha=0 and tweens up — note that until the open animation finishes,
        // _isAnimating is TRUE and OnOpenPressed early-returns from many paths,
        // but the OpenButton click *does* still go through OnOpenPressed.
        LogPopupClickState("POST-Refresh");
    }

    /// <summary>[DIAG TEMP] One-shot snapshot of every component that affects whether OpenButton/outsideClickBlocker can receive a click.</summary>
    private void LogPopupClickState(string label)
    {
        // outsideClickBlocker
        if (outsideClickBlocker == null)
        {
            Debug.LogWarning($"[ChestPopup][Show:{label}] outsideClickBlocker is NULL.");
        }
        else
        {
            var go = outsideClickBlocker.gameObject;
            var img = outsideClickBlocker.GetComponent<Image>();
            var cg = outsideClickBlocker.GetComponentInParent<CanvasGroup>();
            Debug.Log($"[ChestPopup][Show:{label}] outsideClickBlocker activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} interactable={outsideClickBlocker.interactable} " +
                      $"img.raycastTarget={(img != null ? img.raycastTarget.ToString() : "<no Image>")} " +
                      $"cg={(cg != null ? $"blocks={cg.blocksRaycasts} interactable={cg.interactable} alpha={cg.alpha} on={cg.gameObject.name}" : "<none>")}");
        }
        // popupPanel
        if (popupPanel != null)
        {
            Debug.Log($"[ChestPopup][Show:{label}] popupPanel activeSelf={popupPanel.gameObject.activeSelf} activeInHierarchy={popupPanel.gameObject.activeInHierarchy}");
        }
        // openObj
        if (openObj != null)
        {
            Button btn = openObj.GetComponent<Button>();
            Image img = openObj.GetComponent<Image>();
            CanvasGroup cg = openObj.GetComponentInParent<CanvasGroup>();
            Debug.Log($"[ChestPopup][Show:{label}] openObj activeSelf={openObj.activeSelf} activeInHierarchy={openObj.activeInHierarchy} " +
                      $"btn.interactable={(btn != null ? btn.interactable.ToString() : "<no Button>")} " +
                      $"img.raycastTarget={(img != null ? img.raycastTarget.ToString() : "<no Image>")} " +
                      $"cg={(cg != null ? $"blocks={cg.blocksRaycasts} interactable={cg.interactable} alpha={cg.alpha} on={cg.gameObject.name}" : "<none>")} " +
                      $"isAnimating={_isAnimating}");
        }
        else
        {
            Debug.LogWarning($"[ChestPopup][Show:{label}] openObj is NULL.");
        }
        // popupRoot CanvasGroup (animation)
        if (_rootCanvasGroup != null)
            Debug.Log($"[ChestPopup][Show:{label}] popupRoot CG alpha={_rootCanvasGroup.alpha} interactable={_rootCanvasGroup.interactable} blocksRaycasts={_rootCanvasGroup.blocksRaycasts}");
    }

    /// <summary>
    /// Applies tutorial-free-chest overrides: rewrites the Open button label to
    /// "Open (Free)" and (only on the very first tutorial popup ever shown)
    /// disables the outside-click-to-close blocker so the player must press Open.
    /// </summary>
    private void ApplyTutorialFreeChestOverrides(int chestIndex)
    {
        bool isTutorialFree = false;
        if (ChestInventoryManager.Instance != null)
        {
            var cd = ChestInventoryManager.Instance.GetChestAt(chestIndex);
            if (cd != null) isTutorialFree = cd.isTutorialFreeChest;
        }

        if (openButtonText != null)
            openButtonText.text = isTutorialFree ? "Open (Free)" : _defaultOpenButtonText;

        if (outsideClickBlocker != null)
        {
            // IMPORTANT: never use Button.interactable=false to "block" outside-clicks.
            // A disabled Button still SWALLOWS pointer events (its Image.raycastTarget
            // stays true) — if the blocker overlaps the popup panel/OpenButton in any
            // way, every click on OpenButton is eaten by the blocker and OpenButton
            // appears dead.
            //
            // Instead:
            //  • First tutorial popup → fully deactivate the blocker GameObject so it
            //    cannot eat any clicks. There is no UI outside the panel anyway, so
            //    "outside-tap close" is naturally disabled.
            //  • All other popups → re-activate + re-arm the close listener.
            GameObject blockerGo = outsideClickBlocker.gameObject;

            if (isTutorialFree)
            {
                TutorialSaveData data = TutorialSaveData.Load();
                if (!data.firstTutorialPopupShown)
                {
                    // First tutorial popup ever — force the player through Open.
                    // Disable the blocker entirely so it never intercepts OpenButton clicks.
                    if (blockerGo.activeSelf) blockerGo.SetActive(false);
                    outsideClickBlocker.interactable = true; // restored for next popup
                    data.firstTutorialPopupShown = true;
                    data.Save();
                    Debug.Log($"[ChestPopup][Override] FIRST tutorial popup path → blocker.activeSelf={blockerGo.activeSelf} interactable={outsideClickBlocker.interactable} img.raycastTarget={GetRaycastTarget(blockerGo)}");
                }
                else
                {
                    if (!blockerGo.activeSelf) blockerGo.SetActive(true);
                    outsideClickBlocker.interactable = true;
                    Debug.Log($"[ChestPopup][Override] SECOND/THIRD tutorial popup path → blocker.activeSelf={blockerGo.activeSelf} interactable={outsideClickBlocker.interactable} img.raycastTarget={GetRaycastTarget(blockerGo)}");
                }
            }
            else
            {
                if (!blockerGo.activeSelf) blockerGo.SetActive(true);
                outsideClickBlocker.interactable = true;
                Debug.Log($"[ChestPopup][Override] NORMAL popup path → blocker.activeSelf={blockerGo.activeSelf} interactable={outsideClickBlocker.interactable} img.raycastTarget={GetRaycastTarget(blockerGo)}");
            }
        }
    }

    private static string GetRaycastTarget(GameObject go)
    {
        if (go == null) return "<null>";
        var img = go.GetComponent<Image>();
        return img != null ? img.raycastTarget.ToString() : "<no Image>";
    }

    /// <summary>Legacy: shows first available chest.</summary>
    public void ShowPopupFromInventory() => ShowPopupForChest(0);

    public void ClosePopup()
    {
        Debug.Log($"[ChestPopup][Click] Outside blocker clicked / close attempted (isAnimating={_isAnimating} IsPopupOpen={IsPopupOpen})");
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

        // Tutorial/free chest visual override (UI-only, never mutates ChestData/state).
        // Guarantees the popup shows ONLY the Open button labeled "Open (Free)" even if
        // some other code path drifts the state-driven visuals.
        if (cd.isTutorialFreeChest)
            ApplyTutorialFreeChestPopupState();

        // [DIAG] Refresh end-state snapshot — which slots ended up visible + label.
        Debug.Log($"[ChestPopup][Refresh] state={cd.state} isTutorialFree={cd.isTutorialFreeChest} " +
                  $"timer={Active(timerTextObj)} startUnlock={Active(startUnlockObj)} halfTime={Active(halfTimeObj)} openNow={Active(openNowObj)} openObj={Active(openObj)} " +
                  $"openLabel='{(openButtonText != null ? openButtonText.text : "<null>")}' " +
                  $"blocker(active={(outsideClickBlocker != null ? outsideClickBlocker.gameObject.activeSelf.ToString() : "<null>")} interactable={(outsideClickBlocker != null ? outsideClickBlocker.interactable.ToString() : "<null>")})");
    }

    private static string Active(GameObject go) => go == null ? "<null>" : go.activeSelf.ToString();

    /// <summary>
    /// Force the popup visuals into the tutorial/free configuration:
    /// hide timer/start-unlock/half-time/open-now, show open + reward text,
    /// and stamp the Open button label as "Open (Free)". Does NOT mutate
    /// <see cref="ChestInventoryManager.ChestData"/> or any save data —
    /// state ownership stays in the inventory layer.
    /// </summary>
    private void ApplyTutorialFreeChestPopupState()
    {
        SetActive(timerTextObj, false);
        SetActive(startUnlockObj, false);
        SetActive(halfTimeObj, false);
        SetActive(openNowObj, false);
        SetActive(openObj, true);
        SetActive(openGetRewardTextObj, true);
        if (openButtonText != null) openButtonText.text = "Open (Free)";
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
        // Diagnostic: verify the click is actually reaching the controller, plus
        // dump the visual+save state in case the click is being eaten elsewhere.
        Debug.Log("[ChestPopup][Click] OnOpenPressed ENTER");
        if (openObj != null)
        {
            Button btn = openObj.GetComponent<Button>();
            CanvasGroup cg = openObj.GetComponent<CanvasGroup>();
            Debug.Log($"[ChestPopup][Click] OpenButton state: activeSelf={openObj.activeSelf} activeInHierarchy={openObj.activeInHierarchy} " +
                      $"buttonInteractable={(btn != null ? btn.interactable.ToString() : "<no Button>")} " +
                      $"canvasGroup={(cg != null ? $"alpha={cg.alpha} interactable={cg.interactable} blocksRaycasts={cg.blocksRaycasts}" : "<none>")} " +
                      $"isAnimating={_isAnimating}");
        }
        if (outsideClickBlocker != null)
        {
            Image bImg = outsideClickBlocker.GetComponent<Image>();
            Debug.Log($"[ChestPopup][Click] outsideClickBlocker: activeSelf={outsideClickBlocker.gameObject.activeSelf} " +
                      $"activeInHierarchy={outsideClickBlocker.gameObject.activeInHierarchy} " +
                      $"interactable={outsideClickBlocker.interactable} " +
                      $"raycastTarget={(bImg != null ? bImg.raycastTarget.ToString() : "<no Image>")}");
        }
        if (ChestInventoryManager.Instance != null)
        {
            var diag = ChestInventoryManager.Instance.GetChestAt(_selectedIndex);
            if (diag != null)
                Debug.Log($"[ChestPopup][Click] Chest at idx {_selectedIndex}: type={diag.chestType} state={diag.state} isTutorialFreeChest={diag.isTutorialFreeChest}");
            else
                Debug.LogWarning($"[ChestPopup][Click] Chest at idx {_selectedIndex} is NULL.");
        }

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