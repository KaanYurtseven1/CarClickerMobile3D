// ════════════════════════════════════════════════════════════════
// CarShowcaseDirector.cs – Master sequencer for the cinematic
// car showcase in TakeTheCarScene.
//
// Plays shots one at a time.  Each shot has a smooth transition
// followed by a hold period with optional micro-drift.  Tapping
// the screen during a hold advances to the next shot immediately
// with a smooth transition.
//
// Attach to a dedicated "ShowcaseDirector" GameObject in the scene.
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

public class CarShowcaseDirector : MonoBehaviour
{
    // ─────────────────── Shot Entry ───────────────────
    [System.Serializable]
    public struct ShotEntry
    {
        [Tooltip("Shot data asset (timing, FOV, drift, easing).")]
        public CinematicShotSO data;

        [Tooltip("Scene transform defining camera position + rotation for this shot.")]
        public Transform anchor;

        [Tooltip("Optional intermediate transforms for curved path movement (orbit shots). " +
                 "Only used when data.usePathMovement is true.")]
        public Transform[] pathPoints;
    }

    // ─────────────────── References ───────────────────
    [Header("── References ──")]
    [Tooltip("The camera to control.  If unset, Camera.main is used.")]
    [SerializeField] private Camera cam;

    [Tooltip("The car root transform (for future extensions).")]
    [SerializeField] private Transform carRoot;

    [Tooltip("Fade overlay controller.  Optional — kept for fade-out at the end.")]
    [SerializeField] private ShowcaseFadeController fadeController;

    [Tooltip("Post-process controller.  Optional — drives per-shot Bloom / Vignette / DOF.")]
    [SerializeField] private ShowcasePostProcessController postProcessController;

    [Tooltip("Car name reveal controller.  Optional — animates brand + model text.")]
    [SerializeField] private ShowcaseCarNameReveal carNameReveal;

    [Tooltip("Particle system to burst-play at the very start of the cinematic (confetti / sparkles). Optional.")]
    [SerializeField] private ParticleSystem introParticles;

    // ─────────────────── Shots ───────────────────
    [Header("── Shots ──")]
    [Tooltip("Ordered list of cinematic shots.  Played 0 → N.")]
    [SerializeField] private ShotEntry[] shots;

