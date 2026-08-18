using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controls the Bank > DailyOffers section.
/// Attach to: Canvas/Panel_Bank2/ScrollView_Bank/Viewport/Content/Section_DailyOffers
///
/// Inspector wiring:
///   - countdownText   → Section_DailyOffers/Text  (TMP_Text showing "New offers will appear in ...")
///   - freeSlot        → OfferSlot_Free  (DailyOfferSlotUI)
///   - cardSlot2       → OfferSlot_2     (DailyOfferSlotUI)
///   - cardSlot3       → OfferSlot_3     (DailyOfferSlotUI)
/// </summary>
public class DailyOffersController : MonoBehaviour
{
    // ──────────────────────────────── Inspector ────────────────────────────────

    [Header("Countdown")]
    [Tooltip("TMP_Text that shows remaining time until next daily reset.")]
    [SerializeField] private TMP_Text countdownText;

    [Header("Slots")]
    [Tooltip("DailyOfferSlotUI on OfferSlot_Free.")]
    [SerializeField] private DailyOfferSlotUI freeSlot;

    [Tooltip("DailyOfferSlotUI on OfferSlot_2.")]
    [SerializeField] private DailyOfferSlotUI cardSlot2;

    [Tooltip("DailyOfferSlotUI on OfferSlot_3.")]
    [SerializeField] private DailyOfferSlotUI cardSlot3;

    [Header("Prices")]
    [SerializeField] private int slot2Price = 15;
    [SerializeField] private int slot3Price = 30;

    [Header("Per-Purchase Scaling (prices & copies increase each buy)")]
    [Tooltip("Price increase per purchase for slot 2.")]
    [SerializeField] private int slot2PriceStep = 5;
    [Tooltip("Price increase per purchase for slot 3.")]
    [SerializeField] private int slot3PriceStep = 10;
    [Tooltip("Copy bonus per purchase (added on top of base copies).")]
    [SerializeField] private int copiesStepPerPurchase = 1;

    [Header("Card Copies Granted")]
    [SerializeField] private int copiesPerPurchase = 5;

    [Header("Free Reward Config")]
    [Tooltip("Min / max soft-currency (Money) reward for the free slot.")]
    [SerializeField] private double freeMoneyMin = 50;
    [SerializeField] private double freeMoneyMax = 200;

    [Tooltip("Min / max NitroCoins reward for the free slot.")]
    [SerializeField] private int freeNitroMin = 1;
    [SerializeField] private int freeNitroMax = 5;

    // ──────────────────────────────── PlayerPrefs keys ─────────────────────────

    private const string KEY_RESET_TICKS = "DailyOffers_ResetTicks";
    private const string KEY_FREE_CLAIMED = "DailyOffers_FreeClaimed";
    private const string KEY_SLOT2_CARD = "DailyOffers_Slot2Card";
    private const string KEY_SLOT3_CARD = "DailyOffers_Slot3Card";
    private const string KEY_SLOT2_BOUGHT = "DailyOffers_Slot2Bought";
    private const string KEY_SLOT3_BOUGHT = "DailyOffers_Slot3Bought";
    private const string KEY_SLOT2_PURCHASES = "DailyOffers_Slot2Purchases";
    private const string KEY_SLOT3_PURCHASES = "DailyOffers_Slot3Purchases";

    // ──────────────────────────────── Runtime state ────────────────────────────

    private DateTime nextResetUtc;      // when offers rotate next
    private bool freeClaimed;
    private bool slot2Bought;
    private bool slot3Bought;
    private CardType slot2Card;
    private CardType slot3Card;

    // Per-purchase scaling: total lifetime purchases for each slot (persists across days)
    private int slot2TotalPurchases = 0;
    private int slot3TotalPurchases = 0;

    private Coroutine timerCoroutine;

    // ──────────────────────────────── Lifecycle ────────────────────────────────

    private void OnEnable()
    {
        LoadState();
        WireButtons();
        RefreshAllUI();

        // Subscribe to card changes so progress bars stay up-to-date
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsChanged += OnCardsChanged;

        // Start the 1-second countdown coroutine
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        timerCoroutine = StartCoroutine(CountdownTick());
    }

