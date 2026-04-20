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
    [Tooltip("Independent radar scroll speed (units/second). Not tied to road speed.")]
    [SerializeField] private float moveSpeed = 15f;

    [Header("Despawn")]
    [Tooltip("Z position at which the radar counts as missed and is destroyed.")]
    [SerializeField] private float despawnZ = -15f;

    [Header("Popularity")]
    [Tooltip("Normalized popularity increment per miss. 0.01 = +1 on the 0–100 scale.")]
    [SerializeField] private float popularityDelta = 0.01f;

    [Header("Debug")]
    [Tooltip("Log miss handling details to console.")]
    [SerializeField] private bool enableDebug = false;

    // ==================== STATE ====================

    private bool isAlive = true;
    /// <summary>Unified guard: ensures popularity logic (miss OR defuse) fires at most once per instance.</summary>
    private bool _handled = false;

    // ==================== LIFECYCLE ====================

    /// <summary>Uses the local moveSpeed field (independent from road scroll speed).</summary>
    private float CurrentSpeed => moveSpeed;

    private void Update()
    {
        if (!isAlive) return;

        // Move in -Z to match road scroll direction
        transform.position += Vector3.back * CurrentSpeed * Time.deltaTime;

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

        // ---- Popularity decrease (defuse) ----
        if (!_handled)
        {
            _handled = true;

            // P8: Radar defuse SFX
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlayRadarDefuse();

            if (PopularityManager.Instance != null)
            {
                PopularityManager.Instance.AddPopularityNormalized(
                    -popularityDelta, "RadarDefuse", this);

                // Notify AmbientHeatManager (and any other subscribers) that a radar was defused
                PopularityManager.Instance.NotifyRadarDefused();
            }
        }

        // Fade out interactable highlight in sync with vanish animation
        var highlight = GetComponent<InteractableHighlight>();
        if (highlight != null)
            highlight.FadeOut(0.25f);

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

        // Unified guard: only one popularity change (miss OR defuse) per radar instance
        if (_handled)
        {
            if (enableDebug)
                Debug.Log($"[Radar] {name} OnMissed BLOCKED (_handled already true)");
            Destroy(gameObject);
            return;
        }
        _handled = true;

        // P9: Radar miss SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayRadarMiss();

        // Increase popularity by exactly +1 on the 0–100 scale (0.01 normalized)
        if (PopularityManager.Instance != null)
        {
            PopularityManager.Instance.AddPopularityNormalized(
                popularityDelta, "RadarMiss", this);

            // Fire radar photo event (drives PoliceCatchTrigger counter)
            PopularityManager.Instance.NotifyRadarPhotoTaken();
        }
        else
        {
            Debug.LogWarning("[Radar] Missed but PopularityManager.Instance is null.");
        }

        // Show radar snapshot popup (pass side for camera pose selection)
        // Suppress popup when a non-Clicker bottom bar panel is open
        if (RadarPopupController.Instance != null && !UIFlowState.IsContentPanelOpen)
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
