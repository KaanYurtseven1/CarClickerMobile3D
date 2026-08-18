// ════════════════════════════════════════════════════════════════
// HorizontalSwipeScroller.cs – Horizontal swipe / drag scrolling
//                               using Unity ScrollRect under the hood.
//
// Attach to the VIEWPORT element (e.g. Slide_Parts).
//
// Requirements on the viewport GameObject:
//   • Image component (can be fully transparent, alpha=0)
//     with Raycast Target = ON, so drag events are received.
//   • RectMask2D (or Mask + Image) for content clipping.
//
// Inspector wiring:
//   content → Modifiye_Parts RectTransform
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class HorizontalSwipeScroller : MonoBehaviour
{
    [Header("Content")]
    [Tooltip("Assign the content RectTransform (e.g. Modifiye_Parts).")]
    [SerializeField] private RectTransform content;

    [Header("Behavior")]
    [Tooltip("Enable smooth deceleration after releasing.")]
    [SerializeField] private bool useInertia = true;

    [Tooltip("Deceleration rate (0 = instant stop, 1 = never stop). Default 0.135 matches Unity ScrollRect.")]
    [SerializeField, Range(0f, 1f)] private float decelerationRate = 0.135f;

    private ScrollRect _scrollRect;

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();

        // Horizontal only, clamped, no scrollbar
        _scrollRect.horizontal = true;
        _scrollRect.vertical = false;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.inertia = useInertia;
        _scrollRect.decelerationRate = decelerationRate;
        _scrollRect.scrollSensitivity = 0f;

        // Assign content & viewport
        if (content != null)
            _scrollRect.content = content;

        _scrollRect.viewport = GetComponent<RectTransform>();

        // Ensure no scrollbars are displayed
        _scrollRect.horizontalScrollbar = null;
        _scrollRect.verticalScrollbar = null;
        _scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    // ══════════════════ Public API ══════════════════

    /// <summary>
    /// Resets the scroll position to the far left (beginning).
    /// Call after the active car changes if you want to reset scroll.
    /// </summary>
    public void ResetScroll()
    {
        if (_scrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        _scrollRect.horizontalNormalizedPosition = 0f;
    }
}
