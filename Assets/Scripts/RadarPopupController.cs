using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using System;

/// <summary>
/// Shows a brief radar-snapshot popup when the player misses a radar.
/// Captures one frame from SnapshotCamera into the RenderTexture,
/// displays the popup with fade+zoom animation, then auto-closes.
///
/// Fires OnRadarPopupClosed when the popup finishes (animation complete or force-closed).
/// PoliceCatchTrigger listens to this to defer Police Chase start until after the popup.
///
/// Inspector wiring for chestShown:
///   Drag the "ChestShown" GameObject (child of Canvas) into the chestShown field.
///   If left null, the controller will try to find it by name under "Canvas/ChestShown" at runtime.
/// </summary>
public class RadarPopupController : MonoBehaviour
{
    public static RadarPopupController Instance;

    /// <summary>
    /// Fired when a radar popup finishes closing (animation complete or force-closed).
    /// PoliceCatchTrigger uses this to know when it is safe to start a deferred Police Chase.
    /// </summary>
    public static event Action OnRadarPopupClosed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnRadarPopupClosed = null;
    }

    [Header("UI")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private RawImage snapshotImage;

    [Header("ChestShown (hidden while popup is open)")]
    [Tooltip("Optional: drag Canvas/ChestShown here. If null, auto-found at runtime.")]
    [SerializeField] private GameObject chestShown;

    [Header("Camera")]
    [SerializeField] private Camera snapshotCamera;

    [Header("Camera Poses (scene transforms defining position+rotation)")]
    [Tooltip("3 camera poses for radars that spawned on the left side.")]
    [SerializeField] private Transform[] leftCameraPoses;
    [Tooltip("3 camera poses for radars that spawned on the right side.")]
    [SerializeField] private Transform[] rightCameraPoses;

    [Header("Micro-Jitter")]
    [Tooltip("Random X offset applied to pose (±).")]
    [SerializeField] private float jitterX = 0.05f;
    [Tooltip("Random Y-axis yaw offset applied to pose (± degrees).")]
    [SerializeField] private float jitterYaw = 1.5f;

    [Header("Timing")]
    [Tooltip("How long the popup stays visible (seconds).")]
    [SerializeField] private float displayDuration = 2f;

    [Header("Animation")]
    [Tooltip("Fade+zoom in duration.")]
    [SerializeField] private float animIn = 0.18f;
    [Tooltip("Fade+zoom out duration.")]
    [SerializeField] private float animOut = 0.18f;
    [Tooltip("Scale at start/end of animation (< 1 = zoom-in effect).")]
    [SerializeField] private float zoomScale = 0.92f;

    [Header("Tap-to-Dismiss")]
    [Tooltip("When true, any tap while the popup is visible immediately starts the close animation.\nThe popup plays its full animOut sequence — it does not skip instantly.")]
    [SerializeField] private bool tapToDismiss = true;
    [Tooltip("Log to the Console when a tap-to-dismiss event fires.")]
    [SerializeField] private bool tapToDismissDebugLog = false;

    /// <summary>True during the entire visible window (including fade in/out).</summary>
    public bool IsPopupOpen { get; private set; }

    /// <summary>
    /// True while the animated close is running (set by RequestClose or the auto-timer path).
    /// Guards against duplicate close triggers during the animOut window.
    /// </summary>
    public bool IsClosing => _isClosing;

    private CanvasGroup _canvasGroup;
    private Sequence _popupSequence;
    private bool _chestShownPreviousState;

    /// <summary>Prevents duplicate close triggers during the animOut window.</summary>
    private bool _isClosing;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Start hidden
        if (popupRoot != null)
            popupRoot.SetActive(false);

        // Ensure snapshot camera is disabled (we only render manually)
        if (snapshotCamera != null)
            snapshotCamera.enabled = false;

        // Ensure CanvasGroup exists on popupRoot
        if (popupRoot != null)
        {
            _canvasGroup = popupRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = popupRoot.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        // Auto-find ChestShown if not assigned in Inspector
        if (chestShown == null)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Transform t = canvas.transform.Find("ChestShown");
                if (t != null)
                    chestShown = t.gameObject;
            }
        }
    }

    private void Update()
    {
        // Tap-to-dismiss: any tap while the popup is open triggers the animated close.
        // TapInputRaycaster already blocks gameplay taps while popup is open — this is the dedicated handler.
        if (!tapToDismiss || !IsPopupOpen || _isClosing) return;

        bool tapped = false;
#if UNITY_EDITOR
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            tapped = true;
        if (!tapped && Input.GetMouseButtonDown(0))
            tapped = true;
#else
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            tapped = true;
        if (!tapped && Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            tapped = true;
#endif
        if (tapped)
        {
            if (tapToDismissDebugLog)
                Debug.Log("[RadarPopup] Tap-to-dismiss — starting animated close.");
            RequestClose();
        }
    }

    private void OnDestroy()
    {
        _isClosing = false;
        KillSequence();
        RestoreChestShown();
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        _isClosing = false;
        KillSequence();
        RestoreChestShown();
    }

    /// <summary>
    /// Captures a single-frame snapshot and shows the popup.
    /// Kept for backward compatibility (uses default Left side).
    /// </summary>
    public void ShowSnapshot()
    {
        ShowSnapshot(RadarSide.Left);
    }

    /// <summary>
    /// Captures a snapshot from a pose matching the radar's side, then shows the popup.
    /// </summary>
    public void ShowSnapshot(RadarSide side)
    {
        if (popupRoot == null) return;

        // P10: Radar popup snapshot SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayRadarPopup();

        // Kill any previous popup animation to avoid overlap
        KillSequence();

        // Reset close state so a fresh popup is fully interactive
        _isClosing = false;

        // Capture snapshot with side-based camera pose
        CaptureFrame(side);

        // Hide ChestShown (store previous state for restore)
        if (chestShown != null)
        {
            _chestShownPreviousState = chestShown.activeSelf;
            chestShown.SetActive(false);
        }

        // Mark open immediately (before animation starts)
        IsPopupOpen = true;

        // Prepare initial state: transparent + zoomed down
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
        popupRoot.transform.localScale = Vector3.one * zoomScale;

        // Activate popupRoot so animation is visible
        popupRoot.SetActive(true);

        // Build DOTween Sequence: fade-in → hold → fade-out → cleanup
        _popupSequence = DOTween.Sequence();

        // Phase 1: Fade + Zoom IN
        if (_canvasGroup != null)
            _popupSequence.Join(_canvasGroup.DOFade(1f, animIn).SetEase(Ease.OutCubic));
        _popupSequence.Join(popupRoot.transform.DOScale(1f, animIn).SetEase(Ease.OutCubic));

        // Phase 2: Hold visible
        _popupSequence.AppendInterval(displayDuration);

        // Phase 3: Fade + Zoom OUT
        if (_canvasGroup != null)
            _popupSequence.Append(_canvasGroup.DOFade(0f, animOut).SetEase(Ease.InCubic));
        _popupSequence.Join(popupRoot.transform.DOScale(zoomScale, animOut).SetEase(Ease.InCubic));

        // Phase 4: Cleanup — shared path with RequestClose()
        _popupSequence.OnComplete(FinishClose);

        _popupSequence.SetUpdate(true); // unscaled time so pauses don't break it
    }

    /// <summary>
    /// Requests an animated close of the popup.
    /// Safe to call at any time — does nothing if the popup is not open or is already closing.
    /// Uses the same animOut fade+zoom-out as the auto-timer path.
    /// OnRadarPopupClosed fires once the animation completes.
    /// </summary>
    public void RequestClose()
    {
        if (!IsPopupOpen || _isClosing) return;

        _isClosing = true;

        // Kill the current sequence (stops fade-in or hold timer)
        KillSequence();

        // Build the close-only sequence: fade+zoom OUT → FinishClose
        _popupSequence = DOTween.Sequence();

        if (_canvasGroup != null)
            _popupSequence.Append(_canvasGroup.DOFade(0f, animOut).SetEase(Ease.InCubic));
        _popupSequence.Join(popupRoot.transform.DOScale(zoomScale, animOut).SetEase(Ease.InCubic));

        _popupSequence.OnComplete(FinishClose);
        _popupSequence.SetUpdate(true);
    }

    /// <summary>
    /// Immediately closes the popup without animation (kills animation, restores state).
    /// Fires OnRadarPopupClosed if the popup was open.
    /// Prefer RequestClose() for smooth animated dismissal.
    /// </summary>
    public void Close()
    {
        _isClosing = false;
        KillSequence();

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
            popupRoot.transform.localScale = Vector3.one;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
        }

        RestoreChestShown();

        bool wasOpen = IsPopupOpen;
        IsPopupOpen = false;

        // Fire event if we were actually open (avoid spurious calls)
        if (wasOpen)
            OnRadarPopupClosed?.Invoke();
    }

    // ==================== INTERNAL ====================

    /// <summary>
    /// Shared cleanup called at the end of any close animation (auto-timer or RequestClose).
    /// Fires OnRadarPopupClosed exactly once per popup open/close cycle.
    /// </summary>
    private void FinishClose()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
            popupRoot.transform.localScale = Vector3.one;
        }
        RestoreChestShown();
        IsPopupOpen = false;
        _isClosing = false;
        _popupSequence = null;

        OnRadarPopupClosed?.Invoke();
    }

    private void KillSequence()
    {
        if (_popupSequence != null && _popupSequence.IsActive())
        {
            _popupSequence.Kill();
            _popupSequence = null;
        }
    }

    private void RestoreChestShown()
    {
        if (chestShown == null || !IsPopupOpen)
            return; // nothing to restore — popup wasn't open or chestShown unassigned

        // Don't restore if police chase is active — ChestShownUI guard handles it
        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
        {
            chestShown.SetActive(false);
            return;
        }

        chestShown.SetActive(_chestShownPreviousState);
    }

    private void CaptureFrame(RadarSide side)
    {
        if (snapshotCamera == null)
        {
            Debug.LogWarning("[RadarPopup] snapshotCamera is null — cannot capture.");
            return;
        }

        if (snapshotCamera.targetTexture == null)
        {
            Debug.LogWarning("[RadarPopup] snapshotCamera.targetTexture is null — assign RT_RadarSnapshot.");
            return;
        }

        // Save original camera transform
        Vector3 origPos = snapshotCamera.transform.position;
        Quaternion origRot = snapshotCamera.transform.rotation;

        // Pick a random pose based on side
        Transform[] poses = (side == RadarSide.Left) ? leftCameraPoses : rightCameraPoses;
        if (poses != null && poses.Length > 0)
        {
            Transform pose = poses[UnityEngine.Random.Range(0, poses.Length)];
            if (pose != null)
            {
                snapshotCamera.transform.position = pose.position;
                snapshotCamera.transform.rotation = pose.rotation;
            }
        }
        // else: keep current camera transform as fallback

        // Apply micro-jitter for variety
        snapshotCamera.transform.position += new Vector3(
            UnityEngine.Random.Range(-jitterX, jitterX), 0f, 0f);
        snapshotCamera.transform.rotation *= Quaternion.Euler(
            0f, UnityEngine.Random.Range(-jitterYaw, jitterYaw), 0f);

        // Single-frame manual render (camera stays disabled in the render loop)
        snapshotCamera.Render();

        // Restore original camera transform
        snapshotCamera.transform.position = origPos;
        snapshotCamera.transform.rotation = origRot;

        // Assign the RT to the RawImage (in case it wasn't pre-wired)
        if (snapshotImage != null && snapshotImage.texture != snapshotCamera.targetTexture)
            snapshotImage.texture = snapshotCamera.targetTexture;
    }
}
