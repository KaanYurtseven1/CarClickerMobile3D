using UnityEngine;

public class NitroCoinSpawner : MonoBehaviour
{
    public GameObject nitroCoinPrefab;

    [Header("Spawn Points")]
    public Transform spawnTop;      // coin'in göründüğü yer
    public Transform spawnBottom;   // yok olacağı yer

    [Header("X Spawn Range (world X)")]
    public float minX = -1.5f;
    public float maxX = 1.5f;

    [Header("Spawn Timing (seconds)")]
    public float minSpawnInterval = 20f;
    public float maxSpawnInterval = 45f;

    private float timer = 0f;
    private float nextSpawnTime = 0f;

    private void Start()
    {
        ScheduleNext();
    }

    private void Update()
    {
        if (nitroCoinPrefab == null || spawnTop == null || spawnBottom == null)
            return;

        timer += Time.deltaTime;
        if (timer >= nextSpawnTime)
        {
            SpawnNitroCoin();
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        timer = 0f;
        nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    private void SpawnNitroCoin()
    {
        // Guard: police chase active — do not spawn during minigame
        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
            return;

        // X'i random seç, Y ve Z'yi SpawnTop'tan al
        float x = Random.Range(minX, maxX);
        Vector3 pos = new Vector3(x, spawnTop.position.y, spawnTop.position.z);

        GameObject obj = Instantiate(nitroCoinPrefab, pos, Quaternion.identity);

        // Coin'e bottom Z bilgisini ver
        NitroCoin coin = obj.GetComponent<NitroCoin>();
        if (coin != null)
        {
            coin.despawnZ = spawnBottom.position.z;

            // Notify NitroMagnetController of newly spawned coin (primary detection path)
            if (NitroMagnetController.Instance != null)
            {
                NitroMagnetController.Instance.OnCoinSpawned(coin);
            }
        }
    }

    // Sahne içinde çizgileri görebilmek için
    private void OnDrawGizmosSelected()
    {
        if (spawnTop == null || spawnBottom == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            new Vector3(minX, spawnTop.position.y, spawnTop.position.z),
            new Vector3(maxX, spawnTop.position.y, spawnTop.position.z)
        );

        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            new Vector3(minX, spawnBottom.position.y, spawnBottom.position.z),
            new Vector3(maxX, spawnBottom.position.y, spawnBottom.position.z)
        );
    }
}
