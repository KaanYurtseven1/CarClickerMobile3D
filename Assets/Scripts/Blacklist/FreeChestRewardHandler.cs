using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DontDestroyOnLoad singleton that handles free chest chains
/// from Blacklist rewards. When pending free chests > 0, listens
/// for OnChestCompleted, waits for the Main scene to reload,
/// and triggers the next chest opening.
///
/// SETUP: Create an empty GameObject in the Main scene, attach this script.
/// It self-promotes to DontDestroyOnLoad on Awake.
/// </summary>
public class FreeChestRewardHandler : MonoBehaviour
{
    public static FreeChestRewardHandler Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    private bool _waitingForMainScene;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        ChestSessionManager.OnChestCompleted += OnChestCompleted;
    }

    private void OnDestroy()
    {
        ChestSessionManager.OnChestCompleted -= OnChestCompleted;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Called by RewardPopupController to trigger the first free chest.
    /// </summary>
    public void TriggerNextFreeChest()
    {
        var claimData = BlacklistRewardClaimData.LoadFromPrefs();
        if (!claimData.HasPendingFreeChests)
        {
            Debug.Log("[FreeChestHandler] No pending free chests.");
            return;
        }

        StartChestSession(claimData);
    }

    // ─── Chain logic ───

    private void OnChestCompleted()
    {
        var claimData = BlacklistRewardClaimData.LoadFromPrefs();
        if (!claimData.HasPendingFreeChests)
        {
            Debug.Log("[FreeChestHandler] Chain complete, no more pending chests.");
            return;
        }

        // Main scene reload happens right after OnChestCompleted.
        // Wait for it to load before opening next chest.
        _waitingForMainScene = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_waitingForMainScene) return;

        if (scene.name == "Main")
        {
            _waitingForMainScene = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // Wait a frame for Main to initialize, then trigger next chest
            StartCoroutine(TriggerNextChestDelayed());
        }
    }

    private IEnumerator TriggerNextChestDelayed()
    {
        // Wait 2 frames to let Main scene fully initialize
        yield return null;
        yield return null;

        var claimData = BlacklistRewardClaimData.LoadFromPrefs();
        if (claimData.HasPendingFreeChests)
        {
            StartChestSession(claimData);
        }
    }

    // ─── Chest creation ───

    private void StartChestSession(BlacklistRewardClaimData claimData)
    {
        // Decrement pending count
        claimData.pendingFreeChests = Mathf.Max(0, claimData.pendingFreeChests - 1);
        claimData.SaveToPrefs();

        Debug.Log($"[FreeChestHandler] Opening free chest. Remaining after this: {claimData.pendingFreeChests}");

        // Create chest data for a free blacklist chest
        var chestData = new ChestInventoryManager.ChestData
        {
            chestType = ChestType.Common,
            chestName = "Blacklist Chest",
            unlockDurationSeconds = 0f,
            state = ChestState.Idle,
            unlockEndUtcTicks = 0,
            halfTimeUsed = false
        };

        // Begin session and load chest scene
        var sessionMgr = ChestSessionManager.Instance;
        if (sessionMgr == null)
        {
            Debug.LogError("[FreeChestHandler] ChestSessionManager not found!");
            return;
        }

        sessionMgr.BeginSession(chestData);
        SceneManager.LoadScene("ChestOpenScene");
    }
}
