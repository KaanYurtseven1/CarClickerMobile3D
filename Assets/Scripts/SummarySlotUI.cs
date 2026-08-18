using UnityEngine;

/// <summary>
/// Lightweight UI component for a single dynamically generated summary slot.
/// Attach to the SummarySlotPrefab root. Contains one SpriteRenderer that
/// displays the reward icon (money, nitro, card art, or sticker).
/// </summary>
public class SummarySlotUI : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rewardImage;

    /// <summary>Direct access for fade animations.</summary>
    public SpriteRenderer RewardImage => rewardImage;

    /// <summary>Set the sprite shown by this slot.</summary>
    public void SetSprite(Sprite sprite)
    {
        if (rewardImage != null && sprite != null)
            rewardImage.sprite = sprite;
    }
}
