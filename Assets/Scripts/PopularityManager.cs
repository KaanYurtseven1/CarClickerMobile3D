using UnityEngine;
using System;

/// <summary>
/// Popularity stages (0–100 integer scale mapped to 0..1 normalized).
/// Each stage has a color and a radar-catch threshold for police chase.
/// </summary>
public enum PopularityStage
{
    Stage1 = 1, // [0, 18)   — safest
    Stage2 = 2, // [18, 36)
    Stage3 = 3, // [36, 54)
    Stage4 = 4, // [54, 72)
    Stage5 = 5, // [72, 90)
    Stage6 = 6  // [90, 100] — most dangerous
}

public class PopularityManager : MonoBehaviour
{
    public static PopularityManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnRadarPhotoTaken = null;
    }

    // ==================== EVENTS ====================

    /// <summary>
    /// Fired whenever popularity changes. Parameter: normalized value 0..1.
    /// </summary>
    public event Action<float> OnPopularityChanged;

    /// <summary>
    /// Static event fired when a radar takes a photo (player missed the radar).
    /// Subscribers (e.g. PoliceCatchTrigger) use this to track catch count.
    /// </summary>
    public static event Action OnRadarPhotoTaken;

    // ==================== STAGE COLORS ====================

    /// <summary>Colors for each popularity stage, indexable by (stage - 1).</summary>
    public static readonly Color[] StageColors = new Color[]
    {
        HexToColor("2E7F18"), // Stage1
        HexToColor("45731E"), // Stage2
        HexToColor("675E24"), // Stage3
        HexToColor("8D472B"), // Stage4
        HexToColor("B13433"), // Stage5
        HexToColor("C82538"), // Stage6
    };

    /// <summary>Radar-catch thresholds per stage (catches needed to trigger police).</summary>
    public static readonly int[] StageThresholds = new int[]
    {
        13, // Stage1
        11, // Stage2
         9, // Stage3
         7, // Stage4
         5, // Stage5
         3, // Stage6
    };

    // ==================== SERIALIZED ====================

    [Header("Debug")]
    [Tooltip("When true, every popularity change logs source, context, before/after, frame, and stack trace.")]
    public bool enableDebug = false;

    [Header("Runtime (read-only)")]
    [SerializeField] private float popularity01 = 0f;

    /// <summary>Current popularity as normalized 0..1.</summary>
    public float Popularity01 => popularity01;

    /// <summary>Current popularity mapped to integer 0–100.</summary>
    public int PopularityInt => Mathf.RoundToInt(popularity01 * 100f);

    // ==================== LIFECYCLE ====================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    // ==================== STAGE CALCULATION ====================

    /// <summary>
    /// Returns the current popularity stage based on integer popularity (0–100).
    /// Boundaries: 0-17 → Stage1, 18-35 → Stage2, 36-53 → Stage3,
    ///             54-71 → Stage4, 72-89 → Stage5, 90-100 → Stage6.
    /// </summary>
    public PopularityStage GetCurrentStage()
    {
        return GetStageForValue(popularity01);
    }

    /// <summary>
    /// Returns the stage for a given normalized popularity value.
    /// </summary>
    public static PopularityStage GetStageForValue(float value01)
    {
        int p = Mathf.RoundToInt(Mathf.Clamp01(value01) * 100f);
        if (p < 18) return PopularityStage.Stage1;
        if (p < 36) return PopularityStage.Stage2;
        if (p < 54) return PopularityStage.Stage3;
        if (p < 72) return PopularityStage.Stage4;
        if (p < 90) return PopularityStage.Stage5;
        return PopularityStage.Stage6;
    }

    /// <summary>
    /// Returns the color for a given stage.
    /// </summary>
    public static Color GetStageColor(PopularityStage stage)
    {
        int idx = (int)stage - 1;
        if (idx >= 0 && idx < StageColors.Length)
            return StageColors[idx];
        return StageColors[0];
    }

    /// <summary>
    /// Returns the radar-catch threshold for a given stage.
    /// </summary>
    public static int GetStageThreshold(PopularityStage stage)
    {
        int idx = (int)stage - 1;
        if (idx >= 0 && idx < StageThresholds.Length)
            return StageThresholds[idx];
        return StageThresholds[0];
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// THE single entry point for all popularity INCREMENTS.
    /// Every caller must provide a distinct source label so duplicates are easy to spot.
    /// When enableDebug is true, logs: source, context, before→after (normalized + int),
    /// Time.frameCount, and a short stack trace.
    /// </summary>
    /// <param name="delta">Normalized delta (0.01 = +1 on the 0–100 scale).</param>
    /// <param name="source">Caller label, e.g. "Radar.OnMissed".</param>
    /// <param name="context">Optional UnityEngine.Object for name+instanceID logging.</param>
    public void AddPopularityNormalized(float delta, string source, UnityEngine.Object context = null)
    {
        float before = popularity01;
        float after = Mathf.Clamp01(popularity01 + delta);

        if (enableDebug)
        {
            int beforeInt = Mathf.RoundToInt(before * 100f);
            int afterInt = Mathf.RoundToInt(after * 100f);
            string ctxInfo = context != null
                ? $"{context.name} (id={context.GetInstanceID()})"
                : "(no context)";
            string stack = TrimmedStackTrace(6);
            Debug.Log($"[PopularityManager] AddPopularityNormalized\n" +
                      $"  source   = {source}\n" +
                      $"  context  = {ctxInfo}\n" +
                      $"  delta    = {delta:F4}\n" +
                      $"  before   = {before:F4} ({beforeInt}/100)\n" +
                      $"  after    = {after:F4} ({afterInt}/100)\n" +
                      $"  frame    = {Time.frameCount}\n" +
                      $"  stack:\n{stack}", context);
        }

        if (Mathf.Approximately(after, popularity01))
            return;

        popularity01 = after;
        OnPopularityChanged?.Invoke(popularity01);
    }

    /// <summary>
    /// Set popularity to an exact normalized value (clamped 0..1).
    /// Used for loading saves and resets — NOT for gameplay increments.
    /// </summary>
    /// <param name="source">Caller label for debug tracing.</param>
    public void Set(float value01, string source = "")
    {
        float before = popularity01;
        float clamped = Mathf.Clamp01(value01);

        if (enableDebug && !string.IsNullOrEmpty(source))
        {
            int beforeInt = Mathf.RoundToInt(before * 100f);
            int afterInt = Mathf.RoundToInt(clamped * 100f);
            Debug.Log($"[PopularityManager] Set\n" +
                      $"  source = {source}\n" +
                      $"  before = {before:F4} ({beforeInt}/100)\n" +
                      $"  after  = {clamped:F4} ({afterInt}/100)\n" +
                      $"  frame  = {Time.frameCount}");
        }

        if (Mathf.Approximately(clamped, popularity01))
            return;

        popularity01 = clamped;
        OnPopularityChanged?.Invoke(popularity01);
    }

    /// <summary>
    /// Reset popularity to 0.
    /// </summary>
    public void Reset()
    {
        Set(0f, "PopularityManager.Reset");
    }

    /// <summary>
    /// Call this when a radar takes a photo (player missed a radar).
    /// Fires the static OnRadarPhotoTaken event so subscribers can react.
    /// </summary>
    public void NotifyRadarPhotoTaken()
    {
        OnRadarPhotoTaken?.Invoke();
    }

    // ==================== HELPERS ====================

    private static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
            return c;
        return Color.white;
    }

    // ==================== INTERNAL HELPERS ====================

    /// <summary>Returns first N lines of the current stack trace (skips this method).</summary>
    private static string TrimmedStackTrace(int maxLines)
    {
        string full = System.Environment.StackTrace;
        if (string.IsNullOrEmpty(full)) return "  (unavailable)";
        string[] lines = full.Split('\n');
        // Skip first 2 lines (TrimmedStackTrace + AddPopularityNormalized themselves)
        var sb = new System.Text.StringBuilder();
        int printed = 0;
        for (int i = 2; i < lines.Length && printed < maxLines; i++)
        {
            string line = lines[i].TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;
            sb.Append("    ").AppendLine(line);
            printed++;
        }
        return sb.ToString();
    }

    // ---- Editor-only manual test ----
#if UNITY_EDITOR
    [ContextMenu("TEST: +10% Popularity")]
    private void DebugIncrease10()
    {
        AddPopularityNormalized(0.1f, "DebugIncrease10");
    }

    [ContextMenu("TEST: Reset Popularity")]
    private void DebugReset()
    {
        Reset();
    }
#endif

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
