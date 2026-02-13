using UnityEngine;

/// <summary>
/// Electric-arc tether VFX between two transforms using LineRenderer (URP compatible).
/// Animates Perlin-noise offsets on interior segments for an electric arc look.
///
/// SETUP:
/// 1. Create a prefab with this component + LineRenderer.
/// 2. Assign the Plasma shader material (e.g., plsm2) to the LineRenderer.
/// 3. Set LineRenderer: useWorldSpace = true, widths ~0.03–0.08.
/// 4. NitroMagnetController.arcVfxPrefab → this prefab.
///
/// Mobile-friendly: pre-allocated arrays, zero per-frame GC, LateUpdate only.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ArcLineVFX : MonoBehaviour
{
    [Header("Arc Settings")]
    [Tooltip("Number of line segments (8–12 recommended for electric look)")]
    [Range(4, 20)]
    public int segmentCount = 10;

    [Tooltip("Base displacement amplitude of arc from straight line")]
    public float noiseAmplitude = 0.25f;

    [Tooltip("Perlin noise scroll speed (arc flicker rate)")]
    public float noiseSpeed = 10f;

    [Tooltip("Perlin noise frequency (arc jaggedness)")]
    public float noiseFrequency = 2.5f;

    [Header("Endpoint Offset")]
    [Tooltip("Local-space offset applied to the coin end of the arc (tune to hit model center)")]
    public Vector3 endOffset = new Vector3(0f, 0.2f, 0f);

    [Header("Width")]
    [Tooltip("Start width of the arc line")]
    public float startWidth = 0.06f;

    [Tooltip("End width of the arc line")]
    public float endWidth = 0.03f;

    // ── Runtime ──
    private LineRenderer lr;
    private Transform anchorA; // magnet anchor
    private Transform anchorB; // coin
    private Vector3[] positions; // reused — zero GC
    private float noiseSeed;
    private bool isActive;
    private bool _glowPulseRegistered;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = segmentCount;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;

        positions = new Vector3[segmentCount];
        noiseSeed = Random.Range(0f, 1000f);
        lr.enabled = false;
    }

    // ══════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════

    /// <summary>
    /// Activate the electric arc between magnetAnchor and the coin transform.
    /// </summary>
    public void Init(Transform magnetAnchor, Transform coin)
    {
        anchorA = magnetAnchor;
        anchorB = coin;
        isActive = true;
        lr.enabled = true;
        lr.positionCount = segmentCount;

        // Bloom glow pulse (ref-counted)
        if (!_glowPulseRegistered && ArcGlowPulseController.Instance != null)
        {
            ArcGlowPulseController.Instance.BeginArcGlowPulse();
            _glowPulseRegistered = true;
        }
    }

    /// <summary>
    /// Deactivate the arc. Safe to call multiple times.
    /// </summary>
    public void Cleanup()
    {
        isActive = false;
        if (lr != null)
            lr.enabled = false;
        anchorA = null;
        anchorB = null;

        // Bloom glow pulse (ref-counted, guarded against double-end)
        if (_glowPulseRegistered)
        {
            ArcGlowPulseController.Instance?.EndArcGlowPulse();
            _glowPulseRegistered = false;
        }
    }

    // ══════════════════════════════════════════
    //  UPDATE
    // ══════════════════════════════════════════

    private void LateUpdate()
    {
        if (!isActive)
            return;

        // Auto-cleanup if either endpoint is destroyed
        if (anchorA == null || anchorB == null)
        {
            Cleanup();
            return;
        }

        Vector3 start = anchorA.position;
        Vector3 end = anchorB.position + anchorB.TransformDirection(endOffset);
        Vector3 forward = end - start;
        float length = forward.magnitude;

        if (length < 0.001f)
        {
            lr.enabled = false;
            return;
        }

        lr.enabled = true;

        Vector3 dir = forward / length; // normalized

        // Build perpendicular basis for displacement
        Vector3 up = Vector3.Cross(dir, Vector3.right);
        if (up.sqrMagnitude < 0.001f)
            up = Vector3.Cross(dir, Vector3.forward);
        up.Normalize();
        Vector3 right = Vector3.Cross(dir, up).normalized;

        float time = Time.time * noiseSpeed;

        for (int i = 0; i < segmentCount; i++)
        {
            float t = (float)i / (segmentCount - 1);
            Vector3 basePos = Vector3.Lerp(start, end, t);

            // Endpoints fixed — no noise
            if (i == 0 || i == segmentCount - 1)
            {
                positions[i] = basePos;
                continue;
            }

            // Sine envelope: displacement fades near both endpoints
            float envelope = Mathf.Sin(t * Mathf.PI);

            // Two perpendicular Perlin channels for 2D displacement
            float nX = Mathf.PerlinNoise(noiseSeed + i * noiseFrequency, time) - 0.5f;
            float nY = Mathf.PerlinNoise(noiseSeed + 100f + i * noiseFrequency, time + 50f) - 0.5f;

            // Scale amplitude by envelope and line length (capped so short arcs aren't invisible)
            float amp = noiseAmplitude * envelope * Mathf.Clamp(length * 0.15f, 0.15f, 1.5f);
            positions[i] = basePos + (up * nX + right * nY) * amp * 2f;
        }

        lr.SetPositions(positions);
    }

    private void OnDestroy()
    {
        Cleanup();
    }
}
