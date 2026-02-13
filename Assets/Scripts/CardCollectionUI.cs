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

    [Header("Prefab")]
    public CardSlotUI cardSlotPrefab;

    [Header("Detail Popup")]
    public CardDetailPopupController cardDetailPopup;

    private readonly List<CardSlotUI> slots = new List<CardSlotUI>();

    private void Awake()
    {
        Instance = this;
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

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void OnCardSlotClicked(CardDefinition def)
    {
        Debug.Log($"[CardCollectionUI] Card clicked: {def.displayName} (Type: {def.type})");

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
}
