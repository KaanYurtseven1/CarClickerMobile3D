using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI textMoney;
    public TextMeshProUGUI textMPS;
    public TextMeshProUGUI textMPT;

    private void Update()
    {

        if (CurrencyManager.Instance == null)
        {
            return;
        }

        textMoney.text = $"Money: {CurrencyManager.Instance.money:0}";
        textMPS.text = $"MPS: {System.Math.Round(CurrencyManager.Instance.moneyPerSecond):0}/s";

        // Apply TurboFinger multiplier to displayed MPT
        double baseMpt = CurrencyManager.Instance.moneyPerTap;
        float turboMultiplier = TurboFingerController.Instance != null
            ? TurboFingerController.Instance.CurrentMultiplier
            : 1f;
        double effectiveMpt = baseMpt * turboMultiplier;

        if (turboMultiplier > 1f)
        {
            textMPT.text = $"MPT: {effectiveMpt:0.0}/tap (x{turboMultiplier})";
        }
        else
        {
            textMPT.text = $"MPT: {effectiveMpt:0.0}/tap";
        }
    }
}
