using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;
using DG.Tweening;
using System;

public class CurrencyUI : MonoBehaviour
{
    public static CurrencyUI Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [Header("Money & Income")]
    public TextMeshProUGUI textMoneyBig;   // ortadaki büyük money
    public TextMeshProUGUI textMPSLine;    // "31.4 $ / second"
    public TextMeshProUGUI textMPTLine;    // "MPT: 1 / tap"

    [Header("Premium & Rank")]
    public TextMeshProUGUI textPremium;    // elmas sayısı
    public TextMeshProUGUI textRank;       // şimdilik placeholder: "Rank: --"

    [Header("Boost UI")]
    public Button btnBoostX2;
    public TextMeshProUGUI textBoostLabel; // "BOOST x2" vs.
    public Slider boostSlider;             // dolan bar

    [Header("Update Settings")]
    public float uiUpdateInterval = 0.1f; // saniyede 10 kez UI yenile
    private float uiTimer = 0f;

    // cache
    private long lastMoneyInt = long.MinValue;
    private double lastMps = -1;   // NaN DEĞİL
    private double lastMpt = -1;   // NaN DEĞİL
    private int lastPremium = int.MinValue;
    private float lastBoostProgress = -1f;
    private float lastBoostMultiplier = -1f;
    private bool lastBoostActive = false;
    private bool rankInitialized = false;

    private double currentMpsDisplay = 0;
    private bool mpsInitialized = false;

    private static readonly CultureInfo moneyCulture = new CultureInfo("en-US");

