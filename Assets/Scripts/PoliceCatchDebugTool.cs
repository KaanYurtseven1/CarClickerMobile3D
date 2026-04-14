using UnityEngine;

/// <summary>
/// Debug utility for manually triggering a police chase from the Inspector.
/// Attach to any GameObject in the scene (e.g. GameManager).
/// The custom editor (PoliceCatchDebugToolEditor) draws a button in the Inspector.
/// </summary>
public class PoliceCatchDebugTool : MonoBehaviour
{
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

        Debug.Log("[PoliceCatchDebug] Starting police chase...");
        PoliceCatchController.Instance.StartChase();
    }
}
