// ════════════════════════════════════════════════════════════════
// ColorUIController.cs – Manages the 6 color-choose buttons.
//
// Inspector wiring:
//   colorButtonsParent → Canvas/ColorChooseButtons
// ════════════════════════════════════════════════════════════════
using System;
using UnityEngine;
using UnityEngine.UI;

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

    // ─── Callback (set by GarageController) ───
    [NonSerialized] public Action<int> onColorSelected;

    // ─── Cached ───
    private readonly Button[] _buttons = new Button[6];
    private readonly Image[] _colorImages = new Image[6];
    private readonly RectTransform[] _buttonRects = new RectTransform[6];

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

    /// <summary>
    /// Updates the 6 color swatches and applies a selection visual (scale).
    /// </summary>
    public void Refresh(CarDataSO data, int selectedColorIndex)
    {
        if (data == null) return;

        for (int i = 0; i < 6; i++)
        {
            // Set swatch color
            if (i < data.colors.Count && _colorImages[i] != null)
                _colorImages[i].color = data.colors[i].GetColor();

            // Selection feedback via scale
            if (_buttonRects[i] != null)
            {
                float s = (i == selectedColorIndex) ? selectedScale : normalScale;
                _buttonRects[i].localScale = Vector3.one * s;
            }
        }
    }
}
