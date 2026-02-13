using UnityEngine;

public enum ChestState
{
    Idle,
    Unlocking,
    ReadyToOpen,
    Opened
}

public class Chest : MonoBehaviour
{
    [Header("Config")]
    public string chestName = "Wooden Chest";

    public double minReward = 840;
    public double maxReward = 1300;

    public int cardReward = 1;

    public int turboMin = 2;
    public int turboMax = 4;

    [Tooltip("30 dk = 1800 sn (test için bunu 30 yapabilirsin)")]
    public float unlockDurationSeconds = 1800f;

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
        // Toplanmamış chest ekrandan çıkarsa yok olsun
        if (autoDespawnOffScreen && !collected)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(transform.position);
                if (vp.z < 0f || vp.y < -offscreenMargin || vp.y > 1f + offscreenMargin)
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    // Yoldaki chest'e tıklanınca
    public void OnTapped()
    {
        if (collected) return;
        collected = true;

        // Try animated vanish; fallback to instant hide
        TapVanishAnimator vanish = GetComponent<TapVanishAnimator>();
        if (vanish != null && !vanish.IsPlaying)
        {
            // Disable colliders immediately so no double-tap
            if (colliders != null)
                foreach (var c in colliders)
                    if (c != null) c.enabled = false;

            vanish.Play(() =>
            {
                // Restore scale before hiding — chest is NOT destroyed and may be reused
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
        // Inventory’ye ekle
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.AddChestFromWorld(this);

        // ChestShown UI’yi güncelle (0 ise gizlesin, >0 ise göstersin)
        if (ChestShownUI.Instance != null)
            ChestShownUI.Instance.RefreshVisibilityAndCount();

        // Not: Popup burada açılmaz. Popup sadece ChestShown’a tıklayınca açılacak.
    }

    private void SetWorldVisible(bool visible)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (visible && transform.localScale.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning($"[Chest] SetWorldVisible(true) called but localScale is near zero ({transform.localScale}). " +
                             "This chest will appear invisible. Call TapVanishAnimator.ResetScale() first.", this);
        }
#endif

        if (renderers != null)
            foreach (var r in renderers)
                if (r != null) r.enabled = visible;

        if (colliders != null)
            foreach (var c in colliders)
                if (c != null) c.enabled = visible;
    }
}
