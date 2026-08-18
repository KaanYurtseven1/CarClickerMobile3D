using UnityEngine;
using TMPro;
using DG.Tweening;
using System;

/// <summary>
/// Controls a single world-space reward card (SpriteRenderer plane).
/// Spawned by <see cref="ChestOpenSceneController"/> for each reward reveal.
///
/// Animation: TWO-STEP emerge from chest mouth:
///   Step 1  RISE: from mouthAnchor UP to (mouthX, parkY, mouthZ). Scale + rotate + alpha.
///   Step 2  SLIDE: from risen position horizontally to parkAnchor.position.
/// Always faces camera (billboard).
///
/// Prefab hierarchy:
///   WorldRewardCardPrefab_TMP  (this script + SortingGroup)
///     CardBG   (SpriteRenderer  card background)
///     Icon     (SpriteRenderer  reward icon)
///     OverlayTMP (TextMeshPro 3D  "x3", "+10", "2x")
/// </summary>
public class WorldRewardCardController : MonoBehaviour
{
    //  PREFAB REFERENCES 
    [Header("Prefab References")]
    [Tooltip("SpriteRenderer for the card background quad")]
    [SerializeField] private SpriteRenderer cardBGRenderer;

    [Tooltip("SpriteRenderer that displays the reward icon")]
    [SerializeField] private SpriteRenderer iconRenderer;

    [Tooltip("TextMeshPro (world-space) for overlay label like 'x3', '+10'")]
    [SerializeField] private TextMeshPro overlayText;

    //  ANIMATION TUNING 
    [Header("Rise Phase (step 1: vertical)")]
    [SerializeField] private float riseStartScale = 0.15f;
    [SerializeField] private float riseEndScale = 0.55f;
    [SerializeField] private float riseDuration = 0.40f;
    [SerializeField] private float riseStartTiltZ = 20f;
    [SerializeField] private float riseStartAlpha = 0.30f;

    [Header("Slide Phase (step 2: horizontal to park)")]
    [SerializeField] private float slideDuration = 0.35f;
    [SerializeField] private float parkScale = 0.60f;

    [Header("Hide Animation")]
    [SerializeField] private float hideDuration = 0.22f;

    [Header("Reveal Rotation (D: additive 180° flip)")]
    [Tooltip("Enable an additive Y-axis rotation during rise+slide")]
    [SerializeField] private bool enableRevealRotation = true;
    [Tooltip("Total rotation degrees during reveal (applied to Y axis)")]
    [SerializeField] private float revealRotationDegrees = 180f;

    [Header("Unified Reward Size")]
    [Tooltip("Target world-space width for ALL reward types (money, nitro, card art).\n"
           + "The active renderer child is auto-scaled so its world width matches this value.\n"
           + "If > 0: used directly. If == 0: auto-detected from the icon renderer each reveal\n"
           + "(icon mode becomes a no-op; cardBG mode falls back to this field — set it!).\n"
           + "Calibration: play a money reveal, read the log 'widthBefore=X', set this to X.")]
    [SerializeField] private float referenceWorldWidth = 1.0f;

    [Tooltip("Clamp the computed scale factor to a safe range.")]
    [SerializeField] private float fitMinScale = 0.05f;
    [SerializeField] private float fitMaxScale = 5.0f;

    //  RUNTIME 
    private Camera _cam;
    private Transform _mouthAnchor;
    private Transform _parkAnchor;
    private bool _isBillboard;

    // D: Additive Y rotation offset (animated, applied in LateUpdate billboard)
    private float _revealYOffset;

    // Cached default localScales (captured in Awake, restored each reveal)
    private Vector3 _iconDefaultLocalScale = Vector3.one;
    private Vector3 _cardBGDefaultLocalScale = Vector3.one;

    // 
    //  LIFECYCLE
    // 

