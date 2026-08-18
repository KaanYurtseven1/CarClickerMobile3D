using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Safety guard placed on PoliceCatchUI (the full-screen overlay used by police chase).
///
/// PURPOSE: PoliceCatchController lives on DontDestroyOnLoad GameManager.
/// After a scene switch (e.g. ChestOpenScene → Main), the DDOL controller's
/// serialized uiRoot reference becomes null, so ForceHideUI() cannot disable
/// the newly created PoliceCatchUI. This guard ensures:
///
///   1) On Awake, the GameObject disables itself immediately.
///   2) A CanvasGroup is added/configured to block nothing when hidden.
///   3) Decorative child Images (like Dim_BG) have raycastTarget set to false
///      when the overlay is not shown, preventing invisible input blocking.
///
/// PoliceCatchController.StartChase() re-enables this object when needed, and
/// the CanvasGroup/Image states are managed by the controller during chase.
///
/// Place this MonoBehaviour on the PoliceCatchUI GameObject in the Main scene.
/// </summary>
[DefaultExecutionOrder(-200)] // Run before PoliceCatchController (-100 default)
public class PoliceCatchUIGuard : MonoBehaviour
{
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        Debug.Log($"[PoliceCatchUIGuard] Awake — disabling PoliceCatchUI (was active: {gameObject.activeSelf}).");

        // Add / get CanvasGroup and configure it to not block anything
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        // Disable raycastTarget on ALL child Image components (e.g. Dim_BG)
        // so even if this GO were somehow left active, nothing blocks input.
        SetChildImageRaycasts(false);

        // Disable self
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Called by PoliceCatchController when the chase UI should become visible.
    /// Restores CanvasGroup and Image raycast targets.
    /// </summary>
    public void OnShow()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        SetChildImageRaycasts(true);
        Debug.Log("[PoliceCatchUIGuard] OnShow — CanvasGroup blocking, Image raycasts ON.");
    }

    /// <summary>
    /// Called by PoliceCatchController when the chase UI should be hidden.
    /// Disables CanvasGroup and Image raycast targets.
    /// </summary>
    public void OnHide()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        SetChildImageRaycasts(false);
        Debug.Log("[PoliceCatchUIGuard] OnHide — CanvasGroup non-blocking, Image raycasts OFF.");
    }

    /// <summary>
    /// Sets raycastTarget on all child Image components.
    /// When hidden, Dim_BG and other decorative images should not intercept input.
    /// </summary>
    private void SetChildImageRaycasts(bool enabled)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            img.raycastTarget = enabled;
        }
    }
}
