using UnityEngine;

/// <summary>
/// Spawns Radar prefabs at a random interval on the side of the road.
/// Timing follows the same simple min/max random pattern as ChestSpawner
/// and NitroCoinSpawner.
/// </summary>
public class RadarSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject radarPrefab;

    [Header("Spawn Points")]
    [Tooltip("Spawn position for radars on the left side of the road.")]
    [SerializeField] private Transform spawnPointLeft;
    [Tooltip("Spawn position for radars on the right side of the road.")]
    [SerializeField] private Transform spawnPointRight;

    [Header("Spawn Timing (seconds)")]
    [SerializeField] private float minSpawnInterval = 8f;
    [SerializeField] private float maxSpawnInterval = 20f;

    [Header("Limits")]
    [Tooltip("Maximum radar objects alive at once.")]
    [SerializeField] private int maxAliveRadars = 1;

    [Header("Tutorial")]
    [Tooltip("Z position at which the tutorial Radar should stop, fire OnTutorialRadarReachedCenter, and start its idle bounce.")]
    [SerializeField] private float radarTutorialFreezeZ = -2f;

    private float timer = 0f;
    private float nextSpawnTime;

    private void Start()
    {
        ScheduleNext();
    }

    private void Update()
    {
        if (radarPrefab == null) return;
        if (spawnPointLeft == null && spawnPointRight == null) return;

        // Tutorial gating: Radar spawning is locked until a future tutorial step unlocks it.
        if (!TutorialGate.RadarUnlocked || TutorialGate.GameplayFrozen)
            return;

        // UI content-panel suppression: freeze timer while a panel is open.
        // When the panel closes the timer resumes — no burst-spawn.
        if (UIFlowState.IsSpawnSuppressed)
            return;

        timer += Time.deltaTime;
        if (timer >= nextSpawnTime)
        {
            TrySpawn();
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        timer = 0f;
        nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    private void TrySpawn()
    {
        // Guard: chest popup open
        if (ChestPopupController.Instance != null && ChestPopupController.Instance.IsPopupOpen)
            return;

        // Guard: radar popup open
        if (RadarPopupController.Instance != null && RadarPopupController.Instance.IsPopupOpen)
            return;

        // Guard: police chase active
        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
            return;

        // Guard: too many alive radars
        int alive = FindObjectsByType<Radar>(FindObjectsSortMode.None).Length;
        if (alive >= maxAliveRadars)
            return;

        // Choose left or right randomly (50/50), fallback if one is null
        bool chooseLeft = Random.value < 0.5f;
        Transform chosen;
        RadarSide side;

        if (chooseLeft && spawnPointLeft != null)
        {
            chosen = spawnPointLeft;
            side = RadarSide.Left;
        }
        else if (!chooseLeft && spawnPointRight != null)
        {
            chosen = spawnPointRight;
            side = RadarSide.Right;
        }
        else if (spawnPointLeft != null)
        {
            chosen = spawnPointLeft;
            side = RadarSide.Left;
        }
        else
        {
            chosen = spawnPointRight;
            side = RadarSide.Right;
        }

        GameObject obj = Instantiate(radarPrefab, chosen.position, chosen.rotation);

        // Adjust SM_Radar child Y rotation based on spawn side
        Transform smRadar = obj.transform.Find("SM_Radar");
        if (smRadar != null)
        {
            Vector3 euler = smRadar.localEulerAngles;
            euler.y = side == RadarSide.Left ? 135f : 225f;
            smRadar.localEulerAngles = euler;
        }

        Radar radar = obj.GetComponent<Radar>();
        if (radar != null)
            radar.Init(side);
    }

    /// <summary>
    /// Tutorial entry point: deterministically force-spawns a Radar at one of
    /// the side spawn points and tags it as the tutorial Radar (so it triggers
    /// the Z-center freeze handler in <see cref="Radar"/>). Bypasses the random
    /// spawn interval and the alive-count guard — callers must ensure no other
    /// tutorial radar is alive before invoking. Returns the spawned
    /// <see cref="Radar"/> or null.
    /// </summary>
    public Radar ForceSpawnTutorialRadar()
    {
        Debug.Log($"[RadarSpawner][RadarTut] ForceSpawnTutorialRadar ENTER: radarPrefab={(radarPrefab != null ? radarPrefab.name : "NULL")} spawnPointLeft={(spawnPointLeft != null)} spawnPointRight={(spawnPointRight != null)} RadarUnlocked={TutorialGate.RadarUnlocked} GameplayFrozen={TutorialGate.GameplayFrozen} freezeZ={radarTutorialFreezeZ}");
        if (radarPrefab == null) { Debug.LogError("[RadarSpawner][RadarTut] radarPrefab NULL on RadarSpawner — assign it in Inspector. Aborting."); return null; }
        if (spawnPointLeft == null && spawnPointRight == null) { Debug.LogError("[RadarSpawner][RadarTut] both spawn points NULL — assign at least one in Inspector. Aborting."); return null; }

        // Idempotency guard: if a tutorial radar is already alive in the scene
        // (e.g. duplicate poll within the same frame, double-routing, or a
        // residual spawn), do NOT spawn another. Return the existing one so
        // callers can still set their persisted "first spawned" flag once.
        Radar[] alive = FindObjectsByType<Radar>(FindObjectsSortMode.None);
        for (int i = 0; i < alive.Length; i++)
        {
            if (alive[i] != null && alive[i].IsTutorialRadar)
            {
                Debug.LogWarning($"[RadarSpawner][RadarTut] ForceSpawnTutorialRadar: tutorial radar '{alive[i].name}' already alive (pos={alive[i].transform.position}). Skipping duplicate spawn.");
                return alive[i];
            }
        }

        bool chooseLeft = Random.value < 0.5f;
        Transform chosen;
        RadarSide side;
        if (chooseLeft && spawnPointLeft != null)
        {
            chosen = spawnPointLeft; side = RadarSide.Left;
        }
        else if (!chooseLeft && spawnPointRight != null)
        {
            chosen = spawnPointRight; side = RadarSide.Right;
        }
        else if (spawnPointLeft != null)
        {
            chosen = spawnPointLeft; side = RadarSide.Left;
        }
        else
        {
            chosen = spawnPointRight; side = RadarSide.Right;
        }

        GameObject obj = Instantiate(radarPrefab, chosen.position, chosen.rotation);
        Debug.Log($"[RadarSpawner][RadarTut] Tutorial radar spawned side={side} position={obj.transform.position}");

        Transform smRadar = obj.transform.Find("SM_Radar");
        if (smRadar != null)
        {
            Vector3 euler = smRadar.localEulerAngles;
            euler.y = side == RadarSide.Left ? 135f : 225f;
            smRadar.localEulerAngles = euler;
        }

        Radar radar = obj.GetComponent<Radar>();
        if (radar != null)
        {
            radar.Init(side);
            radar.MarkAsTutorialRadar(radarTutorialFreezeZ);
            Debug.Log($"[RadarSpawner][RadarTut] MarkAsTutorialRadar called on '{obj.name}' freezeZ={radarTutorialFreezeZ}.");
        }
        else
        {
            Debug.LogError($"[RadarSpawner][RadarTut] Spawned object '{obj.name}' has no Radar component.");
        }

        // Reset the random spawn timer so a normal radar won't spawn while the
        // tutorial radar is alive.
        ScheduleNext();
        return radar;
    }
}
