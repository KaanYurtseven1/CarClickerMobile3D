using UnityEngine;

/// <summary>
/// Static helper that checks for pending card-progress rewards from
/// the Blacklist system and consumes them when a card is clicked.
///
/// Called by <see cref="CardCollectionUI.OnCardSlotClicked"/> before
/// the normal card detail popup opens.
/// </summary>
public static class CardProgressRewardHandler
{
    /// <summary>
    /// Checks if there is a pending card-progress reward.
    /// If so, adds copies to the given card, clears the pending state,
    /// and returns true (caller should skip the normal detail popup).
    /// Returns false if no pending reward.
    /// </summary>
    public static bool TryConsume(CardDefinition def)
    {
        if (def == null) return false;

        var claimData = BlacklistRewardClaimData.LoadFromPrefs();
        if (!claimData.HasPendingCardProgress) return false;

        int copies = claimData.pendingCardProgressAmount;

        // Grant the card copies
        if (CardManager.Instance != null)
        {
            CardManager.Instance.AddCardCopies(def.type, copies);
            Debug.Log($"[CardProgressReward] Applied +{copies} copies to card '{def.displayName}' ({def.type}).");
        }
        else
        {
            Debug.LogWarning("[CardProgressReward] CardManager.Instance is null, cannot grant card copies.");
            return false;
        }

        // Clear pending state
        claimData.pendingCardProgressAmount = 0;
        claimData.SaveToPrefs();

        // Save game
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();

        return true;
    }
}
