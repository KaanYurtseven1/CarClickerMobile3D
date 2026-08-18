using UnityEngine;

/// <summary>
/// DEPRECATED: This script is disabled because CurrencyManager.Update() already handles
/// passive income with proper boost multiplier support and buffer accumulation.
/// Keeping the file to preserve any scene/prefab references.
/// </summary>
public class AutoIncome : MonoBehaviour
{
    private static bool _warnedOnce = false;

    private void Awake()
    {
        // Disable to prevent double passive income - CurrencyManager.Update() handles MPS
        enabled = false;

        if (!_warnedOnce)
        {
            Debug.LogWarning("[AutoIncome] Disabled: CurrencyManager already handles passive income with boost support.");
            _warnedOnce = true;
        }
    }

    // Original logic commented out - CurrencyManager handles this now
    /*
    private float timer;

    private void Update() {
        if(CurrencyManager.Instance == null) {
            return;
        }
        timer += Time.deltaTime;
        if(timer >= 1f) {
            timer -= 1f;
            CurrencyManager.Instance.AddMoney(CurrencyManager.Instance.moneyPerSecond);
        }
    }
    */
}
