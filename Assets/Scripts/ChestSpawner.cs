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

    [Header("Tutorial")]
    [Tooltip("World Z threshold the tutorial Common Chest must cross (toward the camera) to trigger gameplay freeze. Mirrors NitroCoinSpawner.tutorialFreezeZ.")]
    public float chestTutorialFreezeZ = 0f;

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
                if (!TutorialGate.ChestUnlocked || TutorialGate.GameplayFrozen) continue;
                if (TutorialGate.ChestPipelineSuspended) continue;
                elapsed += Time.deltaTime;
            }

            // Tutorial gating: do not spawn until 3 Nitro Coins are collected.
            if (!TutorialGate.ChestUnlocked || TutorialGate.GameplayFrozen)
                continue;

            // Tutorial pipeline suspension (post-first-free-chest Cards segment): block
            // any new spawns until the player presses Clicker after Step 11/Ten.
            if (TutorialGate.ChestPipelineSuspended)
                continue;

            // Don't spawn if inventory is full
            if (ChestInventoryManager.Instance != null && ChestInventoryManager.Instance.IsInventoryFull)
            {
                Debug.Log("[ChestSpawner] Inventory full - skipping spawn.");
                continue;
            }

            // Pre-first-free-chest-open guard: while the very first tutorial chest
            // is still alive in the world OR sitting unopened in inventory, block
            // any second chest from spawning. Seven points at that one chest slot
            // and we must not pollute the inventory before the player opens it.
            if (TutorialGate.TutorialFreeChestOpenedCount == 0
                && (TutorialGate.TutorialChestActive
                    || (ChestInventoryManager.Instance != null && ChestInventoryManager.Instance.ChestCount > 0)))
            {
                continue;
            }

            // Tutorial/free Common Chest pipeline gate:
            // While the player has not yet opened all 3 free Common Chests,
            // we never want more than (Quota - openedCount) tutorial-free chests
            // simultaneously in inventory. If the pipeline is already saturated,
            // pause spawning until one of those free chests is opened.
            if (TutorialGate.TutorialFreeChestOpenedCount < TutorialGate.TutorialFreeChestQuota
                && ChestInventoryManager.Instance != null)
            {
                int unopenedFree = ChestInventoryManager.Instance.CountTutorialFreeUnopenedChests();
                if (TutorialGate.TutorialFreeChestOpenedCount + unopenedFree >= TutorialGate.TutorialFreeChestQuota)
                {
                    // Pipeline full — wait for the player to open the free chests already in inventory.
                    continue;
                }
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
    /// Called by SpawnLoop and exposed for debug tools. Returns the spawned
    /// <see cref="Chest"/> component, or null on failure.
    /// </summary>
    public Chest SpawnChestOfType(ChestType type)
    {
        if (commonChestPrefab == null || spawnTop == null || spawnBottom == null)
        {
            Debug.LogError("[ChestSpawner] Cannot spawn — prefab or spawn points are null!");
            return null;
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
        return chest;
    }

    /// <summary>
    /// Tutorial entry point: deterministically force-spawns a Common chest at
    /// the top spawn point and tags it as the tutorial chest (so it triggers
    /// the Z-center freeze handler in <see cref="Chest"/>). Bypasses the random
    /// spawn interval — callers must check inventory-full / chase guards
    /// before invoking. Returns the spawned <see cref="Chest"/> or null.
    /// </summary>
    public Chest ForceSpawnTutorialChest()
    {
        Chest chest = SpawnChestOfType(ChestType.Common);
        if (chest != null)
            chest.MarkAsTutorialChest(chestTutorialFreezeZ);
        return chest;
    }

    private ChestType PickChestType()
    {
        // Tutorial/free Common Chest phase: while the player has not yet opened
        // their first 3 free Common Chests, every random spawn is forced to Common.
        // Read the save directly so the decision is authoritative even if
        // TutorialGate's static mirror has drifted across a scene transition.
        TutorialSaveData tsd = TutorialSaveData.Load();
        int openedCount = tsd != null ? tsd.tutorialFreeChestOpenedCount : 0;
        if (openedCount < TutorialGate.TutorialFreeChestQuota)
        {
            Debug.Log($"[ChestSpawner][TutorialFree] PickChestType: openedCount={openedCount} < quota={TutorialGate.TutorialFreeChestQuota} → forcing Common");
            return ChestType.Common;
        }

        double playerMoney = CurrencyManager.Instance != null ? CurrencyManager.Instance.money : 0;
        ChestTypeConfig.GetSpawnWeights(playerMoney, out float wCommon, out float wRare, out float wLegendary);

        float total = wCommon + wRare + wLegendary;
        float roll = Random.Range(0f, total);

        ChestType picked;
        if (roll < wCommon) picked = ChestType.Common;
        else if (roll < wCommon + wRare) picked = ChestType.Rare;
        else picked = ChestType.Legendary;
        Debug.Log($"[ChestSpawner][Weighted] PickChestType: openedCount={openedCount} → weighted picked={picked}");
        return picked;
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