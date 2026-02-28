// ════════════════════════════════════════════════════════════════
// StickerUIController.cs – Manages the 6 sticker preview slots
//                          and the ArabaStickerOn highlight.
//
// Sticker preview sprites are read from CarDataSO.StickerPreviewSprites
// (colour-independent logo PNGs assigned via the editor tool).
//
// Inspector wiring:
//   arabaStickerOn      → Canvas/CarSticker_BG/ArabaStickerOn
//   stickerSlotsParent  → Canvas/CarSticker_BG/CarStickers
// ════════════════════════════════════════════════════════════════
using System;
using UnityEngine;
using UnityEngine.UI;

public class StickerUIController : MonoBehaviour
{
    // ─── Serialized ───
    [Header("References")]
    [SerializeField] private RectTransform arabaStickerOn;
    [SerializeField] private Transform stickerSlotsParent;

    // ─── Callback (set by GarageController) ───
    [NonSerialized] public Action<int> onStickerSelected;

    // ─── Cached UI elements ───
    private readonly Button[] _slotButtons = new Button[6];
    private readonly Image[] _slotImages = new Image[6];
    private readonly RectTransform[] _slotRects = new RectTransform[6];

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        ResolveSlots();
    }

    // ══════════════════ Slot Resolution ══════════════════

    private void ResolveSlots()
    {
        if (stickerSlotsParent == null)
        {
            Debug.LogError("[StickerUIController] stickerSlotsParent is not assigned.");
            return;
        }

        for (int i = 0; i < 6; i++)
        {
            string slotName = (i + 1).ToString(); // "1" … "6"
            Transform slot = stickerSlotsParent.Find(slotName);
            if (slot == null)
            {
                Debug.LogError($"[StickerUIController] Slot '{slotName}' not found under '{stickerSlotsParent.name}'.");
                continue;
            }

            _slotRects[i] = slot.GetComponent<RectTransform>();
            _slotImages[i] = slot.GetComponent<Image>();

            // Ensure the slot has a Button
            _slotButtons[i] = slot.GetComponent<Button>();
            if (_slotButtons[i] == null)
                _slotButtons[i] = slot.gameObject.AddComponent<Button>();

            int captured = i;
            _slotButtons[i].onClick.AddListener(() => OnSlotClicked(captured));
        }
    }

    private void OnSlotClicked(int index)
    {
        onStickerSelected?.Invoke(index);
    }

    // ══════════════════ Public API ══════════════════

    /// <summary>
    /// Refreshes all 6 sticker preview images using the car's pre-assigned
    /// logo sprites (<see cref="CarDataSO.StickerPreviewSprites"/>),
    /// then moves the highlight to <paramref name="selectedStickerIndex"/>.
    /// </summary>
    public void Refresh(CarDataSO data, int selectedStickerIndex)
    {
        if (data == null) return;

        var sprites = data.StickerPreviewSprites;

        for (int s = 0; s < 6; s++)
        {
            if (_slotImages[s] == null) continue;

            Sprite spr = (sprites != null && s < sprites.Count) ? sprites[s] : null;

            if (spr != null)
            {
                _slotImages[s].sprite = spr;
                _slotImages[s].color = Color.white;
            }
            else
            {
                // Fallback: tinted placeholder
                _slotImages[s].sprite = null;
                _slotImages[s].color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            }
        }

        SetHighlight(selectedStickerIndex);
    }

    /// <summary>
    /// Moves the ArabaStickerOn highlight to the given slot index (0-5).
    /// </summary>
    public void SetHighlight(int stickerIndex)
    {
        if (stickerIndex < 0 || stickerIndex >= 6) return;
        if (arabaStickerOn == null || _slotRects[stickerIndex] == null) return;

        // Match world position so the highlight overlays the selected slot
        arabaStickerOn.position = _slotRects[stickerIndex].position;
    }
}
