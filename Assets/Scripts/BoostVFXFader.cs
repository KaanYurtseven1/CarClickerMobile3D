using UnityEngine;
using DG.Tweening;

/// <summary>
/// Smooth fade-in / fade-out controller for boost fire VFX via emission-rate modulation.
/// Attach this component to each BoostFX_L and BoostFX_R prefab instance.
///
/// How it works:
///   Fade-in  → Activates the GameObject, clears old particles, ramps emission from 0→100 %.
///   Fade-out → Ramps emission from 100→0 %, stops emitting, waits for remaining particles
///              to die, then deactivates the GameObject.
///
/// Falls back to instant on/off if DOTween is unavailable or duration ≤ 0.
/// </summary>
public class BoostVFXFader : MonoBehaviour
{
    [Header("Fade Timing")]
    [Tooltip("Seconds for the fire to ramp up to full emission.")]
    public float fadeInDuration = 0.4f;

    [Tooltip("Seconds for the fire to ramp emission down to zero.")]
    public float fadeOutDuration = 0.6f;

    [Tooltip("Extra seconds after emission stops before the GameObject is deactivated " +
             "(gives remaining particles time to finish their lifetime).")]
    public float deactivateDelay = 2f;

    // ── Internal state ──────────────────────────────────────────
    private ParticleSystem[] _particleSystems;
    private float[] _originalRateMultipliers;
    private float _fade;                 // 0 = fully faded, 1 = fully visible
    private Tweener _fadeTween;
    private Tween _deactivateTween;

    // ── Lazy init (safe even when GO starts inactive) ───────────
    private void EnsureInitialized()
    {
        if (_particleSystems != null) return;

        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        _originalRateMultipliers = new float[_particleSystems.Length];
        for (int i = 0; i < _particleSystems.Length; i++)
        {
            float m = _particleSystems[i].emission.rateOverTimeMultiplier;
            _originalRateMultipliers[i] = m > 0f ? m : 1f;   // guard against 0
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Smoothly fade in the VFX. Activates the GameObject, clears stale particles,
    /// and ramps emission from 0 % → 100 % over <see cref="fadeInDuration"/>.
    /// </summary>
    public void FadeIn()
    {
        KillTweens();
        gameObject.SetActive(true);
        EnsureInitialized();

        // Start from zero emission so no instant "pop"
        ApplyFade(0f);

        foreach (var ps in _particleSystems)
        {
            ps.Clear(false);
            ps.Play(false);
        }

        _fade = 0f;
        _fadeTween = DOTween.To(() => _fade, SetFade, 1f, fadeInDuration)
            .SetEase(Ease.OutQuad)
            .SetTarget(this);
    }

    /// <summary>
    /// Smoothly fade out the VFX. Ramps emission to 0 %, stops emitting,
    /// waits <see cref="deactivateDelay"/> seconds, then deactivates the GameObject.
    /// </summary>
    public void FadeOut()
    {
        if (!gameObject.activeSelf) return;
        EnsureInitialized();
        KillTweens();

        _fadeTween = DOTween.To(() => _fade, SetFade, 0f, fadeOutDuration)
            .SetEase(Ease.InQuad)
            .SetTarget(this)
            .OnComplete(() =>
            {
                // Stop new particle emission; existing particles play out naturally.
                foreach (var ps in _particleSystems)
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);

                // Deactivate after remaining particles die out.
                _deactivateTween = DOVirtual.DelayedCall(deactivateDelay, () =>
                {
                    if (_fade <= 0f)           // guard: only deactivate if still faded
                        gameObject.SetActive(false);
                }).SetTarget(this);
            });
    }

    /// <summary>
    /// Instant on / off without any fade animation.
    /// Use for state recovery on scene load or car switching during an active boost.
    /// </summary>
    public void SetImmediate(bool active)
    {
        KillTweens();
        gameObject.SetActive(active);

        if (active)
        {
            EnsureInitialized();
            ApplyFade(1f);
            _fade = 1f;
            foreach (var ps in _particleSystems)
            {
                ps.Clear(false);
                ps.Play(false);
            }
        }
        else
        {
            _fade = 0f;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  INTERNALS
    // ═══════════════════════════════════════════════════════════

    private void SetFade(float value)
    {
        _fade = value;
        ApplyFade(value);
    }

    private void ApplyFade(float t)
    {
        if (_particleSystems == null) return;
        for (int i = 0; i < _particleSystems.Length; i++)
        {
            var emission = _particleSystems[i].emission;
            emission.rateOverTimeMultiplier = _originalRateMultipliers[i] * t;
        }
    }

    private void KillTweens()
    {
        _fadeTween?.Kill();
        _fadeTween = null;
        _deactivateTween?.Kill();
        _deactivateTween = null;
    }

    private void OnDestroy()
    {
        KillTweens();
    }
}
