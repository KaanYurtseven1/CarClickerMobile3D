using UnityEngine;
using System;
using DG.Tweening;

public class NitroCoin : MonoBehaviour
{
    [Header("Reward")]
    [Tooltip("Bu coin'e tıkladığında kaç Nitro Coin verecek")]
    public int rewardAmount = 1;

    [Header("Arc Anchor")]
    [Tooltip("Optional child transform at the visual center of the coin model. If empty, auto-searches for child named 'ArcTarget'.")]
    [SerializeField] private Transform arcTarget;

    /// <summary>World-space transform the electric arc should connect to (model center).</summary>
    public Transform ArcTargetTransform => arcTarget != null ? arcTarget : transform;

    [Header("Movement")]
    public float speed = 8f;

    // Spawner set edecek
    [HideInInspector]
    public float despawnZ;

    // ── Magnet pull state machine ──
    private enum MagnetPhase { None, Drift, Pull }
    private MagnetPhase magnetPhase = MagnetPhase.None;

    private bool isCollected = false; // Guard against double-collection

    // Drift phase
    private Vector3 driftTarget;
    private float driftSpeed;
    private float driftEndTime;

    // Pull phase
    private Transform magnetTarget;
    private float magnetPullSpeed;
    private float magnetCollectDistance;
    private Action<NitroCoin, int> onMagnetCollectCallback;

    // Smooth pull via SmoothDamp (handles moving target, ease-in feel)
    private Vector3 pullVelocity;
    private float pullElapsed;
    private float pullDuration;
    private const float PullSmoothStart = 0.55f; // high = sluggish start
    private const float PullSmoothEnd = 0.02f;  // low  = snappy finish

    // DOTween drift
    private Tween driftTween;

    // VFX (spawned/destroyed by this coin)
    private ArcLineVFX activeArc;
    private ParticleSystem activeSpark;
    private NitroCoinGlowController _glowController;

    private float moveDirSign;
    private Rigidbody rb;

    /// <summary>Guard flag: true once the coin has been collected (tap or magnet). Prevents double-collection.</summary>
    public bool IsCollected => isCollected;

    /// <summary>True if currently in drift or pull phase.</summary>
    public bool IsBeingMagnetPulled => magnetPhase != MagnetPhase.None;

    private void Start()
    {
        // Auto-find arc anchor child if not assigned in Inspector
        if (arcTarget == null)
        {
            arcTarget = transform.Find("ArcTarget");
        }

        // Top'tan Bottom'a doğru gidecek yönü belirle
        moveDirSign = Mathf.Sign(despawnZ - transform.position.z);
        if (moveDirSign == 0f)
            moveDirSign = -1f; // güvenlik için

        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        switch (magnetPhase)
        {
            case MagnetPhase.Drift:
                UpdateDriftPhase();
                break;
            case MagnetPhase.Pull:
                UpdatePullPhase();
                break;
            default:
                UpdateNormalMovement();
                break;
        }
    }

    // ── NORMAL MOVEMENT ──

    private void UpdateNormalMovement()
    {
        if (rb != null && rb.isKinematic)
        {
            Vector3 movement = new Vector3(0f, 0f, moveDirSign * speed * Time.deltaTime);
            rb.MovePosition(transform.position + movement);
        }
        else
        {
            transform.Translate(0f, 0f, moveDirSign * speed * Time.deltaTime, Space.World);
        }

        // Bottom çizgisini geçti mi?
        if ((moveDirSign > 0f && transform.position.z >= despawnZ) ||
            (moveDirSign < 0f && transform.position.z <= despawnZ))
        {
            Destroy(gameObject);
        }
    }

    // ── DRIFT PHASE (Phase 1): short random drift near entry point ──

    private void UpdateDriftPhase()
    {
        // DOTween handles drift movement; OnComplete triggers TransitionToPull.
        // Safety: if tween was killed externally, transition immediately.
        if (driftTween == null || !driftTween.IsActive())
        {
            TransitionToPull();
        }
    }

