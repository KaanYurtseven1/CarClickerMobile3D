using UnityEngine;
using TMPro;
using DG.Tweening;
using System;

// ======================================================================
//  DATA   pre-computed reward values (determined once, granted at exit)
// ======================================================================

[System.Serializable]
public class ChestRewardPackage
{
    // ---- Money (multiplier-based) ----
    public double currentMoney;       // player balance BEFORE reward
    public int moneyMultiplier;    // 2, 3, 4 or 5
    public double moneyGained;        // currentMoney * (multiplier - 1)
    public double finalMoneyShown;    // currentMoney * multiplier

    // ---- Nitro (additive) ----
    public int nitroReward;           // from {5,10,15,25,50}
    public int currentNitroCoins;     // player balance BEFORE reward
    public int finalNitroTotal;       // current + reward

    // ---- Card ----
    public CardType cardType;
    public string cardDisplayName;
    public Sprite cardIcon;          // small icon (for UI)
    public Sprite cardArtSprite;     // full card artwork for world reveal card (CardBG)
    public CardRarity cardRarity;
    public int cardCopies;           // segments granted (1, 2, 4 or 8)

    // Post-reward preview (simulated, not yet applied)
    public int postRewardLevel;
    public int postRewardCopiesOwned;
    public int copiesNeededForNext;
    public float upgradeProgress01;

    // ---- Static pools ----
    public static readonly int[] MoneyMultipliers = { 2, 3, 4, 5 };
    public static readonly int[] NitroAmounts = { 5, 10, 15, 25, 50 };

    // ---- Rarity weights (Common most likely, Legendary least) ----
    // indices:  0=Common  1=Rare  2=Epic  3=Legendary
    public static readonly float[] RarityWeights = { 50f, 30f, 15f, 5f };
}

// ======================================================================
//  CONTROLLER   world-space TMP texts + progress + summary
//  NO Canvas UI at all (Canvas is only used for a full-screen Dim overlay
//  which is NOT managed by this script).
// ======================================================================

public class ChestRewardRevealController : MonoBehaviour
{
    //  WORLD-SPACE INFO TEXTS (3D TMP) 
    [Header("World-Space Info Texts")]
    [SerializeField] private TextMeshPro worldTitle;
    [SerializeField] private TextMeshPro worldSubtitle;
    [SerializeField] private TextMeshPro worldValue;

    //  PROGRESS BAR (world-space) 
    [Header("Card Progress (world-space)")]
    [Tooltip("Root GO  set inactive by default; shown only during real card reveal")]
    [SerializeField] private GameObject progressRoot;

    [Tooltip("SpriteRenderer whose localScale.x is set to fill01")]
    [SerializeField] private SpriteRenderer progressFillRenderer;

    [SerializeField] private TextMeshPro progressLevelText;
    [SerializeField] private TextMeshPro progressCopiesText;

    //  SUMMARY (world-space) 
    [Header("Summary (world-space)")]
    [Tooltip("Root GO  set inactive by default")]
    [SerializeField] private GameObject summaryRoot;

    [SerializeField] private TextMeshPro summaryTitleText;

    [Header("Summary Cards (SpriteRenderer + TMP overlay each)")]
    [SerializeField] private SpriteRenderer summaryCard1;   // Money
    [SerializeField] private TextMeshPro summaryOverlay1;
    [SerializeField] private SpriteRenderer summaryCard2;   // Nitro
    [SerializeField] private TextMeshPro summaryOverlay2;
    [SerializeField] private SpriteRenderer summaryCard3;   // Real Card
    [SerializeField] private TextMeshPro summaryOverlay3;

    //  REWARD ICON SPRITES 
    [Header("Reward Icon Sprites")]
    [SerializeField] private Sprite moneySprite;
    [SerializeField] private Sprite nitroSprite;

    //  ANIMATION TUNING 
    [Header("Animation")]
    [SerializeField] private float textFadeDuration = 0.25f;
    [SerializeField] private float crossfadeDuration = 0.20f;
    [SerializeField] private float summaryFadeDuration = 0.30f;

