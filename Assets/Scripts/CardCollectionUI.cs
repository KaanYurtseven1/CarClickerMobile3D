using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardCollectionUI : MonoBehaviour
{
    public static CardCollectionUI Instance;

    [Header("Roots")]
    public Transform foundRoot;        // Grid_Found
    public Transform notFoundRoot;     // Grid_NotFound
    public GameObject groupFound;      // Group_Found (tam grup, header + grid)
    public GameObject groupNotFound;   // Group_NotFound (tam grup, header + grid)

    [Header("Flicker Guard")]
    [Tooltip("Optional CanvasGroup on the Cards content (e.g. ScrollView_Cards/Viewport/Content). " +
             "If assigned, alpha is forced to 0 at the start of BuildSlots and back to 1 after the " +
             "rebuild + layout completes — prevents the 1-frame 'all colored → some grey' flicker on " +
             "first Cards tab open while leftover placeholder/stale children are still queued for " +
             "deferred Destroy.")]
    [SerializeField] private CanvasGroup contentCanvasGroup;

    [Header("Prefab")]
    public CardSlotUI cardSlotPrefab;

    [Header("Detail Popup")]
    public CardDetailPopupController cardDetailPopup;

    private readonly List<CardSlotUI> slots = new List<CardSlotUI>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        // Subscribe to CardManager event if available
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCardsChanged += OnCardsChangedHandler;
            Debug.Log("[CardCollectionUI] Subscribed to OnCardsChanged");
        }

        // Always rebuild when enabled (scene loaded / panel opened)
        Rebuild();
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCardsChanged -= OnCardsChangedHandler;
            Debug.Log("[CardCollectionUI] Unsubscribed from OnCardsChanged");
        }
    }

    private void OnCardsChangedHandler()
    {
        Debug.Log("[CardCollectionUI] OnCardsChanged event received, calling Rebuild");
        Rebuild();
    }

    private void Start()
    {
        // Rebuild is already called in OnEnable, but keep for safety
        Rebuild();
    }

    // Dışarıdan da çağırabilelim diye public yaptım
    public void Rebuild()
    {
        Debug.Log("[CardCollectionUI] Rebuild called");
        BuildSlots();
    }

    private void BuildSlots()
    {
        if (CardManager.Instance == null || cardSlotPrefab == null)
        {
            Debug.LogWarning("[CardCollectionUI] BuildSlots aborted: CardManager or prefab is null");
            return;
        }

        // Flicker guard: hide content while we tear down + repopulate slots so the
        // user never sees stale colored placeholders or the prefab's default colored
        // state for a single frame before Setup/Refresh applies the locked sprite.
        if (contentCanvasGroup != null)
            contentCanvasGroup.alpha = 0f;

        // Eski çocukları temizle
        ClearChildren(foundRoot);
        ClearChildren(notFoundRoot);
        slots.Clear();

        bool anyFound = false;
        bool anyLocked = false;

        Debug.Log("[CardCollectionUI] --- Building card slots ---");

        foreach (var def in CardManager.Instance.cards)
        {
            // Debug each card's state
            Debug.Log($"[CardCollectionUI] Card: {def.type}, Level: {def.currentLevel}, Copies: {def.copiesOwned}, IsUnlocked: {def.IsUnlocked}");

            bool unlocked = def.IsUnlocked; // (currentLevel > 0 || copiesOwned > 0)
            Transform parent = unlocked ? foundRoot : notFoundRoot;

            var slot = Instantiate(cardSlotPrefab, parent);
            slot.Setup(def, OnCardSlotClicked);
            slots.Add(slot);

            if (unlocked)
                anyFound = true;
            else
                anyLocked = true;
        }

        Debug.Log($"[CardCollectionUI] Summary: anyFound={anyFound}, anyLocked={anyLocked}");

        // Toggle group visibility based on what we have
        if (groupFound != null)
            groupFound.SetActive(anyFound);

        if (groupNotFound != null)
            groupNotFound.SetActive(anyLocked);

        // Force layout rebuild to ensure UI updates immediately
        ForceLayoutRebuild();

        // Reveal content now that every slot has Setup/Refresh + layout applied.
        if (contentCanvasGroup != null)
            contentCanvasGroup.alpha = 1f;
    }

    private void ForceLayoutRebuild()
    {
        Canvas.ForceUpdateCanvases();

        if (foundRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(foundRoot as RectTransform);
        }
        if (notFoundRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(notFoundRoot as RectTransform);
        }

        Debug.Log("[CardCollectionUI] Layout rebuild forced");
    }

    private void ClearChildren(Transform root)
    {
        if (root == null) return;

        int childCount = root.childCount;
        if (childCount > 0)
        {
            // Authored placeholders / stale slots from a previous BuildSlots are
            // expected to be cleared here. Warn so designers can audit
            // Grid_Found / Grid_NotFound in the Editor — any authored CardSlot
            // children would also have shown up briefly with the prefab's
            // default colored visuals on first Cards tab open.
            Debug.LogWarning($"[CardCollectionUI] ClearChildren: found {childCount} pre-existing child(ren) under '{root.name}'. " +
                             "If this fires on the FIRST BuildSlots after scene load, there are authored placeholder " +
                             "slots in the scene — inspect Grid_Found/Grid_NotFound in the Hierarchy and remove them.");
        }

        for (int i = childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            // Force-deactivate BEFORE Destroy so Unity's deferred (end-of-frame)
            // destruction cannot render the stale child for one more frame.
            if (child.activeSelf) child.SetActive(false);
            Destroy(child);
        }
    }

    private void OnCardSlotClicked(CardDefinition def)
    {
        Debug.Log($"[CardCollectionUI] Card clicked: {def.displayName} (Type: {def.type})");

        // Check for pending Blacklist card-progress reward
        if (CardProgressRewardHandler.TryConsume(def))
        {
            Debug.Log($"[CardCollectionUI] Card-progress reward consumed for '{def.displayName}'. Refreshing slots.");
            RefreshAll();
            return;
        }

        // Try to open the detail popup
        if (cardDetailPopup != null)
        {
            cardDetailPopup.Show(def);
        }
        else if (CardDetailPopupController.Instance != null)
        {
            // Fallback to singleton if not assigned
            Debug.LogWarning("[CardCollectionUI] cardDetailPopup not assigned, using singleton.");
            CardDetailPopupController.Instance.Show(def);
        }
        else
        {
            Debug.LogWarning("[CardCollectionUI] No CardDetailPopupController found! Cannot show card details.");
        }
    }

    public void RefreshAll()
    {
        foreach (var s in slots)
            s.Refresh();
    }

    /// <summary>
    /// Returns the active <see cref="CardSlotUI"/> displaying the given <paramref name="type"/>,
    /// or null if no matching slot exists yet (e.g. <see cref="BuildSlots"/> hasn't run).
    /// Used by the tutorial system to anchor pointer/click-restriction logic on the
    /// card the player just earned from the first free chest.
    /// </summary>
    public CardSlotUI GetSlotForType(CardType type)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            CardSlotUI s = slots[i];
            if (s == null) continue;
            CardDefinition def = s.Card;
            if (def != null && def.type == type) return s;
        }
        return null;
    }

    /// <summary>Read-only access to the live slot list. Order matches <see cref="BuildSlots"/>.</summary>
    public System.Collections.Generic.IReadOnlyList<CardSlotUI> Slots => slots;
}
