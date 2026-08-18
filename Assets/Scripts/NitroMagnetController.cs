using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

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

    /// <summary>
    /// Fired when a coin is collected by the magnet. Passes reward amount.
    /// Used by BlacklistStatTracker to count lifetime magnet coin collections.
    /// </summary>
    public static event System.Action<int> OnMagnetCoinCollected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnMagnetCoinCollected = null;
    }

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

    [Header("Level Configuration: [Taps Required, Coins to Collect]  (nerfed)")]
    [Tooltip("L1: [40, 2], L2: [50, 3], L3: [60, 4], L4: [70, 5], L5: [80, 7], L6: [90, 9]")]
    public int[] tapsRequired = { 40, 50, 60, 70, 80, 90 };
    public int[] coinsToCollect = { 2, 3, 4, 5, 7, 9 };

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // ── VFX Fade Settings ──
    [Header("VFX Fade & Coin-Proximity Monitoring")]
    [Tooltip("Duration of the shield VFX fade-in (seconds)")]
    public float vfxFadeInDuration = 0.35f;

    [Tooltip("Duration of the shield VFX fade-out (seconds)")]
    public float vfxFadeOutDuration = 0.25f;

    [Tooltip("Z threshold: VFX activates when any NitroCoin's Z drops below this value (meaning it has moved toward the player). Coins spawn at Z≈74 and travel toward Z≈-7.")]
    public float coinZThreshold = 42f;

    [Tooltip("How often to scan for NitroCoins near the player (seconds)")]
    public float vfxMonitorInterval = 0.12f;

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

    // ── Nitro Rain Synergy ──
    private bool _nitroRainActive = false;
    private bool _subscribedToRain = false;
    private bool _postRainDraining = false;

    // ── Cooldown System ──
    [Header("Cooldown (seconds per level: L1=60, L2=90, L3=120, …  +30 each)")]
    [Tooltip("Base cooldown at level 1 in seconds")]
    [SerializeField] private float cooldownBase = 60f;
    [Tooltip("Seconds added per level above 1")]
    [SerializeField] private float cooldownPerLevel = 30f;
    [Tooltip("Cooldown multiplier when Nitro Rain overlapped with the magnet session")]
    [SerializeField] private float rainCooldownMultiplier = 2f;

    private bool _isOnCooldown = false;
    private float _cooldownEndTime = 0f;
    private bool _rainOverlappedThisSession = false;

    private const string SaveKey_CooldownEnd = "Save_NitroMagnet_CooldownEnd";
    private const string SaveKey_CooldownActive = "Save_NitroMagnet_CooldownActive";

    // ── Chase Suspend State ──
    private bool _suspendedForChase;
    private int _suspendedQuota;
    private int _suspendedCollected;
    private float _suspendedRemainingTime;
    private bool _suspendedRainOverlapped;

    // ── VFX Fade State ──
    private Vector3 _shieldOriginalScale;
    private bool _shieldScaleCached;
    private bool _vfxVisible;           // current visual state
    private Tween _vfxFadeTween;        // active scale tween
    private Coroutine _vfxMonitorRoutine;

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

        // Apply VFX to match loaded state —
        // VFX starts hidden; monitoring coroutine will fade it in when coins approach
        if (shieldVFX != null)
        {
            CacheShieldScale();
            shieldVFX.transform.localScale = Vector3.zero;
            shieldVFX.SetActive(true); // keep active so DOTween can animate it
            _vfxVisible = false;
        }

        if (magnetTarget == null)
            Debug.LogError("[NitroMagnetController] magnetTarget is NULL! Assign MagnetAnchor in Inspector.");
        if (areaCollider == null)
            Debug.LogError("[NitroMagnetController] areaCollider is NULL! Assign BoxCollider in Inspector.");
        if (tapsRequired.Length != coinsToCollect.Length)
            Debug.LogError("[NitroMagnetController] tapsRequired and coinsToCollect arrays must have same length!");

        TrySubscribeToRain();

        // If loaded as armed, start bounds check coroutine and VFX monitor
        if (isArmed)
        {
            StartBoundsCheck();
            StartVFXMonitor();
        }
    }

    private void Update()
    {
        // Lazy-bind to NitroRainController if it wasn't ready at Start()
        if (!_subscribedToRain)
            TrySubscribeToRain();

        // ── Cooldown expiration ──
        if (_isOnCooldown && Time.time >= _cooldownEndTime)
        {
            _isOnCooldown = false;
            SaveState();

            if (enableDebugLogs)
                Debug.Log("[NitroMagnet] Cooldown EXPIRED — magnet can be armed again");
        }

        // Arm timeout check — skip timeout while rain synergy is active
        if (isArmed && maxArmedDuration > 0f && !_nitroRainActive)
        {
            if (Time.time - armTime >= maxArmedDuration)
            {
                DisarmMagnet("timeout");
            }
        }
    }

    // ══════════════════════════════════════════
    //  CAR REBIND (called by MainSceneCarController)
    // ══════════════════════════════════════════

    /// <summary>
    /// Rebinds magnetTarget, shieldVFX, and areaCollider to the children
    /// of the given car root.  Each car has its own MagnetAnchor hierarchy:
    ///   CarRoot/MagnetAnchor → Plasma Sphere (shield) + NitroMagnetArea (trigger).
    /// </summary>
    public void RefreshCarReferences(Transform activeCar)
    {
        if (activeCar == null) return;

        // Find MagnetAnchor under the active car (could be named MagnetAnchor, MagnetAnchor (1), etc.)
        Transform anchor = FindChildByPrefix(activeCar, "MagnetAnchor");
        if (anchor != null)
        {
            magnetTarget = anchor;

            // Shield VFX: first child named "Plasma Sphere*"
            Transform shield = FindChildByPrefix(anchor, "Plasma Sphere");
            if (shield != null)
                shieldVFX = shield.gameObject;

            // Area collider: child named "NitroMagnetArea*"
            Transform area = FindChildByPrefix(anchor, "NitroMagnetArea");
            if (area != null)
            {
                Collider col = area.GetComponent<Collider>();
                if (col != null)
                    areaCollider = col;
            }

            Debug.Log($"[NitroMagnet] Rebound to {activeCar.name}: anchor={anchor.name}, shield={shieldVFX?.name}, area={areaCollider?.name}");

            // ── DIAGNOSTIC: runtime object identity & transform chain ──
            LogShieldDiagnostics("RefreshCarReferences", activeCar);
        }
        else
        {
            Debug.LogWarning($"[NitroMagnet] MagnetAnchor not found under {activeCar.name}");
        }
    }

    /// <summary>
    /// TEMPORARY DIAGNOSTIC — prints full runtime identity & transform chain
    /// for the shield VFX. Remove after debugging.
    /// </summary>
    private void LogShieldDiagnostics(string caller, Transform activeCar)
    {
        Debug.Log($"[ShieldDiag][{caller}] ════════════════════════════════════════");
        Debug.Log($"[ShieldDiag] activeCar.name          = {(activeCar != null ? activeCar.name : "NULL")}");
        Debug.Log($"[ShieldDiag] activeCar.instanceID     = {(activeCar != null ? activeCar.GetInstanceID().ToString() : "N/A")}");
        Debug.Log($"[ShieldDiag] activeCar.localPos       = {(activeCar != null ? activeCar.localPosition.ToString("F4") : "N/A")}");
        Debug.Log($"[ShieldDiag] activeCar.worldPos       = {(activeCar != null ? activeCar.position.ToString("F4") : "N/A")}");
        Debug.Log($"[ShieldDiag] activeCar.localScale     = {(activeCar != null ? activeCar.localScale.ToString("F4") : "N/A")}");
        Debug.Log($"[ShieldDiag] activeCar.parent         = {(activeCar != null && activeCar.parent != null ? activeCar.parent.name : "NULL")}");

        if (magnetTarget != null)
        {
            Debug.Log($"[ShieldDiag] magnetTarget.name        = {magnetTarget.name}");
            Debug.Log($"[ShieldDiag] magnetTarget.instanceID   = {magnetTarget.GetInstanceID()}");
            Debug.Log($"[ShieldDiag] magnetTarget.localPos     = {magnetTarget.localPosition.ToString("F4")}");
            Debug.Log($"[ShieldDiag] magnetTarget.worldPos     = {magnetTarget.position.ToString("F4")}");
            Debug.Log($"[ShieldDiag] magnetTarget.localScale   = {magnetTarget.localScale.ToString("F4")}");
            Debug.Log($"[ShieldDiag] magnetTarget.parent       = {(magnetTarget.parent != null ? magnetTarget.parent.name : "NULL")}");
            Debug.Log($"[ShieldDiag] magnetTarget.fullPath     = {GetFullPath(magnetTarget)}");

            // Check for duplicate MagnetAnchors under activeCar
            int anchorCount = 0;
            foreach (Transform child in activeCar)
                if (child.name.StartsWith("MagnetAnchor")) anchorCount++;
            Debug.Log($"[ShieldDiag] MagnetAnchor count under {activeCar.name} = {anchorCount}");
        }
        else
        {
            Debug.LogWarning($"[ShieldDiag] magnetTarget is NULL");
        }

        if (shieldVFX != null)
        {
            Transform st = shieldVFX.transform;
            Debug.Log($"[ShieldDiag] shieldVFX.name           = {shieldVFX.name}");
            Debug.Log($"[ShieldDiag] shieldVFX.instanceID      = {shieldVFX.GetInstanceID()}");
            Debug.Log($"[ShieldDiag] shieldVFX.activeSelf      = {shieldVFX.activeSelf}");
            Debug.Log($"[ShieldDiag] shieldVFX.localPos        = {st.localPosition.ToString("F4")}");
            Debug.Log($"[ShieldDiag] shieldVFX.worldPos        = {st.position.ToString("F4")}");
            Debug.Log($"[ShieldDiag] shieldVFX.localScale      = {st.localScale.ToString("F4")}");
            Debug.Log($"[ShieldDiag] shieldVFX.lossyScale      = {st.lossyScale.ToString("F4")}");
            Debug.Log($"[ShieldDiag] shieldVFX.localRot        = {st.localRotation.eulerAngles.ToString("F2")}");
            Debug.Log($"[ShieldDiag] shieldVFX.parent          = {(st.parent != null ? st.parent.name : "NULL")}");
            Debug.Log($"[ShieldDiag] shieldVFX.fullPath        = {GetFullPath(st)}");
            Debug.Log($"[ShieldDiag] shieldVFX.childCount      = {st.childCount}");

            // Check for duplicate Plasma Spheres under magnetTarget
            if (magnetTarget != null)
            {
                int shieldCount = 0;
                foreach (Transform child in magnetTarget)
                    if (child.name.StartsWith("Plasma Sphere")) shieldCount++;
                Debug.Log($"[ShieldDiag] Plasma Sphere count under {magnetTarget.name} = {shieldCount}");
            }

            // MeshFilter check
            var mf = shieldVFX.GetComponent<MeshFilter>();
            Debug.Log($"[ShieldDiag] shieldVFX mesh             = {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "NONE")}");
            var mr = shieldVFX.GetComponent<MeshRenderer>();
            Debug.Log($"[ShieldDiag] shieldVFX material          = {(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "NONE")}");

            // Print full parent chain with world positions
            Debug.Log($"[ShieldDiag] ── Parent chain (child → root) ──");
            Transform current = st;
            int depth = 0;
            while (current != null && depth < 10)
            {
                Debug.Log($"[ShieldDiag]   [{depth}] {current.name} | localPos={current.localPosition.ToString("F4")} worldPos={current.position.ToString("F4")} localScale={current.localScale.ToString("F4")}");
                current = current.parent;
                depth++;
            }
        }
        else
        {
            Debug.LogWarning($"[ShieldDiag] shieldVFX is NULL");
        }

        Debug.Log($"[ShieldDiag] _shieldScaleCached        = {_shieldScaleCached}");
        Debug.Log($"[ShieldDiag] _shieldOriginalScale       = {_shieldOriginalScale.ToString("F4")}");
        Debug.Log($"[ShieldDiag] ════════════════════════════════════════");
    }

    private static string GetFullPath(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    /// <summary>Finds a direct child whose name starts with the given prefix.</summary>
    private static Transform FindChildByPrefix(Transform parent, string prefix)
    {
        foreach (Transform child in parent)
        {
            if (child.name.StartsWith(prefix))
                return child;
        }
        // Recursive fallback for nested prefabs
        foreach (Transform child in parent)
        {
            Transform found = FindChildByPrefix(child, prefix);
            if (found != null) return found;
        }
        return null;
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

        if (_isOnCooldown)
            return; // Cooldown active, block activation

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

            // Quota already full (reservation)? Skip during rain synergy or post-rain drain.
            if (!_nitroRainActive && !_postRainDraining && coinsCollected + inFlightCoins.Count >= quota)
                break; // no point checking more

            if (IsInsideArea(coin.transform.position))
            {
                trackedCoins.RemoveAt(i);
                TryAcceptCoin(coin);
            }
        }

        CheckDrainComplete();
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

        // ── RESERVATION GATE ── (bypassed during Nitro Rain synergy or post-rain drain)
        if (!_nitroRainActive && !_postRainDraining && coinsCollected + inFlightCoins.Count >= quota)
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

        OnMagnetCoinCollected?.Invoke(rewardAmount);

        if (enableDebugLogs)
        {
            Debug.Log($"[CardEffectApplied] NitroMagnet coin collected by magnet (coinId:{coinId}, reward:{rewardAmount}, collected:{coinsCollected}/{quota})");
        }

        // Reset arm timer on each collection (prevents timeout while actively collecting)
        armTime = Time.time;

        // Check if quota fulfilled (skip during rain synergy or post-rain drain — magnet stays armed)
        if (!_nitroRainActive && !_postRainDraining && coinsCollected >= quota)
        {
            DisarmMagnet("quota_reached");
        }

        CheckDrainComplete();

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
        _rainOverlappedThisSession = false;
        quota = GetCoinsToCollect(magnetLevel);
        armTime = Time.time;

        if (shieldVFX != null)
        {
            // VFX starts hidden; the monitoring coroutine will fade it in
            // once NitroCoins cross the Z threshold.
            CacheShieldScale();
            shieldVFX.transform.localScale = Vector3.zero;
            shieldVFX.SetActive(true);
            _vfxVisible = false;

            // ── DIAGNOSTIC ──
            Debug.Log($"[ShieldDiag][ArmMagnet] cachedScale={_shieldOriginalScale.ToString("F4")} | shieldVFX.localPos={shieldVFX.transform.localPosition.ToString("F4")} worldPos={shieldVFX.transform.position.ToString("F4")} | mesh={(shieldVFX.GetComponent<MeshFilter>()?.sharedMesh?.name ?? "NONE")} | mat={(shieldVFX.GetComponent<MeshRenderer>()?.sharedMaterial?.name ?? "NONE")}");
        }

        if (enableDebugLogs)
        {
            int requiredTaps = GetRequiredTaps(magnetLevel);
            Debug.Log($"[CardEffectApplied] NitroMagnet ARM (level:{magnetLevel}, requiredTaps:{requiredTaps}, quota:{quota})");
        }

        SaveState();

        // N9: Magnet shield activation SFX
        if (SFXManager.Instance != null) SFXManager.Instance.PlayMagnetActivate();

        // Start periodic bounds check coroutine
        StartBoundsCheck();

        // Start VFX proximity monitoring
        StartVFXMonitor();

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

        // Fade out VFX smoothly instead of instant disable
        FadeOutVFX();
        StopVFXMonitor();

        StopBoundsCheck();

        if (enableDebugLogs)
        {
            int magnetLevel = CardManager.Instance != null ? CardManager.Instance.GetCardLevel(CardType.NitroMagnet) : 0;
            Debug.Log($"[CardEffectApplied] NitroMagnet DISARM (reason:{reason}, level:{magnetLevel}, collected:{coinsCollected}/{quota})");
        }

        inFlightCoins.Clear();
        trackedCoins.Clear();
        _postRainDraining = false;
        quota = 0;
        coinsCollected = 0;

        // ── Start cooldown ──
        StartCooldown();

        // N11: Magnet shield deactivation SFX
        if (SFXManager.Instance != null) SFXManager.Instance.PlayMagnetDeactivate();

        SaveState();
    }

    /// <summary>
    /// Public disarm for external use (e.g., card upgrade refresh).
    /// </summary>
    public void ForceDisarm()
    {
        DisarmMagnet("manual");
    }

    // ══════════════════════════════════════════════
    //  POLICE CHASE SUSPEND / RESUME
    // ══════════════════════════════════════════════

    /// <summary>
    /// Suspends the Nitro Magnet for the duration of a police chase.
    /// Saves session progress, fades out VFX, stops monitors, clears isArmed
    /// WITHOUT triggering cooldown.
    /// </summary>
    public void SuspendForChase()
    {
        if (_suspendedForChase) return;

        if (!isArmed)
        {
            Debug.Log("[NitroMagnet] SuspendForChase: not armed, nothing to suspend");
            return;
        }

        // Save session progress
        _suspendedQuota = quota;
        _suspendedCollected = coinsCollected;
        _suspendedRainOverlapped = _rainOverlappedThisSession;
        float elapsed = Time.time - armTime;
        _suspendedRemainingTime = Mathf.Max(0f, maxArmedDuration - elapsed);

        // Cancel in-flight coins cleanly
        CancelAllInFlightCoins("chase_suspend");

        // Fade out VFX (plays close animation)
        if (_vfxVisible)
            FadeOutVFX();

        // Stop monitors
        StopVFXMonitor();
        StopBoundsCheck();

        // Clear armed state — no cooldown, no SFX, no save
        isArmed = false;
        inFlightCoins.Clear();
        trackedCoins.Clear();

        _suspendedForChase = true;
        Debug.Log($"[NitroMagnet] SuspendForChase: suspended (quota={_suspendedQuota}, collected={_suspendedCollected}, remaining={_suspendedRemainingTime:F1}s)");
    }

    /// <summary>
    /// Resumes the Nitro Magnet after a police chase ends.
    /// Re-arms with saved session state so the player doesn't lose progress.
    /// </summary>
    public void ResumeAfterChase()
    {
        if (!_suspendedForChase)
        {
            Debug.Log("[NitroMagnet] ResumeAfterChase: was not suspended, skipping");
            return;
        }

        _suspendedForChase = false;

        // If no remaining time or quota already met, just start cooldown
        if (_suspendedRemainingTime <= 0f || _suspendedCollected >= _suspendedQuota)
        {
            Debug.Log("[NitroMagnet] ResumeAfterChase: no time left or quota met, starting cooldown");
            StartCooldown();
            return;
        }

        // Re-arm with restored state
        isArmed = true;
        quota = _suspendedQuota;
        coinsCollected = _suspendedCollected;
        _rainOverlappedThisSession = _suspendedRainOverlapped;
        armTime = Time.time - (maxArmedDuration - _suspendedRemainingTime);

        if (shieldVFX != null)
        {
            CacheShieldScale();
            shieldVFX.transform.localScale = Vector3.zero;
            shieldVFX.SetActive(true);
            _vfxVisible = false;
        }

        // Restart monitors
        StartBoundsCheck();
        StartVFXMonitor();

        Debug.Log($"[NitroMagnet] ResumeAfterChase: re-armed (quota={quota}, collected={coinsCollected}, remaining={_suspendedRemainingTime:F1}s)");
    }

    /// <summary>
    /// DEBUG ONLY: Immediately arms the magnet regardless of tap count.
    /// Uses the current card level, or the given level override.
    /// </summary>
    public void DebugForceArm(int levelOverride = -1)
    {
        int level = levelOverride >= 1 ? levelOverride : 1;
        if (CardManager.Instance != null)
        {
            int cardLevel = CardManager.Instance.GetCardLevel(CardType.NitroMagnet);
            if (cardLevel >= 1 && levelOverride < 1)
                level = cardLevel;
        }

        if (isArmed)
            DisarmMagnet("debug_rearm");

        // Clear cooldown for debug arming
        _isOnCooldown = false;
        _cooldownEndTime = 0f;

        ArmMagnet(level);
        Debug.Log($"[DebugCardLoadout] NitroMagnet force-armed at level {level}");
    }

    /// <summary>
    /// Cancels all currently in-flight coin pulls and destroys the coins.
    /// Mid-pull coins are removed rather than resuming normal movement
    /// (which would cause them to shoot off-screen).
    /// </summary>
    private void CancelAllInFlightCoins(string reason)
    {
        foreach (var kvp in inFlightCoins)
        {
            NitroCoin coin = kvp.Value;
            if (coin != null)
            {
                coin.CancelMagnetPull(reason);
                Destroy(coin.gameObject);
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
    public bool IsOnCooldown => _isOnCooldown;
    public float CooldownRemainingSeconds => _isOnCooldown ? Mathf.Max(0f, _cooldownEndTime - Time.time) : 0f;
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

        // Persist cooldown as UTC end time so offline time counts toward expiry
        PlayerPrefs.SetInt(SaveKey_CooldownActive, _isOnCooldown ? 1 : 0);
        if (_isOnCooldown)
        {
            double remainingSec = _cooldownEndTime - Time.time;
            if (remainingSec > 0)
            {
                long utcEnd = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)System.Math.Ceiling(remainingSec);
                PlayerPrefs.SetString(SaveKey_CooldownEnd, utcEnd.ToString());
            }
            else
            {
                PlayerPrefs.SetString(SaveKey_CooldownEnd, "0");
            }
        }
        else
        {
            PlayerPrefs.SetString(SaveKey_CooldownEnd, "0");
        }
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

        // Restore cooldown from UTC end timestamp (offline time counted)
        bool hadCooldown = PlayerPrefs.GetInt(SaveKey_CooldownActive, 0) == 1;
        if (hadCooldown)
        {
            string endStr = PlayerPrefs.GetString(SaveKey_CooldownEnd, "0");
            // Backward compat: old saves stored a float via SetFloat (key type may differ)
            if (!long.TryParse(endStr, out long utcEnd))
            {
                // Fallback: try reading as old float format (seconds remaining)
                float oldRemaining = PlayerPrefs.GetFloat(SaveKey_CooldownEnd, 0f);
                if (oldRemaining > 0f)
                {
                    _isOnCooldown = true;
                    _cooldownEndTime = Time.time + oldRemaining;
                }
                else
                {
                    _isOnCooldown = false;
                    _cooldownEndTime = 0f;
                }
            }
            else
            {
                long nowUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long remaining = utcEnd - nowUtc;
                if (remaining > 0)
                {
                    _isOnCooldown = true;
                    _cooldownEndTime = Time.time + remaining;
                }
                else
                {
                    // Cooldown expired while offline
                    _isOnCooldown = false;
                    _cooldownEndTime = 0f;
                }
            }
        }
        else
        {
            _isOnCooldown = false;
            _cooldownEndTime = 0f;
        }
    }

    private void OnDestroy()
    {
        StopBoundsCheck();
        StopVFXMonitor();
        KillVFXTween();
        UnsubscribeFromRain();
        if (Instance == this)
            Instance = null;
    }

    // ══════════════════════════════════════════
    //  NITRO RAIN SYNERGY
    // ══════════════════════════════════════════

    private void TrySubscribeToRain()
    {
        if (_subscribedToRain) return;
        var nrc = NitroRainController.Instance;
        if (nrc == null) return;

        nrc.OnRainStarted += HandleNitroRainStarted;
        nrc.OnRainEnded += HandleNitroRainEnded;
        _subscribedToRain = true;
    }

    private void UnsubscribeFromRain()
    {
        if (!_subscribedToRain) return;
        var nrc = NitroRainController.Instance;
        if (nrc != null)
        {
            nrc.OnRainStarted -= HandleNitroRainStarted;
            nrc.OnRainEnded -= HandleNitroRainEnded;
        }
        _subscribedToRain = false;
    }

    /// <summary>
    /// When Nitro Rain starts while magnet is armed, activate synergy:
    /// quota is bypassed so the magnet can pull all coins that enter the area.
    /// Coins still must enter the NitroMagnetArea BoxCollider before pull/VFX starts.
    /// </summary>
    private void HandleNitroRainStarted(float duration, int level)
    {
        _nitroRainActive = true;
        _postRainDraining = false;

        if (!isArmed) return;

        // Mark that rain overlapped with this magnet session (doubles cooldown)
        _rainOverlappedThisSession = true;

        if (enableDebugLogs)
            Debug.Log($"[NitroMagnet] Rain synergy ACTIVATED — quota bypassed, area detection active");

        // Reset arm timer so timeout doesn't fire during rain
        armTime = Time.time;

        // Track all existing scene coins so they get picked up when they enter the area
        TrackAllSceneCoins();
    }

    /// <summary>
    /// When Nitro Rain ends, enter drain mode: keep quota bypassed until all
    /// rain-spawned coins still in transit have been processed (collected or despawned).
    /// </summary>
    private void HandleNitroRainEnded()
    {
        _nitroRainActive = false;

        if (!isArmed) return;

        // Enter drain mode if there are still rain coins traveling or being pulled
        if (trackedCoins.Count > 0 || inFlightCoins.Count > 0)
        {
            _postRainDraining = true;

            if (enableDebugLogs)
                Debug.Log($"[NitroMagnet] Rain synergy ENDED — draining remaining coins (tracked:{trackedCoins.Count}, inFlight:{inFlightCoins.Count})");
        }
        else
        {
            // No coins left, reset counter for fresh quota cycle
            coinsCollected = 0;
            _postRainDraining = false;

            if (enableDebugLogs)
                Debug.Log($"[NitroMagnet] Rain synergy ENDED — no coins to drain");
        }
    }

    /// <summary>
    /// Checks if post-rain drain is complete (all rain coins collected or despawned).
    /// When complete, resets coinsCollected so the magnet gets a fresh quota cycle.
    /// </summary>
    private void CheckDrainComplete()
    {
        if (!_postRainDraining) return;
        if (trackedCoins.Count > 0 || inFlightCoins.Count > 0) return;

        _postRainDraining = false;
        coinsCollected = 0;

        if (enableDebugLogs)
            Debug.Log("[NitroMagnet] Post-rain drain complete — resuming normal quota");
    }

    // ══════════════════════════════════════════
    //  COOLDOWN
    // ══════════════════════════════════════════

    /// <summary>
    /// Returns the cooldown duration in seconds for the given card level.
    /// Pattern: L1=60s, L2=90s, L3=120s, … (+30s each level).
    /// Doubled if Nitro Rain overlapped with the magnet session.
    /// </summary>
    private float GetCooldownDuration(int level)
    {
        float baseDuration = cooldownBase + cooldownPerLevel * Mathf.Max(0, level - 1);
        if (_rainOverlappedThisSession)
            baseDuration *= rainCooldownMultiplier;
        return baseDuration;
    }

    /// <summary>
    /// Initiates cooldown after the magnet disarms.
    /// During cooldown, RegisterTap() rejects all taps.
    /// </summary>
    private void StartCooldown()
    {
        int magnetLevel = CardManager.Instance != null
            ? CardManager.Instance.GetCardLevel(CardType.NitroMagnet)
            : 1;
        if (magnetLevel <= 0) magnetLevel = 1;

        float duration = GetCooldownDuration(magnetLevel);
        _isOnCooldown = true;
        _cooldownEndTime = Time.time + duration;

        if (enableDebugLogs)
        {
            string rainTag = _rainOverlappedThisSession ? " (rain 2×)" : "";
            Debug.Log($"[NitroMagnet] Cooldown STARTED — {duration:F1}s{rainTag} (level:{magnetLevel})");
        }
    }

    /// <summary>
    /// Adds all existing NitroCoins in the scene to the tracked list.
    /// Those already inside the area are accepted immediately;
    /// others will be picked up by the periodic bounds check when they enter.
    /// </summary>
    private void TrackAllSceneCoins()
    {
        NitroCoin[] allCoins = FindObjectsByType<NitroCoin>(FindObjectsSortMode.None);
        foreach (NitroCoin coin in allCoins)
        {
            if (coin == null || coin.IsCollected || coin.IsBeingMagnetPulled)
                continue;

            if (IsInsideArea(coin.transform.position))
            {
                TryAcceptCoin(coin);
            }
            else if (!trackedCoins.Contains(coin))
            {
                trackedCoins.Add(coin);
            }
        }
    }

    // ══════════════════════════════════════════
    //  VFX FADE-IN / FADE-OUT (scale-based)
    // ══════════════════════════════════════════

    private void CacheShieldScale()
    {
        if (_shieldScaleCached || shieldVFX == null) return;
        _shieldOriginalScale = shieldVFX.transform.localScale;
        if (_shieldOriginalScale == Vector3.zero)
            _shieldOriginalScale = Vector3.one; // safety fallback
        _shieldScaleCached = true;
    }

    private void KillVFXTween()
    {
        if (_vfxFadeTween != null && _vfxFadeTween.IsActive())
        {
            _vfxFadeTween.Kill();
            _vfxFadeTween = null;
        }
    }

    private void FadeInVFX()
    {
        if (shieldVFX == null || _vfxVisible) return;
        _vfxVisible = true;
        KillVFXTween();

        CacheShieldScale();
        shieldVFX.SetActive(true);

        bool chaseActive = PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive;
        Debug.Log($"[MagnetVFXDebug] FadeInVFX: chaseActive={chaseActive} shieldVFX.localPos={shieldVFX.transform.localPosition.ToString("F4")} worldPos={shieldVFX.transform.position.ToString("F4")} parent={shieldVFX.transform.parent?.name ?? "NULL"} targetScale={_shieldOriginalScale.ToString("F4")}");

        _vfxFadeTween = shieldVFX.transform
            .DOScale(_shieldOriginalScale, vfxFadeInDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);

        if (enableDebugLogs)
        {
            Debug.Log("[NitroMagnetVFX] Fade-IN started");
            Debug.Log($"[ShieldDiag][FadeInVFX] DOScale target={_shieldOriginalScale.ToString("F4")} | currentScale={shieldVFX.transform.localScale.ToString("F4")} | localPos={shieldVFX.transform.localPosition.ToString("F4")} | worldPos={shieldVFX.transform.position.ToString("F4")} | fullPath={GetFullPath(shieldVFX.transform)}");
        }
    }

    private void FadeOutVFX()
    {
        if (shieldVFX == null || !_vfxVisible) return;
        _vfxVisible = false;
        KillVFXTween();

        bool chaseActive = PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive;
        Debug.Log($"[MagnetVFXDebug] FadeOutVFX: chaseActive={chaseActive} shieldVFX.localPos={shieldVFX.transform.localPosition.ToString("F4")} worldPos={shieldVFX.transform.position.ToString("F4")} scale={shieldVFX.transform.localScale.ToString("F4")}");

        _vfxFadeTween = shieldVFX.transform
            .DOScale(Vector3.zero, vfxFadeOutDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true);

        if (enableDebugLogs)
            Debug.Log("[NitroMagnetVFX] Fade-OUT started");
    }

    // ══════════════════════════════════════════
    //  VFX PROXIMITY MONITOR (coin Z check loop)
    // ══════════════════════════════════════════

    private void StartVFXMonitor()
    {
        if (_vfxMonitorRoutine != null) return;
        _vfxMonitorRoutine = StartCoroutine(VFXMonitorLoop());
    }

    private void StopVFXMonitor()
    {
        if (_vfxMonitorRoutine != null)
        {
            StopCoroutine(_vfxMonitorRoutine);
            _vfxMonitorRoutine = null;
        }
    }

    /// <summary>
    /// Continuously scans for active NitroCoins that have crossed the Z threshold
    /// (meaning they've moved close enough to the player). Drives VFX fade-in/out.
    /// Runs while the magnet is armed.
    /// </summary>
    private IEnumerator VFXMonitorLoop()
    {
        var wait = new WaitForSeconds(vfxMonitorInterval);

        while (isArmed)
        {
            bool anyCoinPastThreshold = false;

            // Scan all live NitroCoins in the scene
            NitroCoin[] coins = FindObjectsByType<NitroCoin>(FindObjectsSortMode.None);
            for (int i = 0; i < coins.Length; i++)
            {
                NitroCoin c = coins[i];
                if (c == null || c.IsCollected) continue;

                // Coin has moved past the threshold toward the player
                if (c.transform.position.z < coinZThreshold)
                {
                    anyCoinPastThreshold = true;
                    break; // one is enough
                }
            }

            if (anyCoinPastThreshold && !_vfxVisible)
                FadeInVFX();
            else if (!anyCoinPastThreshold && _vfxVisible)
                FadeOutVFX();

            yield return wait;
        }

        _vfxMonitorRoutine = null;
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
