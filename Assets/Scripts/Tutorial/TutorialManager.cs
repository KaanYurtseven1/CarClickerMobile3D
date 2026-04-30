using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    /// <summary>Scene-scoped singleton. Used by post-tutorial hooks
    /// (e.g. <see cref="BlacklistPanelController"/>) to notify the manager
    /// of one-time UI events without scene-search overhead.</summary>
    public static TutorialManager Instance { get; private set; }
    [Header("Tutorial Objects")]
    [SerializeField] private GameObject tutorialRoot;
    [SerializeField] private GameObject dim;
    [SerializeField] private GameObject one;
    [SerializeField] private GameObject two;
    [SerializeField] private GameObject three;
    [SerializeField] private GameObject four;
    [SerializeField] private GameObject five;
    [SerializeField] private GameObject six;
    [SerializeField] private GameObject seven;

    [Header("Step 7/8 \u2014 Chest Tutorial")]
    [Tooltip("World ChestSpawner used to deterministically force-spawn the first tutorial Common Chest after 3 Nitros are collected.")]
    [SerializeField] private ChestSpawner chestSpawner;
    [Tooltip("Optional small delay before the Seven popup appears after the tutorial chest is collected.")]
    [SerializeField] private float sevenOpenDelay = 0.4f;

    [Header("Steps 9\u201312 \u2014 Post-First-Free-Chest Cards Tutorial")]
    [Tooltip("UI_Tutorial/Three_New pointer GameObject \u2014 second-show pointer aimed at BottomBar/Shop&Cards after the first free chest is opened. Distinct from UI_Tutorial/Three (used by original Step 3).")]
    [SerializeField] private GameObject threeNew;
    [Tooltip("UI_Tutorial/Nine pointer GameObject \u2014 highlights Btn_TabCards.")]
    [SerializeField] private GameObject nine;
    [Tooltip("UI_Tutorial/Ten pointer GameObject \u2014 highlights the earned card slot.")]
    [SerializeField] private GameObject ten;
    [Tooltip("CardCollectionUI in the Cards tab. Used to resolve the earned card slot for Step Ten.")]
    [SerializeField] private CardCollectionUI cardCollectionUI;
    [Tooltip("Delay before re-showing UI_Tutorial/Three after returning from the first free chest.")]
    [SerializeField] private float cardsTutorialThreeDelay = 0.4f;
    [Tooltip("Delay before showing UI_Tutorial/Nine after PanelShop&Cards opens.")]
    [SerializeField] private float nineOpenDelay = 0.25f;
    [Tooltip("Delay before showing UI_Tutorial/Ten after Cards tab opens (allows BuildSlots to run).")]
    [SerializeField] private float tenOpenDelay = 0.25f;

    [Header("Steps 14–19 — Radar / Police Tutorial")]
    [Tooltip("World RadarSpawner used to deterministically force-spawn the first tutorial Radar after the 3rd free Common Chest is opened.")]
    [SerializeField] private RadarSpawner radarSpawner;
    [Tooltip("PoliceCatchTrigger reference used to deterministically start the tutorial police chase. Optional — falls back to PoliceCatchTrigger.Instance.")]
    [SerializeField] private PoliceCatchTrigger policeCatchTrigger;
    [Tooltip("PopularityBar GameObject in the TopBar revealed at Step 14 with the same animation as Premium reveal.")]
    [SerializeField] private GameObject popularityBarRoot;
    [Tooltip("UI_Tutorial/Eleven dialog GameObject — shown together with Twelve after the player taps the tutorial Radar.")]
    [SerializeField] private GameObject eleven;
    [Tooltip("UI_Tutorial/Twelve pointer GameObject — shown together with Eleven after the player taps the tutorial Radar.")]
    [SerializeField] private GameObject twelve;
    [Tooltip("UI_Tutorial/Thirteen dialog GameObject — shown the moment the tutorial police chase starts (OnChaseStarted).")]
    [SerializeField] private GameObject thirteen;
    [Tooltip("Delay before opening Eleven/Twelve after the player taps the tutorial Radar.")]
    [SerializeField] private float elevenTwelveOpenDelay = 0.4f;
    [Tooltip("Delay between dismissing Eleven/Twelve and force-starting the tutorial police chase.")]
    [SerializeField] private float policeTutorialStartDelay = 0.4f;

    [Header("Steps 20–22 — Garage Tutorial")]
    [Tooltip("TopBar/Btn_Garage GameObject — hidden until Step 19 completes, then animated in. MUST also be present in 'Top Bar Initially Hidden' so it stays hidden through earlier steps.")]
    [SerializeField] private GameObject btnGarageRoot;
    [Tooltip("TopBar/Btn_Garage Button — Step 21 listener target. The existing GarageSceneLoader.LoadGarageScene UnityEvent listener is left intact and runs alongside.")]
    [SerializeField] private Button btnGarage;
    [Tooltip("UI_Tutorial/Fourteen pointer GameObject — finger pointer aimed at TopBar/Btn_Garage during Step 20.")]
    [SerializeField] private GameObject fourteen;
    [Tooltip("Optional UI_Tutorial/FourteenInputBlocker GameObject — transparent full-screen Image that catches all raycasts during Step 20 EXCEPT Btn_Garage. Ensure its sibling-index is below Btn_Garage's parent so Btn_Garage stays clickable. If null, a CanvasGroup-based fallback is not provided — leave only Btn_Garage interactable manually.")]
    [SerializeField] private GameObject fourteenInputBlocker;
    [Tooltip("Delay before Btn_Garage reveal + Fourteen open after Step 19 (Thirteen dismissed) finishes.")]
    [SerializeField] private float garageTutorialOpenDelay = 0.4f;

    [Header("Post-Tutorial — First Blacklist Visit")]
    [Tooltip("UI_Tutorial/Seventeen dialog GameObject — shown the first time Panel_BlackList opens after the Garage tutorial completes (TutorialGate.BottomBarFullyUnlocked && !blacklistTutorialShown). Tap-anywhere to dismiss; uses Dim. Authored RectTransform position/scale are preserved.")]
    [SerializeField] private GameObject seventeen;

    [Header("Step 6/7 — Nitro/Chest Unlock Phase")]
    [Tooltip("Reference to the world NitroCoinSpawner used to force-spawn the first tutorial coin.")]
    [SerializeField] private NitroCoinSpawner nitroCoinSpawner;
    [Tooltip("Premium GameObject in the TopBar that should animate in when Clicker is pressed after Step 5.")]
    [SerializeField] private GameObject premiumRoot;
    [SerializeField] private float premiumIntroDuration = 0.35f;
    [SerializeField] private float premiumStartScale = 0.85f;
    [SerializeField] private Ease premiumIntroEase = Ease.OutBack;
    [Tooltip("Total Nitro Coin collections required during the unlock phase to unlock Chest spawning.")]
    [SerializeField] private int chestUnlockNitroThreshold = 3;
    [Tooltip("Optional small delay before the Six popup appears after the first tutorial coin is collected.")]
    [SerializeField] private float sixOpenDelay = 0.15f;

    [Header("Main UI Lock (Step 1)")]
    [SerializeField] private GameObject bottomBarRoot;
    [SerializeField] private GameObject[] topBarAlwaysActive;
    [SerializeField] private GameObject[] topBarInitiallyHidden;

    [Header("BottomBar Tabs (Post Step 2)")]
    [SerializeField] private Button bankButton;
    [SerializeField] private Button shopAndCardsButton;
    [SerializeField] private Button clickerButton;
    [SerializeField] private Button blacklistButton;
    [SerializeField] private Button rankingButton;

    [Header("Shop & Cards Lock")]
    [SerializeField] private Button cardsTabButton;
    [SerializeField] private int cardsTabUnlockStepIndex = 4;

    [Header("Panels")]
    [SerializeField] private RectTransform panelShopCards;

    [Header("Input")]
    [SerializeField] private bool useUnscaledTimeForDismissDelay = true;

    [Header("Step Transition Animation")]
    [SerializeField] private float dimFadeDuration = 0.2f;
    [SerializeField] private float panelInDuration = 0.3f;
    [SerializeField] private float panelOutDuration = 0.2f;
    [SerializeField] private float panelStartScale = 0.9f;
    [SerializeField] private float panelEndScale = 1f;
    [SerializeField] private Ease easeIn = Ease.OutBack;
    [SerializeField] private Ease easeOut = Ease.InBack;

    [Header("Step 2 Loop Animation")]
    [SerializeField] private float stepTwoPulseScalePercent = 0.04f;
    [SerializeField] private float stepTwoBounceDistance = 10f;
    [SerializeField] private float stepTwoLoopDuration = 0.7f;

    [Header("BottomBar Intro Animation")]
    [SerializeField] private float bottomBarIntroDuration = 0.35f;
    [SerializeField] private float bottomBarIntroYOffset = -120f;
    [SerializeField] private Ease bottomBarIntroEase = Ease.OutCubic;

    [Header("Step 3")]
    [SerializeField] private float stepThreeStartDelay = 0.18f;

    [Header("Step 4")]
    [SerializeField] private float stepFourStartDelay = 0.1f;

    [Header("Step 5")]
    [SerializeField] private float stepFiveStartDelay = 0.1f;

    private TutorialSaveData _saveData;
    private bool _isFirstStepOpen;
    private bool _isStepTwoActive;
    private bool _isStepTwoCompletionInProgress;
    private bool _isStepThreeActive;
    private bool _isStepThreeCompletionInProgress;
    private bool _isStepFourActive;
    private bool _isStepFourCompletionInProgress;
    private bool _isStepFourPendingStart;
    private bool _isStepFourStartDelayQueued;
    private bool _isStepFourPurchaseListenerRegistered;
    private bool _isStepFiveActive;
    private bool _isStepFiveCompletionInProgress;
    private bool _canDismissStepFive;
    private bool _isShopAndCardsListenerRegistered;
    private bool _isDismissInProgress;
    private bool _hadSuppressionBeforeTutorial;
    private bool _hadSuppressionBeforeStepFive;
    private bool _hasReleasedSuppressionAfterStepOne;
    private bool _isTransitionInProgress;
    private Sequence _activeTransition;
    private Sequence _stepTwoLoopSequence;
    private Sequence _stepThreeLoopSequence;
    private Sequence _stepFourLoopSequence;
    private Sequence _stepSevenLoopSequence;
    private CanvasGroup _dimCanvasGroup;
    private CanvasGroup _twoCanvasGroup;
    private CanvasGroup _threeCanvasGroup;
    private CanvasGroup _fourCanvasGroup;
    private CanvasGroup _fiveCanvasGroup;
    private CanvasGroup _sixCanvasGroup;
    private CanvasGroup _sevenCanvasGroup;
    private CanvasGroup _premiumCanvasGroup;
    private RectTransform _twoRectTransform;
    private RectTransform _threeRectTransform;
    private RectTransform _fourRectTransform;
    private RectTransform _fiveRectTransform;
    private RectTransform _sixRectTransform;
    private RectTransform _sevenRectTransform;
    private RectTransform _premiumRectTransform;
    private Vector3 _twoOriginalScale = Vector3.one;
    private Vector2 _twoOriginalAnchoredPosition;
    private bool _twoOriginalTransformCached;
    private Vector3 _threeOriginalScale = Vector3.one;
    private Vector2 _threeOriginalAnchoredPosition;
    private bool _threeOriginalTransformCached;
    private Vector3 _fourOriginalScale = Vector3.one;
    private Vector2 _fourOriginalAnchoredPosition;
    private bool _fourOriginalTransformCached;
    private Vector3 _fiveOriginalScale = Vector3.one;
    private Vector2 _fiveOriginalAnchoredPosition;
    private bool _fiveOriginalTransformCached;
    private Vector3 _sixOriginalScale = Vector3.one;
    private Vector2 _sixOriginalAnchoredPosition;
    private bool _sixOriginalTransformCached;
    private Vector3 _sevenOriginalScale = Vector3.one;
    private Vector2 _sevenOriginalAnchoredPosition;
    private bool _sevenOriginalTransformCached;
    private Vector3 _premiumOriginalScale = Vector3.one;
    private bool _premiumOriginalTransformCached;

    // ── Step 6/7 runtime state ──
    private bool _isClickerUnlockListenerRegistered;
    private bool _isStepSixActive;
    private bool _isStepSixCompletionInProgress;
    private bool _canDismissStepSix;
    private bool _isAwaitingFirstTutorialNitroCollect;

    // ── Step 7/8 (Chest tutorial) runtime state ──
    private bool _isStepSevenActive;
    private bool _isStepSevenCompletionInProgress;
    private bool _isAwaitingTutorialChestSpawn;
    private bool _isAwaitingTutorialChestCollect;

    // ── Steps 9–12 (Post-first-free-chest Cards tutorial) runtime state ──
    private bool _isStepThreeSecondActive;
    private bool _isStepThreeSecondCompletionInProgress;
    private bool _isStepThreeSecondStartDelayQueued;
    private bool _isShopCardsCardsTutorialListenerRegistered;
    private bool _isStepNineActive;
    private bool _isStepNineCompletionInProgress;
    private bool _isStepNineStartDelayQueued;
    private bool _isCardsTabCardsTutorialListenerRegistered;
    private bool _isStepTenActive;
    private bool _isStepTenCompletionInProgress;
    private bool _isStepTenStartDelayQueued;
    private bool _isCardDetailPopupListenerRegistered;
    private bool _isResumePipelineClickerListenerRegistered;
    private CanvasGroup _threeNewCanvasGroup;
    private CanvasGroup _nineCanvasGroup;
    private CanvasGroup _tenCanvasGroup;
    private RectTransform _threeNewRectTransform;
    private RectTransform _nineRectTransform;
    private RectTransform _tenRectTransform;
    private Vector3 _threeNewOriginalScale = Vector3.one;
    private Vector2 _threeNewOriginalAnchoredPosition;
    private bool _threeNewOriginalTransformCached;
    private Vector3 _nineOriginalScale = Vector3.one;
    private Vector2 _nineOriginalAnchoredPosition;
    private bool _nineOriginalTransformCached;
    private Vector3 _tenOriginalScale = Vector3.one;
    private Vector2 _tenOriginalAnchoredPosition;
    private bool _tenOriginalTransformCached;
    private Sequence _stepThreeNewLoopSequence;
    private Sequence _stepNineLoopSequence;
    private Sequence _stepTenLoopSequence;
    // Tracks every CardSlotUI button whose interactability we forcibly disabled
    // for Step 10 — keyed by reference so we can fully restore on completion.
    private System.Collections.Generic.List<UnityEngine.UI.Button> _stepTenSuppressedSlotButtons;

    // ── Steps 14–19 (Radar/Police tutorial) runtime state ──
    private bool _isAwaitingTutorialRadarSpawn;
    private bool _isStepSixteenActive;
    private bool _isStepSixteenCompletionInProgress;
    private bool _canDismissEleven;
    private bool _isAwaitingTutorialChase;
    private bool _isStepEighteenActive;
    private bool _isStepEighteenCompletionInProgress;
    private bool _canDismissThirteen;
    private CanvasGroup _elevenCanvasGroup;
    private CanvasGroup _twelveCanvasGroup;
    private CanvasGroup _thirteenCanvasGroup;
    private CanvasGroup _popularityBarCanvasGroup;
    private RectTransform _elevenRectTransform;
    private RectTransform _twelveRectTransform;
    private RectTransform _thirteenRectTransform;
    private RectTransform _popularityBarRectTransform;
    private Vector3 _elevenOriginalScale = Vector3.one;
    private Vector2 _elevenOriginalAnchoredPosition;
    private bool _elevenOriginalTransformCached;
    private Vector3 _twelveOriginalScale = Vector3.one;
    private Vector2 _twelveOriginalAnchoredPosition;
    private bool _twelveOriginalTransformCached;
    private Vector3 _thirteenOriginalScale = Vector3.one;
    private Vector2 _thirteenOriginalAnchoredPosition;
    private bool _thirteenOriginalTransformCached;
    private Vector3 _popularityBarOriginalScale = Vector3.one;
    private bool _popularityBarOriginalTransformCached;
    private Sequence _stepTwelveLoopSequence;

    // ── Steps 20–22 (Garage tutorial) runtime state ──
    private bool _isGarageButtonListenerRegistered;
    /// <summary>True between Thirteen-dismissed (CompleteStepEighteen) and the
    /// next OnChaseEnded+TopBar-restored. Gates EnterGarageTutorial so Fourteen
    /// can never appear while a chase is running.</summary>
    private bool _isAwaitingGarageTutorialAfterChase;
    private Coroutine _garageTutorialAfterChaseCoroutine;
    /// <summary>[DIAG TEMP] Cached Btn_Garage activeSelf for the runtime watcher in Update.</summary>
    private bool _btnGarageWatcherLastActiveSelf;
    private bool _btnGarageWatcherInitialized;
    private CanvasGroup _btnGarageCanvasGroup;
    private RectTransform _btnGarageRectTransform;
    private Vector3 _btnGarageOriginalScale = Vector3.one;
    private bool _btnGarageOriginalTransformCached;
    private CanvasGroup _fourteenCanvasGroup;
    private RectTransform _fourteenRectTransform;
    private Vector3 _fourteenOriginalScale = Vector3.one;
    private Vector2 _fourteenOriginalAnchoredPosition;
    private bool _fourteenOriginalTransformCached;
    private Sequence _stepFourteenLoopSequence;

    // ── Step Seventeen (post-tutorial first-Blacklist dialog) ──
    private CanvasGroup _seventeenCanvasGroup;
    private RectTransform _seventeenRectTransform;
    private Vector3 _seventeenOriginalScale = Vector3.one;
    private Vector2 _seventeenOriginalAnchoredPosition;
    private bool _seventeenOriginalTransformCached;
    private bool _isStepSeventeenActive;
    private bool _isStepSeventeenCompletionInProgress;
    private bool _canDismissSeventeen;

    // ── Active-car animator pause state (Step 6 freeze) ──
    private Animator _frozenCarAnimator;
    private float _frozenCarAnimatorPriorSpeed;

    private CanvasGroup _bottomBarCanvasGroup;
    private RectTransform _bottomBarRectTransform;
    private Vector2 _bottomBarOriginalAnchoredPosition;
    private bool _bottomBarOriginalAnchoredPositionCached;

    private void Awake()
    {
        // Default to component owner if not set.
        if (tutorialRoot == null)
            tutorialRoot = gameObject;

        // Scene-scoped singleton. Last-loaded wins is fine here because the
        // scene only ever instantiates one TutorialManager per scene; if a
        // duplicate slips in we still keep the most recent reference and
        // the previous instance's OnDestroy will clear Instance only if it
        // is still the current owner (see OnDestroy guard).
        Instance = this;

        _saveData = TutorialSaveData.Load();
        TutorialGate.SyncFromSave(_saveData);
        // [GarageDebug] Lifecycle: Awake start. Capture initial scene state of Btn_Garage.
        Debug.Log($"[Lifecycle][GarageDebug] TutorialManager Awake. btnGarageRoot={(btnGarageRoot != null ? btnGarageRoot.name : "<null>")} active={(btnGarageRoot != null && btnGarageRoot.activeSelf)} stepIdx={_saveData.currentStepIndex} " +
                  $"nitroUnlocked={_saveData.nitroUnlocked} radarUnlocked={_saveData.radarUnlocked} thirteenDismissed={_saveData.thirteenDismissed} garageTutorialQueued={_saveData.garageTutorialQueued} fifteenDismissed={_saveData.fifteenDismissed}");
        if (btnGarageRoot != null && btnGarageRoot.activeSelf && !ShouldBtnGarageBeVisible())
        {
            Debug.LogWarning("[STACK][GarageDebug] Btn_Garage was already ACTIVE in scene at TutorialManager.Awake (before any code touched it).\n" + System.Environment.StackTrace);
        }
        _dimCanvasGroup = GetOrAddCanvasGroup(dim);
        _twoCanvasGroup = GetOrAddCanvasGroup(two);
        _threeCanvasGroup = GetOrAddCanvasGroup(three);
        _fourCanvasGroup = GetOrAddCanvasGroup(four);
        _fiveCanvasGroup = GetOrAddCanvasGroup(five);
        _sixCanvasGroup = GetOrAddCanvasGroup(six);
        _twoRectTransform = two != null ? two.GetComponent<RectTransform>() : null;
        _threeRectTransform = three != null ? three.GetComponent<RectTransform>() : null;
        _fourRectTransform = four != null ? four.GetComponent<RectTransform>() : null;
        _fiveRectTransform = five != null ? five.GetComponent<RectTransform>() : null;
        _sixRectTransform = six != null ? six.GetComponent<RectTransform>() : null;
        if (premiumRoot != null)
        {
            _premiumCanvasGroup = GetOrAddCanvasGroup(premiumRoot);
            _premiumRectTransform = premiumRoot.GetComponent<RectTransform>();
            CachePremiumOriginalTransform();
        }
        CacheStepTwoOriginalTransform();
        CacheStepThreeOriginalTransform();
        CacheStepFourOriginalTransform();
        CacheStepFiveOriginalTransform();
        CacheBottomBarOriginalTransform();

        // Ensure current step panel supports fade animation.
        if (one != null)
            GetOrAddCanvasGroup(one);

        if (two != null)
        {
            _twoCanvasGroup.interactable = false;
            _twoCanvasGroup.blocksRaycasts = false;
        }

        if (three != null)
        {
            _threeCanvasGroup.interactable = false;
            _threeCanvasGroup.blocksRaycasts = false;

            Image threeImage = three.GetComponent<Image>();
            if (threeImage != null)
                threeImage.raycastTarget = false;
        }

        if (four != null)
        {
            _fourCanvasGroup.interactable = false;
            _fourCanvasGroup.blocksRaycasts = false;

            Image fourImage = four.GetComponent<Image>();
            if (fourImage != null)
                fourImage.raycastTarget = false;
        }

        if (five != null)
        {
            _fiveCanvasGroup.interactable = false;
            _fiveCanvasGroup.blocksRaycasts = false;
        }

        if (six != null)
        {
            _sixCanvasGroup.interactable = false;
            _sixCanvasGroup.blocksRaycasts = false;
            six.SetActive(false);
        }

        if (seven != null)
        {
            if (_sevenCanvasGroup != null)
            {
                _sevenCanvasGroup.interactable = false;
                _sevenCanvasGroup.blocksRaycasts = false;
            }
            seven.SetActive(false);
        }

        if (nine != null)
        {
            _nineCanvasGroup = GetOrAddCanvasGroup(nine);
            _nineRectTransform = nine.GetComponent<RectTransform>();
            CacheStepNineOriginalTransform();
            if (_nineCanvasGroup != null)
            {
                _nineCanvasGroup.interactable = false;
                _nineCanvasGroup.blocksRaycasts = false;
                _nineCanvasGroup.alpha = 0f;
            }
            Image nineImage = nine.GetComponent<Image>();
            if (nineImage != null)
                nineImage.raycastTarget = false;
            nine.SetActive(false);
        }

        if (threeNew != null)
        {
            _threeNewCanvasGroup = GetOrAddCanvasGroup(threeNew);
            _threeNewRectTransform = threeNew.GetComponent<RectTransform>();
            CacheStepThreeNewOriginalTransform();
            if (_threeNewCanvasGroup != null)
            {
                _threeNewCanvasGroup.interactable = false;
                _threeNewCanvasGroup.blocksRaycasts = false;
                _threeNewCanvasGroup.alpha = 0f;
            }
            Image threeNewImage = threeNew.GetComponent<Image>();
            if (threeNewImage != null)
                threeNewImage.raycastTarget = false;
            threeNew.SetActive(false);
        }

        if (ten != null)
        {
            _tenCanvasGroup = GetOrAddCanvasGroup(ten);
            _tenRectTransform = ten.GetComponent<RectTransform>();
            CacheStepTenOriginalTransform();
            if (_tenCanvasGroup != null)
            {
                _tenCanvasGroup.interactable = false;
                _tenCanvasGroup.blocksRaycasts = false;
                _tenCanvasGroup.alpha = 0f;
            }
            Image tenImage = ten.GetComponent<Image>();
            if (tenImage != null)
                tenImage.raycastTarget = false;
            ten.SetActive(false);
        }

        // Steps 14–19: Eleven / Twelve / Thirteen + PopularityBar reveal cache.
        if (eleven != null)
        {
            _elevenCanvasGroup = GetOrAddCanvasGroup(eleven);
            _elevenRectTransform = eleven.GetComponent<RectTransform>();
            CacheStepElevenOriginalTransform();
            if (_elevenCanvasGroup != null)
            {
                _elevenCanvasGroup.interactable = false;
                _elevenCanvasGroup.blocksRaycasts = false;
                _elevenCanvasGroup.alpha = 0f;
            }
            eleven.SetActive(false);
        }

        if (twelve != null)
        {
            _twelveCanvasGroup = GetOrAddCanvasGroup(twelve);
            _twelveRectTransform = twelve.GetComponent<RectTransform>();
            CacheStepTwelveOriginalTransform();
            if (_twelveCanvasGroup != null)
            {
                _twelveCanvasGroup.interactable = false;
                _twelveCanvasGroup.blocksRaycasts = false;
                _twelveCanvasGroup.alpha = 0f;
            }
            Image twelveImage = twelve.GetComponent<Image>();
            if (twelveImage != null)
                twelveImage.raycastTarget = false;
            twelve.SetActive(false);
        }

        if (thirteen != null)
        {
            _thirteenCanvasGroup = GetOrAddCanvasGroup(thirteen);
            _thirteenRectTransform = thirteen.GetComponent<RectTransform>();
            CacheStepThirteenOriginalTransform();
            if (_thirteenCanvasGroup != null)
            {
                _thirteenCanvasGroup.interactable = false;
                _thirteenCanvasGroup.blocksRaycasts = false;
                _thirteenCanvasGroup.alpha = 0f;
            }
            thirteen.SetActive(false);
        }

        if (popularityBarRoot != null)
        {
            _popularityBarCanvasGroup = GetOrAddCanvasGroup(popularityBarRoot);
            _popularityBarRectTransform = popularityBarRoot.GetComponent<RectTransform>();
            CachePopularityBarOriginalTransform();
        }

        // Steps 20–22: Btn_Garage + Fourteen reveal cache.
        if (btnGarageRoot != null)
        {
            _btnGarageCanvasGroup = GetOrAddCanvasGroup(btnGarageRoot);
            _btnGarageRectTransform = btnGarageRoot.GetComponent<RectTransform>();
            CacheBtnGarageOriginalTransform();
            // Defensive: ensure Btn_Garage starts hidden until the police tutorial
            // has fully completed (post-chase). Even if a designer left it active
            // in the scene, ApplyBtnGarageGateVisibility will pull it offline.
            bool wasActive = btnGarageRoot.activeSelf;
            btnGarageRoot.SetActive(false);
            Debug.Log($"[TutorialMgr][GarageDebug] Setting Btn_Garage INACTIVE (Awake forced-off). wasActive={wasActive}");
        }
        if (fourteen != null)
        {
            _fourteenCanvasGroup = GetOrAddCanvasGroup(fourteen);
            _fourteenRectTransform = fourteen.GetComponent<RectTransform>();
            CacheStepFourteenOriginalTransform();
            if (_fourteenCanvasGroup != null)
            {
                _fourteenCanvasGroup.interactable = false;
                _fourteenCanvasGroup.blocksRaycasts = false;
                _fourteenCanvasGroup.alpha = 0f;
            }
            Image fourteenImage = fourteen.GetComponent<Image>();
            if (fourteenImage != null)
                fourteenImage.raycastTarget = false;
            fourteen.SetActive(false);
        }
        if (fourteenInputBlocker != null)
            fourteenInputBlocker.SetActive(false);

        // Step Seventeen — first-Blacklist dialog. Cache transform + CanvasGroup
        // and force-hide so it cannot intercept raycasts behind ChestPopup or
        // other UI before being explicitly shown.
        if (seventeen != null)
        {
            _seventeenCanvasGroup = GetOrAddCanvasGroup(seventeen);
            _seventeenRectTransform = seventeen.GetComponent<RectTransform>();
            CacheStepSeventeenOriginalTransform();
            if (_seventeenCanvasGroup != null)
            {
                _seventeenCanvasGroup.interactable = false;
                _seventeenCanvasGroup.blocksRaycasts = false;
                _seventeenCanvasGroup.alpha = 0f;
            }
            seventeen.SetActive(false);
        }

        if (_dimCanvasGroup != null)
        {
            _dimCanvasGroup.interactable = false;
            _dimCanvasGroup.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        Debug.Log($"[Lifecycle][GarageDebug] TutorialManager OnEnable. btnGarageRoot active={(btnGarageRoot != null && btnGarageRoot.activeSelf)}");
        SaveSystem.OnGameLoaded += HandleGameLoaded;
        NitroCoin.OnTutorialCoinReachedCenter += HandleTutorialCoinReachedCenter;
        NitroCoin.OnWorldNitroCollected += HandleWorldNitroCollected;
        TutorialGate.OnGameplayFrozenChanged += HandleGameplayFrozenChanged;
        Chest.OnTutorialChestReachedCenter += HandleTutorialChestReachedCenter;
        Chest.OnTutorialChestCollected += HandleTutorialChestCollected;
        ChestShownUI.OnSlotTappedEvent += HandleChestSlotTappedForStepSeven;
        ChestInventoryManager.OnChestRemovedAfterOpen += HandleChestRemovedAfterOpen;
        CardDetailPopupController.OnShown += HandleCardDetailPopupShownForStepTen;
        _isCardDetailPopupListenerRegistered = true;
        Radar.OnTutorialRadarReachedCenter += HandleTutorialRadarReachedCenter;
        Radar.OnTutorialRadarTapped += HandleTutorialRadarTapped;
        PoliceCatchController.OnChaseStarted += HandleChaseStartedForTutorial;
        PoliceCatchController.OnChaseEnded += HandleChaseEndedForGarageTutorial;
    }

    private void OnDisable()
    {
        SaveSystem.OnGameLoaded -= HandleGameLoaded;
        NitroCoin.OnTutorialCoinReachedCenter -= HandleTutorialCoinReachedCenter;
        NitroCoin.OnWorldNitroCollected -= HandleWorldNitroCollected;
        TutorialGate.OnGameplayFrozenChanged -= HandleGameplayFrozenChanged;
        Chest.OnTutorialChestReachedCenter -= HandleTutorialChestReachedCenter;
        Chest.OnTutorialChestCollected -= HandleTutorialChestCollected;
        ChestShownUI.OnSlotTappedEvent -= HandleChestSlotTappedForStepSeven;
        ChestInventoryManager.OnChestRemovedAfterOpen -= HandleChestRemovedAfterOpen;
        if (_isCardDetailPopupListenerRegistered)
        {
            CardDetailPopupController.OnShown -= HandleCardDetailPopupShownForStepTen;
            _isCardDetailPopupListenerRegistered = false;
        }
        Radar.OnTutorialRadarReachedCenter -= HandleTutorialRadarReachedCenter;
        Radar.OnTutorialRadarTapped -= HandleTutorialRadarTapped;
        PoliceCatchController.OnChaseStarted -= HandleChaseStartedForTutorial;
        PoliceCatchController.OnChaseEnded -= HandleChaseEndedForGarageTutorial;
        UnregisterShopCardsCardsTutorialListener();
        UnregisterCardsTabCardsTutorialListener();
        UnregisterResumePipelineClickerListener();
        StopStepThreeNewLoopAnimation(restoreTransform: true);
        StopStepNineLoopAnimation(restoreTransform: true);
        StopStepTenLoopAnimation(restoreTransform: true);
        RestoreStepTenSuppressedSlotButtons();
        KillActiveTransition();
        StopStepTwoLoopAnimation(restoreTransform: true);
        StopStepThreeLoopAnimation(restoreTransform: true);
        StopStepFourLoopAnimation(restoreTransform: true);
        StopStepSevenLoopAnimation(restoreTransform: true);
        UnregisterShopAndCardsClickListener();
        UnregisterStepFourPurchaseListener();
        UnregisterClickerUnlockListener();
        // Defensive: never leave the gameplay frozen if the tutorial host disables.
        if (TutorialGate.GameplayFrozen)
            TutorialGate.SetGameplayFrozen(false);
        // Defensive: if we held a paused animator, restore its speed even if the
        // freeze flag was force-cleared without raising the change event.
        RestoreFrozenCarAnimatorIfAny();

        // Never keep suppression stuck when tutorial host disables unexpectedly.
        if (!_hasReleasedSuppressionAfterStepOne)
            ApplySuppressionRestoreImmediate();

        if (_isStepFiveActive || _isStepFiveCompletionInProgress)
            UIFlowState.IsContentPanelOpen = _hadSuppressionBeforeStepFive;
    }

    private void Start()
    {
        Debug.Log($"[Lifecycle][GarageDebug] TutorialManager Start. btnGarageRoot active={(btnGarageRoot != null && btnGarageRoot.activeSelf)}");
        // Also evaluate once at startup in case this script activates after load.
        ApplyTutorialState();
    }

    /// <summary>[DIAG TEMP] Evaluates whether Btn_Garage SHOULD be visible right now.</summary>
    private bool ShouldBtnGarageBeVisible()
    {
        if (_saveData == null) return false;
        bool chaseActive = PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive;
        bool policeDone = _saveData.thirteenDismissed && !_isAwaitingGarageTutorialAfterChase && !chaseActive;
        bool garageUnlocked = _saveData.garageTutorialQueued || _saveData.fifteenDismissed;
        return garageUnlocked && policeDone;
    }

    private void Update()
    {
        // Step Seventeen — post-tutorial first-Blacklist dialog tap-anywhere.
        // Handled BEFORE all other step branches so it can dismiss while normal
        // gameplay is otherwise unblocked. Independent from the main tutorial
        // step state machine — it only runs after fifteenDismissed=true.
        if (_isStepSeventeenActive)
        {
            if (_isStepSeventeenCompletionInProgress || !_canDismissSeventeen)
                return;

            if (WasAnyPointerPressedThisFrame())
                CompleteStepSeventeen();

            return;
        }

        // [WATCH][GarageDebug] Runtime watcher — fires only on activeSelf edges.
        if (btnGarageRoot != null)
        {
            bool nowActive = btnGarageRoot.activeSelf;
            if (!_btnGarageWatcherInitialized)
            {
                _btnGarageWatcherLastActiveSelf = nowActive;
                _btnGarageWatcherInitialized = true;
            }
            else if (nowActive != _btnGarageWatcherLastActiveSelf)
            {
                bool shouldBeVisible = ShouldBtnGarageBeVisible();
                int stepIdx = _saveData != null ? _saveData.currentStepIndex : -1;
                bool policeDone = _saveData != null && _saveData.thirteenDismissed && !_isAwaitingGarageTutorialAfterChase && (PoliceCatchController.Instance == null || !PoliceCatchController.Instance.IsChaseActive);
                bool garageUnlocked = _saveData != null && (_saveData.garageTutorialQueued || _saveData.fifteenDismissed);
                bool radarDone = _saveData != null && _saveData.radarUnlocked;
                if (nowActive)
                {
                    if (!shouldBeVisible)
                    {
                        Debug.LogWarning($"[WATCH][GarageDebug] Btn_Garage became ACTIVE at runtime UNEXPECTEDLY! stepIdx={stepIdx} policeDone={policeDone} radarDone={radarDone} garageUnlocked={garageUnlocked} frame={Time.frameCount} time={Time.unscaledTime:F2}");
                        Debug.Log("[STACK][GarageDebug] Btn_Garage activated\n" + System.Environment.StackTrace);
                    }
                    else
                    {
                        Debug.Log($"[WATCH][GarageDebug] Btn_Garage became ACTIVE (expected). stepIdx={stepIdx} policeDone={policeDone} radarDone={radarDone} garageUnlocked={garageUnlocked} frame={Time.frameCount} time={Time.unscaledTime:F2}");
                    }
                }
                else
                {
                    Debug.Log($"[WATCH][GarageDebug] Btn_Garage became INACTIVE. stepIdx={stepIdx} policeDone={policeDone} radarDone={radarDone} garageUnlocked={garageUnlocked} frame={Time.frameCount} time={Time.unscaledTime:F2}");
                }
                _btnGarageWatcherLastActiveSelf = nowActive;
            }
        }

        if (_isStepFourPendingStart)
            TryStartStepFourPending();

        // Cards-tutorial: poll for PanelShop&Cards open after Three(second) completes
        // so we can deterministically launch Step Nine.
        if (_isStepNineStartDelayQueued)
            TryStartStepNinePending();

        // Tutorial chest spawn poll: when chestUnlocked just flipped, deterministically
        // spawn a single tutorial Common Chest as soon as the world is in a safe state.
        if (_isAwaitingTutorialChestSpawn)
            TryForceSpawnTutorialChest();

        // Steps 14–19 polling.
        if (_isAwaitingTutorialRadarSpawn)
            TryForceSpawnTutorialRadar();

        if (_isAwaitingTutorialChase)
            TryForceTutorialChase();

        if (_isStepSixteenActive)
        {
            if (_isStepSixteenCompletionInProgress || _isTransitionInProgress || !_canDismissEleven)
                return;

            if (WasAnyPointerPressedThisFrame())
                CompleteStepSixteen();

            return;
        }

        if (_isStepEighteenActive)
        {
            if (_isStepEighteenCompletionInProgress || _isTransitionInProgress || !_canDismissThirteen)
                return;

            if (WasAnyPointerPressedThisFrame())
                CompleteStepEighteen();

            return;
        }

        if (_isStepSevenActive)
        {
            // Seven completes via slot-tap event, not via global pointer-press.
            return;
        }

        if (_isStepSixActive)
        {
            if (_isStepSixCompletionInProgress || _isTransitionInProgress || !_canDismissStepSix)
                return;

            if (WasAnyPointerPressedThisFrame())
                CompleteStepSix();

            return;
        }

        if (_isStepFiveActive)
        {
            if (_isStepFiveCompletionInProgress || _isTransitionInProgress || !_canDismissStepFive)
                return;

            if (WasAnyPointerPressedThisFrame())
                CompleteStepFive();

            return;
        }

        if (!_isFirstStepOpen || _isDismissInProgress || _isTransitionInProgress)
        {
            if (_isStepTwoActive && !_isStepTwoCompletionInProgress && !_isTransitionInProgress)
                TryCompleteStepTwoByAffordability();

            return;
        }

        if (WasAnyPointerPressedThisFrame())
        {
            DismissFirstStep();
        }
    }

    private void HandleGameLoaded()
    {
        _saveData = TutorialSaveData.Load();
        TutorialGate.SyncFromSave(_saveData);
        // [Save][GarageDebug] Snapshot of garage-relevant flags after load.
        Debug.Log($"[Save][GarageDebug] After Load: stepIndex={_saveData.currentStepIndex} " +
                  $"nitroUnlocked={_saveData.nitroUnlocked} radarUnlocked={_saveData.radarUnlocked} " +
                  $"policeTutorialChaseStarted={_saveData.policeTutorialChaseStarted} thirteenDismissed={_saveData.thirteenDismissed} " +
                  $"garageTutorialQueued={_saveData.garageTutorialQueued} garageButtonClicked={_saveData.garageButtonClicked} fifteenDismissed={_saveData.fifteenDismissed} " +
                  $"btnGarageRoot.activeSelf={(btnGarageRoot != null && btnGarageRoot.activeSelf)}");
        // [DEBUG][CardsTut] Trace what state we just loaded.
        Debug.Log($"[TutorialMgr][CardsTut] HandleGameLoaded: stepIdx={_saveData.currentStepIndex} " +
                  $"chestUnlocked={_saveData.chestUnlocked} tutorialChestCollected={_saveData.tutorialChestCollected} " +
                  $"chestSlotTutorialShown={_saveData.chestSlotTutorialShown} freeOpenedCount={_saveData.tutorialFreeChestOpenedCount} " +
                  $"cardsTutStarted={_saveData.cardsTutorialStarted} shopCardsAfterFirst={_saveData.shopCardsClickedAfterFirstChest} " +
                  $"cardsTabClicked={_saveData.cardsTabClicked} tenCompleted={_saveData.tenCompleted} " +
                  $"clickerPressed={_saveData.cardsSegmentClickerPressed}");
        Debug.Log($"[TutorialMgr][RadarTut] HandleGameLoaded radar/police flags: stepIdx={_saveData.currentStepIndex} " +
                  $"freeOpenedCount={_saveData.tutorialFreeChestOpenedCount} radarTutorialQueued={_saveData.radarTutorialQueued} " +
                  $"firstTutorialRadarSpawned={_saveData.firstTutorialRadarSpawned} radarTutorialTapped={_saveData.radarTutorialTapped} " +
                  $"elevenTwelveDismissed={_saveData.elevenTwelveDismissed} policeTutorialChaseStarted={_saveData.policeTutorialChaseStarted} " +
                  $"thirteenDismissed={_saveData.thirteenDismissed} radarUnlocked={_saveData.radarUnlocked} policeLocked={_saveData.policeLocked} " +
                  $"radarSpawnerAssigned={(radarSpawner != null)} popularityBarRootAssigned={(popularityBarRoot != null)}");
        ApplyTutorialState();
    }

    private void ApplyTutorialState()
    {
        _isStepFourPendingStart = false;
        _isStepFourStartDelayQueued = false;
        _canDismissStepFive = false;
        UnregisterStepFourPurchaseListener();

        ApplyStepOneUILock();
        SetDimHiddenImmediate();
        ApplyCardsTabLockState();
        ApplyPremiumPersistedVisibility();

        if (_saveData.IsFifthStepCompleted)
        {
            SetStepOneVisibleImmediate(false);
            SetStepTwoVisibleImmediate(false);
            SetStepThreeVisibleImmediate(false);
            SetStepFourVisibleImmediate(false);
            SetStepFiveVisibleImmediate(false);
            ApplySuppressionRestoreImmediate();

            _isFirstStepOpen = false;
            _isStepTwoActive = false;
            _isStepTwoCompletionInProgress = false;
            _isStepThreeActive = false;
            _isStepThreeCompletionInProgress = false;
            _isStepFourActive = false;
            _isStepFourCompletionInProgress = false;
            _isStepFiveActive = false;
            _isStepFiveCompletionInProgress = false;
            _isDismissInProgress = false;
            _isTransitionInProgress = false;

            ShowBottomBarCompletedStateImmediate();
            ApplyBottomBarInteractabilityLock();
            ApplyCardsTabLockState();
            ApplyStepSixOrSevenStateImmediate();
            return;
        }

        if (_saveData.IsFourthStepCompleted)
        {
            SetStepOneVisibleImmediate(false);
            SetStepTwoVisibleImmediate(false);
            SetStepThreeVisibleImmediate(false);
            SetStepFourVisibleImmediate(false);
            ApplySuppressionRestoreImmediate();

            _isFirstStepOpen = false;
            _isStepTwoActive = false;
            _isStepTwoCompletionInProgress = false;
            _isStepThreeActive = false;
            _isStepThreeCompletionInProgress = false;
            _isStepFourActive = false;
            _isStepFourCompletionInProgress = false;
            _isStepFiveActive = false;
            _isStepFiveCompletionInProgress = false;
            _isDismissInProgress = false;
            _isTransitionInProgress = false;

            ShowBottomBarCompletedStateImmediate();
            ApplyBottomBarInteractabilityLock();
            ApplyCardsTabLockState();
            StartStepFiveAfterDelay();
            return;
        }

        if (_saveData.IsThirdStepCompleted)
        {
            SetStepOneVisibleImmediate(false);
            SetStepTwoVisibleImmediate(false);
            SetStepThreeVisibleImmediate(false);
            SetStepFourVisibleImmediate(false);
            SetStepFiveVisibleImmediate(false);
            ApplySuppressionRestoreImmediate();

            _isFirstStepOpen = false;
            _isStepTwoActive = false;
            _isStepTwoCompletionInProgress = false;
            _isStepThreeActive = false;
            _isStepThreeCompletionInProgress = false;
            _isStepFourActive = false;
            _isStepFourCompletionInProgress = false;
            _isStepFiveActive = false;
            _isStepFiveCompletionInProgress = false;
            _isDismissInProgress = false;
            _isTransitionInProgress = false;

            ShowBottomBarCompletedStateImmediate();
            _isStepFourPendingStart = true;
            return;
        }

        if (_saveData.IsSecondStepCompleted)
        {
            SetStepOneVisibleImmediate(false);
            SetStepTwoVisibleImmediate(false);
            SetStepThreeVisibleImmediate(false);
            SetStepFourVisibleImmediate(false);
            SetStepFiveVisibleImmediate(false);
            ApplySuppressionRestoreImmediate();

            _isFirstStepOpen = false;
            _isStepTwoActive = false;
            _isStepTwoCompletionInProgress = false;
            _isStepThreeActive = false;
            _isStepThreeCompletionInProgress = false;
            _isStepFourActive = false;
            _isStepFourCompletionInProgress = false;
            _isStepFiveActive = false;
            _isStepFiveCompletionInProgress = false;
            _isDismissInProgress = false;
            _isTransitionInProgress = false;

            ShowBottomBarCompletedStateImmediate();
            StartStepThreeAfterDelay();
            return;
        }

        if (_saveData.IsFirstStepCompleted)
        {
            SetStepOneVisibleImmediate(false);
            SetStepThreeVisibleImmediate(false);
            SetStepFourVisibleImmediate(false);
            SetStepFiveVisibleImmediate(false);
            ApplySuppressionRestoreImmediate();

            _isFirstStepOpen = false;
            _isDismissInProgress = false;
            _isTransitionInProgress = false;

            EnterStepTwo();
            return;
        }

        if (_isFirstStepOpen || _isTransitionInProgress)
            return;

        // First-time/fresh-save path.
        SetStepTwoVisibleImmediate(false);
        SetStepThreeVisibleImmediate(false);
        SetStepFourVisibleImmediate(false);
        SetStepFiveVisibleImmediate(false);
        SetStepOneVisibleAnimated(true);

        _hadSuppressionBeforeTutorial = UIFlowState.IsContentPanelOpen;
        UIFlowState.IsContentPanelOpen = true;
        _hasReleasedSuppressionAfterStepOne = false;

        _isFirstStepOpen = true;
        _isStepTwoActive = false;
        _isStepTwoCompletionInProgress = false;
        _isStepThreeActive = false;
        _isStepThreeCompletionInProgress = false;
        _isStepFourActive = false;
        _isStepFourCompletionInProgress = false;
        _isStepFiveActive = false;
        _isStepFiveCompletionInProgress = false;
        _isDismissInProgress = false;
    }

    private void ApplyStepOneUILock()
    {
        SetBottomBarHiddenImmediate();

        bool btnGarageActiveBefore = btnGarageRoot != null && btnGarageRoot.activeSelf;
        Debug.Log($"[TutorialMgr][GarageDebug] ApplyStepOneUILock ENTER. StepIndex={(_saveData != null ? _saveData.currentStepIndex : -1)}, policeDone={(_saveData != null && _saveData.thirteenDismissed)}, radarDone={(_saveData != null && _saveData.radarUnlocked)}, garageUnlocked={(_saveData != null && (_saveData.garageTutorialQueued || _saveData.fifteenDismissed))}");
        Debug.Log($"[TutorialMgr][GarageDebug] ApplyStepOneUILock Btn_Garage activeBefore={btnGarageActiveBefore}");

        if (topBarAlwaysActive != null)
        {
            for (int i = 0; i < topBarAlwaysActive.Length; i++)
            {
                if (topBarAlwaysActive[i] != null)
                    topBarAlwaysActive[i].SetActive(true);
            }
        }

        if (topBarInitiallyHidden != null)
        {
            bool nitroUnlocked = _saveData != null && _saveData.nitroUnlocked;
            bool radarUnlocked = _saveData != null && _saveData.radarUnlocked;
            // Btn_Garage must remain hidden until the tutorial police chase has
            // fully completed (Thirteen dismissed AND chase ended AND topbar
            // restored). garageTutorialQueued alone (set the moment Thirteen is
            // dismissed) is NOT a sufficient unlock condition — the chase is
            // still running at that moment.
            bool garageRevealed = _saveData != null
                                  && _saveData.garageTutorialQueued
                                  && !_isAwaitingGarageTutorialAfterChase
                                  && (PoliceCatchController.Instance == null || !PoliceCatchController.Instance.IsChaseActive);
            for (int i = 0; i < topBarInitiallyHidden.Length; i++)
            {
                if (topBarInitiallyHidden[i] == null)
                    continue;

                // Once Nitro is unlocked the Premium reveal must persist across reloads,
                // so do not force-deactivate Premium here.
                if (nitroUnlocked && premiumRoot != null && topBarInitiallyHidden[i] == premiumRoot)
                    continue;

                // Once the radar tutorial has revealed the PopularityBar, keep it visible.
                if (radarUnlocked && popularityBarRoot != null && topBarInitiallyHidden[i] == popularityBarRoot)
                    continue;

                // Only skip the deactivate for Btn_Garage when the police tutorial
                // is fully complete — otherwise force-hide it here.
                if (garageRevealed && btnGarageRoot != null && topBarInitiallyHidden[i] == btnGarageRoot)
                    continue;

                topBarInitiallyHidden[i].SetActive(false);
            }
        }

        // Final authoritative gate — always force Btn_Garage to the correct state.
        ApplyBtnGarageGateVisibility("ApplyStepOneUILock");
        Debug.Log($"[TutorialMgr][GarageDebug] ApplyStepOneUILock EXIT Btn_Garage activeAfter={(btnGarageRoot != null && btnGarageRoot.activeSelf)}");
    }

    private void SetStepOneVisibleAnimated(bool visible)
    {
        KillActiveTransition();

        if (!visible)
        {
            SetStepOneVisibleImmediate(false);
            return;
        }

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowDim());
        sequence.Join(ShowStepVisual(one));
        sequence.OnComplete(() =>
        {
            _isTransitionInProgress = false;
            _activeTransition = null;
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private void SetStepOneVisibleImmediate(bool visible)
    {
        KillActiveTransition();

        if (!visible)
        {
            HideStepVisualImmediate(one);
            return;
        }

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        ShowStepVisualImmediate(one);
    }

    private void EnterStepTwo()
    {
        if (_isStepTwoActive || _isStepTwoCompletionInProgress)
            return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        _isFirstStepOpen = false;
        _isDismissInProgress = false;
        _isStepTwoActive = true;

        ApplySuppressionRestoreImmediate();

        HideStepVisualImmediate(one);
        SetDimHiddenImmediate();
        SetStepTwoVisibleAnimated(true);
    }

    private void SetStepTwoVisibleAnimated(bool visible)
    {
        StopStepTwoLoopAnimation(restoreTransform: true);

        if (!visible)
        {
            SetStepTwoVisibleImmediate(false);
            return;
        }

        CacheStepTwoOriginalTransform();

        if (two == null)
            return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        if (_twoCanvasGroup == null)
            _twoCanvasGroup = GetOrAddCanvasGroup(two);

        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideDim());
        sequence.Join(ShowStepTwoVisual());
        sequence.OnComplete(() =>
        {
            _isTransitionInProgress = false;
            _activeTransition = null;
            StartStepTwoLoopAnimation();
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private void SetStepTwoVisibleImmediate(bool visible)
    {
        StopStepTwoLoopAnimation(restoreTransform: true);

        if (two == null)
            return;

        if (_twoCanvasGroup == null)
            _twoCanvasGroup = GetOrAddCanvasGroup(two);

        CacheStepTwoOriginalTransform();

        if (visible)
        {
            two.SetActive(true);
            RestoreStepTwoOriginalTransform();

            if (_twoCanvasGroup != null)
                _twoCanvasGroup.alpha = 1f;
        }
        else
        {
            if (_twoCanvasGroup != null)
                _twoCanvasGroup.alpha = 0f;

            two.SetActive(false);
        }

        if (_twoCanvasGroup != null)
        {
            _twoCanvasGroup.interactable = false;
            _twoCanvasGroup.blocksRaycasts = false;
        }
    }

    private void TryCompleteStepTwoByAffordability()
    {
        if (!_isStepTwoActive || _isStepTwoCompletionInProgress)
            return;

        if (CurrencyManager.Instance == null || BuildingManager.Instance == null)
            return;

        double currentMoney = CurrencyManager.Instance.money;
        double currentStreetDealsCost = BuildingManager.Instance.GetCurrentCost(BuildingType.StreetDeals);

        if (currentStreetDealsCost <= 0)
            return;

        if (currentMoney < currentStreetDealsCost)
            return;

        CompleteStepTwo();
    }

    private void CompleteStepTwo()
    {
        if (_isStepTwoCompletionInProgress)
            return;

        _isStepTwoCompletionInProgress = true;

        KillActiveTransition();
        StopStepTwoLoopAnimation(restoreTransform: true);
        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepTwoVisual());
        sequence.Append(ShowBottomBarAnimated());
        sequence.OnComplete(() =>
        {
            SetStepTwoVisibleImmediate(false);

            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 2);
            _saveData.Save();
            ApplyCardsTabLockState();

            _isStepTwoActive = false;
            _isStepTwoCompletionInProgress = false;
            _isTransitionInProgress = false;
            _activeTransition = null;

            StartStepThreeAfterDelay();
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private Tween ShowBottomBarAnimated()
    {
        if (bottomBarRoot == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        CacheBottomBarOriginalTransform();

        if (_bottomBarCanvasGroup == null)
            _bottomBarCanvasGroup = GetOrAddCanvasGroup(bottomBarRoot);

        bottomBarRoot.SetActive(true);
        ApplyBottomBarInteractabilityLock();

        if (_bottomBarCanvasGroup != null)
        {
            _bottomBarCanvasGroup.alpha = 0f;
            _bottomBarCanvasGroup.interactable = true;
            _bottomBarCanvasGroup.blocksRaycasts = true;
        }

        if (_bottomBarRectTransform != null)
            _bottomBarRectTransform.anchoredPosition = _bottomBarOriginalAnchoredPosition + Vector2.up * bottomBarIntroYOffset;

        Sequence intro = DOTween.Sequence();
        intro.SetUpdate(true);

        if (_bottomBarCanvasGroup != null)
            intro.Join(_bottomBarCanvasGroup.DOFade(1f, bottomBarIntroDuration));

        if (_bottomBarRectTransform != null)
        {
            intro.Join(_bottomBarRectTransform
                .DOAnchorPos(_bottomBarOriginalAnchoredPosition, bottomBarIntroDuration)
                .SetEase(bottomBarIntroEase));
        }

        return intro;
    }

    private void ShowBottomBarCompletedStateImmediate()
    {
        if (bottomBarRoot == null)
            return;

        CacheBottomBarOriginalTransform();

        if (_bottomBarCanvasGroup == null)
            _bottomBarCanvasGroup = GetOrAddCanvasGroup(bottomBarRoot);

        bottomBarRoot.SetActive(true);
        ApplyBottomBarInteractabilityLock();

        if (_bottomBarRectTransform != null)
            _bottomBarRectTransform.anchoredPosition = _bottomBarOriginalAnchoredPosition;

        if (_bottomBarCanvasGroup != null)
        {
            _bottomBarCanvasGroup.alpha = 1f;
            _bottomBarCanvasGroup.interactable = true;
            _bottomBarCanvasGroup.blocksRaycasts = true;
        }
    }

    private void SetBottomBarHiddenImmediate()
    {
        if (bottomBarRoot == null)
            return;

        if (_bottomBarCanvasGroup == null)
            _bottomBarCanvasGroup = GetOrAddCanvasGroup(bottomBarRoot);

        if (_bottomBarCanvasGroup != null)
        {
            _bottomBarCanvasGroup.alpha = 0f;
            _bottomBarCanvasGroup.interactable = false;
            _bottomBarCanvasGroup.blocksRaycasts = false;
        }

        bottomBarRoot.SetActive(false);
    }

    // Tracks last-applied state per BottomBar gated button so we can emit
    // transition-only logs (locked → unlocked / unlocked → locked) instead of
    // spamming every call. Nullable: null = no state ever applied yet.
    private bool? _bankBottomBarLastInteractable;
    private bool? _blacklistBottomBarLastInteractable;
    private bool? _rankingBottomBarLastInteractable;

    private void ApplyBottomBarInteractabilityLock()
    {
        // During the post-first-free-chest Cards tutorial sub-steps the player
        // must follow the guided pointer: Three-second points only at Shop&Cards
        // (Clicker locked), Nine and Ten happen inside the Shop&Cards panel
        // (Clicker still locked). Clicker re-opens for Step 12 (resume pipeline)
        // after Step 10/Ten completes.
        bool clickerLockedForCardsSegment =
            _isStepThreeSecondActive || _isStepNineActive || _isStepTenActive;

        SetButtonInteractable(shopAndCardsButton, true);
        SetButtonInteractable(clickerButton, !clickerLockedForCardsSegment);

        // Bank/Blacklist/Ranking are permanently unlocked once the FIRST Garage
        // tutorial finishes (UI_Tutorial/Fifteen dismissed in NewGarage). Until
        // then they are forced locked so the guided steps cannot be bypassed.
        // Ranking has an additional gameplay gate owned by RankingService — even
        // after the Garage tutorial it stays locked until the player is ranked.
        bool garageDone = _saveData != null && _saveData.fifteenDismissed;
        bool rankingSystemUnlocked = RankingService.Instance != null && RankingService.Instance.IsRankingUnlocked;

        bool bankInteractable = garageDone;
        bool blacklistInteractable = garageDone;
        bool rankingInteractable = garageDone && rankingSystemUnlocked;

        ApplyAndLogBottomBarButton(bankButton, bankInteractable, ref _bankBottomBarLastInteractable, "Bank");
        ApplyAndLogBottomBarButton(blacklistButton, blacklistInteractable, ref _blacklistBottomBarLastInteractable, "Blacklist");
        ApplyAndLogRankingButton(rankingButton, rankingInteractable, garageDone, rankingSystemUnlocked);
    }

    private static void ApplyAndLogBottomBarButton(Button button, bool interactable, ref bool? lastApplied, string label)
    {
        SetButtonInteractable(button, interactable);
        if (!lastApplied.HasValue || lastApplied.Value != interactable)
        {
            Debug.Log($"[TutorialMgr][BottomBarUnlock] {label} {(interactable ? "unlocked" : "locked")}");
            lastApplied = interactable;
        }
    }

    private void ApplyAndLogRankingButton(Button button, bool interactable, bool garageDone, bool rankingSystemUnlocked)
    {
        SetButtonInteractable(button, interactable);
        if (!_rankingBottomBarLastInteractable.HasValue || _rankingBottomBarLastInteractable.Value != interactable)
        {
            if (interactable)
                Debug.Log($"[TutorialMgr][BottomBarUnlock] Ranking unlocked: garageDone=true rankingSystemUnlocked=true");
            else
                Debug.Log($"[TutorialMgr][BottomBarUnlock] Ranking waiting: garageDone={garageDone}, rankingSystemUnlocked={rankingSystemUnlocked}");
            _rankingBottomBarLastInteractable = interactable;
        }
    }

    private void ApplyCardsTabLockState()
    {
        if (cardsTabButton == null || _saveData == null)
            return;

        // Cards tab is unlocked the moment Step 9 (Three-second) completes (i.e.
        // shopCardsClickedAfterFirstChest=true, which means Nine is about to/has
        // appeared). It then stays unlocked permanently. Before that point it is
        // locked — including throughout the chest tutorial — so the player
        // cannot bypass the guided Nine pointer.
        cardsTabButton.interactable = _saveData.shopCardsClickedAfterFirstChest;
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void CacheStepTwoOriginalTransform()
    {
        if (_twoRectTransform == null)
            _twoRectTransform = two != null ? two.GetComponent<RectTransform>() : null;

        if (_twoRectTransform == null)
            return;

        if (_twoOriginalTransformCached)
            return;

        _twoOriginalScale = _twoRectTransform.localScale;
        _twoOriginalAnchoredPosition = _twoRectTransform.anchoredPosition;
        _twoOriginalTransformCached = true;
    }

    private void RestoreStepTwoOriginalTransform()
    {
        if (_twoRectTransform == null)
            return;

        if (!_twoOriginalTransformCached)
            CacheStepTwoOriginalTransform();

        _twoRectTransform.localScale = _twoOriginalScale;
        _twoRectTransform.anchoredPosition = _twoOriginalAnchoredPosition;
    }

    private void CacheBottomBarOriginalTransform()
    {
        if (_bottomBarRectTransform == null)
            _bottomBarRectTransform = bottomBarRoot != null ? bottomBarRoot.GetComponent<RectTransform>() : null;

        if (_bottomBarRectTransform == null)
            return;

        if (_bottomBarOriginalAnchoredPositionCached)
            return;

        _bottomBarOriginalAnchoredPosition = _bottomBarRectTransform.anchoredPosition;
        _bottomBarOriginalAnchoredPositionCached = true;
    }

    private void StartStepTwoLoopAnimation()
    {
        StopStepTwoLoopAnimation(restoreTransform: true);

        if (two == null || !two.activeInHierarchy)
            return;

        if (_twoRectTransform == null)
            _twoRectTransform = two.GetComponent<RectTransform>();

        if (_twoRectTransform == null)
            return;

        CacheStepTwoOriginalTransform();
        RestoreStepTwoOriginalTransform();

        Vector3 pulseScale = _twoOriginalScale * (1f + stepTwoPulseScalePercent);
        Vector2 bouncePos = _twoOriginalAnchoredPosition + Vector2.up * stepTwoBounceDistance;

        _stepTwoLoopSequence = DOTween.Sequence();
        _stepTwoLoopSequence.SetUpdate(true);
        _stepTwoLoopSequence.Join(_twoRectTransform.DOScale(pulseScale, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepTwoLoopSequence.Join(_twoRectTransform.DOAnchorPos(bouncePos, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepTwoLoopSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopStepTwoLoopAnimation(bool restoreTransform)
    {
        if (_stepTwoLoopSequence != null)
        {
            _stepTwoLoopSequence.Kill();
            _stepTwoLoopSequence = null;
        }

        if (restoreTransform)
            RestoreStepTwoOriginalTransform();
    }

    private void StartStepThreeAfterDelay()
    {
        if (_saveData == null)
            return;

        if (_saveData.IsThirdStepCompleted)
        {
            SetStepThreeVisibleImmediate(false);
            UnregisterShopAndCardsClickListener();
            return;
        }

        if (_isStepThreeActive || _isStepThreeCompletionInProgress)
            return;

        DOVirtual.DelayedCall(stepThreeStartDelay, StartStepThree, true).SetUpdate(true);
    }

    private void StartStepThree()
    {
        if (_saveData == null || _saveData.IsThirdStepCompleted)
            return;

        if (_isStepThreeActive || _isStepThreeCompletionInProgress)
            return;

        _isStepThreeActive = true;

        ApplyBottomBarInteractabilityLock();
        ApplyCardsTabLockState();
        RegisterShopAndCardsClickListener();
        SetStepThreeVisibleAnimated(true);
    }

    private void SetStepThreeVisibleAnimated(bool visible)
    {
        StopStepThreeLoopAnimation(restoreTransform: true);

        if (!visible)
        {
            SetStepThreeVisibleImmediate(false);
            return;
        }

        CacheStepThreeOriginalTransform();

        if (three == null)
            return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        if (_threeCanvasGroup == null)
            _threeCanvasGroup = GetOrAddCanvasGroup(three);

        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowStepThreeVisual());
        sequence.OnComplete(() =>
        {
            _isTransitionInProgress = false;
            _activeTransition = null;
            StartStepThreeLoopAnimation();
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private void SetStepThreeVisibleImmediate(bool visible)
    {
        StopStepThreeLoopAnimation(restoreTransform: true);

        if (three == null)
            return;

        if (_threeCanvasGroup == null)
            _threeCanvasGroup = GetOrAddCanvasGroup(three);

        CacheStepThreeOriginalTransform();

        if (visible)
        {
            three.SetActive(true);
            RestoreStepThreeOriginalTransform();

            if (_threeCanvasGroup != null)
                _threeCanvasGroup.alpha = 1f;
        }
        else
        {
            if (_threeCanvasGroup != null)
                _threeCanvasGroup.alpha = 0f;

            three.SetActive(false);
        }

        if (_threeCanvasGroup != null)
        {
            _threeCanvasGroup.interactable = false;
            _threeCanvasGroup.blocksRaycasts = false;
        }

        Image threeImage = three.GetComponent<Image>();
        if (threeImage != null)
            threeImage.raycastTarget = false;
    }

    private void RegisterShopAndCardsClickListener()
    {
        if (_isShopAndCardsListenerRegistered || shopAndCardsButton == null)
            return;

        shopAndCardsButton.onClick.AddListener(HandleShopAndCardsClickedForStepThree);
        _isShopAndCardsListenerRegistered = true;
    }

    private void UnregisterShopAndCardsClickListener()
    {
        if (!_isShopAndCardsListenerRegistered || shopAndCardsButton == null)
            return;

        shopAndCardsButton.onClick.RemoveListener(HandleShopAndCardsClickedForStepThree);
        _isShopAndCardsListenerRegistered = false;
    }

    private void HandleShopAndCardsClickedForStepThree()
    {
        if (!_isStepThreeActive || _isStepThreeCompletionInProgress)
            return;

        CompleteStepThree();
    }

    private void CompleteStepThree()
    {
        if (_isStepThreeCompletionInProgress)
            return;

        _isStepThreeCompletionInProgress = true;
        _isStepThreeActive = false;

        UnregisterShopAndCardsClickListener();
        KillActiveTransition();
        StopStepThreeLoopAnimation(restoreTransform: true);
        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepThreeVisual());
        sequence.OnComplete(() =>
        {
            SetStepThreeVisibleImmediate(false);

            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 3);
            _saveData.Save();

            ApplyBottomBarInteractabilityLock();
            ApplyCardsTabLockState();

            _isStepFourPendingStart = true;
            _isStepFourStartDelayQueued = false;

            _isStepThreeCompletionInProgress = false;
            _isTransitionInProgress = false;
            _activeTransition = null;
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private void TryStartStepFourPending()
    {
        if (!_isStepFourPendingStart || _isTransitionInProgress)
            return;

        if (_saveData == null || _saveData.IsFourthStepCompleted)
        {
            _isStepFourPendingStart = false;
            _isStepFourStartDelayQueued = false;
            return;
        }

        if (!IsShopCardsPanelOpen())
            return;

        StartStepFourAfterDelay();
    }

    private bool IsShopCardsPanelOpen()
    {
        if (panelShopCards == null)
            return true;

        if (!panelShopCards.gameObject.activeInHierarchy)
            return false;

        CanvasGroup panelCg = panelShopCards.GetComponent<CanvasGroup>();
        if (panelCg == null)
            return true;

        return panelCg.alpha > 0.01f;
    }

    private void StartStepFourAfterDelay()
    {
        if (_saveData == null)
            return;

        if (_saveData.IsFourthStepCompleted)
        {
            SetStepFourVisibleImmediate(false);
            UnregisterStepFourPurchaseListener();
            _isStepFourPendingStart = false;
            _isStepFourStartDelayQueued = false;
            return;
        }

        if (_isStepFourActive || _isStepFourCompletionInProgress || _isStepFourStartDelayQueued)
            return;

        _isStepFourStartDelayQueued = true;

        DOVirtual.DelayedCall(stepFourStartDelay, () =>
        {
            _isStepFourStartDelayQueued = false;

            if (!isActiveAndEnabled)
                return;

            StartStepFour();
        }, true).SetUpdate(true);
    }

    private void StartStepFour()
    {
        if (_saveData == null || _saveData.IsFourthStepCompleted)
            return;

        if (_isStepFourActive || _isStepFourCompletionInProgress)
            return;

        _isStepFourPendingStart = false;
        _isStepFourActive = true;

        ApplyBottomBarInteractabilityLock();
        ApplyCardsTabLockState();
        RegisterStepFourPurchaseListener();
        SetStepFourVisibleAnimated(true);
    }

    private void RegisterStepFourPurchaseListener()
    {
        if (_isStepFourPurchaseListenerRegistered)
            return;

        if (BuildingManager.Instance == null)
            return;

        BuildingManager.Instance.OnBuildingPurchased += HandleBuildingPurchasedForStepFour;
        _isStepFourPurchaseListenerRegistered = true;
    }

    private void UnregisterStepFourPurchaseListener()
    {
        if (!_isStepFourPurchaseListenerRegistered)
            return;

        if (BuildingManager.Instance != null)
            BuildingManager.Instance.OnBuildingPurchased -= HandleBuildingPurchasedForStepFour;

        _isStepFourPurchaseListenerRegistered = false;
    }

    private void HandleBuildingPurchasedForStepFour(BuildingType buildingType, int newCount)
    {
        if (!_isStepFourActive || _isStepFourCompletionInProgress)
            return;

        if (buildingType != BuildingType.StreetDeals)
            return;

        CompleteStepFour();
    }

    private void CompleteStepFour()
    {
        if (_isStepFourCompletionInProgress)
            return;

        _isStepFourCompletionInProgress = true;
        _isStepFourActive = false;

        UnregisterStepFourPurchaseListener();
        KillActiveTransition();
        StopStepFourLoopAnimation(restoreTransform: true);
        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepFourVisual());
        sequence.OnComplete(() =>
        {
            SetStepFourVisibleImmediate(false);

            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 4);
            _saveData.Save();

            ApplyBottomBarInteractabilityLock();
            ApplyCardsTabLockState();

            _isStepFourCompletionInProgress = false;
            _isTransitionInProgress = false;
            _activeTransition = null;

            StartStepFiveAfterDelay();
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private void StartStepFiveAfterDelay()
    {
        if (_saveData == null)
            return;

        if (_saveData.IsFifthStepCompleted)
        {
            SetStepFiveVisibleImmediate(false);
            SetDimHiddenImmediate();
            return;
        }

        if (_isStepFiveActive || _isStepFiveCompletionInProgress)
            return;

        DOVirtual.DelayedCall(stepFiveStartDelay, () =>
        {
            if (!isActiveAndEnabled)
                return;

            StartStepFive();
        }, true).SetUpdate(true);
    }

    private void StartStepFive()
    {
        if (_saveData == null || _saveData.IsFifthStepCompleted)
            return;

        if (_isStepFiveActive || _isStepFiveCompletionInProgress)
            return;

        _isStepFiveActive = true;
        _canDismissStepFive = false;
        _hadSuppressionBeforeStepFive = UIFlowState.IsContentPanelOpen;
        UIFlowState.IsContentPanelOpen = true;

        ApplyBottomBarInteractabilityLock();
        ApplyCardsTabLockState();
        SetStepFiveVisibleAnimated(true);
    }

    private void CompleteStepFive()
    {
        if (_isStepFiveCompletionInProgress)
            return;

        _isStepFiveCompletionInProgress = true;
        _isStepFiveActive = false;
        _canDismissStepFive = false;

        KillActiveTransition();
        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepFiveVisual());
        sequence.Join(HideDim());
        sequence.OnComplete(() =>
        {
            SetStepFiveVisibleImmediate(false);

            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 5);
            _saveData.Save();

            UIFlowState.IsContentPanelOpen = _hadSuppressionBeforeStepFive;

            ApplyBottomBarInteractabilityLock();
            ApplyCardsTabLockState();

            // Step 5 just completed → register the Clicker listener that will
            // unlock Nitro on the next Clicker tap (start of Step 6).
            if (_saveData != null && !_saveData.nitroUnlocked)
                RegisterClickerUnlockListener();

            _isStepFiveCompletionInProgress = false;
            _isTransitionInProgress = false;
            _activeTransition = null;
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private IEnumerator ArmStepFiveDismissOnNextFrame()
    {
        yield return null;
        _canDismissStepFive = true;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  STEP 6 / 7 — NITRO + CHEST UNLOCK PHASE
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called from <see cref="ApplyTutorialState"/> after Step 5 is completed.
    /// Restores correct runtime state for the unlock phase based on the persisted
    /// save data: registers the Clicker listener if Nitro is still locked, or
    /// re-applies the Premium-visible / chest-unlocked state on reload.
    /// </summary>
    private void ApplyStepSixOrSevenStateImmediate()
    {
        // Always keep TutorialGate mirrored from save (defensive).
        TutorialGate.SyncFromSave(_saveData);
        // Transient flags clear on every (re)apply.
        TutorialGate.SetGameplayFrozen(false);
        TutorialGate.SetTutorialNitroActive(false);
        // Defensive: if the previous session left an animator paused (e.g. domain
        // reload mid-freeze), restore it even if the freeze edge already fired.
        RestoreFrozenCarAnimatorIfAny();

        SetStepSixVisibleImmediate(false);

        // Reveal Premium without animation if it has already been unlocked previously.
        if (_saveData != null && _saveData.nitroUnlocked)
            ShowPremiumImmediate();

        // Step 5 done but Nitro still locked → wait for Clicker tap.
        if (_saveData != null && !_saveData.nitroUnlocked)
        {
            RegisterClickerUnlockListener();
            return;
        }

        // Step 5 done, Nitro already unlocked, but the very first tutorial coin was
        // either not yet spawned, or was lost (e.g. user reloaded mid-step-6 before
        // tapping it). Re-spawn it deterministically so the flow can continue.
        if (_saveData != null && _saveData.nitroUnlocked && !_saveData.firstTutorialNitroCollected)
        {
            _isAwaitingFirstTutorialNitroCollect = true;
            ForceSpawnFirstTutorialCoinIfNeeded();
        }

        // ── Step 7/8 (chest tutorial) state restoration on reload ──
        SetStepSevenVisibleImmediate(false);

        // [HIGH-PRIORITY EXPLICIT ROUTE] If the persistent "post-first-chest"
        // pending flag is set, route DIRECTLY to the Cards tutorial segment.
        // This flag is set in ChestInventoryManager.BumpTutorialFreeChestOpenedCount
        // the moment the first free chest is opened, and cleared on Shop&Cards
        // click — making it the single authoritative trigger for Three_New.
        if (_saveData != null
            && _saveData.postFirstChestShopTutorialPending
            && !_saveData.shopCardsClickedAfterFirstChest)
        {
            Debug.Log("[TutorialMgr][CardsTut] EXPLICIT route → postFirstChestShopTutorialPending=TRUE → ApplyCardsTutorialSegmentStateImmediate");
            ApplyCardsTutorialSegmentStateImmediate();
            return;
        }

        if (_saveData != null && _saveData.chestUnlocked && !_saveData.tutorialChestCollected)
        {
            // Player crossed the 3-Nitro threshold but didn't tap the chest yet.
            // Re-queue the force-spawn so it pops as soon as the world is safe.
            _isAwaitingTutorialChestSpawn = true;
        }
        else if (_saveData != null && _saveData.tutorialChestCollected && !_saveData.chestSlotTutorialShown)
        {
            // Player tapped the chest pre-reload but the Seven popup never shipped
            // (e.g. crash before slot pointer appeared). Re-show Seven if there is
            // at least one chest slot to point at.
            if (ChestInventoryManager.Instance != null && ChestInventoryManager.Instance.ChestCount > 0)
                DOVirtual.DelayedCall(sevenOpenDelay, StartStepSeven, true).SetUpdate(true);
        }
        else if (_saveData != null
                 && _saveData.chestSlotTutorialShown
                 && _saveData.tutorialFreeChestOpenedCount >= 1
                 && !_saveData.cardsSegmentClickerPressed)
        {
            // Step 8 done, first free chest opened, but the Cards tutorial segment
            // (Steps 9–12) has not yet been completed by pressing Clicker.
            // Suspend the chest spawn pipeline and route to the highest-priority
            // unfinished sub-step.
            Debug.Log("[TutorialMgr][CardsTut] Routing → ApplyCardsTutorialSegmentStateImmediate");
            ApplyCardsTutorialSegmentStateImmediate();
        }
        else
        {
            Debug.Log($"[TutorialMgr][CardsTut] No Cards-segment route. chestSlotTutorialShown={_saveData?.chestSlotTutorialShown} " +
                      $"freeOpenedCount={_saveData?.tutorialFreeChestOpenedCount} clickerPressed={_saveData?.cardsSegmentClickerPressed} " +
                      $"chestUnlocked={_saveData?.chestUnlocked} tutorialChestCollected={_saveData?.tutorialChestCollected}");
        }

        // Steps 14–19: Radar/Police tutorial segment routing on reload.
        if (_saveData != null
            && _saveData.radarTutorialQueued
            && !_saveData.thirteenDismissed)
        {
            // First entry from ChestOpenScene return: radarTutorialQueued just
            // got persisted by ChestInventoryManager.BumpTutorialFreeChestOpenedCount
            // but radarUnlocked has not yet been set. Use the animated reveal path.
            // Subsequent reloads (radarUnlocked already true) use the immediate
            // path so PopularityBar pops in without re-animating.
            if (!_saveData.radarUnlocked)
            {
                Debug.Log("[TutorialMgr][RadarTut] Routing → FIRST-ENTRY animated path (EnterRadarPoliceTutorial). radarTutorialQueued=TRUE, radarUnlocked=FALSE.");
                EnterRadarPoliceTutorial();
            }
            else
            {
                Debug.Log("[TutorialMgr][RadarTut] Routing → RELOAD immediate path (ApplyRadarPoliceTutorialStateImmediate). radarTutorialQueued=TRUE, radarUnlocked=TRUE.");
                ApplyRadarPoliceTutorialStateImmediate();
            }
        }
        else if (_saveData != null && _saveData.thirteenDismissed)
        {
            // Permanent post-segment state: PopularityBar visible, police unlocked.
            Debug.Log("[TutorialMgr][RadarTut] Routing → POST-SEGMENT (thirteenDismissed=TRUE) → ShowPopularityBarImmediate only.");
            ShowPopularityBarImmediate();
        }
        else
        {
            Debug.Log($"[TutorialMgr][RadarTut] Routing → SKIPPED. radarTutorialQueued={_saveData?.radarTutorialQueued} thirteenDismissed={_saveData?.thirteenDismissed} freeOpenedCount={_saveData?.tutorialFreeChestOpenedCount}/{TutorialGate.TutorialFreeChestQuota}.");
        }

        // Steps 20–22: Garage tutorial segment routing on reload.
        if (_saveData != null
            && _saveData.garageTutorialQueued
            && !_saveData.fifteenDismissed)
        {
            // Authoritative gate first — if chase is somehow still active or
            // we're awaiting OnChaseEnded, force-hide before doing anything else.
            ApplyBtnGarageGateVisibility("HandleGameLoaded.GarageRouting");

            bool chaseActive = PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive;
            if (chaseActive || _isAwaitingGarageTutorialAfterChase)
            {
                Debug.Log($"[TutorialMgr][GarageTut] Routing → garageTutorialQueued=TRUE but chaseActive={chaseActive} awaiting={_isAwaitingGarageTutorialAfterChase} → deferring ApplyGarageTutorialStateImmediate.");
            }
            else
            {
                Debug.Log($"[TutorialMgr][GarageTut] Routing → garageTutorialQueued=TRUE fifteenDismissed=FALSE garageButtonClicked={_saveData.garageButtonClicked} → ApplyGarageTutorialStateImmediate.");
                ApplyGarageTutorialStateImmediate();
            }
        }
        else if (_saveData != null && _saveData.fifteenDismissed)
        {
            // Segment complete — Btn_Garage permanently visible.
            Debug.Log("[TutorialMgr][GarageTut] Routing → POST-SEGMENT (fifteenDismissed=TRUE) → ShowBtnGarageImmediate only.");
            ShowBtnGarageImmediate();
        }
        else
        {
            // Not yet queued — force hidden.
            ApplyBtnGarageGateVisibility("HandleGameLoaded.NotQueued");
        }
    }

    private void RegisterClickerUnlockListener()
    {
        if (_isClickerUnlockListenerRegistered || clickerButton == null)
            return;

        clickerButton.onClick.AddListener(HandleClickerPressedForStepSix);
        _isClickerUnlockListenerRegistered = true;
    }

    private void UnregisterClickerUnlockListener()
    {
        if (!_isClickerUnlockListenerRegistered || clickerButton == null)
            return;

        clickerButton.onClick.RemoveListener(HandleClickerPressedForStepSix);
        _isClickerUnlockListenerRegistered = false;
    }

    private void HandleClickerPressedForStepSix()
    {
        if (_saveData == null) return;
        // Guard against duplicate unlock if listener fires twice in a frame.
        if (_saveData.nitroUnlocked) { UnregisterClickerUnlockListener(); return; }
        if (!_saveData.IsFifthStepCompleted) return;

        // Persist the unlock and mirror to gate before any spawn happens.
        _saveData.nitroUnlocked = true;
        _saveData.Save();
        TutorialGate.SetNitroUnlocked(true);

        // One-shot: never re-register on subsequent Clicker taps.
        UnregisterClickerUnlockListener();

        // Reveal Premium with smooth animation.
        ShowPremiumAnimated();

        // Force-spawn the first deterministic tutorial Nitro Coin.
        _isAwaitingFirstTutorialNitroCollect = true;
        ForceSpawnFirstTutorialCoinIfNeeded();
    }

    private void ForceSpawnFirstTutorialCoinIfNeeded()
    {
        if (nitroCoinSpawner == null)
        {
            Debug.LogWarning("[TutorialManager] nitroCoinSpawner is not assigned — cannot force-spawn the first tutorial coin.");
            return;
        }

        NitroCoin coin = nitroCoinSpawner.ForceSpawnTutorialCoin();
        if (coin != null && _saveData != null)
        {
            _saveData.firstTutorialNitroSpawned = true;
            _saveData.Save();
        }
    }

    private void HandleTutorialCoinReachedCenter(NitroCoin coin)
    {
        // Only the first tutorial coin should freeze gameplay; later coins are normal.
        if (coin == null || !coin.IsTutorial) return;
        if (_saveData != null && _saveData.firstTutorialNitroCollected) return;

        TutorialGate.SetGameplayFrozen(true);
    }

    private void HandleWorldNitroCollected(int rewardAmount)
    {
        if (_saveData == null) return;
        if (!TutorialGate.NitroUnlocked) return;

        // First tutorial coin tap → release freeze, mark step 6, show Six popup.
        if (_isAwaitingFirstTutorialNitroCollect && !_saveData.firstTutorialNitroCollected)
        {
            _isAwaitingFirstTutorialNitroCollect = false;
            _saveData.firstTutorialNitroCollected = true;
            // Tutorial coin is the first counted nitro toward the chest threshold.
            BumpTutorialNitroCount();
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 6);
            _saveData.Save();

            // Keep gameplay frozen during Six popup so the dismiss tap cannot
            // accidentally trigger a Car tap underneath the UI.
            TutorialGate.SetGameplayFrozen(true);

            DOVirtual.DelayedCall(sixOpenDelay, StartStepSix, true).SetUpdate(true);
            return;
        }

        // Subsequent collected nitros count toward chest unlock.
        if (!_saveData.chestUnlocked)
        {
            BumpTutorialNitroCount();
            if (_saveData.tutorialNitroCount >= chestUnlockNitroThreshold)
            {
                _saveData.chestUnlocked = true;
                _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 7);
                TutorialGate.SetChestUnlocked(true);
                // Now that chest spawning is unlocked, queue the deterministic
                // tutorial Common Chest. The Update poll will spawn it as soon
                // as the world is in a safe state (inventory not full, no
                // active tutorial chest already in flight).
                if (!_saveData.tutorialChestCollected)
                    _isAwaitingTutorialChestSpawn = true;
            }
            _saveData.Save();
        }
    }

    private void BumpTutorialNitroCount()
    {
        if (_saveData == null) return;
        // Cap at threshold so future taps after chest unlock don't grow unbounded.
        if (_saveData.tutorialNitroCount < chestUnlockNitroThreshold)
            _saveData.tutorialNitroCount++;
    }

    // ── Active-car animator pause (freeze edge) ──

    /// <summary>
    /// Edge handler bound to <see cref="TutorialGate.OnGameplayFrozenChanged"/>.
    /// Pauses/resumes the active car's <see cref="Animator"/> on freeze transitions.
    /// The active car is always the GameObject tagged "Car" (set by
    /// <c>MainSceneCarController</c>) so no per-prefab wiring is needed and the
    /// behavior remains correct regardless of which car the player has selected.
    /// </summary>
    private void HandleGameplayFrozenChanged(bool frozen)
    {
        if (frozen)
            PauseActiveCarAnimator();
        else
            RestoreFrozenCarAnimatorIfAny();
    }

    private void PauseActiveCarAnimator()
    {
        // If a previous pause was never restored (defensive), restore it first
        // so we don't leak the original speed value.
        if (_frozenCarAnimator != null)
            RestoreFrozenCarAnimatorIfAny();

        GameObject carGO = GameObject.FindGameObjectWithTag("Car");
        if (carGO == null) return;

        Animator animator = carGO.GetComponentInChildren<Animator>(includeInactive: false);
        if (animator == null) return;

        _frozenCarAnimator = animator;
        _frozenCarAnimatorPriorSpeed = animator.speed;
        animator.speed = 0f;
    }

    private void RestoreFrozenCarAnimatorIfAny()
    {
        if (_frozenCarAnimator == null) return;

        // The Animator may have been destroyed mid-freeze (e.g. scene reload).
        // The implicit Unity-null check on UnityEngine.Object handles that safely.
        if (_frozenCarAnimator)
            _frozenCarAnimator.speed = _frozenCarAnimatorPriorSpeed;

        _frozenCarAnimator = null;
        _frozenCarAnimatorPriorSpeed = 1f;
    }

    // ── Step Six popup ──

    private void StartStepSix()
    {
        if (_saveData == null) return;
        if (_isStepSixActive || _isStepSixCompletionInProgress) return;

        _isStepSixActive = true;
        _canDismissStepSix = false;

        SetStepSixVisibleAnimated(true);
    }

    private void CompleteStepSix()
    {
        if (_isStepSixCompletionInProgress) return;

        _isStepSixCompletionInProgress = true;
        _isStepSixActive = false;
        _canDismissStepSix = false;

        KillActiveTransition();
        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepSixVisual());
        sequence.Join(HideDim());
        sequence.OnComplete(() =>
        {
            SetStepSixVisibleImmediate(false);
            // Resume gameplay only after the Six dismiss animation completes.
            TutorialGate.SetGameplayFrozen(false);

            _isStepSixCompletionInProgress = false;
            _isTransitionInProgress = false;
            _activeTransition = null;
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private void SetStepSixVisibleAnimated(bool visible)
    {
        if (!visible)
        {
            SetStepSixVisibleImmediate(false);
            return;
        }

        CacheStepSixOriginalTransform();

        if (six == null) return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        if (_sixCanvasGroup == null)
            _sixCanvasGroup = GetOrAddCanvasGroup(six);

        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowDim(blockInput: true));
        sequence.Join(ShowStepSixVisual());
        sequence.OnComplete(() =>
        {
            _isTransitionInProgress = false;
            _activeTransition = null;
            StartCoroutine(ArmStepSixDismissOnNextFrame());
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private IEnumerator ArmStepSixDismissOnNextFrame()
    {
        yield return null;
        _canDismissStepSix = true;
    }

    private void SetStepSixVisibleImmediate(bool visible)
    {
        if (six == null) return;

        if (_sixCanvasGroup == null)
            _sixCanvasGroup = GetOrAddCanvasGroup(six);

        CacheStepSixOriginalTransform();

        if (visible)
        {
            six.SetActive(true);
            RestoreStepSixOriginalTransform();
            if (_sixCanvasGroup != null)
            {
                _sixCanvasGroup.alpha = 1f;
                _sixCanvasGroup.interactable = true;
                _sixCanvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            if (_sixCanvasGroup != null)
            {
                _sixCanvasGroup.alpha = 0f;
                _sixCanvasGroup.interactable = false;
                _sixCanvasGroup.blocksRaycasts = false;
            }
            six.SetActive(false);
        }
    }

    private Tween ShowStepSixVisual()
    {
        if (six == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_sixCanvasGroup == null)
            _sixCanvasGroup = GetOrAddCanvasGroup(six);

        CacheStepSixOriginalTransform();
        six.SetActive(true);

        if (_sixCanvasGroup != null)
        {
            _sixCanvasGroup.alpha = 0f;
            _sixCanvasGroup.interactable = true;
            _sixCanvasGroup.blocksRaycasts = true;
        }

        if (_sixRectTransform != null)
            _sixRectTransform.localScale = _sixOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);

        if (_sixRectTransform != null)
            show.Join(_sixRectTransform.DOScale(_sixOriginalScale, panelInDuration).SetEase(easeIn));

        if (_sixCanvasGroup != null)
            show.Join(_sixCanvasGroup.DOFade(1f, panelInDuration));

        return show;
    }

    private Tween HideStepSixVisual()
    {
        if (six == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_sixCanvasGroup == null)
            _sixCanvasGroup = GetOrAddCanvasGroup(six);

        if (_sixRectTransform == null)
            _sixRectTransform = six.GetComponent<RectTransform>();

        CacheStepSixOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);

        if (_sixRectTransform != null)
            hide.Join(_sixRectTransform.DOScale(_sixOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));

        if (_sixCanvasGroup != null)
            hide.Join(_sixCanvasGroup.DOFade(0f, panelOutDuration));

        hide.OnComplete(() =>
        {
            RestoreStepSixOriginalTransform();
            six.SetActive(false);
        });

        return hide;
    }

    private void CacheStepSixOriginalTransform()
    {
        if (_sixRectTransform == null)
            _sixRectTransform = six != null ? six.GetComponent<RectTransform>() : null;

        if (_sixRectTransform == null) return;
        if (_sixOriginalTransformCached) return;

        _sixOriginalScale = _sixRectTransform.localScale;
        _sixOriginalAnchoredPosition = _sixRectTransform.anchoredPosition;
        _sixOriginalTransformCached = true;
    }

    private void RestoreStepSixOriginalTransform()
    {
        if (_sixRectTransform == null) return;
        if (!_sixOriginalTransformCached) CacheStepSixOriginalTransform();

        _sixRectTransform.localScale = _sixOriginalScale;
        _sixRectTransform.anchoredPosition = _sixOriginalAnchoredPosition;
    }

    // ── Premium reveal animation ──

    private void CachePremiumOriginalTransform()
    {
        if (_premiumRectTransform == null) return;
        if (_premiumOriginalTransformCached) return;

        _premiumOriginalScale = _premiumRectTransform.localScale;
        _premiumOriginalTransformCached = true;
    }

    private void ApplyPremiumPersistedVisibility()
    {
        if (premiumRoot == null) return;
        if (_saveData == null) return;

        if (_saveData.nitroUnlocked)
            ShowPremiumImmediate();
    }

    private void ShowPremiumImmediate()
    {
        if (premiumRoot == null) return;
        CachePremiumOriginalTransform();

        premiumRoot.SetActive(true);
        if (_premiumRectTransform != null)
            _premiumRectTransform.localScale = _premiumOriginalScale;
        if (_premiumCanvasGroup != null)
        {
            _premiumCanvasGroup.alpha = 1f;
            _premiumCanvasGroup.interactable = true;
            _premiumCanvasGroup.blocksRaycasts = true;
        }
    }

    private void ShowPremiumAnimated()
    {
        if (premiumRoot == null) return;
        CachePremiumOriginalTransform();

        premiumRoot.SetActive(true);

        if (_premiumCanvasGroup != null)
        {
            _premiumCanvasGroup.alpha = 0f;
            _premiumCanvasGroup.interactable = false;
            _premiumCanvasGroup.blocksRaycasts = false;
        }

        if (_premiumRectTransform != null)
            _premiumRectTransform.localScale = _premiumOriginalScale * premiumStartScale;

        Sequence intro = DOTween.Sequence();
        intro.SetUpdate(true);

        if (_premiumRectTransform != null)
            intro.Join(_premiumRectTransform.DOScale(_premiumOriginalScale, premiumIntroDuration).SetEase(premiumIntroEase));

        if (_premiumCanvasGroup != null)
            intro.Join(_premiumCanvasGroup.DOFade(1f, premiumIntroDuration));

        intro.OnComplete(() =>
        {
            if (_premiumCanvasGroup != null)
            {
                _premiumCanvasGroup.interactable = true;
                _premiumCanvasGroup.blocksRaycasts = true;
            }
        });
    }

    private void SetStepFourVisibleAnimated(bool visible)
    {
        StopStepFourLoopAnimation(restoreTransform: true);

        if (!visible)
        {
            SetStepFourVisibleImmediate(false);
            return;
        }

        CacheStepFourOriginalTransform();

        if (four == null)
            return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        if (_fourCanvasGroup == null)
            _fourCanvasGroup = GetOrAddCanvasGroup(four);

        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowStepFourVisual());
        sequence.OnComplete(() =>
        {
            _isTransitionInProgress = false;
            _activeTransition = null;
            StartStepFourLoopAnimation();
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private void SetStepFourVisibleImmediate(bool visible)
    {
        StopStepFourLoopAnimation(restoreTransform: true);

        if (four == null)
            return;

        if (_fourCanvasGroup == null)
            _fourCanvasGroup = GetOrAddCanvasGroup(four);

        CacheStepFourOriginalTransform();

        if (visible)
        {
            four.SetActive(true);
            RestoreStepFourOriginalTransform();

            if (_fourCanvasGroup != null)
                _fourCanvasGroup.alpha = 1f;
        }
        else
        {
            if (_fourCanvasGroup != null)
                _fourCanvasGroup.alpha = 0f;

            four.SetActive(false);
        }

        if (_fourCanvasGroup != null)
        {
            _fourCanvasGroup.interactable = false;
            _fourCanvasGroup.blocksRaycasts = false;
        }

        Image fourImage = four.GetComponent<Image>();
        if (fourImage != null)
            fourImage.raycastTarget = false;
    }

    private void CacheStepFourOriginalTransform()
    {
        if (_fourRectTransform == null)
            _fourRectTransform = four != null ? four.GetComponent<RectTransform>() : null;

        if (_fourRectTransform == null)
            return;

        if (_fourOriginalTransformCached)
            return;

        _fourOriginalScale = _fourRectTransform.localScale;
        _fourOriginalAnchoredPosition = _fourRectTransform.anchoredPosition;
        _fourOriginalTransformCached = true;
    }

    private void RestoreStepFourOriginalTransform()
    {
        if (_fourRectTransform == null)
            return;

        if (!_fourOriginalTransformCached)
            CacheStepFourOriginalTransform();

        _fourRectTransform.localScale = _fourOriginalScale;
        _fourRectTransform.anchoredPosition = _fourOriginalAnchoredPosition;
    }

    private void StartStepFourLoopAnimation()
    {
        StopStepFourLoopAnimation(restoreTransform: true);

        if (four == null || !four.activeInHierarchy)
            return;

        if (_fourRectTransform == null)
            _fourRectTransform = four.GetComponent<RectTransform>();

        if (_fourRectTransform == null)
            return;

        CacheStepFourOriginalTransform();
        RestoreStepFourOriginalTransform();

        Vector3 pulseScale = _fourOriginalScale * (1f + stepTwoPulseScalePercent);
        Vector2 bouncePos = _fourOriginalAnchoredPosition + Vector2.up * stepTwoBounceDistance;

        _stepFourLoopSequence = DOTween.Sequence();
        _stepFourLoopSequence.SetUpdate(true);
        _stepFourLoopSequence.Join(_fourRectTransform.DOScale(pulseScale, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepFourLoopSequence.Join(_fourRectTransform.DOAnchorPos(bouncePos, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepFourLoopSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopStepFourLoopAnimation(bool restoreTransform)
    {
        if (_stepFourLoopSequence != null)
        {
            _stepFourLoopSequence.Kill();
            _stepFourLoopSequence = null;
        }

        if (restoreTransform)
            RestoreStepFourOriginalTransform();
    }

    private Tween ShowStepFourVisual()
    {
        if (four == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_fourCanvasGroup == null)
            _fourCanvasGroup = GetOrAddCanvasGroup(four);

        CacheStepFourOriginalTransform();
        four.SetActive(true);

        if (_fourCanvasGroup != null)
        {
            _fourCanvasGroup.alpha = 0f;
            _fourCanvasGroup.interactable = false;
            _fourCanvasGroup.blocksRaycasts = false;
        }

        Image fourImage = four.GetComponent<Image>();
        if (fourImage != null)
            fourImage.raycastTarget = false;

        if (_fourRectTransform != null)
            _fourRectTransform.localScale = _fourOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);

        if (_fourRectTransform != null)
            show.Join(_fourRectTransform.DOScale(_fourOriginalScale, panelInDuration).SetEase(easeIn));

        if (_fourCanvasGroup != null)
            show.Join(_fourCanvasGroup.DOFade(1f, panelInDuration));

        return show;
    }

    private Tween HideStepFourVisual()
    {
        if (four == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_fourCanvasGroup == null)
            _fourCanvasGroup = GetOrAddCanvasGroup(four);

        if (_fourRectTransform == null)
            _fourRectTransform = four.GetComponent<RectTransform>();

        CacheStepFourOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);

        if (_fourRectTransform != null)
            hide.Join(_fourRectTransform.DOScale(_fourOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));

        if (_fourCanvasGroup != null)
            hide.Join(_fourCanvasGroup.DOFade(0f, panelOutDuration));

        hide.OnComplete(() =>
        {
            RestoreStepFourOriginalTransform();
            four.SetActive(false);
        });

        return hide;
    }

    private void SetStepFiveVisibleAnimated(bool visible)
    {
        if (!visible)
        {
            SetStepFiveVisibleImmediate(false);
            return;
        }

        CacheStepFiveOriginalTransform();

        if (five == null)
            return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        if (_fiveCanvasGroup == null)
            _fiveCanvasGroup = GetOrAddCanvasGroup(five);

        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowDim(blockInput: true));
        sequence.Join(ShowStepFiveVisual());
        sequence.OnComplete(() =>
        {
            _isTransitionInProgress = false;
            _activeTransition = null;
            StartCoroutine(ArmStepFiveDismissOnNextFrame());
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private void SetStepFiveVisibleImmediate(bool visible)
    {
        if (five == null)
            return;

        if (_fiveCanvasGroup == null)
            _fiveCanvasGroup = GetOrAddCanvasGroup(five);

        CacheStepFiveOriginalTransform();

        if (visible)
        {
            five.SetActive(true);
            RestoreStepFiveOriginalTransform();

            if (_fiveCanvasGroup != null)
            {
                _fiveCanvasGroup.alpha = 1f;
                _fiveCanvasGroup.interactable = true;
                _fiveCanvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            if (_fiveCanvasGroup != null)
            {
                _fiveCanvasGroup.alpha = 0f;
                _fiveCanvasGroup.interactable = false;
                _fiveCanvasGroup.blocksRaycasts = false;
            }

            five.SetActive(false);
        }
    }

    private void CacheStepFiveOriginalTransform()
    {
        if (_fiveRectTransform == null)
            _fiveRectTransform = five != null ? five.GetComponent<RectTransform>() : null;

        if (_fiveRectTransform == null)
            return;

        if (_fiveOriginalTransformCached)
            return;

        _fiveOriginalScale = _fiveRectTransform.localScale;
        _fiveOriginalAnchoredPosition = _fiveRectTransform.anchoredPosition;
        _fiveOriginalTransformCached = true;
    }

    private void RestoreStepFiveOriginalTransform()
    {
        if (_fiveRectTransform == null)
            return;

        if (!_fiveOriginalTransformCached)
            CacheStepFiveOriginalTransform();

        _fiveRectTransform.localScale = _fiveOriginalScale;
        _fiveRectTransform.anchoredPosition = _fiveOriginalAnchoredPosition;
    }

    private Tween ShowStepFiveVisual()
    {
        if (five == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_fiveCanvasGroup == null)
            _fiveCanvasGroup = GetOrAddCanvasGroup(five);

        CacheStepFiveOriginalTransform();
        five.SetActive(true);

        if (_fiveCanvasGroup != null)
        {
            _fiveCanvasGroup.alpha = 0f;
            _fiveCanvasGroup.interactable = true;
            _fiveCanvasGroup.blocksRaycasts = true;
        }

        if (_fiveRectTransform != null)
            _fiveRectTransform.localScale = _fiveOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);

        if (_fiveRectTransform != null)
            show.Join(_fiveRectTransform.DOScale(_fiveOriginalScale, panelInDuration).SetEase(easeIn));

        if (_fiveCanvasGroup != null)
            show.Join(_fiveCanvasGroup.DOFade(1f, panelInDuration));

        return show;
    }

    private Tween HideStepFiveVisual()
    {
        if (five == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_fiveCanvasGroup == null)
            _fiveCanvasGroup = GetOrAddCanvasGroup(five);

        if (_fiveRectTransform == null)
            _fiveRectTransform = five.GetComponent<RectTransform>();

        CacheStepFiveOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);

        if (_fiveRectTransform != null)
            hide.Join(_fiveRectTransform.DOScale(_fiveOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));

        if (_fiveCanvasGroup != null)
            hide.Join(_fiveCanvasGroup.DOFade(0f, panelOutDuration));

        hide.OnComplete(() =>
        {
            RestoreStepFiveOriginalTransform();
            five.SetActive(false);
        });

        return hide;
    }

    private void CacheStepThreeOriginalTransform()
    {
        if (_threeRectTransform == null)
            _threeRectTransform = three != null ? three.GetComponent<RectTransform>() : null;

        if (_threeRectTransform == null)
            return;

        if (_threeOriginalTransformCached)
            return;

        _threeOriginalScale = _threeRectTransform.localScale;
        _threeOriginalAnchoredPosition = _threeRectTransform.anchoredPosition;
        _threeOriginalTransformCached = true;
    }

    private void RestoreStepThreeOriginalTransform()
    {
        if (_threeRectTransform == null)
            return;

        if (!_threeOriginalTransformCached)
            CacheStepThreeOriginalTransform();

        _threeRectTransform.localScale = _threeOriginalScale;
        _threeRectTransform.anchoredPosition = _threeOriginalAnchoredPosition;
    }

    private void StartStepThreeLoopAnimation()
    {
        StopStepThreeLoopAnimation(restoreTransform: true);

        if (three == null || !three.activeInHierarchy)
            return;

        if (_threeRectTransform == null)
            _threeRectTransform = three.GetComponent<RectTransform>();

        if (_threeRectTransform == null)
            return;

        CacheStepThreeOriginalTransform();
        RestoreStepThreeOriginalTransform();

        Vector3 pulseScale = _threeOriginalScale * (1f + stepTwoPulseScalePercent);
        Vector2 bouncePos = _threeOriginalAnchoredPosition + Vector2.up * stepTwoBounceDistance;

        _stepThreeLoopSequence = DOTween.Sequence();
        _stepThreeLoopSequence.SetUpdate(true);
        _stepThreeLoopSequence.Join(_threeRectTransform.DOScale(pulseScale, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepThreeLoopSequence.Join(_threeRectTransform.DOAnchorPos(bouncePos, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepThreeLoopSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopStepThreeLoopAnimation(bool restoreTransform)
    {
        if (_stepThreeLoopSequence != null)
        {
            _stepThreeLoopSequence.Kill();
            _stepThreeLoopSequence = null;
        }

        if (restoreTransform)
            RestoreStepThreeOriginalTransform();
    }

    private Tween ShowStepThreeVisual()
    {
        if (three == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_threeCanvasGroup == null)
            _threeCanvasGroup = GetOrAddCanvasGroup(three);

        CacheStepThreeOriginalTransform();
        three.SetActive(true);

        if (_threeCanvasGroup != null)
        {
            _threeCanvasGroup.alpha = 0f;
            _threeCanvasGroup.interactable = false;
            _threeCanvasGroup.blocksRaycasts = false;
        }

        Image threeImage = three.GetComponent<Image>();
        if (threeImage != null)
            threeImage.raycastTarget = false;

        if (_threeRectTransform != null)
            _threeRectTransform.localScale = _threeOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);

        if (_threeRectTransform != null)
            show.Join(_threeRectTransform.DOScale(_threeOriginalScale, panelInDuration).SetEase(easeIn));

        if (_threeCanvasGroup != null)
            show.Join(_threeCanvasGroup.DOFade(1f, panelInDuration));

        return show;
    }

    private Tween HideStepThreeVisual()
    {
        if (three == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_threeCanvasGroup == null)
            _threeCanvasGroup = GetOrAddCanvasGroup(three);

        if (_threeRectTransform == null)
            _threeRectTransform = three.GetComponent<RectTransform>();

        CacheStepThreeOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);

        if (_threeRectTransform != null)
            hide.Join(_threeRectTransform.DOScale(_threeOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));

        if (_threeCanvasGroup != null)
            hide.Join(_threeCanvasGroup.DOFade(0f, panelOutDuration));

        hide.OnComplete(() =>
        {
            RestoreStepThreeOriginalTransform();
            three.SetActive(false);
        });

        return hide;
    }

    private Tween ShowStepTwoVisual()
    {
        if (two == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_twoCanvasGroup == null)
            _twoCanvasGroup = GetOrAddCanvasGroup(two);

        CacheStepTwoOriginalTransform();
        two.SetActive(true);

        if (_twoCanvasGroup != null)
        {
            _twoCanvasGroup.alpha = 0f;
            _twoCanvasGroup.interactable = false;
            _twoCanvasGroup.blocksRaycasts = false;
        }

        if (_twoRectTransform != null)
            _twoRectTransform.localScale = _twoOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);

        if (_twoRectTransform != null)
            show.Join(_twoRectTransform.DOScale(_twoOriginalScale, panelInDuration).SetEase(easeIn));

        if (_twoCanvasGroup != null)
            show.Join(_twoCanvasGroup.DOFade(1f, panelInDuration));

        return show;
    }

    private Tween HideStepTwoVisual()
    {
        if (two == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_twoCanvasGroup == null)
            _twoCanvasGroup = GetOrAddCanvasGroup(two);

        if (_twoRectTransform == null)
            _twoRectTransform = two.GetComponent<RectTransform>();

        CacheStepTwoOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);

        if (_twoRectTransform != null)
            hide.Join(_twoRectTransform.DOScale(_twoOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));

        if (_twoCanvasGroup != null)
            hide.Join(_twoCanvasGroup.DOFade(0f, panelOutDuration));

        hide.OnComplete(() =>
        {
            RestoreStepTwoOriginalTransform();
            two.SetActive(false);
        });

        return hide;
    }

    private void SetDimHiddenImmediate()
    {
        if (dim == null)
            return;

        if (_dimCanvasGroup == null)
            _dimCanvasGroup = GetOrAddCanvasGroup(dim);

        if (_dimCanvasGroup != null)
        {
            _dimCanvasGroup.alpha = 0f;
            _dimCanvasGroup.interactable = false;
            _dimCanvasGroup.blocksRaycasts = false;
        }

        dim.SetActive(false);
    }

    private void ReleaseTapSuppressionAfterStepOne()
    {
        if (_hasReleasedSuppressionAfterStepOne)
            return;

        if (useUnscaledTimeForDismissDelay)
            StartCoroutine(ReleaseSuppressionOnFrameBoundary());
        else
            ApplySuppressionRestoreImmediate();
    }

    private IEnumerator ReleaseSuppressionOnFrameBoundary()
    {
        yield return new WaitForEndOfFrame();
        ApplySuppressionRestoreImmediate();
    }

    private void ApplySuppressionRestoreImmediate()
    {
        UIFlowState.IsContentPanelOpen = _hadSuppressionBeforeTutorial;
        _hasReleasedSuppressionAfterStepOne = true;
    }

    private Tween ShowDim(bool blockInput = false)
    {
        if (dim == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_dimCanvasGroup == null)
            _dimCanvasGroup = GetOrAddCanvasGroup(dim);

        dim.SetActive(true);

        if (_dimCanvasGroup == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        _dimCanvasGroup.alpha = 0f;
        _dimCanvasGroup.interactable = blockInput;
        _dimCanvasGroup.blocksRaycasts = blockInput;

        return _dimCanvasGroup.DOFade(1f, dimFadeDuration).SetUpdate(true);
    }

    private Tween HideDim()
    {
        if (dim == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_dimCanvasGroup == null)
            _dimCanvasGroup = GetOrAddCanvasGroup(dim);

        if (_dimCanvasGroup == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        _dimCanvasGroup.interactable = false;
        _dimCanvasGroup.blocksRaycasts = false;

        return _dimCanvasGroup
            .DOFade(0f, dimFadeDuration)
            .SetUpdate(true)
            .OnComplete(() => dim.SetActive(false));
    }

    private void ShowStepVisualImmediate(GameObject stepVisual)
    {
        if (stepVisual == null)
            return;

        CanvasGroup cg = GetOrAddCanvasGroup(stepVisual);
        if (cg != null)
            cg.alpha = 1f;

        stepVisual.transform.localScale = Vector3.one * panelEndScale;
        stepVisual.SetActive(true);
    }

    private void HideStepVisualImmediate(GameObject stepVisual)
    {
        if (stepVisual == null)
            return;

        CanvasGroup cg = GetOrAddCanvasGroup(stepVisual);
        if (cg != null)
            cg.alpha = 0f;

        stepVisual.transform.localScale = Vector3.one * panelStartScale;
        stepVisual.SetActive(false);
    }

    private Sequence ShowStepVisual(GameObject stepVisual)
    {
        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);

        if (stepVisual == null)
            return sequence;

        CanvasGroup cg = GetOrAddCanvasGroup(stepVisual);
        stepVisual.SetActive(true);
        stepVisual.transform.localScale = Vector3.one * panelStartScale;

        if (cg != null)
            cg.alpha = 0f;

        sequence.Join(stepVisual.transform.DOScale(panelEndScale, panelInDuration).SetEase(easeIn));

        if (cg != null)
            sequence.Join(cg.DOFade(1f, panelInDuration));

        return sequence;
    }

    private Sequence HideStepVisual(GameObject stepVisual)
    {
        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);

        if (stepVisual == null)
            return sequence;

        CanvasGroup cg = GetOrAddCanvasGroup(stepVisual);

        sequence.Join(stepVisual.transform.DOScale(panelStartScale, panelOutDuration).SetEase(easeOut));

        if (cg != null)
            sequence.Join(cg.DOFade(0f, panelOutDuration));

        sequence.OnComplete(() => stepVisual.SetActive(false));
        return sequence;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = target.AddComponent<CanvasGroup>();

        return cg;
    }

    private void KillActiveTransition()
    {
        if (_activeTransition != null)
        {
            _activeTransition.Kill();
            _activeTransition = null;
        }

        _isTransitionInProgress = false;
    }

    private void DismissFirstStep()
    {
        if (_isDismissInProgress || _isTransitionInProgress)
            return;

        _isDismissInProgress = true;
        _isFirstStepOpen = false;

        KillActiveTransition();
        _isTransitionInProgress = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideDim());
        sequence.Join(HideStepVisual(one));
        sequence.OnComplete(() =>
        {
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 1);
            _saveData.Save();

            // Keep Step 1 lock state until Step 2 completion.
            ApplyStepOneUILock();

            _isDismissInProgress = false;
            _isTransitionInProgress = false;
            _activeTransition = null;

            ReleaseTapSuppressionAfterStepOne();
            EnterStepTwo();
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });

        _activeTransition = sequence;
    }

    private static bool WasAnyPointerPressedThisFrame()
    {
#if UNITY_EDITOR
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        return Input.GetMouseButtonDown(0);
#else
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;

        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
#endif
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP 7/8 — Chest tutorial (deterministic Common Chest + slot pointer)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tutorial chest reached its configured Z freeze line — pause gameplay so
    /// the player can study the chest before tapping it. Mirrors the Step 6
    /// freeze flow used for the first tutorial Nitro Coin.
    /// </summary>
    private void HandleTutorialChestReachedCenter(Chest chest)
    {
        if (chest == null || !chest.IsTutorial) return;
        if (_saveData != null && _saveData.tutorialChestCollected) return;

        _isAwaitingTutorialChestCollect = true;
        TutorialGate.SetGameplayFrozen(true);
    }

    /// <summary>
    /// Tutorial chest tapped: release the freeze immediately, persist the
    /// collected flag, then schedule the Seven popup that points to the
    /// inventory slot the chest landed in.
    /// </summary>
    private void HandleTutorialChestCollected(Chest chest)
    {
        if (_saveData == null) return;
        if (chest == null || !chest.IsTutorial) return;
        if (_saveData.tutorialChestCollected) return;

        _isAwaitingTutorialChestCollect = false;
        TutorialGate.SetGameplayFrozen(false);
        TutorialGate.SetTutorialChestActive(false);

        _saveData.tutorialChestCollected = true;
        _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 7);
        _saveData.Save();

        // Schedule Seven slightly after the collect → vanish animation completes
        // and after ChestShownUI has had a frame to spawn the new slot widget.
        DOVirtual.DelayedCall(sevenOpenDelay, StartStepSeven, true).SetUpdate(true);
    }

    /// <summary>
    /// Forwarded by ChestShownUI on every chest-slot tap. While Step 7 is
    /// active this hides the Seven panel and lets the existing ChestPopup
    /// open flow continue uninterrupted.
    /// </summary>
    private void HandleChestSlotTappedForStepSeven(int chestIndex)
    {
        if (!_isStepSevenActive || _isStepSevenCompletionInProgress) return;
        CompleteStepSeven();
    }

    /// <summary>
    /// Inventory raised the post-open removal event for a chest. If it was a
    /// tutorial-free Common chest, bump the persisted opened-count so the
    /// spawner knows when to stop forcing Common.
    /// </summary>
    private void HandleChestRemovedAfterOpen(ChestInventoryManager.ChestData removed)
    {
        if (_saveData == null || removed == null) return;
        if (!removed.isTutorialFreeChest) return;

        int next = Mathf.Min(_saveData.tutorialFreeChestOpenedCount + 1,
                             TutorialGate.TutorialFreeChestQuota);
        if (next == _saveData.tutorialFreeChestOpenedCount) return;

        _saveData.tutorialFreeChestOpenedCount = next;
        _saveData.Save();
        TutorialGate.SetTutorialFreeChestOpenedCount(next);

        // Step 14 entry: the third free Common Chest just opened.
        if (next >= TutorialGate.TutorialFreeChestQuota
            && !_saveData.radarTutorialQueued
            && !_saveData.thirteenDismissed)
        {
            _saveData.radarTutorialQueued = true;
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 14);
            _saveData.Save();
            Debug.Log("[TutorialMgr][RadarPolice] Quota reached — entering Step 14 (radar/police segment).");
            EnterRadarPoliceTutorial();
        }
    }

    /// <summary>
    /// Polled from Update while a tutorial chest spawn is pending. Spawns
    /// exactly once when the world is in a safe state.
    /// </summary>
    private void TryForceSpawnTutorialChest()
    {
        if (_saveData == null) { _isAwaitingTutorialChestSpawn = false; return; }
        if (_saveData.tutorialChestCollected) { _isAwaitingTutorialChestSpawn = false; return; }
        if (chestSpawner == null) return;
        if (TutorialGate.TutorialChestActive) return;
        if (ChestInventoryManager.Instance != null && ChestInventoryManager.Instance.IsInventoryFull) return;

        Chest spawned = chestSpawner.ForceSpawnTutorialChest();
        if (spawned != null)
            _isAwaitingTutorialChestSpawn = false;
    }

    // ── Step Seven popup (chest slot pointer) ──

    private void StartStepSeven()
    {
        if (_saveData == null) return;
        if (_isStepSevenActive || _isStepSevenCompletionInProgress) return;
        if (_saveData.chestSlotTutorialShown) return;
        if (seven == null) return;

        // Require an actual slot to point at. If the inventory is somehow empty
        // (chest already opened-and-removed before Seven scheduled), skip.
        if (ChestInventoryManager.Instance == null || ChestInventoryManager.Instance.ChestCount <= 0)
            return;

        _isStepSevenActive = true;

        MuteBottomBarForStepSeven();
        SetStepSevenVisibleAnimated(true);
    }

    private void CompleteStepSeven()
    {
        if (_isStepSevenCompletionInProgress) return;

        _isStepSevenCompletionInProgress = true;
        _isStepSevenActive = false;

        // Persist Step 7 completion BEFORE the hide animation starts. Tapping
        // the chest slot synchronously opens ChestPopup and the player can
        // immediately press "Open Now" → SceneManager.LoadScene(ChestOpenScene).
        // If we deferred this Save() into the OnComplete (≈0.2 s later), the
        // scene swap would race the save and on return to Main the routing
        // would still see chestSlotTutorialShown == false → the Cards-segment
        // (Three/Nine/Ten) would never appear.
        if (_saveData != null)
        {
            _saveData.chestSlotTutorialShown = true;
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 8);
            _saveData.Save();
            Debug.Log($"[TutorialMgr][CardsTut] CompleteStepSeven: persisted chestSlotTutorialShown=true, stepIdx={_saveData.currentStepIndex}");
        }

        StopStepSevenLoopAnimation(restoreTransform: false);

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepSevenVisual());
        sequence.OnComplete(() =>
        {
            SetStepSevenVisibleImmediate(false);

            _isStepSevenCompletionInProgress = false;

            // Restore normal bottombar interactability (still respects the
            // Step 5 lock applied via ApplyBottomBarInteractabilityLock).
            ApplyBottomBarInteractabilityLock();
            ApplyCardsTabLockState();
        });
    }

    /// <summary>
    /// Disables every BottomBar tab while the Seven pointer is on screen so
    /// the player can only interact with the highlighted chest slot. Restored
    /// on Seven completion via <see cref="ApplyBottomBarInteractabilityLock"/>.
    /// </summary>
    private void MuteBottomBarForStepSeven()
    {
        SetButtonInteractable(bankButton, false);
        SetButtonInteractable(shopAndCardsButton, false);
        SetButtonInteractable(clickerButton, false);
        SetButtonInteractable(blacklistButton, false);
        SetButtonInteractable(rankingButton, false);
        SetButtonInteractable(cardsTabButton, false);
    }

    private void SetStepSevenVisibleAnimated(bool visible)
    {
        if (!visible)
        {
            SetStepSevenVisibleImmediate(false);
            return;
        }

        CacheStepSevenOriginalTransform();

        if (seven == null) return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        if (_sevenCanvasGroup == null)
            _sevenCanvasGroup = GetOrAddCanvasGroup(seven);

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowStepSevenVisual());
        sequence.OnComplete(() =>
        {
            StartStepSevenLoopAnimation();
        });
    }

    private void SetStepSevenVisibleImmediate(bool visible)
    {
        if (seven == null) return;

        if (_sevenCanvasGroup == null)
            _sevenCanvasGroup = GetOrAddCanvasGroup(seven);

        CacheStepSevenOriginalTransform();

        if (visible)
        {
            seven.SetActive(true);
            RestoreStepSevenOriginalTransform();
            if (_sevenCanvasGroup != null)
            {
                _sevenCanvasGroup.alpha = 1f;
                // Seven does not block raycasts so the chest slot underneath
                // can receive the tap that completes Step Seven.
                _sevenCanvasGroup.interactable = false;
                _sevenCanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            if (_sevenCanvasGroup != null)
            {
                _sevenCanvasGroup.alpha = 0f;
                _sevenCanvasGroup.interactable = false;
                _sevenCanvasGroup.blocksRaycasts = false;
            }
            seven.SetActive(false);
        }
    }

    private Tween ShowStepSevenVisual()
    {
        if (seven == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_sevenCanvasGroup == null)
            _sevenCanvasGroup = GetOrAddCanvasGroup(seven);

        CacheStepSevenOriginalTransform();
        seven.SetActive(true);

        if (_sevenCanvasGroup != null)
        {
            _sevenCanvasGroup.alpha = 0f;
            // Pointer overlay must NOT swallow the slot tap underneath.
            _sevenCanvasGroup.interactable = false;
            _sevenCanvasGroup.blocksRaycasts = false;
        }

        if (_sevenRectTransform != null)
            _sevenRectTransform.localScale = _sevenOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);

        if (_sevenRectTransform != null)
            show.Join(_sevenRectTransform.DOScale(_sevenOriginalScale, panelInDuration).SetEase(easeIn));

        if (_sevenCanvasGroup != null)
            show.Join(_sevenCanvasGroup.DOFade(1f, panelInDuration));

        return show;
    }

    private Tween HideStepSevenVisual()
    {
        if (seven == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);

        if (_sevenCanvasGroup == null)
            _sevenCanvasGroup = GetOrAddCanvasGroup(seven);

        if (_sevenRectTransform == null)
            _sevenRectTransform = seven.GetComponent<RectTransform>();

        CacheStepSevenOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);

        if (_sevenRectTransform != null)
            hide.Join(_sevenRectTransform.DOScale(_sevenOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));

        if (_sevenCanvasGroup != null)
            hide.Join(_sevenCanvasGroup.DOFade(0f, panelOutDuration));

        hide.OnComplete(() =>
        {
            RestoreStepSevenOriginalTransform();
            seven.SetActive(false);
        });

        return hide;
    }

    private void CacheStepSevenOriginalTransform()
    {
        if (_sevenRectTransform == null)
            _sevenRectTransform = seven != null ? seven.GetComponent<RectTransform>() : null;

        if (_sevenRectTransform == null)
            return;

        if (_sevenOriginalTransformCached)
            return;

        _sevenOriginalScale = _sevenRectTransform.localScale;
        _sevenOriginalAnchoredPosition = _sevenRectTransform.anchoredPosition;
        _sevenOriginalTransformCached = true;
    }

    private void RestoreStepSevenOriginalTransform()
    {
        if (_sevenRectTransform == null)
            return;

        if (!_sevenOriginalTransformCached)
            CacheStepSevenOriginalTransform();

        _sevenRectTransform.localScale = _sevenOriginalScale;
        _sevenRectTransform.anchoredPosition = _sevenOriginalAnchoredPosition;
    }

    private void StartStepSevenLoopAnimation()
    {
        StopStepSevenLoopAnimation(restoreTransform: true);

        if (seven == null || !seven.activeInHierarchy)
            return;

        if (_sevenRectTransform == null)
            _sevenRectTransform = seven.GetComponent<RectTransform>();

        if (_sevenRectTransform == null)
            return;

        CacheStepSevenOriginalTransform();
        RestoreStepSevenOriginalTransform();

        Vector3 pulseScale = _sevenOriginalScale * (1f + stepTwoPulseScalePercent);
        Vector2 bouncePos = _sevenOriginalAnchoredPosition + Vector2.up * stepTwoBounceDistance;

        _stepSevenLoopSequence = DOTween.Sequence();
        _stepSevenLoopSequence.SetUpdate(true);
        _stepSevenLoopSequence.Join(_sevenRectTransform.DOScale(pulseScale, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepSevenLoopSequence.Join(_sevenRectTransform.DOAnchorPos(bouncePos, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepSevenLoopSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopStepSevenLoopAnimation(bool restoreTransform)
    {
        if (_stepSevenLoopSequence != null)
        {
            _stepSevenLoopSequence.Kill();
            _stepSevenLoopSequence = null;
        }

        if (restoreTransform)
            RestoreStepSevenOriginalTransform();
    }

    /// <summary>
    /// Public reset hook for future reset paths that do not clear all PlayerPrefs.
    /// </summary>
    public void ResetTutorialProgress()
    {
        TutorialSaveData.ResetProgress();
        _saveData = TutorialSaveData.Load();

        _isFirstStepOpen = false;
        _isStepTwoActive = false;
        _isStepTwoCompletionInProgress = false;
        _isStepThreeActive = false;
        _isStepThreeCompletionInProgress = false;
        _isStepFourActive = false;
        _isStepFourCompletionInProgress = false;
        _isStepFourPendingStart = false;
        _isStepFourStartDelayQueued = false;
        _isStepFiveActive = false;
        _isStepFiveCompletionInProgress = false;
        _canDismissStepFive = false;
        _isStepSixActive = false;
        _isStepSixCompletionInProgress = false;
        _canDismissStepSix = false;
        _isAwaitingFirstTutorialNitroCollect = false;
        _isStepSevenActive = false;
        _isStepSevenCompletionInProgress = false;
        _isAwaitingTutorialChestSpawn = false;
        _isAwaitingTutorialChestCollect = false;
        _isDismissInProgress = false;
        _hasReleasedSuppressionAfterStepOne = true;

        // Cards tutorial segment runtime flags
        _isStepThreeSecondActive = false;
        _isStepThreeSecondCompletionInProgress = false;
        _isStepThreeSecondStartDelayQueued = false;
        _isStepNineActive = false;
        _isStepNineCompletionInProgress = false;
        _isStepNineStartDelayQueued = false;
        _isStepTenActive = false;
        _isStepTenCompletionInProgress = false;
        _isStepTenStartDelayQueued = false;

        // Steps 14–19 runtime flags
        _isAwaitingTutorialRadarSpawn = false;
        _isStepSixteenActive = false;
        _isStepSixteenCompletionInProgress = false;
        _canDismissEleven = false;
        _isAwaitingTutorialChase = false;
        _isStepEighteenActive = false;
        _isStepEighteenCompletionInProgress = false;
        _canDismissThirteen = false;

        // Steps 20–22 runtime flags
        _isAwaitingGarageTutorialAfterChase = false;
        if (_garageTutorialAfterChaseCoroutine != null)
        {
            StopCoroutine(_garageTutorialAfterChaseCoroutine);
            _garageTutorialAfterChaseCoroutine = null;
        }

        // Mirror cleared persistent flags into the gate and clear transient state.
        TutorialGate.SyncFromSave(_saveData);
        TutorialGate.SetGameplayFrozen(false);
        TutorialGate.SetTutorialNitroActive(false);
        TutorialGate.SetTutorialChestActive(false);
        TutorialGate.SetTutorialFreeChestOpenedCount(0);
        TutorialGate.SetChestPipelineSuspended(false);
        RestoreFrozenCarAnimatorIfAny();

        StopStepTwoLoopAnimation(restoreTransform: true);
        StopStepThreeLoopAnimation(restoreTransform: true);
        StopStepFourLoopAnimation(restoreTransform: true);
        StopStepSevenLoopAnimation(restoreTransform: true);
        StopStepThreeNewLoopAnimation(restoreTransform: true);
        StopStepNineLoopAnimation(restoreTransform: true);
        StopStepTenLoopAnimation(restoreTransform: true);
        UnregisterShopAndCardsClickListener();
        UnregisterStepFourPurchaseListener();
        UnregisterClickerUnlockListener();
        UnregisterShopCardsCardsTutorialListener();
        UnregisterCardsTabCardsTutorialListener();
        UnregisterResumePipelineClickerListener();
        RestoreStepTenSuppressedSlotButtons();
        SetStepSixVisibleImmediate(false);
        SetStepSevenVisibleImmediate(false);
        SetStepThreeNewVisibleImmediate(false);
        SetStepNineVisibleImmediate(false);
        SetStepTenVisibleImmediate(false);
        SetStepElevenVisibleImmediate(false);
        SetStepTwelveVisibleImmediate(false);
        SetStepThirteenVisibleImmediate(false);
        StopStepTwelveLoopAnimation(restoreTransform: true);
        // Steps 20–22: Garage tutorial cleanup.
        UnregisterBtnGarageListener();
        StopStepFourteenLoopAnimation(restoreTransform: true);
        SetStepFourteenVisibleImmediate(false);
        if (fourteenInputBlocker != null) fourteenInputBlocker.SetActive(false);
        ApplyTutorialState();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Steps 9–12 — Post-First-Free-Chest Cards Tutorial
    //  Sequence (after the player opens the very first tutorial/free chest and
    //  returns to Main):
    //    9.  UI_Tutorial/Three (second show) — re-points at Shop&Cards button.
    //   10.  UI_Tutorial/Nine — points at Btn_TabCards inside PanelShop&Cards.
    //   11.  UI_Tutorial/Ten — points at the earned card slot in Cards tab.
    //   12.  CardDetailedPopup opens → wait for Clicker press → resume the
    //         suspended free-chest pipeline (counts 1/3 → 2/3 → 3/3 → normal).
    //  Throughout 9–11 the chest spawn pipeline is suspended via
    //  TutorialGate.ChestPipelineSuspended; it is cleared at the end of 12.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Routes to the highest-priority unfinished sub-step in the Cards tutorial
    /// segment based on the persisted save flags. Suspends the chest spawn
    /// pipeline for the duration of the segment.
    /// </summary>
    private void ApplyCardsTutorialSegmentStateImmediate()
    {
        if (_saveData == null)
            return;

        // Suspend chest pipeline until Step 12 (Clicker press after Ten).
        TutorialGate.SetChestPipelineSuspended(true);
        Debug.Log($"[TutorialMgr][CardsTut] ApplyCardsTutorialSegmentStateImmediate: pipeline suspended. " +
                  $"flags pending={_saveData.postFirstChestShopTutorialPending} cardsTutStarted={_saveData.cardsTutorialStarted} " +
                  $"shopAfterFirst={_saveData.shopCardsClickedAfterFirstChest} cardsTabClicked={_saveData.cardsTabClicked} " +
                  $"tenCompleted={_saveData.tenCompleted} clickerPressed={_saveData.cardsSegmentClickerPressed} " +
                  $"freeOpenedCount={_saveData.tutorialFreeChestOpenedCount} threeNewRef={(threeNew != null)} " +
                  $"tutorialRootActive={(tutorialRoot != null && tutorialRoot.activeSelf)}");

        // Re-assert bottom-bar / cards-tab lock with the segment-active rules.
        ApplyCardsTabLockState();
        ApplyBottomBarInteractabilityLock();

        // Clear stale Nine/Ten visuals from prior sessions. Note: we DO NOT
        // hide Three_New here. If the player belongs in Step 9 we would just
        // immediately re-show it, and a second SaveSystem.OnGameLoaded firing
        // mid-show would kill the in-flight fade tween (the symptom that
        // caused Three_New to silently disappear). Step 9's own start path
        // is responsible for its visual; if the player is past Step 9, the
        // dedicated start methods (Nine/Ten/Pipeline-resume) do not touch it.
        SetStepNineVisibleImmediate(false);
        SetStepTenVisibleImmediate(false);

        // Step 12 — Awaiting clicker press to resume pipeline.
        if (_saveData.tenCompleted && !_saveData.cardsSegmentClickerPressed)
        {
            Debug.Log("[TutorialMgr][CardsTut] Branch: Step 12 (await Clicker press).");
            RegisterResumePipelineClickerListener();
            return;
        }

        // Step 11 — Show Ten on the earned card slot.
        if (_saveData.cardsTabClicked && !_saveData.tenCompleted)
        {
            Debug.Log("[TutorialMgr][CardsTut] Branch: Step 11 (Ten on card slot).");
            // Make sure Cards tab is open (BuildSlots has run there).
            if (ShopCardsTabs.Instance != null)
                ShopCardsTabs.Instance.ShowCards();

            DOVirtual.DelayedCall(tenOpenDelay, StartStepTen, true).SetUpdate(true);
            return;
        }

        // Step 10 — Show Nine on Btn_TabCards (requires PanelShop&Cards open).
        if (_saveData.shopCardsClickedAfterFirstChest && !_saveData.cardsTabClicked)
        {
            Debug.Log("[TutorialMgr][CardsTut] Branch: Step 10 (Nine on Btn_TabCards, queued).");
            _isStepNineStartDelayQueued = true;
            DOVirtual.DelayedCall(nineOpenDelay, () =>
            {
                _isStepNineStartDelayQueued = false;
                if (!isActiveAndEnabled) return;
                StartStepNine();
            }, true).SetUpdate(true);
            return;
        }

        // Step 9 — Re-show Three pointing at Shop&Cards.
        if (_saveData.cardsTutorialStarted && !_saveData.shopCardsClickedAfterFirstChest)
        {
            // Mid-segment reload (Three was already shown at least once but the
            // player did not click Shop&Cards yet) → re-show immediately.
            Debug.Log("[TutorialMgr][CardsTut] Branch: Step 9 mid-reload → StartCardsTutorialThreeSecond NOW.");
            StartCardsTutorialThreeSecond();
            return;
        }

        // First entry to the segment (Step 9 begins now).
        if (!_saveData.cardsTutorialStarted)
        {
            Debug.Log($"[TutorialMgr][CardsTut] Branch: Step 9 first-entry → schedule Three(second) in {cardsTutorialThreeDelay}s.");
            _isStepThreeSecondStartDelayQueued = true;
            DOVirtual.DelayedCall(cardsTutorialThreeDelay, () =>
            {
                _isStepThreeSecondStartDelayQueued = false;
                Debug.Log($"[TutorialMgr][CardsTut] Delayed entry firing. isActiveAndEnabled={isActiveAndEnabled}");
                if (!isActiveAndEnabled) return;
                _saveData.cardsTutorialStarted = true;
                _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 9);
                _saveData.Save();
                StartCardsTutorialThreeSecond();
            }, true).SetUpdate(true);
        }
    }

    // ── Step 9: Three (second show) ──────────────────────────────────────────

    private void StartCardsTutorialThreeSecond()
    {
        Debug.Log($"[TutorialMgr][CardsTut] StartCardsTutorialThreeSecond ENTER. " +
                  $"shopAfterFirst={_saveData?.shopCardsClickedAfterFirstChest} active={_isStepThreeSecondActive} " +
                  $"completing={_isStepThreeSecondCompletionInProgress} threeNewRef={(threeNew != null)}");
        if (_saveData == null || _saveData.shopCardsClickedAfterFirstChest)
            return;

        if (_isStepThreeSecondCompletionInProgress)
        {
            Debug.Log("[TutorialMgr][CardsTut] StartCardsTutorialThreeSecond EARLY-RETURN: completion in progress.");
            return;
        }

        // If we are already "active" (a previous start ran in this Main load),
        // a second SaveSystem.OnGameLoaded re-entry can have hidden the visual
        // mid-tween. Do NOT early-return — forcibly re-ensure the visual so
        // Three_New always ends up actually shown.
        if (_isStepThreeSecondActive)
        {
            Debug.Log("[TutorialMgr][CardsTut] StartCardsTutorialThreeSecond: already active → EnsureThreeNewVisibleForCardsTutorial (re-ensure).");
            EnsureThreeNewVisibleForCardsTutorial();
            return;
        }

        _isStepThreeSecondActive = true;

        ApplyBottomBarInteractabilityLock();
        ApplyCardsTabLockState();
        RegisterShopCardsCardsTutorialListener();

        // Use the dedicated Three_New pointer (does NOT reuse Step 3's Three).
        SetStepThreeNewVisibleAnimated(true);
        Debug.Log($"[TutorialMgr][CardsTut] StartCardsTutorialThreeSecond CALLED SetStepThreeNewVisibleAnimated(true). threeNew.activeSelf={threeNew?.activeSelf}");
    }

    /// <summary>
    /// Idempotent, force-visible restore of UI_Tutorial/Three_New for the
    /// post-first-chest Shop&amp;Cards pointer. Bypasses the
    /// <c>_isStepThreeSecondActive</c> guard and the animated show pipeline so
    /// that any external interference (a second SaveSystem.OnGameLoaded
    /// hiding the object, a layout pass overwriting CanvasGroup alpha, the
    /// running show tween being killed mid-flight, etc.) cannot leave the
    /// pointer invisible while the player still needs to tap Shop&amp;Cards.
    /// Safe to call multiple times in the same frame.
    /// </summary>
    private void EnsureThreeNewVisibleForCardsTutorial()
    {
        if (threeNew == null)
        {
            Debug.LogError("[TutorialMgr][CardsTut] EnsureThreeNewVisibleForCardsTutorial: threeNew is NULL — Inspector field unassigned!");
            return;
        }

        if (tutorialRoot != null && !tutorialRoot.activeSelf)
            tutorialRoot.SetActive(true);

        if (_threeNewCanvasGroup == null) _threeNewCanvasGroup = GetOrAddCanvasGroup(threeNew);
        if (_threeNewRectTransform == null) _threeNewRectTransform = threeNew.GetComponent<RectTransform>();

        CacheStepThreeNewOriginalTransform();

        // Hard-set visual state (no tween — must end visible immediately).
        if (!threeNew.activeSelf) threeNew.SetActive(true);
        if (_threeNewRectTransform != null)
        {
            _threeNewRectTransform.localScale = _threeNewOriginalScale;
            _threeNewRectTransform.anchoredPosition = _threeNewOriginalAnchoredPosition;
            _threeNewRectTransform.SetAsLastSibling();
        }
        if (_threeNewCanvasGroup != null)
        {
            _threeNewCanvasGroup.alpha = 1f;
            _threeNewCanvasGroup.interactable = false;
            _threeNewCanvasGroup.blocksRaycasts = false;
        }

        Image threeNewImage = threeNew.GetComponent<Image>();
        if (threeNewImage != null)
        {
            threeNewImage.raycastTarget = false;
            if (!threeNewImage.enabled) threeNewImage.enabled = true;
            Color c = threeNewImage.color;
            if (c.a < 0.99f) { c.a = 1f; threeNewImage.color = c; }
        }

        // Restart pulse/bounce loop (StopStepThreeNewLoopAnimation kills any
        // existing one first, so this is safe even if it was already running).
        StartStepThreeNewLoopAnimation();

        // Make sure logical state mirrors visual state and the click listener
        // is registered (a previous Unregister-everything reset on this frame
        // could have removed it).
        _isStepThreeSecondActive = true;
        RegisterShopCardsCardsTutorialListener();

        // Re-assert bottom-bar lock so only Shop&Cards is interactable.
        ApplyBottomBarInteractabilityLock();
        ApplyCardsTabLockState();

        Transform parent = threeNew.transform.parent;
        string parentChain = parent != null ? $"{parent.name}.activeInHierarchy={parent.gameObject.activeInHierarchy}" : "<no parent>";
        Debug.Log($"[TutorialMgr][CardsTut] EnsureThreeNewVisibleForCardsTutorial DONE. " +
                  $"activeSelf={threeNew.activeSelf} activeInHierarchy={threeNew.activeInHierarchy} parent={parentChain} " +
                  $"cgAlpha={(_threeNewCanvasGroup != null ? _threeNewCanvasGroup.alpha : -1f)} " +
                  $"scale={(_threeNewRectTransform != null ? _threeNewRectTransform.localScale : Vector3.zero)} " +
                  $"anchoredPos={(_threeNewRectTransform != null ? _threeNewRectTransform.anchoredPosition : Vector2.zero)} " +
                  $"siblingIdx={(_threeNewRectTransform != null ? _threeNewRectTransform.GetSiblingIndex() : -1)} " +
                  $"imgEnabled={(threeNewImage != null && threeNewImage.enabled)} " +
                  $"imgColorA={(threeNewImage != null ? threeNewImage.color.a : -1f)} " +
                  $"loopRunning={(_stepThreeNewLoopSequence != null && _stepThreeNewLoopSequence.IsActive())} " +
                  $"listenerReg={_isShopCardsCardsTutorialListenerRegistered}");
    }

    private void RegisterShopCardsCardsTutorialListener()
    {
        if (_isShopCardsCardsTutorialListenerRegistered || shopAndCardsButton == null)
        {
            Debug.Log($"[TutorialMgr][CardsTut] RegisterShopCardsCardsTutorialListener SKIPPED. alreadyReg={_isShopCardsCardsTutorialListenerRegistered} btnRef={(shopAndCardsButton != null)}");
            return;
        }

        shopAndCardsButton.onClick.AddListener(HandleShopCardsClickedForCardsTutorial);
        _isShopCardsCardsTutorialListenerRegistered = true;
        Debug.Log("[TutorialMgr][CardsTut] Shop&Cards click listener REGISTERED for Three_New.");
    }

    private void UnregisterShopCardsCardsTutorialListener()
    {
        if (!_isShopCardsCardsTutorialListenerRegistered || shopAndCardsButton == null)
            return;

        shopAndCardsButton.onClick.RemoveListener(HandleShopCardsClickedForCardsTutorial);
        _isShopCardsCardsTutorialListenerRegistered = false;
    }

    private void HandleShopCardsClickedForCardsTutorial()
    {
        if (!_isStepThreeSecondActive || _isStepThreeSecondCompletionInProgress)
            return;

        CompleteCardsTutorialThreeSecond();
    }

    private void CompleteCardsTutorialThreeSecond()
    {
        if (_isStepThreeSecondCompletionInProgress)
            return;

        _isStepThreeSecondCompletionInProgress = true;
        _isStepThreeSecondActive = false;

        UnregisterShopCardsCardsTutorialListener();
        StopStepThreeNewLoopAnimation(restoreTransform: true);

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepThreeNewVisual());
        sequence.OnComplete(() =>
        {
            SetStepThreeNewVisibleImmediate(false);

            _saveData.shopCardsClickedAfterFirstChest = true;
            _saveData.postFirstChestShopTutorialPending = false; // Pending flag consumed.
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 10);
            _saveData.Save();
            Debug.Log("[TutorialMgr][CardsTut] CompleteCardsTutorialThreeSecond: shopCardsClickedAfterFirstChest=true, pending cleared, stepIdx>=10 saved.");

            // Cards tab unlocks now (permanently). Bottom bar state refreshes.
            ApplyCardsTabLockState();
            ApplyBottomBarInteractabilityLock();

            // Wait for PanelShop&Cards to actually be open before showing Nine.
            _isStepNineStartDelayQueued = true;
            _isStepThreeSecondCompletionInProgress = false;
            TryStartStepNinePending();
        });
    }

    private void TryStartStepNinePending()
    {
        if (!_isStepNineStartDelayQueued)
            return;

        if (_saveData == null
            || !_saveData.shopCardsClickedAfterFirstChest
            || _saveData.cardsTabClicked)
        {
            _isStepNineStartDelayQueued = false;
            return;
        }

        if (!IsShopCardsPanelOpen())
            return;

        _isStepNineStartDelayQueued = false;

        DOVirtual.DelayedCall(nineOpenDelay, () =>
        {
            if (!isActiveAndEnabled) return;
            StartStepNine();
        }, true).SetUpdate(true);
    }

    // ── Step 10: Nine — points at Btn_TabCards ───────────────────────────────

    private void StartStepNine()
    {
        if (_saveData == null || _saveData.cardsTabClicked)
            return;

        if (_isStepNineActive || _isStepNineCompletionInProgress)
            return;

        _isStepNineActive = true;

        ApplyBottomBarInteractabilityLock();
        ApplyCardsTabLockState();
        RegisterCardsTabCardsTutorialListener();
        SetStepNineVisibleAnimated(true);
    }

    private void RegisterCardsTabCardsTutorialListener()
    {
        if (_isCardsTabCardsTutorialListenerRegistered || cardsTabButton == null)
            return;

        cardsTabButton.onClick.AddListener(HandleCardsTabClickedForCardsTutorial);
        _isCardsTabCardsTutorialListenerRegistered = true;
    }

    private void UnregisterCardsTabCardsTutorialListener()
    {
        if (!_isCardsTabCardsTutorialListenerRegistered || cardsTabButton == null)
            return;

        cardsTabButton.onClick.RemoveListener(HandleCardsTabClickedForCardsTutorial);
        _isCardsTabCardsTutorialListenerRegistered = false;
    }

    private void HandleCardsTabClickedForCardsTutorial()
    {
        if (!_isStepNineActive || _isStepNineCompletionInProgress)
            return;

        CompleteStepNine();
    }

    private void CompleteStepNine()
    {
        if (_isStepNineCompletionInProgress)
            return;

        _isStepNineCompletionInProgress = true;
        _isStepNineActive = false;

        UnregisterCardsTabCardsTutorialListener();
        StopStepNineLoopAnimation(restoreTransform: true);

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepNineVisual());
        sequence.OnComplete(() =>
        {
            SetStepNineVisibleImmediate(false);

            _saveData.cardsTabClicked = true;
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 11);
            _saveData.Save();

            ApplyBottomBarInteractabilityLock();

            _isStepNineCompletionInProgress = false;
            DOVirtual.DelayedCall(tenOpenDelay, StartStepTen, true).SetUpdate(true);
        });
    }

    // ── Step 11: Ten — points at the earned card slot ────────────────────────

    private void StartStepTen()
    {
        if (_saveData == null || _saveData.tenCompleted)
            return;

        if (_isStepTenActive || _isStepTenCompletionInProgress)
            return;

        // Resolve target slot. Primary: the card type captured from the first
        // tutorial/free chest. Fallback: first unlocked slot in the collection
        // (covers any save-corruption / skipped-capture edge case).
        CardSlotUI targetSlot = ResolveStepTenTargetSlot();
        if (targetSlot == null)
        {
            // No resolvable slot — degrade gracefully: skip to Step 12 so the
            // player isn't stuck. ChestPipelineSuspended stays true; pressing
            // Clicker will clear it.
            Debug.LogWarning("[TutorialMgr][StepTen] No target slot resolvable. Skipping Ten.");
            _saveData.tenCompleted = true;
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 12);
            _saveData.Save();
            ApplyBottomBarInteractabilityLock();
            RegisterResumePipelineClickerListener();
            return;
        }

        _isStepTenActive = true;

        // Lock all card slots except the target so the player can only tap the
        // earned card.
        SuppressAllCardSlotButtonsExcept(targetSlot);

        ApplyBottomBarInteractabilityLock();
        SetStepTenVisibleAnimated(true);
    }

    private CardSlotUI ResolveStepTenTargetSlot()
    {
        if (cardCollectionUI == null)
            return null;

        if (_saveData != null && _saveData.firstFreeChestCardType >= 0)
        {
            CardType captured = (CardType)_saveData.firstFreeChestCardType;
            CardSlotUI byType = cardCollectionUI.GetSlotForType(captured);
            if (byType != null)
                return byType;
        }

        // Fallback: first slot whose card has been unlocked.
        var allSlots = cardCollectionUI.Slots;
        if (allSlots != null)
        {
            for (int i = 0; i < allSlots.Count; i++)
            {
                CardSlotUI s = allSlots[i];
                if (s != null && s.Card != null && s.Card.IsUnlocked)
                    return s;
            }
        }
        return null;
    }

    private void SuppressAllCardSlotButtonsExcept(CardSlotUI keepInteractable)
    {
        if (cardCollectionUI == null)
            return;

        if (_stepTenSuppressedSlotButtons == null)
            _stepTenSuppressedSlotButtons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
        else
            _stepTenSuppressedSlotButtons.Clear();

        var allSlots = cardCollectionUI.Slots;
        if (allSlots == null) return;

        for (int i = 0; i < allSlots.Count; i++)
        {
            CardSlotUI s = allSlots[i];
            if (s == null || s.button == null) continue;
            bool shouldBeInteractable = (s == keepInteractable);
            if (s.button.interactable != shouldBeInteractable)
            {
                _stepTenSuppressedSlotButtons.Add(s.button);
                s.button.interactable = shouldBeInteractable;
            }
        }
    }

    private void RestoreStepTenSuppressedSlotButtons()
    {
        if (_stepTenSuppressedSlotButtons == null)
            return;

        for (int i = 0; i < _stepTenSuppressedSlotButtons.Count; i++)
        {
            var b = _stepTenSuppressedSlotButtons[i];
            if (b != null) b.interactable = true;
        }
        _stepTenSuppressedSlotButtons.Clear();
    }

    private void HandleCardDetailPopupShownForStepTen(CardDefinition shown)
    {
        if (!_isStepTenActive || _isStepTenCompletionInProgress)
            return;

        CompleteStepTen();
    }

    private void CompleteStepTen()
    {
        if (_isStepTenCompletionInProgress)
            return;

        _isStepTenCompletionInProgress = true;
        _isStepTenActive = false;

        StopStepTenLoopAnimation(restoreTransform: true);
        RestoreStepTenSuppressedSlotButtons();

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepTenVisual());
        sequence.OnComplete(() =>
        {
            SetStepTenVisibleImmediate(false);

            _saveData.tenCompleted = true;
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 12);
            _saveData.Save();

            // Clicker becomes interactable again now that Ten is out.
            ApplyBottomBarInteractabilityLock();

            _isStepTenCompletionInProgress = false;
            RegisterResumePipelineClickerListener();
        });
    }

    // ── Step 12: Awaiting Clicker press to resume chest pipeline ─────────────

    private void RegisterResumePipelineClickerListener()
    {
        if (_isResumePipelineClickerListenerRegistered || clickerButton == null)
            return;

        clickerButton.onClick.AddListener(HandleClickerPressedForResumePipeline);
        _isResumePipelineClickerListenerRegistered = true;
    }

    private void UnregisterResumePipelineClickerListener()
    {
        if (!_isResumePipelineClickerListenerRegistered || clickerButton == null)
            return;

        clickerButton.onClick.RemoveListener(HandleClickerPressedForResumePipeline);
        _isResumePipelineClickerListenerRegistered = false;
    }

    private void HandleClickerPressedForResumePipeline()
    {
        if (_saveData == null || _saveData.cardsSegmentClickerPressed)
        {
            UnregisterResumePipelineClickerListener();
            return;
        }

        UnregisterResumePipelineClickerListener();

        _saveData.cardsSegmentClickerPressed = true;
        _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 13);
        _saveData.Save();

        // Resume the chest spawn pipeline.
        TutorialGate.SetChestPipelineSuspended(false);

        // Refresh bottom bar (clicker stays interactable; this just ensures
        // any sibling lock state recomputes cleanly).
        ApplyBottomBarInteractabilityLock();
    }

    // ── Step 10/11 visual pipeline (Nine + Ten) ──────────────────────────────

    private void CacheStepNineOriginalTransform()
    {
        if (_nineRectTransform == null)
            _nineRectTransform = nine != null ? nine.GetComponent<RectTransform>() : null;
        if (_nineRectTransform == null) return;
        if (_nineOriginalTransformCached) return;
        _nineOriginalScale = _nineRectTransform.localScale;
        _nineOriginalAnchoredPosition = _nineRectTransform.anchoredPosition;
        _nineOriginalTransformCached = true;
    }

    private void RestoreStepNineOriginalTransform()
    {
        if (_nineRectTransform == null) return;
        if (!_nineOriginalTransformCached) CacheStepNineOriginalTransform();
        _nineRectTransform.localScale = _nineOriginalScale;
        _nineRectTransform.anchoredPosition = _nineOriginalAnchoredPosition;
    }

    private void StartStepNineLoopAnimation()
    {
        StopStepNineLoopAnimation(restoreTransform: true);
        if (nine == null || !nine.activeInHierarchy) return;
        if (_nineRectTransform == null) _nineRectTransform = nine.GetComponent<RectTransform>();
        if (_nineRectTransform == null) return;

        CacheStepNineOriginalTransform();
        RestoreStepNineOriginalTransform();

        Vector3 pulseScale = _nineOriginalScale * (1f + stepTwoPulseScalePercent);
        Vector2 bouncePos = _nineOriginalAnchoredPosition + Vector2.up * stepTwoBounceDistance;

        _stepNineLoopSequence = DOTween.Sequence();
        _stepNineLoopSequence.SetUpdate(true);
        _stepNineLoopSequence.Join(_nineRectTransform.DOScale(pulseScale, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepNineLoopSequence.Join(_nineRectTransform.DOAnchorPos(bouncePos, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepNineLoopSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopStepNineLoopAnimation(bool restoreTransform)
    {
        if (_stepNineLoopSequence != null)
        {
            _stepNineLoopSequence.Kill();
            _stepNineLoopSequence = null;
        }
        if (restoreTransform) RestoreStepNineOriginalTransform();
    }

    private void SetStepNineVisibleAnimated(bool visible)
    {
        StopStepNineLoopAnimation(restoreTransform: true);

        if (!visible)
        {
            SetStepNineVisibleImmediate(false);
            return;
        }

        if (nine == null) return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        if (_nineCanvasGroup == null) _nineCanvasGroup = GetOrAddCanvasGroup(nine);
        CacheStepNineOriginalTransform();

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowStepNineVisual());
        sequence.OnComplete(() => StartStepNineLoopAnimation());
    }

    private void SetStepNineVisibleImmediate(bool visible)
    {
        if (nine == null) return;
        if (_nineCanvasGroup == null) _nineCanvasGroup = GetOrAddCanvasGroup(nine);

        if (visible)
        {
            CacheStepNineOriginalTransform();
            nine.SetActive(true);
            if (_nineCanvasGroup != null)
            {
                _nineCanvasGroup.alpha = 1f;
                _nineCanvasGroup.interactable = false;
                _nineCanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            StopStepNineLoopAnimation(restoreTransform: true);
            if (_nineCanvasGroup != null)
            {
                _nineCanvasGroup.alpha = 0f;
                _nineCanvasGroup.interactable = false;
                _nineCanvasGroup.blocksRaycasts = false;
            }
            Image nineImage = nine.GetComponent<Image>();
            if (nineImage != null) nineImage.raycastTarget = false;
            nine.SetActive(false);
        }
    }

    private Tween ShowStepNineVisual()
    {
        if (nine == null) return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_nineCanvasGroup == null) _nineCanvasGroup = GetOrAddCanvasGroup(nine);

        CacheStepNineOriginalTransform();
        nine.SetActive(true);

        if (_nineCanvasGroup != null)
        {
            _nineCanvasGroup.alpha = 0f;
            _nineCanvasGroup.interactable = false;
            _nineCanvasGroup.blocksRaycasts = false;
        }

        Image nineImage = nine.GetComponent<Image>();
        if (nineImage != null) nineImage.raycastTarget = false;

        if (_nineRectTransform != null)
            _nineRectTransform.localScale = _nineOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);
        if (_nineRectTransform != null)
            show.Join(_nineRectTransform.DOScale(_nineOriginalScale, panelInDuration).SetEase(easeIn));
        if (_nineCanvasGroup != null)
            show.Join(_nineCanvasGroup.DOFade(1f, panelInDuration));
        return show;
    }

    private Tween HideStepNineVisual()
    {
        if (nine == null) return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_nineCanvasGroup == null) _nineCanvasGroup = GetOrAddCanvasGroup(nine);
        if (_nineRectTransform == null) _nineRectTransform = nine.GetComponent<RectTransform>();

        CacheStepNineOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);
        if (_nineRectTransform != null)
            hide.Join(_nineRectTransform.DOScale(_nineOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));
        if (_nineCanvasGroup != null)
            hide.Join(_nineCanvasGroup.DOFade(0f, panelOutDuration));

        hide.OnComplete(() =>
        {
            RestoreStepNineOriginalTransform();
            nine.SetActive(false);
        });
        return hide;
    }

    // ── Step 9 visual pipeline (Three_New — second-show Shop&Cards pointer) ───

    private void CacheStepThreeNewOriginalTransform()
    {
        if (_threeNewRectTransform == null)
            _threeNewRectTransform = threeNew != null ? threeNew.GetComponent<RectTransform>() : null;
        if (_threeNewRectTransform == null) return;
        if (_threeNewOriginalTransformCached) return;
        _threeNewOriginalScale = _threeNewRectTransform.localScale;
        _threeNewOriginalAnchoredPosition = _threeNewRectTransform.anchoredPosition;
        _threeNewOriginalTransformCached = true;
    }

    private void RestoreStepThreeNewOriginalTransform()
    {
        if (_threeNewRectTransform == null) return;
        if (!_threeNewOriginalTransformCached) CacheStepThreeNewOriginalTransform();
        _threeNewRectTransform.localScale = _threeNewOriginalScale;
        _threeNewRectTransform.anchoredPosition = _threeNewOriginalAnchoredPosition;
    }

    private void StartStepThreeNewLoopAnimation()
    {
        StopStepThreeNewLoopAnimation(restoreTransform: true);
        if (threeNew == null || !threeNew.activeInHierarchy) return;
        if (_threeNewRectTransform == null) _threeNewRectTransform = threeNew.GetComponent<RectTransform>();
        if (_threeNewRectTransform == null) return;

        CacheStepThreeNewOriginalTransform();
        RestoreStepThreeNewOriginalTransform();

        Vector3 pulseScale = _threeNewOriginalScale * (1f + stepTwoPulseScalePercent);
        Vector2 bouncePos = _threeNewOriginalAnchoredPosition + Vector2.up * stepTwoBounceDistance;

        _stepThreeNewLoopSequence = DOTween.Sequence();
        _stepThreeNewLoopSequence.SetUpdate(true);
        _stepThreeNewLoopSequence.Join(_threeNewRectTransform.DOScale(pulseScale, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepThreeNewLoopSequence.Join(_threeNewRectTransform.DOAnchorPos(bouncePos, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepThreeNewLoopSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopStepThreeNewLoopAnimation(bool restoreTransform)
    {
        if (_stepThreeNewLoopSequence != null)
        {
            _stepThreeNewLoopSequence.Kill();
            _stepThreeNewLoopSequence = null;
        }
        if (restoreTransform) RestoreStepThreeNewOriginalTransform();
    }

    private void SetStepThreeNewVisibleAnimated(bool visible)
    {
        StopStepThreeNewLoopAnimation(restoreTransform: true);

        if (!visible)
        {
            SetStepThreeNewVisibleImmediate(false);
            return;
        }

        if (threeNew == null) return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        if (_threeNewCanvasGroup == null) _threeNewCanvasGroup = GetOrAddCanvasGroup(threeNew);
        CacheStepThreeNewOriginalTransform();

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowStepThreeNewVisual());
        sequence.OnComplete(() => StartStepThreeNewLoopAnimation());
    }

    private void SetStepThreeNewVisibleImmediate(bool visible)
    {
        if (threeNew == null) return;
        if (_threeNewCanvasGroup == null) _threeNewCanvasGroup = GetOrAddCanvasGroup(threeNew);

        if (visible)
        {
            CacheStepThreeNewOriginalTransform();
            threeNew.SetActive(true);
            RestoreStepThreeNewOriginalTransform();
            if (_threeNewCanvasGroup != null)
            {
                _threeNewCanvasGroup.alpha = 1f;
                _threeNewCanvasGroup.interactable = false;
                _threeNewCanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            StopStepThreeNewLoopAnimation(restoreTransform: true);
            if (_threeNewCanvasGroup != null)
            {
                _threeNewCanvasGroup.alpha = 0f;
                _threeNewCanvasGroup.interactable = false;
                _threeNewCanvasGroup.blocksRaycasts = false;
            }
            Image threeNewImage = threeNew.GetComponent<Image>();
            if (threeNewImage != null) threeNewImage.raycastTarget = false;
            threeNew.SetActive(false);
        }
    }

    private Tween ShowStepThreeNewVisual()
    {
        Debug.Log($"[TutorialMgr][CardsTut] ShowStepThreeNewVisual ENTER. threeNewRef={(threeNew != null)} " +
                  $"cgRef={(_threeNewCanvasGroup != null)} rtRef={(_threeNewRectTransform != null)} " +
                  $"originalScale={_threeNewOriginalScale} originalAnchored={_threeNewOriginalAnchoredPosition} cached={_threeNewOriginalTransformCached}");
        if (threeNew == null)
        {
            Debug.LogError("[TutorialMgr][CardsTut] threeNew is NULL! Assign UI_Tutorial/Three_New on the TutorialManager Inspector field 'Three New'.");
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        }
        if (_threeNewCanvasGroup == null) _threeNewCanvasGroup = GetOrAddCanvasGroup(threeNew);

        CacheStepThreeNewOriginalTransform();
        threeNew.SetActive(true);

        if (_threeNewCanvasGroup != null)
        {
            _threeNewCanvasGroup.alpha = 0f;
            _threeNewCanvasGroup.interactable = false;
            _threeNewCanvasGroup.blocksRaycasts = false;
        }

        Image threeNewImage = threeNew.GetComponent<Image>();
        if (threeNewImage != null) threeNewImage.raycastTarget = false;

        if (_threeNewRectTransform != null)
            _threeNewRectTransform.localScale = _threeNewOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);
        if (_threeNewRectTransform != null)
            show.Join(_threeNewRectTransform.DOScale(_threeNewOriginalScale, panelInDuration).SetEase(easeIn));
        if (_threeNewCanvasGroup != null)
            show.Join(_threeNewCanvasGroup.DOFade(1f, panelInDuration));
        return show;
    }

    private Tween HideStepThreeNewVisual()
    {
        if (threeNew == null) return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_threeNewCanvasGroup == null) _threeNewCanvasGroup = GetOrAddCanvasGroup(threeNew);
        if (_threeNewRectTransform == null) _threeNewRectTransform = threeNew.GetComponent<RectTransform>();

        CacheStepThreeNewOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);
        if (_threeNewRectTransform != null)
            hide.Join(_threeNewRectTransform.DOScale(_threeNewOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));
        if (_threeNewCanvasGroup != null)
            hide.Join(_threeNewCanvasGroup.DOFade(0f, panelOutDuration));

        hide.OnComplete(() =>
        {
            RestoreStepThreeNewOriginalTransform();
            threeNew.SetActive(false);
        });
        return hide;
    }

    private void CacheStepTenOriginalTransform()
    {
        if (_tenRectTransform == null)
            _tenRectTransform = ten != null ? ten.GetComponent<RectTransform>() : null;
        if (_tenRectTransform == null) return;
        if (_tenOriginalTransformCached) return;
        _tenOriginalScale = _tenRectTransform.localScale;
        _tenOriginalAnchoredPosition = _tenRectTransform.anchoredPosition;
        _tenOriginalTransformCached = true;
    }

    private void RestoreStepTenOriginalTransform()
    {
        if (_tenRectTransform == null) return;
        if (!_tenOriginalTransformCached) CacheStepTenOriginalTransform();
        _tenRectTransform.localScale = _tenOriginalScale;
        _tenRectTransform.anchoredPosition = _tenOriginalAnchoredPosition;
    }

    private void StartStepTenLoopAnimation()
    {
        StopStepTenLoopAnimation(restoreTransform: true);
        if (ten == null || !ten.activeInHierarchy) return;
        if (_tenRectTransform == null) _tenRectTransform = ten.GetComponent<RectTransform>();
        if (_tenRectTransform == null) return;

        CacheStepTenOriginalTransform();
        RestoreStepTenOriginalTransform();

        Vector3 pulseScale = _tenOriginalScale * (1f + stepTwoPulseScalePercent);
        Vector2 bouncePos = _tenOriginalAnchoredPosition + Vector2.up * stepTwoBounceDistance;

        _stepTenLoopSequence = DOTween.Sequence();
        _stepTenLoopSequence.SetUpdate(true);
        _stepTenLoopSequence.Join(_tenRectTransform.DOScale(pulseScale, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepTenLoopSequence.Join(_tenRectTransform.DOAnchorPos(bouncePos, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepTenLoopSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopStepTenLoopAnimation(bool restoreTransform)
    {
        if (_stepTenLoopSequence != null)
        {
            _stepTenLoopSequence.Kill();
            _stepTenLoopSequence = null;
        }
        if (restoreTransform) RestoreStepTenOriginalTransform();
    }

    private void SetStepTenVisibleAnimated(bool visible)
    {
        StopStepTenLoopAnimation(restoreTransform: true);

        if (!visible)
        {
            SetStepTenVisibleImmediate(false);
            return;
        }

        if (ten == null) return;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        if (_tenCanvasGroup == null) _tenCanvasGroup = GetOrAddCanvasGroup(ten);
        CacheStepTenOriginalTransform();

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowStepTenVisual());
        sequence.OnComplete(() => StartStepTenLoopAnimation());
    }

    private void SetStepTenVisibleImmediate(bool visible)
    {
        if (ten == null) return;
        if (_tenCanvasGroup == null) _tenCanvasGroup = GetOrAddCanvasGroup(ten);

        if (visible)
        {
            CacheStepTenOriginalTransform();
            ten.SetActive(true);
            if (_tenCanvasGroup != null)
            {
                _tenCanvasGroup.alpha = 1f;
                _tenCanvasGroup.interactable = false;
                _tenCanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            StopStepTenLoopAnimation(restoreTransform: true);
            if (_tenCanvasGroup != null)
            {
                _tenCanvasGroup.alpha = 0f;
                _tenCanvasGroup.interactable = false;
                _tenCanvasGroup.blocksRaycasts = false;
            }
            Image tenImage = ten.GetComponent<Image>();
            if (tenImage != null) tenImage.raycastTarget = false;
            ten.SetActive(false);
        }
    }

    private Tween ShowStepTenVisual()
    {
        if (ten == null) return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_tenCanvasGroup == null) _tenCanvasGroup = GetOrAddCanvasGroup(ten);

        CacheStepTenOriginalTransform();
        ten.SetActive(true);

        if (_tenCanvasGroup != null)
        {
            _tenCanvasGroup.alpha = 0f;
            _tenCanvasGroup.interactable = false;
            _tenCanvasGroup.blocksRaycasts = false;
        }

        Image tenImage = ten.GetComponent<Image>();
        if (tenImage != null) tenImage.raycastTarget = false;

        if (_tenRectTransform != null)
            _tenRectTransform.localScale = _tenOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);
        if (_tenRectTransform != null)
            show.Join(_tenRectTransform.DOScale(_tenOriginalScale, panelInDuration).SetEase(easeIn));
        if (_tenCanvasGroup != null)
            show.Join(_tenCanvasGroup.DOFade(1f, panelInDuration));
        return show;
    }

    private Tween HideStepTenVisual()
    {
        if (ten == null) return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_tenCanvasGroup == null) _tenCanvasGroup = GetOrAddCanvasGroup(ten);
        if (_tenRectTransform == null) _tenRectTransform = ten.GetComponent<RectTransform>();

        CacheStepTenOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);
        if (_tenRectTransform != null)
            hide.Join(_tenRectTransform.DOScale(_tenOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));
        if (_tenCanvasGroup != null)
            hide.Join(_tenCanvasGroup.DOFade(0f, panelOutDuration));

        hide.OnComplete(() =>
        {
            RestoreStepTenOriginalTransform();
            ten.SetActive(false);
        });
        return hide;
    }

    [ContextMenu("Tutorial: Reset Progress")]
    private void ContextResetTutorialProgress()
    {
        ResetTutorialProgress();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Steps 14–19 — Radar / Police Tutorial Segment
    //  After the 3rd free Common Chest opens:
    //   14. PopularityBar reveal (animated) + RadarUnlocked = true.
    //   15. Force-spawn deterministic tutorial Radar; freeze on center.
    //   16. Show Eleven (dialog) + Twelve (pointer) on tap.
    //   17. Tap-anywhere dismiss → unfreeze → force tutorial police chase.
    //   18. OnChaseStarted: freeze + show Thirteen.
    //   19. Tap-anywhere dismiss → unfreeze → PoliceLocked = false (permanent).
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Step 14 first-entry hook (called from <see cref="HandleChestRemovedAfterOpen"/>
    /// the moment the 3rd free chest opens).
    /// </summary>
    private void EnterRadarPoliceTutorial()
    {
        if (_saveData == null) { Debug.LogWarning("[TutorialMgr][RadarTut] EnterRadarPoliceTutorial: _saveData==null, abort."); return; }

        Debug.Log($"[TutorialMgr][RadarTut] EnterRadarPoliceTutorial: ENTER. radarSpawnerAssigned={(radarSpawner != null)} popularityBarRootAssigned={(popularityBarRoot != null)}");
        if (radarSpawner == null)
            Debug.LogError("[TutorialMgr][RadarTut] radarSpawner is NULL on TutorialManager — assign Inspector field 'Radar Spawner' to the scene RadarSpawner. Tutorial radar will NOT spawn.");
        if (popularityBarRoot == null)
            Debug.LogError("[TutorialMgr][RadarTut] popularityBarRoot is NULL on TutorialManager — assign Inspector field 'Popularity Bar Root' to the TopBar/PopularityBar GameObject. PopularityBar will NOT animate in.");

        _saveData.radarUnlocked = true;
        _saveData.Save();
        TutorialGate.SetRadarUnlocked(true);
        Debug.Log("[TutorialMgr][RadarTut] EnterRadarPoliceTutorial: radarUnlocked=TRUE persisted+gated.");

        ShowPopularityBarAnimated();

        // Begin the wait for a deterministic tutorial radar spawn.
        _isAwaitingTutorialRadarSpawn = true;
        Debug.Log("[TutorialMgr][RadarTut] EnterRadarPoliceTutorial: _isAwaitingTutorialRadarSpawn=TRUE.");
    }

    /// <summary>
    /// Re-applies the radar/police tutorial segment state on reload, branching
    /// to the highest-priority unfinished sub-step.
    /// </summary>
    private void ApplyRadarPoliceTutorialStateImmediate()
    {
        if (_saveData == null) return;

        TutorialGate.SyncFromSave(_saveData);
        // Transient flags clear on every (re)apply.
        TutorialGate.SetGameplayFrozen(false);
        RestoreFrozenCarAnimatorIfAny();

        // PopularityBar must be visible — immediate (no animation) on reload.
        ShowPopularityBarImmediate();

        // Defensive: ensure radar gate is open.
        if (!TutorialGate.RadarUnlocked)
        {
            _saveData.radarUnlocked = true;
            _saveData.Save();
            TutorialGate.SetRadarUnlocked(true);
        }

        // Reset transient visuals.
        SetStepElevenVisibleImmediate(false);
        SetStepTwelveVisibleImmediate(false);
        SetStepThirteenVisibleImmediate(false);
        StopStepTwelveLoopAnimation(restoreTransform: true);

        Debug.Log($"[TutorialMgr][RadarPolice] ApplyRadarPoliceTutorialStateImmediate: " +
                  $"firstSpawned={_saveData.firstTutorialRadarSpawned} tapped={_saveData.radarTutorialTapped} " +
                  $"elevenDismissed={_saveData.elevenTwelveDismissed} chaseStarted={_saveData.policeTutorialChaseStarted} " +
                  $"thirteenDismissed={_saveData.thirteenDismissed}");
        Debug.Log($"[TutorialMgr][RadarTut] ApplyRadarPoliceTutorialStateImmediate ENTER: radarSpawnerAssigned={(radarSpawner != null)} " +
                  $"popularityBarRootAssigned={(popularityBarRoot != null)} radarUnlocked={_saveData.radarUnlocked} policeLocked={_saveData.policeLocked} " +
                  $"queued={_saveData.radarTutorialQueued} firstSpawned={_saveData.firstTutorialRadarSpawned} tapped={_saveData.radarTutorialTapped} " +
                  $"elevenDismissed={_saveData.elevenTwelveDismissed} chaseStarted={_saveData.policeTutorialChaseStarted} thirteenDismissed={_saveData.thirteenDismissed}");
        if (radarSpawner == null)
            Debug.LogError("[TutorialMgr][RadarTut] radarSpawner NULL — assign Inspector field 'Radar Spawner'.");
        if (popularityBarRoot == null)
            Debug.LogError("[TutorialMgr][RadarTut] popularityBarRoot NULL — assign Inspector field 'Popularity Bar Root'.");

        // Step 19 — segment complete (defensive; outer caller already filtered this).
        if (_saveData.thirteenDismissed)
        {
            Debug.Log("[TutorialMgr][RadarTut] Branch=Step19 (segment complete).");
            return;
        }

        // Step 18 — chase was triggered last session but Thirteen never dismissed.
        // Per design: re-trigger the chase fresh and re-show Thirteen on OnChaseStarted.
        if (_saveData.policeTutorialChaseStarted && !_saveData.thirteenDismissed)
        {
            Debug.Log("[TutorialMgr][RadarTut] Branch=Step18 (chase started, Thirteen not dismissed) → re-trigger chase.");
            // Reset so a fresh chase fires.
            _saveData.policeTutorialChaseStarted = false;
            _saveData.Save();
            _isAwaitingTutorialChase = true;
            return;
        }

        // Step 17 — Eleven/Twelve already dismissed; need to start chase.
        if (_saveData.elevenTwelveDismissed && !_saveData.policeTutorialChaseStarted)
        {
            Debug.Log("[TutorialMgr][RadarTut] Branch=Step17 (await chase trigger).");
            _isAwaitingTutorialChase = true;
            return;
        }

        // Step 16 — player tapped tutorial radar; need to (re-)show Eleven/Twelve.
        if (_saveData.radarTutorialTapped && !_saveData.elevenTwelveDismissed)
        {
            Debug.Log("[TutorialMgr][RadarTut] Branch=Step16 (re-show Eleven/Twelve).");
            TutorialGate.SetGameplayFrozen(true);
            DOVirtual.DelayedCall(elevenTwelveOpenDelay, StartStepSixteen, true).SetUpdate(true);
            return;
        }

        // Step 15 / 14 — need a tutorial radar in the world.
        Debug.Log("[TutorialMgr][RadarTut] Branch=Step14/15 (await tutorial radar spawn). _isAwaitingTutorialRadarSpawn=TRUE.");
        _isAwaitingTutorialRadarSpawn = true;
    }

    // ── Step 14 — PopularityBar reveal ──

    private void CachePopularityBarOriginalTransform()
    {
        if (_popularityBarRectTransform == null) return;
        if (_popularityBarOriginalTransformCached) return;
        _popularityBarOriginalScale = _popularityBarRectTransform.localScale;
        _popularityBarOriginalTransformCached = true;
    }

    private void ShowPopularityBarImmediate()
    {
        if (popularityBarRoot == null)
        {
            Debug.LogError("[PopularityBar][RadarTut] ShowPopularityBarImmediate: popularityBarRoot NULL — assign Inspector field on TutorialManager. Aborting.");
            return;
        }
        CachePopularityBarOriginalTransform();

        bool wasActive = popularityBarRoot.activeSelf;
        Vector3 priorScale = _popularityBarRectTransform != null ? _popularityBarRectTransform.localScale : Vector3.one;
        float priorAlpha = _popularityBarCanvasGroup != null ? _popularityBarCanvasGroup.alpha : -1f;

        popularityBarRoot.SetActive(true);
        if (_popularityBarRectTransform != null)
            _popularityBarRectTransform.localScale = _popularityBarOriginalScale;
        if (_popularityBarCanvasGroup != null)
        {
            _popularityBarCanvasGroup.alpha = 1f;
            _popularityBarCanvasGroup.interactable = true;
            _popularityBarCanvasGroup.blocksRaycasts = true;
        }
        Debug.Log($"[PopularityBar][RadarTut] ShowPopularityBarImmediate: activeSelf {wasActive}→{popularityBarRoot.activeSelf} activeInHierarchy={popularityBarRoot.activeInHierarchy} alpha {priorAlpha}→{(_popularityBarCanvasGroup != null ? _popularityBarCanvasGroup.alpha : -1f)} scale {priorScale}→{(_popularityBarRectTransform != null ? _popularityBarRectTransform.localScale : Vector3.one)}");
    }

    private void ShowPopularityBarAnimated()
    {
        if (popularityBarRoot == null)
        {
            Debug.LogError("[PopularityBar][RadarTut] ShowPopularityBarAnimated: popularityBarRoot NULL — assign Inspector field 'Popularity Bar Root' on TutorialManager. Aborting.");
            return;
        }
        CachePopularityBarOriginalTransform();

        bool wasActive = popularityBarRoot.activeSelf;
        Vector3 priorScale = _popularityBarRectTransform != null ? _popularityBarRectTransform.localScale : Vector3.one;
        float priorAlpha = _popularityBarCanvasGroup != null ? _popularityBarCanvasGroup.alpha : -1f;
        Debug.Log($"[PopularityBar][RadarTut] ShowPopularityBarAnimated ENTER: activeSelf={wasActive} activeInHierarchy={popularityBarRoot.activeInHierarchy} alpha={priorAlpha} scale={priorScale} originalScale={_popularityBarOriginalScale} startScale={premiumStartScale} duration={premiumIntroDuration}");

        popularityBarRoot.SetActive(true);

        if (_popularityBarCanvasGroup != null)
        {
            _popularityBarCanvasGroup.alpha = 0f;
            _popularityBarCanvasGroup.interactable = false;
            _popularityBarCanvasGroup.blocksRaycasts = false;
        }

        if (_popularityBarRectTransform != null)
            _popularityBarRectTransform.localScale = _popularityBarOriginalScale * premiumStartScale;

        Sequence intro = DOTween.Sequence();
        intro.SetUpdate(true);

        if (_popularityBarRectTransform != null)
            intro.Join(_popularityBarRectTransform.DOScale(_popularityBarOriginalScale, premiumIntroDuration).SetEase(premiumIntroEase));
        if (_popularityBarCanvasGroup != null)
            intro.Join(_popularityBarCanvasGroup.DOFade(1f, premiumIntroDuration));

        intro.OnComplete(() =>
        {
            if (_popularityBarCanvasGroup != null)
            {
                _popularityBarCanvasGroup.interactable = true;
                _popularityBarCanvasGroup.blocksRaycasts = true;
            }
            Debug.Log($"[PopularityBar][RadarTut] ShowPopularityBarAnimated COMPLETE: alpha={(_popularityBarCanvasGroup != null ? _popularityBarCanvasGroup.alpha : -1f)} scale={(_popularityBarRectTransform != null ? _popularityBarRectTransform.localScale : Vector3.one)} activeInHierarchy={popularityBarRoot.activeInHierarchy}");
        });
    }

    // ── Step 15 — Force-spawn tutorial radar ──

    private bool _radarTutSpawnLoggedNullSpawner;
    private bool _radarTutSpawnLoggedFrozen;
    private void TryForceSpawnTutorialRadar()
    {
        if (_saveData == null) { _isAwaitingTutorialRadarSpawn = false; return; }
        if (_saveData.radarTutorialTapped) { _isAwaitingTutorialRadarSpawn = false; return; }

        // Idempotency: if a tutorial radar is already alive in the scene, do NOT
        // spawn another. Clear the awaiting flag so polling stops.
        Radar[] existing = FindObjectsByType<Radar>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].IsTutorialRadar)
            {
                _isAwaitingTutorialRadarSpawn = false;
                if (!_saveData.firstTutorialRadarSpawned)
                {
                    _saveData.firstTutorialRadarSpawned = true;
                    _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 15);
                    _saveData.Save();
                }
                Debug.Log($"[TutorialMgr][RadarTut] TryForceSpawnTutorialRadar: tutorial radar already alive ('{existing[i].name}'). Skipping spawn, clearing await.");
                return;
            }
        }

        if (radarSpawner == null)
        {
            if (!_radarTutSpawnLoggedNullSpawner)
            {
                Debug.LogError("[TutorialMgr][RadarTut] TryForceSpawnTutorialRadar: radarSpawner NULL — assign Inspector field 'Radar Spawner' on TutorialManager. Cannot force-spawn tutorial radar.");
                _radarTutSpawnLoggedNullSpawner = true;
            }
            return;
        }
        if (TutorialGate.GameplayFrozen)
        {
            if (!_radarTutSpawnLoggedFrozen)
            {
                Debug.Log("[TutorialMgr][RadarTut] TryForceSpawnTutorialRadar: gameplay frozen, deferring spawn.");
                _radarTutSpawnLoggedFrozen = true;
            }
            return;
        }
        _radarTutSpawnLoggedFrozen = false;

        // Clear the awaiting flag and persist firstTutorialRadarSpawned BEFORE
        // calling Instantiate, so any re-entrant Update poll within the same
        // frame cannot trigger a second spawn.
        _isAwaitingTutorialRadarSpawn = false;
        bool wasFirst = _saveData.firstTutorialRadarSpawned;
        if (!_saveData.firstTutorialRadarSpawned)
        {
            _saveData.firstTutorialRadarSpawned = true;
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 15);
            _saveData.Save();
        }

        Debug.Log($"[TutorialMgr][RadarTut] TryForceSpawnTutorialRadar: invoking radarSpawner.ForceSpawnTutorialRadar(). RadarUnlocked={TutorialGate.RadarUnlocked} firstSpawnedBefore={wasFirst}");
        Radar spawned = radarSpawner.ForceSpawnTutorialRadar();
        if (spawned != null)
        {
            Debug.Log($"[TutorialMgr][RadarTut] TryForceSpawnTutorialRadar: SPAWNED '{spawned.name}' at {spawned.transform.position}. firstSpawned {wasFirst}→{_saveData.firstTutorialRadarSpawned}.");
        }
        else
        {
            // Spawn failed (prefab/spawnpoints missing). Roll back so we can retry.
            _isAwaitingTutorialRadarSpawn = true;
            if (!wasFirst)
            {
                _saveData.firstTutorialRadarSpawned = false;
                _saveData.Save();
            }
            Debug.LogWarning("[TutorialMgr][RadarTut] TryForceSpawnTutorialRadar: ForceSpawnTutorialRadar() returned NULL. Rolled back firstTutorialRadarSpawned and re-queued spawn.");
        }
    }

    private void HandleTutorialRadarReachedCenter(Radar radar)
    {
        if (radar == null || !radar.IsTutorialRadar) return;

        Debug.Log($"[TutorialMgr][RadarTut] HandleTutorialRadarReachedCenter: radar='{radar.name}' pos={radar.transform.position}. Freezing gameplay.");
        // Freeze gameplay so the radar holds at the center until tapped.
        TutorialGate.SetGameplayFrozen(true);
    }

    private void HandleTutorialRadarTapped(Radar radar)
    {
        if (radar == null || !radar.IsTutorialRadar) return;
        if (_saveData == null) return;
        if (_saveData.radarTutorialTapped) return;

        _saveData.radarTutorialTapped = true;
        _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 16);
        _saveData.Save();
        Debug.Log($"[TutorialMgr][RadarTut] HandleTutorialRadarTapped: radar='{radar.name}'. radarTutorialTapped=TRUE persisted. Scheduling Eleven/Twelve in {elevenTwelveOpenDelay}s.");

        // Keep gameplay frozen across the tap-vanish animation; Eleven/Twelve are about to open.
        DOVirtual.DelayedCall(elevenTwelveOpenDelay, StartStepSixteen, true).SetUpdate(true);
    }

    // ── Step 16 — Eleven (dialog) + Twelve (pointer) ──

    private void StartStepSixteen()
    {
        if (_saveData == null) return;
        if (_saveData.elevenTwelveDismissed) return;
        if (_isStepSixteenActive || _isStepSixteenCompletionInProgress) return;

        // Make sure gameplay stays frozen during the popup.
        if (!TutorialGate.GameplayFrozen)
            TutorialGate.SetGameplayFrozen(true);

        _isStepSixteenActive = true;
        _canDismissEleven = false;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        _isTransitionInProgress = true;
        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowDim(blockInput: true));
        sequence.Join(ShowStepElevenVisual());
        sequence.Join(ShowStepTwelveVisual());
        sequence.OnComplete(() =>
        {
            _isTransitionInProgress = false;
            _activeTransition = null;
            StartStepTwelveLoopAnimation();
            StartCoroutine(ArmStepSixteenDismissOnNextFrame());
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });
        _activeTransition = sequence;
    }

    private IEnumerator ArmStepSixteenDismissOnNextFrame()
    {
        yield return null;
        _canDismissEleven = true;
    }

    private void CompleteStepSixteen()
    {
        if (_isStepSixteenCompletionInProgress) return;

        _isStepSixteenCompletionInProgress = true;
        _isStepSixteenActive = false;
        _canDismissEleven = false;

        StopStepTwelveLoopAnimation(restoreTransform: true);
        KillActiveTransition();

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepElevenVisual());
        sequence.Join(HideStepTwelveVisual());
        sequence.Join(HideDim());
        sequence.OnComplete(() =>
        {
            SetStepElevenVisibleImmediate(false);
            SetStepTwelveVisibleImmediate(false);

            _saveData.elevenTwelveDismissed = true;
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 17);
            _saveData.Save();

            TutorialGate.SetGameplayFrozen(false);

            _isStepSixteenCompletionInProgress = false;

            // Schedule the deterministic tutorial police chase.
            DOVirtual.DelayedCall(policeTutorialStartDelay, () =>
            {
                if (!isActiveAndEnabled) return;
                _isAwaitingTutorialChase = true;
            }, true).SetUpdate(true);
        });
    }

    // ── Step 17 — Force tutorial police chase ──

    private void TryForceTutorialChase()
    {
        if (_saveData == null) { _isAwaitingTutorialChase = false; return; }
        if (_saveData.policeTutorialChaseStarted) { _isAwaitingTutorialChase = false; return; }

        PoliceCatchTrigger trigger = policeCatchTrigger != null
            ? policeCatchTrigger
            : PoliceCatchTrigger.Instance;

        if (trigger == null) return;
        if (PoliceCatchController.Instance == null) return;
        if (PoliceCatchController.Instance.IsChaseActive) return;

        _saveData.policeTutorialChaseStarted = true;
        _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 18);
        _saveData.Save();
        _isAwaitingTutorialChase = false;

        trigger.ForceTutorialChase();
    }

    private void HandleChaseStartedForTutorial()
    {
        if (_saveData == null) return;
        if (_saveData.thirteenDismissed) return;
        if (!_saveData.policeTutorialChaseStarted) return;

        Debug.Log("[TutorialMgr][GarageTut] police chase started");

        // Defensive: chase started — ensure Btn_Garage is forced hidden until completion.
        ApplyBtnGarageGateVisibility("HandleChaseStartedForTutorial");

        // Freeze chase progression and show Thirteen.
        TutorialGate.SetGameplayFrozen(true);
        StartStepEighteen();
    }

    /// <summary>
    /// Fired by PoliceCatchController.OnChaseEnded. If the Garage tutorial is
    /// pending (Thirteen dismissed but Fourteen not yet shown), wait until the
    /// TopBar finishes restoring then enter the Garage tutorial. Otherwise no-op.
    /// </summary>
    private void HandleChaseEndedForGarageTutorial()
    {
        if (!_isAwaitingGarageTutorialAfterChase) return;
        if (_saveData == null) return;
        if (_saveData.fifteenDismissed) { _isAwaitingGarageTutorialAfterChase = false; return; }
        if (!_saveData.garageTutorialQueued) return;
        if (_saveData.garageButtonClicked) { _isAwaitingGarageTutorialAfterChase = false; return; }

        Debug.Log("[TutorialMgr][GarageTut] waiting for police chase end/topbar restore");

        if (_garageTutorialAfterChaseCoroutine != null)
            StopCoroutine(_garageTutorialAfterChaseCoroutine);
        _garageTutorialAfterChaseCoroutine = StartCoroutine(WaitForTopBarRestoredThenEnterGarageTutorial());
    }

    private IEnumerator WaitForTopBarRestoredThenEnterGarageTutorial()
    {
        // Poll until the TopBar reports restored (not compact). PoliceCatchController
        // calls TopBarAnimator.ShowAnimated() on chase end; the animation typically
        // toggles isCompact synchronously, but we still safety-poll a few frames.
        const float maxWaitSeconds = 3f;
        float elapsed = 0f;
        while (elapsed < maxWaitSeconds)
        {
            bool topBarRestored = TopBarAnimator.Instance == null || !TopBarAnimator.Instance.IsCompact;
            bool chaseClear = PoliceCatchController.Instance == null || !PoliceCatchController.Instance.IsChaseActive;
            if (topBarRestored && chaseClear) break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Re-validate guards (state may have changed during the wait).
        if (_saveData == null) yield break;
        if (_saveData.fifteenDismissed) { _isAwaitingGarageTutorialAfterChase = false; yield break; }
        if (!_saveData.garageTutorialQueued) yield break;
        if (_saveData.garageButtonClicked) { _isAwaitingGarageTutorialAfterChase = false; yield break; }

        Debug.Log("[TutorialMgr][GarageTut] police chase ended/topbar restored → starting Fourteen");
        _isAwaitingGarageTutorialAfterChase = false;
        _garageTutorialAfterChaseCoroutine = null;
        EnterGarageTutorial();
    }

    // ── Step 18 — Thirteen (dialog over the active chase) ──

    private void StartStepEighteen()
    {
        if (_saveData == null) return;
        if (_saveData.thirteenDismissed) return;
        if (_isStepEighteenActive || _isStepEighteenCompletionInProgress) return;

        if (!TutorialGate.GameplayFrozen)
            TutorialGate.SetGameplayFrozen(true);

        _isStepEighteenActive = true;
        _canDismissThirteen = false;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        _isTransitionInProgress = true;
        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowDim(blockInput: true));
        sequence.Join(ShowStepThirteenVisual());
        sequence.OnComplete(() =>
        {
            _isTransitionInProgress = false;
            _activeTransition = null;
            StartCoroutine(ArmStepEighteenDismissOnNextFrame());
        });
        sequence.OnKill(() =>
        {
            if (_activeTransition == sequence)
                _activeTransition = null;
        });
        _activeTransition = sequence;
    }

    private IEnumerator ArmStepEighteenDismissOnNextFrame()
    {
        yield return null;
        _canDismissThirteen = true;
    }

    private void CompleteStepEighteen()
    {
        if (_isStepEighteenCompletionInProgress) return;

        _isStepEighteenCompletionInProgress = true;
        _isStepEighteenActive = false;
        _canDismissThirteen = false;

        KillActiveTransition();

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepThirteenVisual());
        sequence.Join(HideDim());
        sequence.OnComplete(() =>
        {
            SetStepThirteenVisibleImmediate(false);

            _saveData.thirteenDismissed = true;
            _saveData.policeLocked = false;
            _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 19);
            // Step 20: queue the Garage tutorial. Btn_Garage stays permanently visible
            // from now on (topBarInitiallyHidden exception keyed on garageTutorialQueued).
            _saveData.garageTutorialQueued = true;
            _saveData.Save();

            TutorialGate.SetPoliceLocked(false);
            TutorialGate.SetGarageUnlocked(true);
            TutorialGate.SetGameplayFrozen(false);

            _isStepEighteenCompletionInProgress = false;
            Debug.Log("[TutorialMgr][RadarPolice] Step 19 complete — police permanently unlocked. Garage tutorial queued.");

            // Step 20 deferred: do NOT show Fourteen now — the police chase is
            // still running and the TopBar is in compact mode. Mark pending and
            // wait for OnChaseEnded + TopBar restored (handled in
            // HandleChaseEndedForGarageTutorial). On a cold reload while pending,
            // ApplyGarageTutorialStateImmediate covers the case (chase isn't
            // active on reload).
            _isAwaitingGarageTutorialAfterChase = true;
            Debug.Log("[TutorialMgr][GarageTut] thirteen dismissed — waiting for police chase end/topbar restore before showing Fourteen.");
            // Defensive: even though garageTutorialQueued is now TRUE, the chase
            // is still active. Keep Btn_Garage hidden until OnChaseEnded reveals it.
            ApplyBtnGarageGateVisibility("CompleteStepEighteen.OnComplete");
        });
    }

    // ── Step 16/18 visual pipeline (Eleven, Twelve, Thirteen) ──

    private void CacheStepElevenOriginalTransform()
    {
        if (_elevenRectTransform == null)
            _elevenRectTransform = eleven != null ? eleven.GetComponent<RectTransform>() : null;
        if (_elevenRectTransform == null) return;
        if (_elevenOriginalTransformCached) return;
        _elevenOriginalScale = _elevenRectTransform.localScale;
        _elevenOriginalAnchoredPosition = _elevenRectTransform.anchoredPosition;
        _elevenOriginalTransformCached = true;
    }

    private void RestoreStepElevenOriginalTransform()
    {
        if (_elevenRectTransform == null) return;
        if (!_elevenOriginalTransformCached) CacheStepElevenOriginalTransform();
        _elevenRectTransform.localScale = _elevenOriginalScale;
        _elevenRectTransform.anchoredPosition = _elevenOriginalAnchoredPosition;
    }

    private void SetStepElevenVisibleImmediate(bool visible)
    {
        if (eleven == null) return;
        if (_elevenCanvasGroup == null) _elevenCanvasGroup = GetOrAddCanvasGroup(eleven);
        CacheStepElevenOriginalTransform();

        if (visible)
        {
            eleven.SetActive(true);
            RestoreStepElevenOriginalTransform();
            if (_elevenCanvasGroup != null)
            {
                _elevenCanvasGroup.alpha = 1f;
                _elevenCanvasGroup.interactable = true;
                _elevenCanvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            if (_elevenCanvasGroup != null)
            {
                _elevenCanvasGroup.alpha = 0f;
                _elevenCanvasGroup.interactable = false;
                _elevenCanvasGroup.blocksRaycasts = false;
            }
            eleven.SetActive(false);
        }
    }

    private Tween ShowStepElevenVisual()
    {
        if (eleven == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_elevenCanvasGroup == null) _elevenCanvasGroup = GetOrAddCanvasGroup(eleven);
        CacheStepElevenOriginalTransform();

        eleven.SetActive(true);
        if (_elevenCanvasGroup != null)
        {
            _elevenCanvasGroup.alpha = 0f;
            _elevenCanvasGroup.interactable = true;
            _elevenCanvasGroup.blocksRaycasts = true;
        }
        if (_elevenRectTransform != null)
            _elevenRectTransform.localScale = _elevenOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);
        if (_elevenRectTransform != null)
            show.Join(_elevenRectTransform.DOScale(_elevenOriginalScale, panelInDuration).SetEase(easeIn));
        if (_elevenCanvasGroup != null)
            show.Join(_elevenCanvasGroup.DOFade(1f, panelInDuration));
        return show;
    }

    private Tween HideStepElevenVisual()
    {
        if (eleven == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_elevenCanvasGroup == null) _elevenCanvasGroup = GetOrAddCanvasGroup(eleven);
        if (_elevenRectTransform == null) _elevenRectTransform = eleven.GetComponent<RectTransform>();
        CacheStepElevenOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);
        if (_elevenRectTransform != null)
            hide.Join(_elevenRectTransform.DOScale(_elevenOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));
        if (_elevenCanvasGroup != null)
            hide.Join(_elevenCanvasGroup.DOFade(0f, panelOutDuration));
        hide.OnComplete(() =>
        {
            RestoreStepElevenOriginalTransform();
            eleven.SetActive(false);
        });
        return hide;
    }

    private void CacheStepTwelveOriginalTransform()
    {
        if (_twelveRectTransform == null)
            _twelveRectTransform = twelve != null ? twelve.GetComponent<RectTransform>() : null;
        if (_twelveRectTransform == null) return;
        if (_twelveOriginalTransformCached) return;
        _twelveOriginalScale = _twelveRectTransform.localScale;
        _twelveOriginalAnchoredPosition = _twelveRectTransform.anchoredPosition;
        _twelveOriginalTransformCached = true;
    }

    private void RestoreStepTwelveOriginalTransform()
    {
        if (_twelveRectTransform == null) return;
        if (!_twelveOriginalTransformCached) CacheStepTwelveOriginalTransform();
        _twelveRectTransform.localScale = _twelveOriginalScale;
        _twelveRectTransform.anchoredPosition = _twelveOriginalAnchoredPosition;
    }

    private void SetStepTwelveVisibleImmediate(bool visible)
    {
        if (twelve == null) return;
        if (_twelveCanvasGroup == null) _twelveCanvasGroup = GetOrAddCanvasGroup(twelve);
        CacheStepTwelveOriginalTransform();

        if (visible)
        {
            twelve.SetActive(true);
            RestoreStepTwelveOriginalTransform();
            if (_twelveCanvasGroup != null)
            {
                _twelveCanvasGroup.alpha = 1f;
                _twelveCanvasGroup.interactable = false;
                _twelveCanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            if (_twelveCanvasGroup != null)
            {
                _twelveCanvasGroup.alpha = 0f;
                _twelveCanvasGroup.interactable = false;
                _twelveCanvasGroup.blocksRaycasts = false;
            }
            twelve.SetActive(false);
        }
    }

    private Tween ShowStepTwelveVisual()
    {
        if (twelve == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_twelveCanvasGroup == null) _twelveCanvasGroup = GetOrAddCanvasGroup(twelve);
        CacheStepTwelveOriginalTransform();

        twelve.SetActive(true);
        if (_twelveCanvasGroup != null)
        {
            _twelveCanvasGroup.alpha = 0f;
            _twelveCanvasGroup.interactable = false;
            _twelveCanvasGroup.blocksRaycasts = false;
        }
        Image twelveImage = twelve.GetComponent<Image>();
        if (twelveImage != null) twelveImage.raycastTarget = false;

        if (_twelveRectTransform != null)
            _twelveRectTransform.localScale = _twelveOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);
        if (_twelveRectTransform != null)
            show.Join(_twelveRectTransform.DOScale(_twelveOriginalScale, panelInDuration).SetEase(easeIn));
        if (_twelveCanvasGroup != null)
            show.Join(_twelveCanvasGroup.DOFade(1f, panelInDuration));
        return show;
    }

    private Tween HideStepTwelveVisual()
    {
        if (twelve == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_twelveCanvasGroup == null) _twelveCanvasGroup = GetOrAddCanvasGroup(twelve);
        if (_twelveRectTransform == null) _twelveRectTransform = twelve.GetComponent<RectTransform>();
        CacheStepTwelveOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);
        if (_twelveRectTransform != null)
            hide.Join(_twelveRectTransform.DOScale(_twelveOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));
        if (_twelveCanvasGroup != null)
            hide.Join(_twelveCanvasGroup.DOFade(0f, panelOutDuration));
        hide.OnComplete(() =>
        {
            RestoreStepTwelveOriginalTransform();
            twelve.SetActive(false);
        });
        return hide;
    }

    private void StartStepTwelveLoopAnimation()
    {
        StopStepTwelveLoopAnimation(restoreTransform: true);
        if (twelve == null || !twelve.activeInHierarchy) return;
        if (_twelveRectTransform == null) _twelveRectTransform = twelve.GetComponent<RectTransform>();
        if (_twelveRectTransform == null) return;

        CacheStepTwelveOriginalTransform();
        RestoreStepTwelveOriginalTransform();

        Vector3 pulseScale = _twelveOriginalScale * (1f + stepTwoPulseScalePercent);
        Vector2 bouncePos = _twelveOriginalAnchoredPosition + Vector2.up * stepTwoBounceDistance;

        _stepTwelveLoopSequence = DOTween.Sequence();
        _stepTwelveLoopSequence.SetUpdate(true);
        _stepTwelveLoopSequence.Join(_twelveRectTransform.DOScale(pulseScale, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepTwelveLoopSequence.Join(_twelveRectTransform.DOAnchorPos(bouncePos, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepTwelveLoopSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopStepTwelveLoopAnimation(bool restoreTransform)
    {
        if (_stepTwelveLoopSequence != null)
        {
            _stepTwelveLoopSequence.Kill();
            _stepTwelveLoopSequence = null;
        }
        if (restoreTransform)
            RestoreStepTwelveOriginalTransform();
    }

    private void CacheStepThirteenOriginalTransform()
    {
        if (_thirteenRectTransform == null)
            _thirteenRectTransform = thirteen != null ? thirteen.GetComponent<RectTransform>() : null;
        if (_thirteenRectTransform == null) return;
        if (_thirteenOriginalTransformCached) return;
        _thirteenOriginalScale = _thirteenRectTransform.localScale;
        _thirteenOriginalAnchoredPosition = _thirteenRectTransform.anchoredPosition;
        _thirteenOriginalTransformCached = true;
    }

    private void RestoreStepThirteenOriginalTransform()
    {
        if (_thirteenRectTransform == null) return;
        if (!_thirteenOriginalTransformCached) CacheStepThirteenOriginalTransform();
        _thirteenRectTransform.localScale = _thirteenOriginalScale;
        _thirteenRectTransform.anchoredPosition = _thirteenOriginalAnchoredPosition;
    }

    private void SetStepThirteenVisibleImmediate(bool visible)
    {
        if (thirteen == null) return;
        if (_thirteenCanvasGroup == null) _thirteenCanvasGroup = GetOrAddCanvasGroup(thirteen);
        CacheStepThirteenOriginalTransform();

        if (visible)
        {
            thirteen.SetActive(true);
            RestoreStepThirteenOriginalTransform();
            if (_thirteenCanvasGroup != null)
            {
                _thirteenCanvasGroup.alpha = 1f;
                _thirteenCanvasGroup.interactable = true;
                _thirteenCanvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            if (_thirteenCanvasGroup != null)
            {
                _thirteenCanvasGroup.alpha = 0f;
                _thirteenCanvasGroup.interactable = false;
                _thirteenCanvasGroup.blocksRaycasts = false;
            }
            thirteen.SetActive(false);
        }
    }

    private Tween ShowStepThirteenVisual()
    {
        if (thirteen == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_thirteenCanvasGroup == null) _thirteenCanvasGroup = GetOrAddCanvasGroup(thirteen);
        CacheStepThirteenOriginalTransform();

        thirteen.SetActive(true);
        if (_thirteenCanvasGroup != null)
        {
            _thirteenCanvasGroup.alpha = 0f;
            _thirteenCanvasGroup.interactable = true;
            _thirteenCanvasGroup.blocksRaycasts = true;
        }
        if (_thirteenRectTransform != null)
            _thirteenRectTransform.localScale = _thirteenOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);
        if (_thirteenRectTransform != null)
            show.Join(_thirteenRectTransform.DOScale(_thirteenOriginalScale, panelInDuration).SetEase(easeIn));
        if (_thirteenCanvasGroup != null)
            show.Join(_thirteenCanvasGroup.DOFade(1f, panelInDuration));
        return show;
    }

    private Tween HideStepThirteenVisual()
    {
        if (thirteen == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_thirteenCanvasGroup == null) _thirteenCanvasGroup = GetOrAddCanvasGroup(thirteen);
        if (_thirteenRectTransform == null) _thirteenRectTransform = thirteen.GetComponent<RectTransform>();
        CacheStepThirteenOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);
        if (_thirteenRectTransform != null)
            hide.Join(_thirteenRectTransform.DOScale(_thirteenOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));
        if (_thirteenCanvasGroup != null)
            hide.Join(_thirteenCanvasGroup.DOFade(0f, panelOutDuration));
        hide.OnComplete(() =>
        {
            RestoreStepThirteenOriginalTransform();
            thirteen.SetActive(false);
        });
        return hide;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Step Seventeen — first Blacklist visit dialog (post-tutorial)
    //  Triggered the FIRST time Panel_BlackList is opened after the Garage
    //  tutorial completes (TutorialGate.BottomBarFullyUnlocked == true and
    //  TutorialSaveData.blacklistTutorialShown == false). Mirrors the
    //  popup/zoom + Dim style used by Eleven / Thirteen / Fifteen, with a
    //  tap-anywhere dismiss. Authored RectTransform position/scale are
    //  preserved across show/hide cycles. Save flag is set ONLY after the
    //  player dismisses, so a reload while still showing re-triggers next
    //  time Blacklist opens. No interaction with steps 1–22 — those have all
    //  completed by the time this can fire.
    // ═════════════════════════════════════════════════════════════════════════

    private void CacheStepSeventeenOriginalTransform()
    {
        if (_seventeenRectTransform == null)
            _seventeenRectTransform = seventeen != null ? seventeen.GetComponent<RectTransform>() : null;
        if (_seventeenRectTransform == null) return;
        if (_seventeenOriginalTransformCached) return;
        _seventeenOriginalScale = _seventeenRectTransform.localScale;
        _seventeenOriginalAnchoredPosition = _seventeenRectTransform.anchoredPosition;
        _seventeenOriginalTransformCached = true;
    }

    private void RestoreStepSeventeenOriginalTransform()
    {
        if (_seventeenRectTransform == null) return;
        if (!_seventeenOriginalTransformCached) CacheStepSeventeenOriginalTransform();
        _seventeenRectTransform.localScale = _seventeenOriginalScale;
        _seventeenRectTransform.anchoredPosition = _seventeenOriginalAnchoredPosition;
    }

    private Tween ShowStepSeventeenVisual()
    {
        if (seventeen == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_seventeenCanvasGroup == null) _seventeenCanvasGroup = GetOrAddCanvasGroup(seventeen);
        CacheStepSeventeenOriginalTransform();

        seventeen.SetActive(true);
        if (_seventeenCanvasGroup != null)
        {
            _seventeenCanvasGroup.alpha = 0f;
            _seventeenCanvasGroup.interactable = true;
            _seventeenCanvasGroup.blocksRaycasts = true;
        }
        if (_seventeenRectTransform != null)
            _seventeenRectTransform.localScale = _seventeenOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);
        if (_seventeenRectTransform != null)
            show.Join(_seventeenRectTransform.DOScale(_seventeenOriginalScale, panelInDuration).SetEase(easeIn));
        if (_seventeenCanvasGroup != null)
            show.Join(_seventeenCanvasGroup.DOFade(1f, panelInDuration));
        return show;
    }

    private Tween HideStepSeventeenVisual()
    {
        if (seventeen == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_seventeenCanvasGroup == null) _seventeenCanvasGroup = GetOrAddCanvasGroup(seventeen);
        if (_seventeenRectTransform == null) _seventeenRectTransform = seventeen.GetComponent<RectTransform>();
        CacheStepSeventeenOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);
        if (_seventeenRectTransform != null)
            hide.Join(_seventeenRectTransform.DOScale(_seventeenOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));
        if (_seventeenCanvasGroup != null)
            hide.Join(_seventeenCanvasGroup.DOFade(0f, panelOutDuration));
        hide.OnComplete(() =>
        {
            RestoreStepSeventeenOriginalTransform();
            if (_seventeenCanvasGroup != null)
            {
                _seventeenCanvasGroup.alpha = 0f;
                _seventeenCanvasGroup.interactable = false;
                _seventeenCanvasGroup.blocksRaycasts = false;
            }
            seventeen.SetActive(false);
        });
        return hide;
    }

    /// <summary>
    /// Public hook called by <see cref="BlacklistPanelController.OnEnable"/>
    /// every time Panel_BlackList becomes active. Idempotent: only the FIRST
    /// open after Garage tutorial completion shows Seventeen; further opens
    /// (and any open before BottomBar is fully unlocked) are no-ops.
    /// </summary>
    public void NotifyBlacklistPanelOpened()
    {
        if (_saveData == null) return;
        // Hard guard: never fire before BottomBar is fully unlocked. The Blacklist
        // button itself is locked until then so this is a defensive double-check.
        if (!TutorialGate.BottomBarFullyUnlocked) return;
        if (_saveData.blacklistTutorialShown) return;
        if (_isStepSeventeenActive || _isStepSeventeenCompletionInProgress) return;

        StartStepSeventeen();
    }

    private void StartStepSeventeen()
    {
        if (seventeen == null)
        {
            Debug.LogWarning("[TutorialMgr][BlacklistTut] StartStepSeventeen: 'seventeen' Inspector field is NULL — assign UI_Tutorial/Seventeen on TutorialManager. Marking as shown to avoid repeated trigger.");
            if (_saveData != null)
            {
                _saveData.blacklistTutorialShown = true;
                _saveData.Save();
            }
            return;
        }

        _isStepSeventeenActive = true;
        _canDismissSeventeen = false;

        if (tutorialRoot != null)
            tutorialRoot.SetActive(true);

        Debug.Log("[TutorialMgr][BlacklistTut] Showing Seventeen");

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(ShowDim(blockInput: true));
        sequence.Join(ShowStepSeventeenVisual());
        sequence.OnComplete(() =>
        {
            // Arm dismiss on next frame so the same tap that opened the
            // Blacklist panel cannot accidentally close Seventeen.
            StartCoroutine(ArmStepSeventeenDismissOnNextFrame());
        });
    }

    private IEnumerator ArmStepSeventeenDismissOnNextFrame()
    {
        yield return null;
        _canDismissSeventeen = true;
    }

    private void CompleteStepSeventeen()
    {
        if (_isStepSeventeenCompletionInProgress) return;

        _isStepSeventeenCompletionInProgress = true;
        _isStepSeventeenActive = false;
        _canDismissSeventeen = false;

        Debug.Log("[TutorialMgr][BlacklistTut] Dismissing Seventeen");

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(HideStepSeventeenVisual());
        sequence.Join(HideDim());
        sequence.OnComplete(() =>
        {
            _isStepSeventeenCompletionInProgress = false;
            if (_saveData != null)
            {
                _saveData.blacklistTutorialShown = true;
                _saveData.Save();
                Debug.Log("[TutorialMgr][BlacklistTut] blacklistTutorialShown=true saved");
            }
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Steps 20–22 — Garage Tutorial Segment
    //  After Step 19 (Thirteen dismissed) the player must be guided to the
    //  garage:
    //   20. Btn_Garage reveal (animated) on TopBar; Fourteen pointer loop;
    //       transparent input blocker so only Btn_Garage is interactable.
    //   21. Btn_Garage clicked → smooth Fourteen hide + remove blocker; the
    //       existing GarageSceneLoader.LoadGarageScene UnityEvent listener on
    //       Btn_Garage handles the scene swap. Persist garageButtonClicked.
    //   22. NewGarage scene: GarageTutorialController shows Dim+Fifteen,
    //       tap-anywhere dismiss → fifteenDismissed=true (segment complete).
    // ═════════════════════════════════════════════════════════════════════════

    private void EnterGarageTutorial()
    {
        if (_saveData == null) { Debug.LogWarning("[TutorialMgr][GarageTut] EnterGarageTutorial: _saveData==null, abort."); return; }
        if (_saveData.fifteenDismissed) { Debug.Log("[TutorialMgr][GarageTut] EnterGarageTutorial: fifteenDismissed=TRUE → skip."); return; }

        Debug.Log($"[TutorialMgr][GarageTut] EnterGarageTutorial: ENTER. btnGarageRoot={(btnGarageRoot != null)} btnGarage={(btnGarage != null)} fourteen={(fourteen != null)} fourteenInputBlocker={(fourteenInputBlocker != null)}");
        if (btnGarageRoot == null)
            Debug.LogError("[TutorialMgr][GarageTut] btnGarageRoot NULL — assign Inspector field 'Btn Garage Root'.");
        if (btnGarage == null)
            Debug.LogError("[TutorialMgr][GarageTut] btnGarage NULL — assign Inspector field 'Btn Garage' (Button component).");
        if (fourteen == null)
            Debug.LogError("[TutorialMgr][GarageTut] fourteen NULL — assign Inspector field 'Fourteen'.");

        ShowBtnGarageAnimated();

        DOVirtual.DelayedCall(garageTutorialOpenDelay, () =>
        {
            if (_saveData == null || _saveData.garageButtonClicked || _saveData.fifteenDismissed) return;
            if (fourteenInputBlocker != null) fourteenInputBlocker.SetActive(true);
            Sequence open = DOTween.Sequence();
            open.SetUpdate(true);
            open.Join(ShowStepFourteenVisual());
            open.OnComplete(() => StartStepFourteenLoopAnimation());
            RegisterBtnGarageListener();
            Debug.Log("[TutorialMgr][GarageReveal] Fourteen shown");
            Debug.Log("[TutorialMgr][GarageTut] Fourteen shown");
        }, true).SetUpdate(true);
    }

    private void ApplyGarageTutorialStateImmediate()
    {
        if (_saveData == null) return;

        Debug.Log($"[TutorialMgr][GarageTut] ApplyGarageTutorialStateImmediate: garageButtonClicked={_saveData.garageButtonClicked} fifteenDismissed={_saveData.fifteenDismissed}");

        // Btn_Garage must be visible immediately (no animation) on reload.
        ShowBtnGarageImmediate();

        // Reset transient visuals.
        StopStepFourteenLoopAnimation(restoreTransform: true);
        SetStepFourteenVisibleImmediate(false);
        if (fourteenInputBlocker != null) fourteenInputBlocker.SetActive(false);

        // If the player has already clicked Btn_Garage, the segment is in the
        // NewGarage-scene phase (handled by GarageTutorialController). Nothing
        // else to do in Main.
        if (_saveData.garageButtonClicked)
        {
            Debug.Log("[TutorialMgr][GarageTut] Branch=Step22 (awaiting Fifteen dismiss in NewGarage).");
            return;
        }

        // Step 20 — re-show Fourteen pointer + blocker; re-arm Btn_Garage listener.
        Debug.Log("[TutorialMgr][GarageTut] Branch=Step20 (re-show Fourteen pointer + blocker).");
        if (fourteenInputBlocker != null) fourteenInputBlocker.SetActive(true);
        SetStepFourteenVisibleImmediate(true);
        StartStepFourteenLoopAnimation();
        RegisterBtnGarageListener();
    }

    private void RegisterBtnGarageListener()
    {
        if (_isGarageButtonListenerRegistered || btnGarage == null) return;
        btnGarage.onClick.AddListener(HandleBtnGarageClickedForStepTwentyOne);
        _isGarageButtonListenerRegistered = true;
        Debug.Log("[TutorialMgr][GarageTut] RegisterBtnGarageListener: listener attached.");
    }

    private void UnregisterBtnGarageListener()
    {
        if (!_isGarageButtonListenerRegistered || btnGarage == null) return;
        btnGarage.onClick.RemoveListener(HandleBtnGarageClickedForStepTwentyOne);
        _isGarageButtonListenerRegistered = false;
    }

    private void HandleBtnGarageClickedForStepTwentyOne()
    {
        if (_saveData == null) return;
        if (_saveData.garageButtonClicked) return;

        Debug.Log("[TutorialMgr][GarageTut] HandleBtnGarageClickedForStepTwentyOne: Btn_Garage clicked → persist garageButtonClicked + smooth Fourteen hide.");

        _saveData.garageButtonClicked = true;
        _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 21);
        _saveData.Save();

        UnregisterBtnGarageListener();
        StopStepFourteenLoopAnimation(restoreTransform: true);
        if (fourteenInputBlocker != null) fourteenInputBlocker.SetActive(false);

        Sequence close = DOTween.Sequence();
        close.SetUpdate(true);
        close.Join(HideStepFourteenVisual());
        // The existing GarageSceneLoader.LoadGarageScene UnityEvent persistent
        // listener on Btn_Garage handles the actual scene swap; we just clean
        // up the tutorial UI here.
    }

    // ── Btn_Garage reveal (mirrors PopularityBar pattern) ──

    private void CacheBtnGarageOriginalTransform()
    {
        if (_btnGarageRectTransform == null) return;
        if (_btnGarageOriginalTransformCached) return;
        _btnGarageOriginalScale = _btnGarageRectTransform.localScale;
        _btnGarageOriginalTransformCached = true;
    }

    /// <summary>
    /// Authoritative Btn_Garage visibility gate. Forces btnGarageRoot inactive
    /// any time the police tutorial has not fully completed (i.e. chase still
    /// active OR awaiting OnChaseEnded). Never reveals on its own — that path
    /// goes through ShowBtnGarageImmediate / ShowBtnGarageAnimated.
    /// </summary>
    private void ApplyBtnGarageGateVisibility(string callSite)
    {
        if (btnGarageRoot == null) return;
        if (_saveData == null) return;

        bool chaseActive = PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive;
        bool policeDone = _saveData.thirteenDismissed && !_isAwaitingGarageTutorialAfterChase && !chaseActive;
        bool garageUnlocked = _saveData.garageTutorialQueued || _saveData.fifteenDismissed;
        bool activeBefore = btnGarageRoot.activeSelf;
        bool shouldShow = garageUnlocked && policeDone;

        // Gate only force-hides. Reveal is owned by ShowBtnGarageImmediate /
        // ShowBtnGarageAnimated, which are called from EnterGarageTutorial
        // (post-OnChaseEnded) or HandleGameLoaded routing.
        if (!shouldShow && activeBefore)
        {
            Debug.Log($"[TutorialMgr][GarageDebug] StepIndex={_saveData.currentStepIndex}, policeDone={policeDone}, radarDone={_saveData.radarUnlocked}, garageUnlocked={garageUnlocked}");
            Debug.Log($"[TutorialMgr][GarageDebug] Btn_Garage activeBefore={activeBefore} activeAfter=False (callSite={callSite})");
            Debug.Log($"[TutorialMgr][GarageTut] Btn_Garage visibility check ({callSite}): stepIdx={_saveData.currentStepIndex} policeDone={policeDone} garageUnlocked={garageUnlocked} activeBefore={activeBefore} activeAfter=False");
            Debug.Log($"[TutorialMgr][GarageTut] Forcing Btn_Garage hidden before police completion (callSite={callSite})");
            Debug.Log("[TutorialMgr][GarageDebug] Setting Btn_Garage INACTIVE (gate force-hide)");
            btnGarageRoot.SetActive(false);
            return;
        }

        // No-op log so reveals are visible in the timeline.
        Debug.Log($"[TutorialMgr][GarageTut] Btn_Garage visibility check ({callSite}): stepIdx={_saveData.currentStepIndex} policeDone={policeDone} garageUnlocked={garageUnlocked} activeBefore={activeBefore} activeAfter={activeBefore}");
    }

    private void ShowBtnGarageImmediate()
    {
        if (btnGarageRoot == null)
        {
            Debug.LogError("[TutorialMgr][GarageTut] ShowBtnGarageImmediate: btnGarageRoot NULL — assign Inspector field. Aborting.");
            return;
        }
        CacheBtnGarageOriginalTransform();

        Debug.Log("[TutorialMgr][GarageTut] Revealing Btn_Garage after police completion (immediate)");
        Debug.Log("[TutorialMgr][GarageDebug] Setting Btn_Garage ACTIVE (ShowBtnGarageImmediate)\n" + System.Environment.StackTrace);
        btnGarageRoot.SetActive(true);
        if (_btnGarageRectTransform != null)
            _btnGarageRectTransform.localScale = _btnGarageOriginalScale;
        if (_btnGarageCanvasGroup != null)
        {
            _btnGarageCanvasGroup.alpha = 1f;
            _btnGarageCanvasGroup.interactable = true;
            _btnGarageCanvasGroup.blocksRaycasts = true;
        }

        // Hand Btn_Garage ownership back to TopBarAnimator. From now on it is a
        // normal TopBar hideGroup member (compact/expand cycles include it).
        TutorialGate.SetBtnGarageOwnedByTutorial(false);
        Debug.Log("[TutorialMgr][GarageReveal] Btn_Garage ownership released to TopBarAnimator (immediate path)");
    }

    private void ShowBtnGarageAnimated()
    {
        if (btnGarageRoot == null)
        {
            Debug.LogError("[TutorialMgr][GarageTut] ShowBtnGarageAnimated: btnGarageRoot NULL — assign Inspector field 'Btn Garage Root'. Aborting.");
            return;
        }
        CacheBtnGarageOriginalTransform();

        Debug.Log("[TutorialMgr][GarageReveal] Starting dedicated Btn_Garage reveal");
        Debug.Log("[TutorialMgr][GarageTut] Revealing Btn_Garage after police completion (animated)");
        Debug.Log("[TutorialMgr][GarageDebug] ShowBtnGarageAnimated final reveal");
        Debug.Log("[TutorialMgr][GarageDebug] Setting Btn_Garage ACTIVE (ShowBtnGarageAnimated)\n" + System.Environment.StackTrace);
        btnGarageRoot.SetActive(true);

        if (_btnGarageCanvasGroup != null)
        {
            _btnGarageCanvasGroup.alpha = 0f;
            _btnGarageCanvasGroup.interactable = false;
            _btnGarageCanvasGroup.blocksRaycasts = false;
        }

        if (_btnGarageRectTransform != null)
            _btnGarageRectTransform.localScale = _btnGarageOriginalScale * premiumStartScale;

        Sequence intro = DOTween.Sequence();
        intro.SetUpdate(true);
        if (_btnGarageRectTransform != null)
            intro.Join(_btnGarageRectTransform.DOScale(_btnGarageOriginalScale, premiumIntroDuration).SetEase(premiumIntroEase));
        if (_btnGarageCanvasGroup != null)
            intro.Join(_btnGarageCanvasGroup.DOFade(1f, premiumIntroDuration));

        intro.OnComplete(() =>
        {
            if (_btnGarageCanvasGroup != null)
            {
                _btnGarageCanvasGroup.interactable = true;
                _btnGarageCanvasGroup.blocksRaycasts = true;
            }
            // Hand Btn_Garage ownership back to TopBarAnimator. From now on it is a
            // normal TopBar hideGroup member (compact/expand cycles include it).
            TutorialGate.SetBtnGarageOwnedByTutorial(false);
            Debug.Log("[TutorialMgr][GarageReveal] Btn_Garage reveal complete");
            Debug.Log("[TutorialMgr][GarageTut] ShowBtnGarageAnimated COMPLETE.");
        });
    }

    // ── Fourteen pointer (mirrors Twelve pattern) ──

    private void CacheStepFourteenOriginalTransform()
    {
        if (_fourteenRectTransform == null)
            _fourteenRectTransform = fourteen != null ? fourteen.GetComponent<RectTransform>() : null;
        if (_fourteenRectTransform == null) return;
        if (_fourteenOriginalTransformCached) return;
        _fourteenOriginalScale = _fourteenRectTransform.localScale;
        _fourteenOriginalAnchoredPosition = _fourteenRectTransform.anchoredPosition;
        _fourteenOriginalTransformCached = true;
    }

    private void RestoreStepFourteenOriginalTransform()
    {
        if (_fourteenRectTransform == null) return;
        if (!_fourteenOriginalTransformCached) CacheStepFourteenOriginalTransform();
        _fourteenRectTransform.localScale = _fourteenOriginalScale;
        _fourteenRectTransform.anchoredPosition = _fourteenOriginalAnchoredPosition;
    }

    private void SetStepFourteenVisibleImmediate(bool visible)
    {
        if (fourteen == null) return;
        if (_fourteenCanvasGroup == null) _fourteenCanvasGroup = GetOrAddCanvasGroup(fourteen);
        CacheStepFourteenOriginalTransform();

        if (visible)
        {
            fourteen.SetActive(true);
            RestoreStepFourteenOriginalTransform();
            if (_fourteenCanvasGroup != null)
            {
                _fourteenCanvasGroup.alpha = 1f;
                _fourteenCanvasGroup.interactable = false;
                _fourteenCanvasGroup.blocksRaycasts = false;
            }
        }
        else
        {
            if (_fourteenCanvasGroup != null)
            {
                _fourteenCanvasGroup.alpha = 0f;
                _fourteenCanvasGroup.interactable = false;
                _fourteenCanvasGroup.blocksRaycasts = false;
            }
            fourteen.SetActive(false);
        }
    }

    private Tween ShowStepFourteenVisual()
    {
        if (fourteen == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_fourteenCanvasGroup == null) _fourteenCanvasGroup = GetOrAddCanvasGroup(fourteen);
        CacheStepFourteenOriginalTransform();

        fourteen.SetActive(true);
        if (_fourteenCanvasGroup != null)
        {
            _fourteenCanvasGroup.alpha = 0f;
            _fourteenCanvasGroup.interactable = false;
            _fourteenCanvasGroup.blocksRaycasts = false;
        }
        Image fourteenImage = fourteen.GetComponent<Image>();
        if (fourteenImage != null) fourteenImage.raycastTarget = false;

        if (_fourteenRectTransform != null)
            _fourteenRectTransform.localScale = _fourteenOriginalScale * panelStartScale;

        Sequence show = DOTween.Sequence();
        show.SetUpdate(true);
        if (_fourteenRectTransform != null)
            show.Join(_fourteenRectTransform.DOScale(_fourteenOriginalScale, panelInDuration).SetEase(easeIn));
        if (_fourteenCanvasGroup != null)
            show.Join(_fourteenCanvasGroup.DOFade(1f, panelInDuration));
        return show;
    }

    private Tween HideStepFourteenVisual()
    {
        if (fourteen == null)
            return DOVirtual.DelayedCall(0f, () => { }).SetUpdate(true);
        if (_fourteenCanvasGroup == null) _fourteenCanvasGroup = GetOrAddCanvasGroup(fourteen);
        if (_fourteenRectTransform == null) _fourteenRectTransform = fourteen.GetComponent<RectTransform>();
        CacheStepFourteenOriginalTransform();

        Sequence hide = DOTween.Sequence();
        hide.SetUpdate(true);
        if (_fourteenRectTransform != null)
            hide.Join(_fourteenRectTransform.DOScale(_fourteenOriginalScale * panelStartScale, panelOutDuration).SetEase(easeOut));
        if (_fourteenCanvasGroup != null)
            hide.Join(_fourteenCanvasGroup.DOFade(0f, panelOutDuration));
        hide.OnComplete(() =>
        {
            RestoreStepFourteenOriginalTransform();
            fourteen.SetActive(false);
        });
        return hide;
    }

    private void StartStepFourteenLoopAnimation()
    {
        StopStepFourteenLoopAnimation(restoreTransform: true);
        if (fourteen == null || !fourteen.activeInHierarchy) return;
        if (_fourteenRectTransform == null) _fourteenRectTransform = fourteen.GetComponent<RectTransform>();
        if (_fourteenRectTransform == null) return;

        CacheStepFourteenOriginalTransform();
        RestoreStepFourteenOriginalTransform();

        Vector3 pulseScale = _fourteenOriginalScale * (1f + stepTwoPulseScalePercent);
        Vector2 bouncePos = _fourteenOriginalAnchoredPosition + Vector2.up * stepTwoBounceDistance;

        _stepFourteenLoopSequence = DOTween.Sequence();
        _stepFourteenLoopSequence.SetUpdate(true);
        _stepFourteenLoopSequence.Join(_fourteenRectTransform.DOScale(pulseScale, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepFourteenLoopSequence.Join(_fourteenRectTransform.DOAnchorPos(bouncePos, stepTwoLoopDuration).SetEase(Ease.InOutSine));
        _stepFourteenLoopSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopStepFourteenLoopAnimation(bool restoreTransform)
    {
        if (_stepFourteenLoopSequence != null)
        {
            _stepFourteenLoopSequence.Kill();
            _stepFourteenLoopSequence = null;
        }
        if (restoreTransform)
            RestoreStepFourteenOriginalTransform();
    }
}
