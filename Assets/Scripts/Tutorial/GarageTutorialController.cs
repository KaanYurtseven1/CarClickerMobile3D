using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Step 22 of the tutorial — runs in the NewGarage scene only.
///
/// Triggered when the player has clicked Btn_Garage during Step 21
/// (<c>TutorialSaveData.garageButtonClicked</c> = true) and the segment has
/// not yet completed (<c>TutorialSaveData.fifteenDismissed</c> = false).
///
/// On enter:
///   1. Fade in <see cref="dim"/> (full-screen darken).
///   2. Scale-in <see cref="fifteen"/> (intro dialog) with the same
///      panelStartScale / OutBack ease used by Eleven/Thirteen.
///
/// On any pointer tap (after a one-frame arm coroutine to avoid consuming the
/// click that loaded this scene):
///   1. Scale-out Fifteen + fade Dim out.
///   2. Persist <c>fifteenDismissed = true</c> + <c>currentStepIndex &gt;= 22</c>.
///
/// Save data is loaded directly via <see cref="TutorialSaveData.Load"/> /
/// <see cref="TutorialSaveData.Save"/>, mirroring the cross-scene PlayerPrefs
/// pattern used by ChestOpenScene → Main.
/// </summary>
[DisallowMultipleComponent]
public class GarageTutorialController : MonoBehaviour
{
    [Header("Refs (NewGarage Canvas)")]
    [Tooltip("Full-screen darken Image (CanvasGroup added automatically). Sibling-index above gameplay UI but below Fifteen.")]
    [SerializeField] private GameObject dim;
    [Tooltip("UI_Tutorial/Fifteen dialog GameObject (scale-in popup). Should already have CanvasGroup or one will be added.")]
    [SerializeField] private GameObject fifteen;

    [Header("Timing & Easing")]
    [Tooltip("Delay between scene-load and Dim+Fifteen intro.")]
    [SerializeField] private float popupOpenDelay = 0.3f;
    [Tooltip("Intro duration (Fifteen scale + Dim fade).")]
    [SerializeField] private float introDuration = 0.35f;
    [Tooltip("Outro duration on dismiss.")]
    [SerializeField] private float outroDuration = 0.2f;
    [Tooltip("Initial scale multiplier for Fifteen during intro (matches TutorialManager.panelStartScale).")]
    [SerializeField] private float startScale = 0.9f;
    [SerializeField] private Ease easeIn = Ease.OutBack;
    [SerializeField] private Ease easeOut = Ease.InBack;
    [Tooltip("Maximum alpha for Dim during intro.")]
    [SerializeField, Range(0f, 1f)] private float dimMaxAlpha = 0.7f;

    private TutorialSaveData _saveData;
    private CanvasGroup _dimCanvasGroup;
    private CanvasGroup _fifteenCanvasGroup;
    private RectTransform _fifteenRectTransform;
    private Vector3 _fifteenOriginalScale = Vector3.one;
    private bool _fifteenOriginalScaleCached;

    private bool _isActive;
    private bool _canDismiss;
    private bool _isDismissInProgress;
    private Sequence _activeSequence;

    private void Awake()
    {
        _saveData = TutorialSaveData.Load();
        if (_saveData == null)
        {
            Debug.LogWarning("[GarageTutCtl] Awake: TutorialSaveData.Load() returned null — disabling.");
            DeactivateAll();
            enabled = false;
            return;
        }

        // Eligibility: only show Fifteen if Btn_Garage was clicked but Fifteen
        // not yet dismissed.
        if (!_saveData.garageButtonClicked || _saveData.fifteenDismissed)
        {
            Debug.Log($"[GarageTutCtl] Awake: skipping Step 22. garageButtonClicked={_saveData.garageButtonClicked} fifteenDismissed={_saveData.fifteenDismissed}");
            DeactivateAll();
            enabled = false;
            return;
        }

        Debug.Log("[GarageTutCtl] Awake: eligible for Step 22 — caching refs.");

        if (dim == null)
            Debug.LogError("[GarageTutCtl] dim NULL — assign Inspector field 'Dim' to NewGarage Canvas/UI_Tutorial/Dim.");
        if (fifteen == null)
            Debug.LogError("[GarageTutCtl] fifteen NULL — assign Inspector field 'Fifteen' to NewGarage Canvas/UI_Tutorial/Fifteen.");

        if (dim != null)
        {
            _dimCanvasGroup = dim.GetComponent<CanvasGroup>();
            if (_dimCanvasGroup == null) _dimCanvasGroup = dim.AddComponent<CanvasGroup>();
            _dimCanvasGroup.alpha = 0f;
            _dimCanvasGroup.interactable = false;
            _dimCanvasGroup.blocksRaycasts = false;
            dim.SetActive(false);
        }

        if (fifteen != null)
        {
            _fifteenCanvasGroup = fifteen.GetComponent<CanvasGroup>();
            if (_fifteenCanvasGroup == null) _fifteenCanvasGroup = fifteen.AddComponent<CanvasGroup>();
            _fifteenRectTransform = fifteen.GetComponent<RectTransform>();
            if (_fifteenRectTransform != null && !_fifteenOriginalScaleCached)
            {
                _fifteenOriginalScale = _fifteenRectTransform.localScale;
                _fifteenOriginalScaleCached = true;
            }
            _fifteenCanvasGroup.alpha = 0f;
            _fifteenCanvasGroup.interactable = false;
            _fifteenCanvasGroup.blocksRaycasts = false;
            // Keep raycast off the dialog body itself; tap-anywhere is global.
            Image fifteenImage = fifteen.GetComponent<Image>();
            if (fifteenImage != null) fifteenImage.raycastTarget = false;
            fifteen.SetActive(false);
        }
    }

