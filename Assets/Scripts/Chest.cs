using UnityEngine;

public class Chest : MonoBehaviour
{
    [Header("Chest Type")]
    public ChestType chestType = ChestType.Common;

    [Header("World Runtime")]
    public bool autoDespawnOffScreen = true;
    public float offscreenMargin = 0.1f;

    private Renderer[] renderers;
    private Collider[] colliders;

    private bool collected = false;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void Update()
    {
        if (autoDespawnOffScreen && !collected)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(transform.position);
                if (vp.z < 0f || vp.y < -offscreenMargin || vp.y > 1f + offscreenMargin)
                    Destroy(gameObject);
            }
        }
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
