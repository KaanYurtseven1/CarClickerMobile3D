using UnityEngine;

/// <summary>
/// Attach to Plasma Sphere (shield VFX) GameObject.
/// Automatically disables ALL Colliders on this object and its children
/// whenever the object is activated, preventing it from blocking raycasts.
///
/// This is the primary fix for the shield-blocks-car-taps issue.
/// Even if someone accidentally adds a collider to the VFX later, it will be stripped at runtime.
/// </summary>
public class ShieldColliderDisabler : MonoBehaviour
{
    private bool _loggedOnce = false;

    private void OnEnable()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        int disabled = 0;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].enabled)
            {
                colliders[i].enabled = false;
                disabled++;
            }
        }

        if (disabled > 0 && !_loggedOnce)
        {
            Debug.LogWarning(
                $"[ShieldColliderDisabler] Disabled {disabled} collider(s) on '{gameObject.name}' " +
                "to prevent raycast blocking. Remove colliders from VFX to avoid this at edit time.");
            _loggedOnce = true;
        }
    }
}
