using System;
using UnityEngine;

/// <summary>
/// Singleton that manages the active chest-opening session.
/// Survives scene loads via DontDestroyOnLoad.
///
/// Responsibilities:
///   - Holds the runtime active session (primary handoff between scenes)
///   - Persists session to PlayerPrefs as fallback / crash recovery
///   - Commits rewards (applies to player data + saves) when lid opens
///   - Recovers from interrupted sessions on app restart
///
/// Lifecycle:
///   BeginSession()  → called from ChestPopupController when player presses Open
///   CommitRewards()  → called from ChestOpenSceneController when lid opens & rewards computed
///   CompleteSession() → called from ChestOpenSceneController on final tap / exit
///   RecoverIfNeeded() → called from SaveSystem.LoadGame() on every app start / scene reload
/// </summary>
public class ChestSessionManager : MonoBehaviour
{
    public static ChestSessionManager Instance;

    private const string PP_SESSION_KEY = "Save_ChestOpeningSession";

    /// <summary>
    /// Fired when a chest session is fully completed (reveal finished).
    /// Used by BlacklistStatTracker to count lifetime chests opened.
    /// </summary>
    public static event Action OnChestCompleted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnChestCompleted = null;
    }

    /// <summary>The in-memory active session. Null when no chest is being opened.</summary>
    private ChestOpeningSession _activeSession;

    /// <summary>Public read-only access to the active session.</summary>
    public ChestOpeningSession ActiveSession => _activeSession;

    /// <summary>True if there is an active session in memory.</summary>
    public bool HasActiveSession => _activeSession != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log($"[ChestSessionMgr] Duplicate detected ({name}#{GetInstanceID()}), destroying. Keeping {Instance.name}#{Instance.GetInstanceID()}");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad only works on root GameObjects.
        if (transform.parent != null)
        {
            Debug.Log($"[ChestSessionMgr] Detaching from parent '{transform.parent.name}' for DontDestroyOnLoad.");
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
        Debug.Log($"[ChestSessionMgr] Instance assigned to {name}#{GetInstanceID()}, DontDestroyOnLoad applied.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Debug.Log($"[ChestSessionMgr] OnDestroy — clearing Instance ({name}#{GetInstanceID()}).");
            Instance = null;
        }
    }

    /// <summary>
    /// Returns the existing Instance, or creates one on-demand if missing.
    /// Use in cross-scene code where timing cannot be guaranteed.
    /// </summary>
    public static ChestSessionManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        Instance = FindObjectOfType<ChestSessionManager>();
        if (Instance != null)
        {
            Debug.Log($"[ChestSessionMgr] EnsureInstance — found orphaned instance {Instance.name}#{Instance.GetInstanceID()}, re-assigned.");
            return Instance;
        }

        var go = new GameObject("[Auto] ChestSessionManager");
        Instance = go.AddComponent<ChestSessionManager>();
        Debug.Log("[ChestSessionMgr] EnsureInstance — created new instance from scratch.");
        return Instance;
    }

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Restores an active session from a persisted ChestOpeningSession object.
    /// Called by ChestOpenSceneController when it finds a persisted session in
    /// PlayerPrefs but the runtime manager has no in-memory session (e.g. after
    /// the manager was freshly bootstrapped during a scene transition).
    /// </summary>
    public void RestoreSessionFromPersisted(ChestOpeningSession session)
    {
        if (session == null) return;
        if (_activeSession != null)
        {
            Debug.LogWarning($"[ChestSessionMgr] RestoreSessionFromPersisted: already have active session '{_activeSession.sessionId}', ignoring.");
            return;
        }
        _activeSession = session;
        Debug.Log($"[ChestSessionMgr] Session RESTORED from persisted: id={session.sessionId} committed={session.rewardsCommitted}");
    }

    /// <summary>
    /// Creates a new chest-opening session. Called before loading ChestOpenScene.
    /// The chest should already be marked as OpeningInProgress in inventory.
    /// Persists the session immediately for crash safety.
    /// </summary>
    public ChestOpeningSession BeginSession(ChestInventoryManager.ChestData chestData)
    {
        if (_activeSession != null)
        {
            Debug.LogWarning($"[ChestSessionMgr] BeginSession called while session '{_activeSession.sessionId}' is active! Overwriting.");
        }

        _activeSession = ChestOpeningSession.Create(chestData);
        PersistSession();

        Debug.Log($"[ChestSessionMgr] Session STARTED: id={_activeSession.sessionId} chest='{chestData.chestName}'");
        return _activeSession;
    }

    /// <summary>
    /// Commits rewards: applies them to player data, saves the game, and marks
    /// the session as committed. After this call, the reward is safe from loss.
    ///
    /// Call order:
    ///   1. Store reward amounts in session + mark committed
    ///   2. Apply rewards to CurrencyManager / CardManager
    ///   3. Remove OpeningInProgress chest from inventory
    ///   4. Save full game state (includes rewards + chest removal)
    ///   5. Persist session with committed flag
    ///   6. PlayerPrefs.Save() — single atomic flush
    ///
    /// If crash between steps 4 and 6: game state has rewards, session says
    /// committed on next flush. Recovery finds committed session → just cleans up.
    /// If crash before step 4: session not committed → recovery reverts chest.
    /// </summary>
    public void CommitRewards(ChestRewardPackage package)
    {
        if (_activeSession == null)
        {
            Debug.LogError("[ChestSessionMgr] CommitRewards called with no active session!");
            return;
        }
        if (_activeSession.rewardsCommitted)
        {
            Debug.LogWarning("[ChestSessionMgr] CommitRewards called but rewards already committed. Skipping.");
            return;
        }

        // 1) Store reward values in session
        _activeSession.rewardsComputed = true;
        _activeSession.committedMoneyGained = package.moneyGained;
        _activeSession.committedNitroReward = package.nitroReward;
        _activeSession.committedCardType = (int)package.cardType;
        _activeSession.committedCardCopies = package.cardCopies;
        _activeSession.committedHasSticker = package.hasStickerReward;
        _activeSession.committedStickerCarId = package.stickerCarId;
        _activeSession.committedStickerIndex = package.stickerIndex;
        _activeSession.rewardsCommitted = true;

        // ── Tutorial: remember the card type from the very first tutorial/free chest ──
        // UI_Tutorial/Ten points to this exact card after the player returns to Main.
        // Captured here because this is the only site that knows both "chest was
        // tutorial-free" and the committed CardType, before the session is cleared.
        if (_activeSession.chestData != null
            && _activeSession.chestData.isTutorialFreeChest
            && package.cardCopies > 0)
        {
            TutorialSaveData tsd = TutorialSaveData.Load();
            if (tsd != null && tsd.firstFreeChestCardType < 0)
            {
                tsd.firstFreeChestCardType = (int)package.cardType;
                tsd.Save();
                Debug.Log($"[ChestSessionMgr][TutorialFree] Captured firstFreeChestCardType={package.cardType}");
            }
        }

        // 2) Apply rewards to in-memory singletons
        Debug.Log($"[ChestSessionMgr] CommitRewards: singletons alive? SaveSystem={(SaveSystem.Instance != null)} " +
                  $"CurrencyManager={(CurrencyManager.Instance != null)} CardManager={(CardManager.Instance != null)}");
        ApplyRewardsToPlayer();

        // 3) Remove the OpeningInProgress chest from inventory
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.RemoveOpeningChest();

        // 4) Save full game state (economy + cards + chest inventory now reflect rewards)
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.SaveGame();
            Debug.Log("[ChestSessionMgr] SaveGame called successfully after commit.");
        }
        else
        {
            Debug.LogError("[ChestSessionMgr] SaveSystem.Instance is NULL — CANNOT save rewards to PlayerPrefs!");
        }

        // 5) Persist session with committed=true (SaveGame already called PlayerPrefs.Save,
        //    but PersistSession writes our key; we flush again to be safe)
        PersistSession();
        PlayerPrefs.Save();

        Debug.Log($"[ChestSessionMgr] Rewards COMMITTED: money={package.moneyGained:N0} " +
                  $"nitro=+{package.nitroReward} card={package.cardType} x{package.cardCopies}");
    }

    /// <summary>
    /// Marks the session as complete (reveal finished) and cleans up.
    /// Called on final tap / exit from ChestOpenScene.
    /// </summary>
    public void CompleteSession()
    {
        if (_activeSession == null)
        {
            Debug.LogWarning("[ChestSessionMgr] CompleteSession called with no active session.");
            ClearPersistedSession(); // safety
            return;
        }

        Debug.Log($"[ChestSessionMgr] Session COMPLETED: id={_activeSession.sessionId}");

        _activeSession.revealCompleted = true;
        ClearPersistedSession();
        _activeSession = null;

        OnChestCompleted?.Invoke();
    }

    /// <summary>
    /// Recovery logic. Called on every LoadGame() (app start / return to Main).
    /// Detects interrupted sessions and resolves them safely.
    ///
    /// Recovery rules:
    ///   - rewardsCommitted=true  → rewards already saved. Clean up session.
    ///   - rewardsCommitted=false → rewards NOT applied. Revert chest to ReadyToOpen.
    ///   - stale session (>48h)   → force-clean (revert if uncommitted).
    /// </summary>
    public void RecoverIfNeeded()
    {
        // If we have an in-memory session, skip persistence recovery (we're mid-flow)
        if (_activeSession != null)
        {
            Debug.Log($"[ChestSessionMgr] RecoverIfNeeded: active session in memory (id={_activeSession.sessionId}), skipping recovery.");
            return;
        }

        var persisted = LoadPersistedSession();
        if (persisted == null)
            return; // no pending session

        Debug.Log($"[ChestSessionMgr] RecoverIfNeeded: found persisted session id={persisted.sessionId} " +
                  $"committed={persisted.rewardsCommitted} completed={persisted.revealCompleted} " +
                  $"stale={persisted.IsStale()}");

        if (persisted.revealCompleted)
        {
            // Edge case: session was completed but not cleared (crash during cleanup)
            Debug.Log("[ChestSessionMgr] Recovery: session already completed, just clearing.");
            ClearPersistedSession();
            return;
        }

        if (persisted.IsStale())
        {
            Debug.LogWarning("[ChestSessionMgr] Recovery: STALE session detected, force-cleaning.");
            if (!persisted.rewardsCommitted)
            {
                RevertOpeningChest();
            }
            ClearPersistedSession();
            SaveAfterRecovery();
            return;
        }

        if (persisted.rewardsCommitted)
        {
            // Rewards were applied and game was saved. Chest already removed.
            // Just clean up the session marker.
            Debug.Log("[ChestSessionMgr] Recovery: rewards were committed. Cleaning session.");
            ClearPersistedSession();
            // No need to save — game state is already correct from the commit.
        }
        else
        {
            // Rewards were NOT committed. The chest is still in OpeningInProgress.
            // Revert it back to ReadyToOpen so the player can try again.
            Debug.Log("[ChestSessionMgr] Recovery: rewards NOT committed. Reverting chest to ReadyToOpen.");
            RevertOpeningChest();
            ClearPersistedSession();
            SaveAfterRecovery();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  INTERNAL HELPERS
    // ═══════════════════════════════════════════════════════════════

    private void ApplyRewardsToPlayer()
    {
        if (_activeSession == null) return;

        if (CurrencyManager.Instance != null && _activeSession.committedMoneyGained > 0)
        {
            CurrencyManager.Instance.AddMoney(_activeSession.committedMoneyGained);
            Debug.Log($"[ChestSessionMgr] Applied money: +{_activeSession.committedMoneyGained:N0}");
        }
        else if (_activeSession.committedMoneyGained > 0)
        {
            Debug.LogError($"[ChestSessionMgr] CurrencyManager.Instance is NULL — cannot apply money +{_activeSession.committedMoneyGained:N0}!");
        }

        if (CurrencyManager.Instance != null && _activeSession.committedNitroReward > 0)
        {
            CurrencyManager.Instance.AddNitroCoins(_activeSession.committedNitroReward);
            Debug.Log($"[ChestSessionMgr] Applied nitro: +{_activeSession.committedNitroReward}");
        }
        else if (_activeSession.committedNitroReward > 0)
        {
            Debug.LogError($"[ChestSessionMgr] CurrencyManager.Instance is NULL — cannot apply nitro +{_activeSession.committedNitroReward}!");
        }

        if (CardManager.Instance != null && _activeSession.committedCardCopies > 0)
        {
            var cardBefore = CardManager.Instance.GetCard((CardType)_activeSession.committedCardType);
            int lvlBefore = cardBefore != null ? cardBefore.currentLevel : -1;
            int copBefore = cardBefore != null ? cardBefore.copiesOwned : -1;

            CardManager.Instance.AddCardCopies(
                (CardType)_activeSession.committedCardType,
                _activeSession.committedCardCopies);

            int lvlAfter = cardBefore != null ? cardBefore.currentLevel : -1;
            int copAfter = cardBefore != null ? cardBefore.copiesOwned : -1;

            Debug.Log($"[ChestSessionMgr] Applied card: {(CardType)_activeSession.committedCardType} x{_activeSession.committedCardCopies} — " +
                      $"BEFORE: L{lvlBefore} copies={copBefore} — AFTER: L{lvlAfter} copies={copAfter}");
        }
        else if (_activeSession.committedCardCopies > 0)
        {
            Debug.LogError($"[ChestSessionMgr] CardManager.Instance is NULL — cannot apply card {(CardType)_activeSession.committedCardType} x{_activeSession.committedCardCopies}!");
        }

        // Sticker reward
        if (_activeSession.committedHasSticker)
        {
            StickerRewardHelper.GrantSticker(new StickerRewardHelper.StickerReward
            {
                carId = _activeSession.committedStickerCarId,
                stickerIndex = _activeSession.committedStickerIndex
            });
            Debug.Log($"[ChestSessionMgr] Applied sticker: car={_activeSession.committedStickerCarId} idx={_activeSession.committedStickerIndex}");
        }
    }

    private void RevertOpeningChest()
    {
        if (ChestInventoryManager.Instance != null)
        {
            bool reverted = ChestInventoryManager.Instance.RevertOpeningChestToReady();
            Debug.Log($"[ChestSessionMgr] RevertOpeningChest: {(reverted ? "SUCCESS" : "no OpeningInProgress chest found")}");
        }
    }

    private void SaveAfterRecovery()
    {
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.SaveToPrefs();
        PlayerPrefs.Save();
        Debug.Log("[ChestSessionMgr] Saved after recovery.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  PLAYERPREFS PERSISTENCE
    // ═══════════════════════════════════════════════════════════════

    private void PersistSession()
    {
        if (_activeSession == null)
        {
            PlayerPrefs.DeleteKey(PP_SESSION_KEY);
            return;
        }

        string json = JsonUtility.ToJson(_activeSession);
        PlayerPrefs.SetString(PP_SESSION_KEY, json);
        // Note: caller is responsible for PlayerPrefs.Save() at the right time
        Debug.Log($"[ChestSessionMgr] Session persisted: {json.Length} chars");
    }

    private ChestOpeningSession LoadPersistedSession()
    {
        if (!PlayerPrefs.HasKey(PP_SESSION_KEY))
            return null;

        string json = PlayerPrefs.GetString(PP_SESSION_KEY, "");
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonUtility.FromJson<ChestOpeningSession>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ChestSessionMgr] Failed to parse persisted session: {e.Message}");
            PlayerPrefs.DeleteKey(PP_SESSION_KEY);
            return null;
        }
    }

    private void ClearPersistedSession()
    {
        PlayerPrefs.DeleteKey(PP_SESSION_KEY);
        PlayerPrefs.Save();
        Debug.Log("[ChestSessionMgr] Persisted session CLEARED.");
    }
}
