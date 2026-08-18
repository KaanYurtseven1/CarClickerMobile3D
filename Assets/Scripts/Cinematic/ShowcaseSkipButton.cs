// ════════════════════════════════════════════════════════════════
// ShowcaseSkipButton.cs – Tap-anywhere overlay for the cinematic
// car showcase.  Each tap smoothly advances to the next shot.
//
// SETUP:
//   1) Create a Canvas → full-screen transparent Button.
//   2) Attach this script to the Button's GameObject.
//   3) Assign the CarShowcaseDirector reference.
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(Button))]
public class ShowcaseSkipButton : MonoBehaviour
{
    // ─────────────────── Inspector ───────────────────
    [Header("── References ──")]
    [Tooltip("The showcase director to advance.")]
    [SerializeField] private CarShowcaseDirector director;

    [Header("── Hint Text ──")]
    [Tooltip("Optional 'Tap to continue' label.")]
    [SerializeField] private TMP_Text hintLabel;

    [Tooltip("Seconds before the hint appears.")]
    [SerializeField] private float hintDelay = 2f;

    [Tooltip("Duration of the hint fade-in.")]
    [SerializeField] private float hintFadeDuration = 0.4f;

    // ─────────────────── Runtime ───────────────────
    private Button _button;
    private CanvasGroup _hintGroup;
    private Tween _hintTween;

    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnTapped);

        if (hintLabel != null)
        {
            _hintGroup = hintLabel.GetComponent<CanvasGroup>();
            if (_hintGroup == null)
                _hintGroup = hintLabel.gameObject.AddComponent<CanvasGroup>();
            _hintGroup.alpha = 0f;
        }
    }

    private void Start()
    {
        if (_hintGroup != null)
        {
            _hintTween = _hintGroup
                .DOFade(1f, hintFadeDuration)
                .SetDelay(hintDelay)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }

    private void OnDestroy()
    {
        _hintTween?.Kill();
        if (_button != null)
            _button.onClick.RemoveListener(OnTapped);
    }

    // ═══════════════════════════════════════════════════════════

    private void OnTapped()
    {
        if (director == null || !director.IsPlaying) return;

        // Advance to the next shot (smooth transition).
        director.GoToNextShot();
    }
}
