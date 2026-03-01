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
    public float minSpawnInterval = 60f;
    public float maxSpawnInterval = 90f;

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

        // UI content-panel suppression: freeze timer while a panel is open.
        // When the panel closes the timer continues from where it left off — no burst-spawn.
        if (UIFlowState.IsSpawnSuppressed)
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

        // Enforce Y=180 rotation so the coin faces the camera.
        // Previous code used Quaternion.identity which zeroed all axes, overriding the prefab.
        Quaternion spawnRot = Quaternion.Euler(0f, 180f, 0f);
        GameObject obj = Instantiate(nitroCoinPrefab, pos, spawnRot);

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
