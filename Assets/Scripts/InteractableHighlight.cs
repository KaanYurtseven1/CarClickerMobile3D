using UnityEngine;
using DG.Tweening;

/// <summary>
/// Adds a soft billboard / ground-halo glow beneath an interactive world object
/// (chest, radar, nitro coin) to signal "tappable".
///
/// SETUP:
///   1. Add this component to any interactive prefab root.
///   2. Assign <see cref="glowMaterial"/> (Particles/Unlit, Additive blend).
///      If left null the component is disabled gracefully.
///   3. Tune color, size, and pulse per prefab variant in the Inspector.
///
/// The glow quad is created at runtime — no manual child objects needed.
/// Uses MaterialPropertyBlock for per-instance tinting (SRP Batcher safe).
/// </summary>
public class InteractableHighlight : MonoBehaviour
{
    [Header("Material")]
    [Tooltip("Shared glow material (Particles/Unlit, Additive). " +
             "Per-instance color is applied via MaterialPropertyBlock.")]
    [SerializeField] private Material glowMaterial;

    [Header("Appearance")]
    [Tooltip("Glow tint color (HDR for extra brightness).")]
    [ColorUsage(false, true)]
    [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.3f, 1f);

    [Tooltip("World-space diameter of the glow quad.")]
    [SerializeField] private float glowSize = 2f;

    [Tooltip("Y offset from this object's pivot (use small positive to avoid z-fight with ground).")]
    [SerializeField] private float yOffset = 0.02f;

    [Header("Orientation")]
    [Tooltip("If true: glow lays flat on the ground (XZ plane). " +
             "If false: glow always faces the camera (billboard).")]
    [SerializeField] private bool groundHalo = true;

    [Header("Pulse Animation")]
    [Tooltip("Enable a gentle scale pulse.")]
    [SerializeField] private bool enablePulse = true;

    [Tooltip("Fraction of glowSize the pulse oscillates (0.1 = ±10%).")]
    [Range(0f, 0.4f)]
    [SerializeField] private float pulseAmplitude = 0.12f;

    [Tooltip("Pulse cycles per second.")]
    [SerializeField] private float pulseFrequency = 1f;

    [Header("Fade")]
    [Tooltip("Fade-in duration when the object spawns (seconds).")]
    [SerializeField] private float fadeInDuration = 0.35f;

    // ── Runtime ──
    private Transform _glowTransform;
    private Renderer _glowRenderer;
    private MaterialPropertyBlock _mpb;
    private Tween _pulseTween;
    private Tween _fadeTween;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static Mesh _sharedQuad;

    // ──────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (glowMaterial == null)
        {
            enabled = false;
            return;
        }

