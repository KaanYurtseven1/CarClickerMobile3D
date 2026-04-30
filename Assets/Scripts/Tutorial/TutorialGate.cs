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
    /// <summary>True once the Garage tutorial is queued / Btn_Garage is allowed to be visible.
    /// Mirrored from <see cref="TutorialSaveData"/>: thirteenDismissed || garageTutorialQueued ||
    /// fifteenDismissed || currentStepIndex &gt;= 20. Used by <see cref="TopBarAnimator"/> to
    /// suppress the auto-restore of the Btn_Garage CanvasGroup until the player has cleared
    /// the police-chase tutorial.</summary>
    public static bool GarageUnlocked { get; private set; }
    /// <summary>True while the Btn_Garage CanvasGroup is owned by <see cref="TutorialManager"/>'s
    /// dedicated reveal pipeline (<c>ShowBtnGarageAnimated</c>/<c>ShowBtnGarageImmediate</c>).
    /// While true, <see cref="TopBarAnimator"/>.SetCompact must SKIP Btn_Garage entirely so
    /// the generic TopBar restore (e.g. after a police chase) cannot flicker the button on
    /// before the dedicated reveal animation runs. Defaults to <c>true</c> so that any
    /// subsystem waking before <see cref="TutorialManager"/> is conservative. Cleared after
    /// the first reveal completes, or on load if the player is already past Step 19.</summary>
    public static bool BtnGarageOwnedByTutorial { get; private set; } = true;
    /// <summary>True once the player has completed the first Garage tutorial visit
    /// (UI_Tutorial/Fifteen dismissed in NewGarage). Mirrors
    /// <see cref="TutorialSaveData.fifteenDismissed"/>. Used by the BottomBar to
    /// permanently unlock Bank/Blacklist (and gate Ranking together with
    /// <c>RankingService.IsRankingUnlocked</c>) after the first garage visit.</summary>
    public static bool BottomBarFullyUnlocked { get; private set; }
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
        GarageUnlocked = false;
        BtnGarageOwnedByTutorial = true;
        BottomBarFullyUnlocked = false;
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
            GarageUnlocked = false;
            BtnGarageOwnedByTutorial = true;
            BottomBarFullyUnlocked = false;
            PoliceLocked = true;
            TutorialFreeChestOpenedCount = 0;
            return;
        }

        NitroUnlocked = data.nitroUnlocked;
        ChestUnlocked = data.chestUnlocked;
        RadarUnlocked = data.radarUnlocked;
        GarageUnlocked = data.thirteenDismissed || data.garageTutorialQueued || data.fifteenDismissed || data.currentStepIndex >= 20;
        // Btn_Garage is owned by the tutorial reveal until the dedicated reveal has
        // run at least once. We treat any persisted progress past Step 19
        // (garageTutorialQueued / fifteenDismissed / currentStepIndex>=20) as
        // "already revealed" — on cold reload ShowBtnGarageImmediate handles the
        // static placement and TopBarAnimator may freely manage compact/expand.
        BtnGarageOwnedByTutorial = !(data.garageTutorialQueued || data.fifteenDismissed || data.currentStepIndex >= 20);
        BottomBarFullyUnlocked = data.fifteenDismissed;
        PoliceLocked = data.policeLocked;
        TutorialFreeChestOpenedCount = Mathf.Clamp(data.tutorialFreeChestOpenedCount, 0, TutorialFreeChestQuota);
    }

    public static void SetNitroUnlocked(bool value) { NitroUnlocked = value; }
    public static void SetChestUnlocked(bool value) { ChestUnlocked = value; }
    public static void SetRadarUnlocked(bool value) { RadarUnlocked = value; }
    public static void SetGarageUnlocked(bool value) { GarageUnlocked = value; }
    /// <summary>Set by <see cref="TutorialManager"/> only. <c>true</c> while Btn_Garage's
    /// first reveal is still owned by the tutorial; <c>false</c> after
    /// <c>ShowBtnGarageAnimated</c>/<c>ShowBtnGarageImmediate</c> hands ownership to
    /// <see cref="TopBarAnimator"/>.</summary>
    public static void SetBtnGarageOwnedByTutorial(bool value) { BtnGarageOwnedByTutorial = value; }
    public static void SetBottomBarFullyUnlocked(bool value) { BottomBarFullyUnlocked = value; }
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
