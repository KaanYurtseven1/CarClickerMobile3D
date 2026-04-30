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
    /// <summary>
    /// Fired when the deterministic tutorial Radar crosses its configured Z
    /// freeze threshold (toward the camera). Used by <see cref="TutorialManager"/>
    /// to freeze gameplay and start the idle bounce loop on the radar.
    /// </summary>
    public static event System.Action<Radar> OnTutorialRadarReachedCenter;

    /// <summary>
    /// Fired when the deterministic tutorial Radar is tapped by the player.
    /// Used by <see cref="TutorialManager"/> to open the Eleven/Twelve popups.
    /// </summary>
    public static event System.Action<Radar> OnTutorialRadarTapped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsRadar()
    {
        OnTutorialRadarReachedCenter = null;
        OnTutorialRadarTapped = null;
    }

    /// <summary>Which side this radar was spawned on (set by RadarSpawner).</summary>
    public RadarSide Side { get; private set; }

    /// <summary>True when this radar is the deterministic tutorial Radar.</summary>
    public bool IsTutorialRadar { get; private set; }
    private float _tutorialFreezeZ;
    private bool _tutorialCenterTriggered;
    private float _tutorialMoveDirSign;
    private Tween _tutorialBounceTween;
    private Vector3 _tutorialBounceBaseScale;

    /// <summary>Called by RadarSpawner right after instantiation.</summary>
    public void Init(RadarSide side)
    {
        Side = side;
    }

    /// <summary>
    /// Marks this radar as the deterministic tutorial Radar. Spawner calls this
    /// immediately after Instantiate. The radar will, on its way down, raise
    /// <see cref="OnTutorialRadarReachedCenter"/> when it crosses
    /// <paramref name="freezeZ"/> toward the camera and start a subtle bounce loop.
    /// </summary>
    public void MarkAsTutorialRadar(float freezeZ)
    {
        IsTutorialRadar = true;
        _tutorialFreezeZ = freezeZ;
        _tutorialCenterTriggered = false;
        _tutorialMoveDirSign = Mathf.Sign(freezeZ - transform.position.z);
        if (_tutorialMoveDirSign == 0f) _tutorialMoveDirSign = -1f;
        Debug.Log($"[RadarSpawner][RadarTut] MarkAsTutorialRadar: name='{name}' pos={transform.position} freezeZ={freezeZ} dirSign={_tutorialMoveDirSign}");
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

        // Tutorial radar: detect Z-center crossing once, then halt and notify.
        if (IsTutorialRadar && !_tutorialCenterTriggered)
        {
            bool crossed = (_tutorialMoveDirSign > 0f && transform.position.z >= _tutorialFreezeZ) ||
                           (_tutorialMoveDirSign < 0f && transform.position.z <= _tutorialFreezeZ);
            if (crossed)
            {
                _tutorialCenterTriggered = true;
                StartTutorialBounce();
                Debug.Log($"[RadarSpawner][RadarTut] Tutorial radar reached center: name='{name}' pos={transform.position} freezeZ={_tutorialFreezeZ}. Firing OnTutorialRadarReachedCenter.");
                OnTutorialRadarReachedCenter?.Invoke(this);
                return;
            }
        }

        // Tutorial radar: once at center, never move further until tapped.
        if (IsTutorialRadar && _tutorialCenterTriggered) return;

        // Tutorial gating: hold position while gameplay is frozen.
        if (TutorialGate.GameplayFrozen) return;

        // Move in -Z to match road scroll direction
        transform.position += Vector3.back * CurrentSpeed * Time.deltaTime;

        // Despawn check
        if (transform.position.z <= despawnZ)
        {
            OnMissed();
        }
    }

    private void StartTutorialBounce()
    {
        if (_tutorialBounceTween != null && _tutorialBounceTween.IsActive())
            return;

        _tutorialBounceBaseScale = transform.localScale;
        Vector3 peak = _tutorialBounceBaseScale * 1.18f;

        _tutorialBounceTween = transform
            .DOScale(peak, 0.55f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void StopTutorialBounce()
    {
        if (_tutorialBounceTween != null)
        {
            _tutorialBounceTween.Kill();
            _tutorialBounceTween = null;
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

            // Tutorial radar: deterministic event — do NOT mutate popularity or
            // notify radar-defused listeners (would skew AmbientHeat/PoliceCatch).
            if (IsTutorialRadar)
            {
                StopTutorialBounce();
                Debug.Log($"[RadarSpawner][RadarTut] Tutorial radar tapped: name='{name}'. GameplayFrozen={TutorialGate.GameplayFrozen}. Firing OnTutorialRadarTapped.");
                OnTutorialRadarTapped?.Invoke(this);
            }
            else if (PopularityManager.Instance != null)
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
        StopTutorialBounce();
        transform.DOKill();
    }
}
