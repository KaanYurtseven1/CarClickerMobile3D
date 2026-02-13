using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Nitro Magnet card: After N taps, arms magnet to auto-collect K nitro coins.
/// Attach to NitroMagnetArea GameObject.
///
/// PRIMARY DETECTION: spawn-based (NitroCoinSpawner calls OnCoinSpawned) +
///   periodic bounds check every 0.1 s for tracked coins outside area.
/// SECONDARY DETECTION: OnTriggerEnter (kept as fallback).
///
/// INSPECTOR SETUP CHECKLIST:
/// 1. Assign magnetTarget   → MagnetAnchor Transform (child of car, at car center)
/// 2. Assign shieldVFX      → Plasma Sphere GameObject
/// 3. Assign areaCollider   → BoxCollider on NitroMagnetArea (IsTrigger=true)
/// 4. NitroMagnetArea needs Rigidbody (IsKinematic=true, UseGravity=false) for trigger detection
/// 5. NitroCoin prefab: Rigidbody (IsKinematic=true) + CapsuleCollider (IsTrigger=false)
/// 6. Coin and NitroMagnetArea layers must NOT physically block each other
/// </summary>
public class NitroMagnetController : MonoBehaviour
{
    public static NitroMagnetController Instance { get; private set; }

    [Header("References")]
    [Tooltip("Transform where coins will be pulled to (e.g., MagnetAnchor near car)")]
    public Transform magnetTarget;

    [Tooltip("Shield VFX GameObject (Plasma Sphere) — enabled when magnet is armed")]
    public GameObject shieldVFX;

    [Tooltip("BoxCollider (trigger) that defines the magnet area in front of the car")]
    public Collider areaCollider;

    [Header("Pull Settings")]
    [Tooltip("Base speed for pulling coins toward target (accelerates via ease-in)")]
    public float pullSpeed = 15f;

    [Tooltip("Distance threshold to consider coin reached and collect it")]
    public float collectDistance = 0.5f;

    [Header("Drift Phase Settings")]
    [Tooltip("Minimum duration of drift phase before magnet pull (seconds)")]
    public float driftDurationMin = 0.25f;

    [Tooltip("Maximum duration of drift phase before magnet pull (seconds)")]
    public float driftDurationMax = 0.45f;

    [Tooltip("Radius for random drift target around coin's entry point")]
    public float driftRadius = 0.6f;

    [Tooltip("Speed during drift phase")]
    public float driftSpeed = 3f;

    [Header("VFX Prefabs")]
    [Tooltip("Arc line VFX prefab (ArcLineVFX + LineRenderer). Assign plsm2 material on the LineRenderer.")]
    public ArcLineVFX arcVfxPrefab;

    [Tooltip("(Optional) Spark ParticleSystem prefab — spawned on coin during magnet pull")]
    public ParticleSystem sparkPrefab;

    [Header("Arm Timeout")]
    [Tooltip("Maximum seconds the magnet stays armed without collecting. 0 = no timeout.")]
    public float maxArmedDuration = 30f;

    [Header("Level Configuration: [Taps Required, Coins to Collect]")]
    [Tooltip("L1: [30, 3], L2: [40, 4], L3: [50, 5], L4: [55, 7], L5: [60, 9], L6: [70, 12]")]
    public int[] tapsRequired = { 30, 40, 50, 55, 60, 70 };
    public int[] coinsToCollect = { 3, 4, 5, 7, 9, 12 };

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // ── State ──
    private bool isArmed = false;
    private int currentTapCount = 0;
    private int quota = 0;          // Total coins to collect this activation
    private int coinsCollected = 0; // Magnet-collected so far this activation

    // Arm timeout
    private float armTime = 0f;

    // In-flight coin tracking (reservation).  Key = coin.GetInstanceID()
    private readonly Dictionary<int, NitroCoin> inFlightCoins = new Dictionary<int, NitroCoin>();

    // Coins spawned OUTSIDE the area bounds — checked every 0.1 s until they enter or are destroyed
    private readonly List<NitroCoin> trackedCoins = new List<NitroCoin>();

