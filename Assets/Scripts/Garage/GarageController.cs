// ════════════════════════════════════════════════════════════════
// GarageController.cs – Main orchestrator for the Garage scene.
//
// Responsibilities:
//   • Active-car switching (GoLeft / GoRight)
//   • Per-car state persistence (color, sticker, enabled parts)
//   • Skin application via CarCustomizer
//   • Coordinating UI sub-controllers
//
// Inspector wiring:
//   database          → GarageDatabaseSO asset
//   carsParent        → CarPlatform/Car
//   carNameTMP        → Canvas/Car_Name
//   modelNameTMP      → Canvas/Model_Name
//   goLeftButton      → Canvas/GoLeft_Button
//   goRightButton     → Canvas/GoRight_Button
//   stickerUI         → StickerUIController component
//   colorUI           → ColorUIController component
//   partsUI           → PartsUIController component
//   barsUI            → BarsUIController component
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class GarageController : MonoBehaviour
{
    // ─────────────────── Serialized ───────────────────
    [Header("─── Database ───")]
    [SerializeField] private GarageDatabaseSO database;

    [Header("─── Scene References ───")]
    [SerializeField] private Transform carsParent;
    [SerializeField] private TMP_Text carNameTMP;
    [SerializeField] private TMP_Text modelNameTMP;
    [SerializeField] private Button goLeftButton;
    [SerializeField] private Button goRightButton;

    [Header("─── Sub-Controllers ───")]
    [SerializeField] private StickerUIController stickerUI;
    [SerializeField] private ColorUIController colorUI;
    [SerializeField] private PartsUIController partsUI;
    [SerializeField] private BarsUIController barsUI;

    [Header("─── Lock / Unlock Visuals ───")]
    [Tooltip("Full-screen overlay shown when the active car is locked. " +
             "Must have a CanvasGroup — this script sets blocksRaycasts = false " +
             "so GoLeft / GoRight buttons underneath remain tappable.")]
    [SerializeField] private GameObject lockedOverlay;
    [Tooltip("TMP label inside LockedUI displaying the blacklist tier (e.g. 'BLACKLIST #4').")]
    [SerializeField] private TMP_Text lockedBlacklistText;

    /// <summary>Cached CanvasGroup on lockedOverlay — ensures raycasts pass through.</summary>
    private CanvasGroup _lockedOverlayCG;

    [Header("─── Customisation Slide Panels (for locked-shake) ───")]
    [Tooltip("RectTransform of the color slide panel (shaken when car is locked).")]
    [SerializeField] private RectTransform slideColorsRect;
    [Tooltip("RectTransform of the sticker slide panel.")]
    [SerializeField] private RectTransform slideStickersRect;
    [Tooltip("RectTransform of the parts slide panel.")]
    [SerializeField] private RectTransform slidePartsRect;

    [Header("─── Shop / Economy ───")]
    [SerializeField] private GarageShopConfig shopConfig;

    // ─── Glitch Transition Settings (inspector-tunable) ───
    [Header("─── Glitch Transition ───")]
    [SerializeField] private float glitchOutDur = 0.22f;
    [SerializeField] private float glitchInDur = 0.18f;
    [SerializeField] private float jitterX = 0.06f;
    [SerializeField] private float jitterRot = 3f;
    [SerializeField] private float punch = 0.06f;
    [SerializeField] private Ease glitchEase = Ease.OutQuad;

    // ─────────────────── Internal State ───────────────────
    private int _currentCarIndex;
    private bool _isTransitioning;
    private Transform[] _carRoots;
    private CarCustomizer[] _customizers;
    private CarState[] _states;

    private int CarCount => (database != null && database.cars != null) ? database.cars.Count : 0;
    private CarDataSO CurrentCarData => database.cars[_currentCarIndex];
    private CarCustomizer CurrentCustomizer => _customizers[_currentCarIndex];
    private CarState CurrentState => _states[_currentCarIndex];

    private const string LockChainChildName = "CarLockChane";

    /// <summary>True when the currently selected car is locked (not yet unlocked via Blacklist).</summary>
    private bool IsCurrentCarLocked
    {
        get
        {
            if (GarageSaveData.Instance == null) return false;
            CarDataSO data = CurrentCarData;
            return data != null && !GarageSaveData.Instance.IsCarUnlocked(data.carId);
        }
    }

    // Per-car selection state (lives in memory; extend to PlayerPrefs later)
    private class CarState
    {
        public int colorIndex;
        public int stickerIndex;
        public HashSet<string> enabledParts = new HashSet<string>();
    }

    // ══════════════════ Lifecycle ══════════════════

    private void Start()
    {
        if (database == null || CarCount == 0)
        {
            Debug.LogError("[GarageController] Database is null or has no cars.  Disabling.");
            enabled = false;
            return;
        }

        CacheCarRoots();
        BindButtons();
        BindSubControllers();
        EnsureLockedOverlayPassthrough();

        int startIndex = 0;
        if (GarageSaveData.Instance != null)
            startIndex = Mathf.Clamp(GarageSaveData.Instance.SelectedCarIndex, 0, CarCount - 1);

        SelectCar(startIndex);
    }

    // ══════════════════ Initialization ══════════════════

    private void CacheCarRoots()
    {
        int count = CarCount;
        _carRoots = new Transform[count];
        _customizers = new CarCustomizer[count];
        _states = new CarState[count];

        for (int i = 0; i < count; i++)
        {
            CarDataSO data = database.cars[i];
            if (data == null)
            {
                Debug.LogError($"[GarageController] database.cars[{i}] is null.");
                _states[i] = new CarState();
                continue;
            }

            // Find the car root Transform by name
            Transform root = carsParent.Find(data.CarRootName);
            if (root == null)
            {
                Debug.LogError($"[GarageController] Car root '{data.CarRootName}' not found under '{carsParent.name}'.");
                _states[i] = new CarState();
                continue;
            }

            _carRoots[i] = root;

            // Require a CarCustomizer component on the root
            CarCustomizer cust = root.GetComponent<CarCustomizer>();
            if (cust == null)
            {
                Debug.LogError($"[GarageController] CarCustomizer component missing on '{data.CarRootName}'.  " +
                               "Add the component and assign bodyRenderers in the Inspector.");
                _states[i] = new CarState();
                continue;
            }
            cust.Initialize(database.globalPartKeys);
            _customizers[i] = cust;

            // Restore saved state (or create blank)
            _states[i] = new CarState();
            if (GarageSaveData.Instance != null)
            {
                var saved = GarageSaveData.Instance.GetStateForCar(data.carId);
                _states[i].colorIndex = saved.colorIndex;
                _states[i].stickerIndex = saved.stickerIndex;
                _states[i].enabledParts = new HashSet<string>(saved.enabledParts);
            }

            // All cars start inactive
            root.gameObject.SetActive(false);
        }
    }

    private void BindButtons()
    {
        if (goLeftButton != null) goLeftButton.onClick.AddListener(GoLeft);
        if (goRightButton != null) goRightButton.onClick.AddListener(GoRight);
    }

    private void BindSubControllers()
    {
        if (stickerUI != null) stickerUI.onStickerSelected = SetSticker;
        if (colorUI != null) colorUI.onColorSelected = SetColor;
        if (partsUI != null) partsUI.onPartToggled = TogglePart;
    }

    /// <summary>
    /// Ensures LockedUI has a CanvasGroup with blocksRaycasts = false so
    /// the overlay never intercepts taps meant for GoLeft / GoRight buttons.
    /// Customisation is already gated at the code level (BlockIfLocked).
    /// </summary>
    private void EnsureLockedOverlayPassthrough()
    {
        if (lockedOverlay == null) return;
        _lockedOverlayCG = lockedOverlay.GetComponent<CanvasGroup>();
        if (_lockedOverlayCG == null)
            _lockedOverlayCG = lockedOverlay.AddComponent<CanvasGroup>();
        _lockedOverlayCG.blocksRaycasts = false;
        _lockedOverlayCG.interactable = false;
    }

    // ══════════════════ Car Switching ══════════════════

    private void SelectCar(int index)
    {
        if (index < 0 || index >= CarCount) return;

        // Deactivate previous
        if (_carRoots[_currentCarIndex] != null)
            _carRoots[_currentCarIndex].gameObject.SetActive(false);

        _currentCarIndex = index;

        // Only persist as the "equipped" car if it is actually unlocked.
        // Browsing a locked car is visual-only and must NOT change what
        // MainScene uses as the active gameplay car.
        if (GarageSaveData.Instance != null)
        {
            CarDataSO data = CurrentCarData;
            if (data != null && GarageSaveData.Instance.IsCarUnlocked(data.carId))
                GarageSaveData.Instance.SelectedCarIndex = _currentCarIndex;
        }

        // Activate new
        if (_carRoots[_currentCarIndex] != null)
            _carRoots[_currentCarIndex].gameObject.SetActive(true);

        ApplyCarState();
        RefreshAllUI();
    }

    private void GoLeft()
    {
        if (_isTransitioning) return;
        if (_currentCarIndex > 0)
        {
            // G1: Garage car switch SFX
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlayGarageCarSwitch();

            PlayGlitchTransition(_currentCarIndex - 1);
        }
    }

    private void GoRight()
    {
        if (_isTransitioning) return;
        if (_currentCarIndex < CarCount - 1)
        {
            // G1: Garage car switch SFX
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlayGarageCarSwitch();

            PlayGlitchTransition(_currentCarIndex + 1);
        }
    }

    // ══════════════════ Glitch Transition (visual-only) ══════════════════

    /// <summary>
    /// Plays a short DOTween "glitch" animation on the current car root,
    /// then calls SelectCar(targetIndex), then plays a "glitch-in" on the
    /// new car root.  No gameplay logic is altered.
    /// </summary>
    private void PlayGlitchTransition(int targetIndex)
    {
        Transform currentRoot = _carRoots[_currentCarIndex];
        if (currentRoot == null)
        {
            // Fallback: no root to animate, just switch immediately
            SelectCar(targetIndex);
            return;
        }

        _isTransitioning = true;

        // Disable nav buttons while transitioning
        if (goLeftButton != null) goLeftButton.interactable = false;
        if (goRightButton != null) goRightButton.interactable = false;

        // Cache originals for the outgoing car
        Vector3 origPos = currentRoot.localPosition;
        Quaternion origRot = currentRoot.localRotation;
        Vector3 origScale = currentRoot.localScale;

        // Kill any leftover tweens on this transform
        DOTween.Kill(currentRoot);

        // ── GlitchOut sequence ──
        float stepOut = glitchOutDur / 5f;
        Sequence outSeq = DOTween.Sequence().SetTarget(currentRoot).SetUpdate(true);

        // Position X jitter
        outSeq.Append(currentRoot.DOLocalMoveX(origPos.x + jitterX, stepOut).SetEase(glitchEase));
        outSeq.Append(currentRoot.DOLocalMoveX(origPos.x - jitterX * 0.8f, stepOut).SetEase(glitchEase));
        outSeq.Append(currentRoot.DOLocalMoveX(origPos.x + jitterX * 0.6f, stepOut).SetEase(glitchEase));
        outSeq.Append(currentRoot.DOLocalMoveX(origPos.x - jitterX * 0.4f, stepOut).SetEase(glitchEase));
        outSeq.Append(currentRoot.DOLocalMoveX(origPos.x, stepOut).SetEase(glitchEase));

        // Rotation Z jitter (runs in parallel with position)
        outSeq.Insert(0f, currentRoot.DOLocalRotate(new Vector3(0, 0, jitterRot), stepOut, RotateMode.Fast).SetEase(glitchEase));
        outSeq.Insert(stepOut, currentRoot.DOLocalRotate(new Vector3(0, 0, -jitterRot * 0.7f), stepOut, RotateMode.Fast).SetEase(glitchEase));
        outSeq.Insert(stepOut * 2, currentRoot.DOLocalRotate(new Vector3(0, 0, jitterRot * 0.5f), stepOut, RotateMode.Fast).SetEase(glitchEase));
        outSeq.Insert(stepOut * 3, currentRoot.DOLocalRotate(origRot.eulerAngles, stepOut * 2, RotateMode.Fast).SetEase(glitchEase));

        // Scale punch (runs in parallel)
        outSeq.Insert(0f, currentRoot.DOScale(origScale * (1f + punch), glitchOutDur * 0.35f).SetEase(glitchEase));
        outSeq.Insert(glitchOutDur * 0.35f, currentRoot.DOScale(origScale * (1f - punch), glitchOutDur * 0.3f).SetEase(glitchEase));
        outSeq.Insert(glitchOutDur * 0.65f, currentRoot.DOScale(origScale, glitchOutDur * 0.35f).SetEase(glitchEase));

        outSeq.OnComplete(() =>
        {
            // Guarantee exact restoration before deactivation
            currentRoot.localPosition = origPos;
            currentRoot.localRotation = origRot;
            currentRoot.localScale = origScale;

            // ── Actual car switch (the only call) ──
            SelectCar(targetIndex);

            // ── GlitchIn on the new car ──
            Transform nextRoot = _carRoots[_currentCarIndex];
            if (nextRoot != null)
            {
                PlayGlitchIn(nextRoot);
            }
            else
            {
                FinishTransition();
            }
        });
    }

    /// <summary>
    /// Short "glitch-in" pop animation on the newly activated car root.
    /// Restores exact transform values at the end.
    /// </summary>
    private void PlayGlitchIn(Transform root)
    {
        Vector3 origPos = root.localPosition;
        Quaternion origRot = root.localRotation;
        Vector3 origScale = root.localScale;

        DOTween.Kill(root);

        float stepIn = glitchInDur / 4f;
        float halfPunch = punch * 0.5f;

        // Start slightly small
        root.localScale = origScale * (1f - halfPunch);

        Sequence inSeq = DOTween.Sequence().SetTarget(root).SetUpdate(true);

        // Scale pop
        inSeq.Append(root.DOScale(origScale * (1f + halfPunch), stepIn * 2f).SetEase(Ease.OutBack));
        inSeq.Append(root.DOScale(origScale, stepIn * 2f).SetEase(glitchEase));

        // Light X jitter
        float halfJitterX = jitterX * 0.4f;
        inSeq.Insert(0f, root.DOLocalMoveX(origPos.x + halfJitterX, stepIn).SetEase(glitchEase));
        inSeq.Insert(stepIn, root.DOLocalMoveX(origPos.x - halfJitterX * 0.6f, stepIn).SetEase(glitchEase));
        inSeq.Insert(stepIn * 2, root.DOLocalMoveX(origPos.x, stepIn * 2f).SetEase(glitchEase));

        // Light rotation jitter
        float halfJitterRot = jitterRot * 0.4f;
        inSeq.Insert(0f, root.DOLocalRotate(new Vector3(0, 0, halfJitterRot), stepIn, RotateMode.Fast).SetEase(glitchEase));
        inSeq.Insert(stepIn, root.DOLocalRotate(origRot.eulerAngles, stepIn * 2f, RotateMode.Fast).SetEase(glitchEase));

        inSeq.OnComplete(() =>
        {
            // Guarantee exact restoration
            root.localPosition = origPos;
            root.localRotation = origRot;
            root.localScale = origScale;

            FinishTransition();
        });
    }

    /// <summary>Clears transition lock and re-applies correct button interactable states.</summary>
    private void FinishTransition()
    {
        _isTransitioning = false;

        // Re-apply proper interactable state (mirrors RefreshAllUI logic)
        if (goLeftButton != null) goLeftButton.interactable = _currentCarIndex > 0;
        if (goRightButton != null) goRightButton.interactable = _currentCarIndex < CarCount - 1;
    }

    // ══════════════════ State Application ══════════════════

    private void ApplyCarState()
    {
        CarDataSO data = CurrentCarData;
        CarState state = CurrentState;
        CarCustomizer cust = CurrentCustomizer;
        if (data == null || cust == null) return;

        // Skin
        Material mat = data.GetMaterial(state.colorIndex, state.stickerIndex);
        cust.ApplySkin(mat);

        // Parts
        cust.RestoreParts(state.enabledParts);
    }

    private void RefreshAllUI()
    {
        CarDataSO data = CurrentCarData;
        CarState state = CurrentState;
        if (data == null) return;

        bool locked = IsCurrentCarLocked;

        // TMP texts
        if (carNameTMP != null) carNameTMP.text = data.displayCarName;
        if (modelNameTMP != null) modelNameTMP.text = data.displayModelName;

        // Navigation buttons (browsing allowed even when locked)
        if (goLeftButton != null) goLeftButton.interactable = _currentCarIndex > 0;
        if (goRightButton != null) goRightButton.interactable = _currentCarIndex < CarCount - 1;

        // ── Lock overlay ──
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
        if (lockedBlacklistText != null && locked)
        {
            int tier = BlacklistManager.Instance != null
                ? BlacklistManager.Instance.GetTierIndexForCar(data.carId)
                : -1;
            lockedBlacklistText.text = tier > 0
                ? $"BLACKLIST #{tier}"
                : "LOCKED";
        }

        // ── CarLockChane child on the 3D model ──
        RefreshLockChain(locked);

        // Sub-controllers
        if (colorUI != null) colorUI.Refresh(data, state.colorIndex);
        if (stickerUI != null) stickerUI.Refresh(data, state.stickerIndex);
        if (partsUI != null) partsUI.Refresh(data, state.enabledParts, database.globalPartKeys);

        // Bars
        RefreshBars();

        // Affordability visuals
        RefreshAffordability();
    }

    /// <summary>Activates or deactivates the CarLockChane child on the current car root.</summary>
    private void RefreshLockChain(bool locked)
    {
        Transform root = _carRoots[_currentCarIndex];
        if (root == null) return;
        Transform chain = root.Find(LockChainChildName);
        if (chain != null) chain.gameObject.SetActive(locked);
    }

    // ══════════════════ Affordability Refresh ══════════════════

    /// <summary>
    /// Keeps all main-UI item buttons at full opacity (no dimming).
    /// Affordability is now communicated inside BuyPopupPanel (Btn_Yes disabled).
    /// </summary>
    private void RefreshAffordability()
    {
        if (shopConfig == null || GarageSaveData.Instance == null) return;

        string carId = CurrentCarData != null ? CurrentCarData.carId : null;
        if (carId == null) return;

        // Colors – always bright
        if (colorUI != null)
        {
            for (int i = 0; i < 6; i++)
            {
                bool owned = GarageSaveData.Instance.IsColorOwned(carId, i);
                colorUI.SetButtonAffordable(i, true, owned);
            }
        }

        // Stickers – always bright
        if (stickerUI != null)
        {
            for (int i = 0; i < 6; i++)
            {
                bool owned = GarageSaveData.Instance.IsStickerOwned(carId, i);
                stickerUI.SetSlotAffordable(i, true, owned);
            }
        }

        // Parts – always bright
        if (partsUI != null && database.globalPartKeys != null)
        {
            for (int i = 0; i < database.globalPartKeys.Count; i++)
            {
                string key = database.globalPartKeys[i];
                bool owned = GarageSaveData.Instance.IsPartOwned(carId, key);
                partsUI.SetPartAffordable(i, true, owned);
            }
        }
    }

    // ══════════════════ Public API (called by sub-controllers) ══════════════════

    /// <summary>Shake the relevant panel and block interaction when the car is locked.</summary>
    private bool BlockIfLocked(RectTransform shakeTarget)
    {
        if (!IsCurrentCarLocked) return false;
        GarageAffordabilityHelper.ShakeButton(shakeTarget);
        return true;
    }

    /// <summary>Changes the active car's color. If not owned, opens BuyPopup for confirmation.</summary>
    public void SetColor(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= 6) return;
        if (BlockIfLocked(slideColorsRect)) return;

        string carId = CurrentCarData.carId;

        // ── Purchase gate (routes through confirmation popup) ──
        if (!GarageSaveData.Instance.IsColorOwned(carId, colorIndex))
        {
            int cost = shopConfig != null ? shopConfig.GetColorCost(colorIndex) : 0;
            if (cost > 0)
            {
                // Immediate click animation feedback (scales up the clicked button)
                if (colorUI != null)
                    colorUI.Refresh(CurrentCarData, colorIndex);

                // Open popup regardless of affordability; popup handles Btn_Yes state
                if (GarageBuyPopupController.Instance != null)
                {
                    GarageBuyPopupController.Instance.ShowForColor(colorIndex, cost);
                    return;
                }
            }
            // Free item (cost == 0) or no popup available → mark owned
            GarageSaveData.Instance.MarkColorOwned(carId, colorIndex);
        }

        CarState state = CurrentState;
        state.colorIndex = colorIndex;

        // Apply new skin
        Material mat = CurrentCarData.GetMaterial(state.colorIndex, state.stickerIndex);
        CurrentCustomizer?.ApplySkin(mat);

        // Persist
        PersistCurrentState();

        // Sticker previews are colour-independent; refresh to update highlight
        if (stickerUI != null)
            stickerUI.Refresh(CurrentCarData, state.stickerIndex);

        // Update color selection visual
        if (colorUI != null)
            colorUI.Refresh(CurrentCarData, state.colorIndex);

        // Refresh affordability after purchase
        RefreshAffordability();
    }

    /// <summary>Changes the active car's sticker. If not owned, opens BuyPopup for confirmation.</summary>
    public void SetSticker(int stickerIndex)
    {
        if (stickerIndex < 0 || stickerIndex >= 6) return;
        if (BlockIfLocked(slideStickersRect)) return;

        string carId = CurrentCarData.carId;

        // ── Purchase gate (routes through confirmation popup) ──
        if (!GarageSaveData.Instance.IsStickerOwned(carId, stickerIndex))
        {
            int cost = shopConfig != null ? shopConfig.GetStickerCost(stickerIndex) : 0;
            if (cost > 0)
            {
                // Immediate click animation feedback (moves highlight + emphasis)
                if (stickerUI != null)
                    stickerUI.SetHighlight(stickerIndex);

                if (GarageBuyPopupController.Instance != null)
                {
                    GarageBuyPopupController.Instance.ShowForSticker(stickerIndex, cost);
                    return;
                }
            }
            GarageSaveData.Instance.MarkStickerOwned(carId, stickerIndex);
        }

        CarState state = CurrentState;
        state.stickerIndex = stickerIndex;

        // Apply new skin
        Material mat = CurrentCarData.GetMaterial(state.colorIndex, state.stickerIndex);
        CurrentCustomizer?.ApplySkin(mat);

        // Persist
        PersistCurrentState();

        // Move highlight
        if (stickerUI != null)
            stickerUI.SetHighlight(stickerIndex);

        RefreshAffordability();
    }

    /// <summary>Toggles a mod part on/off. If not owned, attempts purchase with Gold first.
    /// Group-exclusive: only one part per group (Camurluk, Egzoz, Kaput, Spoiler)
    /// can be equipped at a time.</summary>
    public void TogglePart(string partKey)
    {
        if (string.IsNullOrEmpty(partKey)) return;
        if (BlockIfLocked(slidePartsRect)) return;

        string carId = CurrentCarData.carId;
        CarState state = CurrentState;

        // ── Unequip (always allowed, no cost) ──
        if (state.enabledParts.Contains(partKey))
        {
            state.enabledParts.Remove(partKey);
            CurrentCustomizer?.SetPartActive(partKey, false);
            if (partsUI != null) partsUI.UpdatePartHighlight(partKey, false);
            PersistCurrentState();
            RefreshBars(animate: false);
            RefreshAffordability();
            return;
        }

        // ── Purchase gate (routes through confirmation popup) ──
        if (!GarageSaveData.Instance.IsPartOwned(carId, partKey))
        {
            double cost = shopConfig != null
                ? shopConfig.GetPartCostByKey(partKey, database.globalPartKeys)
                : 0;
            if (cost > 0)
            {
                if (GarageBuyPopupController.Instance != null)
                {
                    GarageBuyPopupController.Instance.ShowForPart(partKey, cost);
                    return;
                }
            }
            GarageSaveData.Instance.MarkPartOwned(carId, partKey);
        }

        // ── Group-exclusive: remove any existing part in the same group ──
        string group = PartStatData.GetGroupPrefix(partKey);
        if (group != null)
        {
            string existing = null;
            foreach (string key in state.enabledParts)
            {
                if (key.StartsWith(group)) { existing = key; break; }
            }
            if (existing != null)
            {
                state.enabledParts.Remove(existing);
                CurrentCustomizer?.SetPartActive(existing, false);
                if (partsUI != null) partsUI.UpdatePartHighlight(existing, false);
            }
        }

        // ── Equip new part ──
        state.enabledParts.Add(partKey);
        CurrentCustomizer?.SetPartActive(partKey, true);
        if (partsUI != null) partsUI.UpdatePartHighlight(partKey, true);

        // Persist
        PersistCurrentState();

        RefreshBars(animate: false);
        RefreshAffordability();
    }

    // ══════════════════ Popup Finalize Purchases ══════════════════

    /// <summary>Finalises a color purchase: spends currency, marks owned, then applies via SetColor.</summary>
    public void FinalizeColorPurchase(int colorIndex)
    {
        string carId = CurrentCarData.carId;
        int cost = shopConfig != null ? shopConfig.GetColorCost(colorIndex) : 0;
        if (cost > 0 && CurrencyManager.Instance != null)
        {
            if (!CurrencyManager.Instance.TrySpendNitroCoins(cost)) return;
            if (GarageCurrencyUI.Instance != null) GarageCurrencyUI.Instance.RefreshNitro();
        }
        GarageSaveData.Instance.MarkColorOwned(carId, colorIndex);
        SetColor(colorIndex);

        // G2: Garage color purchase SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayGaragePurchase();
    }

    /// <summary>Finalises a sticker purchase: spends currency, marks owned, then applies via SetSticker.</summary>
    public void FinalizeStickerPurchase(int stickerIndex)
    {
        string carId = CurrentCarData.carId;
        int cost = shopConfig != null ? shopConfig.GetStickerCost(stickerIndex) : 0;
        if (cost > 0 && CurrencyManager.Instance != null)
        {
            if (!CurrencyManager.Instance.TrySpendNitroCoins(cost)) return;
            if (GarageCurrencyUI.Instance != null) GarageCurrencyUI.Instance.RefreshNitro();
        }
        GarageSaveData.Instance.MarkStickerOwned(carId, stickerIndex);
        SetSticker(stickerIndex);

        // G3: Garage sticker purchase SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayGaragePurchase();
    }

    /// <summary>Finalises a part purchase: spends currency, marks owned, then equips via TogglePart.</summary>
    public void FinalizePartPurchase(string partKey)
    {
        string carId = CurrentCarData.carId;
        double cost = shopConfig != null
            ? shopConfig.GetPartCostByKey(partKey, database.globalPartKeys)
            : 0;
        if (cost > 0 && CurrencyManager.Instance != null)
        {
            if (!CurrencyManager.Instance.TrySpendMoney(cost)) return;
            if (GarageCurrencyUI.Instance != null) GarageCurrencyUI.Instance.RefreshGold();
        }
        GarageSaveData.Instance.MarkPartOwned(carId, partKey);
        TogglePart(partKey);

        // G4: Garage part purchase SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayGaragePurchase();
    }

    // ══════════════════ Preview API (for BuyPopup) ══════════════════

    /// <summary>Temporarily shows a color on the car without persisting or changing state.</summary>
    public void PreviewColor(int colorIndex)
    {
        Material mat = CurrentCarData.GetMaterial(colorIndex, CurrentState.stickerIndex);
        CurrentCustomizer?.ApplySkin(mat);
    }

    /// <summary>Temporarily shows a sticker on the car without persisting or changing state.</summary>
    public void PreviewSticker(int stickerIndex)
    {
        Material mat = CurrentCarData.GetMaterial(CurrentState.colorIndex, stickerIndex);
        CurrentCustomizer?.ApplySkin(mat);
    }

    /// <summary>Temporarily shows a part as equipped (group-exclusive hiding) without changing state.</summary>
    public void PreviewPart(string partKey)
    {
        string group = PartStatData.GetGroupPrefix(partKey);
        if (group != null)
        {
            foreach (string key in CurrentState.enabledParts)
            {
                if (key.StartsWith(group))
                {
                    CurrentCustomizer?.SetPartActive(key, false);
                    break;
                }
            }
        }
        CurrentCustomizer?.SetPartActive(partKey, true);
    }

    /// <summary>Reverts any temporary preview by re-applying the real saved car state.</summary>
    public void RevertPreview()
    {
        ApplyCarState();
    }

    /// <summary>
    /// Reverts the selection UI (color scale + sticker highlight) back to
    /// the real committed state.  Called by BuyPopupController when
    /// a purchase is cancelled or the popup is closed without buying.
    /// </summary>
    public void RevertSelectionUI()
    {
        CarState state = CurrentState;
        if (state == null) return;

        if (colorUI != null)
            colorUI.Refresh(CurrentCarData, state.colorIndex);
        if (stickerUI != null)
            stickerUI.SetHighlight(state.stickerIndex);
    }

    public void AnimateColorSelection(int colorIndex)
    {
        if (colorUI != null)
            colorUI.Refresh(CurrentCarData, colorIndex);
    }

    public void AnimateStickerSelection(int stickerIndex)
    {
        if (stickerUI != null)
            stickerUI.SetHighlight(stickerIndex);
    }

    // ══════════════════ Persistence Helper ══════════════════

    private void PersistCurrentState()
    {
        if (GarageSaveData.Instance == null) return;
        CarDataSO data = CurrentCarData;
        CarState state = CurrentState;
        if (data == null || state == null) return;
        GarageSaveData.Instance.SetStateForCar(data.carId, state.colorIndex, state.stickerIndex, state.enabledParts);
    }

    // ══════════════════ Stats / Bars ══════════════════

    /// <summary>Recomputes stats from base + equipped parts and updates the bars UI.
    /// When <paramref name="animate"/> is false the bars snap instantly (used by TogglePart).</summary>
    private void RefreshBars(bool animate = true)
    {
        if (barsUI == null) return;

        ComputeStats(out int dur, out int acc, out int spd);
        barsUI.Refresh(dur, acc, spd, animate);
    }

    /// <summary>
    /// Calculates clamped (0-15) stat values for the current car using
    /// <see cref="CarDataSO"/> base stats plus bonuses from equipped parts.
    /// </summary>
    private void ComputeStats(out int durability, out int acceleration, out int speed)
    {
        CarDataSO data = CurrentCarData;
        durability = data != null ? data.baseDurability : 0;
        acceleration = data != null ? data.baseAcceleration : 0;
        speed = data != null ? data.baseSpeed : 0;

        CarState state = CurrentState;
        if (state == null) return;

        foreach (string key in state.enabledParts)
        {
            if (PartStatData.Bonuses.TryGetValue(key, out PartStatBonus bonus))
            {
                durability += bonus.durability;
                acceleration += bonus.acceleration;
                speed += bonus.speed;
            }
        }

        durability = Mathf.Clamp(durability, 0, 15);
        acceleration = Mathf.Clamp(acceleration, 0, 15);
        speed = Mathf.Clamp(speed, 0, 15);
    }
}
