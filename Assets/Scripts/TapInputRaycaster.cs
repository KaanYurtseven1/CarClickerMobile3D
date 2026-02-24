using UnityEngine;
using UnityEngine.InputSystem; // Yeni input system kullanıyorsun ya
using System.Collections.Generic;

public class TapInputRaycaster : MonoBehaviour
{
    public Camera cam;

    [Header("Floating Text Ayarları")]
    public float textYOffset = 0.1f; // arabanın üstünde biraz yukarı

    [Header("Raycast Layer Filtering")]
    [SerializeField] private LayerMask tapLayerMask = ~0; // All layers by default (configure in Inspector to exclude IgnoreRaycast/NitroMagnet layers)

    // Diagnostic gating for Momentum tap reward log (prevent spam)
    private static int _lastLoggedMomentumLevel = -1;
    private static int _lastLoggedMomentumStacks = -1;
    private static float _lastLoggedMomentumMultiplier = -1f;
    private static double _lastLoggedMomentumDelta = -1.0;

    // Pre-allocated buffer for RaycastNonAlloc (avoids GC; 16 is plenty for a single ray)
    private static readonly RaycastHit[] _rayHitBuffer = new RaycastHit[16];

    // Police chase tap isolation flag (set by PoliceCatchController)
    [HideInInspector] public bool isPoliceChaseActive = false;

    // Shield-block diagnostic (logged once)
    private bool _loggedShieldBlock = false;

    // Non-spam diagnostic state tracking
    private bool _prevIsFocused;
    private bool _prevMousePresent;
    private bool _prevIsPressed;
    private bool _loggedChestPopupBlock;
    private bool _loggedCameraNull;
    private HashSet<string> _loggedOnceKeys = new HashSet<string>();

    private void Start()
    {
        bool isFocused = Application.isFocused;
        bool mousePresent = (Mouse.current != null);

        Debug.Log($"[TapInput] Initialized - focused:{isFocused}, mouse:{mousePresent}, hybrid input enabled");

        _prevIsFocused = isFocused;
        _prevMousePresent = mousePresent;
        _prevIsPressed = false;

        // Layer mask sanity check — warn if set to Everything (common cause of Ground interception)
        if (tapLayerMask == ~0)
        {
            Debug.LogWarning("[TapInput] tapLayerMask is set to Everything. " +
                "Set it to 'Interactable' layer only (Car, NitroCoin, Chest, Radar) " +
                "to avoid Ground/VFX colliders intercepting taps.");
        }
    }

    private void Update()
    {
        // Camera check (log only once if null)
        if (cam == null) cam = Camera.main;
        if (cam == null && !_loggedCameraNull)
        {
            Debug.LogError("[TapInput] Camera is NULL (Camera.main not found). Check Main Camera tag!");
            _loggedCameraNull = true;
        }

        // Chest popup gate (log only first time it blocks)
        if (ChestPopupController.Instance != null &&
            ChestPopupController.Instance.IsPopupOpen)
        {
            if (!_loggedChestPopupBlock)
            {
                Debug.LogWarning("[TapInput] ChestPopup is OPEN - blocking all input");
                _loggedChestPopupBlock = true;
            }
            return;
        }
        else
        {
            _loggedChestPopupBlock = false;
        }

        // Radar popup gate
        if (RadarPopupController.Instance != null &&
            RadarPopupController.Instance.IsPopupOpen)
        {
            return;
        }

        // UI content-panel suppression gate (Bank / ShopCards / TimeWarp / Ranking open)
        // Police chase taps must still work even when a panel is open,
        // so this gate only applies to normal gameplay taps.
        if (UIFlowState.IsTapSuppressed && !isPoliceChaseActive)
        {
            return;
        }

        Vector2 screenPos = Vector2.zero;
        bool inputDetected = false;

#if UNITY_EDITOR
        // === EDITOR MODE: Multi-tier detection ===
        
        if (Mouse.current != null)
        {
            bool newIsPressed = Mouse.current.leftButton.isPressed;

            // 1) Try wasPressedThisFrame
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPos = Mouse.current.position.ReadValue();
                inputDetected = true;
            }
            // 2) Fallback: Rising edge detection (only if wasPressedThisFrame missed)
            else if (newIsPressed && !_prevIsPressed)
            {
                screenPos = Mouse.current.position.ReadValue();
                inputDetected = true;
            }

            // Always update previous state so the edge detector stays in sync
            _prevIsPressed = newIsPressed;
        }
        else
        {
            _prevIsPressed = false;
        }

        // 3) Fallback to Legacy Input
        if (!inputDetected && Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            inputDetected = true;
        }
#else
        // === MOBILE MODE: Try New Input System first, fallback to Old Input ===

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            inputDetected = true;
        }
        else if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                screenPos = touch.position;
                inputDetected = true;
            }
        }
