// ════════════════════════════════════════════════════════════════
// ShowcasePostProcessController.cs – Per-shot URP post-processing
// tweens for the cinematic car showcase.
//
// Drives Bloom, Vignette, and Depth of Field overrides on the
// scene's URP Volume.  Called by CarShowcaseDirector at each shot
// transition via the public ApplyShot / Reset methods.
//
// Follows the exact DOTween + URP Volume pattern from
// BoostPostProcessController.cs.
//
// SETUP:
//   1) Attach to the ShowcaseDirector GameObject (or its own child).
//   2) Assign the scene's Global Volume in the Inspector.
//   3) The Volume profile MUST have Bloom, Vignette, and
//      DepthOfField overrides ENABLED (even if values are at default).
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

public class ShowcasePostProcessController : MonoBehaviour
{
    // ─────────────────── Inspector ───────────────────
    [Header("── Volume ──")]
    [SerializeField] private Volume volume;

    [Header("── Default Look ──")]
    [Tooltip("Bloom intensity when no shot override is active.")]
    [SerializeField] private float defaultBloom = 0.3f;

    [Tooltip("Vignette intensity at rest.")]
    [SerializeField] private float defaultVignette = 0.15f;

    [Tooltip("DOF focus distance at rest (far = everything sharp).")]
    [SerializeField] private float defaultFocusDistance = 20f;

    // ─────────────────── Runtime ───────────────────
    private Bloom _bloom;
    private Vignette _vignette;
    private DepthOfField _dof;

    private Tween _bloomTween;
    private Tween _vignetteTween;
    private Tween _dofTween;

    private bool _ready;

    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        CacheOverrides();
    }

    private void OnDestroy()
    {
        KillAll();
    }

    // ─── Cache ───

    private void CacheOverrides()
    {
        if (volume == null || volume.profile == null)
        {
            Debug.LogWarning("[ShowcasePostProcess] Volume or profile is null.");
            return;
        }

        volume.profile.TryGet(out _bloom);
        volume.profile.TryGet(out _vignette);
        volume.profile.TryGet(out _dof);

        _ready = _bloom != null || _vignette != null || _dof != null;

        if (!_ready)
            Debug.LogWarning("[ShowcasePostProcess] No Bloom/Vignette/DOF found on Volume profile.");
    }

    // ═══════════════════════════════════════════════════════════
    //  Public API — called by CarShowcaseDirector
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns a Tween (or Sequence) that transitions post-processing
    /// to the look defined by <paramref name="hint"/>.  Append/Join this
    /// into the Director's master Sequence.
    /// </summary>
    public Tween CreateShotTransition(ShowcasePostProcessHint hint, float duration)
    {
        if (!_ready) return DOTween.Sequence();  // no-op tween

        var seq = DOTween.Sequence();

        switch (hint)
        {
            case ShowcasePostProcessHint.ShallowDOF:
                seq.Join(TweenBloom(defaultBloom + 0.1f, duration));
                seq.Join(TweenVignette(defaultVignette + 0.1f, duration));
                seq.Join(TweenDOF(3f, duration));        // tight focus
                break;

            case ShowcasePostProcessHint.BloomEmphasis:
                seq.Join(TweenBloom(defaultBloom + 0.5f, duration));
                seq.Join(TweenVignette(defaultVignette, duration));
                seq.Join(TweenDOF(defaultFocusDistance, duration));
                break;

            case ShowcasePostProcessHint.Vignette:
                seq.Join(TweenBloom(defaultBloom, duration));
                seq.Join(TweenVignette(0.45f, duration));
                seq.Join(TweenDOF(defaultFocusDistance, duration));
                break;

            case ShowcasePostProcessHint.Dramatic:
                seq.Join(TweenBloom(defaultBloom + 0.35f, duration));
                seq.Join(TweenVignette(0.4f, duration));
                seq.Join(TweenDOF(5f, duration));
                break;

            case ShowcasePostProcessHint.None:
            default:
                seq.Join(TweenBloom(defaultBloom, duration));
                seq.Join(TweenVignette(defaultVignette, duration));
                seq.Join(TweenDOF(defaultFocusDistance, duration));
                break;
        }

        return seq;
    }

    /// <summary>Reset all overrides to default values instantly.</summary>
    public void ResetToDefaults()
    {
        KillAll();
        if (_bloom != null) _bloom.intensity.value = defaultBloom;
        if (_vignette != null) _vignette.intensity.value = defaultVignette;
        if (_dof != null) _dof.focusDistance.value = defaultFocusDistance;
    }

    /// <summary>Set defaults immediately on start (before first shot).</summary>
    public void ApplyDefaults()
    {
        ResetToDefaults();
    }

    // ═══════════════════════════════════════════════════════════
    //  Tween Builders
    // ═══════════════════════════════════════════════════════════

    private Tween TweenBloom(float target, float duration)
    {
        if (_bloom == null) return DOTween.Sequence();
        KillTween(ref _bloomTween);
        _bloomTween = DOTween.To(
            () => _bloom.intensity.value,
            x => _bloom.intensity.value = x,
            target, duration
        ).SetEase(Ease.InOutSine);
        return _bloomTween;
    }

    private Tween TweenVignette(float target, float duration)
    {
        if (_vignette == null) return DOTween.Sequence();
        KillTween(ref _vignetteTween);
        _vignetteTween = DOTween.To(
            () => _vignette.intensity.value,
            x => _vignette.intensity.value = x,
            target, duration
        ).SetEase(Ease.InOutSine);
        return _vignetteTween;
    }

    private Tween TweenDOF(float focusDist, float duration)
    {
        if (_dof == null) return DOTween.Sequence();
        KillTween(ref _dofTween);
        _dofTween = DOTween.To(
            () => _dof.focusDistance.value,
            x => _dof.focusDistance.value = x,
            focusDist, duration
        ).SetEase(Ease.InOutSine);
        return _dofTween;
    }

    // ─── Cleanup ───

    private static void KillTween(ref Tween t)
    {
        if (t != null && t.IsActive()) t.Kill();
        t = null;
    }

    private void KillAll()
    {
        KillTween(ref _bloomTween);
        KillTween(ref _vignetteTween);
        KillTween(ref _dofTween);
    }
}
