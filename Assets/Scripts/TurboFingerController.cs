using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// TurboFingerController manages the Turbo Finger card effect activation.
/// 
/// ACTIVATION: 50 taps within a 15-second rolling window triggers activation.
/// EFFECT: Level-based MPT multiplier for 30 seconds.
///   Level 1 -> x5, Level 2 -> x10, Level 3 -> x20
///   Level 4 -> x50, Level 5 -> x100, Level 6+ -> x200
/// COOLDOWN: 120 seconds after effect ends before it can activate again.
/// 
/// State Machine: Ready -> Active -> Cooldown -> Ready
/// </summary>
public class TurboFingerController : MonoBehaviour
{
    public static TurboFingerController Instance { get; private set; }

    // ==================== CONFIGURATION ====================

    [Header("Activation Settings")]
    [Tooltip("Rolling window duration in seconds for tap tracking")]
    [SerializeField] private float tapWindowSeconds = 15f;

    [Tooltip("Number of taps required within the window to activate")]
    [SerializeField] private int tapsRequiredToActivate = 50;

    [Header("Effect Settings")]
    [Tooltip("Duration of the active multiplier effect in seconds")]
    [SerializeField] private float activeDurationSeconds = 30f;

    // Level-based multipliers (index = level, value = multiplier)
    // Level 0 = x1 (locked), Level 1 = x2, Level 2 = x3, etc.
    // Moderate values: noticeable but not extreme.
    private static readonly float[] LevelMultipliers = { 1f, 2f, 3f, 5f, 7f, 10f, 14f };

    [Header("Cooldown Settings")]
    [Tooltip("Cooldown duration after effect ends in seconds")]
    [SerializeField] private float cooldownDurationSeconds = 120f;

    // ==================== STATE ====================

    public enum TurboFingerState
    {
        Ready,      // Can be activated by meeting tap threshold
        Active,     // Multiplier is active
        Cooldown    // Cannot be activated, waiting for cooldown
    }

    private TurboFingerState _currentState = TurboFingerState.Ready;
    public TurboFingerState CurrentState => _currentState;

    // Tap tracking: queue of timestamps for rolling window
    private Queue<float> _tapTimestamps = new Queue<float>();

    // Timers (using Time.time for frame-rate independence)
    private float _stateEndTime = 0f;

    // ==================== PUBLIC PROPERTIES ====================

    /// <summary>
    /// Returns true if the effect is currently active (5x multiplier).
    /// </summary>
    public bool IsActive => _currentState == TurboFingerState.Active;

    /// <summary>
    /// Returns true if currently in cooldown.
    /// </summary>
    public bool IsCooldown => _currentState == TurboFingerState.Cooldown;

    /// <summary>
    /// Returns true if ready to be activated.
    /// </summary>
    public bool IsReady => _currentState == TurboFingerState.Ready;

    /// <summary>
    /// Returns the current MPT multiplier based on card level.
    /// Returns 1.0 if not active, or level-based multiplier when active.
    /// </summary>
    public float CurrentMultiplier => IsActive ? GetMultiplierForCurrentLevel() : 1f;

    /// <summary>
    /// Returns the current TurboFinger card level (0 if locked/not found).
    /// </summary>
    public int CurrentCardLevel => GetTurboFingerCardLevel();

    /// <summary>
    /// Returns the multiplier that would apply at the current card level.
    /// </summary>
    public float PotentialMultiplier => GetMultiplierForLevel(CurrentCardLevel);

    /// <summary>
    /// Remaining time in Active state (0 if not active).
    /// </summary>
    public float RemainingActiveTime => IsActive ? Mathf.Max(0f, _stateEndTime - Time.time) : 0f;

    /// <summary>
    /// Remaining time in Cooldown state (0 if not in cooldown).
    /// </summary>
    public float RemainingCooldownTime => IsCooldown ? Mathf.Max(0f, _stateEndTime - Time.time) : 0f;

    /// <summary>
    /// Current number of taps in the rolling window.
    /// </summary>
    public int CurrentTapsInWindow => _tapTimestamps.Count;

    /// <summary>
    /// Progress toward activation (0..1).
    /// </summary>
    public float ActivationProgress => Mathf.Clamp01((float)CurrentTapsInWindow / tapsRequiredToActivate);

    // ==================== EVENTS ====================

    /// <summary>
    /// Fired when Turbo Finger activates (enters Active state).
    /// </summary>
    public event Action OnActivated;

    /// <summary>
    /// Fired when the active effect ends (enters Cooldown state).
    /// </summary>
    public event Action OnEffectEnded;

    /// <summary>
    /// Fired when cooldown ends (enters Ready state).
    /// </summary>
    public event Action OnCooldownEnded;

    // ==================== UNITY LIFECYCLE ====================

    private void Awake()
    {
        // Singleton setup (does NOT use DontDestroyOnLoad - runtime only)
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        float now = Time.time;

        // State machine update
        switch (_currentState)
        {
            case TurboFingerState.Ready:
                // Nothing to update in Ready state (activation handled by OnTap)
                break;

            case TurboFingerState.Active:
                // Check if effect duration has ended
                if (now >= _stateEndTime)
                {
                    TransitionToCooldown();
                }
                break;

            case TurboFingerState.Cooldown:
                // Check if cooldown has ended
                if (now >= _stateEndTime)
                {
                    TransitionToReady();
                }
                break;
        }
    }

