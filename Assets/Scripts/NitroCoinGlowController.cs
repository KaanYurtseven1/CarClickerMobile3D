using UnityEngine;
using DG.Tweening;

/// <summary>
/// Per-coin emission glow during magnet pull, using MaterialPropertyBlock
/// (no material instancing, zero GC per frame).
///
/// Works with URP Lit / Simple Lit / Unlit shaders that support _EmissionColor.
/// Bloom picks up the HDR emission automatically via the global Volume.
///
/// SETUP:
///   Attach to the NitroCoin prefab (or let NitroCoin add it at runtime).
///   Tune glowColor (HDR) and glowIntensity in Inspector.
/// </summary>
public class NitroCoinGlowController : MonoBehaviour
{
    [Header("Glow Settings")]
    [Tooltip("HDR base color for glow emission")]
    [ColorUsage(false, true)]
    public Color glowColor = new Color(0.15f, 0.55f, 1f, 1f); // cyan-ish

    [Tooltip("Peak emission intensity multiplier (applied to glowColor)")]
    public float glowIntensity = 0.6f;

    [Tooltip("Fade-in duration (seconds)")]
    public float fadeIn = 0.2f;

    [Tooltip("Fade-out duration (seconds)")]
    public float fadeOut = 0.25f;

    [Header("Pulse")]
    [Tooltip("Oscillate glow intensity while active")]
    public bool pulse = true;

    [Tooltip("How much the glow dips during pulse (0–1 range, fraction of full glow)")]
    [Range(0f, 0.9f)]
    public float pulseAmplitude = 0.35f;

    [Tooltip("Pulse oscillation speed (cycles per second × 2π)")]
    public float pulseSpeed = 6f;

    // ── Shader property IDs (cached, zero alloc) ──
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // ── Runtime ──
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;           // single reused block
    private Color[] _originalEmission;             // per-renderer original
    private Tween _glowTween;
    private Tween _pulseTween;
    private float _currentFactor;                  // 0 = off, 1 = full glow
    private bool _glowActive;
    private bool _initialized;

    // ══════════════════════════════════════════
    //  INIT
    // ══════════════════════════════════════════

    /// <summary>
    /// Must be called once before EnableGlow.  Caches renderers + original emission.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
        _originalEmission = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].GetPropertyBlock(_mpb);
            // If the MPB already has emission, keep it; otherwise read from shared material.
            if (_mpb.HasColor(EmissionColorID))
            {
                _originalEmission[i] = _mpb.GetColor(EmissionColorID);
            }
            else
            {
                var mat = _renderers[i].sharedMaterial;
                _originalEmission[i] = mat != null && mat.HasColor(EmissionColorID)
                    ? mat.GetColor(EmissionColorID)
                    : Color.black;
            }
        }

        _currentFactor = 0f;
        _initialized = true;
    }

    // ══════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════

    /// <summary>Fade in emission glow.  Call from NitroCoin.StartMagnetPull.</summary>
    public void EnableGlow()
    {
        if (!_initialized) Initialize();
        if (_glowActive) return;
        _glowActive = true;

        KillTween();

        _glowTween = DOTween.To(
            () => _currentFactor,
            x =>
            {
                _currentFactor = x;
                ApplyEmission(x);
            },
            1f,
            fadeIn
        ).SetEase(Ease.OutQuad)
         .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
         .OnComplete(StartPulse);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[VFX] NitroCoinGlow ON");
#endif
    }

    /// <summary>Fade out emission glow.  Call from NitroCoin cleanup paths.</summary>
    public void DisableGlow()
    {
        if (!_initialized || !_glowActive) return;
        _glowActive = false;

        KillPulseTween();
        KillTween();

        _glowTween = DOTween.To(
            () => _currentFactor,
            x =>
            {
                _currentFactor = x;
                ApplyEmission(x);
            },
            0f,
            fadeOut
        ).SetEase(Ease.InQuad).SetLink(gameObject, LinkBehaviour.KillOnDestroy);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[VFX] NitroCoinGlow OFF");
#endif
    }

    /// <summary>Instantly clear glow without fade (for destroy paths).</summary>
    public void DisableGlowImmediate()
    {
        if (!_initialized) return;
        KillPulseTween();
        KillTween();
        _glowActive = false;
        _currentFactor = 0f;
        ApplyEmission(0f);
    }

    // ══════════════════════════════════════════
    //  INTERNALS
    // ══════════════════════════════════════════

    /// <summary>
    /// Applies emission to all renderers via MaterialPropertyBlock.
    /// factor: 0 = original emission, 1 = original + glowColor * glowIntensity.
    /// </summary>
    private void ApplyEmission(float factor)
    {
        Color additive = glowColor * (glowIntensity * factor);

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            Color final = _originalEmission[i] + additive;

            _renderers[i].GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorID, final);
            _renderers[i].SetPropertyBlock(_mpb);
        }
    }

    private void KillTween()
    {
        if (_glowTween != null && _glowTween.IsActive())
            _glowTween.Kill();
        _glowTween = null;
    }

    private void KillPulseTween()
    {
        if (_pulseTween != null && _pulseTween.IsActive())
            _pulseTween.Kill();
        _pulseTween = null;
    }

    /// <summary>Starts yoyo pulse after fade-in completes (if pulse is enabled).</summary>
    private void StartPulse()
    {
        if (!pulse || !_glowActive) return;

        float low = 1f - pulseAmplitude;
        float halfCycle = Mathf.PI / Mathf.Max(pulseSpeed, 0.1f); // duration of one half-cycle

        _pulseTween = DOTween.To(
            () => _currentFactor,
            x =>
            {
                _currentFactor = x;
                ApplyEmission(x);
            },
            low,
            halfCycle
        ).SetEase(Ease.InOutSine)
         .SetLoops(-1, LoopType.Yoyo)
         .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void OnDestroy()
    {
        KillPulseTween();
        KillTween();
    }
}
