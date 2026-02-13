using UnityEngine;
using System;

/// <summary>
/// SmallInvestmentController – Card A: "Small Investment"
///
/// Refunds a percentage of every money / nitro-coin spend back to the player.
/// The refund itself is guarded so it never re-triggers spend events (no infinite loops).
///
/// ╔════════╦═══════════════╦════════════════════════════════════════════════╗
/// ║ Level  ║ Refund %      ║ Formula                                      ║
/// ╠════════╬═══════════════╬════════════════════════════════════════════════╣
/// ║   1    ║   2 %         ║ base(2) + (1-1)*step(2) = 2                  ║
/// ║   2    ║   4 %         ║ base(2) + (2-1)*step(2) = 4                  ║
/// ║   3    ║   6 %         ║ base(2) + (3-1)*step(2) = 6                  ║
/// ║   4    ║   8 %         ║ base(2) + (4-1)*step(2) = 8                  ║
/// ║   5    ║  10 %         ║ base(2) + (5-1)*step(2) = 10                 ║
/// ║   6    ║  12 %         ║ base(2) + (6-1)*step(2) = 12  (= maxRefund) ║
/// ╚════════╩═══════════════╩════════════════════════════════════════════════╝
///
/// refundPercent(level) = clamp( basePercent + (level-1) * stepPercent, 0, maxRefundPercent )
///
/// Values reasoning:
///   • 2 % at level 1 is a gentle entry – noticeable but not economy-breaking.
///   • Linear +2 % per level is easy to understand and preview.
///   • 12 % at level 6 cap keeps long-term progression healthy.
///
/// Integration:
///   Attach to the same persistent GameObject as CurrencyManager / CardManager.
///   Subscribes to CurrencyManager.OnMoneySpent and OnNitroCoinsSpent.
/// </summary>
public class SmallInvestmentController : MonoBehaviour
{
    public static SmallInvestmentController Instance { get; private set; }

    // ==================== CONFIGURATION ====================

    [Header("Refund Scaling")]
    [Tooltip("Refund % at level 1")]
    [SerializeField] private float basePercent = 2f;

    [Tooltip("Additional refund % per level above 1")]
    [SerializeField] private float stepPercent = 2f;

    [Tooltip("Absolute maximum refund % (hard cap)")]
    [SerializeField] private float maxRefundPercent = 12f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = false;

    // Diagnostic gating: log only when level changes or first refund at new level
    private int _lastLoggedMoneyRefundLevel = -1;
    private int _lastLoggedNitroRefundLevel = -1;

    // ==================== EVENTS ====================

    /// <summary>
    /// Fired after a refund is applied. UI can display a "+refund" floating text.
    /// Parameters: refundedAmount, currencyType ("Money" or "NitroCoins")
    /// </summary>
    public event Action<double, string> OnRefundApplied;