    // Penalty animation state
    private bool _isPenaltyAnimating;
    private Color _moneyDefaultColor;
    private bool _moneyDefaultColorCaptured;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[CurrencyUI] Duplicate detected (ID={GetInstanceID()}) — destroying.");
#endif
            Destroy(gameObject);
            return;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CurrencyUI] Instance set (ID={GetInstanceID()})");
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Kill all lingering DOTweens to prevent callbacks into dead objects
        DOTween.Kill("MoneySuccessBounce");
        DOTween.Kill("MoneyPenaltyAnim");
        DOTween.Kill("MoneyPenaltyBlink");
        DOTween.Kill("MoneyPenaltyPunch");
        DOTween.Kill("MoneySuccessRewardAnim");
        DOTween.Kill("MoneySuccessRewardBlink");
        DOTween.Kill("MoneyBufferedApplyAnim");
        DOTween.Kill("MoneyBufferedApplyPunch");

        // Reset animation state so next session starts clean
        _isPenaltyAnimating = false;
    }

    private void Start()
    {
        if (btnBoostX2 != null)
            btnBoostX2.onClick.AddListener(OnClickBoostX2);

        // Set rank text once (static placeholder until backend exists)
        if (textRank != null)
        {
            textRank.text = "#Rank:";
            rankInitialized = true;
        }

        // Capture default money text color once
        if (textMoneyBig != null && !_moneyDefaultColorCaptured)
        {
            _moneyDefaultColor = textMoneyBig.color;
            _moneyDefaultColorCaptured = true;
        }
    }

    private void Update()
    {
        if (CurrencyManager.Instance == null) return;
        var cm = CurrencyManager.Instance;

        uiTimer += Time.unscaledDeltaTime;
        if (uiTimer < uiUpdateInterval)
            return; // her frame değil, ~0.1 sn'de bir UI güncelle
        uiTimer = 0f;

        // --- Money ---
        // Skip normal money display update while penalty animation is running
        // (the animation drives the text directly).
        if (!_isPenaltyAnimating && !cm.suppressTopBarMoneyUpdates)
        {
            long moneyInt = (long)cm.money;
            if (moneyInt != lastMoneyInt)
            {
                lastMoneyInt = moneyInt;
                if (textMoneyBig != null)
                {
                    string formatted = cm.money.ToString("N0", moneyCulture);
                    textMoneyBig.text = formatted + " $";
                }

            } // end suppress guard
        }

        // --- MPS (tap + auto + boost) ---
        // !!! SADECE BU SATIRI DEĞİŞTİRDİK !!!
        double targetMps = cm.GetDisplayedMPS();   // son pencerenin toplam kazancı / süre

        if (!mpsInitialized)
        {
            // İlk defa → direkt hedefe zıpla (ekran "0" gösterip sonradan fırlamasın)
            currentMpsDisplay = targetMps;
            lastMps = currentMpsDisplay;
            mpsInitialized = true;

            if (textMPSLine != null)
            {
                string formatted = currentMpsDisplay.ToString("N1", moneyCulture);
                textMPSLine.text = formatted + " per second";
            }
        }
        else
        {
            double diff = targetMps - currentMpsDisplay;

            if (System.Math.Abs(diff) > 0.01)
            {
                double step;

                // 🔹 Küçük farklarda (|diff| <= 5) → 1'er 1'er kay
                if (System.Math.Abs(diff) <= 5.0)
                {
                    step = System.Math.Sign(diff) * 1.0;
                }
                else
                {
                    // 🔹 Büyük farklarda hızlı yaklaş (örn. %30'u kadar)
                    step = diff * 0.3;
                }

                // Overshoot engelle
                if (System.Math.Abs(step) > System.Math.Abs(diff))
                    step = diff;

                currentMpsDisplay += step;
            }
            else
            {
                currentMpsDisplay = targetMps;
            }

            // UI'yi gerekirse güncelle
            if (System.Math.Abs(currentMpsDisplay - lastMps) > 0.0001)
            {
                lastMps = currentMpsDisplay;
                if (textMPSLine != null)
                {
                    string formatted = currentMpsDisplay.ToString("N1", moneyCulture);
                    textMPSLine.text = formatted + " per second";
                }
            }
        }

        // --- MPT (with TurboFinger multiplier) ---
        float turboMultiplier = TurboFingerController.Instance != null
            ? TurboFingerController.Instance.CurrentMultiplier
            : 1f;
        double effectiveMpt = cm.moneyPerTap * turboMultiplier;

        // Update if base MPT changed OR multiplier changed
        if (System.Math.Abs(effectiveMpt - lastMpt) > 0.0001)
        {
            lastMpt = effectiveMpt;
            if (textMPTLine != null)
            {
                if (turboMultiplier > 1f)
                {
                    textMPTLine.text = "MPT: " + FormatNumber(effectiveMpt) + " / tap (x" + turboMultiplier + ")";
                }
                else
                {
                    textMPTLine.text = "MPT: " + FormatNumber(effectiveMpt) + " / tap";
                }
            }
        }

        // --- Premium (elmas) ---
        if (cm.nitroCoins != lastPremium)
        {
            lastPremium = cm.nitroCoins;
            if (textPremium != null)
                textPremium.text = lastPremium.ToString();
        }

        // --- Rank (set once in Start, no per-frame update needed) ---
        if (!rankInitialized && textRank != null)
        {
            textRank.text = "#Rank:";
            rankInitialized = true;
        }

        // --- Boost bar & label ---
        float boostProgress = cm.GetBoostProgress01();
        if (boostSlider != null && Mathf.Abs(boostProgress - lastBoostProgress) > 0.001f)
        {
            boostSlider.value = boostProgress;
            lastBoostProgress = boostProgress;
        }

        // Only update boost label if state changed
        if (textBoostLabel != null)
        {
            bool boostActive = cm.GetBoostRemaining() > 0f && cm.incomeBoostMultiplier > 1f;
            float boostMult = cm.incomeBoostMultiplier;

            // Check if boost state or multiplier changed
            if (boostActive != lastBoostActive ||
                (boostActive && Mathf.Abs(boostMult - lastBoostMultiplier) > 0.001f))
            {
                lastBoostActive = boostActive;
                lastBoostMultiplier = boostMult;

                if (boostActive)
                {
                    textBoostLabel.text = "BOOST x" + boostMult.ToString("0.#");
                }
                else
                {
                    textBoostLabel.text = "BOOST x2";
                    lastBoostMultiplier = -1f; // Reset cache when boost inactive
                }
            }
        }
    }

    // Büyük sayıları kısalt: 1.2K, 3.4M, 5.6B, 7.8T...
    private string FormatNumber(double value)
    {
        double abs = System.Math.Abs(value);

        if (abs >= 1_000_000_000_000)
            return (value / 1_000_000_000_000d).ToString("0.##") + "T"; // Trillion
        if (abs >= 1_000_000_000)
            return (value / 1_000_000_000d).ToString("0.##") + "B";
        if (abs >= 1_000_000)
            return (value / 1_000_000d).ToString("0.##") + "M";
        if (abs >= 1_000)
            return (value / 1_000d).ToString("0.##") + "K";

        return value.ToString("0");
    }

    // --- BOOST x2 butonu (şimdilik test amaçlı, reklamsız) ---
    private void OnClickBoostX2()
    {
        if (CurrencyManager.Instance == null) return;

        // TODO: Reklam izleme bittikten sonra burayı çağıracaksın.
        CurrencyManager.Instance.ActivateBoost(60f, 2f); // 60 sn x2
    }

    // ==================== POLICE CATCH FEEDBACK ====================

    /// <summary>
    /// Brief scale-punch on TextMoneyBig to celebrate a successful escape.
    /// </summary>
    public void PlaySuccessBounce()
    {
        if (textMoneyBig == null) return;
        Debug.Log("[CurrencyUI] Success bounce on TextMoneyBig");
        DOTween.Kill("MoneySuccessBounce");
        textMoneyBig.rectTransform.localScale = Vector3.one;
        textMoneyBig.rectTransform.DOPunchScale(Vector3.one * 0.15f, 0.4f, 8, 0.5f)
            .SetId("MoneySuccessBounce");
    }

    /// <summary>
    /// Animates the displayed money value counting down from fromValue to toValue,
    /// blinks TextMoneyBig red, hides MPS/MPT text during the animation,
    /// then restores everything and calls onComplete.
    /// The real money field is already set before calling this — this is purely visual.
    /// </summary>
    public void PlayPenaltyAnimation(double fromValue, double toValue, float duration, Action onComplete)
    {
        if (textMoneyBig == null)
        {
            Debug.LogWarning("[CurrencyUI] PlayPenaltyAnimation: textMoneyBig is null, skipping.");
            onComplete?.Invoke();
            return;
        }

        Debug.Log($"[CurrencyUI] Penalty anim START from {fromValue:F0} to {toValue:F0} over {duration}s");
        _isPenaltyAnimating = true;

        // Capture default color if not already
        if (!_moneyDefaultColorCaptured)
        {
            _moneyDefaultColor = textMoneyBig.color;
            _moneyDefaultColorCaptured = true;
        }

        // Hide MPS/MPT during animation (null-safe: MPT may not be assigned in Inspector)
        if (textMPSLine != null) textMPSLine.alpha = 0f;
        if (textMPTLine != null) textMPTLine.alpha = 0f;

        // Kill any prior money animation tweens by ID
        DOTween.Kill("MoneyPenaltyAnim");
        DOTween.Kill("MoneyPenaltyBlink");
        DOTween.Kill("MoneyPenaltyPunch");

        // Red blink on money text (2 blinks over duration)
        Sequence blink = DOTween.Sequence().SetId("MoneyPenaltyBlink");
        float blinkHalf = duration * 0.25f;
        blink.Append(textMoneyBig.DOColor(Color.red, blinkHalf));
        blink.Append(textMoneyBig.DOColor(_moneyDefaultColor, blinkHalf));
        blink.Append(textMoneyBig.DOColor(Color.red, blinkHalf));
        blink.Append(textMoneyBig.DOColor(_moneyDefaultColor, blinkHalf));

        // Subtle scale punch for penalty feel
        textMoneyBig.rectTransform.localScale = Vector3.one;
        textMoneyBig.rectTransform.DOPunchScale(Vector3.one * 0.1f, duration, 6, 0.5f)
            .SetId("MoneyPenaltyPunch");

        // Count-down tween: animate a float from 0→1, lerp fromValue→toValue
        DOTween.To(() => 0f, t =>
        {
            double displayValue = fromValue + (toValue - fromValue) * t;
            if (textMoneyBig != null)
            {
                string formatted = displayValue.ToString("N0", moneyCulture);
                textMoneyBig.text = formatted + " $";
            }
        }, 1f, duration)
        .SetEase(Ease.InOutSine)
        .SetId("MoneyPenaltyAnim")
        .OnComplete(() =>
        {
            Debug.Log("[CurrencyUI] Penalty anim END");

            // Final snap
            if (textMoneyBig != null)
            {
                string formatted = toValue.ToString("N0", moneyCulture);
                textMoneyBig.text = formatted + " $";
                textMoneyBig.color = _moneyDefaultColor;
                textMoneyBig.rectTransform.localScale = Vector3.one;
            }

            // MPS/MPT stay hidden; PlayBufferedEarningsApplyAnimation will restore them.
            // _isPenaltyAnimating stays true; cleared by PlayBufferedEarningsApplyAnimation.

            // Invalidate cache so normal Update picks up new value
            lastMoneyInt = long.MinValue;

            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Animates money counting UP from fromValue to toValue with a green blink + bounce.
    /// Used after a successful police escape to show the reward.
    /// MPS/MPT hidden during animation; PlayBufferedEarningsApplyAnimation restores them.
    /// </summary>
    public void PlaySuccessRewardAnimation(double fromValue, double toValue, float duration, Action onComplete)
    {
        if (textMoneyBig == null)
        {
            Debug.LogWarning("[CurrencyUI] PlaySuccessRewardAnimation: textMoneyBig is null, skipping.");
            onComplete?.Invoke();
            return;
        }

        Debug.Log($"[CurrencyUI] Success reward anim START from {fromValue:F0} to {toValue:F0} over {duration}s");
        _isPenaltyAnimating = true; // reuse guard to prevent Update overwrite

        // Capture default color if not already
        if (!_moneyDefaultColorCaptured)
        {
            _moneyDefaultColor = textMoneyBig.color;
            _moneyDefaultColorCaptured = true;
        }

        // Hide MPS/MPT during animation (null-safe)
        if (textMPSLine != null) textMPSLine.alpha = 0f;
        if (textMPTLine != null) textMPTLine.alpha = 0f;

        // Kill any prior money animation tweens
        DOTween.Kill("MoneySuccessBounce");
        DOTween.Kill("MoneySuccessRewardAnim");
        DOTween.Kill("MoneySuccessRewardBlink");

        // Green blink on money text (2 blinks over duration)
        Color successGreen = new Color(0.1f, 0.85f, 0.2f, 1f);
        Sequence blink = DOTween.Sequence().SetId("MoneySuccessRewardBlink");
        float blinkHalf = duration * 0.25f;
        blink.Append(textMoneyBig.DOColor(successGreen, blinkHalf));
        blink.Append(textMoneyBig.DOColor(_moneyDefaultColor, blinkHalf));
        blink.Append(textMoneyBig.DOColor(successGreen, blinkHalf));
        blink.Append(textMoneyBig.DOColor(_moneyDefaultColor, blinkHalf));

        // Scale bounce
        textMoneyBig.rectTransform.localScale = Vector3.one;
        textMoneyBig.rectTransform.DOPunchScale(Vector3.one * 0.15f, duration, 8, 0.5f)
            .SetId("MoneySuccessBounce");

        // Count-up tween: animate from fromValue to toValue
        DOTween.To(() => 0f, t =>
        {
            double displayValue = fromValue + (toValue - fromValue) * t;
            if (textMoneyBig != null)
            {
                string formatted = displayValue.ToString("N0", moneyCulture);
                textMoneyBig.text = formatted + " $";
            }
        }, 1f, duration)
        .SetEase(Ease.OutCubic)
        .SetId("MoneySuccessRewardAnim")
        .OnComplete(() =>
        {
            Debug.Log("[CurrencyUI] Success reward anim END");

            if (textMoneyBig != null)
            {
                string formatted = toValue.ToString("N0", moneyCulture);
                textMoneyBig.text = formatted + " $";
                textMoneyBig.color = _moneyDefaultColor;
                textMoneyBig.rectTransform.localScale = Vector3.one;
            }

            // MPS/MPT stay hidden; PlayBufferedEarningsApplyAnimation will restore them.
            // _isPenaltyAnimating stays true; cleared by PlayBufferedEarningsApplyAnimation.
            lastMoneyInt = long.MinValue;
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Animates TextMoneyBig counting up from startValue to startValue + bufferedAmount.
    /// Small bounce, no color blink. Restores MPS/MPT and clears _isPenaltyAnimating on completion.
    /// Used to visually apply buffered earnings after penalty/reward animation.
    /// </summary>
    public void PlayBufferedEarningsApplyAnimation(double startValue, double bufferedAmount, Action onComplete)
    {
        double endValue = startValue + bufferedAmount;

        if (textMoneyBig == null || bufferedAmount <= 0)
        {
            Debug.Log("[CurrencyUI] BufferedApply SKIP (null text or zero amount)");
            // Restore state even when skipping
            if (textMPSLine != null) textMPSLine.alpha = 1f;
            if (textMPTLine != null) textMPTLine.alpha = 1f;
            lastMoneyInt = long.MinValue;
            _isPenaltyAnimating = false;
            onComplete?.Invoke();
            return;
        }

        Debug.Log($"[CurrencyUI] BufferedApply START amount={bufferedAmount:F0}");

        // Kill prior tweens
        DOTween.Kill("MoneyBufferedApplyAnim");
        DOTween.Kill("MoneyBufferedApplyPunch");

        // Subtle bounce
        textMoneyBig.rectTransform.localScale = Vector3.one;
        textMoneyBig.rectTransform.DOPunchScale(Vector3.one * 0.08f, 0.5f, 6, 0.5f)
            .SetId("MoneyBufferedApplyPunch");

        // Count-up tween
        DOTween.To(() => 0f, t =>
        {
            double displayValue = startValue + bufferedAmount * t;
            if (textMoneyBig != null)
            {
                string formatted = displayValue.ToString("N0", moneyCulture);
                textMoneyBig.text = formatted + " $";
            }
        }, 1f, 0.5f)
        .SetEase(Ease.OutSine)
        .SetId("MoneyBufferedApplyAnim")
        .OnComplete(() =>
        {
            Debug.Log($"[CurrencyUI] BufferedApply END amount={bufferedAmount:F0}");

            if (textMoneyBig != null)
            {
                string formatted = endValue.ToString("N0", moneyCulture);
                textMoneyBig.text = formatted + " $";
                textMoneyBig.rectTransform.localScale = Vector3.one;
            }

            // Restore MPS/MPT
            if (textMPSLine != null) textMPSLine.alpha = 1f;
            if (textMPTLine != null) textMPTLine.alpha = 1f;

            lastMoneyInt = long.MinValue;
            _isPenaltyAnimating = false;
            onComplete?.Invoke();
        });
    }
}
