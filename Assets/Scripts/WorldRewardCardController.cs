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

    [Header("Icon Size Normalization")]
    [Tooltip("Target world-space height for the icon sprite at localScale=1 on the iconRenderer child. \n"
           + "All reward icons are scaled so their SpriteRenderer renders at exactly this world height, \n"
           + "making Money / Nitro / Card reward cards identical in size. \n"
           + "Set to the natural bounds.size.y of your Money/Nitro icon sprites (check Debug logs). \n"
           + "Set to 0 to disable normalization (legacy behaviour).")]
    [SerializeField] private float normalizedIconWorldHeight = 1.0f;

    //  RUNTIME 
    private Camera _cam;
    private Transform _mouthAnchor;
    private Transform _parkAnchor;
    private bool _isBillboard;

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
                {
                    iconRenderer.sprite = icon;
                    NormalizeIconScale(icon);
                }
            }
        }

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
    //  ICON SIZE NORMALIZATION
    // 

    /// <summary>
    /// Adjusts <see cref="iconRenderer"/>'s local scale so the sprite always renders
    /// at <see cref="normalizedIconWorldHeight"/> world units tall, regardless of
    /// sprite texture dimensions or Pixels-Per-Unit setting.
    ///
    /// This is the minimal fix for the size divergence between Money/Nitro icon sprites
    /// (small, purpose-built) and the Card artwork sprite (large, card-sized artwork):
    /// both end up at the same on-screen height once the root transform applies parkScale.
    /// </summary>
    private void NormalizeIconScale(Sprite icon)
    {
        if (iconRenderer == null || icon == null) return;

        if (normalizedIconWorldHeight <= 0f)
        {
            // Normalization disabled — reset to prefab default so legacy behaviour is preserved.
            iconRenderer.transform.localScale = Vector3.one;
            Debug.Log($"[WorldCard] NormalizeIcon DISABLED (normalizedIconWorldHeight=0)  "
                    + $"sprite='{icon.name}' bounds.y={icon.bounds.size.y:F4}  using scale=1");
            return;
        }

        float spriteWorldHeight = icon.bounds.size.y;   // world units at localScale=1
        if (spriteWorldHeight < 1e-5f)
        {
            Debug.LogWarning($"[WorldCard] NormalizeIcon: sprite '{icon.name}' has near-zero bounds.y!  Skipping.");
            iconRenderer.transform.localScale = Vector3.one;
            return;
        }

        float corrective = normalizedIconWorldHeight / spriteWorldHeight;
        iconRenderer.transform.localScale = Vector3.one * corrective;

        Debug.Log($"[WorldCard] NormalizeIcon  sprite='{icon.name}' "
                + $"bounds.y={spriteWorldHeight:F4}  "
                + $"target={normalizedIconWorldHeight:F4}  "
                + $"corrective={corrective:F4}  "
                + $"iconLocalScale={iconRenderer.transform.localScale}");
    }

    // 
    //  BILLBOARD
    // 

    private void LateUpdate()
    {
        if (!_isBillboard || _cam == null) return;
        Vector3 fwd = _cam.transform.forward;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
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