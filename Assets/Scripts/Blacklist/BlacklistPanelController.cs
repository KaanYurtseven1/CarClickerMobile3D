using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Controls the Panel_BlackList UI. Reads data from <see cref="BlacklistManager"/>
/// and updates header, car image, mission rows, and the TakeTheCarButton.
///
/// Attach to the Panel_BlackList GameObject in the scene.
/// </summary>
public class BlacklistPanelController : MonoBehaviour
{
    // ─── Header ───
    [Header("Header")]
    [SerializeField] private TMP_Text blacklistTitle;

    // ─── Car area ───
    [Header("Car Image Area")]
    [SerializeField] private Image carImage;
    [SerializeField] private TMP_Text carName;

    // ─── Missions ───
    [Header("Missions")]
    [SerializeField] private Transform missionsContainer;
    [SerializeField] private GameObject missionRowPrefab;

    // ─── Take The Car button ───
    [Header("Take The Car")]
    [SerializeField] private Button takeTheCarButton;
    [SerializeField] private CanvasGroup takeTheCarCanvasGroup;
    [SerializeField] private Image takeTheCarImage;

    // ─── Animation settings ───
    [Header("Animation")]
    [SerializeField] private float refreshInterval = 0.5f;
    [SerializeField] private float transitionFadeDuration = 0.25f;

    [Header("Reward Popup")]
    [SerializeField] private RewardPopupController rewardPopup;

    // ─── Runtime ───
    private MissionRowUI[] _rows;
    private CanvasGroup _panelCanvasGroup;
    private Tween _pulseTween;
    private float _nextRefreshTime;
    private bool _allComplete;

    /// <summary>
    /// Returns the first active BlacklistPanelController in the scene.
    /// Used by RewardPopupController to close the panel after collecting.
    /// </summary>
    public static BlacklistPanelController FindInstance()
    {
        return FindFirstObjectByType<BlacklistPanelController>();
    }

    // ─── Lifecycle ───

    private void Awake()
    {
        _panelCanvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (BlacklistManager.Instance != null)
        {
            BlacklistManager.Instance.OnTierChanged += OnTierChanged;
            BlacklistManager.Instance.OnProgressChanged += OnProgressChanged;
        }

        BuildUI();
        _nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    private void OnDisable()
    {
        if (BlacklistManager.Instance != null)
        {
            BlacklistManager.Instance.OnTierChanged -= OnTierChanged;
            BlacklistManager.Instance.OnProgressChanged -= OnProgressChanged;
        }

        _pulseTween?.Kill();
        _pulseTween = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime) return;
        _nextRefreshTime = Time.unscaledTime + refreshInterval;

        RefreshProgress();
    }

    // ─── Event handlers ───

    private void OnTierChanged()
    {
        AnimateTierTransition();
    }

    private void OnProgressChanged()
    {
        RefreshProgress();
    }

    // ─── UI construction ───

    private void BuildUI()
    {
        ClearMissionRows();

        var mgr = BlacklistManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[BlacklistPanel] BuildUI — BlacklistManager.Instance is null. Rows NOT created.");
            return;
        }

        if (mgr.ActiveTier == null)
        {
            Debug.Log("[BlacklistPanel] BuildUI — ActiveTier is null (campaign complete?).");
            SetCampaignCompleteUI();
            return;
        }

        var tier = mgr.ActiveTier;

        // Header
        if (blacklistTitle != null)
            blacklistTitle.text = tier.tierDisplayName;

        // Car area
        UpdateCarArea(tier);

        // Spawn mission rows
        if (missionRowPrefab == null)
        {
            Debug.LogError("[BlacklistPanel] missionRowPrefab is null! Assign it in Inspector.");
            return;
        }
        if (missionsContainer == null)
        {
            Debug.LogError("[BlacklistPanel] missionsContainer is null! Assign it in Inspector.");
            return;
        }

        int count = tier.missions != null ? tier.missions.Length : 0;
        if (count == 0)
        {
            Debug.LogWarning("[BlacklistPanel] Tier has 0 missions. No rows to create.");
            return;
        }

        _rows = new MissionRowUI[count];
        for (int i = 0; i < count; i++)
        {
            if (tier.missions[i] == null)
            {
                Debug.LogWarning($"[BlacklistPanel] missions[{i}] is null — skipping.");
                continue;
            }

            var rowGO = Instantiate(missionRowPrefab, missionsContainer);
            rowGO.SetActive(true);

            var rowUI = rowGO.GetComponent<MissionRowUI>();
            if (rowUI == null)
            {
                Debug.LogError($"[BlacklistPanel] MissionRowUI component missing on prefab instance (row {i}).");
                _rows[i] = null;
                continue;
            }

            rowUI.Setup(tier.missions[i], i);

            // Subscribe to claim click
            rowUI.OnClaimClicked += OnMissionClaimClicked;

            // If already completed on load, set immediately
            int state = mgr.GetMissionState(i);
            if (state >= BlacklistSaveData.STATE_CLAIMED)
            {
                rowUI.SetClaimedImmediate(tier.missions[i].targetValue);
            }
            else if (state >= BlacklistSaveData.STATE_COMPLETED)
            {
                rowUI.SetCompleteImmediate(tier.missions[i].targetValue);
            }

            _rows[i] = rowUI;
        }

