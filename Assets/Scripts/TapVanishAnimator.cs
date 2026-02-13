using UnityEngine;
using System;
using DG.Tweening;

/// <summary>
/// Reusable "pop then shrink to zero" disappear animation for tappable objects.
/// Attach to any prefab (Chest, NitroCoin, Radar, etc.) alongside the main script.
/// Call Play(onComplete) from the tap handler; the callback runs after the animation finishes.
///
/// If shakeOnTap is enabled, a subtle position shake plays alongside the pop phase.
/// All tweens are killed on destroy to prevent leaks.
/// </summary>
public class TapVanishAnimator : MonoBehaviour
{
    [Header("Pop Phase (scale up)")]
    [Tooltip("Target scale multiplier for the initial pop.")]
    [SerializeField] private float popScale = 1.12f;
    [Tooltip("Duration of the pop phase (seconds).")]
    [SerializeField] private float popDuration = 0.08f;
    [Tooltip("Ease for the pop phase.")]
    [SerializeField] private Ease popEase = Ease.OutQuad;

    [Header("Shrink Phase (scale to zero)")]
    [Tooltip("Duration of the shrink-to-zero phase (seconds).")]
    [SerializeField] private float shrinkDuration = 0.12f;
    [Tooltip("Ease for the shrink phase.")]
    [SerializeField] private Ease shrinkEase = Ease.InBack;

    [Header("Optional Shake")]
    [Tooltip("Enable a subtle position shake during the pop phase.")]
    [SerializeField] private bool shakeOnTap = false;
    [Tooltip("Shake duration (seconds).")]
    [SerializeField] private float shakeDuration = 0.25f;
    [Tooltip("Shake strength (local-space units).")]
    [SerializeField] private float shakeStrength = 0.1f;
    [Tooltip("Vibrato (number of oscillations).")]
    [SerializeField] private int shakeVibrato = 10;

    private Sequence _sequence;
    private bool _isPlaying;
    private Vector3 _initialScale;

    /// <summary>True while the vanish animation is playing. Use to block double-taps.</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>The localScale captured on Awake, before any animation.</summary>
    public Vector3 InitialScale => _initialScale;

    private void Awake()
    {
        _initialScale = transform.localScale;
    }

    /// <summary>
    /// Immediately restores localScale to the value captured on Awake.
    /// Call this after the vanish animation if the object is NOT being destroyed.
    /// </summary>
    public void ResetScale()
    {
        KillSequence();
        transform.localScale = _initialScale;
    }

    /// <summary>
    /// Plays the pop+shrink animation. Calls onComplete when finished.
    /// Safe to call multiple times — subsequent calls are ignored while playing.
    /// </summary>
    public void Play(Action onComplete = null)
    {
        if (_isPlaying) return;
        _isPlaying = true;

        KillSequence();

        // Always start from the original scale to avoid stale 0-scale
        transform.localScale = _initialScale;

        _sequence = DOTween.Sequence();

        // Phase 1: pop up
        _sequence.Append(
            transform.DOScale(Vector3.one * popScale, popDuration)
                .SetEase(popEase));

        // Optional shake (joined with pop so they overlap)
        if (shakeOnTap)
        {
            _sequence.Join(
                transform.DOShakePosition(shakeDuration, shakeStrength, shakeVibrato, 90f, false, true)
                    .SetEase(Ease.OutSine));
        }

        // Phase 2: shrink to zero
        _sequence.Append(
            transform.DOScale(Vector3.zero, shrinkDuration)
                .SetEase(shrinkEase));

        _sequence.OnComplete(() =>
        {
            _isPlaying = false;
            _sequence = null;
            onComplete?.Invoke();
        });

        _sequence.OnKill(() =>
        {
            _isPlaying = false;
            _sequence = null;
        });
    }

    private void KillSequence()
    {
        if (_sequence != null && _sequence.IsActive())
        {
            _sequence.Kill();
            _sequence = null;
        }
        // Also kill any stray tweens on this transform
        transform.DOKill();
    }

    private void OnDestroy()
    {
        KillSequence();
    }
}
