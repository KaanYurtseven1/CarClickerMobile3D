using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    [Header("Setup")]
    public BuildingType buildingType;
    public TextMeshProUGUI labelText;
    public Button button;

    private double currentCost;
    private double lastCost = -1;
    private int lastOwned = -1;

    private void Start()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnClickBuy);

        RefreshData(true);
    }

    private void Update()
    {
        if (CurrencyManager.Instance == null || BuildingManager.Instance == null || button == null)
            return;

        RefreshData(false);
    }

    private void RefreshData(bool forceLabel)
    {
        BuildingDefinition b = BuildingManager.Instance.GetBuilding(buildingType);
        if (b == null) return;

        currentCost = BuildingManager.Instance.GetCurrentCost(buildingType);
        bool canAfford = CurrencyManager.Instance.money >= currentCost;
        button.interactable = canAfford;

        // Sadece cost veya owned değiştiyse label güncelle
        if (forceLabel || System.Math.Abs(currentCost - lastCost) > 0.0001 || b.count != lastOwned)
        {
            lastCost = currentCost;
            lastOwned = b.count;
            UpdateLabel(b);
        }
    }

    private void OnClickBuy()
    {
        if (BuildingManager.Instance == null) return;

        bool success = BuildingManager.Instance.TryBuyBuilding(buildingType);

        if (success)
        {
            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayBuildingBuy();
            }

            RefreshData(true);
        }
    }

    private void UpdateLabel(BuildingDefinition b)
    {
        if (labelText == null) return;

        labelText.text =
            $"{b.displayName}\n" +
            $"Owned: {b.count}\n" +
            $"Cost: {currentCost:0}";
    }
}
