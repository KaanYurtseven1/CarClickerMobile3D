using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ChestShownUI : MonoBehaviour
{
    public static ChestShownUI Instance;

    /// <summary>
    /// Raised when the player taps a chest slot in the ChestShown list. Receives
    /// the inventory index. Subscribers (e.g. <see cref="TutorialManager"/>) can
    /// observe slot taps without intercepting the existing ChestPopup open flow,
    /// which still runs in <see cref="OnSlotTapped"/>.
    /// </summary>
    public static event Action<int> OnSlotTappedEvent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; OnSlotTappedEvent = null; }

    [Header("Slot System")]
    [SerializeField] private Transform slotContainer; // ChestShownPlace with VerticalLayoutGroup
    [SerializeField] private GameObject slotPrefab;   // ChestSlotUI prefab

    [Header("Police Chase Fade")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float chaseFadeOutDuration = 0.35f;
    [SerializeField] private float chaseFadeInDuration = 0.45f;

    private readonly List<ChestSlotUI> _activeSlots = new List<ChestSlotUI>();
    private bool _hiddenByChase;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }
        EnsureCanvasGroup();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.OnInventoryChanged += RefreshSlots;
        PoliceCatchController.OnChaseStarted += HandleChaseStarted;
        PoliceCatchController.OnChaseEnded += HandleChaseEnded;
    }

    private void OnDisable()
    {
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.OnInventoryChanged -= RefreshSlots;
        PoliceCatchController.OnChaseStarted -= HandleChaseStarted;
        PoliceCatchController.OnChaseEnded -= HandleChaseEnded;
    }

    private void Start() => RefreshSlots();

    private void Update()
    {
        // Refresh timer text on unlocking slots
        if (ChestInventoryManager.Instance == null) return;
        var allChests = ChestInventoryManager.Instance.GetAllChests();
        for (int i = 0; i < _activeSlots.Count && i < allChests.Count; i++)
        {
            var cd = allChests[i];
            if (cd.state == ChestState.Unlocking)
                _activeSlots[i].RefreshStatus(cd.state, cd.GetRemainingSeconds());
        }
    }

    public void RefreshSlots()
    {
        if (_hiddenByChase) return;

        // During police chase or radar popup, hide
        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
        {
            SetContainerVisible(false);
            return;
        }
        if (RadarPopupController.Instance != null && RadarPopupController.Instance.IsPopupOpen)
        {
            SetContainerVisible(false);
            return;
        }

        // Clear old slots
        foreach (var slot in _activeSlots)
            if (slot != null) Destroy(slot.gameObject);
        _activeSlots.Clear();

        if (ChestInventoryManager.Instance == null) { SetContainerVisible(false); return; }

        var allChests = ChestInventoryManager.Instance.GetAllChests();
        if (allChests.Count == 0) { SetContainerVisible(false); return; }

        SetContainerVisible(true);
        for (int i = 0; i < allChests.Count; i++)
        {
            var cd = allChests[i];
            if (cd.state == ChestState.OpeningInProgress)
                continue;

            if (slotPrefab == null || slotContainer == null) continue;

            var go = Instantiate(slotPrefab, slotContainer);
            var slot = go.GetComponent<ChestSlotUI>();
            if (slot != null)
            {
                slot.Initialize(i, cd.chestType, cd.state, cd.GetRemainingSeconds(), OnSlotTapped);
                _activeSlots.Add(slot);
            }
        }
    }

    // Legacy bridge
    public void RefreshVisibilityAndCount() => RefreshSlots();

    // Legacy bridge
    public void OnChestShownTapped()
    {
        if (ChestInventoryManager.Instance == null) return;
        if (ChestInventoryManager.Instance.GetUnopenedCount() <= 0) return;
        if (ChestPopupController.Instance != null)
            ChestPopupController.Instance.ShowPopupForChest(0);
    }

    private void OnSlotTapped(int chestIndex)
    {
        Debug.Log($"[ChestShownUI] OnSlotTapped({chestIndex}) called");

        // Notify subscribers (TutorialManager hides Seven on this event) BEFORE
        // opening the popup so the pointer fades in sync with the popup intro.
        try { OnSlotTappedEvent?.Invoke(chestIndex); }
        catch (Exception ex) { Debug.LogException(ex); }

        if (ChestPopupController.Instance == null)
        {
            Debug.LogError("[ChestShownUI] ChestPopupController.Instance is NULL! " +
                "Create a 'ChestPopup' GameObject in the scene with ChestPopupController attached.");
            return;
        }
        ChestPopupController.Instance.ShowPopupForChest(chestIndex);
    }

    private void SetContainerVisible(bool visible)
    {
        if (slotContainer != null) slotContainer.gameObject.SetActive(visible);
        else gameObject.SetActive(visible);
    }

    // ═══════════ POLICE CHASE FADE ═══════════

    private void EnsureCanvasGroup()
    {
        if (canvasGroup != null) return;
        GameObject target = slotContainer != null ? slotContainer.gameObject : gameObject;
        canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = target.AddComponent<CanvasGroup>();
    }

    private void HandleChaseStarted()
    {
        GameObject target = slotContainer != null ? slotContainer.gameObject : gameObject;
        if (!target.activeSelf) return;
        _hiddenByChase = true;
        EnsureCanvasGroup();
        DOTween.Kill(canvasGroup);
        canvasGroup.DOFade(0f, chaseFadeOutDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; });
    }

    private void HandleChaseEnded()
    {
        if (!_hiddenByChase) return;
        _hiddenByChase = false;

        int count = ChestInventoryManager.Instance != null ? ChestInventoryManager.Instance.GetUnopenedCount() : 0;
        if (count <= 0)
        {
            if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }
            return;
        }

        EnsureCanvasGroup();
        DOTween.Kill(canvasGroup);
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.DOFade(1f, chaseFadeInDuration).SetEase(Ease.OutQuad);
        RefreshSlots();
    }
}