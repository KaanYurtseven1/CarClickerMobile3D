// ════════════════════════════════════════════════════════════════
// ShowcaseCarNameReveal.cs – Animates the car name + model text
// during the cinematic showcase.
//
// Two TMP_Text fields slide/fade in after a configurable delay,
// hold, then slide/fade out before the cinematic ends.
//
// SETUP:
//   1) Create two TMP_Text objects on the showcase Canvas:
//        - CarNameLabel  (e.g. "MAZDA")
//        - ModelNameLabel (e.g. "RX-7")
//   2) Attach this script to a parent or the Canvas itself.
//   3) Assign both labels.
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using TMPro;
using DG.Tweening;

public class ShowcaseCarNameReveal : MonoBehaviour
{
    // ─────────────────── Inspector ───────────────────
    [Header("── Text References ──")]
    [Tooltip("Main car brand name (e.g. 'MAZDA').")]
    [SerializeField] private TMP_Text carNameLabel;

    [Tooltip("Model/sub-name (e.g. 'RX-7').  Optional.")]
    [SerializeField] private TMP_Text modelNameLabel;

    [Header("── Animation ──")]
    [Tooltip("Seconds after Play() before the text starts appearing.")]
    [SerializeField] private float revealDelay = 1.5f;

    [Tooltip("Fade-in + slide-in duration.")]
    [SerializeField] private float revealDuration = 0.6f;

    [Tooltip("How long the text stays fully visible.")]
    [SerializeField] private float holdDuration = 3f;

    [Tooltip("Fade-out + slide-out duration.")]
    [SerializeField] private float hideDuration = 0.4f;

    [Tooltip("Horizontal slide distance in pixels.")]
    [SerializeField] private float slideDistance = 60f;

    // ─────────────────── Runtime ───────────────────
    private CanvasGroup _carNameGroup;
    private CanvasGroup _modelNameGroup;
    private Sequence _seq;

    // ═══════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Set the display text.  Called by ShowcaseCarSpawner after
    /// resolving the CarDataSO.
    /// </summary>
    public void SetCarInfo(string carName, string modelName)
    {
        if (carNameLabel != null) carNameLabel.text = carName ?? "";
        if (modelNameLabel != null) modelNameLabel.text = modelName ?? "";
    }

    /// <summary>Build and play the reveal → hold → hide animation.</summary>
    public void Play()
    {
        EnsureCanvasGroups();
        Hide();

        // K3: Car name reveal SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayCinematicNameReveal();

        BuildSequence();
    }

    /// <summary>Instantly hide both labels (e.g. on skip).</summary>
    public void Hide()
    {
        _seq?.Kill();
        _seq = null;

        if (_carNameGroup != null) _carNameGroup.alpha = 0f;
        if (_modelNameGroup != null) _modelNameGroup.alpha = 0f;
    }

    // ═══════════════════════════════════════════════════════════
    //  Internal
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        EnsureCanvasGroups();
        // Start hidden
        if (_carNameGroup != null) _carNameGroup.alpha = 0f;
        if (_modelNameGroup != null) _modelNameGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        _seq?.Kill();
    }

    private void EnsureCanvasGroups()
    {
        if (carNameLabel != null && _carNameGroup == null)
        {
            _carNameGroup = carNameLabel.GetComponent<CanvasGroup>();
            if (_carNameGroup == null)
                _carNameGroup = carNameLabel.gameObject.AddComponent<CanvasGroup>();
        }

        if (modelNameLabel != null && _modelNameGroup == null)
        {
            _modelNameGroup = modelNameLabel.GetComponent<CanvasGroup>();
            if (_modelNameGroup == null)
                _modelNameGroup = modelNameLabel.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void BuildSequence()
    {
        _seq = DOTween.Sequence()
            .SetUpdate(UpdateType.Normal, true)
            .SetLink(gameObject);

        // ── Delay ──
        _seq.AppendInterval(revealDelay);

        // ── Reveal car name ──
        if (_carNameGroup != null)
        {
            RectTransform rt = carNameLabel.rectTransform;
            Vector2 startPos = rt.anchoredPosition;
            Vector2 hidePos = startPos + Vector2.left * slideDistance;

            rt.anchoredPosition = hidePos;

            _seq.Append(_carNameGroup.DOFade(1f, revealDuration).SetEase(Ease.OutQuart));
            _seq.Join(rt.DOAnchorPos(startPos, revealDuration).SetEase(Ease.OutQuart));
        }

        // ── Reveal model name (staggered) ──
        if (_modelNameGroup != null)
        {
            RectTransform rt = modelNameLabel.rectTransform;
            Vector2 startPos = rt.anchoredPosition;
            Vector2 hidePos = startPos + Vector2.left * slideDistance;

            rt.anchoredPosition = hidePos;

            float stagger = 0.15f;
            _seq.Insert(
                revealDelay + stagger,
                _modelNameGroup.DOFade(1f, revealDuration).SetEase(Ease.OutQuart));
            _seq.Insert(
                revealDelay + stagger,
                rt.DOAnchorPos(startPos, revealDuration).SetEase(Ease.OutQuart));
        }

        // ── Hold ──
        _seq.AppendInterval(holdDuration);

        // ── Hide both ──
        if (_carNameGroup != null)
        {
            _seq.Append(_carNameGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
        }

        if (_modelNameGroup != null)
        {
            if (_carNameGroup != null)
                _seq.Join(_modelNameGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
            else
                _seq.Append(_modelNameGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
        }
    }
}
