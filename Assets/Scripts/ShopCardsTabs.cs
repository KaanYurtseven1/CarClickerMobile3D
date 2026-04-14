using UnityEngine;
using UnityEngine.UI;

public class ShopCardsTabs : MonoBehaviour
{
    public static ShopCardsTabs Instance { get; private set; }

    [Header("Buttons")]
    public Button btnShopItems;
    public Button btnCards;

    [Header("Panels")]
    public GameObject scrollViewShopItems;
    public GameObject scrollViewCards;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (btnShopItems != null)
            btnShopItems.onClick.AddListener(ShowShopItems);

        if (btnCards != null)
            btnCards.onClick.AddListener(ShowCards);

        // Başlangıçta ShopItems açık olsun
        ShowShopItems();
    }

    public void ShowShopItems()
    {
        if (scrollViewShopItems != null) scrollViewShopItems.SetActive(true);
        if (scrollViewCards != null) scrollViewCards.SetActive(false);
    }

    public void ShowCards()
    {
        if (scrollViewShopItems != null) scrollViewShopItems.SetActive(false);
        if (scrollViewCards != null) scrollViewCards.SetActive(true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
