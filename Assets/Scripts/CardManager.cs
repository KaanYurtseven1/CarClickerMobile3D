using UnityEngine;
using System;
using System.Collections.Generic;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    /// <summary>
    /// Event fired when card copies or levels change. UI can subscribe to refresh.
    /// </summary>
    public event Action OnCardsChanged;

    [Header("Card List")]
    public CardDefinition[] cards;

    [Header("Turbo Finger Ayarları")]
    [Tooltip("Turbo Finger kartı seviye başına tap bonusu")]
    public double turboFingerTapBonusPerLevel = 1.0;

    [Header("Garage Manager Ayarları")]
    [Tooltip("Garage Manager seviye başına global MPS yüzdesi (0.03 = %3)")]
    public double garageManagerPercentPerLevel = 0.03;

    // Dahili cache (tekrar tekrar eklememek için)
    private double turboFingerTapBonusCached = 0;
    private double garageManagerPercentCached = 0;

    // ----------------- Runtime Activation State -----------------

    // TurboFinger: tap streak activation
    private List<float> recentTapTimes = new List<float>();
    private float turboFingerCooldownUntil = 0f;
    private int turboFingerChargesRemaining = 0;
    private const float TurboFingerStreakWindow = 3f;
    private const int TurboFingerStreakRequired = 10;
    private const float TurboFingerCooldown = 60f;

    // [DEPRECATED] GarageManager now handled by GarageManagerController
    // private float garageManagerActiveUntil = 0f;

    // [DEPRECATED] Legacy NitroMagnet rate-based activation removed.
    // NitroMagnet is now fully handled by NitroMagnetController (tap-based).
    // See IsNitroMagnetActive() for backward-compatible redirect.

    // Track if we've warned about missing controllers (avoid spam)
    private bool _warnedMissingBoostController = false;
    private bool _warnedMissingNitroRainController = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Oyun başlarken bütün kart efektlerini bir kere uygula
        ReapplyAllCardEffects();

        // Validate controller references at startup (not during gameplay)
        ValidateControllerReferences();
    }

    // Update removed — no periodic maintenance needed.
    // Legacy NitroMagnet queue maintenance has been removed.

    /// <summary>
    /// Validates that required controller references exist at startup.
    /// Logs warnings once if missing - does NOT create them dynamically.
    /// </summary>
    private void ValidateControllerReferences()
    {
        if (NitroRainController.Instance == null)
        {
            Debug.LogWarning("[CardManager] NitroRainController.Instance is null at startup. NitroRain effects will not work.");
        }
        if (BoostModeController.Instance == null)
        {
            Debug.LogWarning("[CardManager] BoostModeController.Instance is null at startup. BoostMode effects will not work.");
        }
    }

    // [REMOVED] PurgeStaleNitroPickupTimes — legacy rate-based NitroMagnet system.
    // NitroMagnet is now handled by NitroMagnetController.

    // ----------------- Genel yardımcılar -----------------

    public CardDefinition GetCard(CardType type)
    {
        foreach (var c in cards)
        {
            if (c.type == type)
                return c;
        }

        Debug.LogError("[CardManager] Card not found: " + type);
        return null;
    }

    /// <summary>
    /// Returns the current level of a card, or 0 if not found.
    /// </summary>
    public int GetCardLevel(CardType type)
    {
        var card = GetCard(type);
        return card != null ? card.currentLevel : 0;
    }

    /// <summary>
    /// UI'da "Upgrade" tuşuna basıldığında çağrılacak ana fonksiyon.
    /// Progression: L1->L2 needs 2 copies, L2->L3 needs 3, etc.
    /// Max level is 6.
    /// </summary>
    public bool TryUpgradeCard(CardType type)
    {
        if (CurrencyManager.Instance == null) return false;

        CardDefinition c = GetCard(type);
        if (c == null) return false;

        // Check max level (hard cap at 6)
        if (c.IsMaxLevel)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CardManager] Max level reached for card: {c.displayName} (Level {c.currentLevel})");
#endif
            return false;
        }

        // Calculate copies needed: currentLevel + 1
        int neededCopies = c.GetCopiesRequiredForNextLevel();
        if (c.copiesOwned < neededCopies)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CardManager] Not enough copies for upgrade: {c.displayName}. Have: {c.copiesOwned}, Need: {neededCopies}");
#endif
            return false;
        }

        double cost = c.GetUpgradeCost();
        if (!CurrencyManager.Instance.TrySpendMoney(cost))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CardManager] Not enough money to upgrade card: {c.displayName}. Cost: {cost}");
#endif
            return false;
        }

        // Successful upgrade: spend copies, increment level
        c.copiesOwned -= neededCopies;
        c.currentLevel++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CardManager] Upgraded {c.displayName} to Level {c.currentLevel}. Copies remaining: {c.copiesOwned}");
