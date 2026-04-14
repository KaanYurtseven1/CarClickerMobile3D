using UnityEngine;

public class ChestMover : MonoBehaviour
{
    [Header("Movement")]
    public Transform bottomTarget;   // yolun alt ucu

    [Tooltip("Fallback speed if WorldScrollSpeed is not present. Ignored when WorldScrollSpeed exists.")]
    public float moveSpeed = 3f;

    /// <summary>Uses the local moveSpeed field (tuned per-object, not tied to road scroll).</summary>
    private float CurrentSpeed => moveSpeed;

    private Vector3 finalPos;

    // Yüksekliği sabitlemek için
    private float fixedY;

    private void Start()
    {
        // Spawnlandığı andaki yüksekliği kaydet
        fixedY = transform.position.y;

        if (bottomTarget != null)
        {
            // Hedefin Z'si ve X'i, ama Y sabit
            finalPos = bottomTarget.position;
            finalPos.x = transform.position.x;
            finalPos.y = fixedY;            // << ÖNEMLİ: hedefin Y'si de sabit
        }
    }

    private void Update()
    {
        if (bottomTarget == null) return;

        // Her frame'de kendi Y'ni kilitle
        Vector3 current = transform.position;
        current.y = fixedY;
        transform.position = current;

        Vector3 dir = (finalPos - transform.position).normalized;
        transform.position += dir * CurrentSpeed * Time.deltaTime;

        // hedefi geçince yok et
        if (Vector3.Dot(finalPos - transform.position, dir) <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
