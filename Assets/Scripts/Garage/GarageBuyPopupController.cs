// ════════════════════════════════════════════════════════════════
// GarageBuyPopupController.cs – Confirmation popup for garage
// purchases.  Replaces the old instant-buy flow.
//
// Scene hierarchy (BuyPopupPanel):
//   Image ← popup panel visual (popupRect)
//     Title
//     FiyatPart / GoldPart / GoldText
//     FiyatPart / NitroPart / NitroText
//     Btn_Yes   → confirms purchase, spends currency
//     Btn_Incele → preview item + enter inspect mode
//     CloseButton → cancel, revert preview
//
// Inspector wiring (on BuyPopupSystem GameObject):
//   popupPanel       → BuyPopupPanel (the full-screen overlay GO)
//   popupRect        → BuyPopupPanel/Image (the panel that scales)
//   titleText        → Title TMP
//   goldCostText     → GoldText TMP
//   nitroCostText    → NitroText TMP
//   btnYes           → Btn_Yes Button
//   btnIncele        → Btn_Incele Button
//   closeButton      → CloseButton Button
//   garageController → GarageController
//   focusController  → GarageFocusController
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class GarageBuyPopupController : MonoBehaviour
{
    public static GarageBuyPopupController Instance { get; private set; }

    // ─── Inspector (field names match scene serialisation) ───
    [Header("─── UI References ───")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private RectTransform popupRect;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text goldCostText;
    [SerializeField] private TMP_Text nitroCostText;
    [SerializeField] private Button btnYes;
    [SerializeField] private Button btnIncele;
    [SerializeField] private Button closeButton;

    [Header("─── Scene References ───")]
    [SerializeField] private GarageController garageController;
    [SerializeField] private GarageFocusController focusController;

    [Header("─── Animation ───")]
    [SerializeField] private float openDuration = 0.3f;
    [SerializeField] private float closeDuration = 0.2f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    // ─── Pending purchase tracking ───
    private enum PendingType { None, Color, Sticker, Part }
    private PendingType _pendingType;
    private int _pendingIndex;
    private string _pendingPartKey;
    private double _pendingGoldCost;
    private int _pendingNitroCost;

    // ─── Preview / inspect state ───
    private bool _isPreviewActive;
    private bool _waitingForInspectExit;
    private bool _isAnimating;
    private Tween _activeTween;

    private const string POPUP_TITLE = "Sat\u0131n almak istedi\u011finize\n emin misiniz?";

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (popupPanel != null) popupPanel.SetActive(false);

        if (btnYes != null) btnYes.onClick.AddListener(OnBtnYes);
        if (btnIncele != null) btnIncele.onClick.AddListener(OnBtnIncele);
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseButton);

        if (focusController != null)
            focusController.OnFocusExited += OnFocusExited;
    }

    private void OnDestroy()
    {
        _activeTween?.Kill();
        if (focusController != null)
            focusController.OnFocusExited -= OnFocusExited;
        if (Instance == this) Instance = null;
    }

    // ══════════════════ Public Show Methods ══════════════════

    /// <summary>Open popup for a color purchase (Nitro Coin).</summary>
    public void ShowForColor(int colorIndex, int nitroCost)
    {
        _pendingType = PendingType.Color;
        _pendingIndex = colorIndex;
        _pendingNitroCost = nitroCost;
        _pendingGoldCost = 0;
        _pendingPartKey = null;
        OpenPopup();
    }

    /// <summary>Open popup for a sticker purchase (Nitro Coin).</summary>
    public void ShowForSticker(int stickerIndex, int nitroCost)
    {
        _pendingType = PendingType.Sticker;
        _pendingIndex = stickerIndex;
        _pendingNitroCost = nitroCost;
        _pendingGoldCost = 0;
        _pendingPartKey = null;
        OpenPopup();
    }

    /// <summary>Open popup for a mod-part purchase (Gold).</summary>
    public void ShowForPart(string partKey, double goldCost)
    {
        _pendingType = PendingType.Part;
        _pendingPartKey = partKey;
        _pendingGoldCost = goldCost;
        _pendingNitroCost = 0;
        _pendingIndex = -1;
        OpenPopup();
    }

    /// <summary>True while the popup panel is visible.</summary>
    public bool IsOpen => popupPanel != null && popupPanel.activeSelf;

    // ══════════════════ Popup Open / Close ══════════════════

    private void OpenPopup()
    {
        if (popupPanel == null) return;

        // Title
        if (titleText != null) titleText.text = POPUP_TITLE;

        // Costs
        if (goldCostText != null)
            goldCostText.text = _pendingGoldCost > 0 ? FormatCost(_pendingGoldCost) : "0";
        if (nitroCostText != null)
            nitroCostText.text = _pendingNitroCost > 0 ? _pendingNitroCost.ToString("N0") : "0";

        // Btn_Yes affordability: disable + fade when player cannot afford
        UpdateBtnYesAffordability();

        // Activate
        popupPanel.SetActive(true);

        // Scale-in animation
        _activeTween?.Kill();
        if (popupRect != null)
        {
            popupRect.localScale = Vector3.zero;
            _activeTween = popupRect.DOScale(Vector3.one, openDuration)
                .SetEase(openEase)
                .SetUpdate(true);
        }
    }

    private void ClosePopup(System.Action onComplete = null)
    {
        if (popupPanel == null || !popupPanel.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        _activeTween?.Kill();
        if (popupRect != null)
        {
            _activeTween = popupRect.DOScale(Vector3.zero, closeDuration)
                .SetEase(closeEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    popupPanel.SetActive(false);
                    onComplete?.Invoke();
                });
        }
        else
        {
            popupPanel.SetActive(false);
            onComplete?.Invoke();
        }
    }

    // ══════════════════ Button Handlers ══════════════════

    // ── Btn_Yes: confirm purchase ──
    private void OnBtnYes()
    {
        if (_isAnimating || _pendingType == PendingType.None) return;
        if (!CanAffordPending()) return;   // safety guard
        _isAnimating = true;
        _waitingForInspectExit = false;

        // If we were in preview, that visual is already on the car —
        // Finalize will re-apply properly via SetColor/SetSticker/TogglePart
        _isPreviewActive = false;

        ClosePopup(() =>
        {
            _isAnimating = false;

            if (garageController != null)
            {
                switch (_pendingType)
                {
                    case PendingType.Color:
                        garageController.FinalizeColorPurchase(_pendingIndex);
                        break;
                    case PendingType.Sticker:
                        garageController.FinalizeStickerPurchase(_pendingIndex);
                        break;
                    case PendingType.Part:
                        garageController.FinalizePartPurchase(_pendingPartKey);
                        break;
                }
            }

            ClearPending();
        });
    }

    // ── Btn_Incele: preview + inspect ──
    private void OnBtnIncele()
    {
        if (_isAnimating || _pendingType == PendingType.None) return;
        _isAnimating = true;

        ClosePopup(() =>
        {
            _isAnimating = false;

            // Apply temporary visual preview
            ApplyPreview();
            _isPreviewActive = true;
            _waitingForInspectExit = true;

            // Automatically enter focus/inspect mode
            if (focusController != null && !focusController.IsFocusMode)
                focusController.RequestEnterFocus();
        });
    }

    // ── CloseButton: cancel everything ──
    private void OnCloseButton()
    {
        if (_isAnimating) return;
        _isAnimating = true;

        _waitingForInspectExit = false;

        // Revert preview if it was active
        if (_isPreviewActive && garageController != null)
        {
            garageController.RevertPreview();
            _isPreviewActive = false;
        }

        // Revert selection UI (color scale / sticker highlight) back to real state
        if (garageController != null)
            garageController.RevertSelectionUI();

        ClosePopup(() =>
        {
            _isAnimating = false;
            ClearPending();
        });
    }

    // ══════════════════ Focus-Mode Integration ══════════════════

    /// <summary>
    /// Called by GarageFocusController.OnFocusExited event.
    /// When the player exits inspect mode after Btn_Incele,
    /// the preview is reverted and the popup re-opens.
    /// </summary>
    private void OnFocusExited()
    {
        if (!_waitingForInspectExit) return;
        _waitingForInspectExit = false;

        // Revert temporary preview
        if (_isPreviewActive && garageController != null)
        {
            garageController.RevertPreview();
            _isPreviewActive = false;
        }

        // Revert selection UI back to real committed state before re-showing popup
        if (garageController != null)
            garageController.RevertSelectionUI();

        // Re-show the popup so the player can decide
        OpenPopup();

        // Re-animate selection UI to the clicked item (visible behind popup)
        AnimateClickedSelection();
    }

    // ══════════════════ Preview Helpers ══════════════════

    private void ApplyPreview()
    {
        if (garageController == null) return;

        switch (_pendingType)
        {
            case PendingType.Color:
                garageController.PreviewColor(_pendingIndex);
                break;
            case PendingType.Sticker:
                garageController.PreviewSticker(_pendingIndex);
                break;
            case PendingType.Part:
                garageController.PreviewPart(_pendingPartKey);
                break;
        }
    }

    private void ClearPending()
    {
        _pendingType = PendingType.None;
        _pendingIndex = -1;
        _pendingPartKey = null;
        _pendingGoldCost = 0;
        _pendingNitroCost = 0;
        _isPreviewActive = false;
    }

    /// <summary>
    /// Re-animates the selection UI to the clicked (pending) item.
    /// Used when the popup re-opens after inspect mode so the user sees
    /// the clicked item highlighted behind the popup overlay.
    /// </summary>
    private void AnimateClickedSelection()
    {
        if (garageController == null) return;

        switch (_pendingType)
        {
            case PendingType.Color:
                garageController.AnimateColorSelection(_pendingIndex);
                break;
            case PendingType.Sticker:
                garageController.AnimateStickerSelection(_pendingIndex);
                break;
        }
    }

    // ══════════════════ Btn_Yes Affordability ══════════════════

    /// <summary>Returns true if the player can afford the pending item.</summary>
    private bool CanAffordPending()
    {
        if (_pendingGoldCost > 0)
            return CurrencyManager.Instance != null && CurrencyManager.Instance.money >= _pendingGoldCost;
        if (_pendingNitroCost > 0)
            return CurrencyManager.Instance != null && CurrencyManager.Instance.nitroCoins >= _pendingNitroCost;
        return true;
    }

    /// <summary>Sets Btn_Yes interactable + visual alpha based on current affordability.</summary>
    private void UpdateBtnYesAffordability()
    {
        if (btnYes == null) return;

        bool canAfford = CanAffordPending();
        btnYes.interactable = canAfford;

        // Fade the button image when unaffordable
        Image btnImage = btnYes.GetComponent<Image>();
        if (btnImage != null)
        {
            Color c = btnImage.color;
            c.a = canAfford ? 1f : 0.35f;
            btnImage.color = c;
        }
    }

    // ══════════════════ Format ══════════════════

    private static string FormatCost(double value)
    {
        if (value < 1000) return value.ToString("F0");
        return value.ToString("N0");
    }
}
