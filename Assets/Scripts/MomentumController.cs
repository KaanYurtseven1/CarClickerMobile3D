using UnityEngine;
using System;

/// <summary>
/// MomentumController – Card B: "Momentum"
///
/// Consecutive clicks build stacks that multiply click income.
/// If the player stops clicking for longer than resetWindowSeconds, stacks reset to 0.
///
/// ╔════════╦══════════════════╦══════════════╦══════════════╦═══════════════════════════╗
/// ║ Level  ║ resetWindow (s)  ║ perStackBonus║ stackCap     ║ Max multiplier at cap     ║
/// ╠════════╬══════════════════╬══════════════╬══════════════╬═══════════════════════════╣
/// ║   1    ║   0.80           ║  0.005 (0.5%)║   30         ║ 1 + 30*0.005  = 1.15      ║
/// ║   2    ║   1.00           ║  0.007 (0.7%)║   40         ║ 1 + 40*0.007  = 1.28      ║
/// ║   3    ║   1.20           ║  0.009 (0.9%)║   50         ║ 1 + 50*0.009  = 1.45      ║
/// ║   4    ║   1.40           ║  0.011 (1.1%)║   60         ║ 1 + 60*0.011  = 1.66      ║
/// ║   5    ║   1.60           ║  0.013 (1.3%)║   70         ║ 1 + 70*0.013  = 1.91      ║
/// ║   6    ║   1.80           ║  0.015 (1.5%)║   80         ║ 1 + 80*0.015  = 2.20      ║
/// ╚════════╩══════════════════╩══════════════╩══════════════╩═══════════════════════════╝
///
/// Formulas (level 1–6):
///   resetWindowSeconds(level)  = 0.80 + (level-1) * 0.20
///   perStackBonus(level)       = 0.005 + (level-1) * 0.002
///   stackCap(level)            = 30 + (level-1) * 10
///   multiplier(stacks, level)  = 1 + min(stacks, stackCap(level)) * perStackBonus(level)
///
/// Values reasoning:
///   • resetWindow starts tight (0.8 s) to reward fast tapping; extra 0.2 s per level is forgiving.
///   • perStackBonus is intentionally small (0.5 %–1.5 %) so it doesn't overshadow other systems.
///   • stackCap grows with level to reward sustained play at higher card investment.
///   • Max multiplier at level 6 cap (×2.20) is meaningful but not game-breaking.
///
/// Anti-exploit:
///   autoClickAllowed flag controls whether auto-clickers can build stacks.
///   Call RegisterClick(isAutoClick: true) from auto-click sources; stacks only increase
///   if autoClickAllowed is true.
///
/// Integration:
///   • Attach to same persistent GameObject.
///   • TapInputRaycaster calls MomentumController.Instance.RegisterClick() on valid car taps.
///   • TapInputRaycaster reads MomentumController.Instance.CurrentMultiplier in the reward path.
///   • Update() handles time-based stack reset.
/// </summary>
public class MomentumController : MonoBehaviour
{
    public static MomentumController Instance { get; private set; }

    // ==================== CONFIGURATION ====================

    [Header("Base Scaling (level 1 values)")]
    [Tooltip("Reset window at level 1 (seconds)")]
    [SerializeField] private float baseResetWindow = 0.80f;

    [Tooltip("Additional reset window per level above 1")]
    [SerializeField] private float resetWindowStep = 0.20f;

    [Tooltip("Per-stack bonus multiplier at level 1 (0.005 = 0.5%)")]
    [SerializeField] private float basePerStackBonus = 0.005f;

    [Tooltip("Additional per-stack bonus per level above 1")]
    [SerializeField] private float perStackBonusStep = 0.002f;

    [Tooltip("Stack cap at level 1")]
    [SerializeField] private int baseStackCap = 30;

    [Tooltip("Additional stack cap per level above 1")]
    [SerializeField] private int stackCapStep = 10;

    [Header("Anti-Exploit")]
    [Tooltip("If false, auto-click sources cannot build Momentum stacks")]
    [SerializeField] private bool autoClickAllowed = false;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = false;

    // ==================== STATE ====================

    private int _currentStacks = 0;
    private float _lastClickTime = -999f;

    // Diagnostic gating: log only when state changes meaningfully
    private int _lastLoggedLevel = -1;
    private int _lastLoggedStacks = -1;
    private float _lastLoggedMultiplier = -1f;

    // ==================== EVENTS ====================

    /// <summary>
    /// Fired whenever stacks change. UI can display a combo counter / bar.
    /// Parameters: currentStacks, currentMultiplier
    /// </summary>
    public event Action<int, float> OnMomentumChanged;

    // ==================== PUBLIC PROPERTIES ====================

    /// <summary>Current number of Momentum stacks.</summary>
    public int CurrentStacks => _currentStacks;

    /// <summary>
    /// Current click-income multiplier from Momentum.
    /// Always >= 1.0. This value should multiply the final click reward.
    /// </summary>
    public float CurrentMultiplier => GetMultiplier(_currentStacks, GetCardLevel());

