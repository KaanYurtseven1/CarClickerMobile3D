using UnityEngine;
using DG.Tweening;

/// <summary>
/// Which side of the road the radar spawned on.
/// </summary>
public enum RadarSide { Left, Right }

/// <summary>
/// Radar object that scrolls with the road.
/// If the player taps it before it reaches despawnZ → dodge (shake + destroy).
/// If it reaches despawnZ untapped → missed (popularity increases).
/// </summary>
public class Radar : MonoBehaviour
{
    /// <summary>Which side this radar was spawned on (set by RadarSpawner).</summary>
    public RadarSide Side { get; private set; }

    /// <summary>Called by RadarSpawner right after instantiation.</summary>
    public void Init(RadarSide side)
    {
        Side = side;
    }

    // ==================== CONFIGURATION ====================

    [Header("Movement")]
    [Tooltip("Movement speed along -Z. Leave 0 to auto-read from RoadLooper at spawn.")]
    [SerializeField] private float moveSpeed = 0f;

    [Header("Despawn")]
    [Tooltip("Z position at which the radar counts as missed and is destroyed.")]
    [SerializeField] private float despawnZ = -15f;

    [Header("Popularity")]
    [Tooltip("How much normalized popularity (0..1) to add when the player misses this radar.")]
    [SerializeField] private float popularityDelta = 0.05f;

    // ==================== STATE ====================

    private bool isAlive = true;

    // ==================== LIFECYCLE ====================

    private void Start()
    {
        // Auto-read road speed if moveSpeed was left at 0
        if (moveSpeed <= 0f)
        {
            RoadLooper rl = FindFirstObjectByType<RoadLooper>();
            if (rl != null)
            {
                moveSpeed = rl.speed;
            }
            else
            {
                moveSpeed = 5f; // fallback
                Debug.LogWarning("[Radar] RoadLooper not found, using fallback speed 5.");
            }
        }
    }

    private void Update()
    {
        if (!isAlive) return;

        // Move in -Z to match road scroll direction
        transform.position += Vector3.back * moveSpeed * Time.deltaTime;

        // Despawn check
        if (transform.position.z <= despawnZ)
        {
            OnMissed();
        }
    }

    // ==================== PUBLIC ====================

    /// <summary>
    /// Called by TapInputRaycaster when the player taps this radar.
    /// </summary>
    public void OnTapped()
    {
        if (!isAlive) return;
        isAlive = false;

        // Try animated vanish (with shake); fallback to instant destroy
        TapVanishAnimator vanish = GetComponent<TapVanishAnimator>();
        if (vanish != null && !vanish.IsPlaying)
        {
            // Disable collider immediately to prevent double-tap
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            vanish.Play(() =>
            {
                Destroy(gameObject);
            });
        }
        else
        {
            // Fallback: smooth DOTween shake + shrink
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOShakePosition(0.25f, 0.1f, 10, 90f, false, true)
                .SetEase(Ease.OutSine));
            seq.Append(transform.DOScale(Vector3.zero, 0.12f)
                .SetEase(Ease.InBack));
            seq.OnComplete(() => Destroy(gameObject));
            seq.OnKill(() => { /* safety */ });
        }
    }

    // ==================== INTERNAL ====================

    private void OnMissed()
    {
        if (!isAlive) return;
        isAlive = false;

        // Increase popularity
        if (PopularityManager.Instance != null)
        {
            PopularityManager.Instance.Increase(popularityDelta);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Radar] Missed! Popularity +{popularityDelta:F2} → now {PopularityManager.Instance.Popularity01:P0}");
#endif
        }
        else
        {
            Debug.LogWarning("[Radar] Missed but PopularityManager.Instance is null.");
        }

        // Show radar snapshot popup (pass side for camera pose selection)
        if (RadarPopupController.Instance != null)
        {
            RadarPopupController.Instance.ShowSnapshot(Side);
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Kill any lingering DOTween animations on this transform
        transform.DOKill();
    }
}
