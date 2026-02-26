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
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    // ─────────────────── Internal State ───────────────────
    private int _currentCarIndex;
    private Transform[] _carRoots;
    private CarCustomizer[] _customizers;
    private CarState[] _states;

    private int CarCount => (database != null && database.cars != null) ? database.cars.Count : 0;
    private CarDataSO CurrentCarData => database.cars[_currentCarIndex];
    private CarCustomizer CurrentCustomizer => _customizers[_currentCarIndex];
    private CarState CurrentState => _states[_currentCarIndex];

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
        SelectCar(0);
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

            _states[i] = new CarState();

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

    // ══════════════════ Car Switching ══════════════════

    private void SelectCar(int index)
    {
        if (index < 0 || index >= CarCount) return;

        // Deactivate previous
        if (_carRoots[_currentCarIndex] != null)
            _carRoots[_currentCarIndex].gameObject.SetActive(false);

        _currentCarIndex = index;

        // Activate new
        if (_carRoots[_currentCarIndex] != null)
            _carRoots[_currentCarIndex].gameObject.SetActive(true);

        ApplyCarState();
        RefreshAllUI();
    }

    private void GoLeft()
    {
        if (_currentCarIndex > 0)
            SelectCar(_currentCarIndex - 1);
    }

    private void GoRight()
    {
        if (_currentCarIndex < CarCount - 1)
            SelectCar(_currentCarIndex + 1);
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

        // TMP texts
        if (carNameTMP != null) carNameTMP.text = data.displayCarName;
        if (modelNameTMP != null) modelNameTMP.text = data.displayModelName;

        // Navigation buttons
        if (goLeftButton != null) goLeftButton.interactable = _currentCarIndex > 0;
        if (goRightButton != null) goRightButton.interactable = _currentCarIndex < CarCount - 1;

        // Sub-controllers
        if (colorUI != null) colorUI.Refresh(data, state.colorIndex);
        if (stickerUI != null) stickerUI.Refresh(data, state.colorIndex, state.stickerIndex);
        if (partsUI != null) partsUI.Refresh(data, state.enabledParts, database.globalPartKeys);
    }

    // ══════════════════ Public API (called by sub-controllers) ══════════════════

    /// <summary>Changes the active car's color and refreshes skin + sticker previews.</summary>
    public void SetColor(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= 6) return;

        CarState state = CurrentState;
        state.colorIndex = colorIndex;

        // Apply new skin
        Material mat = CurrentCarData.GetMaterial(state.colorIndex, state.stickerIndex);
        CurrentCustomizer?.ApplySkin(mat);

        // Sticker previews depend on the selected color
        if (stickerUI != null)
            stickerUI.Refresh(CurrentCarData, state.colorIndex, state.stickerIndex);

        // Update color selection visual
        if (colorUI != null)
            colorUI.Refresh(CurrentCarData, state.colorIndex);
    }

    /// <summary>Changes the active car's sticker and refreshes skin + highlight.</summary>
    public void SetSticker(int stickerIndex)
    {
        if (stickerIndex < 0 || stickerIndex >= 6) return;

        CarState state = CurrentState;
        state.stickerIndex = stickerIndex;

        // Apply new skin
        Material mat = CurrentCarData.GetMaterial(state.colorIndex, state.stickerIndex);
        CurrentCustomizer?.ApplySkin(mat);

        // Move highlight
        if (stickerUI != null)
            stickerUI.SetHighlight(stickerIndex);
    }

    /// <summary>Toggles a mod part on/off for the active car.</summary>
    public void TogglePart(string partKey)
    {
        if (string.IsNullOrEmpty(partKey)) return;

        CarState state = CurrentState;

        bool nowActive;
        if (state.enabledParts.Contains(partKey))
        {
            state.enabledParts.Remove(partKey);
            nowActive = false;
        }
        else
        {
            state.enabledParts.Add(partKey);
            nowActive = true;
        }

        CurrentCustomizer?.SetPartActive(partKey, nowActive);

        if (partsUI != null)
            partsUI.UpdatePartHighlight(partKey, nowActive);
    }
}
