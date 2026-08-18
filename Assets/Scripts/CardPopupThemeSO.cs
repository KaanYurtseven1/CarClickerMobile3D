using UnityEngine;

/// <summary>
/// Per-card visual theme for the card detail popup.
/// Create one asset per CardType via: Assets → Create → Cards → Popup Theme.
/// </summary>
[CreateAssetMenu(fileName = "NewCardPopupTheme", menuName = "Cards/Popup Theme")]
public class CardPopupThemeSO : ScriptableObject
{
    [Tooltip("Which card this theme applies to.")]
    public CardType type;

    [Header("Sprites")]
    [Tooltip("Sprite for the PopupPanel Image background.")]
    public Sprite popupPanelSprite;

    [Tooltip("Sprite for the Upgrade Button Image.")]
    public Sprite upgradeButtonSprite;

    [Header("Text Overrides")]
    [Tooltip("If not empty, overrides CardDefinition.displayName in the title.")]
    public string displayNameOverride;

    [Tooltip("Body text shown under the 'Value' label row.")]
    [TextArea(2, 4)]
    public string valueText;

    [Tooltip("Body text shown under the 'Description' label row.")]
    [TextArea(2, 6)]
    public string descriptionText;
}
