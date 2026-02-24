using UnityEngine;
using UnityEngine.UI;
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

    /// <summary>True during the entire visible window (including fade in/out).</summary>
    public bool IsPopupOpen { get; private set; }

    private CanvasGroup _canvasGroup;
    private Sequence _popupSequence;
    private bool _chestShownPreviousState;

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

    private void OnDestroy()
    {
        KillSequence();
        RestoreChestShown();
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
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

        // Kill any previous popup animation to avoid overlap
        KillSequence();

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

        // Phase 4: Cleanup
        _popupSequence.OnComplete(() =>
        {
            popupRoot.SetActive(false);
            popupRoot.transform.localScale = Vector3.one;
            RestoreChestShown();
            IsPopupOpen = false;
            _popupSequence = null;

            // Notify subscribers that the radar popup has fully closed
            OnRadarPopupClosed?.Invoke();
        });

        _popupSequence.SetUpdate(true); // unscaled time so pauses don't break it
    }

    /// <summary>
    /// Immediately closes the popup (kills animation, restores state).
    /// </summary>
    public void Close()
    {
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