    // Coroutine handle for periodic bounds check
    private Coroutine boundsCheckRoutine;

    // ── Save Keys ──
    private const string SaveKey_TapCount = "Save_NitroMagnet_TapCount";
    private const string SaveKey_IsArmed = "Save_NitroMagnet_IsArmed";
    private const string SaveKey_Quota = "Save_NitroMagnet_Quota";
    private const string SaveKey_Collected = "Save_NitroMagnet_Collected";

    // ── Bounds check interval ──
    private const float BoundsCheckInterval = 0.1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Auto-assign areaCollider if not set in Inspector
        if (areaCollider == null)
            areaCollider = GetComponent<Collider>();

        // Load saved state
        LoadState();

        // Apply VFX to match loaded state
        if (shieldVFX != null)
            shieldVFX.SetActive(isArmed);

        if (magnetTarget == null)
            Debug.LogError("[NitroMagnetController] magnetTarget is NULL! Assign MagnetAnchor in Inspector.");
        if (areaCollider == null)
            Debug.LogError("[NitroMagnetController] areaCollider is NULL! Assign BoxCollider in Inspector.");
        if (tapsRequired.Length != coinsToCollect.Length)
            Debug.LogError("[NitroMagnetController] tapsRequired and coinsToCollect arrays must have same length!");