#endif

        // Efekti yeniden uygula
        ApplyCardEffect(type);

        // Fire event for UI refresh (CardCollectionUI should subscribe to this)
        OnCardsChanged?.Invoke();

        // Save after successful upgrade
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.SaveGame();
        }

        return true;
    }

    // ----------------- Efektler -----------------

    /// <summary>
    /// Oyun başında ve load sonrası tüm kartların etkisini yeniden hesaplar.
    /// </summary>
    public void ReapplyAllCardEffects()
    {
        // Önce cache'leri sıfırla
        turboFingerTapBonusCached = 0;
        garageManagerPercentCached = 0;

        // Tüm kartları gez ve switch içinden yeni değerleri hesaplat
        foreach (var c in cards)
        {
            ApplyCardEffect(c.type);
        }
    }

    /// <summary>
    /// Belirli bir kart tipinin oyun değerlerine etkisini hesaplar.
    /// NOT: Burada CurrencyManager.moneyPerTap / moneyPerSecond'a direkt ekleme YAPMIYORUZ.
    /// Tap ve MPS hesapları runtime'da CardManager'dan bonus çekiyor.
    /// </summary>
    private void ApplyCardEffect(CardType type)
    {
        CardDefinition c = GetCard(type);
        if (c == null) return;

        switch (type)
        {
            case CardType.TurboFinger:
                // TurboFinger effect handled by TurboFingerController (5x multiplier system)
                // Old charge-based system disabled
                turboFingerTapBonusCached = 0;
                break;

            case CardType.GarageManager:
                // GarageManager effect handled by GarageManagerController (spend-based activation)
                // Passive multiplier disabled to avoid double-counting
                garageManagerPercentCached = 0;
                break;

            case CardType.NitroRain:
                // NitroRain effect handled by NitroRainController (collect-to-rain system)
                break;

            case CardType.PitStopCrew:
                // PitStopCrew effect handled by PitStopCrewController (offline earnings)
                break;

            case CardType.BoostMode:
                // BoostMode effect handled by BoostModeController (boost bar/charge system)
                break;

            case CardType.SmallInvestment:
                // SmallInvestment effect handled by SmallInvestmentController (spend refund)
                // No cached values needed; controller listens to CurrencyManager events.
                break;

            case CardType.Momentum:
                // Momentum effect handled by MomentumController (click combo stacks)
                // Reset stacks when level changes so new params apply cleanly.
                if (MomentumController.Instance != null)
                {
                    MomentumController.Instance.ResetStacks();
                }
                break;

            case CardType.NitroMagnet:
                // NitroMagnet effect handled by NitroMagnetController (tap-based arm/pull).
                // On level change: notify controller to adjust quota if currently armed.
                if (NitroMagnetController.Instance != null)
                {
                    NitroMagnetController.Instance.OnCardLevelChanged(c.currentLevel);
                }
                break;
        }
    }

    // ----------------- Oyun tarafından kullanılacak getter'lar -----------------

    /// <summary>
    /// Kartlardan gelen toplam tap bonusu (para birimi cinsinden).
    /// TapInputRaycaster bu değeri kullanacak.
    /// </summary>
    public double GetTapBonusFromCards()
    {
        return turboFingerTapBonusCached;
    }

    /// <summary>
    /// Global MPS için kartlardan gelen çarpan.
    /// 1 = etki yok, 1.3 = +%30 MPS gibi.
    /// CurrencyManager.GetAutoMPS bunu kullanacak.
    /// </summary>
    public double GetGlobalMpsMultiplierFromCards()
    {
        return 1.0 + garageManagerPercentCached;
    }

    /// <summary>
    /// [DEPRECATED] NitroRain effect is now handled by NitroRainController.
    /// This method is kept for backward compatibility but returns a fixed 1f.
    /// Use NitroRainController.Instance for NitroRain state queries.
    /// </summary>
    public float GetNitroRainMultiplier()
    {
        // NitroRain logic moved to NitroRainController
        return 1f;
    }

    /// <summary>
    /// PitStopCrew için offline production yüzdesi.
    /// Örn: level başına %5 → level 2 = %10
    /// </summary>
    public float GetOfflineProductionPercent()
    {
        CardDefinition c = GetCard(CardType.PitStopCrew);
        if (c == null) return 0f;

        return 0.05f * c.currentLevel; // 0.10 = %10 vs.
    }

    public void AddCardCopies(CardType type, int amount)
    {
        CardDefinition c = GetCard(type);
        if (c == null)
        {
            Debug.LogWarning($"[CardManager] AddCardCopies failed: CardType {type} not found in cards list.");
            return;
        }

        // Check if this is the first time obtaining this card
        bool wasLocked = !c.IsUnlocked;

        c.copiesOwned += amount;
        if (c.copiesOwned < 0) c.copiesOwned = 0;

        // Auto-unlock to level 1 when first obtained (does NOT consume copies)
        if (wasLocked && c.currentLevel == 0 && c.copiesOwned > 0)
        {
            c.currentLevel = 1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CardManager] Card {type} UNLOCKED! Auto-set to Level 1.");
#endif
            ApplyCardEffect(type);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CardManager] +{amount} copies to {type}. Owned now: {c.copiesOwned}. Level: {c.currentLevel}. Unlocked: {c.IsUnlocked}");