    // ==================== PUBLIC METHODS ====================

    /// <summary>
    /// Call this method on every player tap.
    /// Tracks taps for activation and handles state transitions.
    /// </summary>
    public void OnTap()
    {
        float now = Time.time;

        // Only track taps and check activation when in Ready state
        if (_currentState != TurboFingerState.Ready)
        {
            return;
        }

        // Check if TurboFinger card is unlocked (level >= 1)
        if (!IsTurboFingerCardUnlocked())
        {
            return;
        }

        // Enqueue this tap
        _tapTimestamps.Enqueue(now);

        // Purge taps older than the window
        while (_tapTimestamps.Count > 0 && now - _tapTimestamps.Peek() > tapWindowSeconds)
        {
            _tapTimestamps.Dequeue();
        }

        // Check if we've reached the activation threshold
        if (_tapTimestamps.Count >= tapsRequiredToActivate)
        {
            Activate();
        }
    }

    /// <summary>
    /// Force activation (for testing or special triggers).
    /// Only works if in Ready state and card is unlocked.
    /// </summary>
    public void ForceActivate()
    {
        if (_currentState == TurboFingerState.Ready && IsTurboFingerCardUnlocked())
        {
            Activate();
        }
    }

    /// <summary>
    /// Resets the controller to Ready state (for testing).
    /// </summary>
    public void Reset()
    {
        _currentState = TurboFingerState.Ready;
        _stateEndTime = 0f;
        _tapTimestamps.Clear();
        Debug.Log("[TurboFingerController] Reset to Ready state.");
    }

    // ==================== PRIVATE METHODS ====================

    /// <summary>
    /// Returns the TurboFinger card level (0 if locked/not found).
    /// </summary>
    private int GetTurboFingerCardLevel()
    {
        if (CardManager.Instance == null)
            return 0;

        CardDefinition turboCard = CardManager.Instance.GetCard(CardType.TurboFinger);
        return turboCard != null ? turboCard.currentLevel : 0;
    }

    /// <summary>
    /// Returns true if TurboFinger card is unlocked (level >= 1).
    /// </summary>
    private bool IsTurboFingerCardUnlocked()
    {
        return GetTurboFingerCardLevel() >= 1;
    }

    /// <summary>
    /// Returns the multiplier for a given card level.
    /// Level 1 -> x5, Level 2 -> x10, Level 3 -> x20
    /// Level 4 -> x50, Level 5 -> x100, Level 6+ -> x200
    /// Level 0 or below -> x1 (no effect)
    /// </summary>
    private float GetMultiplierForLevel(int level)
    {
        if (level <= 0) return 1f;

        // Clamp to max index (level 6 = index 6 = x200)
        int index = Mathf.Clamp(level, 0, LevelMultipliers.Length - 1);
        return LevelMultipliers[index];
    }

    /// <summary>
    /// Returns the multiplier for the current card level.
    /// </summary>
    private float GetMultiplierForCurrentLevel()
    {
        return GetMultiplierForLevel(GetTurboFingerCardLevel());
    }

    private void Activate()
    {
        _currentState = TurboFingerState.Active;
        _stateEndTime = Time.time + activeDurationSeconds;

        // Clear tap queue so it doesn't immediately re-trigger after cooldown
        _tapTimestamps.Clear();

        int cardLevel = GetTurboFingerCardLevel();
        float multiplier = GetMultiplierForLevel(cardLevel);
        Debug.Log($"[TurboFingerController] ACTIVATED! Card Level: {cardLevel}, Multiplier: {multiplier}x for {activeDurationSeconds}s.");

        OnActivated?.Invoke();
    }

    private void TransitionToCooldown()
    {
        _currentState = TurboFingerState.Cooldown;
        _stateEndTime = Time.time + cooldownDurationSeconds;

        Debug.Log($"[TurboFingerController] Effect ENDED. Entering cooldown for {cooldownDurationSeconds}s.");

        OnEffectEnded?.Invoke();
    }

    private void TransitionToReady()
    {
        _currentState = TurboFingerState.Ready;
        _stateEndTime = 0f;
        _tapTimestamps.Clear(); // Ensure clean slate

        Debug.Log("[TurboFingerController] Cooldown ENDED. Ready to activate again.");

        OnCooldownEnded?.Invoke();
    }

    // ==================== DEBUG ====================

#if UNITY_EDITOR
    private void OnGUI()
    {
        // Uncomment for debug overlay
        /*
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label($"TurboFinger State: {_currentState}");
        GUILayout.Label($"Taps in Window: {CurrentTapsInWindow}/{tapsRequiredToActivate}");
        GUILayout.Label($"Multiplier: {CurrentMultiplier}x");
        if (IsActive)
            GUILayout.Label($"Active Time Left: {RemainingActiveTime:F1}s");
        if (IsCooldown)
            GUILayout.Label($"Cooldown Left: {RemainingCooldownTime:F1}s");
        GUILayout.EndArea();
        */
    }
#endif
}
