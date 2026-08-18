// ════════════════════════════════════════════════════════════════
// GarageExitPopupController.cs – Exit-confirmation popup in the
// Garage scene.  Saves data and returns to Main on confirm.
//
// Inspector wiring:
//   popupPanel     → The popup root panel (starts inactive)
//   confirmButton  → "Evet" button
//   cancelButton   → "Hayır" button
//   openButton     → The top-bar button that triggers the popup
//                     (MainScene_Button in NewGarage)
// ════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GarageExitPopupController : MonoBehaviour
{
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button openButton;

    private void Start()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
        if (openButton != null) openButton.onClick.AddListener(ShowPopup);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
    }

    private void ShowPopup()
    {
        if (popupPanel != null) popupPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        // Persist garage state before leaving
        if (GarageSaveData.Instance != null)
            GarageSaveData.Instance.SaveToPrefs();

        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();

        SceneManager.LoadScene("Main");
    }

    private void OnCancel()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
    }
}
