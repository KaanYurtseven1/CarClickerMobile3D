using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using DG.Tweening;

/// <summary>
/// PoliceChaseFeedbackController — Audiovisual adrenaline layer for the police chase minigame.
///
/// Responds to PoliceCatchController's three static events and drives all of:
///   1.  Music / SFX swap    — gameplay music ducks; chase stinger + loop plays
///   2.  Heartbeat SFX       — pitch and volume ramp with DangerFraction
///   3.  Police siren        — pitch and volume ramp with DangerFraction
///   4.  Engine roar         — pitch/volume boosted for the chase duration
///   5.  Post-processing     — red vignette intensifies + subtle desaturation (URP Volume)
///   6.  Camera FOV + shake  — FOV widens at start; continuous Perlin shake scales with danger
///   7.  Police lights       — red/blue emissive material + Light flicker coroutine
///   8.  Screen edge flash   — red border pulses in Last Chance Zone
///   9.  Per-tap haptics     — platform-safe Handheld.Vibrate() on each chase tap
///
/// This component is PURELY REACTIVE.  It does NOT touch TPS logic, win/lose conditions,
/// economy, rewards, penalties, or any other gameplay code in PoliceCatchController.
///
/// Required events fired by PoliceCatchController:
///   • OnChaseStarted       — fires the instant the Chase phase begins
///   • OnChaseEnded         — fires when the chase fully ends (success or fail)
///   • OnChaseTapFeedback   — fires on every valid player tap during the chase
///
/// Inspector setup guide ships with this file — see the Unity Editor Setup Checklist.
/// </summary>
[DefaultExecutionOrder(10)]   // Runs after PoliceCatchController (order 0) so events are wired first
public class PoliceChaseFeedbackController : MonoBehaviour
{
    public static PoliceChaseFeedbackController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    // ═══════════════════════════════════════════════════════════════
    // LAST CHANCE ZONE
    // ═══════════════════════════════════════════════════════════════

    [Header("Last Chance Zone")]
    [Tooltip("DangerFraction threshold (0–1) above which the Last Chance Zone activates.\n" +
             "DangerFraction = policeChaseZ / crashZ  (0 = police far, 1 = crash).\n" +
             "0.75 = police is 75 % of the way to crash.  Drives: screen edge pulse,\n" +
             "heartbeat/siren peak, and camera shake burst.")]
    [SerializeField] private float lastChanceThreshold = 0.75f;

    // ═══════════════════════════════════════════════════════════════
    // MUSIC / SFX SWAP
    // ═══════════════════════════════════════════════════════════════

    [Header("Music / SFX Swap")]
    [Tooltip("The gameplay music AudioSource to duck when the chase starts.\n" +
             "Assign the AudioSource that plays your normal background loop.\n" +
             "Leave null to skip ducking.")]
    [SerializeField] private AudioSource gameplayMusicSource;

    [Tooltip("AudioSource for the one-shot chase stinger (plays once on chase start).\n" +
             "Create a child AudioSource: PlayOnAwake=false, Loop=false.")]
    [SerializeField] private AudioSource chaseStingerSource;

    [Tooltip("AudioSource for the high-BPM chase loop (plays throughout chase).\n" +
             "Create a child AudioSource: PlayOnAwake=false, Loop=true.")]
    [SerializeField] private AudioSource chaseLoopSource;

    [Tooltip("Chase start stinger clip (short, punchy, one-shot).")]
    [SerializeField] private AudioClip chaseStingerClip;

    [Tooltip("High-BPM chase loop clip (looped throughout the entire chase duration).")]
    [SerializeField] private AudioClip chaseLoopClip;

    [Tooltip("Volume to duck gameplay music to during chase (0 = silent, 1 = unchanged).")]
    [SerializeField][Range(0f, 1f)] private float musicDuckVolume = 0.10f;

    [Tooltip("Seconds to fade gameplay music down to duck volume on chase start.")]
    [SerializeField] private float musicDuckFadeTime = 0.40f;

    [Tooltip("Seconds to restore gameplay music volume after chase ends.")]
    [SerializeField] private float musicRestoreFadeTime = 1.20f;

    // ═══════════════════════════════════════════════════════════════
    // HEARTBEAT
    // ═══════════════════════════════════════════════════════════════

