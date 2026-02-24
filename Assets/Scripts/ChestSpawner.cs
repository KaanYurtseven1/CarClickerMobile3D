using UnityEngine;
using System.Collections;

public class ChestSpawner : MonoBehaviour
{
    public GameObject chestPrefab;

    [Header("Spawn Points")]
    public Transform spawnTop;       // yolun üst tarafı
    public Transform spawnBottom;    // yolun alt tarafı

    [Header("Spawn Timing (seconds)")]
    public float minSpawnInterval = 20f;
    public float maxSpawnInterval = 40f;

    [Header("X Spawn Range (world X)")]
    public float minX = -0.5f;       // yolun sol iç tarafı
    public float maxX = 0.5f;        // yolun sağ iç tarafı

    [Header("Height Fix")]
    public bool useFixedY = true;
    public float fixedY = 0.35f;

    [Header("Rotation")]
    [Tooltip("true = prefab'ın kendi rotasyonunu kullan, false = spawnTop rotasyonunu kullan")]
    public bool usePrefabRotation = true;

    [Tooltip("Manuel rotasyon override (sadece usePrefabRotation=false ve useManualRotation=true ise)")]
    public bool useManualRotation = false;
    public Vector3 manualEulerRotation = new Vector3(0, 0, 0);

    private bool isSpawning = false;

    private void Start()
    {
        if (chestPrefab == null)
        {
            Debug.LogError("[ChestSpawner] chestPrefab NULL");
            return;
        }
        if (spawnTop == null || spawnBottom == null)
        {
            Debug.LogError("[ChestSpawner] spawnTop/spawnBottom NULL");
            return;
        }

        if (!isSpawning)
        {
            StartCoroutine(SpawnLoop());
        }
    }

    private IEnumerator SpawnLoop()
    {
        isSpawning = true;

        while (true)
        {
            float wait = Random.Range(minSpawnInterval, maxSpawnInterval);

            // Manual timer loop: freeze while UI content panel is open.
            // WaitForSeconds cannot be paused, so we tick manually.
            float elapsed = 0f;
            while (elapsed < wait)
            {
                yield return null;
                // UI content-panel suppression: skip deltaTime accumulation.
                if (UIFlowState.IsSpawnSuppressed)
                    continue;
                elapsed += UnityEngine.Time.deltaTime;
            }

            SpawnChest();
        }
    }

    private void SpawnChest()
    {
        if (chestPrefab == null || spawnTop == null || spawnBottom == null)
            return;

        // Guard: police chase active — do not spawn during minigame
        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
            return;

        // Yukarıdaki çizginin bir noktasından başla
        Vector3 pos = spawnTop.position;

        // Yolun genişliği içinde rastgele X
        float randomX = Random.Range(minX, maxX);
        pos.x = randomX;

        // Y yüksekliğini sabitle
        if (useFixedY)
        {
            pos.y = fixedY;
        }

        // Rotasyon belirleme: prefab > spawnTop > manuel override
        Quaternion rot;
        if (usePrefabRotation)
        {
            rot = chestPrefab.transform.rotation;
        }
        else if (useManualRotation)
        {
            rot = Quaternion.Euler(manualEulerRotation);
        }
        else
        {
            rot = spawnTop.rotation;
        }

        GameObject chestGo = Instantiate(chestPrefab, pos, rot);

        // DEBUG: Rotasyonun değişmediğini doğrula (1 saniye sonra kontrol)
        Debug.Log($"[ChestSpawner] Chest spawned at {pos}, rotation: {rot.eulerAngles}");
        StartCoroutine(VerifyRotationUnchanged(chestGo, rot.eulerAngles));

        // Hareket script'ine hedefi ver
        ChestMover mover = chestGo.GetComponent<ChestMover>();
        if (mover != null)
        {
            mover.bottomTarget = spawnBottom;
        }

        Debug.Log("[ChestSpawner] Chest spawned at " + pos);
    }

    /// <summary>
    /// DEBUG: 1 saniye sonra rotasyonun değişip değişmediğini kontrol et.
    /// Prodüksiyonda bu coroutine'i kaldırabilirsin.
    /// </summary>
    private IEnumerator VerifyRotationUnchanged(GameObject chest, Vector3 expectedEuler)
    {
        yield return new WaitForSeconds(1f);

        if (chest == null) yield break; // Destroyed already

        Vector3 currentEuler = chest.transform.eulerAngles;
        float yDiff = Mathf.Abs(currentEuler.y - expectedEuler.y);

        if (yDiff > 0.1f)
        {
            Debug.LogError($"[ChestSpawner] ROTATION CHANGED! Expected Y={expectedEuler.y}, Got Y={currentEuler.y}");
        }
        else
        {
            Debug.Log($"[ChestSpawner] Rotation verified OK: {currentEuler}");
        }
    }

    [ContextMenu("DEV_SpawnChestNow")]
    private void DevSpawnNow()
    {
        SpawnChest();
    }

    private void OnDrawGizmos()
    {
        if (spawnTop == null || spawnBottom == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(spawnTop.position, 0.2f);
        Gizmos.DrawSphere(spawnBottom.position, 0.2f);
        Gizmos.DrawLine(spawnTop.position, spawnBottom.position);
    }
}
