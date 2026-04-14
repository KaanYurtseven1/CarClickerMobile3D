using UnityEngine;

/// <summary>
/// Single authority for world scroll speed. Every object that moves
/// as part of the endless-road illusion reads EffectiveSpeed from here.
///
/// SETUP: Add this component to a persistent GameObject in the Main scene
///        (e.g. GameManager or a new "WorldScrollSpeed" object).
///        Set baseSpeed in the Inspector (default 5 matches the old RoadLooper value).
/// </summary>
public class WorldScrollSpeed : MonoBehaviour
{
    public static WorldScrollSpeed Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Speed")]
    [Tooltip("Base world scroll speed in units/second.")]
    [SerializeField] private float baseSpeed = 5f;

    [Tooltip("Multiplier applied on top of baseSpeed (e.g. 1.5 during boost). " +
             "Leave at 1 for normal gameplay.")]
    [SerializeField] private float speedMultiplier = 1f;

    /// <summary>Final speed used by road and all world objects: baseSpeed * speedMultiplier.</summary>
    public float EffectiveSpeed => baseSpeed * speedMultiplier;

    /// <summary>Read/write the base speed at runtime.</summary>
    public float BaseSpeed
    {
        get => baseSpeed;
        set => baseSpeed = Mathf.Max(0f, value);
    }

    /// <summary>Read/write the multiplier at runtime (e.g. for boost).</summary>
    public float SpeedMultiplier
    {
        get => speedMultiplier;
        set => speedMultiplier = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