    private void Awake()
    {
        if (iconRenderer != null)   _iconDefaultLocalScale  = iconRenderer.transform.localScale;
        if (cardBGRenderer != null) _cardBGDefaultLocalScale = cardBGRenderer.transform.localScale;
    }

    // 
    //  PUBLIC API
    // 

    /// <summary>
    /// Provide camera + world-space anchors.  Call before ShowWorldCard.
    /// </summary>
    public void SetAnchors(Camera cam, Transform mouthAnchor, Transform parkAnchor)
    {
        _cam = cam;
        _mouthAnchor = mouthAnchor;
        _parkAnchor = parkAnchor;
        Debug.Log($"[WorldCard] SetAnchors cam={cam?.name} mouth={mouthAnchor?.name}({mouthAnchor?.position}) park={parkAnchor?.name}({parkAnchor?.position})");
    }

    /// <summary>
    /// Two-step emerge animation: RISE then SLIDE.
    /// Calls <paramref name="onParked"/> when card reaches park position.
    ///
    /// <param name="icon">Reward icon sprite (used for Money + Nitro reveals).
    ///   Ignored when <paramref name="cardBGOverrideSprite"/> is provided.</param>
    /// <param name="overlayLabel">Text shown on the OverlayTMP (e.g. "4x", "+10").</param>
    /// <param name="cardBGOverrideSprite">Full card artwork sprite for Card reward reveals.
    ///   When non-null the CardBG renderer shows this art and the Icon renderer is hidden.
    ///   Pass null (default) to use the normal icon mode.</param>
    /// </summary>
    public void ShowWorldCard(Sprite icon, string overlayLabel, Action onParked,
                              Sprite cardBGOverrideSprite = null)
    {
        // Kill ALL leftover tweens FIRST (before resetting state)
        transform.DOKill();
        DOTween.Kill(this);

        // Reset child transforms to cached prefab defaults (prevents scale accumulation)
        ResetChildScales();

        //  Set visuals  ─────────────────────────────────────────────────────
        bool useCardBGMode = (cardBGOverrideSprite != null);

        if (useCardBGMode)
        {
            // ── Card artwork mode: full art on CardBG, icon hidden ──────────
            if (cardBGRenderer != null)
                cardBGRenderer.sprite = cardBGOverrideSprite;

            // Hide the icon renderer entirely (won't affect root transform)
            if (iconRenderer != null)
                iconRenderer.gameObject.SetActive(false);
        }
        else
        {
            // ── Icon mode (money / nitro): restore icon, leave CardBG ────────
            if (iconRenderer != null)
            {
                iconRenderer.gameObject.SetActive(true);
                if (icon != null)
                    iconRenderer.sprite = icon;
            }
        }

        // Fit active renderer to unified reference width
        FitActiveRenderer(useCardBGMode);

        if (overlayText != null) overlayText.text = overlayLabel ?? "";

        // ── Debug: size diagnostics ──
        Debug.Log($"[WorldCard] SPAWN  prefab='{gameObject.name}' "
                + $"mode={(useCardBGMode ? "CardBG" : "Icon")} "
                + $"sprite='{(useCardBGMode ? cardBGOverrideSprite?.name : icon?.name)}' "
                + $"overlayLabel='{overlayLabel}' "
                + $"rootLocalScale={transform.localScale} "
                + $"parentLossyScale={transform.parent?.lossyScale}");

        //  Resolve positions  ─────────────────────────────────────────────
        Vector3 startPos = _mouthAnchor != null ? _mouthAnchor.position : transform.position;
        Vector3 parkPos = _parkAnchor != null ? _parkAnchor.position
                                                : startPos + Vector3.up * 3f + Vector3.left * 2f;

        // Step-1 target: rise vertically (keep X/Z of mouth, Y of park)
        Vector3 risenPos = new Vector3(startPos.x, parkPos.y, startPos.z);

        //  Clean initial state (root transform only)  ─────────────────────
        transform.position = startPos;
        transform.localScale = Vector3.one * riseStartScale;
        transform.localEulerAngles = new Vector3(0f, 0f, riseStartTiltZ);
        SetAlpha(riseStartAlpha);
        gameObject.SetActive(true);
        _isBillboard = true;
        _revealYOffset = enableRevealRotation ? revealRotationDegrees : 0f;

        Debug.Log($"[WorldCard] ShowWorldCard start={startPos} risen={risenPos} park={parkPos}");

        //  BUILD SEQUENCE  ────────────────────────────────────────────────
        Sequence seq = DOTween.Sequence()
            .SetId(this)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        //  STEP 1: RISE (vertical)  ────────────────────────────────────────
        seq.Append(transform.DOMove(risenPos, riseDuration).SetEase(Ease.OutCubic));
        seq.Join(transform.DOScale(Vector3.one * riseEndScale, riseDuration).SetEase(Ease.OutCubic));
        seq.Join(transform.DOLocalRotate(Vector3.zero, riseDuration).SetEase(Ease.OutQuad));
        seq.Join(DOTween.To(GetAlpha, SetAlpha, 1f, riseDuration).SetEase(Ease.OutQuad));

        // D: Additive 180° Y rotation during rise + slide (applied via billboard offset)
        if (enableRevealRotation)
        {
            float totalDur = riseDuration + 0.06f + slideDuration;
            seq.Join(DOTween.To(() => _revealYOffset, v => _revealYOffset = v, 0f, totalDur)
                .SetEase(Ease.InOutQuad));
        }

        seq.AppendCallback(() => Debug.Log($"[WorldCard] AFTER RISE  rootLocalScale={transform.localScale}"));

        // tiny beat
        seq.AppendInterval(0.06f);

        //  STEP 2: SLIDE (horizontal to park)  ────────────────────────────
        seq.Append(transform.DOMove(parkPos, slideDuration).SetEase(Ease.InOutQuad));
        seq.Join(transform.DOScale(Vector3.one * parkScale, slideDuration).SetEase(Ease.InOutQuad));

        seq.OnComplete(() =>
        {
            // Force exact park scale (guard against tween floating-point drift)
            transform.localScale = Vector3.one * parkScale;
            _revealYOffset = 0f; // Ensure rotation is zeroed out
            Debug.Log($"[WorldCard] PARK  rootLocalScale={transform.localScale}  pos={parkPos}");
            onParked?.Invoke();
        });
    }

