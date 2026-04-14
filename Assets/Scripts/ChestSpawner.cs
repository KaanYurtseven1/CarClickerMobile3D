using UnityEngine;
using System.Collections;

public class ChestSpawner : MonoBehaviour
{
    [Header("Chest Prefabs (per type)")]
    [Tooltip("Common chest prefab. Also used as fallback for missing types.")]
    public GameObject commonChestPrefab;
    [Tooltip("Rare chest prefab. Falls back to common if null.")]
    public GameObject rareChestPrefab;
    [Tooltip("Legendary chest prefab. Falls back to common if null.")]
    public GameObject legendaryChestPrefab;

    [Header("Spawn Points")]
    public Transform spawnTop;
    public Transform spawnBottom;

    [Header("Spawn Timing (seconds)")]
    public float minSpawnInterval = 20f;
    public float maxSpawnInterval = 40f;

    [Header("X Spawn Range (world X)")]
    public float minX = -0.5f;
    public float maxX = 0.5f;

    [Header("Height Fix")]
    public bool useFixedY = true;
    public float fixedY = 0.35f;

    [Header("Rotation")]
    public bool usePrefabRotation = true;
    public bool useManualRotation = false;
    public Vector3 manualEulerRotation;

    private bool isSpawning = false;

    private void Start()
    {
        if (commonChestPrefab == null)
        {
            Debug.LogError("[ChestSpawner] commonChestPrefab NULL!");
            return;
        }
        if (spawnTop == null || spawnBottom == null)
        {
            Debug.LogError("[ChestSpawner] spawnTop/spawnBottom NULL!");
            return;
        }

        if (!isSpawning)
            StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        isSpawning = true;

        while (true)
        {
            float wait = Random.Range(minSpawnInterval, maxSpawnInterval);
            float elapsed = 0f;

            while (elapsed < wait)
            {
                yield return null;
                if (UIFlowState.IsSpawnSuppressed) continue;
                elapsed += Time.deltaTime;
            }

            // Don't spawn if inventory is full
            if (ChestInventoryManager.Instance != null && ChestInventoryManager.Instance.IsInventoryFull)
            {
                Debug.Log("[ChestSpawner] Inventory full - skipping spawn.");
                continue;
            }

            // Don't spawn during police chase
            if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
                continue;

            SpawnChest();
        }
    }

    private void SpawnChest()
    {
        if (commonChestPrefab == null || spawnTop == null || spawnBottom == null) return;
        ChestType type = PickChestType();
        SpawnChestOfType(type);
    }

    /// <summary>
    /// Spawns a chest of the given type using the full normal spawn flow.
    /// Called by SpawnLoop and exposed for debug tools.
    /// </summary>
    public void SpawnChestOfType(ChestType type)
    {
        if (commonChestPrefab == null || spawnTop == null || spawnBottom == null)
        {
            Debug.LogError("[ChestSpawner] Cannot spawn — prefab or spawn points are null!");
            return;
        }

        GameObject prefab = GetPrefabForType(type);

        Vector3 pos = spawnTop.position;
        pos.x = Random.Range(minX, maxX);
        if (useFixedY) pos.y = fixedY;

        Quaternion rot;
        if (usePrefabRotation) rot = prefab.transform.rotation;
        else if (useManualRotation) rot = Quaternion.Euler(manualEulerRotation);
        else rot = spawnTop.rotation;

        GameObject chestGo = Instantiate(prefab, pos, rot);

        // Set chest type on the component
        var chest = chestGo.GetComponent<Chest>();
        if (chest != null)
            chest.chestType = type;

        // Give move target
        var mover = chestGo.GetComponent<ChestMover>();
        if (mover != null)
            mover.bottomTarget = spawnBottom;

        Debug.Log($"[ChestSpawner] Spawned {type} chest at {pos}");
    }

    private ChestType PickChestType()
    {
        double playerMoney = CurrencyManager.Instance != null ? CurrencyManager.Instance.money : 0;
        ChestTypeConfig.GetSpawnWeights(playerMoney, out float wCommon, out float wRare, out float wLegendary);

        float total = wCommon + wRare + wLegendary;
        float roll = Random.Range(0f, total);

        if (roll < wCommon) return ChestType.Common;
        if (roll < wCommon + wRare) return ChestType.Rare;
        return ChestType.Legendary;
    }

    private GameObject GetPrefabForType(ChestType type)
    {
        switch (type)
        {
            case ChestType.Rare: return rareChestPrefab != null ? rareChestPrefab : commonChestPrefab;
            case ChestType.Legendary: return legendaryChestPrefab != null ? legendaryChestPrefab : commonChestPrefab;
            default: return commonChestPrefab;
        }
    }

    [ContextMenu("DEV_SpawnChestNow")]
    private void DevSpawnNow() { SpawnChest(); }

    [ContextMenu("DEV_SpawnCommon")]
    private void DevSpawnCommon() { SpawnChestOfType(ChestType.Common); }

    [ContextMenu("DEV_SpawnRare")]
    private void DevSpawnRare() { SpawnChestOfType(ChestType.Rare); }

    [ContextMenu("DEV_SpawnLegendary")]
    private void DevSpawnLegendary() { SpawnChestOfType(ChestType.Legendary); }

    private void OnDrawGizmos()
    {
        if (spawnTop == null || spawnBottom == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(spawnTop.position, 0.2f);
        Gizmos.DrawSphere(spawnBottom.position, 0.2f);
        Gizmos.DrawLine(spawnTop.position, spawnBottom.position);
    }
}