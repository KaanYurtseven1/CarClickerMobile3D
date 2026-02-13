using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardDetailPopupController : MonoBehaviour
{
    public static CardDetailPopupController Instance;

    [Header("Root")]
    [SerializeField] private GameObject popupRoot;

    [Header("UI References")]
    [SerializeField] private Image cardIcon;
    [SerializeField] private TextMeshProUGUI textTitle;
    [SerializeField] private TextMeshProUGUI textCardType;
    [SerializeField] private TextMeshProUGUI textValue;
    [SerializeField] private TextMeshProUGUI textDescription;

    [Header("Buttons")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button panelBGButton; // Optional: click background to close

    [Header("Upgrade Button Text")]
    [SerializeField] private TextMeshProUGUI upgradeButtonText;

    [Header("Progress Bar")]
    [Tooltip("Fill Image for copy progress (must be Image Type = Filled)")]
    [SerializeField] private Image progressFill;
    [Tooltip("Optional: Dedicated text for progress (e.g., '2/3'). If null, uses textValue.")]
    [SerializeField] private TextMeshProUGUI textProgress;

    private CardDefinition currentCard;

    private void Awake()
    {
        Instance = this;

        // Wire button listeners
        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (panelBGButton != null)
        {
            panelBGButton.onClick.AddListener(Close);
        }

        // Start hidden
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Opens the popup and displays the given card's details.
    /// </summary>
    public void Show(CardDefinition def)
    {
        if (def == null)
        {
            Debug.LogWarning("[CardDetailPopup] Show called with null CardDefinition!");
            return;
        }

        currentCard = def;

        Debug.Log($"[CardDetailPopup] Showing card: {def.displayName} (Type: {def.type})");

        // Activate popup
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        RefreshUI();
    }

    /// <summary>
    /// Closes the popup.
    /// </summary>
    public void Close()
    {
        Debug.Log("[CardDetailPopup] Closing popup.");

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        currentCard = null;
    }

    /// <summary>
    /// Refreshes the UI to reflect the current card's state.
    /// </summary>
    private void RefreshUI()
    {
        if (currentCard == null) return;

        // Get progress values from helpers
        float progress01 = currentCard.GetUpgradeProgress01();
        string progressText = currentCard.GetUpgradeProgressText();
        int need = currentCard.GetCopiesRequiredForNextLevel();

        // Debug log
        Debug.Log($"[CardUI] {currentCard.type} L{currentCard.currentLevel} copiesOwned={currentCard.copiesOwned} need={need} progress={progress01:F2} text={progressText}");

        // Icon
        if (cardIcon != null)
        {
            if (currentCard.icon != null)
            {
                cardIcon.sprite = currentCard.icon;
                cardIcon.color = Color.white;
            }
            else
            {
                cardIcon.color = new Color(1f, 1f, 1f, 0.3f); // Placeholder grey
            }
        }

        // Title (displayName)
        if (textTitle != null)
        {
            textTitle.text = currentCard.displayName;
        }

        // Card Type
        if (textCardType != null)
        {
            textCardType.text = $"{currentCard.type} - {currentCard.rarity}";
        }

        // Value text (upgrade info)
        if (textValue != null)
        {
            int level = currentCard.currentLevel;
            double cost = currentCard.GetUpgradeCost();

            if (currentCard.IsMaxLevel)
            {
                textValue.text = $"Level {level} (MAX)\nCopies: {currentCard.copiesOwned}";
            }
            else
            {
                textValue.text = $"Level {level} → {level + 1}\nCost: {FormatNumber(cost)}\nCopies: {progressText}";
            }
        }

        // Progress bar fill
        if (progressFill != null)
        {
            progressFill.fillAmount = progress01;
        }

        // Optional dedicated progress text
        if (textProgress != null)
        {
            textProgress.text = progressText;
        }

        // Description (placeholder or rarity info)
        if (textDescription != null)
        {
            textDescription.text = GetCardDescription(currentCard);
        }

        // Update upgrade button state
        UpdateUpgradeButton();
    }

    /// <summary>
    /// Updates the upgrade button's interactable state and text.
    /// </summary>
    private void UpdateUpgradeButton()
    {
        if (upgradeButton == null || currentCard == null) return;

        int copies = currentCard.copiesOwned;
        int copiesNeeded = currentCard.GetCopiesRequiredForNextLevel();
        double cost = currentCard.GetUpgradeCost();
        double money = CurrencyManager.Instance != null ? CurrencyManager.Instance.money : 0;

        bool isMaxLevel = currentCard.IsMaxLevel;
        bool hasEnoughCopies = copies >= copiesNeeded;
        bool hasEnoughMoney = money >= cost;

        bool canUpgrade = !isMaxLevel && hasEnoughCopies && hasEnoughMoney;

        upgradeButton.interactable = canUpgrade;

        // Update button text
        if (upgradeButtonText != null)
        {
            if (isMaxLevel)
            {
                upgradeButtonText.text = "MAX LEVEL";
            }
            else if (!hasEnoughCopies)
            {
                upgradeButtonText.text = $"Need {copiesNeeded - copies} more copies";
            }
            else if (!hasEnoughMoney)
            {
                upgradeButtonText.text = $"Need {FormatNumber(cost - money)} more";
            }
            else
            {
                upgradeButtonText.text = $"UPGRADE ({FormatNumber(cost)})";
            }
        }
    }

    /// <summary>
    /// Called when the upgrade button is clicked.
    /// </summary>
    private void OnUpgradeClicked()
    {
        if (currentCard == null)
        {
            Debug.LogWarning("[CardDetailPopup] OnUpgradeClicked: No card selected!");
            return;
        }

        if (CardManager.Instance == null)
        {
            Debug.LogError("[CardDetailPopup] OnUpgradeClicked: CardManager.Instance is null!");
            return;
        }

        CardType cardType = currentCard.type;
        int oldLevel = currentCard.currentLevel;

        Debug.Log($"[CardDetailPopup] Attempting upgrade for: {cardType}");

        bool success = CardManager.Instance.TryUpgradeCard(cardType);

        if (success)
        {
            Debug.Log($"[CardDetailPopup] Upgraded {cardType} to level {currentCard.currentLevel}");

            // Save the game
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.SaveGame();
                Debug.Log("[CardDetailPopup] Game saved after upgrade.");
            }
            else
            {
                Debug.LogWarning("[CardDetailPopup] SaveSystem.Instance is null, could not save.");
            }

            // Refresh UI to show new values
            RefreshUI();

            // Play upgrade sound if available
            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayUpgrade();
            }
        }
        else
        {
            // Log the reason for failure (TryUpgradeCard already logs specifics)
            Debug.Log($"[CardDetailPopup] Upgrade failed for {cardType}. Check console for details.");

            // Refresh anyway to update button state
            RefreshUI();
        }
    }

    /// <summary>
    /// Returns a description for the card based on its type.
    /// </summary>
    private string GetCardDescription(CardDefinition card)
    {
        switch (card.type)
        {
            case CardType.TurboFinger:
                return "Tap quickly to activate! Grants bonus money per tap for a limited number of taps.";

            case CardType.GarageManager:
                return "Activates after purchases. Grants bonus MPS for a duration.";

            case CardType.NitroRain:
                return "Increases nitro coin spawn rate and rewards.";

            case CardType.PitStopCrew:
                return "Earns money while you're away (offline production).";

            case CardType.BoostMode:
                return "Collect nitro coins to charge the boost bar. Auto-activates at full charge for 20x earnings!";

            default:
                return $"Rarity: {card.rarity}";
        }
    }

    /// <summary>
    /// Formats a number for display (e.g., 1.5K, 2.3M).
    /// </summary>
    private string FormatNumber(double value)
    {
        if (value >= 1_000_000_000)
            return $"{value / 1_000_000_000:F1}B";
        if (value >= 1_000_000)
            return $"{value / 1_000_000:F1}M";
        if (value >= 1_000)
            return $"{value / 1_000:F1}K";
        return value.ToString("F0");
    }

    /// <summary>
    /// Returns true if the popup is currently open.
    /// </summary>
    public bool IsOpen
    {
        get
        {
            if (popupRoot != null)
                return popupRoot.activeSelf;
            return gameObject.activeSelf;
        }
    }
}