    [Header("Summary Card Size Normalization")]
    [Tooltip("Target world-space height (in world units) that every summary SpriteRenderer should occupy. \n"
           + "All 3 summary cards (Money, Nitro, Card) are scaled so their SpriteRenderer renders at \n"
           + "exactly this height, eliminating the size difference caused by different sprite dimensions / PPU. \n"
           + "Calibration: run in Play Mode, read the '[RewardReveal] SummaryCard' logs for summaryCard1 \n"
           + "(Money) and copy its 'bounds.y' value here. Set to 0 to disable normalization.")]
    [SerializeField] private float summaryCardWorldHeight = 1.0f;

    //  RUNTIME 
    private ChestRewardPackage _rewards;

    public Sprite MoneySprite => moneySprite;
    public Sprite NitroSprite => nitroSprite;

    // 
    //  LIFECYCLE
    // 

    private void Awake() { HideAll(); }
    private void OnDestroy() { DOTween.Kill(this); }

    // 
    //  PUBLIC API
    // 

    public void Initialize(ChestRewardPackage rewards)
    {
        _rewards = rewards;
        HideAll();
        Debug.Log("[RewardReveal] Initialized.");
    }

    //  MONEY 

    public void ShowMoneyInfo(Action onComplete)
    {
        if (_rewards == null) { Debug.LogError("[RewardReveal] _rewards is null!"); onComplete?.Invoke(); return; }
        Debug.Log($"[RewardReveal] ShowMoneyInfo {_rewards.moneyMultiplier}x => {_rewards.finalMoneyShown:N0}");
        SetWorldTexts("Money", "Resource Card", FormatMoney(_rewards.finalMoneyShown));
        HideProgress();
        FadeInWorldTexts(onComplete);
    }

    //  NITRO 

    public void ShowNitroInfo(Action onComplete)
    {
        if (_rewards == null) { Debug.LogError("[RewardReveal] _rewards is null!"); onComplete?.Invoke(); return; }
        Debug.Log($"[RewardReveal] ShowNitroInfo +{_rewards.nitroReward} => {_rewards.finalNitroTotal}");
        SetWorldTexts("Nitro Coin", "Resource Card", $"+{_rewards.nitroReward} Nitro Coins");
        HideProgress();
        FadeInWorldTexts(onComplete);
    }

    //  REAL CARD 

    public void ShowCardInfo(Action onComplete)
    {
        if (_rewards == null) { Debug.LogError("[RewardReveal] _rewards is null!"); onComplete?.Invoke(); return; }
        Debug.Log($"[RewardReveal] ShowCardInfo {_rewards.cardDisplayName} x{_rewards.cardCopies}");

        string sub = _rewards.cardRarity != 0 ? $"{_rewards.cardRarity} Card" : "Card";
        SetWorldTexts(_rewards.cardDisplayName, sub, "");

        ShowProgress(
            _rewards.postRewardLevel,
            _rewards.postRewardCopiesOwned,
            _rewards.copiesNeededForNext,
            _rewards.upgradeProgress01
        );
        FadeInWorldTexts(onComplete);
    }

    //  FADE OUT 

    public void FadeOutInfoTexts(Action onComplete)
    {
        Debug.Log("[RewardReveal] FadeOutInfoTexts");

        Sequence seq = DOTween.Sequence()
            .SetId(this)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        bool any = false;
        any |= FadeOutTMP(seq, worldTitle);
        any |= FadeOutTMP(seq, worldSubtitle);
        any |= FadeOutTMP(seq, worldValue);

        if (any)
        {
            seq.OnComplete(() =>
            {
                HideWorldTexts();
                HideProgress();
                onComplete?.Invoke();
            });
        }
        else
        {
            seq.Kill();
            HideWorldTexts();
            HideProgress();
            onComplete?.Invoke();
        }
    }

    //  SUMMARY 

