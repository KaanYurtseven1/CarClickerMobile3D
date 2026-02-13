using UnityEngine;
using TMPro;

public class FloatingTextLife : MonoBehaviour
{
    public float lifeTime = 0.7f;
    public Vector3 moveOffset = new Vector3(0f, 80f, 0f);

    private float timer = 0f;
    private RectTransform rect;
    private Vector3 startPos;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        timer = 0f;
        if (rect == null)
            rect = GetComponent<RectTransform>();

        if (rect != null)
            startPos = rect.position;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (rect != null)
        {
            float t = timer / lifeTime;
            t = Mathf.Clamp01(t);
            rect.position = Vector3.Lerp(startPos, startPos + moveOffset, t);
        }

        if (timer >= lifeTime)
        {
            // Let OnDestroy handle the notification to avoid double-decrement
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // Single point of notification when destroyed (whether by timer or external call)
        if (ClickTextSpawner.Instance != null)
        {
            ClickTextSpawner.Instance.NotifyTextDestroyed();
        }
    }
}
