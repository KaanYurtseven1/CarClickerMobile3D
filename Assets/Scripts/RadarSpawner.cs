using UnityEngine;

/// <summary>
/// Spawns Radar prefabs at a configurable interval on the side of the road.
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

    [Header("Timing")]
    [Tooltip("Base seconds between spawns (before progression scaling).")]
    [SerializeField] private float spawnInterval = 20f;
    [Tooltip("Random variance added to interval (±). 0 = fixed interval.")]
    [SerializeField] private float spawnVariance = 0f;

    [Header("Progression Scaling")]
    [Tooltip("Minimum spawn interval (fastest possible, seconds). Capped so radars remain tappable.")]
    [SerializeField] private float minSpawnInterval = 8f;
    [Tooltip("MPS threshold at which spawn interval reaches minimum.")]
    [SerializeField] private double mpsForFastest = 50000;
    [Tooltip("Enable progression-based spawn speed scaling.")]
    [SerializeField] private bool enableProgressionScaling = true;

    [Header("Limits")]
    [Tooltip("Maximum radar objects alive at once.")]
    [SerializeField] private int maxAliveRadars = 1;

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
        float effectiveInterval = GetProgressionScaledInterval();
        nextSpawnTime = effectiveInterval + Random.Range(-spawnVariance, spawnVariance);
        if (nextSpawnTime < 1f) nextSpawnTime = 1f; // safety floor
    }

    /// <summary>
    /// Returns spawn interval scaled by player progression (MPS).
    /// Uses Lerp from spawnInterval down to minSpawnInterval based on current MPS.
    /// Clamp ensures it's always humanly tappable.
    /// </summary>
    private float GetProgressionScaledInterval()
    {
        if (!enableProgressionScaling) return spawnInterval;
        if (CurrencyManager.Instance == null) return spawnInterval;

        double currentMPS = CurrencyManager.Instance.moneyPerSecond;
        if (currentMPS <= 0) return spawnInterval;

        // t = 0..1 based on log-scale MPS progression
        // Use log10 to make the curve feel smooth across orders of magnitude
        float t = Mathf.Clamp01((float)(System.Math.Log10(System.Math.Max(1, currentMPS)) / System.Math.Log10(System.Math.Max(2, mpsForFastest))));
        return Mathf.Lerp(spawnInterval, minSpawnInterval, t);
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
        Radar radar = obj.GetComponent<Radar>();
        if (radar != null)
            radar.Init(side);
    }
}
