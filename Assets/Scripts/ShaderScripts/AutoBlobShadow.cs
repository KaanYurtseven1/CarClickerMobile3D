/*
 * AutoBlobShadow.cs
 * =================
 * A simple blob shadow controller for Unity 6 (URP) with FIXED shadow size.
 * 
 * USAGE INSTRUCTIONS:
 * -------------------
 * 1. Create a child hierarchy under your car prefab: shadow_Blob/ShadowQuad
 * 2. The ShadowQuad should be a Quad mesh rotated X=90 so it lies flat on the ground.
 * 3. Assign a transparent unlit ShaderGraph material (M_BlobShadow) to the ShadowQuad.
 * 4. Add this component to the car root GameObject.
 * 5. Assign the ShadowQuad transform to the "Shadow Quad" field.
 * 6. Optionally assign "Model Root" to track a specific transform's position.
 * 
 * FEATURES:
 * - Fixed shadow size (no dynamic scaling).
 * - Positions shadow under the car based on modelRoot position.
 * - Optional material parameter overrides.
 * - Works in Editor (ExecuteAlways) and at runtime.
 */

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class AutoBlobShadow : MonoBehaviour
{
    #region Serialized Fields

    [Header("Required References")]
    [Tooltip("The shadow quad transform. Must be assigned.")]
    [SerializeField] private Transform shadowQuad;

    [Tooltip("Optional root transform to follow. If null, uses this transform.")]
    [SerializeField] private Transform modelRoot;

    [Header("Positioning")]
    [Tooltip("Local Y position of the ground plane relative to shadowQuad's parent.")]
    [SerializeField] private float groundLocalY = 0f;

    [Tooltip("Small offset above ground to prevent z-fighting.")]
    [SerializeField] private float yOffset = 0.01f;

    [Header("Shadow Material Override (Optional)")]
    [Tooltip("Enable to override shadow material parameters at runtime.")]
    [SerializeField] private bool overrideMaterialParams = false;

    [Tooltip("Shadow radius parameter.")]
    [SerializeField] private float radius = 1f;

    [Tooltip("Shadow edge softness.")]
    [SerializeField] private float softness = 0.5f;

    [Tooltip("Shadow opacity/strength.")]
    [Range(0f, 1f)]
    [SerializeField] private float strength = 0.5f;

    [Tooltip("Shadow color tint.")]
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.5f);

    [Header("Debug")]
    [Tooltip("Show debug gizmos in Scene view.")]
    [SerializeField] private bool showDebugGizmos = false;

    #endregion

    #region Private Fields

    // Fixed shadow scale (never changes)
    private static readonly Vector3 FixedShadowScale = new Vector3(3.16f, 5.8f, 1f);

    // Cached shadow quad renderer for material override
    private Renderer _shadowQuadRenderer;

    // Material property block for efficient material parameter updates
    private MaterialPropertyBlock _materialPropertyBlock;

    // Cached previous position to detect changes
    private Vector3 _lastRootPosition;

    // Shader property IDs (cached for performance)
    private static readonly int PropRadius = Shader.PropertyToID("_Radius");
    private static readonly int PropSoftness = Shader.PropertyToID("_Softness");
    private static readonly int PropStrength = Shader.PropertyToID("_Strength");
    private static readonly int PropColor = Shader.PropertyToID("_ShadowColor");
    private static readonly int PropColorAlt = Shader.PropertyToID("_Color");

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        Initialize();
        ApplyNow();
    }

    private void OnValidate()
    {
        // Delay to avoid issues during serialization
#if UNITY_EDITOR
        EditorApplication.delayCall += () =>
        {
            if (this != null && enabled)
            {
                ApplyNow();
            }
        };
#endif
    }

    private void Update()
    {
        // Check if position has changed (for runtime movement)
        if (HasPositionChanged())
        {
            ApplyNow();
            CacheCurrentPosition();
        }
    }

    private void LateUpdate()
    {
        // Apply material parameters every frame if override is enabled
        if (overrideMaterialParams && _shadowQuadRenderer != null)
        {
            ApplyMaterialParameters();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || shadowQuad == null) return;

        // Draw shadow quad position
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(shadowQuad.position, 0.1f);

        // Draw reference position
        Transform root = modelRoot != null ? modelRoot : transform;
        if (root != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(root.position, 0.05f);
        }
    }