    /// <summary>Stack cap at the current card level.</summary>
    public int CurrentStackCap => GetStackCap(GetCardLevel());

    /// <summary>Reset window at the current card level (seconds).</summary>
    public float CurrentResetWindow => GetResetWindow(GetCardLevel());

    // ==================== LIFECYCLE ====================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void Update()
    {
        // Check for stack reset due to inactivity
        if (_currentStacks > 0)
        {
            int level = GetCardLevel();
            if (level <= 0)
            {
                // Card locked — silently keep stacks at 0
                if (_currentStacks != 0)
                {
                    _currentStacks = 0;
                    OnMomentumChanged?.Invoke(0, 1f);
                }
                return;
            }

            float window = GetResetWindow(level);
            if (Time.time - _lastClickTime > window)
            {
                int oldStacks = _currentStacks;
                _currentStacks = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (verboseLogs)
                    Debug.Log($"[Momentum] Stacks RESET (timeout {window:F2}s exceeded). Was {oldStacks}.");
#endif

                OnMomentumChanged?.Invoke(0, 1f);
            }
        }
    }

    // ==================== CORE LOGIC ====================

    /// <summary>
    /// Call this when the player performs a valid click on the car.
    /// Increases Momentum stacks by 1 (up to cap).
    /// </summary>
    /// <param name="isAutoClick">True if this click came from an auto-clicker source.</param>
    public void RegisterClick(bool isAutoClick = false)
    {
        int level = GetCardLevel();
        if (level <= 0) return; // Card locked

        // Anti-exploit: block auto-clickers if not allowed
        if (isAutoClick && !autoClickAllowed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log("[Momentum] Auto-click blocked by autoClickAllowed=false");
#endif
            return;
        }

        float now = Time.time;
        float window = GetResetWindow(level);

        // Reset if too much time has passed since last click
        if (now - _lastClickTime > window)
        {
            _currentStacks = 0;
        }

        _lastClickTime = now;

        int cap = GetStackCap(level);
        if (_currentStacks < cap)
        {
            _currentStacks++;
        }
        // else: already at cap, don't increase further

        float multiplier = GetMultiplier(_currentStacks, level);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log($"[Momentum] Click! stacks={_currentStacks}/{cap}, multiplier={multiplier:F3}");
#endif

        // [CardEffectApplied] Diagnostic: log when effect becomes meaningful or changes significantly
        if (multiplier > 1.0f && (level != _lastLoggedLevel || _currentStacks != _lastLoggedStacks || Mathf.Abs(multiplier - _lastLoggedMultiplier) > 0.05f))
        {
            Debug.Log($"[CardEffectApplied] Momentum L{level}: stacks={_currentStacks}/{cap}, multiplier=x{multiplier:F3}, context=RegisterClick");
            _lastLoggedLevel = level;
            _lastLoggedStacks = _currentStacks;
            _lastLoggedMultiplier = multiplier;
        }

        OnMomentumChanged?.Invoke(_currentStacks, multiplier);
    }

    // ==================== PARAMETER FUNCTIONS ====================

    /// <summary>
    /// Reset window in seconds. Longer window = more forgiving.
    /// Formula: 0.80 + (level-1) * 0.20
    /// </summary>
    public float GetResetWindow(int level)
    {
        if (level <= 0) return baseResetWindow;
        return baseResetWindow + (level - 1) * resetWindowStep;
    }

    /// <summary>
    /// Per-stack bonus multiplier.
    /// Formula: 0.005 + (level-1) * 0.002
    /// </summary>
    public float GetPerStackBonus(int level)
    {
        if (level <= 0) return 0f;
        return basePerStackBonus + (level - 1) * perStackBonusStep;
    }

    /// <summary>
    /// Maximum number of stacks the player can accumulate.
    /// Formula: 30 + (level-1) * 10
    /// </summary>
    public int GetStackCap(int level)
    {
        if (level <= 0) return 0;
        return baseStackCap + (level - 1) * stackCapStep;
    }

    /// <summary>
    /// Click-income multiplier from Momentum stacks.
    /// Formula: 1 + min(stacks, stackCap(level)) * perStackBonus(level)
    /// Always returns >= 1.0.
    /// </summary>
    public float GetMultiplier(int stacks, int level)
    {
        if (level <= 0 || stacks <= 0) return 1f;
        int effectiveStacks = Mathf.Min(stacks, GetStackCap(level));
        float bonus = GetPerStackBonus(level);
        return 1f + effectiveStacks * bonus;
    }

    // ==================== HELPERS ====================

    private int GetCardLevel()
    {
        return CardManager.Instance != null
            ? CardManager.Instance.GetCardLevel(CardType.Momentum)
            : 0;
    }

    /// <summary>
    /// Force-reset stacks (e.g., on scene load or card level change).
    /// </summary>
    public void ResetStacks()
    {
        _currentStacks = 0;
        _lastClickTime = -999f;
        OnMomentumChanged?.Invoke(0, 1f);
    }
}
