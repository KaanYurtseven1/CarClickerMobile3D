// ═══════════════════════════════════════════════════════════════════════
// BoostPostProcessController.cs
// Phase 3A — Cinematic post-processing during boost (Bloom + Vignette)
//
// SETUP:
//   1) Add this component to a persistent manager GameObject (e.g. GameManager).
//   2) Assign the scene's Volume (Global URP Volume) in the Inspector.
//   3) The Volume's profile MUST have Bloom and Vignette overrides ENABLED.
//      (URP → Volume → Add Override → Post-processing → Bloom / Vignette)
//   4) Original Bloom/Vignette values are cached at init — they are NOT assumed 0.
// ═══════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using DG.Tweening;

public class BoostPostProcessController : MonoBehaviour
{
    [SerializeField] private Volume volume;

    [Header("Bloom Target")]
    [SerializeField] private float bloomTarget = 0.6f;
    [SerializeField] private float bloomFadeIn = 0.25f;
    [SerializeField] private float bloomFadeOut = 0.9f;

    [Header("Vignette Target")]
    [SerializeField] private float vignetteTarget = 0.35f;
    [SerializeField] private float vignetteFadeIn = 0.25f;
    [SerializeField] private float vignetteFadeOut = 0.9f;

    private Bloom _bloom;
    private Vignette _vignette;
    private float _originalBloom;
    private float _originalVignette;

    private Tween _bloomTween;
    private Tween _vignetteTween;

    private bool _subscribed;

    // ─── Lifecycle ────────────────────────────────────────────

    private void Awake()
    {
        if (!CacheOverrides())
        {
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        if (!enabled) return;
        TrySubscribe();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        KillTweens();
        Unsubscribe();
    }

    private void OnDestroy()
    {
        KillTweens();
        Unsubscribe();
    }

    // ─── Volume cache ─────────────────────────────────────────

    private bool CacheOverrides()
    {
        if (volume == null || volume.profile == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[BoostPostProcess] Volume or profile is null. Disabling component.");
#endif
            return false;
        }

        bool hasBloom = volume.profile.TryGet(out _bloom);
        bool hasVignette = volume.profile.TryGet(out _vignette);

        if (!hasBloom || !hasVignette)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BoostPostProcess] Missing override — Bloom:{hasBloom} Vignette:{hasVignette}. Disabling component.");
#endif
            return false;
        }

        _originalBloom = _bloom.intensity.value;
        _originalVignette = _vignette.intensity.value;
        return true;
    }

    // ─── Subscription with retry coroutine ────────────────────

    private void TrySubscribe()
    {
        if (_subscribed) return;

        if (BoostModeController.Instance != null)
        {
            Subscribe();
        }
        else
        {
            StartCoroutine(RetrySubscribe());
        }
    }

    private IEnumerator RetrySubscribe()
    {
        float elapsed = 0f;
        const float maxWait = 2f;
        const float interval = 0.25f;

        while (elapsed < maxWait)
        {
            yield return new WaitForSeconds(interval);
            elapsed += interval;

            if (BoostModeController.Instance != null)
            {
                Subscribe();
                yield break;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning("[BoostPostProcess] BoostModeController.Instance not found after retry. Post-process effects will not work.");
#endif
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        var bmc = BoostModeController.Instance;
        if (bmc == null) return;

        bmc.OnBoostStarted += HandleBoostStarted;
        bmc.OnBoostEnded += HandleBoostEnded;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        var bmc = BoostModeController.Instance;
        if (bmc != null)
        {
            bmc.OnBoostStarted -= HandleBoostStarted;
            bmc.OnBoostEnded -= HandleBoostEnded;
        }
        _subscribed = false;
    }

    // ─── Event handlers ───────────────────────────────────────

    private void HandleBoostStarted(float duration)
    {
        KillTweens();

        _bloomTween = DOTween.To(
            () => _bloom.intensity.value,
            x => _bloom.intensity.value = x,
            bloomTarget,
            bloomFadeIn
        ).SetEase(Ease.OutCubic).SetLink(gameObject);

        _vignetteTween = DOTween.To(
            () => _vignette.intensity.value,
            x => _vignette.intensity.value = x,
            vignetteTarget,
            vignetteFadeIn
        ).SetEase(Ease.OutCubic).SetLink(gameObject);
    }

    private void HandleBoostEnded()
    {
        KillTweens();

        _bloomTween = DOTween.To(
            () => _bloom.intensity.value,
            x => _bloom.intensity.value = x,
            _originalBloom,
            bloomFadeOut
        ).SetEase(Ease.InOutCubic).SetLink(gameObject);

        _vignetteTween = DOTween.To(
            () => _vignette.intensity.value,
            x => _vignette.intensity.value = x,
            _originalVignette,
            vignetteFadeOut
        ).SetEase(Ease.InOutCubic).SetLink(gameObject);
    }

    // ─── Tween cleanup ────────────────────────────────────────

    private void KillTweens()
    {
        _bloomTween?.Kill();
        _bloomTween = null;
        _vignetteTween?.Kill();
        _vignetteTween = null;
    }
}