        CreateGlowQuad();
        ApplyColor(0f); // start fully transparent for fade-in
    }

    private void Start()
    {
        // Fade in
        float currentAlpha = 0f;
        _fadeTween = DOTween.To(
            () => currentAlpha,
            a => { currentAlpha = a; ApplyColor(a); },
            1f,
            fadeInDuration
        ).SetEase(Ease.OutQuad)
         .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
         .OnComplete(StartPulse);
    }

    private void LateUpdate()
    {
        if (_glowTransform == null) return;

        if (!groundHalo)
        {
            // Billboard: face camera each frame
            Camera cam = Camera.main;
            if (cam != null)
                _glowTransform.rotation = Quaternion.LookRotation(
                    _glowTransform.position - cam.transform.position, Vector3.up);
        }
    }

    private void OnDestroy()
    {
        _pulseTween?.Kill();
        _fadeTween?.Kill();
    }

    /// <summary>
    /// Immediately hides and cleans up the glow quad.
    /// Safe to call multiple times.
    /// </summary>
    public void Hide()
    {
        _pulseTween?.Kill();
        _pulseTween = null;
        _fadeTween?.Kill();
        _fadeTween = null;

        if (_glowTransform != null)
            _glowTransform.gameObject.SetActive(false);
    }

    /// <summary>
    /// Smoothly fades the glow out over <paramref name="duration"/> seconds,
    /// then deactivates the glow quad. Safe to call multiple times.
    /// </summary>
    public void FadeOut(float duration = 0.25f)
    {
        // Kill pulse so scale doesn't fight the fade
        _pulseTween?.Kill();
        _pulseTween = null;

        // Kill any existing fade tween
        _fadeTween?.Kill();
        _fadeTween = null;

        if (_glowRenderer == null || _glowTransform == null)
            return;

        // Read current alpha from the property block
        _glowRenderer.GetPropertyBlock(_mpb);
        Color current = _mpb.GetColor(BaseColorID);
        float startAlpha = current.a;

        if (startAlpha <= 0f)
        {
            _glowTransform.gameObject.SetActive(false);
            return;
        }

        float alpha = startAlpha;
        _fadeTween = DOTween.To(
            () => alpha,
            a => { alpha = a; ApplyColor(a); },
            0f,
            duration
        ).SetEase(Ease.InQuad)
         .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
         .OnComplete(() =>
         {
             if (_glowTransform != null)
                 _glowTransform.gameObject.SetActive(false);
         });
    }

    // ──────────────────────────────────────────────────────────
    //  Glow quad creation
    // ──────────────────────────────────────────────────────────

    private void CreateGlowQuad()
    {
        if (_sharedQuad == null)
            _sharedQuad = CreateQuadMesh();

        var go = new GameObject("_Glow");
        go.layer = gameObject.layer;
        _glowTransform = go.transform;
        _glowTransform.SetParent(transform, worldPositionStays: false);

        // Position: directly below pivot with small Y lift
        _glowTransform.localPosition = new Vector3(0f, yOffset, 0f);

        // Orientation: flat on ground (rotate 90° around X) or default forward
        if (groundHalo)
            _glowTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        else
            _glowTransform.localRotation = Quaternion.identity;

        // Compensate for non-uniform parent scale so glow is always a circle.
        // Without this, a parent scaled (0.5, 0.1, 0.5) like NitroCoin would
        // squash the glow into an ellipse.
        Vector3 ps = transform.lossyScale;
        float invX = ps.x != 0f ? 1f / ps.x : 1f;
        float invY = ps.y != 0f ? 1f / ps.y : 1f;
        float invZ = ps.z != 0f ? 1f / ps.z : 1f;

        // The quad's local X maps to world X, local Y maps to world Z (due to 90° X rotation for ground halo)
        // or world Y for billboard. Compute correct compensation:
        if (groundHalo)
            _glowTransform.localScale = new Vector3(glowSize * invX, glowSize * invZ, 1f * invY);
        else
            _glowTransform.localScale = new Vector3(glowSize * invX, glowSize * invY, 1f * invZ);

        // Renderer
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = _sharedQuad;

        _glowRenderer = go.AddComponent<MeshRenderer>();
        _glowRenderer.sharedMaterial = glowMaterial;
        _glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _glowRenderer.receiveShadows = false;

        _mpb = new MaterialPropertyBlock();
    }

    private static Mesh CreateQuadMesh()
    {
        var mesh = new Mesh { name = "GlowQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ──────────────────────────────────────────────────────────
    //  Color / alpha via MaterialPropertyBlock
    // ──────────────────────────────────────────────────────────

    private void ApplyColor(float alpha)
    {
        if (_glowRenderer == null) return;

        Color c = glowColor;
        c.a = alpha;
        _glowRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorID, c);
        _glowRenderer.SetPropertyBlock(_mpb);
    }

    // ──────────────────────────────────────────────────────────
    //  Pulse
    // ──────────────────────────────────────────────────────────

    private void StartPulse()
    {
        if (!enablePulse || _glowTransform == null) return;

        float baseSize = glowSize;
        float amplitude = glowSize * pulseAmplitude;
        float duration = 1f / Mathf.Max(pulseFrequency, 0.1f);

        // Cache inverse parent scale for uniform circle compensation
        Vector3 ps = transform.lossyScale;
        float invX = ps.x != 0f ? 1f / ps.x : 1f;
        float invY = ps.y != 0f ? 1f / ps.y : 1f;
        float invZ = ps.z != 0f ? 1f / ps.z : 1f;
        bool isGround = groundHalo;

        _pulseTween = DOTween.To(
            () => 0f,
            t =>
            {
                float s = baseSize + Mathf.Sin(t * Mathf.PI * 2f) * amplitude;
                if (_glowTransform != null)
                {
                    if (isGround)
                        _glowTransform.localScale = new Vector3(s * invX, s * invZ, 1f * invY);
                    else
                        _glowTransform.localScale = new Vector3(s * invX, s * invY, 1f * invZ);
                }
            },
            1f,
            duration
        ).SetEase(Ease.Linear)
         .SetLoops(-1, LoopType.Restart)
         .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }
}
