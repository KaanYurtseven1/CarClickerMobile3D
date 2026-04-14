using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// In-scene popup for selecting a free kaplama (sticker) as a Blacklist reward.
/// Step 1: Player selects a car from owned/unlocked cars.
/// Step 2: Player selects a sticker slot (0–5) for that car.
///
/// Attach to the KaplamaPickerPopup GameObject in the scene.
/// Must be under the main Canvas so it renders on top.
///
/// DESIGNER: Assign car buttons and sticker buttons in Inspector,
/// or use the procedural CarButtonPrefab/StickerButtonPrefab approach.
/// </summary>
public class KaplamaPickerController : MonoBehaviour
{
    public static KaplamaPickerController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Popup Root")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private CanvasGroup popupCanvasGroup;

    [Header("Step 1 — Car Selection")]
    [SerializeField] private GameObject carSelectionPanel;
    [SerializeField] private Transform carButtonContainer;
    [SerializeField] private GameObject carButtonPrefab;
    [SerializeField] private TMP_Text carStepTitle;

    [Header("Step 2 — Sticker Selection")]
    [SerializeField] private GameObject stickerSelectionPanel;
    [SerializeField] private Transform stickerButtonContainer;
    [SerializeField] private GameObject stickerButtonPrefab;
    [SerializeField] private TMP_Text stickerStepTitle;
    [SerializeField] private Button backButton;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.2f;

    private const int StickerCount = 6;
    private string _selectedCarId;
    private bool _isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (backButton != null)
            backButton.onClick.AddListener(GoBackToCarSelection);

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Public API ───

    public void Show()
    {
        var claimData = BlacklistRewardClaimData.LoadFromPrefs();
        if (!claimData.HasPendingKaplama)
        {
            Debug.LogWarning("[KaplamaPicker] No pending kaplama reward.");
            return;
        }

        _isOpen = true;
        _selectedCarId = null;

        if (popupRoot != null)
            popupRoot.SetActive(true);

        ShowCarSelection();
        PlayFadeIn();
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;

        PlayFadeOut(() =>
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);
        });
    }

    // ─── Step 1: Car selection ───

    private void ShowCarSelection()
    {
        if (carSelectionPanel != null) carSelectionPanel.SetActive(true);
        if (stickerSelectionPanel != null) stickerSelectionPanel.SetActive(false);
        if (carStepTitle != null) carStepTitle.text = "SELECT A CAR";

        ClearChildren(carButtonContainer);

        string[] carIds = BlacklistStatTracker.GetAllCarIds();
        if (carIds == null) return;

        // Exclude the last car (same logic as endgame cosmetics)
        int count = carIds.Length > 1 ? carIds.Length - 1 : carIds.Length;

        for (int i = 0; i < count; i++)
        {
            string carId = carIds[i];

            // Future-compatible: check if car is owned/unlocked
            if (!IsCarAvailable(carId)) continue;

            if (carButtonPrefab != null && carButtonContainer != null)
            {
                var go = Instantiate(carButtonPrefab, carButtonContainer);
                go.SetActive(true);

                var label = go.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = carId;

                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    string captured = carId;
                    btn.onClick.AddListener(() => OnCarSelected(captured));
                }
            }
        }
    }

    private void OnCarSelected(string carId)
    {
        _selectedCarId = carId;
        ShowStickerSelection(carId);
    }

    // ─── Step 2: Sticker selection ───

    private void ShowStickerSelection(string carId)
    {
        if (carSelectionPanel != null) carSelectionPanel.SetActive(false);
        if (stickerSelectionPanel != null) stickerSelectionPanel.SetActive(true);
        if (stickerStepTitle != null) stickerStepTitle.text = $"SELECT STICKER — {carId}";

        ClearChildren(stickerButtonContainer);

        for (int s = 0; s < StickerCount; s++)
        {
            bool alreadyOwned = GarageSaveData.Instance != null &&
                                GarageSaveData.Instance.IsStickerOwned(carId, s);

            if (stickerButtonPrefab != null && stickerButtonContainer != null)
            {
                var go = Instantiate(stickerButtonPrefab, stickerButtonContainer);
                go.SetActive(true);

                var label = go.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = alreadyOwned ? $"Sticker {s + 1} ✓" : $"Sticker {s + 1}";

                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    if (alreadyOwned)
                    {
                        btn.interactable = false;
                    }
                    else
                    {
                        int captured = s;
                        btn.onClick.AddListener(() => OnStickerSelected(carId, captured));
                    }
                }
            }
        }
    }

    private void OnStickerSelected(string carId, int stickerIndex)
    {
        // Unlock the sticker
        if (GarageSaveData.Instance != null)
        {
            GarageSaveData.Instance.MarkStickerOwned(carId, stickerIndex);
            GarageSaveData.Instance.SaveToPrefs();
            Debug.Log($"[KaplamaPicker] Unlocked sticker {stickerIndex} for car '{carId}'.");
        }

        // Consume one pending kaplama
        var claimData = BlacklistRewardClaimData.LoadFromPrefs();
        claimData.pendingFreeKaplamaCount = Mathf.Max(0, claimData.pendingFreeKaplamaCount - 1);
        claimData.SaveToPrefs();

        // If more kaplamas remain, restart from car selection
        if (claimData.HasPendingKaplama)
        {
            ShowCarSelection();
        }
        else
        {
            Close();
        }
    }

    // ─── Navigation ───

    private void GoBackToCarSelection()
    {
        _selectedCarId = null;
        ShowCarSelection();
    }

    // ─── Car availability ───

    /// <summary>
    /// Returns true if the car is available for kaplama selection.
    /// Checks actual car ownership via GarageSaveData.
    /// </summary>
    private bool IsCarAvailable(string carId)
    {
        if (GarageSaveData.Instance == null) return true;
        return GarageSaveData.Instance.IsCarUnlocked(carId);
    }

    // ─── Helpers ───

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void PlayFadeIn()
    {
        if (popupCanvasGroup == null) return;
        popupCanvasGroup.alpha = 0f;
        popupCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
    }

    private void PlayFadeOut(Action onComplete)
    {
        if (popupCanvasGroup == null) { onComplete?.Invoke(); return; }
        popupCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true)
            .OnComplete(() => onComplete?.Invoke());
    }
}
