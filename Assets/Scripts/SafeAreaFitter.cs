using UnityEngine;

/// <summary>
/// Ensures the attached RectTransform always matches the device's safe area (notch, cutouts, rounded corners).
/// Attach to a UI GameObject (e.g., "SafeArea") under a Canvas.
/// Works in both Play Mode and the Unity Editor.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public class SafeAreaFitter : MonoBehaviour
{
    [Tooltip("Enable to log when the safe area is reapplied.")]
    public bool logChanges = false;

    private RectTransform rectTransform;

    // Last applied values to detect changes
    private Rect lastSafeArea = Rect.zero;
    private int lastScreenWidth = 0;
    private int lastScreenHeight = 0;

    // Called when the script instance is being loaded
    private void Awake()
    {
        CacheRectTransform();
        ApplySafeArea(force: true);
    }

    // Called when the object becomes enabled and active
    private void OnEnable()
    {
        CacheRectTransform();
        ApplySafeArea(force: true);
    }

#if UNITY_EDITOR
    // Ensure changes are applied in the Editor when values change
    private void OnValidate()
    {
        CacheRectTransform();
        ApplySafeArea(force: true);
    }
#endif

    // Cache the RectTransform reference
    private void CacheRectTransform()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
    }

    // Update is called once per frame
    private void Update()
    {
        // Skip if screen size is invalid
        if (Screen.width == 0 || Screen.height == 0)
            return;

        Rect currentSafeArea = Screen.safeArea;
        int currentWidth = Screen.width;
        int currentHeight = Screen.height;

        // Only re-apply if safe area or resolution changed
        if (currentSafeArea != lastSafeArea ||
            currentWidth != lastScreenWidth ||
            currentHeight != lastScreenHeight)
        {
            ApplySafeArea(force: false);
        }
    }

    /// <summary>
    /// Applies the current safe area to the RectTransform.
    /// </summary>
    /// <param name="force">If true, always applies even if values haven't changed.</param>
    private void ApplySafeArea(bool force)
    {
        if (rectTransform == null)
            return;

        if (Screen.width == 0 || Screen.height == 0)
            return;

        Rect safeArea = Screen.safeArea;

        // Only apply if changed, unless forced
        if (!force &&
            safeArea == lastSafeArea &&
            Screen.width == lastScreenWidth &&
            Screen.height == lastScreenHeight)
        {
            return;
        }

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        if (logChanges)
        {
            Debug.LogFormat(
                "[SafeAreaFitter] Applied safe area: {0} (Screen: {1}x{2})\n" +
                "anchorMin: {3}, anchorMax: {4}",
                safeArea, Screen.width, Screen.height, anchorMin, anchorMax);
        }

        // Cache values for change detection
        lastSafeArea = safeArea;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }
}