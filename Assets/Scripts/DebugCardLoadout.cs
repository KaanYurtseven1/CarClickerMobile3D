using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// DEBUG-ONLY tool: temporarily grants/activates cards for the current Play Mode session.
/// Attach to any GameObject in the scene. Configure card overrides in the Inspector.
/// All changes are session-only — nothing is saved permanently.
///
/// SAVE PROTECTION: Before SaveSystem writes card data to PlayerPrefs, this component
/// temporarily restores the real (pre-debug) card values, then re-injects debug values
/// after the save completes. See OnBeforeSaveCards / OnAfterSaveCards.
/// </summary>
public class DebugCardLoadout : MonoBehaviour
{
    public static DebugCardLoadout Instance { get; private set; }

    // ────────────────────────────────────────────────────────────
    //  Serializable entry — one per CardType
    // ────────────────────────────────────────────────────────────

    [Serializable]
    public class DebugCardEntry
    {
        [HideInInspector] public CardType cardType;

        [Tooltip("Grant this card as owned when Play starts")]
        public bool own;

        [Tooltip("Debug level to set (minimum 1 if 'own' is checked)")]
        [Min(0)] public int level = 1;

        [Tooltip("Debug segment balance toward next upgrade")]
        [Min(0)] public int segments = 0;
    }

    [Header("Debug Card Overrides")]
    [Tooltip("One entry per card type. Toggle 'own' to grant a card for this session.")]
    public List<DebugCardEntry> entries = new List<DebugCardEntry>();

    [Header("Options")]
    [Tooltip("Automatically apply debug ownership when Play Mode starts (after save loads)")]
    public bool applyOnPlayStart = true;

    // ────────────────────────────────────────────────────────────
    //  Runtime state (not serialized, session-only)
    // ────────────────────────────────────────────────────────────

    private bool _debugActive;
    /// <summary>True if debug overrides are currently applied to CardManager data.</summary>
    public bool IsDebugActive => _debugActive;

    private struct CardSnapshot
    {
        public CardType type;
        public int realLevel;
        public int realCopies;
    }
    private List<CardSnapshot> _realData = new List<CardSnapshot>();

