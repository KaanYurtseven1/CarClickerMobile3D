// ═══════════════════════════════════════════════════════════════════════
// BoostBarFeedbackController.cs
// Phase 3B+C — Premium pulse on BoostBar & nitro micro-feedback
//
// SETUP:
//   1) Add this component to the Slider_BoostBar root (or same GO as
//      BoostModeController).
//   2) Inspector assignments:
//        sliderRoot  = Slider_BoostBar GameObject's Transform
//        pulseTarget = Fill area RectTransform (preferred) or sliderRoot
//        glowOverlay = (optional) CanvasGroup overlay for glow flash
//   3) If pulseTarget is left null, sliderRoot is used as the pulse target.
//   4) glowOverlay is optional; if null, only the pop feedback plays on collect.
// ═══════════════════════════════════════════════════════════════════════

using UnityEngine;
using System.Collections;
using DG.Tweening;

public class BoostBarFeedbackController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform sliderRoot;
    [SerializeField] private RectTransform pulseTarget;
    [SerializeField] private CanvasGroup glowOverlay;

    [Header("Pulse Settings")]
    [SerializeField] private float pulseScale = 1.06f;
    [SerializeField] private float pulseDuration = 0.6f;

    [Header("Nitro Pop Settings")]
    [SerializeField] private float popPunch = 0.06f;
    [SerializeField] private float popDuration = 0.18f;

    [Header("Glow Settings")]
    [SerializeField] private float glowPeak = 0.6f;
    [SerializeField] private float glowDuration = 0.25f;

    private Transform _effectTarget;
    private Tween _pulseTween;
    private Tween _popTween;
    private Sequence _glowSequence;

    private bool _subscribed;
    private bool _pulsing;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool _warnedMissingTarget;
#endif

    // ─── Lifecycle ────────────────────────────────────────────

    private void Awake()
    {
        ResolveTarget();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        KillAllTweens();
        ResetScale();
        Unsubscribe();
    }

    private void OnDestroy()
    {
        KillAllTweens();
        Unsubscribe();
    }

    // ─── Target resolution ────────────────────────────────────

    private void ResolveTarget()
    {
        if (pulseTarget != null)
        {
            _effectTarget = pulseTarget;
        }
        else if (sliderRoot != null)
        {
            _effectTarget = sliderRoot;
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_warnedMissingTarget)
            {
                _warnedMissingTarget = true;
                Debug.LogWarning("[BoostBarFeedback] Both sliderRoot and pulseTarget are null. Feedback disabled.");
            }
#endif
        }

        // Ensure glow overlay starts invisible
        if (glowOverlay != null)
            glowOverlay.alpha = 0f;
    }

    // ─── Subscription with retry ──────────────────────────────

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
        Debug.LogWarning("[BoostBarFeedback] BoostModeController.Instance not found after retry. Feedback will not work.");
#endif
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        var bmc = BoostModeController.Instance;
        if (bmc == null) return;

        bmc.OnStateChanged += HandleStateChanged;
        bmc.OnNitroChargeAccepted += HandleNitroChargeAccepted;
        bmc.OnBoostStarted += HandleBoostStarted;
        bmc.OnBoostEnded += HandleBoostEnded;
        _subscribed = true;

        // Evaluate current state immediately in case we subscribed late
        EvaluatePulseForState(GetCurrentBoostState());
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        var bmc = BoostModeController.Instance;
        if (bmc != null)
        {
            bmc.OnStateChanged -= HandleStateChanged;
            bmc.OnNitroChargeAccepted -= HandleNitroChargeAccepted;
            bmc.OnBoostStarted -= HandleBoostStarted;
            bmc.OnBoostEnded -= HandleBoostEnded;
        }
        _subscribed = false;
    }

    // ─── State helpers ────────────────────────────────────────

    /// <summary>
    /// Reads current state via the public IsBoostActive flag 
    /// and Charge01 to infer state. Used only as a late-subscribe fallback.
    /// </summary>
    private BoostModeController.BoostState GetCurrentBoostState()
    {
        var bmc = BoostModeController.Instance;
        if (bmc == null) return BoostModeController.BoostState.Locked;
        if (bmc.IsBoostActive) return BoostModeController.BoostState.Active;
        // Can't fully determine externally — return Charging as safe default for pulse
        return BoostModeController.BoostState.Charging;
    }

    // ─── Event handlers ───────────────────────────────────────

    private void HandleStateChanged(BoostModeController.BoostState newState)
    {
        EvaluatePulseForState(newState);
    }

    private void HandleBoostStarted(float duration)
    {
        // Fallback safety: stop pulse during Active
        StopPulse();
    }

    private void HandleBoostEnded()
    {
        // Fallback safety: stop pulse during Cooldown
        StopPulse();
    }

    private void HandleNitroChargeAccepted()
    {
        PlayNitroFeedback();
    }

    // ─── Pulse logic ──────────────────────────────────────────

    private void EvaluatePulseForState(BoostModeController.BoostState state)
    {
        switch (state)
        {
            case BoostModeController.BoostState.Charging:
            case BoostModeController.BoostState.Ready:
                StartPulse();
                break;

            default: // Locked, Active, Cooldown
                StopPulse();
                break;
        }
    }

    private void StartPulse()
    {
        if (_pulsing) return;
        if (_effectTarget == null) return;

        _pulsing = true;
        _effectTarget.localScale = Vector3.one;

        _pulseTween = _effectTarget
            .DOScale(Vector3.one * pulseScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    private void StopPulse()
    {
        if (!_pulsing) return;
        _pulsing = false;

        _pulseTween?.Kill();
        _pulseTween = null;

        ResetScale();
    }

    // ─── Nitro micro-feedback ─────────────────────────────────

    private void PlayNitroFeedback()
    {
        if (_effectTarget == null) return;

        // Pop
        _popTween?.Kill();
        _popTween = _effectTarget
            .DOPunchScale(Vector3.one * popPunch, popDuration, 1, 0f)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                // If pulsing, let pulse tween handle scale; otherwise reset
                if (!_pulsing && _effectTarget != null)
                    _effectTarget.localScale = Vector3.one;
            });

        // Glow overlay
        if (glowOverlay != null)
        {
            _glowSequence?.Kill();
            glowOverlay.alpha = 0f;

            _glowSequence = DOTween.Sequence()
                .Append(DOTween.To(() => glowOverlay.alpha, x => glowOverlay.alpha = x, glowPeak, glowDuration * 0.4f))
                .Append(DOTween.To(() => glowOverlay.alpha, x => glowOverlay.alpha = x, 0f, glowDuration * 0.6f))
                .SetLink(gameObject);
        }
    }

    // ─── Cleanup ──────────────────────────────────────────────

    private void ResetScale()
    {
        if (_effectTarget != null)
            _effectTarget.localScale = Vector3.one;
    }

    private void KillAllTweens()
    {
        _pulseTween?.Kill();
        _pulseTween = null;
        _popTween?.Kill();
        _popTween = null;
        _glowSequence?.Kill();
        _glowSequence = null;
        _pulsing = false;
    }
}