    /// <summary>
    /// Fade out, shrink, then destroy.
    /// </summary>
    public void HideWorldCard(Action onHidden)
    {
        _isBillboard = false;
        transform.DOKill();
        DOTween.Kill(this);

        Sequence seq = DOTween.Sequence()
            .SetId(this)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        seq.Append(DOTween.To(GetAlpha, SetAlpha, 0f, hideDuration));
        seq.Join(transform.DOScale(Vector3.one * 0.10f, hideDuration).SetEase(Ease.InBack));

        seq.OnComplete(() =>
        {
            // Reset scale to clean state before destroy
            transform.localScale = Vector3.one;
            Debug.Log("[WorldCard] Hidden — destroying.");
            onHidden?.Invoke();
            Destroy(gameObject);
        });
    }

    // 
    //  UNIFIED SIZE NORMALIZATION
    // 

    /// <summary>
    /// Restores both child renderers to their prefab-default localScales.
    /// Called at the start of every ShowWorldCard to prevent scale accumulation
    /// across successive reveals.
    /// </summary>
    private void ResetChildScales()
    {
        if (iconRenderer != null)   iconRenderer.transform.localScale  = _iconDefaultLocalScale;
        if (cardBGRenderer != null) cardBGRenderer.transform.localScale = _cardBGDefaultLocalScale;
    }