    // ─────────────────── Timing ───────────────────
    [Header("── Global Timing ──")]
    [Tooltip("Duration of the final fade-out to black.")]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Tooltip("Play the sequence automatically when the scene starts. " +
             "Disable when using ShowcaseCarSpawner (it calls Play() after car setup).")]
    [SerializeField] private bool autoStart = false;

    // ─────────────────── Events ───────────────────
    [Header("── Events ──")]
    [Tooltip("Fires after the last shot completes and fade-out finishes.")]
    public UnityEvent onComplete;

    // ═══════════════════════════════════════════════════════════
    //  Runtime
    // ═══════════════════════════════════════════════════════════
    private Sequence _shotSeq;          // tween(s) for the CURRENT shot
    private int _currentShotIndex = -1;
    private bool _isPlaying;
    private bool _isTransitioning;      // true during move to next anchor
    private bool _isFinishing;          // true once FinishCinematic() has been entered

    /// <summary>True while the cinematic is active.</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>Index of the shot currently showing.</summary>
    public int CurrentShotIndex => _currentShotIndex;

    /// <summary>Total number of shots.</summary>
    public int ShotCount => shots != null ? shots.Length : 0;

    /// <summary>Set the car root transform at runtime.</summary>
    public Transform CarRoot { set => carRoot = value; }

    /// <summary>Car name reveal controller (for spawner wiring).</summary>
    public ShowcaseCarNameReveal CarNameReveal => carNameReveal;

    // ─── Lifecycle ───

    private void Awake()
    {
        DOTween.Init(false, true, null);
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;
        if (autoStart) Play();
    }

    private void OnDestroy()
    {
        KillCurrentShot();
    }

    // ═══════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════

    /// <summary>Start the cinematic from shot 0.</summary>
    public void Play()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[CarShowcaseDirector] No camera assigned and Camera.main is null.");
            if (fadeController != null) fadeController.SetClear();
            onComplete?.Invoke();
            return;
        }

        // Clear fade overlay immediately — no black screen.
        if (fadeController != null) fadeController.SetClear();

        if (postProcessController != null) postProcessController.ApplyDefaults();

        _isPlaying = true;
        _isFinishing = false;
        _currentShotIndex = -1;

        // K1: Cinematic start SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayCinematicReveal();

        // Fire optional extras at cinematic start
        if (introParticles != null) introParticles.Play();
        if (carNameReveal != null) carNameReveal.Play();

        // Jump straight to the first shot.
        GoToNextShot();
    }

    /// <summary>
    /// Advance to the next shot with a smooth transition.
    /// If already on the last shot, finishes the cinematic.
    /// Safe to call during a hold OR during a transition
    /// (the current tween is killed and replaced).
    /// </summary>
    public void GoToNextShot()
    {
        if (_isFinishing) return;          // already winding down

        int nextIndex = _currentShotIndex + 1;
        int shotCount = shots != null ? shots.Length : 0;

        if (nextIndex >= shotCount)
        {
            // Past the last shot — finish the cinematic.
            FinishCinematic();
            return;
        }

        KillCurrentShot();
        _currentShotIndex = nextIndex;

        ShotEntry shot = shots[_currentShotIndex];
        if (shot.data == null || shot.anchor == null)
        {
            Debug.LogWarning($"[CarShowcaseDirector] Shot {_currentShotIndex} has null data/anchor — skipping.");
            GoToNextShot();
            return;
        }

        // Play SFX
        if (shot.data.sfxClip != null) PlaySFX(shot.data.sfxClip);

        // Build a sequence for this single shot: transition → emphasis → hold
        _shotSeq = DOTween.Sequence()
            .SetUpdate(UpdateType.Normal, true)
            .SetLink(gameObject);

        // Always smooth-transition from the camera's current position.
        AppendTransition(_shotSeq, shot);
        _isTransitioning = true;
        _shotSeq.AppendCallback(() => _isTransitioning = false);

        // Post-process
        AppendPostProcess(_shotSeq, shot);

        // Camera emphasis (FOV punch, shake)
        AppendEmphasis(_shotSeq, shot);

        // Hold with micro-drift, then auto-advance when hold ends.
        AppendHold(_shotSeq, shot);

        _shotSeq.AppendCallback(() => GoToNextShot());
        _shotSeq.Play();
    }

    /// <summary>Instantly complete the cinematic (skip everything).</summary>
    public void Skip()
    {
        if (!_isPlaying) return;
        KillCurrentShot();
        FinishCinematic();
    }

    /// <summary>Pause the cinematic.</summary>
    public void Pause()
    {
        if (_shotSeq != null && _shotSeq.IsActive()) _shotSeq.Pause();
    }

    /// <summary>Resume a paused cinematic.</summary>
    public void Resume()
    {
        if (_shotSeq != null && _shotSeq.IsActive()) _shotSeq.Play();
    }

    // ═══════════════════════════════════════════════════════════
    //  Finish
    // ═══════════════════════════════════════════════════════════

    private void FinishCinematic()
    {
        if (_isFinishing) return;
        _isFinishing = true;

        // K2: Cinematic finish SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayCinematicFadeOut();

        KillCurrentShot();

        if (fadeController != null)
        {
            fadeController.CreateFadeOut(fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    _isPlaying = false;
                    onComplete?.Invoke();
                });
        }
        else
        {
            _isPlaying = false;
            onComplete?.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Shot Tween Builders
    // ═══════════════════════════════════════════════════════════

    private void AppendTransition(Sequence seq, ShotEntry shot)
    {
        float dur = shot.data.transitionDuration;
        Ease ease = shot.data.transitionEase;

        if (shot.data.usePathMovement
            && shot.pathPoints != null
            && shot.pathPoints.Length > 0)
        {
            var pts = new Vector3[shot.pathPoints.Length + 1];
            for (int j = 0; j < shot.pathPoints.Length; j++)
            {
                if (shot.pathPoints[j] != null)
                    pts[j] = shot.pathPoints[j].position;
            }
            pts[pts.Length - 1] = shot.anchor.position;

            seq.Append(cam.transform.DOPath(pts, dur, PathType.CatmullRom).SetEase(ease));
            seq.Join(cam.transform.DORotateQuaternion(shot.anchor.rotation, dur).SetEase(ease));
        }
        else
        {
            seq.Append(cam.transform.DOMove(shot.anchor.position, dur).SetEase(ease));
            seq.Join(cam.transform.DORotateQuaternion(shot.anchor.rotation, dur).SetEase(ease));
        }

        if (shot.data.fieldOfView > 0f)
        {
            float fovDur = shot.data.EffectiveFovDuration;
            seq.Join(
                DOTween.To(() => cam.fieldOfView, x => cam.fieldOfView = x,
                    shot.data.fieldOfView, fovDur).SetEase(Ease.InOutSine));
        }
    }

    private void AppendPostProcess(Sequence seq, ShotEntry shot)
    {
        if (postProcessController == null) return;
        if (shot.data.postProcessHint == ShowcasePostProcessHint.None) return;

        float dur = Mathf.Max(shot.data.transitionDuration, 0.3f);
        seq.Join(postProcessController.CreateShotTransition(shot.data.postProcessHint, dur));
    }

    private void AppendEmphasis(Sequence seq, ShotEntry shot)
    {
        if (shot.data.fovPunchOnEntry && shot.data.fieldOfView > 0f)
        {
            float target = shot.data.fieldOfView;
            float overshoot = target + shot.data.fovPunchOvershoot;

            var punchSeq = DOTween.Sequence();
            punchSeq.Append(
                DOTween.To(() => cam.fieldOfView, x => cam.fieldOfView = x,
                    overshoot, 0.35f).SetEase(Ease.OutQuart));
            punchSeq.Append(
                DOTween.To(() => cam.fieldOfView, x => cam.fieldOfView = x,
                    target, 0.45f).SetEase(Ease.OutSine));
            seq.Append(punchSeq);
        }

        if (shot.data.shakeOnEntry)
        {
            seq.Append(cam.transform.DOShakePosition(
                shot.data.shakeDuration, shot.data.shakeStrength,
                shot.data.shakeVibrato, 40f, false, true));
        }
    }

    private void AppendHold(Sequence seq, ShotEntry shot)
    {
        float hold = shot.data.holdDuration;
        if (hold <= 0f) return;

        Vector3 posDrift = shot.data.localMotionDuringHold;
        Vector3 rotDrift = shot.data.localRotationDuringHold;
        bool hasPos = posDrift.sqrMagnitude > 0.0001f;
        bool hasRot = rotDrift.sqrMagnitude > 0.0001f;

        if (hasPos || hasRot)
        {
            Ease driftEase = shot.data.driftEase;
            if (hasPos)
                seq.Append(cam.transform.DOBlendableMoveBy(posDrift, hold).SetEase(driftEase));
            if (hasRot)
            {
                var rotTween = cam.transform.DOBlendableRotateBy(rotDrift, hold).SetEase(driftEase);
                if (hasPos) seq.Join(rotTween); else seq.Append(rotTween);
            }
        }
        else
        {
            seq.AppendInterval(hold);
        }
    }

    // ─── Helpers ───

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (SFXManager.Instance != null && SFXManager.Instance.sfxSource != null)
            SFXManager.Instance.sfxSource.PlayOneShot(clip);
        else
            AudioSource.PlayClipAtPoint(clip, cam.transform.position);
    }

    private void KillCurrentShot()
    {
        if (_shotSeq != null && _shotSeq.IsActive())
            _shotSeq.Kill();
        _shotSeq = null;
        _isTransitioning = false;

        // Also kill any orphaned tweens on the camera.
        if (cam != null) DOTween.Kill(cam.transform);
    }

    // ═══════════════════════════════════════════════════════════
    //  Gizmos
    // ═══════════════════════════════════════════════════════════

    private void OnDrawGizmos()
    {
        if (shots == null) return;

        for (int i = 0; i < shots.Length; i++)
        {
            var shot = shots[i];
            if (shot.anchor == null) continue;

            Gizmos.color = new Color(1f, 0.8f, 0f, 0.8f);
            Gizmos.DrawWireSphere(shot.anchor.position, 0.3f);

            Gizmos.color = new Color(0f, 0.6f, 1f, 0.7f);
            Gizmos.DrawRay(shot.anchor.position, shot.anchor.forward * 2f);

            if (i < shots.Length - 1 && shots[i + 1].anchor != null)
            {
                if (shots[i + 1].data != null
                    && shots[i + 1].data.usePathMovement
                    && shots[i + 1].pathPoints != null
                    && shots[i + 1].pathPoints.Length > 0)
                {
                    Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
                    Vector3 prev = shot.anchor.position;
                    foreach (var wp in shots[i + 1].pathPoints)
                    {
                        if (wp == null) continue;
                        Gizmos.DrawLine(prev, wp.position);
                        Gizmos.DrawWireSphere(wp.position, 0.15f);
                        prev = wp.position;
                    }
                    Gizmos.DrawLine(prev, shots[i + 1].anchor.position);
                }
                else
                {
                    Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
                    Gizmos.DrawLine(shot.anchor.position, shots[i + 1].anchor.position);
                }
            }

#if UNITY_EDITOR
            string label = shot.data != null && !string.IsNullOrEmpty(shot.data.shotName)
                ? shot.data.shotName : $"Shot {i}";
            UnityEditor.Handles.Label(shot.anchor.position + Vector3.up * 0.5f, label);
#endif
        }
    }
}
