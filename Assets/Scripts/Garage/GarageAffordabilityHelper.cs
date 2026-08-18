// ════════════════════════════════════════════════════════════════
// GarageAffordabilityHelper.cs – Static utility for dim/shake
// visual feedback on garage UI buttons.
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public static class GarageAffordabilityHelper
{
    // ── Affordability dimming ──

    private static readonly Color AffordableColor = Color.white;
    private static readonly Color UnaffordableColor = new Color(1f, 1f, 1f, 0.35f);

    /// <summary>
    /// Dims or brightens a CanvasGroup (or Graphic) to indicate affordability.
    /// Prefer calling with the button's Image or CanvasGroup.
    /// </summary>
    public static void SetAffordable(Graphic graphic, bool canAfford)
    {
        if (graphic == null) return;
        graphic.color = canAfford ? AffordableColor : UnaffordableColor;
    }

    /// <summary>Sets affordability alpha via a CanvasGroup.</summary>
    public static void SetAffordable(CanvasGroup group, bool canAfford)
    {
        if (group == null) return;
        group.alpha = canAfford ? 1f : 0.35f;
    }

    // ── Shake feedback for "can't afford" ──

    /// <summary>Plays a quick horizontal shake on a RectTransform.</summary>
    public static void ShakeButton(RectTransform rect)
    {
        if (rect == null) return;
        DOTween.Kill(rect, true);
        rect.DOShakeAnchorPos(0.35f, new Vector2(12f, 0f), 14, 90f, false, true)
            .SetTarget(rect)
            .SetUpdate(true);
    }
}
