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
        if (_isTransitioning) return;
        if (_currentCarIndex > 0)
            PlayGlitchTransition(_currentCarIndex - 1);
    }

    private void GoRight()
    {
        if (_isTransitioning) return;
        if (_currentCarIndex < CarCount - 1)
            PlayGlitchTransition(_currentCarIndex + 1);
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

        // TMP texts
        if (carNameTMP != null) carNameTMP.text = data.displayCarName;
        if (modelNameTMP != null) modelNameTMP.text = data.displayModelName;

        // Navigation buttons
        if (goLeftButton != null) goLeftButton.interactable = _currentCarIndex > 0;
        if (goRightButton != null) goRightButton.interactable = _currentCarIndex < CarCount - 1;

        // Sub-controllers
        if (colorUI != null) colorUI.Refresh(data, state.colorIndex);
        if (stickerUI != null) stickerUI.Refresh(data, state.stickerIndex);
        if (partsUI != null) partsUI.Refresh(data, state.enabledParts, database.globalPartKeys);

        // Bars
        RefreshBars();
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

        // Sticker previews are colour-independent; refresh to update highlight
        if (stickerUI != null)
            stickerUI.Refresh(CurrentCarData, state.stickerIndex);

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

    /// <summary>Toggles a mod part on/off for the active car.
    /// Group-exclusive: only one part per group (Camurluk, Egzoz, Kaput, Spoiler)
    /// can be equipped at a time.  Equipping a new part in the same group
    /// automatically removes the previous one.</summary>
    public void TogglePart(string partKey)
    {
        if (string.IsNullOrEmpty(partKey)) return;

        CarState state = CurrentState;

        if (state.enabledParts.Contains(partKey))
        {
            // ── Unequip ──
            state.enabledParts.Remove(partKey);
            CurrentCustomizer?.SetPartActive(partKey, false);
            if (partsUI != null) partsUI.UpdatePartHighlight(partKey, false);
        }
        else
        {
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
        }

        RefreshBars(animate: false);
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