        // Force the layout to rebuild immediately so the VLG
        // positions the new rows before the first render frame.
        if (missionsContainer is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        Debug.Log($"[BlacklistPanel] BuildUI complete — {count} rows created under '{missionsContainer.name}'.");

        // Button state
        RefreshTakeTheCarButton();
    }

    private void ClearMissionRows()
    {
        if (_rows != null)
        {
            foreach (var row in _rows)
            {
                if (row != null)
                {
                    row.transform.SetParent(null);
                    Destroy(row.gameObject);
                }
            }
            _rows = null;
        }

        // Also destroy any leftover children in the container.
        // SetParent(null) removes them from the VLG immediately so
        // the layout recalculates without zombie children.
        if (missionsContainer != null)
        {
            for (int i = missionsContainer.childCount - 1; i >= 0; i--)
            {
                var child = missionsContainer.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
        }
    }

    private void UpdateCarArea(BlacklistTierSO tier)
    {
        if (carName != null)
            carName.text = tier.carName ?? "";

        if (carImage != null)
        {
            if (tier.carImage != null)
            {
                carImage.sprite = tier.carImage;
                carImage.enabled = true;
            }
            else
            {
                // Graceful handling: keep image component but hide if no sprite
                carImage.enabled = false;
            }
        }
    }

    // ─── Progress refresh ───

    private void RefreshProgress()
    {
        var mgr = BlacklistManager.Instance;
        if (mgr == null || mgr.ActiveTier == null || _rows == null)
            return;

        // Evaluate all missions (may auto-promote to completed)
        mgr.EvaluateAll();

        var tier = mgr.ActiveTier;
        for (int i = 0; i < _rows.Length && i < tier.missions.Length; i++)
        {
            if (_rows[i] == null) continue;

            int state = mgr.GetMissionState(i);

            // If claimed, update visual
            if (state >= BlacklistSaveData.STATE_CLAIMED)
            {
                _rows[i].SetClaimedImmediate(tier.missions[i].targetValue);
                continue;
            }

            double progress = mgr.GetMissionProgress(i);
            double target = tier.missions[i].targetValue;

            // If state is already COMPLETED (e.g. via debug tool) but real
            // stats haven't caught up, use target so the row transitions.
            if (state >= BlacklistSaveData.STATE_COMPLETED && progress < target)
                progress = target;

            _rows[i].UpdateProgress(progress, target);
        }

        RefreshTakeTheCarButton();
    }

    // ─── TakeTheCarButton ───

    private void RefreshTakeTheCarButton()
    {
        var mgr = BlacklistManager.Instance;
        if (mgr == null) return;

        bool allDone = mgr.CampaignComplete || mgr.AreAllMissionsComplete();

        if (allDone && !_allComplete)
        {
            _allComplete = true;
            EnableTakeTheCarButton();
        }
        else if (!allDone && _allComplete)
        {
            _allComplete = false;
            DisableTakeTheCarButton();
        }
        else if (!allDone && !_allComplete)
        {
            DisableTakeTheCarButton();
        }
    }

    private void EnableTakeTheCarButton()
    {
        if (takeTheCarButton != null)
        {
            takeTheCarButton.interactable = true;
            takeTheCarButton.onClick.RemoveAllListeners();

            // If campaign already complete, button does nothing
            if (BlacklistManager.Instance != null && BlacklistManager.Instance.CampaignComplete)
            {
                // Button stays active visually but is informational
                takeTheCarButton.onClick.AddListener(OnCampaignAlreadyComplete);
            }
            else
            {
                takeTheCarButton.onClick.AddListener(OnTakeTheCarPressed);
            }
        }

        if (takeTheCarCanvasGroup != null)
        {
            takeTheCarCanvasGroup.alpha = 1f;
            takeTheCarCanvasGroup.interactable = true;
        }

        // Pulse animation
        _pulseTween?.Kill();
        if (takeTheCarButton != null)
        {
            takeTheCarButton.transform.localScale = Vector3.one;
            _pulseTween = takeTheCarButton.transform
                .DOScale(1.06f, 0.6f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(takeTheCarButton.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private void DisableTakeTheCarButton()
    {
        if (takeTheCarButton != null)
        {
            takeTheCarButton.interactable = false;
            takeTheCarButton.onClick.RemoveAllListeners();
        }

        if (takeTheCarCanvasGroup != null)
        {
            takeTheCarCanvasGroup.alpha = 0.4f;
            takeTheCarCanvasGroup.interactable = false;
        }

        _pulseTween?.Kill();
        _pulseTween = null;
        if (takeTheCarButton != null)
            takeTheCarButton.transform.localScale = Vector3.one;
    }

    // ─── Button callbacks ───

    private void OnTakeTheCarPressed()
    {
        var mgr = BlacklistManager.Instance;
        if (mgr == null || !mgr.AreAllMissionsComplete()) return;

        var tier = mgr.ActiveTier;
        if (tier != null && tier.rewardCar != null)
        {
            // Store which car to showcase (read BEFORE advancing tier).
            ShowcaseCarSpawner.PendingCarId = tier.rewardCar.carId;

            if (SaveSystem.Instance != null)
                SaveSystem.Instance.SaveGame();

            SceneManager.LoadScene("TakeTheCarScene");
        }
        else
        {
            // No cinematic configured for this tier — advance immediately.
            mgr.AdvanceToNextTier();
        }
    }

    private void OnCampaignAlreadyComplete()
    {
        // Campaign is finished — button remains visible but informational.
        Debug.Log("[BlacklistPanel] Campaign is already complete. All blacklist cars unlocked.");
    }

    // ─── Claim reward handler ───

    private void OnMissionClaimClicked(int missionIndex)
    {
        var mgr = BlacklistManager.Instance;
        if (mgr == null || mgr.ActiveTier == null) return;

        // Only allow claiming completed-but-not-yet-claimed missions
        int state = mgr.GetMissionState(missionIndex);
        if (state != BlacklistSaveData.STATE_COMPLETED) return;

        // Get the reward definition
        var tier = mgr.ActiveTier;
        if (missionIndex < 0 || missionIndex >= tier.missions.Length) return;

        var rewardDef = tier.missions[missionIndex].reward;
        if (rewardDef == null)
        {
            // No reward configured — just mark as claimed with no popup
            MarkClaimedDirectly(missionIndex);
            return;
        }

        // Open reward popup
        if (rewardPopup == null)
            rewardPopup = RewardPopupController.Instance;

        if (rewardPopup != null)
        {
            rewardPopup.Show(missionIndex, rewardDef);
        }
        else
        {
            // Fallback: no popup available — claim directly
            Debug.LogWarning("[BlacklistPanel] RewardPopupController not found. Claiming directly.");
            MarkClaimedDirectly(missionIndex);
        }
    }

    private void MarkClaimedDirectly(int missionIndex)
    {
        var mgr = BlacklistManager.Instance;
        if (mgr == null || mgr.SaveData == null) return;

        if (missionIndex >= 0 && missionIndex < mgr.SaveData.missionStates.Length)
        {
            mgr.SaveData.missionStates[missionIndex] = BlacklistSaveData.STATE_CLAIMED;
            mgr.Save();
        }

        // Update visual
        if (_rows != null && missionIndex < _rows.Length && _rows[missionIndex] != null)
        {
            _rows[missionIndex].SetClaimed();
        }
    }

    // ─── Tier transition animation ───

    private void AnimateTierTransition()
    {
        if (_panelCanvasGroup == null)
        {
            // No canvas group — just rebuild immediately
            BuildUI();
            return;
        }

        var seq = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);

        // Fade out
        seq.Append(_panelCanvasGroup.DOFade(0f, transitionFadeDuration).SetEase(Ease.InQuad));

        // Rebuild in the gap
        seq.AppendCallback(() =>
        {
            _allComplete = false;
            BuildUI();
        });

        // Fade in
        seq.Append(_panelCanvasGroup.DOFade(1f, transitionFadeDuration).SetEase(Ease.OutQuad));
    }

    // ─── Campaign complete state ───

    private void SetCampaignCompleteUI()
    {
        var mgr = BlacklistManager.Instance;
        if (mgr == null) return;

        // Show last tier (BL#1) info
        var lastTier = mgr.ActiveTier ?? mgr.GetTierSO(1);
        if (lastTier != null)
        {
            if (blacklistTitle != null)
                blacklistTitle.text = lastTier.tierDisplayName + " — COMPLETE";

            UpdateCarArea(lastTier);

            // Spawn completed mission rows
            if (missionRowPrefab != null && missionsContainer != null)
            {
                _rows = new MissionRowUI[lastTier.missions.Length];
                for (int i = 0; i < lastTier.missions.Length; i++)
                {
                    var rowGO = Instantiate(missionRowPrefab, missionsContainer);
                    rowGO.SetActive(true);
                    var rowUI = rowGO.GetComponent<MissionRowUI>();
                    if (rowUI != null)
                    {
                        rowUI.Setup(lastTier.missions[i], i);
                        rowUI.SetClaimedImmediate(lastTier.missions[i].targetValue);
                    }
                    _rows[i] = rowUI;
                }

                // Force the layout to rebuild immediately
                if (missionsContainer is RectTransform rt)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        // Button is active (visual completion state)
        _allComplete = true;
        EnableTakeTheCarButton();
    }
}
