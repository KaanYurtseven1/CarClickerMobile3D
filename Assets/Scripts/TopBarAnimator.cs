using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;

public class TopBarAnimator : MonoBehaviour
{
    public static TopBarAnimator Instance { get; private set; }

    /// <summary>Fired after the compact/expand transition finishes. Parameter = isCompact.</summary>
    public event Action<bool> OnCompactChanged;

    public bool IsCompact => isCompact;

    // CanvasGroups excluded from compact/expand transitions.
    // Controllers that manage their own visibility (e.g. BoostModeController)
    // should call ExcludeFromCompact so TopBarAnimator doesn't fight over SetActive/alpha.
    private readonly HashSet<CanvasGroup> _excludedGroups = new HashSet<CanvasGroup>();

    /// <summary>Exclude a CanvasGroup from compact/expand transitions.
    /// The excluded CG will not be faded, activated, or deactivated by TopBarAnimator.</summary>
    public void ExcludeFromCompact(CanvasGroup cg) { if (cg != null) _excludedGroups.Add(cg); }

    /// <summary>Re-include a previously excluded CanvasGroup in compact/expand transitions.</summary>
    public void IncludeInCompact(CanvasGroup cg) { if (cg != null) _excludedGroups.Remove(cg); }

    /// <summary>True if <paramref name="cg"/> is the tutorial-gated Btn_Garage group AND
    /// the dedicated tutorial reveal has not yet handed ownership back to TopBarAnimator.
    /// While true, SetCompact() must not fade/activate/deactivate this group —
    /// TutorialManager owns its first reveal (see <see cref="TutorialGate.BtnGarageOwnedByTutorial"/>).
    /// Once the dedicated reveal completes (or on cold reload past Step 19),
    /// the gate flips false and TopBarAnimator manages Btn_Garage normally.</summary>
    private bool IsBtnGarageGated(CanvasGroup cg)
    {
        if (cg == null) return false;
        bool isBtnGarage =
            (btnGarageGroup != null && cg == btnGarageGroup) ||
            (btnGarageGroup == null && cg.gameObject.name == "Btn_Garage");
        if (!isBtnGarage) return false;
        return TutorialGate.BtnGarageOwnedByTutorial;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [Header("Root")]
    [SerializeField] private RectTransform topBar;

    [Header("Always Visible (move up/down)")]
    [SerializeField] private RectTransform moneyBig;   // Text_MoneyBig
    [SerializeField] private RectTransform mpsLine;    // Text_MPSLine

    [Header("Hide When Compact (add CanvasGroup to each)")]
    [SerializeField] private CanvasGroup[] hideGroups;

    [Header("Tutorial-Gated Groups (skipped until TutorialGate.GarageUnlocked)")]
    [Tooltip("Optional. CanvasGroup of Btn_Garage. While TutorialGate.GarageUnlocked is false this group is left untouched by SetCompact() so the garage tutorial owns its first reveal. If left empty, name match 'Btn_Garage' is used as a fallback.")]
    [SerializeField] private CanvasGroup btnGarageGroup;

    [Header("Heights")]
    [SerializeField] private float normalHeight = 220f;
    [SerializeField] private float compactHeight = 120f;

    [Header("Move Up (Anchored Y)")]
    [SerializeField] private float moneyBigNormalY = -60f;
    [SerializeField] private float moneyBigCompactY = -30f;
    [SerializeField] private float mpsNormalY = -120f;
    [SerializeField] private float mpsCompactY = -70f;

    [Header("Timing")]
    [SerializeField] private float duration = 0.30f;
    [SerializeField] private Ease ease = Ease.OutCubic;

    private Sequence seq;
    private bool isCompact;

    private void Awake()
    {
        Debug.Log($"[Lifecycle][GarageDebug] TopBarAnimator Awake. hideGroups.Length={(hideGroups != null ? hideGroups.Length : 0)}");
        if (hideGroups != null)
        {
            for (int i = 0; i < hideGroups.Length; i++)
            {
                if (hideGroups[i] == null) continue;
                Debug.Log($"[TopBar][GarageDebug] hideGroups[{i}] = '{hideGroups[i].gameObject.name}' activeSelf={hideGroups[i].gameObject.activeSelf}");
            }
        }
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(gameObject);
    }

    private void Reset()
    {
        topBar = GetComponent<RectTransform>();
    }

    private void OnDisable()
    {
        // Kill any active sequence to prevent callbacks to disabled/destroyed objects
        if (seq != null && seq.IsActive())
        {
            seq.Kill();
            seq = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Safety: ensure sequence is killed on destroy
        if (seq != null)
        {
            seq.Kill();
            seq = null;
        }
    }

    // ---- Convenience wrappers (used by PoliceCatchController etc.) ----
    /// <summary>Same as SetCompact(true) — hides extra rows with the shop/cards animation.</summary>
    public void HideAnimated() => SetCompact(true);
    /// <summary>Same as SetCompact(false) — restores full top bar.</summary>
    public void ShowAnimated() => SetCompact(false);

    public void SetCompact(bool compact)
    {
        if (isCompact == compact) return;
        Debug.Log($"[TopBar][GarageDebug] SetCompact({compact}) called. Will toggle hideGroups GameObjects.\n" + System.Environment.StackTrace);
        isCompact = compact;

        // Kill previous sequence
        if (seq != null && seq.IsActive()) seq.Kill();

        // Kill any existing tweens on targets to prevent overlapping animations
        if (topBar != null) topBar.DOKill();
        if (moneyBig != null) moneyBig.DOKill();
        if (mpsLine != null) mpsLine.DOKill();
        if (hideGroups != null)
        {
            foreach (var cg in hideGroups)
            {
                if (cg != null) cg.DOKill();
            }
        }

        // Create new sequence with SetUpdate(true) for UI (ignores timeScale)
        seq = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);

        float targetH = compact ? compactHeight : normalHeight;

        // 1) Height + move main texts (same time)
        seq.Join(topBar.DOSizeDelta(new Vector2(topBar.sizeDelta.x, targetH), duration).SetEase(ease));

        if (moneyBig != null)
            seq.Join(moneyBig.DOAnchorPosY(compact ? moneyBigCompactY : moneyBigNormalY, duration).SetEase(ease));

        if (mpsLine != null)
            seq.Join(mpsLine.DOAnchorPosY(compact ? mpsCompactY : mpsNormalY, duration).SetEase(ease));

        // 2) Hide/show groups ALL AT ONCE
        if (hideGroups != null && hideGroups.Length > 0)
        {
            if (compact)
            {
                // Hepsini aynı anda fade-out başlat
                foreach (var cg in hideGroups)
                {
                    if (!cg || _excludedGroups.Contains(cg)) continue;
                    if (IsBtnGarageGated(cg))
                    {
                        Debug.Log($"[TopBar][GarageDebug] SetCompact(true) SKIP Btn_Garage because TutorialGate.BtnGarageOwnedByTutorial=true. activeSelf={cg.gameObject.activeSelf}");
                        continue;
                    }
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                    // aktif kalsın ki fade çalışsın
                    if (!cg.gameObject.activeSelf) cg.gameObject.SetActive(true);
                    seq.Join(cg.DOFade(0f, duration * 0.8f).SetEase(Ease.OutQuad));
                }

                // Fade bitince hepsini aynı anda kapat
                seq.AppendCallback(() =>
                {
                    foreach (var cg in hideGroups)
                    {
                        if (!cg || _excludedGroups.Contains(cg)) continue;
                        if (IsBtnGarageGated(cg)) continue;
                        cg.gameObject.SetActive(false);
                    }
                });
            }
            else
            {
                // Hepsini aynı anda aç + alpha 0 yap
                seq.AppendCallback(() =>
                {
                    foreach (var cg in hideGroups)
                    {
                        if (!cg || _excludedGroups.Contains(cg)) continue;
                        if (cg == btnGarageGroup || (btnGarageGroup == null && cg.gameObject.name == "Btn_Garage"))
                        {
                            Debug.Log($"[TopBar][GarageReveal] SetCompact(false) considering Btn_Garage: garageUnlocked={TutorialGate.GarageUnlocked}, ownedByTutorial={TutorialGate.BtnGarageOwnedByTutorial}, activeBefore={cg.gameObject.activeSelf}");
                        }
                        if (IsBtnGarageGated(cg))
                        {
                            Debug.Log("[TopBar][GarageReveal] SKIP Btn_Garage during generic TopBar restore");
                            Debug.Log($"[TopBar][GarageDebug] SetCompact(false) SKIP Btn_Garage because TutorialGate.BtnGarageOwnedByTutorial=true. activeSelf={cg.gameObject.activeSelf}");
                            continue;
                        }
                        Debug.Log($"[TopBar][GarageDebug] SetCompact(false) re-activating hideGroup '{cg.gameObject.name}' (was activeSelf={cg.gameObject.activeSelf})");
                        if (cg.gameObject.name == "Btn_Garage")
                            Debug.Log("[TopBar][GarageDebug] SetCompact(false) allow Btn_Garage because tutorial ownership released");
                        cg.gameObject.SetActive(true);
                        cg.alpha = 0f;
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                    }
                });

                // Hepsini aynı anda fade-in
                foreach (var cg in hideGroups)
                {
                    if (!cg || _excludedGroups.Contains(cg)) continue;
                    if (IsBtnGarageGated(cg)) continue;
                    seq.Join(cg.DOFade(1f, duration * 0.8f).SetEase(Ease.OutQuad));
                }

                // Fade bitince hepsini aynı anda interactable yap
                seq.AppendCallback(() =>
                {
                    foreach (var cg in hideGroups)
                    {
                        if (!cg || _excludedGroups.Contains(cg)) continue;
                        if (IsBtnGarageGated(cg)) continue;
                        cg.interactable = true;
                        cg.blocksRaycasts = true;
                    }
                });
            }
        }

        // Notify subscribers after the transition completes
        bool compactCapture = compact;
        seq.OnComplete(() => OnCompactChanged?.Invoke(compactCapture));

        seq.Play();
    }
}