    /// <summary>Called when drift phase ends — sets up SmoothDamp pull.</summary>
    private void TransitionToPull()
    {
        driftTween = null;
        magnetPhase = MagnetPhase.Pull;
        pullElapsed = 0f;
        pullVelocity = Vector3.zero;
        pullDuration = magnetTarget != null
            ? Mathf.Max(0.35f, Vector3.Distance(transform.position, magnetTarget.position) / Mathf.Max(magnetPullSpeed, 1f))
            : 1f;
    }

    // ── PULL PHASE (Phase 2): ease-in acceleration toward MagnetAnchor ──

    private void UpdatePullPhase()
    {
        if (magnetTarget == null)
        {
            CancelMagnetPull("target_lost");
            return;
        }

        Vector3 targetPos = magnetTarget.position;
        float distance = Vector3.Distance(transform.position, targetPos);

        // SmoothDamp with shrinking smoothTime → ease-in acceleration toward target.
        // t ramps 0→1 over pullDuration; quadratic mapping makes the smooth-time
        // drop slowly at first (sluggish start) then fast (acceleration feel).
        pullElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(pullElapsed / pullDuration);
        float smoothTime = Mathf.Lerp(PullSmoothStart, PullSmoothEnd, t * t);

        Vector3 newPos = Vector3.SmoothDamp(
            transform.position, targetPos, ref pullVelocity,
            smoothTime, Mathf.Infinity, Time.deltaTime);
        MoveCoin(newPos - transform.position);

        if (distance <= magnetCollectDistance)
        {
            OnMagnetCollected();
        }
    }

    /// <summary>Moves the coin using Rigidbody.MovePosition if kinematic, otherwise transform.</summary>
    private void MoveCoin(Vector3 delta)
    {
        if (rb != null && rb.isKinematic)
        {
            rb.MovePosition(transform.position + delta);
        }
        else
        {
            transform.position += delta;
        }
    }

    // ══════════════════════════════════════════
    //  MAGNET PULL API
    // ══════════════════════════════════════════

    /// <summary>
    /// Called by NitroMagnetController when coin is accepted for magnet pull.
    /// Starts two-phase pull: drift → then ease-in pull toward magnetAnchor.
    /// </summary>
    public void StartMagnetPull(
        Transform target,
        float pullSpeed,
        float collectDist,
        Action<NitroCoin, int> collectCallback,
        Vector3 driftTargetPos,
        float driftDuration,
        float driftSpd,
        ArcLineVFX arcPrefab = null,
        ParticleSystem sparkPrefab = null)
    {
        if (magnetPhase != MagnetPhase.None || isCollected)
            return;

        // Store pull parameters
        magnetTarget = target;
        magnetPullSpeed = pullSpeed;
        magnetCollectDistance = collectDist;
        onMagnetCollectCallback = collectCallback;

        // Drift parameters (kept for fallback reference)
        driftTarget = driftTargetPos;
        driftSpeed = driftSpd;
        driftEndTime = Time.time + driftDuration;

        magnetPhase = MagnetPhase.Drift;

        // ── DOTween smooth drift toward random point ──
        driftTween = transform.DOMove(driftTargetPos, driftDuration)
            .SetEase(Ease.OutSine)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(TransitionToPull);

        // ── Spawn electric arc VFX ──
        if (arcPrefab != null && target != null)
        {
            activeArc = UnityEngine.Object.Instantiate(arcPrefab, Vector3.zero, Quaternion.identity);
            activeArc.Init(target, ArcTargetTransform);
        }

        // ── Spawn spark particles (optional) ──
        if (sparkPrefab != null)
        {
            activeSpark = UnityEngine.Object.Instantiate(sparkPrefab, transform);
            activeSpark.transform.localPosition = Vector3.zero;
            activeSpark.Play();
        }

        // ── Per-coin emission glow ──
        EnsureGlowController();
        _glowController.EnableGlow();
    }