#endif

        if (inputDetected)
        {
            HandleTap(screenPos);
        }
    }


    private void HandleTap(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("[TapInput] HandleTap - Camera is NULL! Cannot raycast.");
            return;
        }

        Ray ray = cam.ScreenPointToRay(screenPos);

        // RaycastNonAlloc: penetrate through VFX/shield objects to find tappable targets.
        // Uses pre-allocated buffer (_rayHitBuffer) to avoid GC allocation.
        int hitCount = Physics.RaycastNonAlloc(ray, _rayHitBuffer, 5000f, tapLayerMask, QueryTriggerInteraction.Ignore);

        // Find the closest hit whose tag is a tappable target (Car, NitroCoin, Chest).
        // Any non-tappable collider (e.g., shield VFX) is skipped.
        RaycastHit bestHit = default;
        bool foundValid = false;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            ref RaycastHit rh = ref _rayHitBuffer[i];
            string tag = rh.collider.tag;

            if (tag == "Car" || tag == "NitroCoin" || tag == "Chest" || tag == "Radar")
            {
                if (rh.distance < bestDist)
                {
                    bestHit = rh;
                    bestDist = rh.distance;
                    foundValid = true;
                }
            }
            else if (!_loggedShieldBlock)
            {
                // A non-tappable object (likely shield VFX) is intercepting the ray.
                // Log once to help diagnosis; the raycast still penetrates past it.
                Debug.LogWarning(
                    $"[TapInput] Non-tappable object '{rh.collider.gameObject.name}' (tag:'{tag}') " +
                    $"intercepted raycast at dist:{rh.distance:F1}. Skipping. " +
                    "Consider adding ShieldColliderDisabler or removing its collider.");
                _loggedShieldBlock = true;
            }
        }

        if (!foundValid)
        {
            if (hitCount > 0)
            {
                // We hit something but nothing tappable — all hits were VFX/shield
                // (already logged above if first time)
            }
            else
            {
                Debug.Log("[TapInput] Raycast missed (no valid collider)");
            }
            return;
        }

        // ── Process the valid hit ──
        RaycastHit hit = bestHit;

        if (hit.collider.CompareTag("Car"))
        {
            // ── Police chase tap isolation ──
            // During chase, car taps route to minigame only — no economy, no cards.
            // Visual feedback (scale + SFX) still fires so the player feels the tap.
            if (isPoliceChaseActive)
            {
                if (PoliceCatchController.Instance != null)
                    PoliceCatchController.Instance.OnChaseTap();

                // Visual feedback only (NO economy, NO momentum, NO turbo, NO magnet)
                var chaseFeedback = hit.collider.GetComponentInParent<ClickScaleFeedback>();
                if (chaseFeedback != null)
                    chaseFeedback.PlayFeedback();

                if (SFXManager.Instance != null)
                    SFXManager.Instance.PlayCarTap();

                return;
            }

            var cm = CurrencyManager.Instance;
            if (cm != null)
            {
                // --- Notify TurboFingerController of tap (for activation tracking) ---
                if (TurboFingerController.Instance != null)
                {
                    TurboFingerController.Instance.OnTap();
                }

                // --- Notify MomentumController of tap (stack building) ---
                // TIMING NOTE: RegisterClick is called BEFORE reward calculation.
                // This means the click that builds stack N also benefits from stack N's multiplier.
                // Example: stack=0 → RegisterClick → stack=1 → multiplier=1.005 → reward uses ×1.005
                // If you want stack N to apply to click N+1 (delayed benefit), move this AFTER AddMoney().
                if (MomentumController.Instance != null)
                {
                    MomentumController.Instance.RegisterClick(isAutoClick: false);
                }

                // --- Notify NitroMagnetController of tap (for activation tracking) ---
                if (NitroMagnetController.Instance != null)
                {
                    NitroMagnetController.Instance.RegisterTap();
                }

                // --- Calculate base MPT ---
                double tapBase = cm.moneyPerTap;
                double cardBonus = 0;

                if (CardManager.Instance != null)
                {
                    cardBonus = CardManager.Instance.GetTapBonusFromCards();
                    // TurboFinger effect handled by TurboFingerController (old charge system disabled)
                }

                double baseAmount = (tapBase + cardBonus) * cm.incomeBoostMultiplier;

                // --- Apply TurboFinger multiplier (5x when active) ---
                float turboMultiplier = TurboFingerController.Instance != null
                    ? TurboFingerController.Instance.CurrentMultiplier
                    : 1f;

                // --- Apply Momentum multiplier (click combo stacks) ---
                float momentumMultiplier = MomentumController.Instance != null
                    ? MomentumController.Instance.CurrentMultiplier
                    : 1f;

                double finalAmount = baseAmount * turboMultiplier * momentumMultiplier;

                // [CardEffectApplied] Diagnostic: log when Momentum effect modifies click reward (gated to prevent spam)
                if (momentumMultiplier > 1.0f)
                {
                    int momentumStacks = MomentumController.Instance != null ? MomentumController.Instance.CurrentStacks : 0;
                    int momentumLevel = CardManager.Instance != null ? CardManager.Instance.GetCardLevel(CardType.Momentum) : 0;
                    double momentumDelta = baseAmount * turboMultiplier * (momentumMultiplier - 1f);

                    // Only log if something meaningful changed
                    bool shouldLog = (momentumLevel != _lastLoggedMomentumLevel) ||
                                     (momentumStacks != _lastLoggedMomentumStacks) ||
                                     (Mathf.Abs(momentumMultiplier - _lastLoggedMomentumMultiplier) > 0.05f) ||
                                     (System.Math.Abs(momentumDelta - _lastLoggedMomentumDelta) > 1.0);

                    if (shouldLog)
                    {
                        Debug.Log($"[CardEffectApplied] Momentum L{momentumLevel} applied to click: stacks={momentumStacks}, multiplier=x{momentumMultiplier:F3}, base={baseAmount * turboMultiplier:F1}, bonus=+{momentumDelta:F1}, final={finalAmount:F1}, context=TapReward");
                        _lastLoggedMomentumLevel = momentumLevel;
                        _lastLoggedMomentumStacks = momentumStacks;
                        _lastLoggedMomentumMultiplier = momentumMultiplier;
                        _lastLoggedMomentumDelta = momentumDelta;
                    }
                }

                // Log for TAP verification
                double baseTapForLog = tapBase + cardBonus;
                float totalMultiplier = cm.incomeBoostMultiplier * turboMultiplier * momentumMultiplier;
                cm.AddMoney(finalAmount, "TAP", baseTapForLog, totalMultiplier);

                // TurboFinger effect handled by TurboFingerController (old NotifyTap disabled)

                // ---- RASTGELE POZİSYON HESABI ----
                Vector3 randomWorldPos = GetRandomPointOnCar(hit.collider);

                // Floating text shows the FINAL amount (after multiplier)
                if (ClickTextSpawner.Instance != null)
                {
                    ClickTextSpawner.Instance.SpawnClickText(
                        randomWorldPos,
                        finalAmount
                    );
                }
            }

            // Scale feedback
            var feedback = hit.collider.GetComponentInParent<ClickScaleFeedback>();
            if (feedback != null)
            {
                feedback.PlayFeedback();
            }

            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayCarTap();
            }
        }
        else if (hit.collider.CompareTag("Chest"))
        {
            Chest chest = hit.collider.GetComponentInParent<Chest>();
            if (chest != null)
            {
                chest.OnTapped();
            }
        }
        else if (hit.collider.CompareTag("NitroCoin"))
        {
            NitroCoin nitro = hit.collider.GetComponentInParent<NitroCoin>();
            if (nitro != null)
            {
                nitro.OnTapped();
            }
        }
        else if (hit.collider.CompareTag("Radar"))
        {
            Radar radar = hit.collider.GetComponentInParent<Radar>();
            if (radar != null)
            {
                radar.OnTapped();
            }
        }
    }

    /// <summary>  
    /// Arabanın collider bounds'ı üzerinden üst yüzeyde rastgele bir nokta seçer.
    /// </summary>
    private Vector3 GetRandomPointOnCar(Collider col)
    {
        Bounds b = col.bounds;

        // Biraz içerden başlasın diye küçük margin (yüzde 10 içeri)
        float marginX = b.size.x * 0.1f;
        float marginZ = b.size.z * 0.1f;

        float minX = b.min.x + marginX;
        float maxX = b.max.x - marginX;
        float minZ = b.min.z + marginZ;
        float maxZ = b.max.z - marginZ;

        // Çok ince collider durumunda margin aşırı olursa clamp’le
        if (minX > maxX)
        {
            float midX = (b.min.x + b.max.x) * 0.5f;
            minX = maxX = midX;
        }

        if (minZ > maxZ)
        {
            float midZ = (b.min.z + b.max.z) * 0.5f;
            minZ = maxZ = midZ;
        }

        float randX = Random.Range(minX, maxX);
        float randZ = Random.Range(minZ, maxZ);

        // Y ekseni: arabanın tam üstü + ufak offset
        float y = b.max.y + textYOffset;

        return new Vector3(randX, y, randZ);
    }
}
