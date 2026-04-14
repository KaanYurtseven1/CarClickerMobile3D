// ════════════════════════════════════════════════════════════════
// MainSceneCarController.cs – Applies garage customisation state
// to the player car in the Main scene.
//
// Place on a GameObject in Main.  Assign the same GarageDatabaseSO
// and a carsParent Transform that contains all 7 car roots (same
// hierarchy as the Garage scene's CarPlatform/Car).
//
// On scene load it activates the selected car, applies the saved
// skin + parts, tags it "Car", and disables CarEvolution.
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

public class MainSceneCarController : MonoBehaviour
{
    [Header("─── References ───")]
    [SerializeField] private GarageDatabaseSO database;
    [SerializeField] private Transform carsParent;

    private void Start()
    {
        ApplyGarageState();
    }

    private void OnEnable()
    {
        SaveSystem.OnGameLoaded += ApplyGarageState;
    }

    private void OnDisable()
    {
        SaveSystem.OnGameLoaded -= ApplyGarageState;
    }

    public void ApplyGarageState()
    {
        if (database == null || carsParent == null || database.cars == null || database.cars.Count == 0)
        {
            Debug.LogWarning("[MainSceneCarController] Missing database or carsParent.");
            return;
        }

        var save = GarageSaveData.Instance;
        int selectedIndex = (save != null) ? Mathf.Clamp(save.SelectedCarIndex, 0, database.cars.Count - 1) : 0;

        // Safety: if the selected car is locked (e.g. stale save), fall back to 0 (Mazda).
        if (save != null)
        {
            CarDataSO selData = database.cars[selectedIndex];
            if (selData != null && !save.IsCarUnlocked(selData.carId))
                selectedIndex = 0;
        }

        // Ensure only the active child car carries the "Car" tag (not the parent container)
        if (carsParent.CompareTag("Car"))
            carsParent.gameObject.tag = "Untagged";

        // Deactivate all cars, then activate the selected one
        for (int i = 0; i < database.cars.Count; i++)
        {
            CarDataSO data = database.cars[i];
            if (data == null) continue;

            Transform root = carsParent.Find(data.CarRootName);
            if (root == null) continue;

            if (i == selectedIndex)
            {
                root.gameObject.SetActive(true);
                root.gameObject.tag = "Car";

                ApplyCustomisation(root, data);
                NotifyCarChanged(root);
            }
            else
            {
                root.gameObject.tag = "Untagged";
                root.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyCustomisation(Transform root, CarDataSO data)
    {
        if (data == null) return;

        // Read saved state for this car
        GarageSaveData.CarSaveEntry entry = null;
        if (GarageSaveData.Instance != null)
            entry = GarageSaveData.Instance.GetStateForCar(data.carId);

        int colorIdx = entry != null ? entry.colorIndex : 0;
        int stickerIdx = entry != null ? entry.stickerIndex : 0;
        HashSet<string> parts = entry != null ? new HashSet<string>(entry.enabledParts) : new HashSet<string>();

        // Skin
        Material mat = data.GetMaterial(colorIdx, stickerIdx);

        CarCustomizer customizer = root.GetComponent<CarCustomizer>();
        if (customizer != null)
        {
            customizer.Initialize(database.globalPartKeys);
            customizer.ApplySkin(mat);
            customizer.RestoreParts(parts);
        }
        else
        {
            // Fallback: apply material directly to all renderers
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mat;
        }

        // Disable CarEvolution to prevent material conflicts
        CarEvolution evo = root.GetComponent<CarEvolution>();
        if (evo != null)
        {
            evo.enabled = false;
            Debug.Log("[MainSceneCarController] Disabled CarEvolution on active car.");
        }

        Debug.Log($"[MainSceneCarController] Applied garage state: car={data.carId} color={colorIdx} sticker={stickerIdx} parts={parts.Count}");
    }

    /// <summary>
    /// Notifies all gameplay systems that cache a reference to the player car
    /// so they rebind to the newly active car.
    /// </summary>
    private void NotifyCarChanged(Transform activeCar)
    {
        // NitroMagnet: rebind magnetTarget, shieldVFX, areaCollider to the new car's children
        if (NitroMagnetController.Instance != null)
            NitroMagnetController.Instance.RefreshCarReferences(activeCar);

        // Boost VFX: rebind BoostFX_L / BoostFX_R under the new car
        if (BoostModeEffectsIntegration.Instance != null)
            BoostModeEffectsIntegration.Instance.RefreshCarReference(activeCar);

        // Boost cinematic: rebind car transform for shake effects
        if (BoostModeCinematicController.Instance != null)
            BoostModeCinematicController.Instance.RefreshCarReference(activeCar);

        Debug.Log($"[MainSceneCarController] Notified all systems of car change → {activeCar.name}");
    }
}