    // ==================== LIFECYCLE ====================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void OnEnable()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnMoneySpent += HandleMoneySpent;
            CurrencyManager.Instance.OnNitroCoinsSpent += HandleNitroCoinsSpent;
        }
        else
        {
            // CurrencyManager might not exist yet; subscribe later
            SaveSystem.OnGameLoaded += LateSubscribe;
        }
    }

    private void OnDisable()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnMoneySpent -= HandleMoneySpent;
            CurrencyManager.Instance.OnNitroCoinsSpent -= HandleNitroCoinsSpent;
        }
        SaveSystem.OnGameLoaded -= LateSubscribe;
    }

    private void LateSubscribe()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnMoneySpent -= HandleMoneySpent; // prevent double-sub
            CurrencyManager.Instance.OnNitroCoinsSpent -= HandleNitroCoinsSpent;
            CurrencyManager.Instance.OnMoneySpent += HandleMoneySpent;
            CurrencyManager.Instance.OnNitroCoinsSpent += HandleNitroCoinsSpent;
        }
    }

    // ==================== CORE LOGIC ====================

    /// <summary>
    /// Returns the refund percentage for a given card level (1..6).
    /// Returns 0 if the card is locked (level 0).
    /// </summary>
    public float GetRefundPercent(int level)
    {
        if (level <= 0) return 0f;
        float pct = basePercent + (level - 1) * stepPercent;
        return Mathf.Clamp(pct, 0f, maxRefundPercent);
    }

    /// <summary>
    /// Returns the current effective refund percentage based on the player's card level.
    /// </summary>
    public float CurrentRefundPercent
    {
        get
        {
            int level = CardManager.Instance != null
                ? CardManager.Instance.GetCardLevel(CardType.SmallInvestment)
                : 0;
            return GetRefundPercent(level);
        }
    }

    // ==================== REFUND HANDLERS ====================
    // ROUNDING STANDARD:
    //   • Money (double): use Math.Floor(...) to truncate fractional cents (NEVER round up)
    //   • NitroCoins (int): use Mathf.FloorToInt(...) for consistent truncation
    //   • Refunds must NOT trigger spend events → guarded by IsApplyingRefund flag

    private void HandleMoneySpent(double amountSpent)
    {
        int level = GetCardLevel();
        if (level <= 0) return;

        float pct = GetRefundPercent(level);
        // Integer-safe: floor the refund so we never grant fractional currency
        double refund = System.Math.Floor(amountSpent * pct / 100.0);
        if (refund <= 0) return;

        ApplyMoneyRefund(refund);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log($"[SmallInvestment] Money refund: spent={amountSpent:F0}, pct={pct}%, refund={refund:F0}");
#endif

        // [CardEffectApplied] Diagnostic: log when refund is granted at new level or first time
        if (level != _lastLoggedMoneyRefundLevel)
        {
            Debug.Log($"[CardEffectApplied] SmallInvestment L{level}: MONEY refund granted. spent={amountSpent:F0}, pct={pct:F1}%, refund={refund:F0}, context=OnMoneySpent");
            _lastLoggedMoneyRefundLevel = level;
        }

        OnRefundApplied?.Invoke(refund, "Money");
    }

    private void HandleNitroCoinsSpent(int amountSpent)
    {
        int level = GetCardLevel();
        if (level <= 0) return;

        float pct = GetRefundPercent(level);
        int refund = Mathf.FloorToInt(amountSpent * pct / 100f);
        if (refund <= 0) return;

        ApplyNitroRefund(refund);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (verboseLogs)
            Debug.Log($"[SmallInvestment] NitroCoin refund: spent={amountSpent}, pct={pct}%, refund={refund}");
#endif

        // [CardEffectApplied] Diagnostic: log when refund is granted at new level or first time
        if (level != _lastLoggedNitroRefundLevel)
        {
            Debug.Log($"[CardEffectApplied] SmallInvestment L{level}: NITROCOIN refund granted. spent={amountSpent}, pct={pct:F1}%, refund={refund}, context=OnNitroCoinsSpent");
            _lastLoggedNitroRefundLevel = level;
        }

        OnRefundApplied?.Invoke(refund, "NitroCoins");
    }

    // ==================== REFUND APPLICATION ====================

    /// <summary>
    /// Adds money back to the player with the refund guard enabled.
    /// The guard prevents CurrencyManager from firing OnMoneySpent for this add,
    /// which would otherwise create an infinite refund loop.
    /// </summary>
    private void ApplyMoneyRefund(double amount)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return;

        cm.IsApplyingRefund = true;
        cm.AddMoney(amount, "SmallInvestmentRefund");
        cm.IsApplyingRefund = false;
    }

    /// <summary>
    /// Adds nitro coins back with the refund guard enabled.
    /// </summary>
    private void ApplyNitroRefund(int amount)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return;

        cm.IsApplyingRefund = true;
        cm.AddNitroCoins(amount);
        cm.IsApplyingRefund = false;
    }

    // ==================== HELPERS ====================

    private int GetCardLevel()
    {
        return CardManager.Instance != null
            ? CardManager.Instance.GetCardLevel(CardType.SmallInvestment)
            : 0;
    }
}