    private void OnDisable()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsChanged -= OnCardsChanged;

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
    }

    // ──────────────────────────────── Timer ────────────────────────────────────

    private IEnumerator CountdownTick()
    {
        var wait = new WaitForSecondsRealtime(1f);
        while (true)
        {
            TimeSpan remaining = nextResetUtc - DateTime.UtcNow;

            if (remaining.TotalSeconds <= 0)
            {
                // Daily reset triggered
                PerformDailyReset();
                remaining = nextResetUtc - DateTime.UtcNow;
            }

            if (countdownText != null)
            {
                int h = Mathf.Max(0, (int)remaining.TotalHours);
                int m = Mathf.Max(0, remaining.Minutes);
                countdownText.text = $"New offers will appear in {h}h {m:D2}m";
            }

            yield return wait;
        }
    }

    // ──────────────────────────────── Daily Reset ─────────────────────────────

    private void PerformDailyReset()
    {
        // Reset state
        freeClaimed = false;
        slot2Bought = false;
        slot3Bought = false;

        // Advance reset to next 24h boundary
        nextResetUtc = DateTime.UtcNow.AddHours(24);

        // Pick new cards
        PickDailyCards();

        SaveState();
        RefreshAllUI();

        Debug.Log("[DailyOffers] Daily reset performed. Next reset: " + nextResetUtc);
    }

    // ──────────────────────────────── Card Selection ───────────────────────────

    /// <summary>
    /// Selection algorithm:
    ///   1. Gather all cards.
    ///   2. Sort by: lowest currentLevel ASC, then fewest segments-toward-upgrade ASC.
    ///   3. Slot 2 gets best candidate, Slot 3 gets second best.
    /// No max-level filter — levels are infinite.
    /// </summary>
    private void PickDailyCards()
    {
        if (CardManager.Instance == null || CardManager.Instance.cards == null)
        {
            Debug.LogWarning("[DailyOffers] CardManager not available. Cannot pick daily cards.");
            return;
        }

        var candidates = GetSortedCardCandidates();

        if (candidates.Count >= 2)
        {
            slot2Card = candidates[0].type;
            slot3Card = candidates[1].type;
        }
        else if (candidates.Count == 1)
        {
            slot2Card = candidates[0].type;
            slot3Card = candidates[0].type; // fallback: same card
            Debug.LogWarning("[DailyOffers] Only 1 eligible card found; both slots use same card.");
        }
        else
        {
            // No eligible cards at all — keep whatever was there before
            Debug.LogWarning("[DailyOffers] No eligible cards for daily offers.");
        }
    }

    private List<CardDefinition> GetSortedCardCandidates()
    {
        var list = new List<CardDefinition>();

        foreach (var card in CardManager.Instance.cards)
        {
            if (card == null) continue;
            // No max-level filter — levels are infinite
            list.Add(card);
        }

        // Sort: lowest level first, then fewest segments toward next upgrade
        list.Sort((a, b) =>
        {
            int levelCmp = a.currentLevel.CompareTo(b.currentLevel);
            if (levelCmp != 0) return levelCmp;

            // Fewer segments owned = further from upgrade = higher priority to offer
            return a.copiesOwned.CompareTo(b.copiesOwned);
        });

        return list;
    }

    // ──────────────────────────────── Button Wiring ────────────────────────────

    private void WireButtons()
    {
        if (freeSlot != null && freeSlot.button != null)
        {
            freeSlot.button.onClick.RemoveAllListeners();
            freeSlot.button.onClick.AddListener(OnFreeSlotClicked);
        }
        if (cardSlot2 != null && cardSlot2.button != null)
        {
            cardSlot2.button.onClick.RemoveAllListeners();
            cardSlot2.button.onClick.AddListener(OnSlot2Clicked);
        }
        if (cardSlot3 != null && cardSlot3.button != null)
        {
            cardSlot3.button.onClick.RemoveAllListeners();
            cardSlot3.button.onClick.AddListener(OnSlot3Clicked);
        }
    }

    // ──────────────────────────────── Click Handlers ──────────────────────────

    private void OnFreeSlotClicked()
    {
        if (freeClaimed)
        {
            Debug.Log("[DailyOffers] Free reward already claimed this cycle.");
            return;
        }

        // U5: Free reward claim SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayDailyFreeClaim();

        GrantFreeReward();

        freeClaimed = true;
        SaveState();
        RefreshFreeSlotUI();
    }

    private void OnSlot2Clicked()
    {
        if (slot2Bought)
        {
            Debug.Log("[DailyOffers] Slot 2 already purchased this cycle.");
            return;
        }
        TryPurchaseCardSlot(slot2Card, slot2Price, isSlot2: true);
    }

    private void OnSlot3Clicked()
    {
        if (slot3Bought)
        {
            Debug.Log("[DailyOffers] Slot 3 already purchased this cycle.");
            return;
        }
        TryPurchaseCardSlot(slot3Card, slot3Price, isSlot2: false);
    }

    // ──────────────────────────────── Free Reward Logic ────────────────────────

    private enum FreeRewardType { Money, NitroCoins, FreeChest }

    private void GrantFreeReward()
    {
        // Pick random reward type
        FreeRewardType reward = (FreeRewardType)UnityEngine.Random.Range(0, 3);

        switch (reward)
        {
            case FreeRewardType.Money:
                double currentMoney = CurrencyManager.Instance != null ? CurrencyManager.Instance.money : 0;
                double scaled = Math.Floor(currentMoney * UnityEngine.Random.Range(0.01f, 0.03f));
                double amount = Math.Max(freeMoneyMin, scaled);
                if (CurrencyManager.Instance != null)
                {
                    CurrencyManager.Instance.AddMoney(amount, "DailyOfferFree");
                    Debug.Log($"[DailyOffers] Free reward: +{amount} Money (scaled from balance {currentMoney})");
                }
                else
                    Debug.LogWarning("[DailyOffers] CurrencyManager is null, cannot grant money.");
                break;

            case FreeRewardType.NitroCoins:
                int nitro = UnityEngine.Random.Range(freeNitroMin, freeNitroMax + 1);
                if (CurrencyManager.Instance != null)
                {
                    CurrencyManager.Instance.AddNitroCoins(nitro);
                    Debug.Log($"[DailyOffers] Free reward: +{nitro} NitroCoins");
                }
                else
                    Debug.LogWarning("[DailyOffers] CurrencyManager is null, cannot grant NitroCoins.");
                break;

            case FreeRewardType.FreeChest:
                OpenFreeCommonChest();
                break;
        }

        // Save after granting
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();
    }

    /// <summary>
    /// Opens a free Common chest using the same session pipeline as Blacklist rewards.
    /// Creates ChestData → BeginSession → LoadScene("ChestOpenScene").
    /// </summary>
    private void OpenFreeCommonChest()
    {
        var sessionMgr = ChestSessionManager.Instance;
        if (sessionMgr == null)
        {
            Debug.LogError("[DailyOffers] ChestSessionManager not found! Cannot open free chest.");
            return;
        }

        var chestData = new ChestInventoryManager.ChestData
        {
            chestType = ChestType.Common,
            chestName = "Daily Offer Chest",
            unlockDurationSeconds = 0f,
            state = ChestState.Idle,
            unlockEndUtcTicks = 0,
            halfTimeUsed = false
        };

        sessionMgr.BeginSession(chestData);

        Debug.Log("[DailyOffers] Free reward: opening Common chest via ChestOpenScene.");
        SceneManager.LoadScene("ChestOpenScene");
    }

    // ──────────────────────────────── Card Purchase Logic ──────────────────────

    private void TryPurchaseCardSlot(CardType cardType, int basePrice, bool isSlot2)
    {
        // Validate CurrencyManager
        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning("[DailyOffers] CurrencyManager is null, cannot purchase.");
            return;
        }

        // Validate CardManager
        if (CardManager.Instance == null)
        {
            Debug.LogWarning("[DailyOffers] CardManager is null, cannot purchase.");
            return;
        }

        // Check card exists
        CardDefinition card = CardManager.Instance.GetCard(cardType);
        if (card == null)
        {
            Debug.LogWarning($"[DailyOffers] Card {cardType} not found.");
            return;
        }

        // Calculate scaled price based on total lifetime purchases
        int totalPurchases = isSlot2 ? slot2TotalPurchases : slot3TotalPurchases;
        int priceStep = isSlot2 ? slot2PriceStep : slot3PriceStep;
        int scaledPrice = basePrice + (totalPurchases * priceStep);
        int scaledCopies = copiesPerPurchase + (totalPurchases * copiesStepPerPurchase);

        // Spend NitroCoins
        if (!CurrencyManager.Instance.TrySpendNitroCoins(scaledPrice))
        {
            Debug.Log($"[DailyOffers] Not enough NitroCoins. Need {scaledPrice}, have {CurrencyManager.Instance.nitroCoins}.");
            return;
        }

        // Grant copies (does NOT auto-upgrade — matches requirement)
        CardManager.Instance.AddCardCopies(cardType, scaledCopies);

        // U6: Card purchase SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayDailyPackBuy();

        Debug.Log($"[DailyOffers] Purchased +{scaledCopies} copies of {cardType} for {scaledPrice} NitroCoins (purchase #{totalPurchases + 1}).");

        // Mark purchased & increment lifetime counter
        if (isSlot2)
        {
            slot2Bought = true;
            slot2TotalPurchases++;
        }
        else
        {
            slot3Bought = true;
            slot3TotalPurchases++;
        }

        SaveState();
        RefreshCardSlotUI(isSlot2);

        // Save full game state
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();
    }

    // ──────────────────────────────── UI Refresh ──────────────────────────────

    private void RefreshAllUI()
    {
        RefreshFreeSlotUI();
        RefreshCardSlotUI(isSlot2: true);
        RefreshCardSlotUI(isSlot2: false);
    }

    private void RefreshFreeSlotUI()
    {
        if (freeSlot == null) return;

        freeSlot.SetTitle("Daily Gift");

        if (freeClaimed)
        {
            freeSlot.SetPurchased("CLAIMED");
        }
        else
        {
            freeSlot.SetAvailable("FREE");
        }
    }

    private void RefreshCardSlotUI(bool isSlot2)
    {
        DailyOfferSlotUI slot = isSlot2 ? cardSlot2 : cardSlot3;
        CardType type = isSlot2 ? slot2Card : slot3Card;
        bool bought = isSlot2 ? slot2Bought : slot3Bought;
        int basePrice = isSlot2 ? slot2Price : slot3Price;

        // Calculate scaled price for display
        int totalPurchases = isSlot2 ? slot2TotalPurchases : slot3TotalPurchases;
        int priceStep = isSlot2 ? slot2PriceStep : slot3PriceStep;
        int scaledPrice = basePrice + (totalPurchases * priceStep);

        if (slot == null) return;

        if (CardManager.Instance == null) return;

        CardDefinition card = CardManager.Instance.GetCard(type);
        if (card == null)
        {
            slot.SetTitle("???");
            slot.SetPurchased("N/A");
            return;
        }

        // Title: "CardType\nRarity"
        slot.SetTitle($"{card.displayName}\n{card.rarity}");

        // Icon
        slot.SetIcon(card.icon);

        // Progress bar (segmented)
        float progress = card.GetUpgradeProgress01();
        string progressTxt = card.GetUpgradeProgressText();
        slot.SetProgress(progress, progressTxt);

        // DAILY OFFERS: countdown + slot gating (UI only)
        // If the card is not yet owned/unlocked, lock the slot visually and disable purchase.
        bool cardOwned = card.IsUnlocked;

        if (bought)
        {
            slot.SetPurchased("PURCHASED");
        }
        else if (!cardOwned)
        {
            slot.SetLocked("LOCKED");
        }
        else
        {
            slot.SetAvailable(scaledPrice.ToString());
        }
    }

    /// <summary>
    /// Called when CardManager fires OnCardsChanged (e.g. after chest opening gives copies).
    /// Refreshes the progress bars on card slots.
    /// </summary>
    private void OnCardsChanged()
    {
        RefreshCardSlotUI(isSlot2: true);
        RefreshCardSlotUI(isSlot2: false);
    }

    // ──────────────────────────────── Persistence ─────────────────────────────

    private void SaveState()
    {
        PlayerPrefs.SetString(KEY_RESET_TICKS, nextResetUtc.Ticks.ToString());
        PlayerPrefs.SetInt(KEY_FREE_CLAIMED, freeClaimed ? 1 : 0);
        PlayerPrefs.SetString(KEY_SLOT2_CARD, slot2Card.ToString());
        PlayerPrefs.SetString(KEY_SLOT3_CARD, slot3Card.ToString());
        PlayerPrefs.SetInt(KEY_SLOT2_BOUGHT, slot2Bought ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SLOT3_BOUGHT, slot3Bought ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SLOT2_PURCHASES, slot2TotalPurchases);
        PlayerPrefs.SetInt(KEY_SLOT3_PURCHASES, slot3TotalPurchases);
        PlayerPrefs.Save();
    }

    private void LoadState()
    {
        // --- Determine reset time ---
        if (PlayerPrefs.HasKey(KEY_RESET_TICKS))
        {
            long ticks;
            if (long.TryParse(PlayerPrefs.GetString(KEY_RESET_TICKS, "0"), out ticks) && ticks > 0)
            {
                nextResetUtc = new DateTime(ticks, DateTimeKind.Utc);
            }
            else
            {
                // Corrupted value — force reset
                nextResetUtc = DateTime.UtcNow;
            }
        }
        else
        {
            // First ever run — schedule first reset 24h from now
            nextResetUtc = DateTime.UtcNow.AddHours(24);
        }

        // --- Check if reset is overdue (app was closed past the deadline) ---
        if (DateTime.UtcNow >= nextResetUtc)
        {
            PerformDailyReset();
            return; // PerformDailyReset already saves + refreshes
        }

        // --- Load per-cycle state ---
        freeClaimed = PlayerPrefs.GetInt(KEY_FREE_CLAIMED, 0) == 1;
        slot2Bought = PlayerPrefs.GetInt(KEY_SLOT2_BOUGHT, 0) == 1;
        slot3Bought = PlayerPrefs.GetInt(KEY_SLOT3_BOUGHT, 0) == 1;

        // Per-purchase scaling counters (persist across daily resets)
        slot2TotalPurchases = PlayerPrefs.GetInt(KEY_SLOT2_PURCHASES, 0);
        slot3TotalPurchases = PlayerPrefs.GetInt(KEY_SLOT3_PURCHASES, 0);

        // Card types
        string s2 = PlayerPrefs.GetString(KEY_SLOT2_CARD, "");
        string s3 = PlayerPrefs.GetString(KEY_SLOT3_CARD, "");

        if (!string.IsNullOrEmpty(s2) && Enum.IsDefined(typeof(CardType), s2))
            slot2Card = (CardType)Enum.Parse(typeof(CardType), s2);
        else
            PickDailyCards(); // fallback

        if (!string.IsNullOrEmpty(s3) && Enum.IsDefined(typeof(CardType), s3))
            slot3Card = (CardType)Enum.Parse(typeof(CardType), s3);
        // else: already set by PickDailyCards fallback above or from previous valid parse

        // Edge-case: if both parsed to the same card, try re-picking
        if (slot2Card == slot3Card)
        {
            var candidates = GetSortedCardCandidates();
            if (candidates.Count >= 2)
            {
                slot2Card = candidates[0].type;
                slot3Card = candidates[1].type;
                SaveState();
            }
        }
    }
}
