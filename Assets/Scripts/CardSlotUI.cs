using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class CardSlotUI : MonoBehaviour
{
    [Header("UI Refs")]
    public Image iconImage;
    public TextMeshProUGUI textName;   // Üstte: "Level 3" veya boş
    public TextMeshProUGUI textLevel;  // Altta: "3/10" veya "0/1"
    public Button button;

    [Header("Progress Bar")]
    [Tooltip("Bar_Fill Image (legacy fillAmount bar — optional, can be null if using segments)")]
    public Image barFill;

    [Tooltip("8 segment fill images under Bar_Fill (one per segment, toggled on/off)")]
    [SerializeField] private Image[] fillSegments;

    [Tooltip("Ghost fills stacked on top of Bar_Fill (Bar_Fill_1 .. Bar_Fill_8) — legacy fade tail")]
    [SerializeField] private Image[] barFillGhosts;

    [Tooltip("If true, fill changes are tweened with DOTween over 0.1 s")]
    [SerializeField] private bool useTween;

    [Header("Colors")]
    public Color unlockedColor = Color.white;
    public Color lockedColor = new Color(1f, 1f, 1f, 0.35f);

    // Ghost alpha ramp: linearly interpolated from 230/255 (ghost 1) → 0 (ghost N).
    // Matches designer spec: main 255, ghost1 230, ghost2 ≈200, … ghost8 0.
    private const float GhostAlphaStart = 230f / 255f;   // ≈ 0.902
    private const float GhostAlphaEnd = 0f;
    private const float TweenDuration = 0.1f;

    private CardDefinition card;
    private System.Action<CardDefinition> onClick;

    // ────────────────────────────────────────────

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnClickInternal);
    }

    private void OnDestroy()
    {
        // Kill any running tweens to prevent callbacks on destroyed objects
        if (useTween)
            KillAllBarTweens();
    }

    // ────────────────────────────────────────────

    public void Setup(CardDefinition def, System.Action<CardDefinition> onClickCallback)
    {
        card = def;
        onClick = onClickCallback;

        // Ensure button listener is wired (in case Awake didn't run yet or button was null)
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickInternal);
        }

        if (iconImage != null && def.icon != null)
            iconImage.sprite = def.icon;

        Refresh();

        Debug.Log($"[CardSlotUI] Setup complete for: {def.type}, callback assigned: {onClick != null}");
    }

    // ────────────────────────────────────────────

    public void Refresh()
    {
        if (card == null) return;

        bool unlocked = card.IsUnlocked;
        int filledSegments = card.GetFilledSegments();
        string progressText = card.GetUpgradeProgressText();

        // Debug log
        Debug.Log($"[CardUI] {card.type} L{card.currentLevel} segments={card.copiesOwned} filled={filledSegments} text={progressText}");

        // TOP TEXT: Level info (no MAX state — infinite levels)
        if (textName != null)
        {
            if (unlocked)
                textName.text = $"Level {card.currentLevel}";
            else
                textName.text = "";
        }

        // BOTTOM TEXT: Progress text from helper
        if (textLevel != null)
        {
            textLevel.text = progressText;
        }

        // PROGRESS BAR: segmented toggle
        UpdateSegments(filledSegments);

        // ICON: swap sprite for locked/unlocked state
        if (iconImage != null)
        {
            if (unlocked)
            {
                // Show the normal coloured icon
                if (card.icon != null)
                    iconImage.sprite = card.icon;
                iconImage.color = Color.white;
            }
            else if (card.lockedIcon != null)
            {
                // Show dedicated grayscale locked art — no tint needed
                iconImage.sprite = card.lockedIcon;
                iconImage.color = Color.white;
            }
            else
            {
                // Fallback: lockedIcon not assigned yet — keep icon + alpha tint
                if (card.icon != null)
                    iconImage.sprite = card.icon;
                iconImage.color = lockedColor;
            }
        }
    }

    // ────────────────────────────────────────────
    //  Segmented progress bar (8 segments, on/off toggle)
    // ────────────────────────────────────────────

    /// <summary>
    /// Toggles segment fill images on/off. Segments 0..N-1 are enabled,
    /// N..7 are disabled. No partial fill (fillAmount) is used.
    /// Falls back to legacy fillAmount bar if fillSegments is empty.
    /// </summary>
    private void UpdateSegments(int filledCount)
    {
        // ── New segmented bar ──────────────────
        if (fillSegments != null && fillSegments.Length > 0)
        {
            for (int i = 0; i < fillSegments.Length; i++)
            {
                if (fillSegments[i] != null)
                    fillSegments[i].gameObject.SetActive(i < filledCount);
            }
        }

        // ── Legacy fill bar (kept as optional fallback) ──
        if (barFill != null)
        {
            float fill01 = (float)filledCount / CardDropTuning.SegmentsPerUpgrade;
            if (useTween)
            {
                DOTween.Kill(barFill);
                barFill.DOFillAmount(fill01, TweenDuration)
                       .SetTarget(barFill)
                       .SetEase(Ease.OutQuad);
            }
            else
            {
                barFill.fillAmount = fill01;
            }
        }

        // ── Legacy ghost fills (kept as optional fallback) ──
        if (barFillGhosts != null && barFillGhosts.Length > 0)
        {
            float mainProgress = (float)filledCount / CardDropTuning.SegmentsPerUpgrade;
            Color baseColor = (barFill != null) ? barFill.color : Color.white;
            bool isFull = filledCount >= CardDropTuning.SegmentsPerUpgrade;
            int count = barFillGhosts.Length;

            for (int i = 0; i < count; i++)
            {
                Image ghost = barFillGhosts[i];
                if (ghost == null) continue;

                if (isFull)
                {
                    if (useTween) DOTween.Kill(ghost);
                    ghost.fillAmount = 0f;
                    ghost.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                    continue;
                }

                float ghostFill = Mathf.Min(1f, mainProgress + (i + 1) * 0.02f);
                float t = (count > 1) ? (float)i / (count - 1) : 1f;
                float alpha = Mathf.Lerp(GhostAlphaStart, GhostAlphaEnd, t);
                ghost.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

                if (useTween)
                {
                    DOTween.Kill(ghost);
                    ghost.DOFillAmount(ghostFill, TweenDuration)
                         .SetTarget(ghost)
                         .SetEase(Ease.OutQuad);
                }
                else
                {
                    ghost.fillAmount = ghostFill;
                }
            }
        }
    }

    private void KillAllBarTweens()
    {
        if (barFill != null)
            DOTween.Kill(barFill);

        if (barFillGhosts != null)
        {
            for (int i = 0; i < barFillGhosts.Length; i++)
            {
                if (barFillGhosts[i] != null)
                    DOTween.Kill(barFillGhosts[i]);
            }
        }
    }

    // ────────────────────────────────────────────

    private void OnClickInternal()
    {
        if (card == null) return;
        onClick?.Invoke(card);
    }
}
