using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component for a single chest slot in the ChestShownPlace list.
/// Instantiated dynamically by ChestShownUI.
/// </summary>
public class ChestSlotUI : MonoBehaviour
{
    [SerializeField] private Image chestIcon;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button slotButton;

    [Header("Type-Specific Icons (assign in prefab)")]
    [SerializeField] private Sprite commonIcon;
    [SerializeField] private Sprite rareIcon;
    [SerializeField] private Sprite legendaryIcon;

    /// <summary>The inventory index this slot represents.</summary>
    public int ChestIndex { get; private set; }

    private System.Action<int> _onTapped;

    public void Initialize(int chestIndex, ChestType chestType, ChestState state,
                           float remainingSeconds, System.Action<int> onTapped)
    {
        ChestIndex = chestIndex;
        _onTapped = onTapped;

        // Set icon by chest type (fallback to commonIcon if type-specific is null)
        if (chestIcon != null)
        {
            Sprite icon = commonIcon;
            switch (chestType)
            {
                case ChestType.Rare: icon = rareIcon != null ? rareIcon : commonIcon; break;
                case ChestType.Legendary: icon = legendaryIcon != null ? legendaryIcon : commonIcon; break;
            }
            if (icon != null) chestIcon.sprite = icon;
        }

        RefreshStatus(state, remainingSeconds);

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() =>
            {
                Debug.Log($"[ChestSlotUI] Slot {ChestIndex} tapped");
                _onTapped?.Invoke(ChestIndex);
            });
        }
        else
        {
            Debug.LogError($"[ChestSlotUI] slotButton is NULL on slot {chestIndex}! Wire the Button in the ChestSlotPrefab.");
        }
    }

    public void RefreshStatus(ChestState state, float remainingSeconds)
    {
        if (statusText == null) return;

        switch (state)
        {
            case ChestState.Idle:
                statusText.text = "New";
                break;
            case ChestState.Unlocking:
                int total = Mathf.CeilToInt(remainingSeconds);
                int h = total / 3600;
                int m = (total % 3600) / 60;
                int s = total % 60;
                statusText.text = h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
                break;
            case ChestState.ReadyToOpen:
                statusText.text = "Ready!";
                break;
            default:
                statusText.text = "";
                break;
        }
    }
}
