using System;
using UnityEngine;

/// <summary>
/// Placeholder rewarded-ad provider. Always succeeds immediately.
/// Replace the body of ShowRewardedAd with your real SDK when ready.
/// </summary>
public static class AdProvider
{
    /// <summary>
    /// Requests a rewarded ad. Calls onRewarded on success, onFailed on failure.
    /// Current: DUMMY provider — always succeeds instantly.
    /// </summary>
    public static void ShowRewardedAd(Action onRewarded, Action onFailed = null)
    {
        Debug.Log("[AdProvider] Showing rewarded ad (DUMMY — auto-success).");
        onRewarded?.Invoke();
    }
}