#endif

        // Fire event for UI refresh
        OnCardsChanged?.Invoke();

        // UI yenile (direct call as fallback)
        if (CardCollectionUI.Instance != null)
            CardCollectionUI.Instance.Rebuild();
    }

    // ----------------- Notify Methods (Runtime Activation) -----------------

    /// <summary>
    /// [DEPRECATED] Old TurboFinger charge-based activation.
    /// TurboFinger effect is now handled by TurboFingerController.
    /// This method is kept for API compatibility but does nothing.
    /// </summary>
    public void NotifyTap()
    {
        // TurboFinger effect handled by TurboFingerController
        // Old charge-based activation system disabled to prevent double rewards.
    }

    /// <summary>
    /// [DEPRECATED] GarageManager effect is now handled by GarageManagerController.
    /// This method is kept for API compatibility but does nothing.
    /// GarageManagerController tracks spend via CurrencyManager.TrySpendMoney automatically.
    /// </summary>
    public void NotifyPurchase()
    {
        // GarageManager logic moved to GarageManagerController
        // Activation is now spend-based, not purchase-count-based
    }

    /// <summary>
    /// Called when the player collects nitro coins. Tracks rate for NitroMagnet activation.
    /// Also notifies NitroRainController for rain effect.
    /// </summary>
    public void NotifyNitroCollected(int amount = 1)
    {
        // [REMOVED] Legacy NitroMagnet rate-based activation logic.
        // NitroMagnet is now fully handled by NitroMagnetController (tap-based arm/pull).

        // --- NitroRain effect (handled by NitroRainController) ---
        if (NitroRainController.Instance != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int cardLevel = GetCardLevel(CardType.NitroRain);
            Debug.Log($"[CardManager] NotifyNitroCollected({amount}) → NitroRainController.OnNitroCollected | NitroRain card level: {cardLevel} | Instance: {NitroRainController.Instance != null}");
#endif
            NitroRainController.Instance.OnNitroCollected(amount);
        }
        else if (!_warnedMissingNitroRainController)
        {
            Debug.LogWarning("[CardManager] NitroRainController.Instance is null. NitroRain effect skipped.");
            _warnedMissingNitroRainController = true;
        }

        // --- BoostMode effect (handled by BoostModeController) ---
        // IMPORTANT: Do NOT use FindObjectOfType or create GameObjects during gameplay!
        // BoostModeController should be set up in the scene or created at startup.
        if (BoostModeController.Instance != null)
        {
            BoostModeController.Instance.OnNitroCollected(amount);
        }
        else if (!_warnedMissingBoostController)
        {
            Debug.LogWarning("[CardManager] BoostModeController.Instance is null. BoostMode effect skipped.");
            _warnedMissingBoostController = true;
        }
    }

    // ----------------- TurboFinger Bonus Consumption -----------------

    /// <summary>
    /// [DEPRECATED] Old TurboFinger charge consumption system.
    /// TurboFinger effect is now handled by TurboFingerController (5x multiplier).
    /// This method is kept for API compatibility but always returns 0.
    /// </summary>
    public double ConsumeTurboFingerBonusIfActive()
    {
        // TurboFinger effect handled by TurboFingerController
        // Old charge-based bonus system disabled to prevent double rewards.
        return 0;
    }

    // ----------------- Debug Getters -----------------

    /// <summary>
    /// Returns true if TurboFinger effect is active.
    /// Now delegates to TurboFingerController.
    /// </summary>
    public bool IsTurboFingerActive()
    {
        // TurboFinger effect handled by TurboFingerController
        return TurboFingerController.Instance != null && TurboFingerController.Instance.IsActive;
    }

    /// <summary>
    /// [DEPRECATED] Old charge system no longer used.
    /// TurboFinger effect handled by TurboFingerController.
    /// </summary>
    public int GetTurboFingerChargesRemaining() => 0;

    /// <summary>
    /// [DEPRECATED] GarageManager effect is now handled by GarageManagerController.
    /// Use GarageManagerController.Instance.IsActive instead.
    /// </summary>
    public bool IsGarageManagerActive() => GarageManagerController.Instance != null && GarageManagerController.Instance.IsActive;

    /// <summary>
    /// [DEPRECATED] GarageManager effect is now handled by GarageManagerController.
    /// Use GarageManagerController.Instance.CurrentBonusMps instead.
    /// </summary>
    public double GetGarageManagerMpsBonusIfActive()
    {
        // Redirect to GarageManagerController for backward compatibility
        if (GarageManagerController.Instance != null)
        {
            return GarageManagerController.Instance.CurrentBonusMps;
        }
        return 0;
    }

    /// <summary>
    /// Returns true if NitroMagnet is currently armed.
    /// [REDIRECTED] Now delegates to NitroMagnetController.Instance.IsArmed.
    /// </summary>
    public bool IsNitroMagnetActive()
    {
        return NitroMagnetController.Instance != null && NitroMagnetController.Instance.IsArmed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