    public void ShowSummary(Action onComplete)
    {
        Debug.Log("[RewardReveal] ShowSummary");

        if (summaryTitleText != null) summaryTitleText.text = "You found:";

        // Money
        if (summaryCard1 != null && moneySprite != null)
        {
            summaryCard1.sprite = moneySprite;
            NormalizeSummaryCardScale(summaryCard1, moneySprite, "summaryCard1(Money)");
        }
        if (summaryOverlay1 != null) summaryOverlay1.text = $"{_rewards.moneyMultiplier}x";

        // Nitro
        if (summaryCard2 != null && nitroSprite != null)
        {
            summaryCard2.sprite = nitroSprite;
            NormalizeSummaryCardScale(summaryCard2, nitroSprite, "summaryCard2(Nitro)");
        }
        if (summaryOverlay2 != null) summaryOverlay2.text = $"+{_rewards.nitroReward}";

        // Real card  ← this is where the size diff previously appeared
        if (summaryCard3 != null && _rewards?.cardIcon != null)
        {
            summaryCard3.sprite = _rewards.cardIcon;
            NormalizeSummaryCardScale(summaryCard3, _rewards.cardIcon, "summaryCard3(Card)");  // ← FIX
        }
        if (summaryOverlay3 != null) summaryOverlay3.text = $"x{_rewards?.cardCopies}";

        if (summaryRoot != null)
        {
            // Set all child renderers/TMPs to alpha 0, activate, then fade in
            SetSummaryAlpha(0f);
            summaryRoot.SetActive(true);

            DOTween.To(() => 0f, a => SetSummaryAlpha(a), 1f, summaryFadeDuration)
                .SetId(this)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() => onComplete?.Invoke());
        }
        else
        {
            Debug.LogWarning("[RewardReveal] summaryRoot is null!");
            onComplete?.Invoke();
        }
    }

    public void HideAll()
    {
        HideWorldTexts();
        HideProgress();
        if (summaryRoot != null) summaryRoot.SetActive(false);
    }

    // 
    //  PRIVATE  content setters
    // 

    private void SetWorldTexts(string title, string subtitle, string value)
    {
        if (worldTitle != null) { worldTitle.text = title; worldTitle.gameObject.SetActive(true); }
        if (worldSubtitle != null) { worldSubtitle.text = subtitle; worldSubtitle.gameObject.SetActive(true); }
        if (worldValue != null) { worldValue.text = value; worldValue.gameObject.SetActive(true); }
    }

    private void HideWorldTexts()
    {
        if (worldTitle != null) worldTitle.gameObject.SetActive(false);
        if (worldSubtitle != null) worldSubtitle.gameObject.SetActive(false);
        if (worldValue != null) worldValue.gameObject.SetActive(false);
    }

    private void HideProgress()
    {
        if (progressRoot != null) progressRoot.SetActive(false);
    }

    /// <summary>
    /// Full-width localScale.x for the ProgressFill SpriteRenderer when bar is 100%.
    /// Empty = 0, Half = 1.3, Full = 2.6.
    /// </summary>
    private const float ProgressFillMaxScaleX = 2.6f;

    private void ShowProgress(int level, int owned, int needed, float fill01)
    {
        if (progressRoot == null) return;
        progressRoot.SetActive(true);

        if (progressFillRenderer != null)
        {
            Vector3 s = progressFillRenderer.transform.localScale;
            s.x = ProgressFillMaxScaleX * Mathf.Clamp01(fill01);
            progressFillRenderer.transform.localScale = s;
        }

        if (progressLevelText != null) progressLevelText.text = $"Level {level}";
        if (progressCopiesText != null)
        {
            if (needed <= 0)
                progressCopiesText.text = "MAX";
            else
            {
                int clamped = Mathf.Clamp(owned, 0, needed);
                progressCopiesText.text = $"{clamped}/{needed}";
            }
        }

        Debug.Log($"[RewardReveal] ShowProgress: L{level} {owned}/{needed} fill01={fill01:F3} scaleX={ProgressFillMaxScaleX * Mathf.Clamp01(fill01):F3}");
    }

    // 
    //  PRIVATE  animation helpers
    // 

    private void FadeInWorldTexts(Action onComplete)
    {
        if (worldTitle != null) worldTitle.alpha = 0f;
        if (worldSubtitle != null) worldSubtitle.alpha = 0f;
        if (worldValue != null) worldValue.alpha = 0f;

        Sequence seq = DOTween.Sequence()
            .SetId(this)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        FadeInTMP(seq, worldTitle, textFadeDuration);
        FadeInTMP(seq, worldSubtitle, textFadeDuration);
        FadeInTMP(seq, worldValue, textFadeDuration);

        seq.OnComplete(() => onComplete?.Invoke());
    }

    private static void FadeInTMP(Sequence seq, TextMeshPro tmp, float dur)
    {
        if (tmp == null) return;
        seq.Join(DOTween.To(() => tmp.alpha, a => tmp.alpha = a, 1f, dur));
    }

    private bool FadeOutTMP(Sequence seq, TextMeshPro tmp)
    {
        if (tmp == null || !tmp.gameObject.activeSelf) return false;
        seq.Join(DOTween.To(() => tmp.alpha, a => tmp.alpha = a, 0f, crossfadeDuration));
        return true;
    }

    // 
    //  SUMMARY CARD SIZE NORMALIZATION
    // 

    /// <summary>
    /// Adjusts <paramref name="sr"/>.transform.localScale so the sprite renders at
    /// <see cref="summaryCardWorldHeight"/> world units tall, regardless of sprite
    /// texture dimensions or Pixels-Per-Unit.  This is the fix for the third summary
    /// card (card artwork) appearing larger than card1/card2 (icon sprites).
    /// </summary>
    private void NormalizeSummaryCardScale(SpriteRenderer sr, Sprite sprite, string debugLabel)
    {
        if (sr == null || sprite == null) return;

        // Debug: always log current state so the caller can read calibration values
        Debug.Log($"[RewardReveal] SummaryCard '{debugLabel}'  "
                + $"sprite='{sprite.name}'  bounds.y={sprite.bounds.size.y:F4}  "
                + $"sr.localScale={sr.transform.localScale}  "
                + $"sr.lossyScale={sr.transform.lossyScale}  "
                + $"parent='{sr.transform.parent?.name}'  "
                + $"parentLossyScale={sr.transform.parent?.lossyScale}");

        if (summaryCardWorldHeight <= 0f)
        {
            // Normalization disabled — reset to prefab default.
            sr.transform.localScale = Vector3.one;
            Debug.Log($"[RewardReveal] SummaryCard '{debugLabel}'  normalization DISABLED (summaryCardWorldHeight=0)  scale reset to 1");
            return;
        }

        float spriteWorldHeight = sprite.bounds.size.y;   // world-units at localScale=1
        if (spriteWorldHeight < 1e-5f)
        {
            Debug.LogWarning($"[RewardReveal] SummaryCard '{debugLabel}'  sprite '{sprite.name}' has near-zero bounds.y — skipping normalization.");
            sr.transform.localScale = Vector3.one;
            return;
        }

        float corrective = summaryCardWorldHeight / spriteWorldHeight;
        sr.transform.localScale = Vector3.one * corrective;

        Debug.Log($"[RewardReveal] SummaryCard '{debugLabel}'  AFTER NORMALIZE  "
                + $"bounds.y={spriteWorldHeight:F4}  target={summaryCardWorldHeight:F4}  "
                + $"corrective={corrective:F4}  "
                + $"finalLocalScale={sr.transform.localScale}  "
                + $"finalLossyScale={sr.transform.lossyScale}");
    }

    private void SetSummaryAlpha(float a)
    {
        if (summaryTitleText != null) summaryTitleText.alpha = a;
        SetSRAlpha(summaryCard1, a);
        SetSRAlpha(summaryCard2, a);
        SetSRAlpha(summaryCard3, a);
        if (summaryOverlay1 != null) summaryOverlay1.alpha = a;
        if (summaryOverlay2 != null) summaryOverlay2.alpha = a;
        if (summaryOverlay3 != null) summaryOverlay3.alpha = a;
    }

    private static void SetSRAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color; c.a = a; sr.color = c;
    }

    // 
    //  STATIC HELPERS
    // 

    public static string FormatMoney(double value)
    {
        if (value >= 1_000_000_000) return $"{value / 1_000_000_000:F1}B $";
        if (value >= 1_000_000) return $"{value / 1_000_000:F1}M $";
        return $"{value:N0} $";
    }
}