#endif

    #endregion

    #region Public Methods

    /// <summary>
    /// Forces an immediate update of the shadow quad position and scale.
    /// </summary>
    public void ApplyNow()
    {
        if (shadowQuad == null)
        {
            return;
        }

        // Get the reference transform
        Transform root = modelRoot != null ? modelRoot : transform;
        if (root == null)
        {
            return;
        }

        // Get shadow quad's parent for local space calculations
        Transform shadowParent = shadowQuad.parent;

        if (shadowParent == null)
        {
            // Shadow quad has no parent, use world space
            ApplyInWorldSpace(root.position);
        }
        else
        {
            // Convert position to shadow parent local space
            ApplyInLocalSpace(root.position, shadowParent);
        }

        // Always set fixed scale
        shadowQuad.localScale = FixedShadowScale;

        // Apply material parameters if enabled
        if (overrideMaterialParams)
        {
            ApplyMaterialParameters();
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Initializes cached references.
    /// </summary>
    private void Initialize()
    {
        if (shadowQuad == null)
        {
            Debug.LogWarning($"[AutoBlobShadow] {gameObject.name}: Shadow Quad is not assigned!", this);
            return;
        }

        // Cache shadow quad renderer for material override
        _shadowQuadRenderer = shadowQuad.GetComponent<Renderer>();

        // Initialize material property block
        if (_materialPropertyBlock == null)
        {
            _materialPropertyBlock = new MaterialPropertyBlock();
        }

        CacheCurrentPosition();
    }

    /// <summary>
    /// Applies shadow position when shadow quad has a parent.
    /// </summary>
    private void ApplyInLocalSpace(Vector3 rootWorldPos, Transform shadowParent)
    {
        // Convert root world position to shadow parent local space
        Vector3 rootLocalPos = shadowParent.InverseTransformPoint(rootWorldPos);

        // Set position: follow root XZ, use ground Y
        Vector3 newLocalPos;
        newLocalPos.x = rootLocalPos.x;
        newLocalPos.y = groundLocalY + yOffset;
        newLocalPos.z = rootLocalPos.z;

        shadowQuad.localPosition = newLocalPos;
    }

    /// <summary>
    /// Applies shadow position when shadow quad has no parent (world space).
    /// </summary>
    private void ApplyInWorldSpace(Vector3 rootWorldPos)
    {
        // Set position: follow root XZ, use ground Y
        Vector3 newPos;
        newPos.x = rootWorldPos.x;
        newPos.y = groundLocalY + yOffset;
        newPos.z = rootWorldPos.z;

        shadowQuad.position = newPos;
    }

    /// <summary>
    /// Applies material parameter overrides using MaterialPropertyBlock.
    /// </summary>
    private void ApplyMaterialParameters()
    {
        if (_shadowQuadRenderer == null || _materialPropertyBlock == null)
        {
            return;
        }

        _shadowQuadRenderer.GetPropertyBlock(_materialPropertyBlock);

        _materialPropertyBlock.SetFloat(PropRadius, radius);
        _materialPropertyBlock.SetFloat(PropSoftness, softness);
        _materialPropertyBlock.SetFloat(PropStrength, strength);
        _materialPropertyBlock.SetColor(PropColor, shadowColor);
        _materialPropertyBlock.SetColor(PropColorAlt, shadowColor);

        _shadowQuadRenderer.SetPropertyBlock(_materialPropertyBlock);
    }

    /// <summary>
    /// Checks if the root position has changed.
    /// </summary>
    private bool HasPositionChanged()
    {
        Transform root = modelRoot != null ? modelRoot : transform;
        if (root == null) return false;

        return root.position != _lastRootPosition;
    }

    /// <summary>
    /// Caches the current position.
    /// </summary>
    private void CacheCurrentPosition()
    {
        Transform root = modelRoot != null ? modelRoot : transform;
        _lastRootPosition = root != null ? root.position : Vector3.zero;
    }

    #endregion

    #region Context Menu

#if UNITY_EDITOR
    [ContextMenu("Apply Now")]
    private void ContextApplyNow()
    {
        ApplyNow();
        Debug.Log($"[AutoBlobShadow] {gameObject.name}: Shadow updated. Fixed scale: {FixedShadowScale}");
    }

    [ContextMenu("Reset to Fixed Scale")]
    private void ContextResetScale()
    {
        if (shadowQuad != null)
        {
            shadowQuad.localScale = FixedShadowScale;
            Debug.Log($"[AutoBlobShadow] {gameObject.name}: Scale reset to {FixedShadowScale}");
        }
    }
#endif

    #endregion
}
