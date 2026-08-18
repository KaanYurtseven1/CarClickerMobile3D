using UnityEngine;
using System.Collections;

/// <summary>
/// Debug utility for manually triggering a police chase from the Inspector.
/// Attach to any GameObject in the scene (e.g. GameManager).
/// The custom editor (PoliceCatchDebugToolEditor) draws a button in the Inspector.
/// </summary>
public class PoliceCatchDebugTool : MonoBehaviour
{
    [Tooltip("Delay before chase starts (seconds). During this delay, Nitro Magnet shutdown runs so you can visually verify it.")]
    [SerializeField] private float debugDelay = 1.5f;

    public void TriggerChase()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PoliceCatchDebug] Must be in Play Mode.");
            return;
        }

        if (PoliceCatchController.Instance == null)
        {
            Debug.LogWarning("[PoliceCatchDebug] PoliceCatchController.Instance is null — no controller found in scene.");
            return;
        }

        if (PoliceCatchController.Instance.IsChaseActive)
        {
            Debug.LogWarning("[PoliceCatchDebug] A chase is already in progress.");
            return;
        }

        StartCoroutine(DelayedChase());
    }

    private IEnumerator DelayedChase()
    {
        // Suspend Nitro Magnet immediately so VFX close is visible during the delay
        if (NitroMagnetController.Instance != null)
            NitroMagnetController.Instance.SuspendForChase();

        Debug.Log($"[PoliceCatchDebug] Nitro Magnet suspended. Waiting {debugDelay}s before starting chase...");
        yield return new WaitForSeconds(debugDelay);

        Debug.Log("[PoliceCatchDebug] Starting police chase...");
        PoliceCatchController.Instance.StartChase();
    }
}
