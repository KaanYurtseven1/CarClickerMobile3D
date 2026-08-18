using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public enum UpgradeType
{
    Tap,        // Money per tap
    MPS,        // Money per second
    Global      // Hem MPT hem MPS için çarpan
}

public class UpgradeButton : MonoBehaviour
{
    [Header("Setup")]
    public UpgradeType upgradeType;
    public TextMeshProUGUI labelText;
    public Button button;

    [Header("Economy")]
    public double baseCost = 25;
    public double costMultiplier = 1.15;

    // Tap upgrade için
    public double tapIncreasePerLevel = 3;

    // MPS upgrade için
    public double mpsIncreasePerLevel = 2;

    // Global upgrade için (örn. 0.1 = %10 artış)
    public double globalMultiplierPerLevel = 0.12;

    [Header("Runtime")]
    public int currentLevel = 0;

    private double currentCost = -1;
    private double lastCost = -1;
    private int lastLevel = -1;
    private bool lastInteractable = true;

    private void Start()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnClickUpgrade);

        if (currentCost <= 0)
            currentCost = baseCost;

        RefreshUI(true);
    }

    private void Update()
    {
        if (CurrencyManager.Instance == null || button == null) return;

        bool canAfford = CurrencyManager.Instance.money >= currentCost;

        if (canAfford != lastInteractable)
        {
            lastInteractable = canAfford;
            button.interactable = canAfford;
        }

        // Level ya da cost değiştiyse label yenile
        if (System.Math.Abs(currentCost - lastCost) > 0.0001 || currentLevel != lastLevel)
        {
            RefreshUI(false);
        }
    }

    private void OnClickUpgrade()
    {
        if (CurrencyManager.Instance == null) return;

        if (!CurrencyManager.Instance.TrySpendMoney(currentCost))
            return;

        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayUpgrade();
        }

        currentLevel++;

        // Sync to DDOL registry so save works from any scene
        SyncToRegistry();

        // Notify CardManager for purchase-based activations
        if (CardManager.Instance != null)
        {
            CardManager.Instance.NotifyPurchase();
        }

        var cm = CurrencyManager.Instance;

        switch (upgradeType)
        {
            case UpgradeType.Tap:
                cm.IncreaseTapIncome(tapIncreasePerLevel);
                break;

            case UpgradeType.MPS:
                cm.IncreaseMPS(mpsIncreasePerLevel);
                break;

            case UpgradeType.Global:
                double factor = 1.0 + globalMultiplierPerLevel;
                cm.moneyPerTap *= factor;
                cm.moneyPerSecond *= factor;
                break;
        }

        currentCost *= costMultiplier;
        RefreshUI(true);
    }

    private void RefreshUI(bool forceButtonState)
    {
        if (button != null && forceButtonState && CurrencyManager.Instance != null)
        {
            bool canAfford = CurrencyManager.Instance.money >= currentCost;
            button.interactable = canAfford;
            lastInteractable = canAfford;
        }

        if (labelText != null)
        {
            string upgradeName = upgradeType switch
            {
                UpgradeType.Tap => "Tap Upgrade",
                UpgradeType.MPS => "Engine Upgrade",
                UpgradeType.Global => "Global Upgrade",
                _ => "Upgrade"
            };

            labelText.text =
                $"{upgradeName}\n" +
                $"Lv. {currentLevel}\n" +
                $"Cost: {currentCost:0}";
        }

        lastCost = currentCost;
        lastLevel = currentLevel;
    }

    // ---- SAVE/LOAD ----
    public void LoadFromSave(int level)
    {
        currentLevel = level;
        currentCost = baseCost * Math.Pow(costMultiplier, currentLevel);
        RefreshUI(true);
        SyncToRegistry();
    }

    /// <summary>
    /// Re-applies the cumulative effect of all purchased levels to CurrencyManager.
    /// Called after building recalc on load to restore upgrade contributions
    /// that were wiped when buildings reset MPS/MPT to building-only values.
    /// </summary>
    public void ReapplyEffect()
    {
        if (CurrencyManager.Instance == null || currentLevel <= 0) return;

        var cm = CurrencyManager.Instance;
        switch (upgradeType)
        {
            case UpgradeType.Tap:
                cm.IncreaseTapIncome(tapIncreasePerLevel * currentLevel);
                break;
            case UpgradeType.MPS:
                cm.IncreaseMPS(mpsIncreasePerLevel * currentLevel);
                break;
            case UpgradeType.Global:

                double factor = Math.Pow(1.0 + globalMultiplierPerLevel, currentLevel);
                cm.moneyPerTap *= factor;
                cm.moneyPerSecond *= factor;
                break;
        }
    }

    public int GetCurrentLevel()
    {
        return currentLevel;
    }

    private void SyncToRegistry()
    {
        if (UpgradeSaveRegistry.Instance != null)
            UpgradeSaveRegistry.Instance.Register(upgradeType.ToString(), currentLevel);
    }
}