        // If loaded as armed, start bounds check coroutine
        if (isArmed)
            StartBoundsCheck();
    }

    private void Update()
    {
        // Arm timeout check
        if (isArmed && maxArmedDuration > 0f)
        {
            if (Time.time - armTime >= maxArmedDuration)
            {
                DisarmMagnet("timeout");
            }
        }
    }

    // ══════════════════════════════════════════
    //  TAP REGISTRATION
    // ══════════════════════════════════════════

    /// <summary>
    /// Called by TapInputRaycaster on each car tap.
    /// Increments tap counter and arms magnet when threshold reached.
    /// </summary>
    public void RegisterTap()
    {
        if (CardManager.Instance == null)
            return;

        int magnetLevel = CardManager.Instance.GetCardLevel(CardType.NitroMagnet);
        if (magnetLevel <= 0)
            return; // Card not owned

        if (isArmed)
            return; // Already armed, don't count more taps

        currentTapCount++;

        int requiredTaps = GetRequiredTaps(magnetLevel);
        if (currentTapCount >= requiredTaps)
        {
            ArmMagnet(magnetLevel);
        }
    }

    // ══════════════════════════════════════════
    //  SPAWN-BASED DETECTION  (primary path)
    // ══════════════════════════════════════════

    /// <summary>
    /// Called by NitroCoinSpawner immediately after a NitroCoin is instantiated.
    /// If magnet is armed: checks bounds and either starts pull or tracks the coin.
    /// </summary>
    public void OnCoinSpawned(NitroCoin coin)
    {
        if (!isArmed || coin == null || coin.IsCollected)
            return;

        if (IsInsideArea(coin.transform.position))
        {
            TryAcceptCoin(coin);
        }
        else
        {
            // Track for periodic bounds check
            if (!trackedCoins.Contains(coin))
                trackedCoins.Add(coin);
        }
    }

    // ══════════════════════════════════════════
    //  PERIODIC BOUNDS CHECK  (0.1 s coroutine)
    // ══════════════════════════════════════════

    private void StartBoundsCheck()
    {
        if (boundsCheckRoutine != null)
            return;
        boundsCheckRoutine = StartCoroutine(BoundsCheckLoop());
    }

    private void StopBoundsCheck()
    {
        if (boundsCheckRoutine != null)
        {
            StopCoroutine(boundsCheckRoutine);
            boundsCheckRoutine = null;
        }
    }

    private IEnumerator BoundsCheckLoop()
    {
        var wait = new WaitForSeconds(BoundsCheckInterval);
        while (isArmed)
        {
            yield return wait;
            ProcessTrackedCoins();
        }
        boundsCheckRoutine = null;
    }

    /// <summary>
    /// Iterates tracked coins; accepts those now inside bounds, removes nulls / collected.
    /// </summary>
    private void ProcessTrackedCoins()
    {
        for (int i = trackedCoins.Count - 1; i >= 0; i--)
        {
            NitroCoin coin = trackedCoins[i];

            // Destroyed or collected — remove silently
            if (coin == null || coin.IsCollected || coin.IsBeingMagnetPulled)
            {
                trackedCoins.RemoveAt(i);
                continue;
            }

            // Quota already full (reservation)?
            if (coinsCollected + inFlightCoins.Count >= quota)
                break; // no point checking more

            if (IsInsideArea(coin.transform.position))
            {
                trackedCoins.RemoveAt(i);
                TryAcceptCoin(coin);
            }
        }
    }

    // ══════════════════════════════════════════
    //  TRIGGER DETECTION (secondary / fallback)
    // ══════════════════════════════════════════

    private void OnTriggerEnter(Collider other)
    {
        if (!isArmed)
            return;

        NitroCoin coin = other.GetComponent<NitroCoin>();
        if (coin == null)
            return;

        // Remove from tracked list if present (avoid double processing)
        trackedCoins.Remove(coin);
        TryAcceptCoin(coin);
    }

    // ══════════════════════════════════════════
    //  ACCEPT / PULL
    // ══════════════════════════════════════════

    /// <summary>
    /// Attempt to accept a coin for magnet pulling.
    /// Reservation system: collected + inFlight must be &lt; quota.
    /// Returns true if accepted.
    /// </summary>
    private bool TryAcceptCoin(NitroCoin coin)
    {
        if (coin == null || coin.IsCollected || coin.IsBeingMagnetPulled)
            return false;

        int coinId = coin.GetInstanceID();

        // Already in-flight
        if (inFlightCoins.ContainsKey(coinId))
            return false;

        // ── RESERVATION GATE ──
        if (coinsCollected + inFlightCoins.Count >= quota)
            return false;

        // Reserve slot
        inFlightCoins.Add(coinId, coin);

        // Calculate drift target (random point near entry position)
        Vector3 driftTarget = coin.transform.position + Random.insideUnitSphere * driftRadius;
        driftTarget.y = coin.transform.position.y; // keep on same Y plane

        float driftDuration = Random.Range(driftDurationMin, driftDurationMax);

        // Start two-phase pull on coin
        coin.StartMagnetPull(
            magnetTarget,
            pullSpeed,
            collectDistance,
            OnCoinCollected,
            driftTarget,
            driftDuration,
            driftSpeed,
            arcVfxPrefab,
            sparkPrefab
        );

        if (enableDebugLogs)
        {
            Debug.Log($"[CardEffectApplied] NitroMagnet coin accepted for pull (coinId:{coinId}, inFlight:{inFlightCoins.Count}, collected:{coinsCollected}, quota:{quota})");
        }

        return true;
    }

    // ══════════════════════════════════════════
    //  BOUNDS HELPER
    // ══════════════════════════════════════════

    /// <summary>
    /// Returns true if worldPos is inside the areaCollider bounds.
    /// Works regardless of collider type (BoxCollider, etc.).
    /// </summary>
    private bool IsInsideArea(Vector3 worldPos)
    {
        if (areaCollider == null)
            return false;
        return areaCollider.bounds.Contains(worldPos);
    }

    // ══════════════════════════════════════════
    //  CALLBACKS
    // ══════════════════════════════════════════

    /// <summary>
    /// Callback when a coin is successfully collected by the magnet.
    /// </summary>
    private void OnCoinCollected(NitroCoin coin, int rewardAmount)
    {
        int coinId = coin.GetInstanceID();
        inFlightCoins.Remove(coinId);

        coinsCollected++;

        if (enableDebugLogs)
        {
            Debug.Log($"[CardEffectApplied] NitroMagnet coin collected by magnet (coinId:{coinId}, reward:{rewardAmount}, collected:{coinsCollected}/{quota})");
        }

        // Reset arm timer on each collection (prevents timeout while actively collecting)
        armTime = Time.time;

        // Check if quota fulfilled
        if (coinsCollected >= quota)
        {
            DisarmMagnet("quota_reached");
        }

        SaveState();
    }

    /// <summary>
    /// Called when a coin being pulled is tapped by player (player wins).
    /// Releases the reserved quota slot so another coin can be pulled.
    /// Does NOT spend magnet quota.
    /// </summary>
    public void NotifyCoinTappedWhilePulling(NitroCoin coin)
    {
        int coinId = coin.GetInstanceID();
        if (inFlightCoins.Remove(coinId))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardEffectApplied] NitroMagnet coin collected by player tap override (coinId:{coinId}, inFlight:{inFlightCoins.Count}, collected:{coinsCollected}, quota:{quota})");
            }
        }
    }

    // ══════════════════════════════════════════
    //  ARM / DISARM
    // ══════════════════════════════════════════

    private void ArmMagnet(int magnetLevel)
    {
        // Cancel any lingering in-flight coins (safety)
        CancelAllInFlightCoins("rearm");
        inFlightCoins.Clear();
        trackedCoins.Clear();

        isArmed = true;
        currentTapCount = 0;
        coinsCollected = 0;
        quota = GetCoinsToCollect(magnetLevel);
        armTime = Time.time;

        if (shieldVFX != null)
            shieldVFX.SetActive(true);

        if (enableDebugLogs)
        {
            int requiredTaps = GetRequiredTaps(magnetLevel);
            Debug.Log($"[CardEffectApplied] NitroMagnet ARM (level:{magnetLevel}, requiredTaps:{requiredTaps}, quota:{quota})");
        }

        SaveState();

        // Start periodic bounds check coroutine
        StartBoundsCheck();

        // Check for coins already inside area (Physics.OverlapBox)
        CheckForExistingCoinsInArea();
    }

    private void DisarmMagnet(string reason)
    {
        if (!isArmed)
            return;

        // Cancel all in-flight coins BEFORE clearing state
        CancelAllInFlightCoins(reason);

        isArmed = false;

        if (shieldVFX != null)
            shieldVFX.SetActive(false);

        StopBoundsCheck();

        if (enableDebugLogs)
        {
            int magnetLevel = CardManager.Instance != null ? CardManager.Instance.GetCardLevel(CardType.NitroMagnet) : 0;
            Debug.Log($"[CardEffectApplied] NitroMagnet DISARM (reason:{reason}, level:{magnetLevel}, collected:{coinsCollected}/{quota})");
        }

        inFlightCoins.Clear();
        trackedCoins.Clear();
        quota = 0;
        coinsCollected = 0;

        SaveState();
    }

    /// <summary>
    /// Public disarm for external use (e.g., card upgrade refresh).
    /// </summary>
    public void ForceDisarm()
    {
        DisarmMagnet("manual");
    }

    /// <summary>
    /// Cancels all currently in-flight coin pulls.
    /// </summary>
    private void CancelAllInFlightCoins(string reason)
    {
        foreach (var kvp in inFlightCoins)
        {
            NitroCoin coin = kvp.Value;
            if (coin != null)
            {
                coin.CancelMagnetPull(reason);
            }
        }
    }

    // ══════════════════════════════════════════
    //  OVERLAP CHECK (for coins already in area on arm)
    // ══════════════════════════════════════════

    private void CheckForExistingCoinsInArea()
    {
        if (areaCollider == null)
            return;

        BoxCollider box = areaCollider as BoxCollider;
        if (box != null)
        {
            // Precise OverlapBox using BoxCollider geometry
            Collider[] nearbyColliders = Physics.OverlapBox(
                box.transform.TransformPoint(box.center),
                Vector3.Scale(box.size / 2f, box.transform.lossyScale),
                box.transform.rotation
            );
            foreach (var col in nearbyColliders)
            {
                NitroCoin coin = col.GetComponent<NitroCoin>();
                if (coin != null)
                    TryAcceptCoin(coin);
            }
        }
        else
        {
            // Fallback: use bounds-based OverlapBox
            Bounds b = areaCollider.bounds;
            Collider[] nearbyColliders = Physics.OverlapBox(b.center, b.extents);
            foreach (var col in nearbyColliders)
            {
                NitroCoin coin = col.GetComponent<NitroCoin>();
                if (coin != null)
                    TryAcceptCoin(coin);
            }
        }
    }

    // ══════════════════════════════════════════
    //  CARD LEVEL CHANGE (called from CardManager.ApplyCardEffect)
    // ══════════════════════════════════════════

    /// <summary>
    /// Called when NitroMagnet card level changes (via ApplyCardEffect).
    /// If armed, adjusts quota to new level's value.
    /// </summary>
    public void OnCardLevelChanged(int newLevel)
    {
        if (!isArmed)
            return;

        int newQuota = GetCoinsToCollect(newLevel);
        if (newQuota != quota)
        {
            quota = newQuota;
            if (coinsCollected >= quota)
            {
                DisarmMagnet("quota_reached");
            }
        }
    }

    // ══════════════════════════════════════════
    //  LEVEL CONFIG HELPERS
    // ══════════════════════════════════════════

    private int GetRequiredTaps(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, tapsRequired.Length - 1);
        return tapsRequired[index];
    }

    private int GetCoinsToCollect(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, coinsToCollect.Length - 1);
        return coinsToCollect[index];
    }

    // ══════════════════════════════════════════
    //  PUBLIC GETTERS
    // ══════════════════════════════════════════

    public bool IsArmed => isArmed;
    public int CurrentTapCount => currentTapCount;
    public int Quota => quota;
    public int CoinsCollected => coinsCollected;
    public int InFlightCount => inFlightCoins.Count;
    public int RemainingPulls => Mathf.Max(0, quota - coinsCollected);

    // ══════════════════════════════════════════
    //  SAVE / LOAD
    // ══════════════════════════════════════════

    public void SaveState()
    {
        PlayerPrefs.SetInt(SaveKey_TapCount, currentTapCount);
        PlayerPrefs.SetInt(SaveKey_IsArmed, isArmed ? 1 : 0);
        PlayerPrefs.SetInt(SaveKey_Quota, quota);
        PlayerPrefs.SetInt(SaveKey_Collected, coinsCollected);
    }

    public void LoadState()
    {
        currentTapCount = PlayerPrefs.GetInt(SaveKey_TapCount, 0);
        bool wasArmed = PlayerPrefs.GetInt(SaveKey_IsArmed, 0) == 1;
        quota = PlayerPrefs.GetInt(SaveKey_Quota, 0);
        coinsCollected = PlayerPrefs.GetInt(SaveKey_Collected, 0);

        if (wasArmed && quota > 0 && coinsCollected < quota)
        {
            isArmed = true;
            armTime = Time.time;
            inFlightCoins.Clear();
        }
        else
        {
            isArmed = false;
            quota = 0;
            coinsCollected = 0;
        }
    }

    private void OnDestroy()
    {
        StopBoundsCheck();
        if (Instance == this)
            Instance = null;
    }

    // ══════════════════════════════════════════
    //  EDITOR GIZMO — draws NitroMagnetArea bounds
    // ══════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Collider col = areaCollider != null ? areaCollider : GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = isArmed ? new Color(0f, 1f, 0.5f, 0.35f) : new Color(1f, 1f, 0f, 0.2f);
        Gizmos.matrix = Matrix4x4.identity;

        BoxCollider box = col as BoxCollider;
        if (box != null)
        {
            // Draw oriented box matching the BoxCollider exactly
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
            Gizmos.matrix = Matrix4x4.TRS(center, box.transform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);

            // Wireframe outline
            Gizmos.color = isArmed ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
        else
        {
            // Generic AABB fallback
            Bounds b = col.bounds;
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = isArmed ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(b.center, b.size);
        }

        // Draw magnetTarget position
        if (magnetTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(magnetTarget.position, 0.3f);
        }
    }
#endif
}
