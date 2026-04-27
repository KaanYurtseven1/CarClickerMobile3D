using UnityEngine;
using System;
using DG.Tweening;

public class Chest : MonoBehaviour
{
    /// <summary>
    /// Fired when the deterministic tutorial Common Chest crosses its configured Z
    /// freeze threshold (toward the camera). Used by <see cref="TutorialManager"/> to
    /// freeze gameplay and start the idle bounce loop on the chest.
    /// </summary>
    public static event Action<Chest> OnTutorialChestReachedCenter;

    /// <summary>
    /// Fired when the deterministic tutorial Common Chest is tapped by the player.
    /// Used by <see cref="TutorialManager"/> to release the gameplay freeze immediately.
    /// </summary>
    public static event Action<Chest> OnTutorialChestCollected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsChest()
    {
        OnTutorialChestReachedCenter = null;
        OnTutorialChestCollected = null;
    }

    [Header("Chest Type")]
    public ChestType chestType = ChestType.Common;

    [Header("World Runtime")]
    public bool autoDespawnOffScreen = true;
    public float offscreenMargin = 0.1f;

    private Renderer[] renderers;
    private Collider[] colliders;

    private bool collected = false;

    // ── Tutorial-mode state (mirrors NitroCoin tutorial pattern) ──
    /// <summary>True when this chest is the deterministic tutorial Common Chest.</summary>
    public bool IsTutorial { get; private set; }
    private float _tutorialFreezeZ;
    private bool _tutorialCenterTriggered;
    private float _tutorialMoveDirSign;
    private Tween _tutorialBounceTween;
    private Vector3 _tutorialBounceBaseScale;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    /// <summary>
    /// Marks this chest as the deterministic tutorial Common Chest. Spawner calls
    /// this immediately after Instantiate. The chest will, on its way down, raise
    /// <see cref="OnTutorialChestReachedCenter"/> when it crosses <paramref name="freezeZ"/>
    /// toward the camera and start a subtle bounce loop.
    /// </summary>
    public void MarkAsTutorialChest(float freezeZ)
    {
        IsTutorial = true;
        _tutorialFreezeZ = freezeZ;
        _tutorialCenterTriggered = false;
        _tutorialMoveDirSign = Mathf.Sign(freezeZ - transform.position.z);
        if (_tutorialMoveDirSign == 0f) _tutorialMoveDirSign = -1f;
        TutorialGate.SetTutorialChestActive(true);
    }

    private void OnDestroy()
    {
        if (_tutorialBounceTween != null)
        {
            _tutorialBounceTween.Kill();
            _tutorialBounceTween = null;
        }
        if (IsTutorial && TutorialGate.TutorialChestActive)
            TutorialGate.SetTutorialChestActive(false);
    }

    private void Update()
    {
        // Tutorial chest: detect Z-center crossing once, then halt and notify.
        if (IsTutorial && !_tutorialCenterTriggered)
        {
            bool crossed = (_tutorialMoveDirSign > 0f && transform.position.z >= _tutorialFreezeZ) ||
                           (_tutorialMoveDirSign < 0f && transform.position.z <= _tutorialFreezeZ);
            if (crossed)
            {
                _tutorialCenterTriggered = true;
                StartTutorialBounce();
                OnTutorialChestReachedCenter?.Invoke(this);
                return;
            }
        }

        // Off-screen auto-despawn: never destroy the tutorial chest while the
        // tutorial freeze is active or while the player has not yet tapped it.
        if (autoDespawnOffScreen && !collected)
        {
            if (IsTutorial && (TutorialGate.GameplayFrozen || _tutorialCenterTriggered))
                return;

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(transform.position);
                if (vp.z < 0f || vp.y < -offscreenMargin || vp.y > 1f + offscreenMargin)
                    Destroy(gameObject);
            }
        }
    }

    private void StartTutorialBounce()
    {
        if (_tutorialBounceTween != null && _tutorialBounceTween.IsActive())
            return;

        _tutorialBounceBaseScale = transform.localScale;
        Vector3 peak = _tutorialBounceBaseScale * 1.18f;

        _tutorialBounceTween = transform
            .DOScale(peak, 0.55f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    public void OnTapped()
    {
        if (collected) return;

        // Reject if inventory is full
        if (ChestInventoryManager.Instance != null && ChestInventoryManager.Instance.IsInventoryFull)
        {
            Debug.Log("[Chest] Inventory full \u2014 chest not collected.");
            return;
        }

        collected = true;

        // Tutorial chest: stop the bounce loop and clear the active-tutorial-chest flag
        // before any further hooks. TutorialManager subscribes to OnTutorialChestCollected
        // to release the gameplay freeze immediately on tap (mirrors Nitro tutorial).
        if (IsTutorial)
        {
            if (_tutorialBounceTween != null)
            {
                _tutorialBounceTween.Kill();
                _tutorialBounceTween = null;
            }
            transform.localScale = _tutorialBounceBaseScale == Vector3.zero
                ? transform.localScale
                : _tutorialBounceBaseScale;
            TutorialGate.SetTutorialChestActive(false);
            try { OnTutorialChestCollected?.Invoke(this); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        // World chest collect SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayWorldChestCollect();

        // Fade out interactable highlight in sync with vanish animation
        var highlight = GetComponent<InteractableHighlight>();
        if (highlight != null)
            highlight.FadeOut(0.2f);

        TapVanishAnimator vanish = GetComponent<TapVanishAnimator>();
        if (vanish != null && !vanish.IsPlaying)
        {
            if (colliders != null)
                foreach (var c in colliders)
                    if (c != null) c.enabled = false;

            vanish.Play(() =>
            {
                vanish.ResetScale();
                SetWorldVisible(false);
                CollectChest();
            });
        }
        else
        {
            SetWorldVisible(false);
            CollectChest();
        }
    }

    private void CollectChest()
    {
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.AddChestFromWorld(this);

        if (ChestShownUI.Instance != null)
            ChestShownUI.Instance.RefreshSlots();
    }

    private void SetWorldVisible(bool visible)
    {
        if (renderers != null)
            foreach (var r in renderers)
                if (r != null) r.enabled = visible;

        if (colliders != null)
            foreach (var c in colliders)
                if (c != null) c.enabled = visible;
    }
}
