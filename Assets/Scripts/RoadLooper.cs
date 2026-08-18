using UnityEngine;

/// <summary>
/// Endless road system: scrolls a fixed number of road segments toward the
/// camera (–Z) and recycles the rearmost to the front in a seamless loop.
///
/// TWO MODES
///
/// 1. PREFAB MODE (recommended — future-proof)
///    Assign 1+ full road prefabs to <see cref="roadVariantPrefabs"/>.
///    At Start the system spawns <see cref="activeSegmentCount"/> lightweight
///    carrier objects, instantiates a random variant on each, and swaps to a
///    new random variant every time a carrier recycles.
///      • 1 prefab  → seamless identical road flow
///      • N prefabs → random visual variety on every recycle
///
/// 2. FALLBACK MODE
///    When <see cref="roadVariantPrefabs"/> is empty the system moves the
///    scene-baked <see cref="roadSegments"/> transforms exactly as before
///    (full backward compatibility — no spawning, no swapping).
///
/// PREFAB RULES
///   1. At scale (1,1,1) fill exactly <see cref="segmentLength"/> units on Z.
///   2. Pivot / root at the segment's local origin (center or front-edge —
///      just be consistent across all variants).
///   3. Y ground plane at 0 (offset internal children if needed).
///   4. Face default +Z.
///   5. Include ALL visuals (road surface, buildings, environment) as children.
/// </summary>
public class RoadLooper : MonoBehaviour
{
    [Header("Scene Segments (Fallback)")]
    [Tooltip("Scene-baked road segment transforms.  Used ONLY when Road Variant " +
             "Prefabs is empty.  Safe to leave wired — they are automatically " +
             "deactivated when prefab mode is active.")]
    public Transform[] roadSegments;

    [Header("Road Variant Prefabs")]
    [Tooltip("Assign 1 or more full road segment prefabs.  Each prefab is a " +
             "complete self-contained road piece (surface + buildings + environment).  " +
             "With 1 prefab the road loops seamlessly.  With multiple prefabs each " +
             "recycle picks a random variant for visual variety.")]
    public GameObject[] roadVariantPrefabs;

    [Header("Segment Configuration")]
    [Tooltip("Total active road segments in the loop.  More segments = more road " +
             "visible ahead = stronger endless-flow illusion.  " +
             "Only applies when Road Variant Prefabs is populated.")]
    [Range(3, 10)]
    public int activeSegmentCount = 5;

    [Tooltip("Z length each road segment fills.  Must match the prefab dimensions.")]
    public float segmentLength = 20f;

    [Tooltip("Fallback speed used only when WorldScrollSpeed is not in the scene.")]
    public float speed = 5f;

    // ---- runtime state ----
    private Transform[] _slots;               // carrier transforms moving each frame
    private GameObject[] _variantInstances;    // per-slot spawned prefab (null in fallback)
    private bool _prefabMode;

    /// <summary>Effective scroll speed: WorldScrollSpeed if available, else local fallback.</summary>
    private float CurrentSpeed =>
        WorldScrollSpeed.Instance != null ? WorldScrollSpeed.Instance.EffectiveSpeed : speed;

    // ------------------------------------------------------------------ //
    //  Lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        _prefabMode = roadVariantPrefabs != null && roadVariantPrefabs.Length > 0;

        if (_prefabMode)
            InitPrefabMode();
        else
            _slots = roadSegments; // fallback: use scene-baked transforms as-is
    }

    private void Update()
    {
        if (_slots == null || _slots.Length == 0) return;

        // Tutorial gating: hold every road segment in place while the tutorial
        // has frozen gameplay. Skipping movement also skips recycle naturally
        // (segments cannot cross the recycle threshold while stationary).
        if (TutorialGate.GameplayFrozen) return;

        float frameSpeed = CurrentSpeed;

        // 1) Move every slot toward the camera (–Z)
        for (int i = 0; i < _slots.Length; i++)
            _slots[i].position += Vector3.back * frameSpeed * Time.deltaTime;

        // 2) Recycle any slot that passed behind the visible area
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].position.z < -segmentLength)
            {
                // Find the forward-most slot
                float maxZ = float.MinValue;
                for (int j = 0; j < _slots.Length; j++)
                {
                    if (_slots[j].position.z > maxZ)
                        maxZ = _slots[j].position.z;
                }

                // Place right in front of the leader
                _slots[i].position = new Vector3(
                    _slots[i].position.x,
                    _slots[i].position.y,
                    maxZ + segmentLength
                );

                // Swap to a (possibly different) full road variant
                if (_variantInstances != null)
                    SwapVariant(i);
            }
        }
    }

    private void OnDestroy()
    {
        if (_variantInstances == null) return;
        foreach (var go in _variantInstances)
            if (go != null) Destroy(go);
    }

    // ------------------------------------------------------------------ //
    //  Prefab-mode initialisation
    // ------------------------------------------------------------------ //

    private void InitPrefabMode()
    {
        // Deactivate scene-baked segments — prefab instances take over
        if (roadSegments != null)
        {
            foreach (var seg in roadSegments)
                if (seg != null) seg.gameObject.SetActive(false);
        }

        int count = Mathf.Max(activeSegmentCount, 3);
        _slots = new Transform[count];
        _variantInstances = new GameObject[count];

        // First slot starts 1 segment-length behind origin so there is
        // road both behind and ahead of the camera from the first frame.
        for (int i = 0; i < count; i++)
        {
            var carrier = new GameObject($"RoadSlot_{i}");
            carrier.transform.SetParent(transform); // tidy hierarchy
            carrier.transform.position = new Vector3(0f, 0f, (i - 1) * segmentLength);
            _slots[i] = carrier.transform;

            SwapVariant(i);
        }
    }

    // ------------------------------------------------------------------ //
    //  Full-prefab swap
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Destroys the current variant instance on the given slot and
    /// instantiates a random full road prefab from the pool.
    /// The instance is parented to the carrier with
    /// <c>worldPositionStays = true</c> so the prefab's authored
    /// scale (1,1,1) is preserved regardless of the carrier's own scale.
    /// </summary>
    private void SwapVariant(int slotIndex)
    {
        if (_variantInstances[slotIndex] != null)
            Destroy(_variantInstances[slotIndex]);

        GameObject prefab = roadVariantPrefabs[Random.Range(0, roadVariantPrefabs.Length)];
        if (prefab == null) return;

        Transform slot = _slots[slotIndex];

        GameObject instance = Instantiate(prefab, slot.position, Quaternion.identity);
        instance.transform.SetParent(slot, worldPositionStays: true);

        _variantInstances[slotIndex] = instance;
    }
}
