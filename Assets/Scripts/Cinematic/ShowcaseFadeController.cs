// ════════════════════════════════════════════════════════════════
// ShowcaseFadeController.cs – Full-screen fade overlay for the
// cinematic showcase.  Returns Tweens that can be appended to a
// DOTween Sequence, or called standalone.
//
// Attach to a Canvas with a full-screen black Image whose
// CanvasGroup starts at alpha = 1 (fully black).
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class ShowcaseFadeController : MonoBehaviour
{
    private CanvasGroup _cg;

    private void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
    }

    /// <summary>Set the overlay to fully opaque (black).</summary>
    public void SetBlack()
    {
        EnsureCG();
        _cg.alpha = 1f;
        _cg.blocksRaycasts = true;
    }

    /// <summary>Set the overlay to fully transparent.</summary>
    public void SetClear()
    {
        EnsureCG();
        _cg.alpha = 0f;
        _cg.blocksRaycasts = false;
    }

    /// <summary>
    /// Fade FROM black TO clear.  Returns a Tween suitable for
    /// appending to a DOTween Sequence.
    /// </summary>
    public Tween CreateFadeIn(float duration)
    {
        EnsureCG();
        return _cg.DOFade(0f, duration)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => _cg.blocksRaycasts = false);
    }

    /// <summary>
    /// Fade FROM clear TO black.  Returns a Tween suitable for
    /// appending to a DOTween Sequence.
    /// </summary>
    public Tween CreateFadeOut(float duration)
    {
        EnsureCG();
        _cg.blocksRaycasts = true;
        return _cg.DOFade(1f, duration)
            .SetEase(Ease.InOutSine);
    }

    private void EnsureCG()
    {
        if (_cg == null) _cg = GetComponent<CanvasGroup>();
    }
}
