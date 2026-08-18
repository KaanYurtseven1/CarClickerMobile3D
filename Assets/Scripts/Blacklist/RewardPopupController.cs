using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// Controls the RewardPopup UI. Displays mission rewards and handles
/// the Collect button which grants rewards via real game systems.
///
/// Attach to the RewardPopup GameObject in the scene.
/// Must be a child of the main Canvas so it renders on top.
/// </summary>
public class RewardPopupController : MonoBehaviour
{
    public static RewardPopupController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Popup Root")]
    [SerializeField] private GameObject popupRoot;

    [Header("UI References")]
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text youReceivedText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Button collectButton;

    [Header("Animation")]
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private RectTransform popupPanel; // RewardImage
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float scaleFrom = 0.7f;

    // ─── Runtime state ───
    private int _pendingMissionIndex = -1;
    private BlacklistRewardDefinition _pendingReward;
    private bool _isOpen;
    private bool _isAnimating;

    /// <summary>True when the popup is visible.</summary>
    public bool IsOpen => _isOpen;

    // ─── Lifecycle ───

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (collectButton != null)
            collectButton.onClick.AddListener(OnCollectPressed);

        // Start hidden
        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Public API ───

    /// <summary>
    /// Opens the reward popup for a specific mission.
    /// </summary>
    public void Show(int missionIndex, BlacklistRewardDefinition reward)
    {
        if (_isOpen || _isAnimating || reward == null) return;

        _pendingMissionIndex = missionIndex;
        _pendingReward = reward;
        _isOpen = true;

        // Populate UI
        if (titleText != null)
            titleText.text = "REWARD!";
        if (youReceivedText != null)
            youReceivedText.text = "YOU RECEIVED:";

        if (rewardText != null)
            rewardText.text = !string.IsNullOrEmpty(reward.rewardDisplayText)
                ? reward.rewardDisplayText : "Reward";

        if (rewardIcon != null)
        {
            if (reward.rewardIcon != null)
            {
                rewardIcon.sprite = reward.rewardIcon;
                rewardIcon.enabled = true;
            }
            else
            {
                rewardIcon.enabled = false;
            }
        }

        // Show
        if (popupRoot != null)
            popupRoot.SetActive(true);

        // U7: Reward popup SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayRewardPopupAppear();

        PlayOpenAnimation();
    }

    /// <summary>Closes the popup without granting rewards.</summary>
    public void Close()
    {
        if (!_isOpen || _isAnimating) return;
        PlayCloseAnimation(() =>
        {
            _isOpen = false;
            _pendingMissionIndex = -1;
            _pendingReward = null;
            if (popupRoot != null)
                popupRoot.SetActive(false);
        });
    }

    // ─── Collect button ───

    private void OnCollectPressed()
    {
        if (!_isOpen || _isAnimating) return;
        if (_pendingReward == null || _pendingMissionIndex < 0) return;

        // 1) Mark mission as Claimed
        MarkMissionClaimed(_pendingMissionIndex);

        // 2) Grant instant rewards
        var reward = _pendingReward;
        GrantInstantRewards(reward);

        // 3) Close popup and route
        var capturedReward = reward;
        int capturedIndex = _pendingMissionIndex;

        PlayCloseAnimation(() =>
        {
            _isOpen = false;
            _pendingMissionIndex = -1;
            _pendingReward = null;
            if (popupRoot != null)
                popupRoot.SetActive(false);

            // 4) Route player based on reward type
            RouteAfterCollect(capturedReward);
        });
    }

    // ─── Mark claimed ───

    private void MarkMissionClaimed(int missionIndex)
    {
        var mgr = BlacklistManager.Instance;
        if (mgr == null || mgr.SaveData == null) return;

        if (missionIndex >= 0 && missionIndex < mgr.SaveData.missionStates.Length)
        {
            mgr.SaveData.missionStates[missionIndex] = BlacklistSaveData.STATE_CLAIMED;
            mgr.Save();
        }
    }

    // ─── Instant reward grants ───

