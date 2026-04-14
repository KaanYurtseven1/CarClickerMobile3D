using UnityEngine;

/// <summary>
/// Debug tool — delegates to the real ChestSpawner so the spawn path
/// is identical to what happens during gameplay.
/// Attach to any GameObject in the scene.
/// </summary>
public class DebugChestSpawner : MonoBehaviour
{
    [Tooltip("Optional — auto-found at runtime if left empty.")]
    [SerializeField] private ChestSpawner chestSpawner;

    private ChestSpawner Spawner
    {
        get
        {
            if (chestSpawner == null)
                chestSpawner = FindObjectOfType<ChestSpawner>();
            return chestSpawner;
        }
    }

    [ContextMenu("Spawn Common Chest")]
    public void SpawnCommon() => SpawnViaReal(ChestType.Common);

    [ContextMenu("Spawn Rare Chest")]
    public void SpawnRare() => SpawnViaReal(ChestType.Rare);

    [ContextMenu("Spawn Legendary Chest")]
    public void SpawnLegendary() => SpawnViaReal(ChestType.Legendary);

    private void SpawnViaReal(ChestType type)
    {
        if (Spawner == null)
        {
            Debug.LogError("[DebugChestSpawner] No ChestSpawner found in scene!");
            return;
        }

        Spawner.SpawnChestOfType(type);
        Debug.Log($"[DebugChestSpawner] Spawned {type} chest via real ChestSpawner path.");
    }
}
