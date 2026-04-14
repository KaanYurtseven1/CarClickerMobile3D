// ════════════════════════════════════════════════════════════════
// CarCustomizer.cs – Attached to each car root under CarPlatform/Car.
// Handles skin application and mod-part toggling for ONE car.
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

public class CarCustomizer : MonoBehaviour
{
    [Header("Body Renderers (receive the skin material)")]
    [Tooltip("Assign the MeshRenderer(s) of the base body mesh that should get the skin.")]
    [SerializeField] private List<Renderer> bodyRenderers = new List<Renderer>();

    // ─── Internal ───
    private readonly Dictionary<string, Transform> _partLookup = new Dictionary<string, Transform>();
    private bool _initialized;

    // ══════════════════ Initialization ══════════════════

    /// <summary>
    /// Called once by <see cref="GarageController"/> to build the part-key → Transform lookup.
    /// All listed parts are set <b>inactive</b> by default.
    /// </summary>
    public void Initialize(List<string> partKeys)
    {
        if (_initialized) return;
        _initialized = true;

        _partLookup.Clear();
        if (partKeys == null) return;

        foreach (string key in partKeys)
        {
            Transform child = transform.Find(key);
            if (child != null)
            {
                _partLookup[key] = child;
                child.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"[CarCustomizer:{gameObject.name}] Part '{key}' not found as direct child.");
            }
        }
    }

    // ══════════════════ Skin ══════════════════

    /// <summary>
    /// Applies the given material to every renderer in <see cref="bodyRenderers"/>
    /// plus the root object's own Renderer (if any), using <c>sharedMaterial</c> (no runtime copy).
    /// The root Renderer is applied unconditionally because prefab instances in
    /// different scenes may have incomplete bodyRenderers lists.
    /// </summary>
    public void ApplySkin(Material mat)
    {
        if (mat == null)
        {
            Debug.LogWarning($"[CarCustomizer:{gameObject.name}] ApplySkin called with null material.");
            return;
        }

        // Always include the root object's own renderer (may be missing from the serialized list)
        Renderer rootRenderer = GetComponent<Renderer>();
        if (rootRenderer != null)
            rootRenderer.sharedMaterial = mat;

        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            if (bodyRenderers[i] != null)
                bodyRenderers[i].sharedMaterial = mat;
        }
    }

    // ══════════════════ Parts ══════════════════

    /// <summary>Activates or deactivates a single mod part by key.</summary>
    public void SetPartActive(string partKey, bool active)
    {
        if (_partLookup.TryGetValue(partKey, out Transform t))
            t.gameObject.SetActive(active);
    }

    /// <summary>Returns whether the mod part is currently active.</summary>
    public bool IsPartActive(string partKey)
    {
        if (_partLookup.TryGetValue(partKey, out Transform t))
            return t.gameObject.activeSelf;
        return false;
    }

    /// <summary>Deactivates every mod part.</summary>
    public void DeactivateAllParts()
    {
        foreach (var kvp in _partLookup)
            kvp.Value.gameObject.SetActive(false);
    }

    /// <summary>
    /// Sets each part active only if its key exists in <paramref name="enabledParts"/>.
    /// </summary>
    public void RestoreParts(HashSet<string> enabledParts)
    {
        foreach (var kvp in _partLookup)
            kvp.Value.gameObject.SetActive(enabledParts.Contains(kvp.Key));
    }
}