    private void GrantInstantRewards(BlacklistRewardDefinition reward)
    {
        // Gold
        if (reward.goldAmount > 0 && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.money += reward.goldAmount;
            CurrencyManager.Instance.totalMoneyEarned += reward.goldAmount;
            Debug.Log($"[RewardPopup] Granted {reward.goldAmount} gold.");
        }

        // Nitro
        if (reward.nitroAmount > 0 && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddNitroCoins(reward.nitroAmount);
            Debug.Log($"[RewardPopup] Granted {reward.nitroAmount} nitro.");
        }

        // Popularity reset
        if (reward.popularityReset && PopularityManager.Instance != null)
        {
            PopularityManager.Instance.Reset();
            Debug.Log("[RewardPopup] Popularity reset to 0.");
        }

        // Heat reset
        if (reward.heatReset && AmbientHeatManager.Instance != null)
        {
            AmbientHeatManager.Instance.ResetHeat();
            Debug.Log("[RewardPopup] Heat reset.");
        }

        // Boost cooldown discount
        if (reward.boostDiscountUses > 0)
        {
            var claimData = GetOrLoadClaimData();
            claimData.AddBoostDiscount(reward.boostDiscountUses, reward.boostDiscountMultiplier);
            Debug.Log($"[RewardPopup] Boost discount +{reward.boostDiscountUses} uses at {reward.boostDiscountMultiplier}x.");
        }

        // Endgame cosmetics
        if (reward.unlockAllCosmeticsForOtherCars)
        {
            GrantEndgameCosmetics();
        }

        // Store deferred rewards
        if (reward.freeChestCount > 0)
        {
            var claimData = GetOrLoadClaimData();
            claimData.pendingFreeChests += reward.freeChestCount;
            claimData.SaveToPrefs();
            Debug.Log($"[RewardPopup] Queued {reward.freeChestCount} free chests.");
        }

        if (reward.cardProgressAmount > 0)
        {
            var claimData = GetOrLoadClaimData();
            claimData.pendingCardProgressAmount += reward.cardProgressAmount;
            claimData.SaveToPrefs();
            Debug.Log($"[RewardPopup] Queued +{reward.cardProgressAmount} card progress.");
        }

        if (reward.freeKaplamaCount > 0)
        {
            var claimData = GetOrLoadClaimData();
            claimData.pendingFreeKaplamaCount += reward.freeKaplamaCount;
            claimData.SaveToPrefs();
            Debug.Log($"[RewardPopup] Queued {reward.freeKaplamaCount} free kaplama(s).");
        }

        // Save game
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();
    }

    // ─── Routing ───

    private void RouteAfterCollect(BlacklistRewardDefinition reward)
    {
        // Priority: chest > card > kaplama > default (clicker)

        // Close Panel_BlackList first
        CloseBlacklistPanel();

        if (reward.freeChestCount > 0)
        {
            // Route to chest — FreeChestRewardHandler will trigger the opening
            if (FreeChestRewardHandler.Instance != null)
            {
                FreeChestRewardHandler.Instance.TriggerNextFreeChest();
            }
            else
            {
                Debug.LogWarning("[RewardPopup] FreeChestRewardHandler not found, falling back to Clicker.");
                SwitchToClicker();
                AnimateGoldIncrease(reward);
            }
            return;
        }

        if (reward.cardProgressAmount > 0)
        {
            // Route to ShopCards — pending progress will be consumed on card click
            SwitchToShopCards();
            return;
        }

        if (reward.freeKaplamaCount > 0)
        {
            // Show kaplama picker
            if (KaplamaPickerController.Instance != null)
            {
                SwitchToClicker();
                KaplamaPickerController.Instance.Show();
            }
            else
            {
                Debug.LogWarning("[RewardPopup] KaplamaPickerController not found.");
                SwitchToClicker();
            }
            return;
        }

        // Default: go to Clicker with visual feedback
        SwitchToClicker();
        AnimateGoldIncrease(reward);
        AnimateNitroIncrease(reward);
    }

    // ─── Panel navigation helpers ───

    /// <summary>
    /// Closes Panel_BlackList and fully restores normal clicker gameplay state.
    /// Panel_BlackList lives outside PanelTransitionManager's BottomTab system,
    /// so SwitchTo(Clicker) is a no-op when Clicker is already the "current tab".
    /// We must manually restore TopBar, clickerRoot, and UIFlowState.
    /// </summary>
    private void CloseBlacklistPanel()
    {
        // 1) Deactivate Panel_BlackList
        var blPanel = BlacklistPanelController.FindInstance();
        if (blPanel != null)
        {
            blPanel.gameObject.SetActive(false);
        }

        // 2) Restore TopBar to normal (expanded) state
        if (TopBarAnimator.Instance != null)
        {
            TopBarAnimator.Instance.SetCompact(false);
        }

        // 3) Ensure UIFlowState suppression is off (re-enable spawns & taps)
        UIFlowState.IsContentPanelOpen = false;
    }

    private void SwitchToClicker()
    {
        // Route through BottomBarController to update both tab visuals and panel state.
        // SwitchTo(Clicker) is typically a no-op since Panel_BlackList is outside the tab
        // system, but SetActiveTab also resets the bottom bar highlight to Clicker.
        if (BottomBarController.Instance != null)
            BottomBarController.Instance.SetActiveTab((int)BottomTab.Clicker);
        else if (PanelTransitionManager.Instance != null)
            PanelTransitionManager.Instance.SwitchTo(BottomTab.Clicker);
    }

