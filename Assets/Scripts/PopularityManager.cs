using UnityEngine;
using System;

public class PopularityManager : MonoBehaviour
{
    public static PopularityManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    /// <summary>
    /// Fired whenever popularity changes. Parameter: normalized value 0..1.
    /// </summary>
    public event Action<float> OnPopularityChanged;

    [Header("Runtime (read-only)")]
    [SerializeField] private float popularity01 = 0f;

    /// <summary>Current popularity as normalized 0..1.</summary>
    public float Popularity01 => popularity01;

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

    /// <summary>
    /// Increase popularity by a normalized delta (e.g. 0.1 = +10%).
    /// </summary>
    public void Increase(float delta01)
    {
        Set(popularity01 + delta01);
    }

    /// <summary>
    /// Set popularity to an exact normalized value (clamped 0..1).
    /// </summary>
    public void Set(float value01)
    {
        float clamped = Mathf.Clamp01(value01);
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
        Set(0f);
    }

    // ---- Editor-only manual test ----
#if UNITY_EDITOR
    [ContextMenu("TEST: +10% Popularity")]
    private void DebugIncrease10()
    {
        Increase(0.1f);
        Debug.Log($"[PopularityManager] TEST popularity now: {popularity01:P0}");
    }

    [ContextMenu("TEST: Reset Popularity")]
    private void DebugReset()
    {
        Reset();
        Debug.Log("[PopularityManager] TEST popularity reset to 0");
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
