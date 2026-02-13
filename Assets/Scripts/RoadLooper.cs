using UnityEngine;

public class RoadLooper : MonoBehaviour
{
    public Transform[] roadSegments; // Road_1, Road_2, Road_3
    public float speed = 5f;         // Yolun akış hızı
    public float segmentLength = 20f; // Her bir segmentin uzunluğu (z yönünde)

    private void Update()
    {
        if (roadSegments == null || roadSegments.Length == 0) return;

        // 1) Tüm segmentleri kameraya doğru hareket ettir (–z yönü)
        foreach (Transform seg in roadSegments)
        {
            seg.position += Vector3.back * speed * Time.deltaTime;
        }

        // 2) En geriye düşen segmenti tekrar en ileriye taşı
        //    (sonsuz loop için)
        foreach (Transform seg in roadSegments)
        {
            if (seg.position.z < -segmentLength)
            {
                // Şu anki segmentten daha ileride olanları bul
                float maxZ = seg.position.z;
                foreach (Transform other in roadSegments)
                {
                    if (other.position.z > maxZ)
                        maxZ = other.position.z;
                }

                // Bu segmenti en ileri segmentin önüne taşı
                seg.position = new Vector3(
                    seg.position.x,
                    seg.position.y,
                    maxZ + segmentLength
                );
            }
        }
    }
}
