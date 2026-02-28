// ════════════════════════════════════════════════════════════════
// GarageFocusController.cs – Double-click focus mode & drag rotation
//
// Attach to any GameObject in the Garage scene (e.g. an empty
// "FocusController" object, or directly on CarPlatform).
//
// Inspector wiring:
//   mainCamera        → Main Camera
//   canvasGroup       → The root Canvas's CanvasGroup component
//                        (add CanvasGroup to the Canvas if not present)
//   carPlatform       → CarPlatform Transform
//   platformCollider  → The CapsuleCollider on CarPlatform
//                        (auto-filled from carPlatform if left null)
//
// Behaviour:
//   • Double-click/tap on CarPlatform toggles "focus mode".
//   • Enter focus: canvas fades out, camera moves closer & tilts.
//   • Exit focus:  camera returns, platform rotation resets, then
//                  canvas fades back in.
//   • While in focus mode, horizontal drag on the platform rotates
//     it around Y.  On exit the rotation returns to default.
//   • No gameplay logic is changed anywhere.
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using DG.Tweening;

public class GarageFocusController : MonoBehaviour
{
    // ─── Inspector References ───
    [Header("─── References ───")]
    [Tooltip("Main Camera in the scene.")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("CanvasGroup on the root UI Canvas (for alpha fade).")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("The CarPlatform Transform (parent of car roots).")]
    [SerializeField] private Transform carPlatform;

    [Tooltip("Collider used for click/tap detection. Auto-filled from carPlatform if null.")]
    [SerializeField] private Collider platformCollider;

    // ─── Double-Click Settings ───
    [Header("─── Double Click ───")]
    [Tooltip("Max time (seconds) between two clicks to register as double-click.")]
    [SerializeField] private float doubleClickThreshold = 0.3f;

    // ─── Focus-Mode Camera Targets ───
    [Header("─── Focus Camera ───")]
    [SerializeField] private float focusCameraRotX = 7f;
    [SerializeField] private float focusCameraZ = -9.3f;

    // ─── Tween Durations & Easing ───
    [Header("─── Tweens ───")]
    [SerializeField] private float enterDuration = 0.4f;
    [SerializeField] private float exitDuration = 0.4f;
    [SerializeField] private float canvasFadeDuration = 0.25f;
    [SerializeField] private Ease tweenEase = Ease.InOutCubic;
    [SerializeField] private Ease fadeEase = Ease.Linear;

    // ─── Drag Rotation ───
    [Header("─── Drag Rotation ───")]
    [Tooltip("Degrees per pixel of horizontal drag.")]
    [SerializeField] private float dragSensitivity = 0.3f;

    // ─── Runtime State ───
    private bool _isFocusMode;
    private bool _isAnimating;

    // Defaults captured at Start
    private Vector3 _defaultCameraPos;
    private Quaternion _defaultCameraRot;
    private float _defaultPlatformY;

    // Double-click tracking
    private float _lastClickTime = -1f;

    // Drag tracking
    private bool _isDragging;
    private float _dragStartScreenX;
    private float _dragStartRotY;

    // Tween sequence (one at a time)
    private Sequence _activeSequence;

    // ══════════════════ Lifecycle ══════════════════

    private void Start()
    {
        // Auto-resolve collider if not assigned
        if (platformCollider == null && carPlatform != null)
            platformCollider = carPlatform.GetComponent<Collider>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Capture defaults
        if (mainCamera != null)
        {
            _defaultCameraPos = mainCamera.transform.localPosition;
            _defaultCameraRot = mainCamera.transform.localRotation;
        }

        if (carPlatform != null)
            _defaultPlatformY = carPlatform.localEulerAngles.y;

        ValidateReferences();
    }

    private void OnDestroy()
    {
        KillActiveSequence();

        if (mainCamera != null)
            DOTween.Kill(mainCamera.transform);
        if (canvasGroup != null)
            DOTween.Kill(canvasGroup);
        if (carPlatform != null)
            DOTween.Kill(carPlatform);
    }

    private void Update()
    {
        HandleInput();
    }

    // ══════════════════ Validation ══════════════════

    private void ValidateReferences()
    {
        if (mainCamera == null)
            Debug.LogError("[GarageFocusController] mainCamera is not assigned.");
        if (canvasGroup == null)
            Debug.LogError("[GarageFocusController] canvasGroup is not assigned.");
        if (carPlatform == null)
            Debug.LogError("[GarageFocusController] carPlatform is not assigned.");
        if (platformCollider == null)
            Debug.LogError("[GarageFocusController] platformCollider could not be resolved.");
    }

    // ══════════════════ Input Handling ══════════════════

    private void HandleInput()
    {
        // ── Pointer down ──
        if (GetPointerDown(out Vector2 screenPos))
        {
            if (RaycastHitsPlatform(screenPos))
            {
                // Double-click detection
                float now = Time.unscaledTime;
                if (now - _lastClickTime <= doubleClickThreshold)
                {
                    _lastClickTime = -1f; // reset so triple-click doesn't fire again
                    OnDoubleClick();
                    return;
                }
                _lastClickTime = now;

                // Begin drag (only in focus mode)
                if (_isFocusMode && !_isAnimating)
                {
                    _isDragging = true;
                    _dragStartScreenX = screenPos.x;
                    _dragStartRotY = carPlatform.localEulerAngles.y;
                }
            }
        }

        // ── Drag ──
        if (_isDragging && GetPointerHeld(out Vector2 currentPos))
        {
            float deltaX = currentPos.x - _dragStartScreenX;
            float newY = _dragStartRotY + deltaX * dragSensitivity;

            Vector3 euler = carPlatform.localEulerAngles;
            euler.y = newY;
            carPlatform.localEulerAngles = euler;
        }

        // ── Pointer up ──
        if (GetPointerUp())
        {
            _isDragging = false;
        }
    }

