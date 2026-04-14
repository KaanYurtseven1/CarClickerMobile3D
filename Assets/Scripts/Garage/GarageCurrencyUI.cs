// ════════════════════════════════════════════════════════════════
// GarageCurrencyUI.cs – Syncs & displays real Gold and Nitro
// currencies in the Garage scene with animated counting.
//
// Attach to any GO in the NewGarage scene.
// Assign GoldText and NitroText TMP_Text references in Inspector.
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using TMPro;
using DG.Tweening;

public class GarageCurrencyUI : MonoBehaviour
{
    public static GarageCurrencyUI Instance { get; private set; }

    [Header("─── Text References ───")]
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text nitroText;

    [Header("─── Animation ───")]
    [Tooltip("Min duration (seconds) for the counting animation.")]
    [SerializeField] private float minCountDuration = 0.3f;
    [Tooltip("Max duration (seconds) for the counting animation.")]
    [SerializeField] private float maxCountDuration = 1.2f;

    // Displayed values (for animation)
    private double _displayedGold;
    private int _displayedNitro;

    // Tween references
    private Tween _goldTween;
    private Tween _nitroTween;

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Snap to current real values on scene load
        if (CurrencyManager.Instance != null)
        {
            _displayedGold = CurrencyManager.Instance.money;
            _displayedNitro = CurrencyManager.Instance.nitroCoins;
        }
        UpdateGoldTextImmediate();
        UpdateNitroTextImmediate();
    }

    private void OnDestroy()
    {
        _goldTween?.Kill();
        _nitroTween?.Kill();
        if (Instance == this) Instance = null;
    }

    // ══════════════════ Public API ══════════════════

    /// <summary>Animates gold display down (or up) to the current real value.</summary>
    public void RefreshGold()
    {
        if (CurrencyManager.Instance == null) return;
        double target = CurrencyManager.Instance.money;
        AnimateGoldTo(target);
    }

    /// <summary>Animates nitro display down (or up) to the current real value.</summary>
    public void RefreshNitro()
    {
        if (CurrencyManager.Instance == null) return;
        int target = CurrencyManager.Instance.nitroCoins;
        AnimateNitroTo(target);
    }

    /// <summary>Refreshes both currencies.</summary>
    public void RefreshAll()
    {
        RefreshGold();
        RefreshNitro();
    }

    // ══════════════════ Animation ══════════════════

    private void AnimateGoldTo(double target)
    {
        _goldTween?.Kill();

        double start = _displayedGold;
        double diff = System.Math.Abs(target - start);
        float duration = CalculateDuration(diff);

        _goldTween = DOTween.To(
            () => _displayedGold,
            x =>
            {
                _displayedGold = x;
                SetGoldText(x);
            },
            target,
            duration
        ).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void AnimateNitroTo(int target)
    {
        _nitroTween?.Kill();

        int start = _displayedNitro;
        double diff = System.Math.Abs(target - start);
        float duration = CalculateDuration(diff);

        // Use a float tween and round for display
        float floatStart = start;
        float floatTarget = target;
        _nitroTween = DOTween.To(
            () => floatStart,
            x =>
            {
                _displayedNitro = Mathf.RoundToInt(x);
                SetNitroText(_displayedNitro);
                floatStart = x; // keep getter in sync
            },
            floatTarget,
            duration
        ).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(() =>
        {
            _displayedNitro = target;
            SetNitroText(target);
        });
    }

    /// <summary>Calculates an appropriate animation duration based on difference magnitude.</summary>
    private float CalculateDuration(double diff)
    {
        if (diff <= 0) return 0f;
        // Logarithmic scaling: small amounts → slower, large → faster (but still visible)
        float t = Mathf.Clamp01((float)(System.Math.Log10(diff + 1) / 10.0));
        return Mathf.Lerp(minCountDuration, maxCountDuration, t);
    }

    // ══════════════════ Text Helpers ══════════════════

    private void SetGoldText(double value)
    {
        if (goldText != null)
            goldText.text = FormatNumber(value);
    }

    private void SetNitroText(int value)
    {
        if (nitroText != null)
            nitroText.text = value.ToString("N0");
    }

    private void UpdateGoldTextImmediate()
    {
        SetGoldText(_displayedGold);
    }

    private void UpdateNitroTextImmediate()
    {
        SetNitroText(_displayedNitro);
    }

    /// <summary>Compact number formatting (K, M, B, T etc.) for large gold values.</summary>
    private static string FormatNumber(double value)
    {
        if (value < 0) return "-" + FormatNumber(-value);

        if (value < 1_000) return value.ToString("F0");
        if (value < 1_000_000) return (value / 1_000).ToString("F1") + "K";
        if (value < 1_000_000_000) return (value / 1_000_000).ToString("F2") + "M";
        if (value < 1_000_000_000_000) return (value / 1_000_000_000).ToString("F2") + "B";
        return (value / 1_000_000_000_000).ToString("F2") + "T";
    }
}
