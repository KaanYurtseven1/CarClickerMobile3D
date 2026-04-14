using UnityEngine;
using System;
using System.Collections;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        _loadWatcherActive = false;
        _loadWatcherFrames = 0;
    }

    // ---- Post-load money watcher (diagnostic) ----
    private static bool _loadWatcherActive;
    private static int _loadWatcherFrames;
    private static double _loadWatcherLastMoney;
    private const int LOAD_WATCHER_MAX_FRAMES = 180; // ~3 seconds at 60 fps

    // ---- Spend Events (for SmallInvestment card refund) ----
    // Fired AFTER a successful spend. Listeners must NOT re-trigger spends.
    // Parameters: amountSpent
    /// <summary>Fired after money is successfully spent. Used by SmallInvestment refund.</summary>
    public event Action<double> OnMoneySpent;
    /// <summary>Fired after nitro coins are successfully spent. Used by SmallInvestment refund.</summary>
    public event Action<int> OnNitroCoinsSpent;

    /// <summary>
    /// Guard flag: true while a refund is being applied.
    /// Prevents the refund itself from triggering another OnMoneySpent event (infinite loop prevention).
    /// Internal setter prevents accidental external writes; SmallInvestmentController can set it.
    /// </summary>
    [NonSerialized] private bool _isApplyingRefund;
    internal bool IsApplyingRefund
    {
        get => _isApplyingRefund;
        set => _isApplyingRefund = value;
    }

    [Header("Current Values")]
    [SerializeField] private double _money = 0;
    public double moneyPerTap = 1;
    public double moneyPerSecond = 0;

    /// <summary>
    /// Use this property for ALL reads/writes to money.
    /// Every write is logged during the post-load watcher window.
    /// </summary>
    public double money
    {
        get => _money;
        set
        {
            double prev = _money;
            _money = value;
            if (_loadWatcherActive && Math.Abs(value - prev) > 0.5)
            {
                // Capture caller info via stack trace (expensive but only for a few seconds)
                string caller = new StackTrace(1, true).GetFrame(0)?.ToString() ?? "unknown";
                Debug.LogWarning($"[MoneyWatcher] money changed: {prev:F2} -> {value:F2} (delta={value - prev:F2}) caller={caller}");
            }
        }
    }

    [Header("Meta")]
    public double totalMoneyEarned = 0;   // sadece kazanılan para, harcamadan etkilenmez

    [Header("Premium Currency")]
    public int premiumCurrency = 0;       // elmas sayısı

    [Header("Boost")]
    [Tooltip("Gelire çarpilan boost katsayisi. 1 = normal, 2 = x2, 10 = x10 vs.")]
    public float incomeBoostMultiplier = 1f;
    private float boostTimer = 0f;
    private float boostDuration = 0f;

    // Pasif geliri "tam sayıya" yuvarlamak için buffer
    private double passiveBuffer = 0;

    [Header("Total MPS Measurement")]
    [Tooltip("MPS ölçümü için pencere (saniye). Örn: 1 = son 1 saniyede kazanılan para.")]
    public float mpsMeasureInterval = 1f;   // <<< İSTERSEN INSPECTOR’DAN DA DEĞİŞTİR
    private float mpsTimer = 0f;

    // totalMoneyEarned üzerinden ölçüm alacağız
    private double lastTotalMoneyForMps = 0;
    private double displayedMps = 0;

    // Offline earnings tracking — excluded from MPS display
    private double _offlineEarningsTotal = 0;
    private double _lastOfflineEarningsForMps = 0;

    [Header("Nitro Coin")]
    public int nitroCoins = 0;

    // Track active money animation to prevent overlap
    private Coroutine _activeMoneyAnimation = null;
    private double _pendingAnimatedAmount = 0.0;

    // ---- Suppress top-bar money display during penalty animation ----
    // When true, AddMoney still tracks totalMoneyEarned but the earned amount
    // is buffered instead of added to 'money'. CurrencyUI should also skip
    // updating the money text. After the penalty animation finishes, call
    // FlushBufferedEarnings() to apply everything at once.
    [NonSerialized] public bool suppressTopBarMoneyUpdates;
    [NonSerialized] public double bufferedEarnings;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // DontDestroyOnLoad only works on ROOT GameObjects.
            // If CurrencyManager is a child (e.g. under "GameManager" under "Managers"), detach first.
            if (transform.parent != null)
            {
                Debug.Log($"[CurrencyManager] Detaching from parent '{transform.parent.name}' for DontDestroyOnLoad.");
                transform.SetParent(null);
            }

            DontDestroyOnLoad(gameObject);
            Debug.Log($"[CurrencyManager] Instance set (ID={GetInstanceID()}), DontDestroyOnLoad applied.");
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"[CurrencyManager] Duplicate detected (ID={GetInstanceID()}) — destroying. Active instance ID={Instance.GetInstanceID()}");
            Destroy(gameObject);
            return;
        }

        // Reset suppress/buffer on fresh start (safety)
        suppressTopBarMoneyUpdates = false;
        bufferedEarnings = 0;

        lastTotalMoneyForMps = totalMoneyEarned;
    }

    // ---- Post-load watcher tick ----
    /// <summary>Call once from SaveSystem after LoadGame to start watching for unexpected money mutations.</summary>
    public void StartLoadWatcher()
    {
        _loadWatcherActive = true;
        _loadWatcherFrames = 0;
        _loadWatcherLastMoney = money;
        Debug.Log($"[MoneyWatcher] Started. money={money:F2}");
    }

    private void LateUpdate()
    {
        if (!_loadWatcherActive) return;
        _loadWatcherFrames++;
        if (_loadWatcherFrames > LOAD_WATCHER_MAX_FRAMES)
        {
            Debug.Log($"[MoneyWatcher] Window closed after {_loadWatcherFrames} frames. Final money={money:F2}");
            _loadWatcherActive = false;
            return;
        }
        if (Math.Abs(money - _loadWatcherLastMoney) > 0.5)
        {
            Debug.LogWarning($"[MoneyWatcher] Frame {_loadWatcherFrames}: money drifted {_loadWatcherLastMoney:F2} -> {money:F2}");
            _loadWatcherLastMoney = money;
        }
    }

    private void Update()
    {
        // 1) BOOST süresi
        if (boostTimer > 0f)
        {
            boostTimer -= Time.deltaTime;
            if (boostTimer <= 0f)
            {
                boostTimer = 0f;
                incomeBoostMultiplier = 1f;
            }
        }

        // 2) Pasif gelir (auto MPS)
        if (moneyPerSecond > 0)
        {
            double baseMps = moneyPerSecond;

            // Add GarageManager bonus if active (now from GarageManagerController)
            double garageBonus = 0;
            if (GarageManagerController.Instance != null)
            {
                garageBonus = GarageManagerController.Instance.CurrentBonusMps;
            }

            double cardMultiplier = 1.0;
            if (CardManager.Instance != null)
            {
                cardMultiplier = CardManager.Instance.GetGlobalMpsMultiplierFromCards();
            }

            double effectiveMPS = (baseMps + garageBonus) * incomeBoostMultiplier * cardMultiplier;

            passiveBuffer += effectiveMPS * Time.deltaTime;

            if (passiveBuffer >= 1.0)
            {
                double delta = System.Math.Floor(passiveBuffer);
                passiveBuffer -= delta;

                // Log for MPS verification
                double baseMpsForLog = baseMps + garageBonus;
                AddMoney(delta, "MPS", baseMpsForLog, incomeBoostMultiplier);
            }
        }

        // 3) TOPLAM MPS ölçümü (tap + auto + boost + chest her şey)
        if (mpsMeasureInterval > 0f)
        {
            mpsTimer += Time.deltaTime;
            if (mpsTimer >= mpsMeasureInterval)
            {
                double earnedDelta = totalMoneyEarned - lastTotalMoneyForMps;   // pencere boyunca kazanılan para

                // Subtract offline earnings that fell within this window so
                // PitStopCrew payouts don't spike the displayed MPS.
                double offlineDelta = _offlineEarningsTotal - _lastOfflineEarningsForMps;
                earnedDelta -= offlineDelta;
                if (earnedDelta < 0) earnedDelta = 0;

                double newMps = earnedDelta / mpsMeasureInterval;              // saniye başına düşen para

                // Buradaki logic sende vardı, aynen bıraktım:
                // Küçük değerlerde direkt güncelle, büyük değerlerde ancak fark büyükse zıpla.
                if (newMps < 20.0 || System.Math.Abs(newMps - displayedMps) >= 5.0)
                {
                    displayedMps = newMps;
                }

                lastTotalMoneyForMps = totalMoneyEarned;
                _lastOfflineEarningsForMps = _offlineEarningsTotal;
                mpsTimer = 0f;
            }
        }
    }

    public void AddMoney(double amount, string source = "", double baseAmount = 0, float appliedMultiplier = 1f)
    {
        // When suppressed (penalty animation), buffer earnings instead of updating money.
        // totalMoneyEarned is ALWAYS updated so MPS measurement stays correct.
        if (suppressTopBarMoneyUpdates)
        {
            bufferedEarnings += amount;
            totalMoneyEarned += amount;
            return;
        }

        money += amount;
        totalMoneyEarned += amount;   // MPS ölçümü bunun üzerinden

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Verification logs for TAP and MPS income
        if (source == "TAP" || source == "MPS")
        {
            bool isBoostActive = BoostModeController.Instance != null && BoostModeController.Instance.IsBoostActive;
            int boostLevel = CardManager.Instance != null ? CardManager.Instance.GetCardLevel(CardType.BoostMode) : -1;
            
            //Debug.Log($"[Income] source={source} | baseAmount={baseAmount:F2} | multiplierApplied={appliedMultiplier:F2} | finalAmountAdded={amount:F2} | isBoostActive={isBoostActive} | boostLevel={boostLevel}");
        }
#endif
    }

    /// <summary>
    /// Adds money with a visible count-up animation over the specified duration.
    /// The real money value increases gradually so UI updates each step.
    /// If called while an animation is running, the new amount is queued and added
    /// to the current animation to prevent overlap bugs.
    /// </summary>
    /// <param name="amount">Total amount to add</param>
    /// <param name="durationSeconds">Duration of the count-up animation</param>
    /// <param name="reason">Optional reason for logging</param>
    public void AddMoneyAnimated(double amount, float durationSeconds, string reason = "")
    {
        if (amount <= 0) return;

        // If duration is too short, just add instantly
        if (durationSeconds <= 0.05f)
        {
            AddMoney(amount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CurrencyManager] AddMoneyAnimated (instant): +{amount:F2} ({reason})");
#endif
            return;
        }

        // If animation is already running, queue this amount into the pending pool
        if (_activeMoneyAnimation != null)
        {
            _pendingAnimatedAmount += amount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CurrencyManager] AddMoneyAnimated QUEUED: +{amount:F2} (pending total: {_pendingAnimatedAmount:F2}) ({reason})");
#endif
            return;
        }

        _activeMoneyAnimation = StartCoroutine(AnimateMoneyAddition(amount, durationSeconds, reason));
    }

    private IEnumerator AnimateMoneyAddition(double totalAmount, float duration, string reason)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CurrencyManager] AddMoneyAnimated START: +{totalAmount:F2} over {duration}s ({reason})");
