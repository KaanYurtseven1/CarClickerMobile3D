// ════════════════════════════════════════════════════════════════
// ShowcaseCarSpawner.cs – Activates the correct car model in
// TakeTheCarScene, applies the player's saved skin / parts, then
// kicks off the cinematic and returns to Main when it finishes.
//
// Attach to a dedicated "ShowcaseSetup" GameObject in the scene.
//
// Flow:
//   BlacklistPanelController  →  sets PendingCarId  →  loads scene
//   ShowcaseCarSpawner.Start  →  activates car  →  director.Play()
//   director.onComplete       →  AdvanceToNextTier  →  LoadScene("Main")
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShowcaseCarSpawner : MonoBehaviour
{
    // ─── Static bridge (set before scene load) ───

    /// <summary>
    /// Car ID to showcase.  Set by <see cref="BlacklistPanelController"/>
    /// before loading TakeTheCarScene.
    /// </summary>
    public static string PendingCarId { get; set; }

    // ─── Inspector references ───

    [Header("── Data ──")]
    [Tooltip("The shared garage database that contains all CarDataSO entries.")]
    [SerializeField] private GarageDatabaseSO database;

    [Header("── Scene Hierarchy ──")]
    [Tooltip("Parent transform whose children are the car root GameObjects " +
             "(same pattern as Main / Garage scenes).")]
    [SerializeField] private Transform carsParent;

    [Tooltip("The showcase director that drives the cinematic sequence.")]
    [SerializeField] private CarShowcaseDirector director;

    // ═══════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════

    private void Start()
    {
        if (database == null || carsParent == null || director == null)
        {
            Debug.LogError("[ShowcaseCarSpawner] Missing reference(s). Check Inspector.");
            return;
        }

        string carId = ResolvePendingCarId();

        if (string.IsNullOrEmpty(carId))
        {
            Debug.LogWarning("[ShowcaseCarSpawner] No PendingCarId set. " +
                             "Returning to Main scene.");
            SceneManager.LoadScene("Main");
            return;
        }

        CarDataSO carData = database.GetCarById(carId);

        if (carData == null)
        {
            Debug.LogError($"[ShowcaseCarSpawner] Car '{carId}' not found in GarageDatabase.");
            SceneManager.LoadScene("Main");
            return;
        }

        ActivateCar(carData);

        // Feed car name to the reveal controller.
        if (director.CarNameReveal != null)
            director.CarNameReveal.SetCarInfo(carData.displayCarName, carData.displayModelName);

        // Wire completion and start the cinematic.
        director.onComplete.AddListener(OnShowcaseComplete);
        director.Play();
    }

    private void OnDestroy()
    {
        if (director != null)
            director.onComplete.RemoveListener(OnShowcaseComplete);
    }

    // ═══════════════════════════════════════════════════════════
    //  Car setup
    // ═══════════════════════════════════════════════════════════

    private void ActivateCar(CarDataSO carData)
    {
        // Deactivate every car root first.
        foreach (Transform child in carsParent)
            child.gameObject.SetActive(false);

        // Find and activate the target car.
        Transform carRoot = carsParent.Find(carData.CarRootName);

        if (carRoot == null)
        {
            Debug.LogError($"[ShowcaseCarSpawner] Car root '{carData.CarRootName}' " +
                           $"not found under '{carsParent.name}'.");
            return;
        }

        carRoot.gameObject.SetActive(true);

        // Apply saved skin / parts (mirrors MainSceneCarController pattern).
        ApplyCustomisation(carRoot, carData);

        // Hand the active car root to the director (for turntable / look-at).
        director.CarRoot = carRoot;
    }

    private void ApplyCustomisation(Transform root, CarDataSO data)
    {
        GarageSaveData.CarSaveEntry entry = null;

        if (GarageSaveData.Instance != null)
            entry = GarageSaveData.Instance.GetStateForCar(data.carId);

        int colorIdx = entry != null ? entry.colorIndex : 0;
        int stickerIdx = entry != null ? entry.stickerIndex : 0;
        HashSet<string> parts = entry != null
            ? new HashSet<string>(entry.enabledParts)
            : new HashSet<string>();

        Material mat = data.GetMaterial(colorIdx, stickerIdx);

        CarCustomizer customizer = root.GetComponent<CarCustomizer>();

        if (customizer != null)
        {
            customizer.Initialize(database.globalPartKeys);
            customizer.ApplySkin(mat);
            customizer.RestoreParts(parts);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Completion
    // ═══════════════════════════════════════════════════════════

    private void OnShowcaseComplete()
    {
        // Unlock the reward car BEFORE advancing the tier
        // (ActiveTier is still the completed tier at this point).
        var mgr = BlacklistManager.Instance;
        if (mgr != null && mgr.ActiveTier != null && mgr.ActiveTier.rewardCar != null)
        {
            string rewardCarId = mgr.ActiveTier.rewardCar.carId;
            if (GarageSaveData.Instance != null)
                GarageSaveData.Instance.MarkCarUnlocked(rewardCarId);
        }

        // Advance the blacklist AFTER the showcase plays.
        mgr?.AdvanceToNextTier();

        SceneManager.LoadScene("Main");
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the car ID to use.  Prefers the static <see cref="PendingCarId"/>;
    /// falls back to <see cref="BlacklistManager.ActiveTier"/> if available.
    /// Clears the static field after reading so it's never stale.
    /// </summary>
    private static string ResolvePendingCarId()
    {
        // Primary: explicit ID set before scene load.
        if (!string.IsNullOrEmpty(PendingCarId))
        {
            string id = PendingCarId;
            PendingCarId = null;           // consume so it's never stale
            return id;
        }

        // Fallback: read from the BlacklistManager if it survived scene load.
        var mgr = BlacklistManager.Instance;
        if (mgr != null && mgr.ActiveTier != null && mgr.ActiveTier.rewardCar != null)
            return mgr.ActiveTier.rewardCar.carId;

        return null;
    }
}