    /// <summary>
    /// Scales the ACTIVE child renderer (iconRenderer in icon mode, cardBGRenderer
    /// in card-art mode) so its world-width matches a single reference width.
    /// Reference = iconRenderer’s current world-width (preferred) or the
    /// serialized <see cref="referenceWorldWidth"/> fallback.
    /// </summary>
    private void FitActiveRenderer(bool useCardBGMode)
    {
        // 1. Compute reference width
        float refWidth = ComputeReferenceWidth();
        if (refWidth < 1e-5f)
        {
            Debug.LogWarning("[WorldCard] FitActiveRenderer: referenceWidth ≈ 0 — skipping.");
            return;
        }

        // 2. Determine active renderer
        SpriteRenderer active = useCardBGMode ? cardBGRenderer : iconRenderer;
        if (active == null || active.sprite == null) return;

        // 3. Current world width = sprite bounds.x * lossyScale.x (includes parent + child scale)
        float activeWidth = active.sprite.bounds.size.x * active.transform.lossyScale.x;
        if (activeWidth < 1e-5f) return;

        // 4. Scale factor applied to child localScale only (root anim scale untouched)
        float scaleFactor = refWidth / activeWidth;
        scaleFactor = Mathf.Clamp(scaleFactor, fitMinScale, fitMaxScale);

        Vector3 prev = active.transform.localScale;
        active.transform.localScale = prev * scaleFactor;

        float finalWidth = active.sprite.bounds.size.x * active.transform.lossyScale.x;

        Debug.Log($"[WorldCard] FitActiveRenderer  refWidth={refWidth:F4}  "
                + $"active='{active.name}' sprite='{active.sprite.name}'  "
                + $"widthBefore={activeWidth:F4}  scaleFactor={scaleFactor:F4}  "
                + $"widthAfter={finalWidth:F4}  childLocalScale={active.transform.localScale}");
    }

    /// <summary>
    /// Returns the reference world-width that all rewards should match.
    /// Prefers the iconRenderer’s live value (auto-detect) because money/nitro
    /// icons define “the standard”.  Falls back to the serialized field.
    /// </summary>
    private float ComputeReferenceWidth()
    {
        // Prefer iconRenderer (the "standard" — money / nitro icon size)
        if (iconRenderer != null && iconRenderer.sprite != null)
        {
            float w = iconRenderer.sprite.bounds.size.x * iconRenderer.transform.lossyScale.x;
            if (w > 1e-5f) return w;
        }

        // Serialized fallback (required when iconRenderer has no default sprite,
        // e.g. during card-art reveals on a fresh prefab instance).
        // Set this to the money/nitro icon’s world width for consistency.
        return referenceWorldWidth;
    }

    // 
    //  BILLBOARD
    // 

    private void LateUpdate()
    {
        if (!_isBillboard || _cam == null) return;
        Vector3 fwd = _cam.transform.forward;
        Quaternion billboard = Quaternion.LookRotation(fwd, Vector3.up);
        // D: Apply additive Y rotation offset from reveal animation
        if (_revealYOffset != 0f)
            billboard *= Quaternion.Euler(0f, _revealYOffset, 0f);
        transform.rotation = billboard;
    }

    // 
    //  ALPHA (SpriteRenderers + TMP)
    // 

    private float GetAlpha()
    {
        // Use iconRenderer as alpha source if it is active (icon mode);
        // otherwise fall back to cardBGRenderer (card-art mode with icon hidden).
        if (iconRenderer != null && iconRenderer.gameObject.activeSelf)
            return iconRenderer.color.a;
        if (cardBGRenderer != null)
            return cardBGRenderer.color.a;
        return 1f;
    }

    private void SetAlpha(float a)
    {
        SetSRAlpha(cardBGRenderer, a);
        // Only affect iconRenderer alpha when it is actually visible
        if (iconRenderer != null && iconRenderer.gameObject.activeSelf)
            SetSRAlpha(iconRenderer, a);
        if (overlayText != null)
        {
            Color c = overlayText.color;
            c.a = a;
            overlayText.color = c;
        }
    }

    private static void SetSRAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }
}