    [Header("Heartbeat Layer")]
    [Tooltip("Looping heartbeat AudioSource.\n" +
             "Create a child AudioSource: PlayOnAwake=false, Loop=true.")]
    [SerializeField] private AudioSource heartbeatSource;

    [Tooltip("Heartbeat audio clip (looping heartbeat sound).")]
    [SerializeField] private AudioClip heartbeatClip;

    [Tooltip("Heartbeat volume at the start of the chase (low danger).")]
    [SerializeField][Range(0f, 1f)] private float heartbeatMinVolume = 0.25f;

    [Tooltip("Heartbeat volume at max danger / last chance zone.")]
    [SerializeField][Range(0f, 1f)] private float heartbeatMaxVolume = 1.00f;

    [Tooltip("Heartbeat pitch at low danger (slow, heavy thud feel).")]
    [SerializeField] private float heartbeatMinPitch = 0.80f;

    [Tooltip("Heartbeat pitch at max danger (fast, frantic heartbeat).")]
    [SerializeField] private float heartbeatMaxPitch = 1.40f;

    // ═══════════════════════════════════════════════════════════════
    // POLICE SIREN
    // ═══════════════════════════════════════════════════════════════

    [Header("Police Siren")]
    [Tooltip("Looping siren AudioSource. Can be on the police car or on this GameObject.\n" +
             "PlayOnAwake=false, Loop=true.")]
    [SerializeField] private AudioSource sirenSource;

    [Tooltip("Police siren audio clip (looping wail/siren loop).")]
    [SerializeField] private AudioClip sirenClip;

    [Tooltip("Siren volume when police is far (chase start).")]
    [SerializeField][Range(0f, 1f)] private float sirenMinVolume = 0.20f;

    [Tooltip("Siren volume at max danger /  last chance zone.")]
    [SerializeField][Range(0f, 1f)] private float sirenMaxVolume = 0.90f;

    [Tooltip("Siren pitch at low danger (distant, calm wail).")]
    [SerializeField] private float sirenMinPitch = 0.90f;

    [Tooltip("Siren pitch at max danger (aggressive, urgent escalation).")]
    [SerializeField] private float sirenMaxPitch = 1.30f;

    // ═══════════════════════════════════════════════════════════════
    // ENGINE ROAR
    // ═══════════════════════════════════════════════════════════════

    [Header("Player Engine Roar")]
    [Tooltip("Engine stress AudioSource. PlayOnAwake=false, Loop=true.\n" +
             "Can be a separate 'stressed engine' clip source on this GameObject.")]
    [SerializeField] private AudioSource engineSource;

    [Tooltip("Engine roar / stress audio clip (looping).")]
    [SerializeField] private AudioClip engineRoarClip;

    [Tooltip("Engine pitch when NOT in a chase (resting state).")]
    [SerializeField] private float engineNormalPitch = 1.00f;

    [Tooltip("Engine pitch during chase (higher = more stressed / floored engine).")]
    [SerializeField] private float engineChasePitch = 1.35f;

    [Tooltip("Engine volume when NOT in a chase.")]
    [SerializeField][Range(0f, 1f)] private float engineNormalVolume = 0.45f;

    [Tooltip("Engine volume during chase (louder = more urgent).")]
    [SerializeField][Range(0f, 1f)] private float engineChaseVolume = 0.80f;

    [Tooltip("Time to ramp engine pitch and volume from normal to chase values (and back).")]
    [SerializeField] private float engineRampTime = 0.30f;

    // ═══════════════════════════════════════════════════════════════
    // POST-PROCESSING (URP Volume)
    // ═══════════════════════════════════════════════════════════════

    [Header("Post-Processing (URP Volume)")]
    [Tooltip("The URP Volume whose profile contains Vignette and ColorAdjustments overrides.\n" +
             "This can be the same Volume used by BoostPostProcessController.\n" +
             "Requirements: Volume Profile must have Vignette override enabled.\n" +
             "Optional: ColorAdjustments override enabled for saturation shift.")]
    [SerializeField] private Volume postProcessVolume;

    [Tooltip("Vignette intensity at chase start (police is far, tension is low).")]
    [SerializeField][Range(0f, 1f)] private float vignetteChaseStartIntensity = 0.22f;

    [Tooltip("Vignette intensity when DangerFraction is 1.0 (crash imminent).")]
    [SerializeField][Range(0f, 1f)] private float vignetteChaseMaxIntensity = 0.55f;

