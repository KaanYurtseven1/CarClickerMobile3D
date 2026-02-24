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
// 9. popupPanelTransform → RectTransform of the PopupPanel (scaled during animation).
// 10. rootCanvasGroup   → CanvasGroup on the popupRoot (faded during animation).
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class CardDetailPopupController : MonoBehaviour
{
    public static CardDetailPopupController Instance;

    // ── Root ─────────────────────────────────────────────────────────────
    [Header("Root")]
    [SerializeField] private GameObject popupRoot;

    // ── Animation ────────────────────────────────────────────────────────
    [Header("Animation")]
    [Tooltip("RectTransform of the PopupPanel — scaled during open/close animation.")]
    [SerializeField] private RectTransform popupPanelTransform;
    [Tooltip("CanvasGroup on the popupRoot — faded during open/close animation.")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private float openDuration = 0.35f;
    [SerializeField] private float closeDuration = 0.2f;

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
    private bool isAnimating;
    private Sequence currentSequence;

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

        // Guard against double-open or animation overlap
        if (isAnimating || IsOpen) return;

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

        // ── Open animation ──────────────────────────────────────────────
        PlayOpenAnimation();
    }

    /// <summary>
    /// Closes the popup with a fade-out + scale-down animation.
    /// </summary>
    public void Close()
    {
        if (isAnimating || !IsOpen) return;

        Debug.Log("[CardDetailPopup] Closing popup.");

        PlayCloseAnimation();
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
    // Animation Helpers
    // =====================================================================

    /// <summary>
    /// Plays the open animation: fade in + scale up with overshoot.
    /// </summary>
    private void PlayOpenAnimation()
    {
        KillCurrentSequence();

        // If references are missing, skip animation and just show instantly
        if (rootCanvasGroup == null || popupPanelTransform == null)
        {
            return;
        }

        isAnimating = true;

        // Prepare initial state
        rootCanvasGroup.alpha = 0f;
        popupPanelTransform.localScale = Vector3.one * 0.85f;
        rootCanvasGroup.blocksRaycasts = false;

        currentSequence = DOTween.Sequence();
        currentSequence
            .Join(rootCanvasGroup.DOFade(1f, openDuration * 0.7f).SetEase(Ease.OutCubic))
            .Join(popupPanelTransform.DOScale(Vector3.one, openDuration).SetEase(Ease.OutBack, 1.1f))
            .SetUpdate(true) // ignore timescale
            .OnComplete(() =>
            {
                isAnimating = false;
                if (rootCanvasGroup != null)
                    rootCanvasGroup.blocksRaycasts = true;
            })
            .OnKill(() =>
            {
                // Ensure valid state even if killed mid-animation
                isAnimating = false;
            });
    }

    /// <summary>
    /// Plays the close animation: fade out + scale down, then deactivates.
    /// </summary>
    private void PlayCloseAnimation()
    {
        KillCurrentSequence();

        // If references are missing, close instantly (legacy behavior)
        if (rootCanvasGroup == null || popupPanelTransform == null)
        {
            DeactivatePopup();
            return;
        }

        isAnimating = true;
        rootCanvasGroup.blocksRaycasts = false;

        currentSequence = DOTween.Sequence();
        currentSequence
            .Join(rootCanvasGroup.DOFade(0f, closeDuration).SetEase(Ease.InCubic))
            .Join(popupPanelTransform.DOScale(Vector3.one * 0.85f, closeDuration).SetEase(Ease.InCubic))
            .SetUpdate(true) // ignore timescale
            .OnComplete(() =>
            {
                DeactivatePopup();
                // Reset scale so the next open starts clean
                if (popupPanelTransform != null)
                    popupPanelTransform.localScale = Vector3.one;
                if (rootCanvasGroup != null)
                    rootCanvasGroup.blocksRaycasts = false;
                isAnimating = false;
            })
            .OnKill(() =>
            {
                isAnimating = false;
            });
    }

    /// <summary>
    /// Deactivates the popup root (or this gameObject as fallback) and clears currentCard.
    /// </summary>
    private void DeactivatePopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);
        else
            gameObject.SetActive(false);

        currentCard = null;
    }

    /// <summary>
    /// Safely kills any running DOTween sequence to prevent leaks or double-plays.
    /// </summary>
    private void KillCurrentSequence()
    {
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
            currentSequence = null;
        }
    }

    private void OnDestroy()
    {
        KillCurrentSequence();
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

        // 4) Value row — constant label (body text set dynamically in RefreshUI)
        if (valueTitleLabel != null)
        {
            valueTitleLabel.text = "Value";
        }

        // 5) Description row — constant label (body text set dynamically in RefreshUI)
        if (descTitleLabel != null)
        {
            descTitleLabel.text = "Description";
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

        // Dynamic value & description body text (level-aware)
        if (valueBodyText != null)
        {
            valueBodyText.text = WrapTwoLines(BuildValueText(currentCard));
        }
        if (descBodyText != null)
        {
            descBodyText.text = WrapTwoLines(BuildDescriptionText(currentCard));
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
    // Dynamic Text Generation (level-aware value & description)
    // =====================================================================

    // ── Scaling tables (must mirror the actual controller constants) ────
    // TurboFinger: LevelMultipliers[0..6] — index = card level
    private static readonly float[] TF_Multipliers = { 1f, 5f, 10f, 20f, 50f, 100f, 200f };

    // NitroRain: RequiredCollects and RainDurations — index = card level
    private static readonly int[]   NR_Collects  = { 0, 3, 4, 5, 6, 7, 8 };
    private static readonly float[] NR_Durations = { 0f, 5f, 8f, 11f, 14f, 17f, 20f };

    // PitStopCrew: EfficiencyByLevel (fraction) and CapHoursByLevel
    private static readonly float[] PS_Efficiency = { 0f, 0.20f, 0.30f, 0.40f, 0.55f, 0.70f, 0.85f };
    private static readonly float[] PS_CapHours   = { 0f, 2f, 3f, 4f, 6f, 8f, 12f };

    // GarageManager: BonusMultipliers and SpendSecondsEquivalents
    private static readonly float[] GM_Multipliers  = { 0f, 10f, 11f, 12f, 13f, 14f, 15f };
    private static readonly float[] GM_SpendSeconds = { 0f, 30f, 28f, 26f, 24f, 22f, 20f };

    // SmallInvestment: base 2%, +2% per level, cap 12%
    private const float SI_Base = 2f;
    private const float SI_Step = 2f;
    private const float SI_Cap  = 12f;

    // Momentum: scaling helpers
    private const float MO_BaseWindow    = 0.80f;
    private const float MO_WindowStep    = 0.20f;
    private const float MO_BaseBonus     = 0.005f;
    private const float MO_BonusStep     = 0.002f;
    private const int   MO_BaseCap       = 30;
    private const int   MO_CapStep       = 10;

    // NitroMagnet: taps required and coins to collect (index = level - 1)
    private static readonly int[] NM_Taps  = { 30, 40, 50, 55, 60, 70 };
    private static readonly int[] NM_Coins = { 3, 4, 5, 7, 9, 12 };

    /// <summary>
    /// Builds a player-friendly VALUE string for the given card based on its
    /// current level and the actual effect scaling rules.
    /// </summary>
    private string BuildValueText(CardDefinition def)
    {
        int lv = def.currentLevel;
        if (lv <= 0) return "Unlock the card to see its effect.";

        switch (def.type)
        {
            case CardType.TurboFinger:
            {
                int idx = Mathf.Clamp(lv, 0, TF_Multipliers.Length - 1);
                float mult = TF_Multipliers[idx];
                return $"Tap income x{mult:G} for 30s\nActivate: 50 taps/15s | CD: 120s";
            }

            case CardType.NitroRain:
            {
                int idx = Mathf.Clamp(lv, 0, NR_Collects.Length - 1);
                int req = NR_Collects[idx];
                float dur = NR_Durations[idx];
                return $"Collect {req} nitro to start rain ({dur:G}s)\n30s delay before rain";
            }

            case CardType.PitStopCrew:
            {
                int idx = Mathf.Clamp(lv, 0, PS_Efficiency.Length - 1);
                int eff = Mathf.RoundToInt(PS_Efficiency[idx] * 100f);
                float cap = PS_CapHours[idx];
                return $"Offline earn: {eff}% of MPS (max {cap:G}h)";
            }

            case CardType.GarageManager:
            {
                int idx = Mathf.Clamp(lv, 0, GM_Multipliers.Length - 1);
                float mult = GM_Multipliers[idx];
                float secs = GM_SpendSeconds[idx];
                return $"MPS x{mult:G} for 60s | CD: 120s\nSpend {secs:G}s of MPS to trigger";
            }

            case CardType.BoostMode:
            {
                int clampLv = Mathf.Clamp(lv, 1, 6);
                float mult = clampLv * 10f;
                int maxCharge = 5 + clampLv * 5;
                float cd = 30f + clampLv * 15f;
                return $"All income x{mult:G} for 10s\nCharge: {maxCharge} nitro | CD: {cd:G}s";
            }

            case CardType.SmallInvestment:
            {
                float pct = Mathf.Clamp(SI_Base + (lv - 1) * SI_Step, 0f, SI_Cap);
                return $"Refund {pct:G}% of all money & nitro spent";
            }

            case CardType.Momentum:
            {
                float win = MO_BaseWindow + (lv - 1) * MO_WindowStep;
                float bonus = MO_BaseBonus + (lv - 1) * MO_BonusStep;
                int cap = MO_BaseCap + (lv - 1) * MO_CapStep;
                float maxMult = 1f + cap * bonus;
                float pctPerTap = bonus * 100f;
                return $"Up to x{maxMult:F2} tap income ({cap} stacks)\nReset: {win:F1}s | +{pctPerTap:G}%/tap";
            }

            case CardType.NitroMagnet:
            {
                int idx = Mathf.Clamp(lv - 1, 0, NM_Taps.Length - 1);
                int taps = NM_Taps[idx];
                int coins = NM_Coins[idx];
                return $"Tap {taps}x to arm | Auto-pull {coins} nitro";
            }

            default:
            {
                // Fallback: try theme static text
                if (_themeCache != null && _themeCache.TryGetValue(def.type, out var theme)
                    && !string.IsNullOrEmpty(theme.valueText))
                    return theme.valueText;
                return $"Level {lv} active.";
            }
        }
    }

    /// <summary>
    /// Builds a short player-friendly DESCRIPTION for the given card type.
    /// Mostly static per type but still goes through WrapTwoLines.
    /// </summary>
    private string BuildDescriptionText(CardDefinition def)
    {
        switch (def.type)
        {
            case CardType.TurboFinger:
                return "Tap fast to trigger a massive tap income\nmultiplier. Higher level = bigger boost.";

            case CardType.NitroRain:
                return "Collect nitro coins to trigger a rain of\nbonus nitro. More level = longer rain.";

            case CardType.PitStopCrew:
                return "Earn money while you're away. Higher level\n= more efficiency and longer time cap.";

            case CardType.GarageManager:
                return "Spend money to charge. When full, your MPS\ngets a huge boost for 60 seconds.";

            case CardType.BoostMode:
                return "Collect nitro to fill the boost bar. When\nfull, all income is massively multiplied.";

            case CardType.SmallInvestment:
                return "Get a percentage of every purchase back.\nHigher level = bigger cashback.";

            case CardType.Momentum:
                return "Keep tapping without stopping to build combo\nstacks. More stacks = more tap income.";

            case CardType.NitroMagnet:
                return "Tap to charge the magnet. When armed, nearby\nnitro coins fly to your car automatically.";

            default:
            {
                // Fallback: try theme static text
                if (_themeCache != null && _themeCache.TryGetValue(def.type, out var theme)
                    && !string.IsNullOrEmpty(theme.descriptionText))
                    return theme.descriptionText;
                return $"Rarity: {def.rarity}";
            }
        }
    }

    // =====================================================================
    // Text Wrapping Utility
    // =====================================================================

    /// <summary>
    /// Enforces the 2-line / 67-char-per-line rule for popup text fields.
    /// Strategy: prefer splitting at last space before limit; hard-split if
    /// no space; truncate with "..." if second line also overflows.
    /// Already-split text (containing '\n') is respected if within limits.
    /// </summary>
    private static string WrapTwoLines(string s, int maxPerLine = 67)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        // If the text already has a newline, honour it
        int nlIndex = s.IndexOf('\n');
        if (nlIndex >= 0)
        {
            string l1 = s.Substring(0, nlIndex);
            string l2 = s.Substring(nlIndex + 1);

            // Trim each line to max length
            if (l1.Length > maxPerLine)
                l1 = TrimLine(l1, maxPerLine);

            // Drop any 3rd+ lines
            int nl2 = l2.IndexOf('\n');
            if (nl2 >= 0)
                l2 = l2.Substring(0, nl2);

            if (l2.Length > maxPerLine)
                l2 = TrimLine(l2, maxPerLine);

            return l2.Length > 0 ? l1 + "\n" + l2 : l1;
        }

        // Single-line text that fits — return as-is
        if (s.Length <= maxPerLine) return s;

        // Need to split into two lines
        int splitAt = s.LastIndexOf(' ', maxPerLine - 1);
        if (splitAt <= 0) splitAt = maxPerLine; // hard-split

        string line1 = s.Substring(0, splitAt).TrimEnd();
        string line2 = s.Substring(splitAt).TrimStart();

        if (line2.Length > maxPerLine)
            line2 = TrimLine(line2, maxPerLine);

        return line2.Length > 0 ? line1 + "\n" + line2 : line1;
    }

    /// <summary>Truncates a single line to maxLen, ending with "..." if cut.</summary>
    private static string TrimLine(string line, int maxLen)
    {
        if (line.Length <= maxLen) return line;
        return line.Substring(0, maxLen - 3) + "...";
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
