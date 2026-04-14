using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// UI binding for a single mission row inside the Blacklist panel.
/// Attach to the MissionRowUI prefab root.
/// </summary>
public class MissionRowUI : MonoBehaviour
{
    [Header("Icon & Description")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text missionDesc;

    [Header("In-Progress State")]
    [SerializeField] private GameObject barBG;
    [SerializeField] private Image barFill;
    [SerializeField] private TMP_Text progressText;

    [Header("Completed State")]
    [SerializeField] private GameObject barBGComplete;
    [SerializeField] private Image barFillFull;
    [SerializeField] private TMP_Text barBGCompleteText;
    [SerializeField] private GameObject completedBtn;
    [SerializeField] private TMP_Text completedBtnText;

    // ─── Runtime state ───
    private int _missionIndex;
    private BlacklistMissionType _missionType;
    private bool _wasComplete;
    private bool _isClaimed;
    private Tween _fillTween;

    /// <summary>Fired when the player clicks the completedBtn to claim the reward.</summary>
    public event Action<int> OnClaimClicked;

    // ─── Setup ───

    /// <summary>
    /// Bind this row to a mission definition. Called once when the tier is loaded.
    /// </summary>
    public void Setup(BlacklistMissionDefinition definition, int missionIndex)
    {
        _missionIndex = missionIndex;
        _missionType = definition.missionType;
        _wasComplete = false;
        _isClaimed = false;

        // Icon
        if (icon != null)
        {
            if (definition.icon != null)
            {
                icon.sprite = definition.icon;
                icon.enabled = true;
            }
            else
            {
                icon.enabled = false;
            }
        }

        // Description
        if (missionDesc != null)
            missionDesc.text = definition.description;

        // Wire completedBtn click → claim
        var btn = completedBtn != null ? completedBtn.GetComponent<Button>() : null;
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClaimClicked?.Invoke(_missionIndex));
        }

        // Start in pending visual state
        SetVisualState(isComplete: false);
        UpdateProgress(0, definition.targetValue);
    }

    // ─── Progress update ───

    /// <summary>
    /// Update progress bar and text. Called periodically by the panel controller.
    /// </summary>
    public void UpdateProgress(double current, double target)
    {
        bool isComplete = current >= target;

        if (isComplete && !_wasComplete)
        {
            // Transition to completed visual
            _wasComplete = true;
            AnimateCompletion(current, target);
            return;
        }

        if (isComplete)
        {
            // Already in completed state
            return;
        }

        // In-progress update
        SetVisualState(isComplete: false);

        float ratio = target > 0 ? (float)(current / target) : 0f;
        ratio = Mathf.Clamp01(ratio);

        // Smooth fill animation
        if (barFill != null)
        {
            _fillTween?.Kill();
            _fillTween = barFill.DOFillAmount(ratio, 0.35f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        // Progress text
        if (progressText != null)
            progressText.text = $"{FormatForType(current)}/{FormatForType(target)}";
    }

    /// <summary>
    /// Force the row into a specific visual state without animation.
    /// Used when reloading the panel for an already-completed mission.
    /// </summary>
    public void SetCompleteImmediate(double target)
    {
        _wasComplete = true;
        SetVisualState(isComplete: true);

        if (barFillFull != null)
            barFillFull.fillAmount = 1f;

        if (barBGCompleteText != null)
            barBGCompleteText.text = $"{FormatForType(target)}/{FormatForType(target)}";
    }

    /// <summary>
    /// Transitions this row to "claimed" state. Called after the reward popup collect.
    /// </summary>
    public void SetClaimed()
    {
        _isClaimed = true;
        SetVisualState(isComplete: true);

        if (completedBtnText != null)
            completedBtnText.text = "CLAIMED";

        // Disable the button
        var btn = completedBtn != null ? completedBtn.GetComponent<Button>() : null;
        if (btn != null)
            btn.interactable = false;

        // Dim the button color
        if (completedBtn != null)
        {
            var img = completedBtn.GetComponent<Image>();
            if (img != null)
                img.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        }
    }

    /// <summary>
    /// Sets claimed state immediately without animation. Used when reloading the panel.
    /// </summary>
    public void SetClaimedImmediate(double target)
    {
        SetCompleteImmediate(target);
        SetClaimed();
    }

    // ─── Visual state ───

    private void SetVisualState(bool isComplete)
    {
        if (barBG != null) barBG.SetActive(!isComplete);
        if (progressText != null) progressText.gameObject.SetActive(!isComplete);
        if (barBGComplete != null) barBGComplete.SetActive(isComplete);
        if (completedBtn != null) completedBtn.SetActive(isComplete);
    }

    // ─── Animations ───

    private void AnimateCompletion(double current, double target)
    {
        // First fill bar to 100%
        if (barFill != null)
        {
            _fillTween?.Kill();
            _fillTween = barFill.DOFillAmount(1f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(true)
                .OnComplete(() =>
                {
                    // Switch to completed visual
                    SetVisualState(isComplete: true);

                    if (barFillFull != null)
                        barFillFull.fillAmount = 1f;

                    if (barBGCompleteText != null)
                        barBGCompleteText.text = $"{FormatForType(target)}/{FormatForType(target)}";

                    // Animate completed button appearance
                    if (completedBtn != null)
                    {
                        completedBtn.transform.localScale = Vector3.zero;
                        completedBtn.transform.DOScale(1f, 0.35f)
                            .SetEase(Ease.OutBack, 1.2f)
                            .SetUpdate(true);
                    }

                    // Punch the completed bar
                    if (barBGComplete != null)
                    {
                        barBGComplete.transform.DOPunchScale(Vector3.one * 0.08f, 0.3f, 6, 0.5f)
                            .SetUpdate(true);
                    }
                });
        }
        else
        {
            SetVisualState(isComplete: true);
        }
    }

    // ─── Number formatting ───

    /// <summary>Formats the value according to this row's mission type.</summary>
    private string FormatForType(double value)
    {
        if (_missionType == BlacklistMissionType.EarnGold)
            return FormatMoney(value);
        return ((int)value).ToString();
    }

    private static string FormatMoney(double value)
    {
        double abs = System.Math.Abs(value);

        if (abs >= 1_000_000_000_000)
            return (value / 1_000_000_000_000.0).ToString("0.##") + "T";
        if (abs >= 1_000_000_000)
            return (value / 1_000_000_000.0).ToString("0.##") + "B";
        if (abs >= 1_000_000)
            return (value / 1_000_000.0).ToString("0.##") + "M";

        return ((long)value).ToString("N0");
    }

    // ─── Cleanup ───

    private void OnDestroy()
    {
        _fillTween?.Kill();
    }
}
