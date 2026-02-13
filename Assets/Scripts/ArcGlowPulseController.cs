using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

/// <summary>
/// Triggers a short Bloom "pulse" on the Global Volume whenever at least one
/// ArcLineVFX is active.  Uses ref-counting so overlapping arcs are safe.
///
/// SETUP:
///   1) Attach to the same GameObject that has the Global URP Volume
///      (the one BoostPostProcessController references).
///   2) Assign the Volume field (or leave null for GetComponent fallback).
///   3) No material / shader changes required.
///
/// PRIORITY RULES:
///   • Boost mode ALWAYS wins.  If boost is active the arc pulse does nothing.
///   • Subscribes to BoostModeController events so it can kill its own tween
///     instantly if boost starts mid-pulse (avoids fighting).
/// </summary>
public class ArcGlowPulseController : MonoBehaviour
{
    public static ArcGlowPulseController Instance { get; private set; }

    [SerializeField] private Volume volume;

    [Header("Arc Bloom Pulse")]
    [Tooltip("Added to current Bloom.intensity while arcs are active")]
    [SerializeField] private float arcBloomAdd = 0.25f;

    [Tooltip("Fade-in duration when first arc appears")]
    [SerializeField] private float arcFadeIn = 0.2f;

    [Tooltip("Fade-out duration when last arc disappears")]
    [SerializeField] private float arcFadeOut = 0.35f;

    // ── Runtime ──
    private Bloom _bloom;
    private Tween _arcTween;
    private int _activeArcCount;
    private float _baselineBeforeArc;
    private bool _subscribedToBoost;

    // ── Lifecycle ──────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (volume == null)
            volume = GetComponent<Volume>();

        if (volume != null && volume.profile != null)
            volume.profile.TryGet(out _bloom);
    }

    private void OnEnable()
    {
        TrySubscribeBoost();
    }

    private void OnDisable()
    {
        KillArcTween();
        UnsubscribeBoost();
    }

    private void OnDestroy()
    {
        KillArcTween();
        UnsubscribeBoost();
        if (Instance == this)
            Instance = null;
    }

    // ── Boost event wiring ────────────────────────────────────

    private void TrySubscribeBoost()
    {
        if (_subscribedToBoost) return;
        var bmc = BoostModeController.Instance;
        if (bmc == null) return;

        bmc.OnBoostStarted += HandleBoostStarted;
        bmc.OnBoostEnded += HandleBoostEnded;
        _subscribedToBoost = true;
    }

    private void UnsubscribeBoost()
    {
        if (!_subscribedToBoost) return;
        var bmc = BoostModeController.Instance;
        if (bmc != null)
        {
            bmc.OnBoostStarted -= HandleBoostStarted;
            bmc.OnBoostEnded -= HandleBoostEnded;
        }
        _subscribedToBoost = false;
    }

    /// <summary>Boost takes priority — kill arc tween immediately.</summary>
    private void HandleBoostStarted(float duration)
    {
        KillArcTween();
    }

    /// <summary>
    /// If arcs are still active after boost ends, we do NOT re-assert the pulse.
    /// Boost's own fade-out restores bloom to _originalBloom.  The remaining arc
    /// pull is short-lived and the visual difference is negligible.
    /// </summary>
    private void HandleBoostEnded()
    {
        // Intentionally empty.  See summary above.
    }

    // ── Public API ────────────────────────────────────────────

    /// <summary>Call when an arc becomes active.  Ref-counted.</summary>
    public void BeginArcGlowPulse()
    {
        if (_bloom == null) return;
        if (!_subscribedToBoost) TrySubscribeBoost();

        _activeArcCount++;

        if (_activeArcCount == 1)
        {
            // Boost owns bloom — don't fight it
            if (IsBoostActive()) return;

            _baselineBeforeArc = _bloom.intensity.value;
            KillArcTween();

            float target = _baselineBeforeArc + arcBloomAdd;
            _arcTween = DOTween.To(
                () => _bloom.intensity.value,
                x => _bloom.intensity.value = x,
                target,
                arcFadeIn
            ).SetEase(Ease.OutQuad).SetLink(gameObject);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[VFX] ArcGlowPulse START");
#endif
        }
    }

    /// <summary>Call when an arc is cleaned up / destroyed.  Ref-counted.</summary>
    public void EndArcGlowPulse()
    {
        if (_bloom == null) return;

        _activeArcCount = Mathf.Max(0, _activeArcCount - 1);

        if (_activeArcCount == 0)
        {
            if (IsBoostActive())
            {
                // Boost controls bloom — it will restore on its own
                return;
            }

            KillArcTween();

            _arcTween = DOTween.To(
                () => _bloom.intensity.value,
                x => _bloom.intensity.value = x,
                _baselineBeforeArc,
                arcFadeOut
            ).SetEase(Ease.InQuad).SetLink(gameObject);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[VFX] ArcGlowPulse END");
#endif
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    private bool IsBoostActive()
    {
        return BoostModeController.Instance != null
            && BoostModeController.Instance.IsBoostActive;
    }

    private void KillArcTween()
    {
        _arcTween?.Kill();
        _arcTween = null;
    }
}