    private void Start()
    {
        if (!enabled) return;
        DOVirtual.DelayedCall(popupOpenDelay, OpenFifteen, true).SetUpdate(true);
    }

    private void OpenFifteen()
    {
        if (_saveData == null || _saveData.fifteenDismissed) return;
        if (_isActive) return;

        _isActive = true;
        _canDismiss = false;

        if (dim != null)
        {
            dim.SetActive(true);
            if (_dimCanvasGroup != null)
            {
                _dimCanvasGroup.alpha = 0f;
                _dimCanvasGroup.interactable = false;
                _dimCanvasGroup.blocksRaycasts = true;
            }
        }

        if (fifteen != null)
        {
            fifteen.SetActive(true);
            if (_fifteenRectTransform != null && _fifteenOriginalScaleCached)
                _fifteenRectTransform.localScale = _fifteenOriginalScale * startScale;
            if (_fifteenCanvasGroup != null)
            {
                _fifteenCanvasGroup.alpha = 0f;
                _fifteenCanvasGroup.interactable = false;
                _fifteenCanvasGroup.blocksRaycasts = false;
            }
        }

        Sequence intro = DOTween.Sequence();
        intro.SetUpdate(true);
        if (_dimCanvasGroup != null)
            intro.Join(_dimCanvasGroup.DOFade(dimMaxAlpha, introDuration));
        if (_fifteenRectTransform != null && _fifteenOriginalScaleCached)
            intro.Join(_fifteenRectTransform.DOScale(_fifteenOriginalScale, introDuration).SetEase(easeIn));
        if (_fifteenCanvasGroup != null)
            intro.Join(_fifteenCanvasGroup.DOFade(1f, introDuration));
        intro.OnComplete(() =>
        {
            StartCoroutine(ArmDismissNextFrame());
        });
        _activeSequence = intro;
        Debug.Log("[GarageTutCtl] OpenFifteen: intro started.");
    }

    private System.Collections.IEnumerator ArmDismissNextFrame()
    {
        // Wait one frame so the click that triggered scene load doesn't dismiss
        // immediately.
        yield return null;
        _canDismiss = true;
        Debug.Log("[GarageTutCtl] ArmDismissNextFrame: dismiss armed.");
    }

    private void Update()
    {
        if (!_isActive || !_canDismiss || _isDismissInProgress) return;
        if (WasAnyPointerPressedThisFrame())
            CompleteStepTwentyTwo();
    }

    private void CompleteStepTwentyTwo()
    {
        if (_isDismissInProgress) return;
        _isDismissInProgress = true;
        _canDismiss = false;

        if (_activeSequence != null && _activeSequence.IsActive())
            _activeSequence.Kill();

        Sequence outro = DOTween.Sequence();
        outro.SetUpdate(true);
        if (_dimCanvasGroup != null)
            outro.Join(_dimCanvasGroup.DOFade(0f, outroDuration));
        if (_fifteenRectTransform != null && _fifteenOriginalScaleCached)
            outro.Join(_fifteenRectTransform.DOScale(_fifteenOriginalScale * startScale, outroDuration).SetEase(easeOut));
        if (_fifteenCanvasGroup != null)
            outro.Join(_fifteenCanvasGroup.DOFade(0f, outroDuration));
        outro.OnComplete(() =>
        {
            DeactivateAll();

            if (_saveData != null)
            {
                _saveData.fifteenDismissed = true;
                _saveData.currentStepIndex = Mathf.Max(_saveData.currentStepIndex, 22);
                _saveData.Save();
                Debug.Log("[GarageTutCtl] CompleteStepTwentyTwo: fifteenDismissed=TRUE persisted (segment complete).");
            }

            _isActive = false;
            _isDismissInProgress = false;
            _activeSequence = null;
            enabled = false;
        });
        _activeSequence = outro;
    }

    private void DeactivateAll()
    {
        if (dim != null) dim.SetActive(false);
        if (fifteen != null) fifteen.SetActive(false);
    }

    private void OnDisable()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
            _activeSequence = null;
        }
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
}