    [Tooltip("Vignette center color at low danger.")]
    [SerializeField] private Color vignetteNormalColor = Color.black;

    [Tooltip("Vignette center color biased toward in the last chance zone\n" +
             "(blended toward this color as DangerFraction increases).")]
    [SerializeField] private Color vignetteDangerColor = new Color(0.55f, 0f, 0f, 1f);

    [Tooltip("Saturation value applied via ColorAdjustments during the chase.\n" +
             "Negative = desaturated / washed-out danger feeling.\n" +
             "Recommended: -15 to -25.  Requires ColorAdjustments override in Volume Profile.")]
    [SerializeField][Range(-100f, 0f)] private float chaseSaturationShift = -18f;

    [Tooltip("Seconds to fade in the post-processing effect on chase start.")]
    [SerializeField] private float ppFadeInDuration = 0.45f;

    [Tooltip("Seconds to restore post-processing to normal after chase ends.")]
    [SerializeField] private float ppFadeOutDuration = 1.10f;

    // ═══════════════════════════════════════════════════════════════
    // CAMERA FOV + SHAKE
    // ═══════════════════════════════════════════════════════════════

    [Header("Camera FOV + Shake")]
    [Tooltip("The gameplay perspective camera.\n" +
             "Assign Main Camera.  Must be perspective (FOV-based).")]
    [SerializeField] private Camera gameplayCamera;

    [Tooltip("Extra FOV degrees added at chase start (wider FOV = speed / adrenaline feel).\n" +
             "Recommended: 4–8 degrees. Too high looks fisheye-distorted.")]
    [SerializeField] private float chaseFOVBoost = 5f;

    [Tooltip("Seconds to ease FOV from normal to boosted value on chase start.")]
    [SerializeField] private float fovBoostDuration = 0.50f;

    [Tooltip("Seconds to ease FOV back to normal on chase end.")]
    [SerializeField] private float fovRestoreDuration = 0.75f;

    [Tooltip("Maximum per-axis Perlin-noise amplitude applied to camera localPosition (world units).\n" +
             "Scales up with DangerFraction. Recommended: 0.02–0.06.")]
    [SerializeField] private float cameraShakeAmplitude = 0.035f;

    [Tooltip("Perlin noise oscillation speed. Higher = faster, jitterier shake.")]
    [SerializeField] private float cameraShakeFrequency = 10f;

    [Tooltip("Multiplier applied to shake amplitude when inside the Last Chance Zone.")]
    [SerializeField] private float cameraShakeDangerMultiplier = 2.5f;

    // ═══════════════════════════════════════════════════════════════
    // POLICE LIGHTS FLICKER
    // ═══════════════════════════════════════════════════════════════

    [Header("Police Lights Flicker")]
    [Tooltip("Renderers on the police car whose emissive material should flash red/blue.\n" +
             "The material slot at emissiveMaterialIndex on each Renderer will have its\n" +
             "_EmissionColor swapped between flashColorRed and flashColorBlue.\n" +
             "REQUIRED: 'Emission' keyword must be enabled on the material (tick it in Inspector).")]
    [SerializeField] private Renderer[] policeLightRenderers;

    [Tooltip("Which material slot (0-based) on each Renderer is the emissive light material.")]
    [SerializeField] private int emissiveMaterialIndex = 0;

    [Tooltip("Optional: Light components on the police car to flash.\n" +
             "Works independently of or alongside the emissive material system.")]
    [SerializeField] private Light[] policeLights;

    [Tooltip("Emissive / light color for the Red phase of the flash.")]
    [SerializeField] private Color flashColorRed = new Color(1f, 0.05f, 0.05f, 1f);

    [Tooltip("Emissive / light color for the Blue phase of the flash.")]
    [SerializeField] private Color flashColorBlue = new Color(0.1f, 0.2f, 1f, 1f);

    [Tooltip("HDR brightness multiplier applied to the emissive color\n" +
             "(higher = more visible glow in post-processing bloom).")]
    [SerializeField] private float emissiveIntensityMultiplier = 3.0f;

    [Tooltip("Seconds each flash phase lasts (red → blue → red).\n" +
             "Lower = faster, more frantic flicker.  Recommended: 0.10–0.20.")]
    [SerializeField] private float flashInterval = 0.15f;

    [Tooltip("Intensity for the Light component flicker (if policeLights[] are used).")]
    [SerializeField] private float lightFlickerIntensity = 2.5f;