#endif

        double addedSoFar = 0.0;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Use ease-out curve for satisfying feel
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // Cubic ease-out

            double targetAddedNow = totalAmount * easedT;
            double deltaThisFrame = targetAddedNow - addedSoFar;

            if (deltaThisFrame > 0)
            {
                // Use AddMoney to properly track totalMoneyEarned
                AddMoney(deltaThisFrame);
                addedSoFar = targetAddedNow;
            }

            yield return null;
        }

        // Add any remaining amount (avoid floating point drift)
        double remaining = totalAmount - addedSoFar;
        if (remaining > 0.0001)
        {
            AddMoney(remaining);
        }

        // DO NOT snap money to startMoney + totalAmount!
        // Player may have spent money during animation - we must respect that.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CurrencyManager] AddMoneyAnimated COMPLETE: +{totalAmount:F2} added ({reason})");
#endif

        // Check for pending amounts that were queued during this animation
        _activeMoneyAnimation = null;
        if (_pendingAnimatedAmount > 0)
        {
            double pending = _pendingAnimatedAmount;
            _pendingAnimatedAmount = 0.0;
            // Start new animation for pending amount (use same duration)
            AddMoneyAnimated(pending, duration, "Queued");
        }
    }

    /// <summary>
    /// Ends suppress mode and applies all buffered earnings to money in one go.
    /// Called after the penalty decrease animation finishes.
    /// </summary>
    public void FlushBufferedEarnings()
    {
        suppressTopBarMoneyUpdates = false;
        if (bufferedEarnings > 0)
        {
            money += bufferedEarnings;
            // totalMoneyEarned was already updated when earnings were buffered
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CurrencyManager] Flushed buffered earnings: +{bufferedEarnings:F0}");
#endif
            bufferedEarnings = 0;
        }
    }

    /// <summary>
    /// Commits buffered earnings to money with a log, clears the buffer,
    /// and turns suppress OFF. Called after PlayBufferedEarningsApplyAnimation finishes.
    /// </summary>
    public void CommitBufferedEarnings()
    {
        if (bufferedEarnings > 0)
        {
            money += bufferedEarnings;
            Debug.Log($"[CurrencyManager] Commit buffered earnings: +{bufferedEarnings:F0}");
            bufferedEarnings = 0;
        }
        suppressTopBarMoneyUpdates = false;
    }

    public bool TrySpendMoney(double cost)
    {
        if (money < cost)
            return false;

        money -= cost;

        // Notify GarageManagerController for spend-based activation
        if (GarageManagerController.Instance != null)
        {
            GarageManagerController.Instance.OnMoneySpent(cost);
        }

        // Fire spend event for SmallInvestment refund (skip if this IS a refund)
        if (!IsApplyingRefund)
        {
            OnMoneySpent?.Invoke(cost);
        }

        return true;
    }

    public void IncreaseTapIncome(double delta)
    {
        moneyPerTap += delta;
    }

    public void IncreaseMPS(double delta)
    {
        moneyPerSecond += delta;
    }

    /// <summary>
    /// Sets the building-derived MPS to an exact value (replaces, not adds).
    /// Used after load to ensure economy consistency with building counts.
    /// WARNING: If other systems (upgrades, cards) also contribute to moneyPerSecond,
    /// those must be re-added separately after calling this.
    /// </summary>
    public void SetBuildingMPS(double buildingMPS)
    {
        moneyPerSecond = buildingMPS;
    }

    /// <summary>
    /// Sets the building-derived tap income to an exact value.
    /// Used after load for StreetDeals (formerly AutoClicker) tap bonus.
    /// Base tap (1.0) is preserved separately; this sets only the building bonus portion.
    /// </summary>
    public void SetBuildingTapIncome(double buildingTapIncome)
    {
        // moneyPerTap = baseTap(1) + building bonus + any other bonuses
        // On fresh load, moneyPerTap is restored from PlayerPrefs which already includes
        // the building portion, so we only call this during recalculation.
        // For deterministic recalc: base tap is always 1.0, building portion = buildingTapIncome
        // Other systems (cards, upgrades) must re-add their bonuses after this.
        moneyPerTap = 1.0 + buildingTapIncome;
    }

    // ---- Premium currency (elmas) ----
    public void AddPremium(int amount)
    {
        premiumCurrency += amount;
        if (premiumCurrency < 0) premiumCurrency = 0;
    }

    public bool TrySpendPremium(int amount)
    {
        if (premiumCurrency < amount) return false;
        premiumCurrency -= amount;
        return true;
    }

    // ---- BOOST sistemi (x2, x10 vs.) ----
    public void ActivateBoost(float durationSeconds, float multiplier)
    {
        boostDuration = durationSeconds;
        boostTimer = durationSeconds;
        incomeBoostMultiplier = multiplier;
    }

    /// <summary>
    /// Sets the boost multiplier directly without using the timer system.
    /// Used by BoostModeEffectsIntegration which manages its own timing via BoostModeController.
    /// Pass 1f to reset to normal.
    /// </summary>
    public void SetBoostMultiplier(float multiplier)
    {
        // Clear timer so Update() doesn't reset our multiplier
        boostTimer = 0f;
        boostDuration = 0f;
        incomeBoostMultiplier = multiplier;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CurrencyManager] SetBoostMultiplier({multiplier})");
