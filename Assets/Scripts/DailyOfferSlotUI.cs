using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Helper component placed on each OfferSlot GameObject inside Section_DailyOffers.
/// Holds references to the child UI elements so DailyOffersController can drive them.
///
/// Progress-bar fade tail:
///   Bar_Fill (main) + Bar_Fill_1..Bar_Fill_8 (ghosts) stacked on top.
///   All must be Image Type=Filled, FillMethod=Horizontal, Origin=Left.
///   Ghosts extend slightly past mainProgress with a decreasing alpha ramp
///   (230 → 0) to create a soft fade edge.
/// </summary>
public class DailyOfferSlotUI : MonoBehaviour
{
    [Header("Required References")]
    [Tooltip("The Button on this slot (click handler is wired by DailyOffersController).")]
    public Button button;

    [Tooltip("Icon image inside BG_Frame/Icon.")]
    public Image icon;

    [Tooltip("Title label – for free slot shows reward name; for card slots shows CardType + rarity.")]
    public TMP_Text titleText;

    [Tooltip("Label shown while the offer is available (e.g. 'FREE' / price).")]
    public TMP_Text freeText;

    [Tooltip("Label shown after purchase/claim (e.g. 'CLAIMED').")]
    public TMP_Text purchasedText;

    [Header("Card-Slot Only (leave null for Free slot)")]
    [Tooltip("Progress bar fill image (legacy fillAmount — optional if using segments).")]
    public Image barFill;

    [Tooltip("Progress text, e.g. '3/8'.")]
    public TMP_Text progressText;

    [Header("Segmented Progress Bar")]
    [Tooltip("8 segment fill images under Bar_Fill (one per segment, toggled on/off). " +
             "Assign in order 0→7. Leave empty / null for Free slot.")]
    [SerializeField] private Image[] fillSegments;

    [Header("Progress Bar – Ghost Fade Tail")]
    [Tooltip("Ghost fills stacked on top of Bar_Fill (Bar_Fill_1 .. Bar_Fill_8). " +
             "Assign in order 1→8. Leave empty / null for Free slot.")]
    [SerializeField] private Image[] barFillGhosts;

    [Tooltip("If true, fill changes are tweened with DOTween over 0.1 s.")]
    [SerializeField] private bool useTween;

    // Ghost alpha ramp: linearly interpolated from 230/255 (ghost 1) → 0 (ghost N).
    private const float GhostAlphaStart = 230f / 255f;   // ≈ 0.902
    private const float GhostAlphaEnd = 0f;
    private const float TweenDuration = 0.1f;

    // ──────────────────────────────── Lifecycle ────────────────────────────────

    private void OnDestroy()
    {
        // Kill any running tweens to prevent callbacks on destroyed objects
        if (useTween)
            KillAllBarTweens();
    }

    // ──────────────────────────────── State helpers ────────────────────────────

    /// <summary>
    /// Show the slot in "available" state (can click to claim/buy).
    /// </summary>
    public void SetAvailable(string labelText)
    {
        if (freeText != null) { freeText.gameObject.SetActive(true); freeText.text = labelText; }
        if (purchasedText != null) purchasedText.gameObject.SetActive(false);
        if (button != null) button.interactable = true;
    }

    /// <summary>
    /// Show the slot in "purchased / claimed" state.
    /// </summary>
    public void SetPurchased(string labelText = "CLAIMED")
    {
        if (freeText != null) freeText.gameObject.SetActive(false);
        if (purchasedText != null) { purchasedText.gameObject.SetActive(true); purchasedText.text = labelText; }
        if (button != null) button.interactable = false;
    }

    /// <summary>
    /// Update progress bar + text for card slots.
    /// Drives segmented fill images AND optional legacy fillAmount bar.
    /// </summary>
    public void SetProgress(float fill01, string text)
    {
        // Derive segment count from fill01 (0..1 maps to 0..8)
        int filledCount = Mathf.Clamp(Mathf.RoundToInt(fill01 * CardDropTuning.SegmentsPerUpgrade), 0, CardDropTuning.SegmentsPerUpgrade);
        UpdateProgressBar(filledCount);
        if (progressText != null) progressText.text = text;
    }

    /// <summary>
    /// Overload that takes an explicit segment count (preferred for accuracy).
    /// </summary>
    public void SetProgressSegments(int segmentBalance, string text)
    {
        int filledCount = Mathf.Clamp(segmentBalance, 0, CardDropTuning.SegmentsPerUpgrade);
        UpdateProgressBar(filledCount);
        if (progressText != null) progressText.text = text;
    }

    /// <summary>
    /// Set the icon sprite. Null-safe.
    /// </summary>
    public void SetIcon(Sprite sprite)
    {
        if (icon != null && sprite != null) icon.sprite = sprite;
    }

    /// <summary>
    /// Set the title label text.
    /// </summary>
    public void SetTitle(string text)
    {
        if (titleText != null) titleText.text = text;
    }

    // ──────────────────────────────── Progress bar (segmented + legacy) ────────

    private void UpdateProgressBar(int filledCount)
    {
        // ── New segmented bar ──────────────────
        if (fillSegments != null && fillSegments.Length > 0)
        {
            for (int i = 0; i < fillSegments.Length; i++)
            {
                if (fillSegments[i] != null)
                    fillSegments[i].gameObject.SetActive(i < filledCount);
            }
        }

        // ── Legacy fill bar (optional fallback) ──
        float mainProgress = (float)filledCount / CardDropTuning.SegmentsPerUpgrade;
        if (barFill != null)
        {
            if (useTween)
            {
                DOTween.Kill(barFill);
                barFill.DOFillAmount(mainProgress, TweenDuration)
                       .SetTarget(barFill)
                       .SetEase(Ease.OutQuad);
            }
            else
            {
                barFill.fillAmount = mainProgress;
            }
        }

        // ── Legacy ghost fills ─────────────────
        if (barFillGhosts == null || barFillGhosts.Length == 0)
            return;

        Color baseColor = (barFill != null) ? barFill.color : Color.white;
        bool isFull = filledCount >= CardDropTuning.SegmentsPerUpgrade;
        int count = barFillGhosts.Length;

        for (int i = 0; i < count; i++)
        {
            Image ghost = barFillGhosts[i];
            if (ghost == null) continue;

            if (isFull)
            {
                if (useTween) DOTween.Kill(ghost);
                ghost.fillAmount = 0f;
                ghost.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                continue;
            }

            float ghostFill = Mathf.Min(1f, mainProgress + (i + 1) * 0.02f);
            float t = (count > 1) ? (float)i / (count - 1) : 1f;
            float alpha = Mathf.Lerp(GhostAlphaStart, GhostAlphaEnd, t);
            ghost.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            if (useTween)
            {
                DOTween.Kill(ghost);
                ghost.DOFillAmount(ghostFill, TweenDuration)
                     .SetTarget(ghost)
                     .SetEase(Ease.OutQuad);
            }
            else
            {
                ghost.fillAmount = ghostFill;
            }
        }
    }

    private void KillAllBarTweens()
    {
        if (barFill != null)
            DOTween.Kill(barFill);

        if (barFillGhosts != null)
        {
            for (int i = 0; i < barFillGhosts.Length; i++)
            {
                if (barFillGhosts[i] != null)
                    DOTween.Kill(barFillGhosts[i]);
            }
        }
    }
}
