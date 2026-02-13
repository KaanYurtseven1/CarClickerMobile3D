using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Helper component placed on each OfferSlot GameObject inside Section_DailyOffers.
/// Holds references to the child UI elements so DailyOffersController can drive them.
/// </summary>
public class DailyOfferSlotUI : MonoBehaviour
{
    [Header("Required References")]
    [Tooltip("The Button on this slot (click handler is wired by DailyOffersController).")]
    public Button button;

    [Tooltip("Icon image inside BG_Frame/Icon.")]
    public Image icon;

    [Tooltip("Title label – for free slot shows reward name; for card slots shows CardType + rarity.")]
    public TMP_Text titleText;

    [Tooltip("Label shown while the offer is available (e.g. 'FREE' / price).")]
    public TMP_Text freeText;

    [Tooltip("Label shown after purchase/claim (e.g. 'CLAIMED').")]
    public TMP_Text purchasedText;

    [Header("Card-Slot Only (leave null for Free slot)")]
    [Tooltip("Progress bar fill image (Image.fillAmount driven by controller).")]
    public Image barFill;

    [Tooltip("Progress text, e.g. '1/2'.")]
    public TMP_Text progressText;

    // ---- State helpers ----

    /// <summary>
    /// Show the slot in "available" state (can click to claim/buy).
    /// </summary>
    public void SetAvailable(string labelText)
    {
        if (freeText != null) { freeText.gameObject.SetActive(true); freeText.text = labelText; }
        if (purchasedText != null) purchasedText.gameObject.SetActive(false);
        if (button != null) button.interactable = true;
    }

    /// <summary>
    /// Show the slot in "purchased / claimed" state.
    /// </summary>
    public void SetPurchased(string labelText = "CLAIMED")
    {
        if (freeText != null) freeText.gameObject.SetActive(false);
        if (purchasedText != null) { purchasedText.gameObject.SetActive(true); purchasedText.text = labelText; }
        if (button != null) button.interactable = false;
    }

    /// <summary>
    /// Update progress bar + text for card slots. If bar references are null, silently skips.
    /// </summary>
    public void SetProgress(float fill01, string text)
    {
        if (barFill != null) barFill.fillAmount = Mathf.Clamp01(fill01);
        if (progressText != null) progressText.text = text;
    }

    /// <summary>
    /// Set the icon sprite. Null-safe.
    /// </summary>
    public void SetIcon(Sprite sprite)
    {
        if (icon != null && sprite != null) icon.sprite = sprite;
    }

    /// <summary>
    /// Set the title label text.
    /// </summary>
    public void SetTitle(string text)
    {
        if (titleText != null) titleText.text = text;
    }
}
