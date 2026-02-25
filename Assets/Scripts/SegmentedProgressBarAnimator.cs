// ============================================================================
// SegmentedProgressBarAnimator.cs — Reusable DOTween-based segment reveal
// ============================================================================
// ANIMATION ONLY: progress bar visual transition, logic unchanged
//
// Attach alongside any UI that has 8-segment fill children (Image[]).
// Accepts an Image[] reference (same array the host script already serializes)
// and provides Hide / Reveal / SetImmediate helpers with scale-pop + alpha fade.
//
// Usage:
//   animator.HideAllImmediate();            // all invisible instantly
//   animator.PlayReveal(filledCount);       // left→right staggered pop-in
//   animator.SetImmediate(filledCount);     // snap visible, no animation
//   animator.Kill();                        // cancel running animation
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SegmentedProgressBarAnimator : MonoBehaviour
{
    // ── Debug ────────────────────────────────────────────────────────────
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void DLog(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[SegBarAnim][{name}#{GetInstanceID()}] t={Time.time:F2} rt={Time.realtimeSinceStartup:F2} f={Time.frameCount} | {msg}");
    }

    // ── Tuning ───────────────────────────────────────────────────────────
    [Header("Animation Tuning")]
    [Tooltip("Delay between each segment starting its reveal.")]
    [SerializeField] private float stepDelay = 0.05f;

    [Tooltip("Duration of each individual segment's scale + fade tween.")]
    [SerializeField] private float segmentDuration = 0.12f;

    [Tooltip("Scale the segment starts at before popping to 1.0.")]
    [SerializeField] private float startScale = 0.5f;

    [Tooltip("Ease curve for the scale pop.")]
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    [Tooltip("Ease curve for the alpha fade.")]
    [SerializeField] private Ease alphaEase = Ease.OutQuad;

    // ── Runtime ──────────────────────────────────────────────────────────
    private Image[] segments;       // set once via Init()
    private Color[] originalColors; // cached original RGBA per segment
    private Sequence revealSeq;

    // =====================================================================
    // Public API
    // =====================================================================

    /// <summary>
    /// Must be called once before any other method. Pass in the same Image[]
    /// array the host script already uses for segment toggling.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public void Init(Image[] segmentImages)
    {
        if (segmentImages == null || segmentImages.Length == 0) return;

        segments = segmentImages;
        originalColors = new Color[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] != null)
                originalColors[i] = segments[i].color;
        }
    }

    /// <summary>
    /// Returns true if Init() has been called with a valid array.
    /// </summary>
    public bool IsInitialized => segments != null && segments.Length > 0;

    /// <summary>
    /// Snap all segments to the correct on/off state with no animation.
    /// </summary>
    public void SetImmediate(int filledCount)
    {
        if (segments == null) return;

        KillInternal();

        int clamped = Mathf.Clamp(filledCount, 0, segments.Length);
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            bool on = i < clamped;
            segments[i].gameObject.SetActive(on);
            if (on)
            {
                segments[i].color = originalColors[i];
                segments[i].transform.localScale = Vector3.one;
            }
        }
    }

    /// <summary>
    /// Instantly hide all segments (inactive + zero alpha + small scale).
    /// </summary>
    public void HideAllImmediate()
    {
        if (segments == null) return;

        DLog($"HideAllImmediate — segments.Length={segments.Length}");

        KillInternal();

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            segments[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Staggered left→right reveal of the first <paramref name="filledCount"/> segments.
    /// Re-entrant safe: kills any running animation first.
    /// </summary>
    public void PlayReveal(int filledCount)
    {
        PlayReveal(filledCount, stepDelay, segmentDuration);
    }

    /// <summary>
    /// Overload with custom timing per call site.
    /// </summary>
    public void PlayReveal(int filledCount, float delay, float duration)
    {
        if (segments == null) return;

        bool wasRunning = revealSeq != null && revealSeq.IsActive();
        if (wasRunning) DLog($"PlayReveal — KILLING previous sequence (re-entrant)");

        KillInternal();

        int clamped = Mathf.Clamp(filledCount, 0, segments.Length);
        DLog($"PlayReveal — filledCount={filledCount} clamped={clamped} segments.Length={segments.Length} delay={delay} duration={duration}");

        // Hide all first
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            segments[i].gameObject.SetActive(false);
        }

        if (clamped <= 0) return;

        revealSeq = DOTween.Sequence();

        for (int i = 0; i < clamped; i++)
        {
            Image seg = segments[i];
            if (seg == null) continue;

            float insertTime = i * delay;
            int idx = i; // capture for closure

            // Activate + set starting state via callback
            revealSeq.InsertCallback(insertTime, () =>
            {
                seg.gameObject.SetActive(true);
                seg.transform.localScale = Vector3.one * startScale;
                Color c = originalColors[idx];
                seg.color = new Color(c.r, c.g, c.b, 0f);
            });

            // Scale: startScale → 1.0
            revealSeq.Insert(insertTime,
                seg.transform.DOScale(Vector3.one, duration)
                    .SetEase(scaleEase));

            // Alpha: 0 → original alpha
            float targetAlpha = originalColors[idx].a;
            revealSeq.Insert(insertTime,
                seg.DOColor(new Color(originalColors[idx].r, originalColors[idx].g,
                                      originalColors[idx].b, targetAlpha), duration)
                    .SetEase(alphaEase));
        }

        // Safety: on kill or complete, snap to correct final state
        revealSeq.OnKill(() => SnapFinal(clamped));
        revealSeq.OnComplete(() => SnapFinal(clamped));
        revealSeq.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        revealSeq.SetUpdate(true); // ignore timescale
    }

    /// <summary>
    /// Kill any running reveal animation.
    /// </summary>
    public void Kill()
    {
        DLog("Kill called");
        KillInternal();
    }

    // =====================================================================
    // Internals
    // =====================================================================

    private void KillInternal()
    {
        if (revealSeq != null && revealSeq.IsActive())
        {
            // Temporarily remove OnKill to avoid double-snap
            revealSeq.OnKill(null);
            revealSeq.Kill();
        }
        revealSeq = null;
    }

    /// <summary>
    /// Guarantees the correct visual end state (segments on/off, full scale, original color).
    /// </summary>
    private void SnapFinal(int filledCount)
    {
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;

            bool on = i < filledCount;
            segments[i].gameObject.SetActive(on);
            if (on)
            {
                segments[i].color = originalColors[i];
                segments[i].transform.localScale = Vector3.one;
            }
        }

        revealSeq = null;
    }

    private void OnDestroy()
    {
        KillInternal();
    }
}
