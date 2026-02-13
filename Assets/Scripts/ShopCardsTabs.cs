using UnityEngine;
using UnityEngine.UI;

public class ShopCardsTabs : MonoBehaviour
{
    [Header("Buttons")]
    public Button btnShopItems;
    public Button btnCards;

    [Header("Panels")]
    public GameObject scrollViewShopItems;
    public GameObject scrollViewCards;

    private void Start()
    {
        if (btnShopItems != null)
            btnShopItems.onClick.AddListener(ShowShopItems);

        if (btnCards != null)
            btnCards.onClick.AddListener(ShowCards);

        // Başlangıçta ShopItems açık olsun
        ShowShopItems();
    }

    private void ShowShopItems()
    {
        if (scrollViewShopItems != null) scrollViewShopItems.SetActive(true);
        if (scrollViewCards != null) scrollViewCards.SetActive(false);
    }

    private void ShowCards()
    {
        if (scrollViewShopItems != null) scrollViewShopItems.SetActive(false);
        if (scrollViewCards != null) scrollViewCards.SetActive(true);
    }
}
