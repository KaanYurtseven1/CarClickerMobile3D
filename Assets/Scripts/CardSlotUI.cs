using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardSlotUI : MonoBehaviour
{
    [Header("UI Refs")]
    public Image iconImage;
    public TextMeshProUGUI textName;   // Üstte: "Level 3" veya boş
    public TextMeshProUGUI textLevel;  // Altta: "3/10" veya "0/1"
    public Button button;

    [Header("Progress Bar")]
    [Tooltip("Bar_Fill Image (must be Image Type = Filled)")]
    public Image barFill;

    [Header("Colors")]
    public Color unlockedColor = Color.white;
    public Color lockedColor = new Color(1f, 1f, 1f, 0.35f);

    private CardDefinition card;
    private System.Action<CardDefinition> onClick;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnClickInternal);
    }

    public void Setup(CardDefinition def, System.Action<CardDefinition> onClickCallback)
    {
        card = def;
        onClick = onClickCallback;

        // Ensure button listener is wired (in case Awake didn't run yet or button was null)
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickInternal);
        }

        if (iconImage != null && def.icon != null)
            iconImage.sprite = def.icon;

        Refresh();

        Debug.Log($"[CardSlotUI] Setup complete for: {def.type}, callback assigned: {onClick != null}");
    }

    public void Refresh()
    {
        if (card == null) return;

        bool unlocked = card.IsUnlocked;
        float progress01 = card.GetUpgradeProgress01();
        string progressText = card.GetUpgradeProgressText();
        int need = card.GetCopiesRequiredForNextLevel();

        // Debug log
        Debug.Log($"[CardUI] {card.type} L{card.currentLevel} copiesOwned={card.copiesOwned} need={need} progress={progress01:F2} text={progressText}");

        // ÜST YAZI: Level bilgisi
        if (textName != null)
        {
            if (unlocked)
            {
                if (card.IsMaxLevel)
                    textName.text = $"Level {card.currentLevel} (MAX)";
                else
                    textName.text = $"Level {card.currentLevel}";
            }
            else
            {
                textName.text = "";
            }
        }

        // ALT YAZI: Progress text from helper
        if (textLevel != null)
        {
            textLevel.text = progressText;
        }

        // PROGRESS BAR: Fill amount from helper
        if (barFill != null)
        {
            barFill.fillAmount = progress01;
        }

        // İKON RENGİ: unlocked beyaz, locked hafif gri
        if (iconImage != null)
        {
            iconImage.color = unlocked ? unlockedColor : lockedColor;
        }
    }

    private void OnClickInternal()
    {
        if (card == null) return;
        onClick?.Invoke(card);
    }
}
