using UnityEngine;

/// <summary>
/// Guarantees that critical singleton managers exist before any scene's Awake runs.
///
/// Why this exists:
///   Managers like ChestInventoryManager and ChestSessionManager are placed as
///   GameObjects in the Main scene with DontDestroyOnLoad. If the scene transition
///   fails to preserve them (child-object DDOL bug, unexpected destruction, or
///   entering Play Mode from a non-Main scene), they become null in ChestOpenScene.
///
///   This bootstrap runs at [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]
///   — after domain reload clears statics, before ANY scene's Awake().
///   It creates any missing managers as root-level GameObjects so DDOL works correctly.
///
/// Execution order (guaranteed by Unity):
///   1. SubsystemRegistration  —  ResetStatics() clears all static Instance fields
///   2. BeforeSceneLoad        —  THIS bootstrap creates managers if missing
///   3. First scene loads      —  Scene Awake() runs; scene-placed managers detect
///                                 the existing Instance and destroy themselves (no dupe)
///
/// No scene placement required — this is a pure static class.
/// </summary>
public static class CriticalManagerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureManager<ChestInventoryManager>("ChestInventoryManager");
        EnsureManager<ChestSessionManager>("ChestSessionManager");
        EnsureManager<RankingService>("RankingService");
        Debug.Log("[Bootstrap] Critical manager bootstrap complete.");
    }

    private static void EnsureManager<T>(string managerName) where T : MonoBehaviour
    {
        // After SubsystemRegistration, statics are null. FindObjectOfType checks
        // for surviving DDOL objects from a previous play session (editor hot-reload).
        if (Object.FindObjectOfType<T>() != null)
        {
            Debug.Log($"[Bootstrap] {managerName} already exists, skipping creation.");
            return;
        }

        var go = new GameObject($"[Bootstrap] {managerName}");
        go.AddComponent<T>();
        // The component's own Awake() handles Instance assignment + DontDestroyOnLoad.
        Debug.Log($"[Bootstrap] {managerName} created as root GameObject.");
    }
}
