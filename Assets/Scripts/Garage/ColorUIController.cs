// ════════════════════════════════════════════════════════════════
// ColorUIController.cs – Manages the 6 color-choose buttons.
//
// Inspector wiring:
//   colorButtonsParent → Canvas/ColorChooseButtons
// ════════════════════════════════════════════════════════════════
using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ColorUIController : MonoBehaviour
{
    // ─── Serialized ───
    [Header("References")]
    [SerializeField] private Transform colorButtonsParent;

    [Header("Selection Visual")]
    [Tooltip("Scale applied to the currently selected button.")]
    [SerializeField] private float selectedScale = 1.25f;
    [Tooltip("Scale applied to non-selected buttons.")]
    [SerializeField] private float normalScale = 1.0f;

    [Header("Animation")]
    [Tooltip("Duration (seconds) for the scale tween.")]
    [SerializeField] private float tweenDuration = 0.22f;
    [Tooltip("Ease curve for the scale tween.")]
    [SerializeField] private Ease tweenEase = Ease.OutBack;

    // ─── Callback (set by GarageController) ───
    [NonSerialized] public Action<int> onColorSelected;

    // ─── Cached ───
    private readonly Button[] _buttons = new Button[6];
    private readonly Image[] _colorImages = new Image[6];
    private readonly RectTransform[] _buttonRects = new RectTransform[6];

    // First Refresh applies instantly (no animation on scene load)
    private bool _firstRefreshDone;

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        ResolveButtons();
    }

    // ══════════════════ Button Resolution ══════════════════

    private void ResolveButtons()
    {
        if (colorButtonsParent == null)
        {
            Debug.LogError("[ColorUIController] colorButtonsParent is not assigned.");
            return;
        }

        for (int i = 0; i < 6; i++)
        {
            // Naming convention:  "ColorChoose_Button", "ColorChoose_Button (1)" … "ColorChoose_Button (5)"
            string btnName = (i == 0) ? "ColorChoose_Button" : $"ColorChoose_Button ({i})";

            Transform btnT = colorButtonsParent.Find(btnName);
            if (btnT == null)
            {
                Debug.LogError($"[ColorUIController] '{btnName}' not found under '{colorButtonsParent.name}'.");
                continue;
            }

            _buttons[i] = btnT.GetComponent<Button>();
            _buttonRects[i] = btnT.GetComponent<RectTransform>();

            // Each button has a child Image named "Color"
            Transform colorChild = btnT.Find("Color");
            if (colorChild != null)
                _colorImages[i] = colorChild.GetComponent<Image>();
            else
                Debug.LogWarning($"[ColorUIController] 'Color' child not found under '{btnName}'.");

            int captured = i;
            if (_buttons[i] != null)
                _buttons[i].onClick.AddListener(() => OnButtonClicked(captured));
        }
    }

    private void OnButtonClicked(int index)
    {
        onColorSelected?.Invoke(index);
    }

    // ══════════════════ Public API ══════════════════

    /// <summary>Sets the affordability visual (dim/bright) for a specific color button.</summary>
    public void SetButtonAffordable(int index, bool canAfford, bool owned)
    {
        if (index < 0 || index >= 6 || _buttonRects[index] == null) return;
        // Use CanvasGroup if present, otherwise dim the button image
        Image img = _buttonRects[index].GetComponent<Image>();
        if (img != null)
        {
            Color c = img.color;
            c.a = canAfford ? 1f : 0.35f;
            img.color = c;
        }
    }

    /// <summary>Shake feedback when player can't afford this color.</summary>
    public void ShakeButton(int index)
    {
        if (index < 0 || index >= 6 || _buttonRects[index] == null) return;
        GarageAffordabilityHelper.ShakeButton(_buttonRects[index]);
    }

    /// <summary>
    /// Updates the 6 color swatches and applies a selection visual (scale).
    /// First call applies instantly; subsequent calls animate via DOTween.
    /// </summary>
    public void Refresh(CarDataSO data, int selectedColorIndex)
    {
        if (data == null) return;

        bool animate = _firstRefreshDone;
        _firstRefreshDone = true;

        for (int i = 0; i < 6; i++)
        {
            // Set swatch color
            if (i < data.colors.Count && _colorImages[i] != null)
                _colorImages[i].color = data.colors[i].GetColor();

            // Selection feedback via animated scale
            if (_buttonRects[i] != null)
            {
                float target = (i == selectedColorIndex) ? selectedScale : normalScale;
                ApplyScale(_buttonRects[i], target, animate);
            }
        }
    }

    // ══════════════════ Helpers ══════════════════

    private void ApplyScale(RectTransform rect, float target, bool animate)
    {
        // Kill any existing tween on this transform to prevent stacking
        DOTween.Kill(rect);

        if (!animate)
        {
            rect.localScale = Vector3.one * target;
            return;
        }

        rect.DOScale(Vector3.one * target, tweenDuration)
            .SetTarget(rect)
            .SetEase(tweenEase)
            .SetUpdate(true); // unscaled time so it works even if Time.timeScale changes
    }
}