    // ═══════════════════════════════════════════════════════════════
    // SCREEN EDGE DANGER FLASH
    // ═══════════════════════════════════════════════════════════════

    [Header("Screen Edge Danger Flash")]
    [Tooltip("CanvasGroup wrapping a full-screen red border / edge Image.\n" +
             "Setup: Canvas > Image (stretch to fill, Raycast Target OFF),\n" +
             "use a hollow red-border sprite (9-slice) or a frame texture so only edges show.\n" +
             "Add CanvasGroup component to the Image or a parent. Starts at alpha=0.")]
    [SerializeField] private CanvasGroup screenEdgeOverlay;

    [Tooltip("Peak alpha of the red edge overlay during Last Chance Zone pulse.")]
    [SerializeField][Range(0f, 1f)] private float edgeFlashMaxAlpha = 0.55f;

    [Tooltip("Pulse cycles per second in the Last Chance Zone (higher = more frantic).")]
    [SerializeField] private float edgePulseFrequency = 2.0f;

    [Tooltip("Max subtle pre-warning glow alpha before entering Last Chance Zone\n" +
             "(scales with DangerFraction so there's a gentle build-up).\n" +
             "Set to 0 to only show the edge in the Last Chance Zone.")]
    [SerializeField][Range(0f, 1f)] private float edgePreWarningMaxAlpha = 0.12f;

    // ═══════════════════════════════════════════════════════════════
    // HAPTICS
    // ═══════════════════════════════════════════════════════════════

    [Header("Per-Tap Haptics")]
    [Tooltip("Enable mobile haptic feedback on each chase tap.\n" +
             "Uses Handheld.Vibrate() on Android / iOS.\n" +
             "No effect in Editor or on PC — safe to leave enabled.")]
    [SerializeField] private bool hapticsEnabled = true;

    // ═══════════════════════════════════════════════════════════════
    // DEBUG
    // ═══════════════════════════════════════════════════════════════

    [Header("Debug")]
    [SerializeField] private bool enableFeedbackDebugLogs = false;

    // ═══════════════════════════════════════════════════════════════
    // RUNTIME STATE
    // ═══════════════════════════════════════════════════════════════

    private bool _chaseActive = false;
    private bool _isLastChanceZone = false;

    // Post-processing cached originals
    private Vignette _vignette;
    private ColorAdjustments _colorAdj;
    private float _ppOriginalVignetteIntensity;
    private Color _ppOriginalVignetteColor;
    private float _ppOriginalSaturation;
    private bool _ppInitialized = false;

    // Camera cached originals
    private float _defaultFOV;
    private Vector3 _cameraOriginalLocalPos;
    private bool _cameraDefaultsCached = false;

    // Coroutines
    private Coroutine _shakeCoroutine;
    private Coroutine _lightsCoroutine;

    // Cached material instances for emissive flicker (created once in Awake)
    private Material[] _cachedEmissiveMats;

