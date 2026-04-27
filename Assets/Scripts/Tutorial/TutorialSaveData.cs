using System;
using UnityEngine;

[Serializable]
public class TutorialSaveData
{
    private const string PrefsKey = "Save_TutorialProgress";

    // Keep a version field so future schema updates can migrate safely.
    public int version = 1;

    // Expandable step pointer:
    // 0 = first tutorial step not completed yet.
    // 1..5 = corresponding step completed.
    // 6 = first tutorial Nitro Coin collected and Six popup dismissed.
    // 7 = three Nitro Coins collected → Chest spawning unlocked.
    public int currentStepIndex = 0;

    public bool IsFirstStepCompleted => currentStepIndex >= 1;
    public bool IsSecondStepCompleted => currentStepIndex >= 2;
    public bool IsThirdStepCompleted => currentStepIndex >= 3;
    public bool IsFourthStepCompleted => currentStepIndex >= 4;
    public bool IsFifthStepCompleted => currentStepIndex >= 5;
    public bool IsSixthStepCompleted => currentStepIndex >= 6;
    public bool IsSeventhStepCompleted => currentStepIndex >= 7;

    // ── Nitro/Chest unlock phase (Step 6/7) ──
    /// <summary>True after the player presses Clicker following Step 5; permits Nitro spawning.</summary>
    public bool nitroUnlocked = false;
    /// <summary>True after 3 Nitro Coins have been collected; permits Chest spawning.</summary>
    public bool chestUnlocked = false;
    /// <summary>Reserved for a future tutorial step that unlocks Radar spawning.</summary>
    public bool radarUnlocked = false;
    /// <summary>True while police chase is fully suppressed by tutorial gating. Default true on fresh save.</summary>
    public bool policeLocked = true;
    /// <summary>True once the very first tutorial-tagged Nitro Coin has been force-spawned.</summary>
    public bool firstTutorialNitroSpawned = false;
    /// <summary>True once the very first tutorial-tagged Nitro Coin has been collected.</summary>
    public bool firstTutorialNitroCollected = false;
    /// <summary>Total Nitro Coins collected during the unlock phase (0..3 cap).</summary>
    public int tutorialNitroCount = 0;

    // ── Chest tutorial phase (Step 7/8) ──
    /// <summary>True once the deterministic tutorial Common Chest has been tapped + collected into ChestShown.</summary>
    public bool tutorialChestCollected = false;
    /// <summary>True once the ChestShown ChestSlotPrefab tutorial pointer (Seven) has been completed.</summary>
    public bool chestSlotTutorialShown = false;
    /// <summary>Number of tutorial/free Common Chests fully opened (0..3 cap). While &lt; 3 the spawner forces Common only.</summary>
    public int tutorialFreeChestOpenedCount = 0;
    /// <summary>True once the very first tutorial chest popup has been shown. Used to disable outside-tap close on that one popup only.</summary>
    public bool firstTutorialPopupShown = false;

    /// <summary>
    /// Set TRUE the moment the FIRST tutorial/free chest's <see cref="tutorialFreeChestOpenedCount"/>
    /// goes 0 → 1 (inside <see cref="ChestInventoryManager"/> while still in ChestOpenScene). On the
    /// next Main scene load this is the single, authoritative trigger for the post-first-chest
    /// Shop&amp;Cards pointer (UI_Tutorial/Three_New). Cleared once the player has clicked
    /// Shop&amp;Cards (i.e. <see cref="shopCardsClickedAfterFirstChest"/> becomes true).
    /// </summary>
    public bool postFirstChestShopTutorialPending = false;

    // ── Post-first-free-chest Cards tutorial segment (Steps 9..13) ──
    /// <summary>Persisted CardType (cast to int) of the card rewarded by the very first tutorial/free chest. -1 = not yet captured.</summary>
    public int firstFreeChestCardType = -1;
    /// <summary>True once the second-show of UI_Tutorial/Three (post-first-chest) has begun.</summary>
    public bool cardsTutorialStarted = false;
    /// <summary>True once Shop&Cards has been tapped during the post-first-chest segment (Step 9 done).</summary>
    public bool shopCardsClickedAfterFirstChest = false;
    /// <summary>True once Btn_TabCards has been tapped during the post-first-chest segment (Step 10 done).</summary>
    public bool cardsTabClicked = false;
    /// <summary>True once UI_Tutorial/Ten has been completed (CardDetailedPopup opened) (Step 11 done).</summary>
    public bool tenCompleted = false;
    /// <summary>True once Clicker has been pressed after Ten completed, resuming the suspended free-chest pipeline (Step 12 done).</summary>
    public bool cardsSegmentClickerPressed = false;

    public static TutorialSaveData Load()
    {
        if (!PlayerPrefs.HasKey(PrefsKey))
            return new TutorialSaveData();

        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new TutorialSaveData();

        try
        {
            TutorialSaveData data = JsonUtility.FromJson<TutorialSaveData>(json);
            if (data == null) return new TutorialSaveData();
            return data;
        }
        catch (Exception)
        {
            // Corrupt tutorial data should not block gameplay.
            return new TutorialSaveData();
        }
    }

    public void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
        PlayerPrefs.Save();
    }

    public static void ResetProgress()
    {
        if (PlayerPrefs.HasKey(PrefsKey))
            PlayerPrefs.DeleteKey(PrefsKey);
    }
}