#endif
    }

    public float GetBoostRemaining()
    {
        return boostTimer;
    }

    public float GetBoostDuration()
    {
        return boostDuration;
    }

    public float GetBoostProgress01()
    {
        if (boostDuration <= 0f) return 0f;
        return Mathf.Clamp01(1f - boostTimer / boostDuration);
    }

    // 🔹 UI'nin kullanacağı MPS (tap + auto + boost hepsi dahil, pencere ortalaması)
    public double GetDisplayedMPS()
    {
        return displayedMps < 0 ? 0 : displayedMps;
    }

    public void ResetMpsAfterLoad()
    {
        lastTotalMoneyForMps = totalMoneyEarned;
        _lastOfflineEarningsForMps = _offlineEarningsTotal;
        mpsTimer = 0f;
        // Seed with known auto-MPS from buildings so the display doesn't
        // start at 0 while waiting for the first measurement window.
        displayedMps = GetAutoMPS();
    }

    /// <summary>
    /// Mark an amount as offline earnings so it is excluded from the
    /// displayed MPS calculation. Call BEFORE granting the money.
    /// </summary>
    public void MarkOfflineEarnings(double amount)
    {
        _offlineEarningsTotal += amount;
    }

    // Bu fonksiyonu başka yerlerde kullanıyorsun diye aynen bıraktım
    public double GetAutoMPS()
    {
        double garageBonus = 0;
        if (GarageManagerController.Instance != null)
            garageBonus = GarageManagerController.Instance.CurrentBonusMps;

        double baseMps = (moneyPerSecond + garageBonus) * incomeBoostMultiplier;
        if (CardManager.Instance != null)
        {
            baseMps *= CardManager.Instance.GetGlobalMpsMultiplierFromCards();
        }
        return baseMps;
    }

    public void AddNitroCoins(int amount)
    {
        nitroCoins += amount;
        if (nitroCoins < 0)
        {
            nitroCoins = 0;
        }
    }

    public bool TrySpendNitroCoins(int amount)
    {
        if (nitroCoins < amount) return false;
        nitroCoins -= amount;

        // Fire spend event for SmallInvestment refund (skip if this IS a refund)
        if (!IsApplyingRefund)
        {
            OnNitroCoinsSpent?.Invoke(amount);
        }

        return true;
    }

    private void OnDestroy()
    {
        // Clear singleton so editor re-play doesn't keep stale instance
        if (Instance == this)
        {
            Instance = null;
        }

        // Reset suppress/buffer state so next session starts clean
        suppressTopBarMoneyUpdates = false;
        bufferedEarnings = 0;

        // Stop any running money animation coroutine
        if (_activeMoneyAnimation != null)
        {
            StopCoroutine(_activeMoneyAnimation);
            _activeMoneyAnimation = null;
        }
        _pendingAnimatedAmount = 0.0;
    }
}
