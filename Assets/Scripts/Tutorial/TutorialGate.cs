using System;
using UnityEngine;

/// <summary>
/// Tutorial-controlled feature gate authority.
///
/// Distinct from <see cref="UIFlowState"/> (which gates panel-driven suppression).
/// This static is the single source of truth for tutorial-driven unlocks and the
/// tutorial-only "GameplayFrozen" state used while the player is being shown a
/// scripted on-board moment (e.g. first Nitro Coin reaches the screen center).
///
/// Persistent unlock flags are mirrored from <see cref="TutorialSaveData"/> at
/// startup so subsystems that wake up before <see cref="TutorialManager"/> still
/// see the correct gates.
/// </summary>
public static class TutorialGate
{
    // ── Persistent unlock flags (mirrored from TutorialSaveData) ──
    public static bool NitroUnlocked { get; private set; }
    public static bool ChestUnlocked { get; private set; }
    public static bool RadarUnlocked { get; private set; }
    /// <summary>True while police chase is fully suppressed by tutorial gating.</summary>
    public static bool PoliceLocked { get; private set; } = true;

    // ── Transient runtime flags (NOT persisted) ──
    /// <summary>True while the tutorial has frozen gameplay (movers/spawn ticking/car taps).</summary>
    public static bool GameplayFrozen { get; private set; }
    /// <summary>True while a tutorial-tagged Nitro Coin is alive in the world.</summary>
    public static bool TutorialNitroActive { get; private set; }
    /// <summary>True while the deterministic tutorial Common Chest is alive in the world (between force-spawn and tap).</summary>
    public static bool TutorialChestActive { get; private set; }
    /// <summary>True while the post-first-free-chest Cards tutorial segment is suspending new chest spawns. Not persisted; re-asserted by TutorialManager.ApplyTutorialState on every load.</summary>
    public static bool ChestPipelineSuspended { get; private set; }

    // ── Tutorial/free Common Chest quota (mirrored from save) ──
    /// <summary>Hard cap on the number of "free" tutorial Common Chests the player gets at the start of the chest tutorial.</summary>
    public const int TutorialFreeChestQuota = 3;
    /// <summary>Mirrored from <see cref="TutorialSaveData.tutorialFreeChestOpenedCount"/>. While &lt; <see cref="TutorialFreeChestQuota"/> the chest spawner forces Common only and new chests are added as instantly-openable.</summary>
    public static int TutorialFreeChestOpenedCount { get; private set; }

    /// <summary>
    /// Raised when <see cref="GameplayFrozen"/> transitions (debounced — no event when value
    /// is set to its current value). Subscribers should pause/resume side‐effects that cannot
    /// be expressed by simple polling (e.g. <see cref="Animator.speed"/> on the active car).
    /// </summary>
    public static event Action<bool> OnGameplayFrozenChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        NitroUnlocked = false;
        ChestUnlocked = false;
        RadarUnlocked = false;
        PoliceLocked = true;
        GameplayFrozen = false;
        TutorialNitroActive = false;
        TutorialChestActive = false;
        ChestPipelineSuspended = false;
        TutorialFreeChestOpenedCount = 0;
        OnGameplayFrozenChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitFromSave()
    {
        // Subsystems may read TutorialGate before TutorialManager.Awake runs,
        // so mirror persisted unlock flags as early as possible.
        TutorialSaveData data = TutorialSaveData.Load();
        SyncFromSave(data);
        // Transient flags always start clean on scene load.
        GameplayFrozen = false;
        TutorialNitroActive = false;
        TutorialChestActive = false;
        ChestPipelineSuspended = false;
    }

    /// <summary>
    /// Mirror persistent unlock flags from a loaded <see cref="TutorialSaveData"/> instance.
    /// Safe to call any time the save changes (e.g. after SaveSystem.OnGameLoaded).
    /// </summary>
    public static void SyncFromSave(TutorialSaveData data)
    {
        if (data == null)
        {
            NitroUnlocked = false;
            ChestUnlocked = false;
            RadarUnlocked = false;
            PoliceLocked = true;
            TutorialFreeChestOpenedCount = 0;
            return;
        }

        NitroUnlocked = data.nitroUnlocked;
        ChestUnlocked = data.chestUnlocked;
        RadarUnlocked = data.radarUnlocked;
        PoliceLocked = data.policeLocked;
        TutorialFreeChestOpenedCount = Mathf.Clamp(data.tutorialFreeChestOpenedCount, 0, TutorialFreeChestQuota);
    }

    public static void SetNitroUnlocked(bool value) { NitroUnlocked = value; }
    public static void SetChestUnlocked(bool value) { ChestUnlocked = value; }
    public static void SetRadarUnlocked(bool value) { RadarUnlocked = value; }
    public static void SetPoliceLocked(bool value) { PoliceLocked = value; }

    public static void SetGameplayFrozen(bool value)
    {
        if (GameplayFrozen == value) return;
        GameplayFrozen = value;
        try { OnGameplayFrozenChanged?.Invoke(value); }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    public static void SetTutorialNitroActive(bool value) { TutorialNitroActive = value; }
    public static void SetTutorialChestActive(bool value) { TutorialChestActive = value; }
    public static void SetChestPipelineSuspended(bool value) { ChestPipelineSuspended = value; }
    public static void SetTutorialFreeChestOpenedCount(int value)
    {
        TutorialFreeChestOpenedCount = Mathf.Clamp(value, 0, TutorialFreeChestQuota);
    }

    /// <summary>
    /// True if the next world chest added to inventory should be marked as a tutorial/free Common chest
    /// (instantly openable, no timer/cost). Caller passes the number of unopened tutorial-free chests
    /// already in inventory so the global quota is respected across spawn + collect timing.
    /// </summary>
    public static bool ShouldNextWorldChestBeTutorialFree(int currentTutorialFreeUnopenedInInventory)
    {
        if (currentTutorialFreeUnopenedInInventory < 0) currentTutorialFreeUnopenedInInventory = 0;
        return TutorialFreeChestOpenedCount + currentTutorialFreeUnopenedInInventory < TutorialFreeChestQuota;
    }
}
