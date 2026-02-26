// ════════════════════════════════════════════════════════════════
// StickerUIController.cs – Manages the 6 sticker preview slots
//                          and the ArabaStickerOn highlight.
//
// Inspector wiring:
//   arabaStickerOn      → Canvas/CarSticker_BG/ArabaStickerOn
//   stickerSlotsParent  → Canvas/CarSticker_BG/CarStickers
// ════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
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

    // ─── Sprite cache:  (carId, colorIndex, stickerIndex) → Sprite ───
    private readonly Dictionary<(string, int, int), Sprite> _spriteCache
        = new Dictionary<(string, int, int), Sprite>();

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
    /// Refreshes all 6 sticker preview images for the given car + colorIndex,
    /// then moves the highlight to <paramref name="selectedStickerIndex"/>.
    /// </summary>
    public void Refresh(CarDataSO data, int colorIndex, int selectedStickerIndex)
    {
        if (data == null) return;

        for (int s = 0; s < 6; s++)
        {
            if (_slotImages[s] == null) continue;

            Sprite spr = GetOrCreateSprite(data, colorIndex, s);
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

    // ══════════════════ Sprite Cache ══════════════════

    private Sprite GetOrCreateSprite(CarDataSO data, int colorIndex, int stickerIndex)
    {
        var key = (data.carId, colorIndex, stickerIndex);
        if (_spriteCache.TryGetValue(key, out Sprite cached))
            return cached;

        Texture2D tex = data.GetSkinTexture(colorIndex, stickerIndex);
        if (tex == null) return null;

        Sprite spr = null;
        try
        {
            spr = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect
            );
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"[StickerUIController] Sprite.Create failed for {data.carId} C{colorIndex} S{stickerIndex}. " +
                $"Ensure the texture has Read/Write enabled in its import settings.  Error: {e.Message}");
            return null;
        }

        _spriteCache[key] = spr;
        return spr;
    }
}