    // ────────────────────────────────────────────────────────────
    //  Lifecycle
    // ────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        EnsureAllCardTypes(); // populate missing entries at runtime too
    }

    private void OnEnable()
    {
        SaveSystem.OnGameLoaded += OnGameLoaded;
    }

    private void OnDisable()
    {
        SaveSystem.OnGameLoaded -= OnGameLoaded;
    }

    private void OnDestroy()
    {
        // Restore real data before this object dies, preventing any late save
        // from capturing debug values.
        if (_debugActive)
        {
            RestoreRealData();
            _debugActive = false;
        }
        if (Instance == this) Instance = null;
    }

    // ────────────────────────────────────────────────────────────
    //  Auto-apply after save system loads
    // ────────────────────────────────────────────────────────────

    private void OnGameLoaded()
    {
        if (!applyOnPlayStart) return;

        bool anyOwned = false;
        foreach (var e in entries)
            if (e.own) { anyOwned = true; break; }

        if (anyOwned)
            ApplyDebugOwnership();
    }

    // ────────────────────────────────────────────────────────────
    //  Snapshot / Restore helpers
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Snapshots the current (real, saved) card data from CardManager
    /// so we can restore it before any save operation.
    /// </summary>
    private void SnapshotRealData()
    {
        _realData.Clear();
        if (CardManager.Instance == null || CardManager.Instance.cards == null) return;

        foreach (var card in CardManager.Instance.cards)
        {
            _realData.Add(new CardSnapshot
            {
                type = card.type,
                realLevel = card.currentLevel,
                realCopies = card.copiesOwned
            });
        }
    }

    /// <summary>
    /// Restores real (pre-debug) card data to CardDefinition fields.
    /// </summary>
    private void RestoreRealData()
    {
        if (CardManager.Instance == null || CardManager.Instance.cards == null) return;

        foreach (var snap in _realData)
        {
            var card = CardManager.Instance.GetCard(snap.type);
            if (card != null)
            {
                card.currentLevel = snap.realLevel;
                card.copiesOwned = snap.realCopies;
            }
        }
    }

    /// <summary>
    /// Re-writes debug override values into CardDefinition fields
    /// for all entries that have 'own' checked.
    /// </summary>
    private void InjectDebugData()
    {
        if (CardManager.Instance == null || CardManager.Instance.cards == null) return;

        foreach (var entry in entries)
        {
            if (!entry.own) continue;

            var card = CardManager.Instance.GetCard(entry.cardType);
            if (card == null) continue;

            card.currentLevel = Mathf.Max(entry.level, 1);
            card.copiesOwned = entry.segments;
        }

        // Let CardManager recalculate bonuses/effects at new levels
        CardManager.Instance.ReapplyAllCardEffects();
    }

    // ────────────────────────────────────────────────────────────
    //  Public actions (Inspector buttons + context menu)
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Grants debug ownership for all checked entries. Call during Play Mode.
    /// </summary>
    [ContextMenu("Apply Debug Ownership")]
    public void ApplyDebugOwnership()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugCardLoadout] Can only apply in Play Mode.");
            return;
        }
        if (CardManager.Instance == null)
        {
            Debug.LogWarning("[DebugCardLoadout] CardManager not ready.");
            return;
        }

        // Snapshot BEFORE we overwrite anything
        SnapshotRealData();

        // Inject debug values into CardDefinition fields
        InjectDebugData();

        _debugActive = true;

        foreach (var entry in entries)
        {
            if (!entry.own) continue;
            Debug.Log($"[DebugCardLoadout] DEBUG OWN: {entry.cardType} → Level {Mathf.Max(entry.level, 1)}, Segments {entry.segments}");
        }
        Debug.Log("[DebugCardLoadout] Debug ownership applied (session-only).");
    }

    /// <summary>
    /// DEBUG: Immediately activates both Nitro Magnet and Nitro Rain together.
    /// Play Mode only. Uses existing card levels or defaults to level 1.
    /// </summary>
    [ContextMenu("Start Nitro Magnet + Nitro Rain Now")]
    public void DebugStartMagnetAndRain()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugCardLoadout] Can only trigger in Play Mode.");
            return;
        }

        // Ensure NitroMagnet card is owned at level >= 1 so the system works
        if (CardManager.Instance != null)
        {
            int magnetLevel = CardManager.Instance.GetCardLevel(CardType.NitroMagnet);
            if (magnetLevel < 1)
            {
                var card = CardManager.Instance.GetCard(CardType.NitroMagnet);
                if (card != null)
                {
                    if (!_debugActive) SnapshotRealData();
                    card.currentLevel = 1;
                    _debugActive = true;
                    Debug.Log("[DebugCardLoadout] NitroMagnet card granted at level 1 for debug.");
                }
            }

            int rainLevel = CardManager.Instance.GetCardLevel(CardType.NitroRain);
            if (rainLevel < 1)
            {
                var card = CardManager.Instance.GetCard(CardType.NitroRain);
                if (card != null)
                {
                    if (!_debugActive) SnapshotRealData();
                    card.currentLevel = 1;
                    _debugActive = true;
                    Debug.Log("[DebugCardLoadout] NitroRain card granted at level 1 for debug.");
                }
            }
        }

        // 1) Force-arm Nitro Magnet
        if (NitroMagnetController.Instance != null)
        {
            NitroMagnetController.Instance.DebugForceArm();
        }
        else
        {
            Debug.LogWarning("[DebugCardLoadout] NitroMagnetController.Instance is null!");
        }

        // 2) Force-start Nitro Rain
        if (NitroRainController.Instance != null)
        {
            NitroRainController.Instance.ForceStartRain();
        }
        else
        {
            Debug.LogWarning("[DebugCardLoadout] NitroRainController.Instance is null!");
        }

        Debug.Log("[DebugCardLoadout] Nitro Magnet + Nitro Rain triggered.");
    }

    /// <summary>
    /// DEBUG: Immediately triggers Turbo / Boost Mode.
    /// Play Mode only. Ensures BoostMode card is owned, then force-starts boost.
    /// </summary>
    [ContextMenu("Start Boost Mode Now")]
    public void DebugStartBoostMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugCardLoadout] Can only trigger in Play Mode.");
            return;
        }

        // Ensure BoostMode card is owned at level >= 1
        if (CardManager.Instance != null)
        {
            int boostLevel = CardManager.Instance.GetCardLevel(CardType.BoostMode);
            if (boostLevel < 1)
            {
                var card = CardManager.Instance.GetCard(CardType.BoostMode);
                if (card != null)
                {
                    if (!_debugActive) SnapshotRealData();
                    card.currentLevel = 1;
                    _debugActive = true;
                    CardManager.Instance.ReapplyAllCardEffects();
                    Debug.Log("[DebugCardLoadout] BoostMode card granted at level 1 for debug.");
                }
            }
        }

        if (BoostModeController.Instance != null)
        {
            BoostModeController.Instance.DebugForceBoost();
        }
        else
        {
            Debug.LogWarning("[DebugCardLoadout] BoostModeController.Instance is null!");
        }

        Debug.Log("[DebugCardLoadout] Boost Mode triggered.");
    }

    /// <summary>
    /// DEBUG: Immediately skips Turbo / Boost Mode cooldown so charging can resume.
    /// Play Mode only. No-op if boost is not currently in cooldown.
    /// </summary>
    [ContextMenu("Skip Boost Cooldown")]
    public void DebugSkipBoostCooldown()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugCardLoadout] Can only trigger in Play Mode.");
            return;
        }

        if (BoostModeController.Instance != null)
        {
            BoostModeController.Instance.DebugSkipCooldown();
        }
        else
        {
            Debug.LogWarning("[DebugCardLoadout] BoostModeController.Instance is null!");
        }
    }

    /// <summary>
    /// Clears all debug overrides and restores real card data.
    /// </summary>
    [ContextMenu("Clear Debug Cards")]
    public void ClearDebugCards()
    {
        if (!_debugActive)
        {
            Debug.Log("[DebugCardLoadout] No debug cards active.");
            return;
        }

        RestoreRealData();

        if (CardManager.Instance != null)
            CardManager.Instance.ReapplyAllCardEffects();

        _debugActive = false;
        _realData.Clear();
        Debug.Log("[DebugCardLoadout] Debug cards cleared. Real card data restored.");
    }

    // ────────────────────────────────────────────────────────────
    //  Save protection — called by SaveSystem (see SaveSystem.cs)
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by SaveSystem BEFORE card data is written to PlayerPrefs.
    /// Temporarily swaps debug values back to real values so saves stay clean.
    /// </summary>
    public static void OnBeforeSaveCards()
    {
        if (Instance != null && Instance._debugActive)
        {
            Instance.RestoreRealData();
        }
    }

    /// <summary>
    /// Called by SaveSystem AFTER card data is written to PlayerPrefs.
    /// Re-applies debug overrides so gameplay continues with debug cards.
    /// </summary>
    public static void OnAfterSaveCards()
    {
        if (Instance != null && Instance._debugActive)
        {
            Instance.InjectDebugData();
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Entry management — ensures one entry per CardType
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the entries list contains exactly one entry per CardType enum value.
    /// Called from the custom editor and at runtime Awake.
    /// </summary>
    public void EnsureAllCardTypes()
    {
        var allTypes = (CardType[])Enum.GetValues(typeof(CardType));
        var existing = new HashSet<CardType>();

        // Remove duplicates, track what we have
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (!existing.Add(entries[i].cardType))
                entries.RemoveAt(i); // duplicate
        }

        // Add missing types
        foreach (var type in allTypes)
        {
            if (!existing.Contains(type))
            {
                entries.Add(new DebugCardEntry
                {
                    cardType = type,
                    own = false,
                    level = 1,
                    segments = 0
                });
            }
        }

        // Sort to match enum order
        entries.Sort((a, b) => a.cardType.CompareTo(b.cardType));
    }
}