    // ── Pointer abstraction (mouse + touch) ──

    private bool GetPointerDown(out Vector2 screenPos)
    {
        // Touch takes priority
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPos = Input.GetTouch(0).position;
            return true;
        }
        if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            return true;
        }
        screenPos = Vector2.zero;
        return false;
    }

    private bool GetPointerHeld(out Vector2 screenPos)
    {
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                screenPos = t.position;
                return true;
            }
        }
        if (Input.GetMouseButton(0))
        {
            screenPos = Input.mousePosition;
            return true;
        }
        screenPos = Vector2.zero;
        return false;
    }

    private bool GetPointerUp()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended)
            return true;
        return Input.GetMouseButtonUp(0);
    }

    // ── Raycast ──

    private bool RaycastHitsPlatform(Vector2 screenPos)
    {
        if (mainCamera == null || platformCollider == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            return hit.collider == platformCollider;
        }
        return false;
    }

    // ══════════════════ Focus Mode Toggle ══════════════════

    private void OnDoubleClick()
    {
        // Block if an animation is already running
        if (_isAnimating) return;

        _isDragging = false; // cancel any drag in progress

        if (_isFocusMode)
            ExitFocusMode();
        else
            EnterFocusMode();
    }

    // ── Enter ──

    private void EnterFocusMode()
    {
        _isAnimating = true;
        _isFocusMode = true;

        KillActiveSequence();
        KillTargetTweens();

        Transform camT = mainCamera.transform;

        // Compute focus targets, preserving non-animated axes
        Vector3 focusPos = _defaultCameraPos;
        focusPos.z = focusCameraZ;

        Vector3 focusRot = _defaultCameraRot.eulerAngles;
        focusRot.x = focusCameraRotX;

        // Build sequence: canvas fade-out + camera move/rotate in parallel
        Sequence seq = DOTween.Sequence().SetUpdate(true);

        seq.Join(
            canvasGroup.DOFade(0f, canvasFadeDuration)
                .SetEase(fadeEase)
                .SetUpdate(true)
        );
        seq.Join(
            camT.DOLocalMove(focusPos, enterDuration)
                .SetEase(tweenEase)
                .SetUpdate(true)
        );
        seq.Join(
            camT.DOLocalRotate(focusRot, enterDuration, RotateMode.Fast)
                .SetEase(tweenEase)
                .SetUpdate(true)
        );

        // Block interaction on the canvas while invisible
        seq.OnComplete(() =>
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            _isAnimating = false;
        });

        _activeSequence = seq;
    }

    // ── Exit ──

    private void ExitFocusMode()
    {
        _isAnimating = true;
        _isFocusMode = false;
        _isDragging = false;

        KillActiveSequence();
        KillTargetTweens();

        Transform camT = mainCamera.transform;

        // Build exit sequence:
        //   Phase 1 (parallel): camera returns + platform rotation returns
        //   Phase 2 (appended): canvas fades back in
        Sequence seq = DOTween.Sequence().SetUpdate(true);

        seq.Join(
            camT.DOLocalMove(_defaultCameraPos, exitDuration)
                .SetEase(tweenEase)
                .SetUpdate(true)
        );
        seq.Join(
            camT.DOLocalRotate(_defaultCameraRot.eulerAngles, exitDuration, RotateMode.Fast)
                .SetEase(tweenEase)
                .SetUpdate(true)
        );

        // Platform rotation Y back to default
        Vector3 platEuler = carPlatform.localEulerAngles;
        platEuler.y = _defaultPlatformY;
        seq.Join(
            carPlatform.DOLocalRotate(platEuler, exitDuration, RotateMode.Fast)
                .SetEase(tweenEase)
                .SetUpdate(true)
        );

        // After camera/platform return, fade canvas back in
        seq.AppendCallback(() =>
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        });
        seq.Append(
            canvasGroup.DOFade(1f, canvasFadeDuration)
                .SetEase(fadeEase)
                .SetUpdate(true)
        );

        seq.OnComplete(() =>
        {
            // Guarantee exact restoration
            camT.localPosition = _defaultCameraPos;
            camT.localRotation = _defaultCameraRot;

            Vector3 euler = carPlatform.localEulerAngles;
            euler.y = _defaultPlatformY;
            carPlatform.localEulerAngles = euler;

            canvasGroup.alpha = 1f;
            _isAnimating = false;
        });

        _activeSequence = seq;
    }

    // ══════════════════ Tween Cleanup ══════════════════

    private void KillActiveSequence()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
            _activeSequence = null;
        }
    }

    private void KillTargetTweens()
    {
        if (mainCamera != null) DOTween.Kill(mainCamera.transform);
        if (canvasGroup != null) DOTween.Kill(canvasGroup);
        if (carPlatform != null) DOTween.Kill(carPlatform);
    }
}
