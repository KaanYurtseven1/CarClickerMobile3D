using UnityEngine;
using TMPro;
using DG.Tweening;
using System;
using System.Globalization;

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

    // Pre-reward snapshot (for progress bar animation)
    public int preRewardCopiesOwned;

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
    // ──────────────────────────────────────────────────────────────────
    //  WORLD-SPACE INFO TEXTS (3D TMP)
    // ──────────────────────────────────────────────────────────────────
    [Header("World-Space Info Texts")]
    [SerializeField] private TextMeshPro worldTitle;
    [SerializeField] private TextMeshPro worldSubtitle;
    [SerializeField] private TextMeshPro worldValue;

    // ──────────────────────────────────────────────────────────────────
    //  PROGRESS BAR (world-space, 8-segment SpriteRenderer model)
    // ──────────────────────────────────────────────────────────────────
    [Header("Card Progress (world-space)")]
    [Tooltip("Root GO — set inactive by default; shown only during real card reveal")]
    [SerializeField] private GameObject progressRoot;

    [Tooltip("Parent transform whose children are the 8 Bar_BG SpriteRenderers (always visible).\n" +
             "If bgSegments array is empty / wrong length, children are auto-resolved at Awake.")]
    [SerializeField] private Transform barBGParent;

    [Tooltip("Parent transform whose children are the 8 Bar_Fill SpriteRenderers (fill left→right).\n" +
             "If fillSegments array is empty / wrong length, children are auto-resolved at Awake.")]
    [SerializeField] private Transform barFillParent;

    [Tooltip("8 Bar_BG SpriteRenderers (always visible). Auto-resolved from barBGParent children if empty.")]
    [SerializeField] private SpriteRenderer[] bgSegments = new SpriteRenderer[8];

    [Tooltip("8 Bar_Fill SpriteRenderers, wired LEFT→RIGHT (index 0 = leftmost). Auto-resolved from barFillParent children if empty.")]
    [SerializeField] private SpriteRenderer[] fillSegments = new SpriteRenderer[8];

    [SerializeField] private TextMeshPro progressLevelText;
    [SerializeField] private TextMeshPro progressCopiesText;

    // ──────────────────────────────────────────────────────────────────
    //  SUMMARY (world-space)
    // ──────────────────────────────────────────────────────────────────
    [Header("Summary (world-space)")]
    [Tooltip("Root GO — set inactive by default")]
    [SerializeField] private GameObject summaryRoot;

    [SerializeField] private TextMeshPro summaryTitleText;

    [Header("Summary Cards (SpriteRenderer + TMP overlay each)")]
    [SerializeField] private SpriteRenderer summaryCard1;   // Money
    [SerializeField] private TextMeshPro summaryOverlay1;
    [SerializeField] private SpriteRenderer summaryCard2;   // Nitro
    [SerializeField] private TextMeshPro summaryOverlay2;
    [SerializeField] private SpriteRenderer summaryCard3;   // Real Card
    [SerializeField] private TextMeshPro summaryOverlay3;

    // ──────────────────────────────────────────────────────────────────
    //  REWARD ICON SPRITES
    // ──────────────────────────────────────────────────────────────────
    [Header("Reward Icon Sprites")]
    [SerializeField] private Sprite moneySprite;
    [SerializeField] private Sprite nitroSprite;

    // ──────────────────────────────────────────────────────────────────
    //  ANIMATION TUNING
    // ──────────────────────────────────────────────────────────────────
    [Header("Animation")]
    [SerializeField] private float crossfadeDuration = 0.20f;
    [SerializeField] private float summaryFadeDuration = 0.30f;

    [Header("Per-Character Typewriter Fade (left\u2192right)")]
    [Tooltip("Stagger delay between consecutive characters' fade start (seconds)")]
    [SerializeField] private float charTypewriterStagger = 0.03f;
    [Tooltip("Duration of each character's individual alpha fade-in")]
    [SerializeField] private float charTypewriterFadeDuration = 0.15f;

    [Header("Progress Bar Animation")]
    [Tooltip("Duration for Bar_BG segments to fade in (alpha 0\u21921) before fill starts")]
    [SerializeField] private float bgFadeInDuration = 0.30f;
    [Tooltip("Duration for each fill segment's pop-in (alpha + scale)")]
    [SerializeField] private float segmentPopDuration = 0.25f;
    [Tooltip("Stagger delay between consecutive fill segments")]
    [SerializeField] private float segmentStaggerDelay = 0.15f;

    [Header("Summary Card Size Normalization")]
    [Tooltip("Target world-space height (in world units) that every summary SpriteRenderer should occupy. \n"
           + "All 3 summary cards (Money, Nitro, Card) are scaled so their SpriteRenderer renders at \n"
           + "exactly this height, eliminating the size difference caused by different sprite dimensions / PPU. \n"
           + "Calibration: run in Play Mode, read the '[RewardReveal] SummaryCard' logs for summaryCard1 \n"
           + "(Money) and copy its 'bounds.y' value here. Set to 0 to disable normalization.")]
    [SerializeField] private float summaryCardWorldHeight = 1.0f;

    // ──────────────────────────────────────────────────────────────────
    //  RUNTIME STATE
    // ──────────────────────────────────────────────────────────────────
    private ChestRewardPackage _rewards;

    /// <summary>Cached prefab localScales for each fill segment (set once in Awake).</summary>
    private Vector3[] _segmentBaseScales;

    private const int SegmentCount = 8;

    public Sprite MoneySprite => moneySprite;
    public Sprite NitroSprite => nitroSprite;

    // ══════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        AutoResolveSegments();
        CacheSegmentScales();
        HideAll();
    }

    /// <summary>
    /// If serialized arrays are null/empty/wrong length, try to auto-resolve
    /// SpriteRenderers from barBGParent / barFillParent children.
    /// </summary>
    private void AutoResolveSegments()
    {
        bgSegments   = ResolveSegmentArray(bgSegments,   barBGParent,   "Bar_BG");
        fillSegments = ResolveSegmentArray(fillSegments, barFillParent, "Bar_Fill");
    }

    private static SpriteRenderer[] ResolveSegmentArray(SpriteRenderer[] existing, Transform parent, string label)
    {
        if (existing != null && existing.Length == SegmentCount)
        {
            // Validate all entries are non-null
            bool allValid = true;
            for (int i = 0; i < existing.Length; i++)
                if (existing[i] == null) { allValid = false; break; }
            if (allValid) return existing;
        }

        if (parent == null)
        {
            Debug.LogWarning($"[RewardReveal] {label}: segments array invalid and no parent transform assigned for auto-resolve.");
            return existing ?? new SpriteRenderer[0];
        }

        int childCount = parent.childCount;
        if (childCount < SegmentCount)
        {
            Debug.LogWarning($"[RewardReveal] {label}: parent '{parent.name}' has {childCount} children, expected {SegmentCount}.");
        }

        SpriteRenderer[] resolved = new SpriteRenderer[Mathf.Min(childCount, SegmentCount)];
        for (int i = 0; i < resolved.Length; i++)
            resolved[i] = parent.GetChild(i).GetComponent<SpriteRenderer>();

        Debug.Log($"[RewardReveal] {label}: auto-resolved {resolved.Length} SpriteRenderers from '{parent.name}'.");
        return resolved;
    }

    private void CacheSegmentScales()
    {
        if (fillSegments == null || fillSegments.Length == 0) return;
        _segmentBaseScales = new Vector3[fillSegments.Length];
        for (int i = 0; i < fillSegments.Length; i++)
        {
            _segmentBaseScales[i] = fillSegments[i] != null
                ? fillSegments[i].transform.localScale
                : Vector3.one;
        }
    }

    private Vector3 GetSegmentBaseScale(int index)
    {
        if (_segmentBaseScales != null && index >= 0 && index < _segmentBaseScales.Length)
            return _segmentBaseScales[index];
        return Vector3.one;
    }

    private void OnDestroy()
    {
        KillPerCharTweens();
        KillSegmentTweens();
        DOTween.Kill(this);
    }

    /// <summary>Kill any running per-character vertex tweens on the three world TMP labels.</summary>
    private void KillPerCharTweens()
    {
        if (worldTitle    != null) DOTween.Kill(worldTitle.GetInstanceID());
        if (worldSubtitle != null) DOTween.Kill(worldSubtitle.GetInstanceID());
        if (worldValue    != null) DOTween.Kill(worldValue.GetInstanceID());
    }

    /// <summary>Kill any running segment fill tweens and snap to final state.</summary>
    private void KillSegmentTweens()
    {
        DOTween.Kill("RevealSegmentFill");
    }

    // ══════════════════════════════════════════════════════════════════
    //  PUBLIC API (signatures unchanged — call sites safe)
    // ══════════════════════════════════════════════════════════════════

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

        // A1: Always show rarity for card phase
        string rarityStr = _rewards.cardRarity.ToString(); // Common / Rare / Epic / Legendary
        string sub = $"{rarityStr} Card";

        // A2: Use displayName (scene-fixed); also sanitize as safety
        string title = SanitizeDisplayName(_rewards.cardDisplayName);
        SetWorldTexts(title, sub, "");

        // C: Show progress with animated fill
        ShowProgressAnimated(
            _rewards.preRewardCopiesOwned,
            _rewards.postRewardCopiesOwned,
            _rewards.postRewardLevel,
            _rewards.copiesNeededForNext,
            _rewards.upgradeProgress01
        );
        FadeInWorldTexts(onComplete);
    }

    //  FADE OUT 

    public void FadeOutInfoTexts(Action onComplete)
    {
        Debug.Log("[RewardReveal] FadeOutInfoTexts");

        // Kill all in-flight per-character vertex tweens and restore meshes
        KillPerCharTweens();
        RestoreTMPVertices(worldTitle);
        RestoreTMPVertices(worldSubtitle);
        RestoreTMPVertices(worldValue);

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

        // Real card
        if (summaryCard3 != null && _rewards?.cardIcon != null)
        {
            summaryCard3.sprite = _rewards.cardIcon;
            NormalizeSummaryCardScale(summaryCard3, _rewards.cardIcon, "summaryCard3(Card)");
        }
        if (summaryOverlay3 != null) summaryOverlay3.text = $"x{_rewards?.cardCopies}";

        if (summaryRoot != null)
        {
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

    // ══════════════════════════════════════════════════════════════════
    //  PRIVATE — CONTENT SETTERS
    // ══════════════════════════════════════════════════════════════════

    private void SetWorldTexts(string title, string subtitle, string value)
    {
        if (worldTitle    != null) { worldTitle.text    = title;    worldTitle.gameObject.SetActive(true); }
        if (worldSubtitle != null) { worldSubtitle.text = subtitle; worldSubtitle.gameObject.SetActive(true); }
        if (worldValue    != null) { worldValue.text    = value;    worldValue.gameObject.SetActive(true); }
    }

    private void HideWorldTexts()
    {
        KillPerCharTweens();
        RestoreTMPVertices(worldTitle);
        RestoreTMPVertices(worldSubtitle);
        RestoreTMPVertices(worldValue);
        if (worldTitle    != null) worldTitle.gameObject.SetActive(false);
        if (worldSubtitle != null) worldSubtitle.gameObject.SetActive(false);
        if (worldValue    != null) worldValue.gameObject.SetActive(false);
    }

    private void HideProgress()
    {
        KillSegmentTweens();
        ResetBarSegments();
        if (progressRoot != null) progressRoot.SetActive(false);
    }

    /// <summary>
    /// Reset all bar segments to default state:
    /// BG segments enabled (always visible), Fill segments disabled.
    /// Fill segments get their base scale restored.
    /// </summary>
    private void ResetBarSegments()
    {
        // BG: always visible
        if (bgSegments != null)
        {
            for (int i = 0; i < bgSegments.Length; i++)
            {
                if (bgSegments[i] != null)
                {
                    bgSegments[i].enabled = true;
                    SetSRAlpha(bgSegments[i], 1f);
                }
            }
        }

        // Fill: all off, restore scale
        if (fillSegments != null)
        {
            for (int i = 0; i < fillSegments.Length; i++)
            {
                if (fillSegments[i] != null)
                {
                    fillSegments[i].enabled = false;
                    SetSRAlpha(fillSegments[i], 0f);
                    fillSegments[i].transform.localScale = GetSegmentBaseScale(i);
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  PROGRESS BAR — NON-ANIMATED (kept for potential future use)
    // ══════════════════════════════════════════════════════════════════

    private void ShowProgress(int level, int owned, int needed, float fill01)
    {
        if (progressRoot == null) return;
        progressRoot.SetActive(true);

        int maxSeg = CardDropTuning.SegmentsPerUpgrade;
        int filledCount = Mathf.Clamp(owned, 0, maxSeg);

        // Ensure BG always visible
        EnsureBGVisible();

        if (!ValidateFillSegments()) return;

        for (int i = 0; i < fillSegments.Length; i++)
        {
            if (fillSegments[i] != null)
            {
                fillSegments[i].enabled = (i < filledCount);
                if (i < filledCount)
                {
                    SetSRAlpha(fillSegments[i], 1f);
                    fillSegments[i].transform.localScale = GetSegmentBaseScale(i);
                }
            }
        }

        // Text labels
        if (progressLevelText != null) progressLevelText.text = $"Level {level}";
        if (progressCopiesText != null)
        {
            if (needed <= 0)
                progressCopiesText.text = "MAX";
            else
                progressCopiesText.text = $"{filledCount}/{needed}";
        }

        Debug.Log($"[RewardReveal] ShowProgress: L{level} segments={filledCount}/{needed} fill01={fill01:F3}");
    }

    // ══════════════════════════════════════════════════════════════════
    //  PROGRESS BAR — ANIMATED FILL (B)
    //  Phase 1: BG segments fade in (alpha 0→1).
    //  Phase 2: Fill segments pop in left→right with readable stagger.
    //  Handles level-up overflow: fill→8, reset, fill→remainder.
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Animated progress bar with two phases:
    /// 1) Fades in all BG segments (alpha 0→1 over bgFadeInDuration).
    /// 2) Shows pre-existing fill instantly, then animates new fill segments
    ///    left-to-right with per-segment alpha (0→1) + subtle scale pop (0.9→1.0).
    /// If level-up overflow detected (postFilled &lt; preFilled with positive copies):
    ///    fills bar to 8, briefly resets, then fills 0→postFilled.
    /// On kill/complete, segments end in correct final state.
    /// </summary>
    private void ShowProgressAnimated(int preOwned, int postOwned, int level, int needed, float fill01)
    {
        if (progressRoot == null) return;
        progressRoot.SetActive(true);

        // Kill any previous segment tweens first
        KillSegmentTweens();

        int maxSeg = CardDropTuning.SegmentsPerUpgrade;
        int preFilled  = Mathf.Clamp(preOwned,  0, maxSeg);
        int postFilled = Mathf.Clamp(postOwned, 0, maxSeg);

        // ── Text labels (show post-reward counts) ──
        if (progressLevelText != null) progressLevelText.text = $"Level {level}";
        if (progressCopiesText != null)
        {
            if (needed <= 0)
                progressCopiesText.text = "MAX";
            else
                progressCopiesText.text = $"{postFilled}/{needed}";
        }

        // ── Validate fillSegments ──
        if (!ValidateFillSegments()) return;

        int segLen = fillSegments.Length;

        // ── Detect overflow / level-up ──
        int copiesGained = (_rewards != null) ? _rewards.cardCopies : 0;
        bool isOverflow = (copiesGained > 0 && postFilled <= preFilled);

        // ── Initial state: BG alpha 0 (will fade in), all fill disabled ──
        if (bgSegments != null)
        {
            for (int i = 0; i < bgSegments.Length; i++)
            {
                if (bgSegments[i] != null)
                {
                    bgSegments[i].enabled = true;
                    SetSRAlpha(bgSegments[i], 0f);
                }
            }
        }
        for (int i = 0; i < segLen; i++)
        {
            if (fillSegments[i] == null) continue;
            fillSegments[i].enabled = false;
            SetSRAlpha(fillSegments[i], 0f);
            fillSegments[i].transform.localScale = GetSegmentBaseScale(i);
        }

        int capturedPost = postFilled;

        Sequence seq = DOTween.Sequence()
            .SetId("RevealSegmentFill")
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // ── Phase 1: BG fade-in (all segments simultaneously) ──
        if (bgSegments != null)
        {
            for (int i = 0; i < bgSegments.Length; i++)
            {
                if (bgSegments[i] == null) continue;
                SpriteRenderer bg = bgSegments[i];
                seq.Insert(0f,
                    DOTween.To(() => bg.color.a, a => SetSRAlpha(bg, a), 1f, bgFadeInDuration)
                        .SetEase(Ease.OutQuad));
            }
        }

        // Small gap after BG is visible before fill starts
        float fillCursor = bgFadeInDuration + 0.06f;

        // ── Phase 2a: Show pre-existing filled segments instantly ──
        int capPre = preFilled;
        seq.InsertCallback(fillCursor, () =>
        {
            for (int i = 0; i < capPre && i < segLen; i++)
            {
                if (fillSegments[i] == null) continue;
                fillSegments[i].enabled = true;
                SetSRAlpha(fillSegments[i], 1f);
                fillSegments[i].transform.localScale = GetSegmentBaseScale(i);
            }
        });

        if (!isOverflow)
        {
            // ── Normal: animate preFilled → postFilled ──
            for (int i = preFilled; i < postFilled && i < segLen; i++)
            {
                int idx = i;
                float t = fillCursor + (idx - preFilled) * segmentStaggerDelay;
                InsertSegmentPopTween(seq, idx, t);
            }
        }
        else
        {
            // ── Overflow: fill preFilled → maxSeg, reset, then 0 → postFilled ──

            // 2b: Fill remaining to complete the bar
            for (int i = preFilled; i < maxSeg && i < segLen; i++)
            {
                int idx = i;
                float t = fillCursor + (idx - preFilled) * segmentStaggerDelay;
                InsertSegmentPopTween(seq, idx, t);
            }

            // Calculate when fill-to-max completes
            int fillToMaxCount = Mathf.Max(0, maxSeg - preFilled);
            float fillToMaxEnd = fillCursor
                + Mathf.Max(0, fillToMaxCount - 1) * segmentStaggerDelay
                + segmentPopDuration;

            // 2c: Brief pause then reset all fill segments
            float resetTime = fillToMaxEnd + 0.20f;
            seq.InsertCallback(resetTime, () =>
            {
                for (int i = 0; i < segLen; i++)
                {
                    if (fillSegments[i] == null) continue;
                    fillSegments[i].enabled = false;
                    SetSRAlpha(fillSegments[i], 0f);
                    fillSegments[i].transform.localScale = GetSegmentBaseScale(i);
                }
            });

            // 2d: Fill 0 → postFilled for the new level
            float refillStart = resetTime + 0.12f;
            for (int i = 0; i < postFilled && i < segLen; i++)
            {
                int idx = i;
                float t = refillStart + idx * segmentStaggerDelay;
                InsertSegmentPopTween(seq, idx, t);
            }
        }

        // On kill or complete: snap to correct final state
        seq.OnKill(() => { EnsureBGVisible(); SnapFillSegments(capturedPost); });
        seq.OnComplete(() =>
        {
            EnsureBGVisible();
            SnapFillSegments(capturedPost);
            Debug.Log($"[RewardReveal] Progress fill done: pre={preFilled} post={capturedPost} overflow={isOverflow}");
        });
    }

    /// <summary>
    /// Inserts a pop-in tween for a single fill segment into the given Sequence.
    /// At time <paramref name="insertTime"/>: enables the segment, sets alpha 0 + scale 0.9×base,
    /// then tweens alpha→1 and scale→base over segmentPopDuration.
    /// </summary>
    private void InsertSegmentPopTween(Sequence seq, int segIndex, float insertTime)
    {
        if (segIndex < 0 || segIndex >= fillSegments.Length) return;
        SpriteRenderer seg = fillSegments[segIndex];
        if (seg == null) return;

        Vector3 baseScale = GetSegmentBaseScale(segIndex);

        // Enable + set start visual state via callback (fires before tweens at same time)
        seq.InsertCallback(insertTime, () =>
        {
            seg.enabled = true;
            SetSRAlpha(seg, 0f);
            seg.transform.localScale = baseScale * 0.9f;
        });

        // Alpha 0 → 1
        seq.Insert(insertTime,
            DOTween.To(() => seg.color.a, a => SetSRAlpha(seg, a), 1f, segmentPopDuration)
                .SetEase(Ease.OutQuad));
        // Scale 0.9×base → base
        seq.Insert(insertTime,
            DOTween.To(
                () => seg.transform.localScale,
                s => seg.transform.localScale = s,
                baseScale,
                segmentPopDuration)
                .SetEase(Ease.OutCubic));
    }

    /// <summary>Force fill segments to their correct final enabled/alpha/scale state.</summary>
    private void SnapFillSegments(int targetFilled)
    {
        if (fillSegments == null) return;
        for (int i = 0; i < fillSegments.Length; i++)
        {
            if (fillSegments[i] == null) continue;
            bool shouldBeOn = i < targetFilled;
            fillSegments[i].enabled = shouldBeOn;
            if (shouldBeOn)
            {
                SetSRAlpha(fillSegments[i], 1f);
                fillSegments[i].transform.localScale = GetSegmentBaseScale(i);
            }
        }
    }

    /// <summary>Ensure all BG segments are enabled and fully opaque.</summary>
    private void EnsureBGVisible()
    {
        if (bgSegments == null) return;
        for (int i = 0; i < bgSegments.Length; i++)
        {
            if (bgSegments[i] != null)
            {
                bgSegments[i].enabled = true;
                SetSRAlpha(bgSegments[i], 1f);
            }
        }
    }

    /// <summary>Returns true if fillSegments is usable, logs warning if not.</summary>
    private bool ValidateFillSegments()
    {
        if (fillSegments == null || fillSegments.Length == 0)
        {
            Debug.LogWarning("[RewardReveal] fillSegments is null or empty. Cannot render progress bar.");
            return false;
        }
        if (fillSegments.Length != SegmentCount)
        {
            Debug.LogWarning($"[RewardReveal] fillSegments.Length={fillSegments.Length}, expected {SegmentCount}. Will clamp.");
        }
        return true;
    }

    // ══════════════════════════════════════════════════════════════════
    //  TEXT ANIMATION — PER-CHARACTER TYPEWRITER FADE (left→right)
    //  Alpha-only: no vertical offset, no position change.
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reveals world texts with per-character typewriter fade (Title → Subtitle → Value, chained).
    /// Characters fade in left-to-right; no position / vertex movement.
    /// </summary>
    private void FadeInWorldTexts(Action onComplete)
    {
        KillPerCharTweens();

        // Chain: title → subtitle → value → done
        AnimateTypewriterFade(worldTitle, charTypewriterFadeDuration, charTypewriterStagger, () =>
            AnimateTypewriterFade(worldSubtitle, charTypewriterFadeDuration, charTypewriterStagger, () =>
                AnimateTypewriterFade(worldValue, charTypewriterFadeDuration, charTypewriterStagger, () =>
                    onComplete?.Invoke())));
    }

    /// <summary>
    /// Per-character left-to-right typewriter fade on a TMP_Text label.
    /// Each visible character fades in (alpha 0→255) smoothly with a stagger.
    /// NO vertical offset, NO vertex position changes — only vertex colors are modified.
    ///
    /// Uses TMP vertex colors32 driven by a single master tween with per-character
    /// OutQuad easing computed analytically inside the setter.
    ///
    /// Safety: if called again on the same label, kills previous tweens and restores
    /// the TMP mesh before starting fresh.  Tween ID = tmp.GetInstanceID().
    /// OnKill always restores original vertex colors via ForceMeshUpdate.
    ///
    /// If the label is null, inactive, or has no text, the callback fires immediately.
    /// </summary>
    /// <param name="tmp">The TMP label to animate.</param>
    /// <param name="fadeDuration">Duration of each character's individual alpha fade.</param>
    /// <param name="stagger">Delay between consecutive characters' fade starts.</param>
    /// <param name="onComplete">Callback fired when the full label animation finishes.</param>
    private void AnimateTypewriterFade(TMP_Text tmp, float fadeDuration, float stagger, Action onComplete = null)
    {
        if (tmp == null || !tmp.gameObject.activeSelf || string.IsNullOrEmpty(tmp.text))
        {
            onComplete?.Invoke();
            return;
        }

        // Kill any previous per-char tween on this label and restore clean mesh
        DOTween.Kill(tmp.GetInstanceID());
        tmp.ForceMeshUpdate();

        // Ensure TMP is fully opaque and all chars potentially visible
        tmp.maxVisibleCharacters = int.MaxValue;
        tmp.alpha = 1f;
        tmp.ForceMeshUpdate();

        TMP_TextInfo textInfo = tmp.textInfo;
        int totalChars = textInfo.characterCount;
        if (totalChars == 0) { onComplete?.Invoke(); return; }

        // Cache original vertex colors per sub-mesh (preserve RGB, only alpha is animated)
        int meshCount = textInfo.meshInfo.Length;
        Color32[][] cachedColors = new Color32[meshCount][];
        for (int m = 0; m < meshCount; m++)
            cachedColors[m] = (Color32[])textInfo.meshInfo[m].colors32.Clone();

        // Set initial state: every visible char → alpha 0 (position untouched)
        for (int i = 0; i < totalChars; i++)
        {
            var ci = textInfo.characterInfo[i];
            if (!ci.isVisible) continue;
            int mi = ci.materialReferenceIndex;
            int vi = ci.vertexIndex;
            var colors = textInfo.meshInfo[mi].colors32;
            for (int v = 0; v < 4; v++)
                colors[vi + v] = new Color32(cachedColors[mi][vi + v].r,
                                             cachedColors[mi][vi + v].g,
                                             cachedColors[mi][vi + v].b, 0);
        }
        PushTMPColorData(tmp, textInfo);

        // Total timeline = last char start + its fade duration
        float totalDuration = Mathf.Max(0.01f, (totalChars - 1) * stagger + fadeDuration);
        float safeFade = Mathf.Max(0.001f, fadeDuration);

        // Single master tween: linear timeline, per-char OutQuad baked in
        float masterTime = 0f;
        DOTween.To(() => masterTime, t =>
        {
            masterTime = t;
            for (int i = 0; i < totalChars; i++)
            {
                var ci = textInfo.characterInfo[i];
                if (!ci.isVisible) continue;

                float charStart = i * stagger;
                float raw = Mathf.Clamp01((t - charStart) / safeFade);
                // OutQuad: 1 - (1-raw)^2
                float inv = 1f - raw;
                float p = 1f - inv * inv;

                int mi = ci.materialReferenceIndex;
                int vi = ci.vertexIndex;
                var colors = textInfo.meshInfo[mi].colors32;
                byte alpha = (byte)(p * 255f);
                for (int v = 0; v < 4; v++)
                    colors[vi + v] = new Color32(cachedColors[mi][vi + v].r,
                                                 cachedColors[mi][vi + v].g,
                                                 cachedColors[mi][vi + v].b, alpha);
            }
            PushTMPColorData(tmp, textInfo);
        }, totalDuration, totalDuration)
            .SetId(tmp.GetInstanceID())
            .SetUpdate(true)
            .SetEase(Ease.Linear)             // linear timeline; per-char ease is baked in
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnKill(() =>
            {
                // Restore mesh so labels don't get stuck invisible
                if (tmp != null && tmp.gameObject.activeSelf)
                    tmp.ForceMeshUpdate();
            })
            .OnComplete(() =>
            {
                // Restore clean vertex colors so future text changes aren't corrupted
                if (tmp != null && tmp.gameObject.activeSelf)
                    tmp.ForceMeshUpdate();
                onComplete?.Invoke();
            });
    }

    // ══════════════════════════════════════════════════════════════════
    //  TMP UTILITY HELPERS
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Push modified vertex color data to the TMP mesh (alpha only — no vertex position changes).</summary>
    private static void PushTMPColorData(TMP_Text tmp, TMP_TextInfo textInfo)
    {
        for (int m = 0; m < textInfo.meshInfo.Length; m++)
            textInfo.meshInfo[m].mesh.colors32 = textInfo.meshInfo[m].colors32;
        tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    /// <summary>Restore TMP mesh to its canonical state (undo any vertex modifications).</summary>
    private static void RestoreTMPVertices(TMP_Text tmp)
    {
        if (tmp != null && tmp.gameObject.activeSelf)
            tmp.ForceMeshUpdate();
    }

    private bool FadeOutTMP(Sequence seq, TextMeshPro tmp)
    {
        if (tmp == null || !tmp.gameObject.activeSelf) return false;
        seq.Join(DOTween.To(() => tmp.alpha, a => tmp.alpha = a, 0f, crossfadeDuration));
        return true;
    }

    // ══════════════════════════════════════════════════════════════════
    //  SUMMARY CARD SIZE NORMALIZATION
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adjusts <paramref name="sr"/>.transform.localScale so the sprite renders at
    /// <see cref="summaryCardWorldHeight"/> world units tall, regardless of sprite
    /// texture dimensions or Pixels-Per-Unit.
    /// </summary>
    private void NormalizeSummaryCardScale(SpriteRenderer sr, Sprite sprite, string debugLabel)
    {
        if (sr == null || sprite == null) return;

        Debug.Log($"[RewardReveal] SummaryCard '{debugLabel}'  "
                + $"sprite='{sprite.name}'  bounds.y={sprite.bounds.size.y:F4}  "
                + $"sr.localScale={sr.transform.localScale}  "
                + $"sr.lossyScale={sr.transform.lossyScale}  "
                + $"parent='{sr.transform.parent?.name}'  "
                + $"parentLossyScale={sr.transform.parent?.lossyScale}");

        if (summaryCardWorldHeight <= 0f)
        {
            sr.transform.localScale = Vector3.one;
            Debug.Log($"[RewardReveal] SummaryCard '{debugLabel}'  normalization DISABLED (summaryCardWorldHeight=0)  scale reset to 1");
            return;
        }

        float spriteWorldHeight = sprite.bounds.size.y;
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

    // ══════════════════════════════════════════════════════════════════
    //  SUMMARY ALPHA
    // ══════════════════════════════════════════════════════════════════

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

    // ══════════════════════════════════════════════════════════════════
    //  STATIC HELPERS
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Full number with thousands separators, no abbreviation.
    /// Uses invariant culture for comma separators: 4,000,000,000 $
    /// </summary>
    public static string FormatMoney(double value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture) + " $";
    }

    /// <summary>
    /// Sanitize displayName as safety net for known typos.
    /// </summary>
    private static string SanitizeDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Card";
        if (name.IndexOf("Sop", StringComparison.OrdinalIgnoreCase) >= 0)
            name = name.Replace("Sop", "Stop").Replace("sop", "stop").Replace("SOP", "STOP");
        return name;
    }
}