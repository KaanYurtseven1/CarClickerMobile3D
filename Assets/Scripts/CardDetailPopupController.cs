// ============================================================================
// CardDetailPopupController.cs — Per-card themed detail popup
// ============================================================================
//
// INSPECTOR WIRING NOTES
// ──────────────────────
// 1. popupPanelImage   → Image component on the PopupPanel background.
// 2. upgradeButton     → The Upgrade Button reference.
//    upgradeButtonImage → Image component on the same Upgrade Button (its graphic).
// 3. textTitle         → TMP_Text in Row_Title (card name).
// 4. valueTitleLabel   → TMP_Text for the constant "Value" label.
//    valueBodyText     → TMP_Text for the per-card value body (Value_Text).
// 5. descTitleLabel    → TMP_Text for the constant "Description" label.
//    descBodyText      → TMP_Text for the per-card description body (Description_Text).
// 6. fillSegments[0..7]→ 8 × Image in Row_Bar fill slots, assigned left→right.
//    (Image Type should be Simple, not Filled. Segments are toggled via SetActive.)
// 7. themeDatabase     → Assign the CardPopupThemeDatabaseSO asset.
// 8. Legacy fields (cardIcon, textCardType, progressFill, textProgress) are kept
//    for backward compatibility and will still update when assigned.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CardDetailPopupController : MonoBehaviour
{
    public static CardDetailPopupController Instance;

    // ── Root ─────────────────────────────────────────────────────────────
    [Header("Root")]
    [SerializeField] private GameObject popupRoot;

    // ── Theme Database ───────────────────────────────────────────────────
    [Header("Theme Database")]
    [Tooltip("Assign the CardPopupThemeDatabaseSO asset that maps CardType → theme.")]
    [SerializeField] private CardPopupThemeDatabaseSO themeDatabase;

    // ── Themed Visuals ───────────────────────────────────────────────────
    [Header("Themed Panel & Button")]
    [Tooltip("Image on the PopupPanel background — sprite swapped per card.")]
    [SerializeField] private Image popupPanelImage;
    [Tooltip("Image on the Upgrade Button — sprite swapped per card.")]
    [SerializeField] private Image upgradeButtonImage;

    // ── Text References ──────────────────────────────────────────────────
    [Header("Text — Title Row")]
    [SerializeField] private TextMeshProUGUI textTitle;

    [Header("Text — Value Row")]
    [Tooltip("Constant label 'Value' (set automatically).")]
    [SerializeField] private TextMeshProUGUI valueTitleLabel;
    [Tooltip("Per-card value body text.")]
    [SerializeField] private TextMeshProUGUI valueBodyText;

    [Header("Text — Description Row")]
    [Tooltip("Constant label 'Description' (set automatically).")]
    [SerializeField] private TextMeshProUGUI descTitleLabel;
    [Tooltip("Per-card description body text.")]
    [SerializeField] private TextMeshProUGUI descBodyText;

    // ── Legacy UI References (kept for backward compat) ──────────────────
    [Header("Legacy UI References")]
    [SerializeField] private Image cardIcon;
    [SerializeField] private TextMeshProUGUI textCardType;
    [SerializeField] private TextMeshProUGUI textValue;
    [SerializeField] private TextMeshProUGUI textDescription;

    // ── Buttons ──────────────────────────────────────────────────────────
    [Header("Buttons")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button panelBGButton; // Optional: click background to close

    [Header("Upgrade Button Text")]
    [SerializeField] private TextMeshProUGUI upgradeButtonText;

    // ── Progress Bar (8 segments) ────────────────────────────────────────
    [Header("Progress Bar")]
    [Tooltip("Fill Image for copy progress (legacy fillAmount bar — optional if using segments)")]
    [SerializeField] private Image progressFill;
    [Tooltip("8 segment fill images (one per segment, toggled on/off). Assign in order 0→7.\nImage Type should be Simple — segments are toggled via SetActive.")]
    [SerializeField] private Image[] fillSegments;
    [Tooltip("Optional: Dedicated text for progress (e.g., '3/8'). If null, uses textValue.")]
    [SerializeField] private TextMeshProUGUI textProgress;

    // ── Runtime ──────────────────────────────────────────────────────────
    private CardDefinition currentCard;
    private Dictionary<CardType, CardPopupThemeSO> _themeCache;

    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    private void Awake()
    {
        Instance = this;

        // Build O(1) theme lookup from database
        BuildThemeCache();

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

    // =====================================================================
    // Public API (unchanged signature — all call sites stay the same)
    // =====================================================================

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

        // Apply per-card theme visuals, then refresh data-driven UI
        ApplyTheme(def);
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

    // =====================================================================
    // Theme Application
    // =====================================================================

    /// <summary>
    /// Applies per-card themed visuals (sprites + texts) from the theme database.
    /// If no theme is found for the card, a warning is logged and current visuals are kept.
    /// </summary>
    private void ApplyTheme(CardDefinition def)
    {
        if (_themeCache == null || !_themeCache.TryGetValue(def.type, out CardPopupThemeSO theme))
        {
            Debug.LogWarning($"[CardDetailPopup] No theme found for {def.type}. Keeping current visuals.");
            // Still set title from def as a fallback
            if (textTitle != null)
                textTitle.text = def.displayName;
            return;
        }

        // 1) PopupPanel background sprite
        if (popupPanelImage != null && theme.popupPanelSprite != null)
        {
            popupPanelImage.sprite = theme.popupPanelSprite;
        }

        // 2) Upgrade button sprite
        if (upgradeButtonImage != null && theme.upgradeButtonSprite != null)
        {
            upgradeButtonImage.sprite = theme.upgradeButtonSprite;
        }

        // 3) Title — use override if provided, else def.displayName
        if (textTitle != null)
        {
            textTitle.text = !string.IsNullOrEmpty(theme.displayNameOverride)
                ? theme.displayNameOverride
                : def.displayName;
        }

        // 4) Value row — constant label + per-card body
        if (valueTitleLabel != null)
        {
            valueTitleLabel.text = "Value";
        }
        if (valueBodyText != null)
        {
            valueBodyText.text = theme.valueText;
        }

        // 5) Description row — constant label + per-card body
        if (descTitleLabel != null)
        {
            descTitleLabel.text = "Description";
        }
        if (descBodyText != null)
        {
            descBodyText.text = theme.descriptionText;
        }
    }

    // =====================================================================
    // UI Refresh (data-driven: segments, costs, button state)
    // =====================================================================

    /// <summary>
    /// Refreshes the UI to reflect the current card's state.
    /// </summary>
    private void RefreshUI()
    {
        if (currentCard == null) return;

        // Get progress values from helpers (8-segment model)
        float progress01 = currentCard.GetUpgradeProgress01();
        string progressText = currentCard.GetUpgradeProgressText();
        int filledSegmentCount = currentCard.GetFilledSegments();

        // Debug log
        Debug.Log($"[CardUI] {currentCard.type} L{currentCard.currentLevel} segments={currentCard.copiesOwned} filled={filledSegmentCount} text={progressText}");

        // Icon (legacy — still works when assigned)
        if (cardIcon != null)
        {
            if (currentCard.icon != null)
            {
                cardIcon.sprite = currentCard.icon;
                cardIcon.color = Color.white;
            }
            else
            {
                cardIcon.color = new Color(1f, 1f, 1f, 0.3f);
            }
        }

        // Card Type (legacy field)
        if (textCardType != null)
        {
            textCardType.text = $"{currentCard.type} - {currentCard.rarity}";
        }

        // Legacy value text (upgrade info — no MAX state)
        if (textValue != null)
        {
            int level = currentCard.currentLevel;
            double cost = currentCard.GetUpgradeCost();
            textValue.text = $"Level {level} → {level + 1}\nCost: {FormatNumber(cost)}\nSegments: {progressText}";
        }

        // 8-segment progress bar
        UpdateSegments(filledSegmentCount);

        // Legacy fill bar (optional fallback)
        if (progressFill != null)
        {
            progressFill.fillAmount = progress01;
        }

        // Optional dedicated progress text
        if (textProgress != null)
        {
            textProgress.text = progressText;
        }

        // Legacy description text
        if (textDescription != null)
        {
            textDescription.text = GetCardDescription(currentCard);
        }

        // Update upgrade button state
        UpdateUpgradeButton();
    }

    // =====================================================================
    // Segment Bar
    // =====================================================================

    /// <summary>
    /// Toggles the 8 fill-segment images on/off based on the card's current segment count.
    /// Bar_BG segments are always visible (they are siblings, not toggled here).
    /// Bar_Fill segment i is ON when i &lt; segmentsFilled, OFF otherwise.
    /// Image Type for these should be Simple (not Filled) — toggled via SetActive.
    /// </summary>
    private void UpdateSegments(int segmentsFilled)
    {
        if (fillSegments == null || fillSegments.Length == 0) return;

        // Clamp to 0..8 for safety (overflow = show full bar)
        int clamped = Mathf.Clamp(segmentsFilled, 0, fillSegments.Length);

        for (int i = 0; i < fillSegments.Length; i++)
        {
            if (fillSegments[i] != null)
                fillSegments[i].gameObject.SetActive(i < clamped);
        }
    }

    // =====================================================================
    // Upgrade Button
    // =====================================================================

    /// <summary>
    /// Updates the upgrade button's interactable state and text.
    /// 8-segment model: can upgrade when segments >= 8 AND enough money.
    /// Never shows "MAX LEVEL" — levels are infinite.
    /// </summary>
    private void UpdateUpgradeButton()
    {
        if (upgradeButton == null || currentCard == null) return;

        int segments = currentCard.copiesOwned;
        int segmentsNeeded = CardDropTuning.SegmentsPerUpgrade;
        double cost = currentCard.GetUpgradeCost();
        double money = CurrencyManager.Instance != null ? CurrencyManager.Instance.money : 0;

        bool hasEnoughSegments = segments >= segmentsNeeded;
        bool hasEnoughMoney = money >= cost;
        bool canUpgrade = hasEnoughSegments && hasEnoughMoney;

        upgradeButton.interactable = canUpgrade;

        // Update button text (no MAX state — infinite levels)
        if (upgradeButtonText != null)
        {
            if (!hasEnoughSegments)
            {
                int missing = segmentsNeeded - (segments % segmentsNeeded);
                upgradeButtonText.text = $"Need {missing} more segments";
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

    // =====================================================================
    // Upgrade Click Handler (unchanged behavior)
    // =====================================================================

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

    // =====================================================================
    // Theme Cache
    // =====================================================================

    /// <summary>
    /// Builds a Dictionary&lt;CardType, CardPopupThemeSO&gt; from the assigned database
    /// for O(1) lookups during Show().
    /// </summary>
    private void BuildThemeCache()
    {
        _themeCache = new Dictionary<CardType, CardPopupThemeSO>();

        if (themeDatabase == null)
        {
            Debug.LogWarning("[CardDetailPopup] No themeDatabase assigned. Themed visuals will be skipped.");
            return;
        }

        if (themeDatabase.themes == null) return;

        foreach (var theme in themeDatabase.themes)
        {
            if (theme == null) continue;

            if (!_themeCache.ContainsKey(theme.type))
            {
                _themeCache.Add(theme.type, theme);
            }
        }

        Debug.Log($"[CardDetailPopup] Theme cache built with {_themeCache.Count} entries.");
    }

    // =====================================================================
    // Helpers (legacy — kept for backward compat)
    // =====================================================================

    /// <summary>
    /// Returns a description for the card based on its type.
    /// NOTE: When a CardPopupThemeSO exists for the card, descBodyText is
    /// populated from the theme instead. This method is the legacy fallback
    /// used by the old textDescription field.
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
}
