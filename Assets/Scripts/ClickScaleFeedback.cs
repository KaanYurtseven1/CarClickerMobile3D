using UnityEngine;
using System.Collections;

public class ClickScaleFeedback : MonoBehaviour
{
    public float scaleMultiplier = 1.1f;
    public float animationDuration = 0.1f;

    private Vector3 originalScale;
    private bool isAnimating = false;

    private void Start()
    {
        originalScale = transform.localScale;
    }

    public void PlayFeedback()
    {
        if (!isAnimating)
        {
            StartCoroutine(ScaleRoutine());
        }
    }

    private IEnumerator ScaleRoutine()
    {
        isAnimating = true;

        Vector3 targetScale = originalScale * scaleMultiplier;
        float t = 0f;

        // Grow
        while (t < animationDuration)
        {
            t += Time.deltaTime;
            float lerp = t / animationDuration;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, lerp);
            yield return null;
        }

        // Shrink back
        t = 0f;
        while (t < animationDuration)
        {
            t += Time.deltaTime;
            float lerp = t / animationDuration;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, lerp);
            yield return null;
        }

        transform.localScale = originalScale;
        isAnimating = false;
    }
}
