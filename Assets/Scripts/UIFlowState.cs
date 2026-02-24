/// <summary>
/// Global static source of truth for UI content-panel suppression.
/// When a BottomBar content panel (Bank, ShopCards, TimeWarp, Ranking) is open,
/// spawning and car tapping are suppressed — but passive income / CurrencyManager
/// continue running (NO timeScale change).
/// </summary>
public static class UIFlowState
{
    private static bool _isContentPanelOpen;

    /// <summary>
    /// True when any non-Clicker BottomBar panel is active.
    /// Set by PanelManager on panel switch.
    /// </summary>
    public static bool IsContentPanelOpen
    {
        get => _isContentPanelOpen;
        set
        {
            if (_isContentPanelOpen == value) return; // no change → no log spam
            _isContentPanelOpen = value;
            UnityEngine.Debug.Log($"[UIFlowState] IsContentPanelOpen changed → {value}");
        }
    }

    /// <summary>Should NitroCoinSpawner / ChestSpawner / RadarSpawner freeze?</summary>
    public static bool IsSpawnSuppressed => _isContentPanelOpen;

    /// <summary>Should car tapping (TapInputRaycaster) be blocked?</summary>
    public static bool IsTapSuppressed => _isContentPanelOpen;

    /// <summary>
    /// Called from [RuntimeInitializeOnLoadMethod] to reset statics on domain reload.
    /// </summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _isContentPanelOpen = false;
    }
}
