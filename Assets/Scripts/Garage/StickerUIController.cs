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
//
// ── Animations added (visual-only, no logic change) ──────────
//   • AnimateHighlightTo(index, instant)
//       Slides ArabaStickerOn to the target slot using DOMove
//       (world-space, because highlight and slots have different parents).
//   • AnimateSlotEmphasis(index, instant)
//       Selected slot smoothly moves to x=-60 & scale 1.3;
//       all others animate back to x=35 & scale 1.0.
//   Both are called from SetHighlight() / Refresh().
// ════════════════════════════════════════════════════════════════
using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class StickerUIController : MonoBehaviour
{
    // ─── Serialized ───
    [Header("References")]
    [SerializeField] private RectTransform arabaStickerOn;
    [SerializeField] private Transform stickerSlotsParent;

    // ─── Animation Settings (inspector-tunable) ───
    [Header("Highlight Slide")]
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private Ease moveEase = Ease.OutCubic;

    [Header("Slot Emphasis")]
    [SerializeField] private float emphasisDuration = 0.25f;
    [SerializeField] private Ease emphasisEase = Ease.OutBack;
    [SerializeField] private float selectedX = -60f;
    [SerializeField] private float unselectedX = 35f;
    [SerializeField] private float selectedScale = 1.3f;
    [SerializeField] private float unselectedScale = 1.0f;

    // ─── Callback (set by GarageController) ───
    [NonSerialized] public Action<int> onStickerSelected;

    // ─── Cached UI elements ───
    private readonly Button[] _slotButtons = new Button[6];
    private readonly Image[] _slotImages = new Image[6];
    private readonly RectTransform[] _slotRects = new RectTransform[6];

    // ─── Cached original anchoredPosition.y per slot ───
    private readonly float[] _slotOriginalY = new float[6];

    // ─── Track whether first highlight has been applied ───
    private bool _firstHighlightDone;

    // ─── Track current highlight index to avoid redundant moves ───
    private int _currentHighlightIndex = -1;

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        ResolveSlots();
    }

    private void OnDestroy()
    {
        // Clean up all tweens owned by this controller's elements
        if (arabaStickerOn != null)
            DOTween.Kill(arabaStickerOn);

        for (int i = 0; i < 6; i++)
        {
            if (_slotRects[i] != null)
                DOTween.Kill(_slotRects[i]);
        }
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

            // Cache original Y so emphasis only changes X
            if (_slotRects[i] != null)
                _slotOriginalY[i] = _slotRects[i].anchoredPosition.y;

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
        // Game-logic callback — untouched
        onStickerSelected?.Invoke(index);
    }

    // ══════════════════ Affordability API ══════════════════

    /// <summary>Sets the affordability visual (dim/bright) for a specific sticker slot.</summary>
    public void SetSlotAffordable(int index, bool canAfford, bool owned)
    {
        if (index < 0 || index >= 6 || _slotImages[index] == null) return;
        Color c = _slotImages[index].color;
        c.a = canAfford ? 1f : 0.35f;
        _slotImages[index].color = c;
    }

    /// <summary>Shake feedback when player can't afford this sticker.</summary>
    public void ShakeSlot(int index)
    {
        if (index < 0 || index >= 6 || _slotRects[index] == null) return;
        GarageAffordabilityHelper.ShakeButton(_slotRects[index]);
    }

    // ══════════════════ Public API ══════════════════

    /// <summary>
    /// Refreshes all 6 sticker preview images using the car's pre-assigned
    /// logo sprites (<see cref="CarDataSO.StickerPreviewSprites"/>),
    /// then moves the highlight to <paramref name="selectedStickerIndex"/>.
    /// The very first call applies the highlight instantly to avoid pop-in.
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

        // First Refresh should snap instantly (no pop-in),
        // subsequent calls animate smoothly.
        bool instant = !_firstHighlightDone;
        _firstHighlightDone = true;

        SetHighlight(selectedStickerIndex, instant);
    }

    /// <summary>
    /// Moves the ArabaStickerOn highlight to the given slot index (0-5).
    /// Optionally snaps instantly (used on first load).
    /// </summary>
    public void SetHighlight(int stickerIndex, bool instant = false)
    {
        if (stickerIndex < 0 || stickerIndex >= 6) return;
        if (arabaStickerOn == null || _slotRects[stickerIndex] == null) return;

        // ── Visual animations (no game-logic change) ──
        AnimateHighlightTo(stickerIndex, instant);
        AnimateSlotEmphasis(stickerIndex, instant);
    }

    // ══════════════════ Animation Helpers (visual-only) ══════════════════

    /// <summary>
    /// Smoothly slides the ArabaStickerOn RectTransform to the target slot's
    /// world position. Uses DOMove because highlight and slots have different
    /// parents (ArabaStickerOn → CarSticker_BG, slots → CarSticker_BG/CarStickers).
    /// </summary>
    private void AnimateHighlightTo(int index, bool instant)
    {
        // Guard: skip if already highlighting this slot (prevents offset accumulation)
        if (!instant && index == _currentHighlightIndex) return;
        _currentHighlightIndex = index;

        // Only take the slot's world-space Y; preserve ArabaStickerOn's own X and Z.
        // This ensures slot emphasis offsets (anchoredPosition.x changes) never leak
        // into the highlight's horizontal position.
        Vector3 targetPos = arabaStickerOn.position;
        targetPos.y = _slotRects[index].position.y;

        // Kill any running tween on the highlight to prevent stacking
        DOTween.Kill(arabaStickerOn);

        if (instant)
        {
            arabaStickerOn.position = targetPos;
            return;
        }

        arabaStickerOn
            .DOMove(targetPos, moveDuration)
            .SetTarget(arabaStickerOn)
            .SetEase(moveEase)
            .SetUpdate(true); // unscaled time
    }

    /// <summary>
    /// Animates the selected slot to x=-60 / scale 1.3 and all other slots
    /// back to x=35 / scale 1.0. Operates on anchoredPosition (same-parent
    /// context for each slot's own RectTransform).
    /// </summary>
    private void AnimateSlotEmphasis(int selectedIndex, bool instant)
    {
        for (int i = 0; i < 6; i++)
        {
            RectTransform rect = _slotRects[i];
            if (rect == null) continue;

            // Kill previous emphasis tweens on this slot
            DOTween.Kill(rect);

            bool isSelected = (i == selectedIndex);
            float targetX = isSelected ? selectedX : unselectedX;
            float targetScl = isSelected ? selectedScale : unselectedScale;

            if (instant)
            {
                // Snap immediately (no animation)
                rect.anchoredPosition = new Vector2(targetX, _slotOriginalY[i]);
                rect.localScale = Vector3.one * targetScl;
            }
            else
            {
                // Smooth anchoredPosition.x tween (preserve original Y)
                rect.DOAnchorPos(new Vector2(targetX, _slotOriginalY[i]), emphasisDuration)
                    .SetTarget(rect)
                    .SetEase(emphasisEase)
                    .SetUpdate(true);

                // Smooth scale tween
                rect.DOScale(targetScl, emphasisDuration)
                    .SetTarget(rect)
                    .SetEase(emphasisEase)
                    .SetUpdate(true);
            }
        }
    }
}
