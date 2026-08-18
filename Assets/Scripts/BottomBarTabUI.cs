using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class BottomBarTabUI : MonoBehaviour
{
    [Header("Scale")]
    public float normalScale = 1f;
    public float selectedScale = 1.25f;
    public float animSpeed = 10f;

    [Header("Optional Color")]
    public Image backgroundImage;
    public Color normalColor = Color.white;
    public Color selectedColor = Color.white;

    private RectTransform rect;
    private bool isSelected = false;

    // Epsilon for "close enough" checks to skip unnecessary Lerp
    private const float ScaleEpsilon = 0.001f;
    private const float ColorEpsilon = 0.004f; // ~1/255

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        rect.localScale = Vector3.one * normalScale;

        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }

    private void Update()
    {
        if (rect == null) return;

        float target = isSelected ? selectedScale : normalScale;
        float currentScale = rect.localScale.x; // uniform scale assumed

        // Only Lerp if not already at target
        if (Mathf.Abs(currentScale - target) > ScaleEpsilon)
        {
            Vector3 targetScale = Vector3.one * target;
            rect.localScale = Vector3.Lerp(rect.localScale, targetScale, Time.unscaledDeltaTime * animSpeed);
        }
        else if (Mathf.Abs(currentScale - target) > 0f)
        {
            // Snap to exact target when very close
            rect.localScale = Vector3.one * target;
        }

        if (backgroundImage != null)
        {
            Color targetCol = isSelected ? selectedColor : normalColor;
            Color currentCol = backgroundImage.color;

            // Only Lerp if not already at target color
            float colorDiff = Mathf.Abs(currentCol.r - targetCol.r) +
                              Mathf.Abs(currentCol.g - targetCol.g) +
                              Mathf.Abs(currentCol.b - targetCol.b) +
                              Mathf.Abs(currentCol.a - targetCol.a);

            if (colorDiff > ColorEpsilon)
            {
                backgroundImage.color = Color.Lerp(currentCol, targetCol, Time.unscaledDeltaTime * animSpeed);
            }
            else if (colorDiff > 0f)
            {
                // Snap to exact target when very close
                backgroundImage.color = targetCol;
            }
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
    }
}
