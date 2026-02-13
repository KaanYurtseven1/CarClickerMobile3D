using UnityEngine;

public class CarIdleMotion : MonoBehaviour
{
    public float amplitude = 0.2f;   // İleri-geri mesafe (0.2 -> çok hafif)
    public float frequency = 1.5f;   // Saniyedeki salınım hızı

    private Vector3 startLocalPos;

    private void Start()
    {
        startLocalPos = transform.localPosition;
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * frequency) * amplitude;

        // Z ekseninde ileri-geri
        Vector3 newPos = startLocalPos + new Vector3(0f, 0f, offset);
        transform.localPosition = newPos;
    }
}