    // Gameplay music original volume
    private float _originalMusicVolume;

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        CacheEmissiveMaterials();
    }

    private void OnEnable()
    {
        PoliceCatchController.OnChaseStarted += HandleChaseStarted;
        PoliceCatchController.OnChaseEnded += HandleChaseEnded;
        PoliceCatchController.OnChaseTapFeedback += HandleChaseTap;
    }

    private void OnDisable()
    {
        PoliceCatchController.OnChaseStarted -= HandleChaseStarted;
        PoliceCatchController.OnChaseEnded -= HandleChaseEnded;
        PoliceCatchController.OnChaseTapFeedback -= HandleChaseTap;
    }

    private void Start()
    {
        // Cache camera defaults at startup so FOV restore is always precise
        if (gameplayCamera != null && !_cameraDefaultsCached)
        {
            _defaultFOV = gameplayCamera.fieldOfView;
            _cameraOriginalLocalPos = gameplayCamera.transform.localPosition;
            _cameraDefaultsCached = true;
        }
        else if (gameplayCamera == null)
        {
            Debug.LogWarning("[ChaseFeedback] gameplayCamera is not assigned. FOV boost and camera shake will be skipped.");
        }

        // Init post-processing
        _ppInitialized = TryInitPostProcess();

        // Ensure screen edge starts fully hidden
        if (screenEdgeOverlay != null)
            screenEdgeOverlay.alpha = 0f;
    }

    private void Update()
    {
        if (!_chaseActive) return;

        PoliceCatchController pcc = PoliceCatchController.Instance;
        if (pcc == null) return;

        float danger = pcc.DangerFraction;

        // Detect Last Chance Zone entry
        bool wasLastChance = _isLastChanceZone;
        _isLastChanceZone = danger >= lastChanceThreshold;
        if (_isLastChanceZone && !wasLastChance)
            OnEnterLastChanceZone();

        // Per-frame dynamic updates
        UpdateHeartbeat(danger);
        UpdateSiren(danger);

        if (_ppInitialized)
            UpdateVignette(danger);

        UpdateScreenEdge(danger);
    }

    // ═══════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private void HandleChaseStarted()
    {
        _chaseActive = true;
        _isLastChanceZone = false;

        if (enableFeedbackDebugLogs)
            Debug.Log("[ChaseFeedback] Chase started — all feedback systems activating.");

        StartMusicSwap();
        StartHeartbeat();
        StartSiren();
        StartEngineRoar();

        if (_ppInitialized)
            StartPostProcessIn();

        StartCameraEffects();
        StartPoliceLights();
    }

    private void HandleChaseEnded()
    {
        _chaseActive = false;
        _isLastChanceZone = false;

        if (enableFeedbackDebugLogs)
            Debug.Log("[ChaseFeedback] Chase ended — all feedback systems restoring.");

        // P6/P7: Chase outcome stinger
        if (SFXManager.Instance != null)
        {
            bool escaped = PoliceCatchController.Instance != null
                           && PoliceCatchController.Instance.WasLastChaseSuccess;
            if (escaped)
                SFXManager.Instance.PlayChaseSuccess();
            else
                SFXManager.Instance.PlayChaseFail();
        }

        StopMusicSwap();
        StopHeartbeat();
        StopSiren();
        StopEngineRoar();

        if (_ppInitialized)
            StopPostProcessOut();

        StopCameraEffects();
        StopPoliceLights();
        HideScreenEdge();
    }

    private void HandleChaseTap()
    {
        TriggerTapHaptic();
    }

    // ═══════════════════════════════════════════════════════════════
    // LAST CHANCE ZONE
    // ═══════════════════════════════════════════════════════════════

    private void OnEnterLastChanceZone()
    {
        if (enableFeedbackDebugLogs)
            Debug.Log("[ChaseFeedback] ⚠ Last Chance Zone entered — DangerFraction >= " + lastChanceThreshold);

        // All per-frame systems (heartbeat, siren, vignette, screen edge, shake) automatically
        // react to _isLastChanceZone == true on their next Update tick.
        // No extra explicit trigger calls needed here — they already read _isLastChanceZone.
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. MUSIC SWAP
    // ═══════════════════════════════════════════════════════════════

    private void StartMusicSwap()
    {
        // Duck gameplay music
        if (gameplayMusicSource != null)
        {
            _originalMusicVolume = gameplayMusicSource.volume;
            gameplayMusicSource.DOFade(musicDuckVolume, musicDuckFadeTime);
        }

        // Play one-shot stinger
        if (chaseStingerSource != null && chaseStingerClip != null)
        {
            chaseStingerSource.clip = chaseStingerClip;
            chaseStingerSource.loop = false;
            chaseStingerSource.Play();
        }

        // Fade in chase loop
        if (chaseLoopSource != null && chaseLoopClip != null)
        {
            chaseLoopSource.clip = chaseLoopClip;
            chaseLoopSource.loop = true;
            chaseLoopSource.volume = 0f;
            chaseLoopSource.Play();
            chaseLoopSource.DOFade(1f, musicDuckFadeTime + 0.25f);
        }
    }

    private void StopMusicSwap()
    {
        // Restore gameplay music
        if (gameplayMusicSource != null)
            gameplayMusicSource.DOFade(_originalMusicVolume, musicRestoreFadeTime);

        // Fade out and stop chase loop
        if (chaseLoopSource != null && chaseLoopSource.isPlaying)
        {
            chaseLoopSource.DOFade(0f, musicRestoreFadeTime * 0.55f)
                .OnComplete(() => { if (chaseLoopSource != null) chaseLoopSource.Stop(); });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. HEARTBEAT
    // ═══════════════════════════════════════════════════════════════

    private void StartHeartbeat()
    {
        if (heartbeatSource == null || heartbeatClip == null) return;
        heartbeatSource.clip = heartbeatClip;
        heartbeatSource.loop = true;
        heartbeatSource.volume = heartbeatMinVolume;
        heartbeatSource.pitch = heartbeatMinPitch;
        heartbeatSource.Play();
    }

    private void UpdateHeartbeat(float danger)
    {
        if (heartbeatSource == null || !heartbeatSource.isPlaying) return;
        heartbeatSource.volume = Mathf.Lerp(heartbeatMinVolume, heartbeatMaxVolume, danger);
        heartbeatSource.pitch = Mathf.Lerp(heartbeatMinPitch, heartbeatMaxPitch, danger);
    }

    private void StopHeartbeat()
    {
        if (heartbeatSource == null) return;
        heartbeatSource.DOFade(0f, 0.40f)
            .OnComplete(() => { if (heartbeatSource != null) heartbeatSource.Stop(); });
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. POLICE SIREN
    // ═══════════════════════════════════════════════════════════════

    private void StartSiren()
    {
        if (sirenSource == null || sirenClip == null) return;
        sirenSource.clip = sirenClip;
        sirenSource.loop = true;
        sirenSource.volume = sirenMinVolume;
        sirenSource.pitch = sirenMinPitch;
        sirenSource.Play();
    }

    private void UpdateSiren(float danger)
    {
        if (sirenSource == null || !sirenSource.isPlaying) return;
        sirenSource.volume = Mathf.Lerp(sirenMinVolume, sirenMaxVolume, danger);
        sirenSource.pitch = Mathf.Lerp(sirenMinPitch, sirenMaxPitch, danger);
    }

    private void StopSiren()
    {
        if (sirenSource == null) return;
        sirenSource.DOFade(0f, 0.50f)
            .OnComplete(() => { if (sirenSource != null) sirenSource.Stop(); });
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. ENGINE ROAR
    // ═══════════════════════════════════════════════════════════════

    private void StartEngineRoar()
    {
        if (engineSource == null || engineRoarClip == null) return;
        engineSource.clip = engineRoarClip;
        engineSource.loop = true;
        engineSource.pitch = engineNormalPitch;
        engineSource.volume = engineNormalVolume;
        engineSource.Play();

        // Ramp volume and pitch up together
        engineSource.DOFade(engineChaseVolume, engineRampTime);
        DOTween.To(() => engineSource.pitch, x => engineSource.pitch = x, engineChasePitch, engineRampTime);
    }

    private void StopEngineRoar()
    {
        if (engineSource == null) return;
        DOTween.To(() => engineSource.pitch, x => engineSource.pitch = x, engineNormalPitch, engineRampTime);
        engineSource.DOFade(0f, engineRampTime)
            .OnComplete(() => { if (engineSource != null) engineSource.Stop(); });
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. POST-PROCESSING (VIGNETTE + COLOR ADJUSTMENTS)
    // ═══════════════════════════════════════════════════════════════

    private bool TryInitPostProcess()
    {
        if (postProcessVolume == null || postProcessVolume.profile == null)
        {
            Debug.LogWarning("[ChaseFeedback] postProcessVolume not assigned — post-FX skipped. " +
                             "Assign a URP Volume with Vignette override in the Inspector.");
            return false;
        }

        bool hasVignette = postProcessVolume.profile.TryGet(out _vignette);
        if (!hasVignette)
        {
            Debug.LogWarning("[ChaseFeedback] Volume Profile is missing a Vignette override. " +
                             "Open the Volume Profile asset and add a Vignette override.");
            return false;
        }

        // ColorAdjustments is optional — missing it only skips saturation shift
        bool hasColor = postProcessVolume.profile.TryGet(out _colorAdj);
        if (!hasColor)
            Debug.LogWarning("[ChaseFeedback] Volume Profile has no ColorAdjustments override. " +
                             "Saturation shift will be skipped (Vignette still works).");

        _ppOriginalVignetteIntensity = _vignette.intensity.value;
        _ppOriginalVignetteColor = _vignette.color.value;
        _ppOriginalSaturation = (_colorAdj != null) ? _colorAdj.saturation.value : 0f;
        return true;
    }

    private void StartPostProcessIn()
    {
        // Vignette intensity is handled entirely per-frame in UpdateVignette().
        // Here we only tween the one-shot saturation shift.
        if (_colorAdj != null)
        {
            DOTween.To(
                () => _colorAdj.saturation.value,
                v => _colorAdj.saturation.Override(v),
                chaseSaturationShift,
                ppFadeInDuration
            );
        }
    }

    private void UpdateVignette(float danger)
    {
        if (_vignette == null) return;

        // Intensity: smoothly tracks danger fraction between start-intensity and max-intensity
        float targetIntensity = Mathf.Lerp(vignetteChaseStartIntensity, vignetteChaseMaxIntensity, danger);
        float newIntensity = Mathf.MoveTowards(
            _vignette.intensity.value, targetIntensity, Time.deltaTime * 2.0f);
        _vignette.intensity.Override(newIntensity);

        // Color: blends from neutral black toward the red danger tint with danger fraction
        _vignette.color.Override(Color.Lerp(vignetteNormalColor, vignetteDangerColor, danger));
    }

    private void StopPostProcessOut()
    {
        // Restore vignette intensity
        DOTween.To(
            () => _vignette.intensity.value,
            v => _vignette.intensity.Override(v),
            _ppOriginalVignetteIntensity,
            ppFadeOutDuration
        );

        // Restore vignette color
        DOTween.To(
            () => _vignette.color.value,
            v => _vignette.color.Override(v),
            _ppOriginalVignetteColor,
            ppFadeOutDuration
        );

        // Restore saturation
        if (_colorAdj != null)
        {
            DOTween.To(
                () => _colorAdj.saturation.value,
                v => _colorAdj.saturation.Override(v),
                _ppOriginalSaturation,
                ppFadeOutDuration
            );
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. CAMERA FOV + SHAKE
    // ═══════════════════════════════════════════════════════════════

    private void StartCameraEffects()
    {
        if (gameplayCamera == null) return;

        // Ensure defaults are cached (may have been refreshed since Start)
        if (!_cameraDefaultsCached)
        {
            _defaultFOV = gameplayCamera.fieldOfView;
            _cameraOriginalLocalPos = gameplayCamera.transform.localPosition;
            _cameraDefaultsCached = true;
        }

        // FOV boost: ease into the widened field of view
        DOTween.Kill("ChaseFOV");
        DOTween.To(
            () => gameplayCamera.fieldOfView,
            fov => gameplayCamera.fieldOfView = fov,
            _defaultFOV + chaseFOVBoost,
            fovBoostDuration
        ).SetEase(Ease.OutCubic).SetId("ChaseFOV");

        // Start continuous Perlin-noise shake coroutine
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(CameraShakeRoutine());
    }

    private void StopCameraEffects()
    {
        // Kill shake coroutine
        if (_shakeCoroutine != null)
        {
            StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = null;
        }

        if (gameplayCamera != null)
        {
            // Snap local position back to cached original (shake offset removed)
            gameplayCamera.transform.localPosition = _cameraOriginalLocalPos;

            // Ease FOV back to default
            DOTween.Kill("ChaseFOV");
            DOTween.To(
                () => gameplayCamera.fieldOfView,
                fov => gameplayCamera.fieldOfView = fov,
                _defaultFOV,
                fovRestoreDuration
            ).SetEase(Ease.OutSine).SetId("ChaseFOV");
        }
    }

    /// <summary>
    /// Applies continuous Perlin-noise camera shake while the chase is active.
    /// Amplitude scales with DangerFraction and multiplies in the Last Chance Zone.
    /// Runs every frame; restores localPosition cleanly on exit.
    /// </summary>
    private IEnumerator CameraShakeRoutine()
    {
        if (gameplayCamera == null) yield break;

        // Read base position at coroutine start — FOV tween does not change localPosition
        Vector3 basePos = _cameraOriginalLocalPos;

        // Unique Perlin seeds for independent X and Y noise
        const float seedX = 47.33f;
        const float seedY = 83.77f;

        while (_chaseActive)
        {
            float danger = (PoliceCatchController.Instance != null)
                ? PoliceCatchController.Instance.DangerFraction
                : 0f;

            // Amplitude ramps from 40 % at start to 100 % at max danger
            float dangerScale = Mathf.Lerp(0.40f, 1.00f, danger);
            float zoneMultiplier = _isLastChanceZone ? cameraShakeDangerMultiplier : 1f;
            float amp = cameraShakeAmplitude * dangerScale * zoneMultiplier;

            float t = Time.time * cameraShakeFrequency;
            float ox = (Mathf.PerlinNoise(t, seedX) - 0.5f) * 2f * amp;
            float oy = (Mathf.PerlinNoise(seedY, t) - 0.5f) * 2f * amp;

            gameplayCamera.transform.localPosition = basePos + new Vector3(ox, oy, 0f);
            yield return null;
        }

        // Clean restore — ensure no residual offset remains after chase
        if (gameplayCamera != null)
            gameplayCamera.transform.localPosition = basePos;

        _shakeCoroutine = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. POLICE LIGHTS FLICKER
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates instanced material copies from each policeLightRenderer's emissive slot.
    /// Caches them so SetColor calls are cheap and don't alloc later.
    /// Must be called in Awake before any chase starts.
    /// </summary>
    private void CacheEmissiveMaterials()
    {
        if (policeLightRenderers == null || policeLightRenderers.Length == 0) return;

        _cachedEmissiveMats = new Material[policeLightRenderers.Length];
        for (int i = 0; i < policeLightRenderers.Length; i++)
        {
            Renderer r = policeLightRenderers[i];
            if (r != null && r.materials.Length > emissiveMaterialIndex)
                // Accessing .materials (plural) creates instanced copies — intentional
                _cachedEmissiveMats[i] = r.materials[emissiveMaterialIndex];
        }
    }

    private void StartPoliceLights()
    {
        if (_lightsCoroutine != null) StopCoroutine(_lightsCoroutine);
        _lightsCoroutine = StartCoroutine(PoliceLightsFlickerRoutine());
    }

    private void StopPoliceLights()
    {
        if (_lightsCoroutine != null)
        {
            StopCoroutine(_lightsCoroutine);
            _lightsCoroutine = null;
        }

        // Disable lights and reset emissive to off
        if (policeLights != null)
            foreach (Light lt in policeLights)
                if (lt != null) lt.enabled = false;

        if (_cachedEmissiveMats != null)
            foreach (Material mat in _cachedEmissiveMats)
                if (mat != null) mat.SetColor("_EmissionColor", Color.black);
    }

    private IEnumerator PoliceLightsFlickerRoutine()
    {
        bool isRedPhase = true;
        float timer = 0f;

        // Set initial state immediately
        SetPoliceLightColor(flashColorRed);

        while (_chaseActive)
        {
            timer += Time.deltaTime;
            if (timer >= flashInterval)
            {
                timer -= flashInterval;
                isRedPhase = !isRedPhase;
                SetPoliceLightColor(isRedPhase ? flashColorRed : flashColorBlue);
            }
            yield return null;
        }

        _lightsCoroutine = null;
    }

    private void SetPoliceLightColor(Color color)
    {
        // Update Light components
        if (policeLights != null)
        {
            foreach (Light lt in policeLights)
            {
                if (lt == null) continue;
                lt.color = color;
                lt.intensity = lightFlickerIntensity;
                lt.enabled = true;
            }
        }

        // Update emissive material instances
        if (_cachedEmissiveMats != null)
        {
            Color emissive = color * emissiveIntensityMultiplier;
            foreach (Material mat in _cachedEmissiveMats)
                if (mat != null) mat.SetColor("_EmissionColor", emissive);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. SCREEN EDGE DANGER FLASH
    // ═══════════════════════════════════════════════════════════════

    private void UpdateScreenEdge(float danger)
    {
        if (screenEdgeOverlay == null) return;

        if (_isLastChanceZone)
        {
            // Sinusoidal pulse between ~35 % and 100 % of max alpha
            float pulse = Mathf.Sin(Time.time * edgePulseFrequency * Mathf.PI * 2f) * 0.5f + 0.5f;
            screenEdgeOverlay.alpha = Mathf.Lerp(edgeFlashMaxAlpha * 0.35f, edgeFlashMaxAlpha, pulse);
        }
        else
        {
            // Subtle ambient glow that builds with danger before threshold
            float targetAlpha = danger * edgePreWarningMaxAlpha;
            screenEdgeOverlay.alpha = Mathf.MoveTowards(
                screenEdgeOverlay.alpha, targetAlpha, Time.deltaTime * 3f);
        }
    }

    private void HideScreenEdge()
    {
        if (screenEdgeOverlay == null) return;
        screenEdgeOverlay.DOFade(0f, 0.50f);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9. HAPTICS
    // ═══════════════════════════════════════════════════════════════

    private void TriggerTapHaptic()
    {
        if (!hapticsEnabled) return;
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