    private void SwitchToShopCards()
    {
        // Route through BottomBarController so the ShopAndCards tab highlights correctly.
        if (BottomBarController.Instance != null)
            BottomBarController.Instance.SetActiveTab((int)BottomTab.ShopAndCards);
        else if (PanelTransitionManager.Instance != null)
            PanelTransitionManager.Instance.SwitchTo(BottomTab.ShopAndCards);

        // Switch to the Cards scroll view (not the default ShopItems view)
        if (ShopCardsTabs.Instance != null)
            ShopCardsTabs.Instance.ShowCards();
    }

    // ─── Gold/Nitro visual feedback ───

    private void AnimateGoldIncrease(BlacklistRewardDefinition reward)
    {
        if (reward.goldAmount > 0 && CurrencyUI.Instance != null && CurrencyManager.Instance != null)
        {
            double after = CurrencyManager.Instance.money;
            double before = after - reward.goldAmount;
            CurrencyUI.Instance.PlaySuccessRewardAnimation(before, after, 0.6f, () =>
            {
                // After reward animation, apply buffered earnings if any
                if (CurrencyUI.Instance != null && CurrencyManager.Instance != null)
                {
                    double buffered = CurrencyManager.Instance.bufferedEarnings;
                    CurrencyUI.Instance.PlayBufferedEarningsApplyAnimation(after, buffered, () =>
                    {
                        if (CurrencyManager.Instance != null)
                            CurrencyManager.Instance.CommitBufferedEarnings();
                    });
                }
            });
        }
    }

    private void AnimateNitroIncrease(BlacklistRewardDefinition reward)
    {
        if (reward.nitroAmount > 0 && CurrencyUI.Instance != null && CurrencyManager.Instance != null)
        {
            int after = CurrencyManager.Instance.nitroCoins;
            int before = after - reward.nitroAmount;
            CurrencyUI.Instance.PlayNitroRewardAnimation(before, after, 0.6f, null);
        }
    }

    // ─── Endgame cosmetics ───

    private void GrantEndgameCosmetics()
    {
        if (GarageSaveData.Instance == null) return;

        // Get all car IDs from BlacklistStatTracker
        string[] carIds = BlacklistStatTracker.GetAllCarIds();
        if (carIds == null || carIds.Length == 0) return;

        // Exclude the last car (rightmost / final blacklist reward car)
        string lastCarId = carIds[carIds.Length - 1];

        foreach (string carId in carIds)
        {
            if (carId == lastCarId) continue;

            // Unlock all 6 colors and all 6 stickers
            for (int i = 0; i < 6; i++)
            {
                GarageSaveData.Instance.MarkColorOwned(carId, i);
                GarageSaveData.Instance.MarkStickerOwned(carId, i);
            }
        }

        GarageSaveData.Instance.SaveToPrefs();
        Debug.Log($"[RewardPopup] Endgame cosmetics unlocked for all cars except '{lastCarId}'.");
    }

    // ─── Claim data helper ───

    private BlacklistRewardClaimData _claimDataCache;

    private BlacklistRewardClaimData GetOrLoadClaimData()
    {
        if (_claimDataCache == null)
            _claimDataCache = BlacklistRewardClaimData.LoadFromPrefs();
        return _claimDataCache;
    }

    // ─── Animations ───

    private void PlayOpenAnimation()
    {
        _isAnimating = true;

        if (popupCanvasGroup != null)
            popupCanvasGroup.alpha = 0f;

        if (popupPanel != null)
            popupPanel.localScale = Vector3.one * scaleFrom;

        var seq = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);

        if (popupCanvasGroup != null)
            seq.Join(popupCanvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad));

        if (popupPanel != null)
            seq.Join(popupPanel.DOScale(1f, fadeDuration).SetEase(Ease.OutBack, 1.1f));

        // Reward icon pop
        if (rewardIcon != null && rewardIcon.enabled)
        {
            rewardIcon.transform.localScale = Vector3.zero;
            seq.Append(rewardIcon.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 1.3f));
        }

        // Collect button bounce
        if (collectButton != null)
        {
            collectButton.transform.localScale = Vector3.one * 0.8f;
            seq.Append(collectButton.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
        }

        seq.OnComplete(() => _isAnimating = false);
    }

    private void PlayCloseAnimation(Action onComplete)
    {
        _isAnimating = true;

        var seq = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);

        if (popupCanvasGroup != null)
            seq.Join(popupCanvasGroup.DOFade(0f, fadeDuration * 0.75f).SetEase(Ease.InQuad));

        if (popupPanel != null)
            seq.Join(popupPanel.DOScale(scaleFrom, fadeDuration * 0.75f).SetEase(Ease.InBack, 1.1f));

        seq.OnComplete(() =>
        {
            _isAnimating = false;
            onComplete?.Invoke();
        });
    }
}
