// ════════════════════════════════════════════════════════════════
// CinematicShotSO.cs – Data definition for one cinematic camera shot.
//
// Create via:  Assets ▸ Create ▸ Cinematic ▸ Shot
//
// This ScriptableObject holds timing, easing, FOV, micro-drift, and
// optional post-process hints.  Scene-specific spatial data (anchor
// transforms, path waypoints) lives on CarShowcaseDirector's ShotEntry.
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using DG.Tweening;

/// <summary>Post-process look hint for future ShowcasePostProcessController.</summary>
public enum ShowcasePostProcessHint
{
    None,
    ShallowDOF,
    BloomEmphasis,
    Vignette,
    Dramatic
}

[CreateAssetMenu(fileName = "NewShot", menuName = "Cinematic/Shot")]
public class CinematicShotSO : ScriptableObject
{
    // ─────────────────── Identity ───────────────────
    [Header("── Identity ──")]
    [Tooltip("Editor-only label (e.g. 'Front Hero', 'Wheel Close-Up').")]
    public string shotName;

    // ─────────────────── Timing ───────────────────
    [Header("── Transition ──")]
    [Tooltip("Seconds to move the camera FROM the previous shot TO this shot's anchor.")]
    [Min(0f)] public float transitionDuration = 1.5f;

    [Tooltip("DOTween easing curve for the position + rotation transition.")]
    public Ease transitionEase = Ease.InOutCubic;

    [Header("── Hold ──")]
    [Tooltip("Seconds the camera stays at this shot before transitioning to the next.")]
    [Min(0f)] public float holdDuration = 2.0f;

    // ─────────────────── FOV ───────────────────
    [Header("── Field of View ──")]
    [Tooltip("Target FOV for this shot.  0 = keep whatever FOV the camera already has.")]
    [Range(0f, 120f)] public float fieldOfView;

    [Tooltip("Seconds for the FOV change.  0 = match transitionDuration.")]
    [Min(0f)] public float fovTransitionDuration;

    /// <summary>Effective FOV transition time (falls back to transitionDuration).</summary>
    public float EffectiveFovDuration =>
        fovTransitionDuration > 0f ? fovTransitionDuration : transitionDuration;

    // ─────────────────── Path Movement ───────────────────
    [Header("── Path Movement ──")]
    [Tooltip("If true, the camera follows a CatmullRom spline through the ShotEntry's pathPoints " +
             "instead of a direct move to the anchor.")]
    public bool usePathMovement;

    // ─────────────────── Micro-Drift During Hold ───────────────────
    [Header("── Hold Micro-Drift ──")]
    [Tooltip("Subtle world-space position drift applied during the hold period. " +
             "Small values (0.1–0.5) prevent a 'frozen' feeling.")]
    public Vector3 localMotionDuringHold;

    [Tooltip("Subtle euler rotation drift during hold (degrees). " +
             "e.g. (0, 2, 0) = slow 2° pan right over the hold duration.")]
    public Vector3 localRotationDuringHold;

    [Tooltip("Easing for the micro-drift movement.")]
    public Ease driftEase = Ease.InOutSine;

    // ─────────────────── Camera Emphasis ───────────────────
    [Header("── Camera Emphasis ──")]
    [Tooltip("If true, the FOV overshoots then settles when arriving at this shot (reveal punch).")]
    public bool fovPunchOnEntry;

    [Tooltip("Extra FOV degrees to overshoot before settling.  Only used when fovPunchOnEntry is true.")]
    [Range(0f, 15f)] public float fovPunchOvershoot = 6f;

    [Tooltip("If true, a subtle camera shake plays when arriving at this shot.")]
    public bool shakeOnEntry;

    [Tooltip("Shake strength (position offset in world units).  Small values recommended (0.01–0.06).")]
    [Range(0f, 0.2f)] public float shakeStrength = 0.03f;

    [Tooltip("Shake duration in seconds.")]
    [Range(0f, 1f)] public float shakeDuration = 0.25f;

    [Tooltip("Shake vibrato (frequency).  10–20 feels mechanical;  5–8 feels cinematic.")]
    [Range(1, 30)] public int shakeVibrato = 8;

    // ─────────────────── Audio ───────────────────
    [Header("── Audio ──")]
    [Tooltip("Optional clip played at the start of this shot (whoosh, rev, sting).")]
    public AudioClip sfxClip;

    // ─────────────────── Post-Process ───────────────────
    [Header("── Post-Process ──")]
    [Tooltip("Post-process look hint.  Drives Bloom / Vignette / DOF via ShowcasePostProcessController.")]
    public ShowcasePostProcessHint postProcessHint = ShowcasePostProcessHint.None;
}
