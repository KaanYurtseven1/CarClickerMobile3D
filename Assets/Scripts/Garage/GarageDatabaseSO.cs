// ════════════════════════════════════════════════════════════════
// GarageDatabaseSO.cs – Central database referencing all CarDataSO assets.
// Create via:  Assets ▸ Create ▸ Garage ▸ Garage Database
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GarageDatabase", menuName = "Garage/Garage Database")]
public class GarageDatabaseSO : ScriptableObject
{
    [Header("Cars (in UI order: Mazda → Bugatti)")]
    [Tooltip("Drag CarDataSO assets here in the exact order shown in the garage.")]
    public List<CarDataSO> cars = new List<CarDataSO>();

    [Header("Global Part Keys (18)")]
    [Tooltip("Shared mod-part key names.  Order must match CarDataSO.partOptions order for every car.")]
    public List<string> globalPartKeys = new List<string>()
    {
        "Camurluk_1", "Camurluk_2", "Camurluk_3",
        "Egzoz_1",    "Egzoz_2",    "Egzoz_3",
        "Kaput_1",    "Kaput_2",    "Kaput_3",
        "Kaput_4",    "Kaput_5",    "Kaput_6",    "Kaput_7",
        "Spoiler_1",  "Spoiler_2",  "Spoiler_3",  "Spoiler_4",  "Spoiler_5"
    };

    [Header("Default Sticker Keys (6)")]
    [Tooltip("Convenience list.  Each CarDataSO may override with its own keys.")]
    public List<string> defaultStickerKeys = new List<string>()
    {
        "Default", "98", "Alev", "Asim", "Bir", "Ejder"
    };

    // ══════════════════ Accessors ══════════════════

    /// <summary>Finds a <see cref="CarDataSO"/> by its <c>carId</c>.</summary>
    public CarDataSO GetCarById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < cars.Count; i++)
            if (cars[i] != null && cars[i].carId == id)
                return cars[i];
        return null;
    }

    /// <summary>Returns the list index for the given <c>carId</c>, or -1.</summary>
    public int GetCarIndex(string id)
    {
        for (int i = 0; i < cars.Count; i++)
            if (cars[i] != null && cars[i].carId == id)
                return i;
        return -1;
    }

    // ══════════════════ Validation ══════════════════

    /// <summary>
    /// Validates every car entry and logs problems to the Console.
    /// </summary>
    public void ValidateDatabase()
    {
        if (cars == null || cars.Count == 0)
        {
            Debug.LogError("[GarageDatabaseSO] No cars in database.");
            return;
        }

        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i] == null)
            {
                Debug.LogError($"[GarageDatabaseSO] cars[{i}] is null.");
                continue;
            }

            if (!cars[i].Validate(out string err))
                Debug.LogError($"[GarageDatabaseSO] Validation failed for cars[{i}] ({cars[i].carId}): {err}");

            // Check every global part key is present in the car's partOptions
            if (globalPartKeys != null)
            {
                foreach (string key in globalPartKeys)
                {
                    bool found = false;
                    if (cars[i].partOptions != null)
                    {
                        foreach (var po in cars[i].partOptions)
                        {
                            if (po != null && po.partKey == key) { found = true; break; }
                        }
                    }
                    if (!found)
                        Debug.LogWarning($"[GarageDatabaseSO] Car '{cars[i].carId}' is missing partOption for key '{key}'.");
                }
            }

            // Check generatedMaterialsFlat
            if (cars[i].generatedMaterialsFlat == null || cars[i].generatedMaterialsFlat.Count < 36)
                Debug.LogWarning($"[GarageDatabaseSO] Car '{cars[i].carId}' has {cars[i].generatedMaterialsFlat?.Count ?? 0}/36 generated materials. Run the generator.");
        }

        Debug.Log($"[GarageDatabaseSO] Validation complete. {cars.Count} car(s) checked.");
    }
}