    /// <summary>
    /// Cancels magnet pull and restores normal coin movement.
    /// Safe to call in any state (collected, not pulling, etc.).
    /// </summary>
    public void CancelMagnetPull(string reason)
    {
        if (magnetPhase == MagnetPhase.None)
            return;

        KillDriftTween();
        CleanupVFX();

        magnetPhase = MagnetPhase.None;
        magnetTarget = null;
        onMagnetCollectCallback = null;
        // Coin resumes normal movement from current position
        // (moveDirSign and despawnZ are still valid)
    }

    // ══════════════════════════════════════════
    //  COLLECTION
    // ══════════════════════════════════════════

    /// <summary>
    /// Called when coin reaches magnet target (Phase 2 complete).
    /// </summary>
    private void OnMagnetCollected()
    {
        if (isCollected)
            return;
        isCollected = true;

        KillDriftTween();
        CleanupVFX();

        // Clear phase to prevent further updates
        magnetPhase = MagnetPhase.None;

        // Notify controller callback (quota tracking)
        onMagnetCollectCallback?.Invoke(this, rewardAmount);
        onMagnetCollectCallback = null;

        // Grant reward
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddNitroCoins(rewardAmount);
        }

        // Notify CardManager for tracking (NitroRain, BoostMode, etc.)
        if (CardManager.Instance != null)
        {
            CardManager.Instance.NotifyNitroCollected(rewardAmount);
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// TapInputRaycaster calls this when player manually taps the coin.
    /// </summary>
    public void OnTapped()
    {
        // Guard against double-collection
        if (isCollected)
            return;
        isCollected = true;

        // If being pulled by magnet, notify controller that player "stole" it
        if (magnetPhase != MagnetPhase.None && NitroMagnetController.Instance != null)
        {
            NitroMagnetController.Instance.NotifyCoinTappedWhilePulling(this);
        }

        // Stop magnet pull (player tap wins)
        KillDriftTween();
        CleanupVFX();
        magnetPhase = MagnetPhase.None;
        magnetTarget = null;
        onMagnetCollectCallback = null;

        // Try animated vanish; fallback to instant destroy
        TapVanishAnimator vanish = GetComponent<TapVanishAnimator>();
        if (vanish != null && !vanish.IsPlaying)
        {
            // Disable collider immediately to prevent double-tap
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            vanish.Play(() =>
            {
                CollectAndDestroy();
            });
        }
        else
        {
            CollectAndDestroy();
        }
    }

    /// <summary>Grants reward and destroys the coin. Called after tap vanish animation.</summary>
    private void CollectAndDestroy()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddNitroCoins(rewardAmount);
        }

        // Notify CardManager for nitro collection tracking
        if (CardManager.Instance != null)
        {
            CardManager.Instance.NotifyNitroCollected(rewardAmount);
        }

        Destroy(gameObject);
    }

    // ══════════════════════════════════════════
    //  CLEANUP HELPERS
    // ══════════════════════════════════════════

    private void KillDriftTween()
    {
        if (driftTween != null && driftTween.IsActive())
            driftTween.Kill();
        driftTween = null;
    }

    private void CleanupVFX()
    {
        if (activeArc != null)
        {
            activeArc.Cleanup();
            Destroy(activeArc.gameObject);
            activeArc = null;
        }
        if (activeSpark != null)
        {
            activeSpark.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(activeSpark.gameObject);
            activeSpark = null;
        }
        if (_glowController != null)
        {
            _glowController.DisableGlow();
        }
    }

    private void OnDestroy()
    {
        KillDriftTween();
        CleanupVFX();
        // Immediate cleanup — no fade on destroy
        if (_glowController != null)
            _glowController.DisableGlowImmediate();
    }

    /// <summary>Lazily adds NitroCoinGlowController (avoids requiring it on prefab).</summary>
    private void EnsureGlowController()
    {
        if (_glowController == null)
        {
            _glowController = GetComponent<NitroCoinGlowController>();
            if (_glowController == null)
                _glowController = gameObject.AddComponent<NitroCoinGlowController>();
            _glowController.Initialize();
        }
    }
}
