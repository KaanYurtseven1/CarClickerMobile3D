// ════════════════════════════════════════════════════════════════
// StickerLogoAssignerEditor.cs
//
// Editor tool that auto-fills CarDataSO.stickerPreviewSprites
// from the sticker logo PNG files located at:
//   Assets/PrefabsBugraElif/UI/StickerLogo_180x95 (1)/StickerLogo_180x95/<CarId>/
//
// Ordering rule:
//   Slot 0 (base)   = the sprite whose filename has exactly 2 '_' characters.
//   Slots 1-5       = the remaining 5 sprites sorted by filename (case-insensitive).
//
// Menu items:
//   Tools ▸ Garage ▸ Assign Sticker Logos ▸ For Selected CarDataSO
//   Tools ▸ Garage ▸ Assign Sticker Logos ▸ For All Cars (GarageDatabaseSO)
// ════════════════════════════════════════════════════════════════
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class StickerLogoAssignerEditor
{
    private const string LOGO_ROOT =
        "Assets/PrefabsBugraElif/UI/StickerLogo_180x95 (1)/StickerLogo_180x95";

    // ═══════════════════ Menu Items ═══════════════════

    [MenuItem("Tools/Garage/Assign Sticker Logos/For Selected CarDataSO")]
    private static void AssignForSelected()
    {
        CarDataSO selected = Selection.activeObject as CarDataSO;
        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "Sticker Logo Assigner",
                "Select a CarDataSO asset in the Project window first.",
                "OK");
            return;
        }

        bool ok = AssignForCar(selected);
        AssetDatabase.SaveAssets();

        if (ok)
            Debug.Log($"[StickerLogoAssigner] Successfully assigned 6 sticker logos for '{selected.carId}'.");
    }

    [MenuItem("Tools/Garage/Assign Sticker Logos/For Selected CarDataSO", true)]
    private static bool ValidateForSelected() => Selection.activeObject is CarDataSO;

    // ───────────────────────────────────────────────────

    [MenuItem("Tools/Garage/Assign Sticker Logos/For All Cars (GarageDatabaseSO)")]
    private static void AssignForAll()
    {
        GarageDatabaseSO db = Selection.activeObject as GarageDatabaseSO;

        if (db == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:GarageDatabaseSO");
            if (guids.Length == 1)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                db = AssetDatabase.LoadAssetAtPath<GarageDatabaseSO>(path);
            }
        }

        if (db == null)
        {
            EditorUtility.DisplayDialog(
                "Sticker Logo Assigner",
                "Select a GarageDatabaseSO asset in the Project window,\n" +
                "or ensure exactly one exists in the project.",
                "OK");
            return;
        }

        int success = 0;
        int total = 0;

        for (int i = 0; i < db.cars.Count; i++)
        {
            CarDataSO data = db.cars[i];
            if (data == null)
            {
                Debug.LogWarning($"[StickerLogoAssigner] db.cars[{i}] is null — skipping.");
                continue;
            }

            total++;
            EditorUtility.DisplayProgressBar(
                "Assigning Sticker Logos",
                $"Processing {data.carId} ({i + 1}/{db.cars.Count})…",
                (float)i / db.cars.Count);

            if (AssignForCar(data))
                success++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        Debug.Log($"[StickerLogoAssigner] ══ COMPLETE ══  {success}/{total} car(s) assigned successfully.");
    }

    [MenuItem("Tools/Garage/Assign Sticker Logos/For All Cars (GarageDatabaseSO)", true)]
    private static bool ValidateForAll()
    {
        if (Selection.activeObject is GarageDatabaseSO) return true;
        return AssetDatabase.FindAssets("t:GarageDatabaseSO").Length > 0;
    }

    // ═══════════════════ Core Logic ═══════════════════

    /// <summary>
    /// Loads the 6 sticker logo sprites for <paramref name="data"/> and writes
    /// them into its <c>stickerPreviewSprites</c> field.
    /// Returns true on success.
    /// </summary>
    private static bool AssignForCar(CarDataSO data)
    {
        if (string.IsNullOrEmpty(data.carId))
        {
            Debug.LogError("[StickerLogoAssigner] carId is empty — skipping.");
            return false;
        }

        string folderPath = $"{LOGO_ROOT}/{data.carId}";

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"[StickerLogoAssigner:{data.carId}] Folder not found: '{folderPath}'.");
            return false;
        }

        // Find all Sprite/Texture assets in the folder (direct children only)
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        List<Sprite> allSprites = new List<Sprite>();

        string normalizedFolder = folderPath.Replace("\\", "/");

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string assetDir = Path.GetDirectoryName(assetPath).Replace("\\", "/");

            // Only direct children — skip sub-folders
            if (!assetDir.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase))
                continue;

            // Try loading as Sprite first; fall back to generating from Texture2D import
            Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (spr != null)
            {
                allSprites.Add(spr);
            }
            else
            {
                Debug.LogWarning($"[StickerLogoAssigner:{data.carId}] '{Path.GetFileName(assetPath)}' " +
                                 "could not be loaded as Sprite. Set its Texture Type to 'Sprite (2D and UI)' " +
                                 "in the import settings.");
            }
        }

        if (allSprites.Count != 6)
        {
            Debug.LogError($"[StickerLogoAssigner:{data.carId}] Found {allSprites.Count} sprites " +
                           $"in '{folderPath}', expected exactly 6.");
            return false;
        }

        // ── Separate base from variants ──
        // Base sprite: filename (without extension) has exactly 2 '_' characters.
        Sprite baseSprite = null;
        List<Sprite> variants = new List<Sprite>();

        foreach (var spr in allSprites)
        {
            string fileName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(spr));
            int underscoreCount = fileName.Count(c => c == '_');

            if (underscoreCount == 1)
            {
                // Pattern like "LogoBmv_Siyah" — only 1 underscore after "Logo"
                if (baseSprite != null)
                {
                    Debug.LogError($"[StickerLogoAssigner:{data.carId}] Multiple base-candidate sprites found " +
                                   $"(both '{Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(baseSprite))}' " +
                                   $"and '{fileName}' have ≤2 underscores). Cannot determine base.");
                    return false;
                }
                baseSprite = spr;
            }
            else
            {
                variants.Add(spr);
            }
        }

        if (baseSprite == null)
        {
            Debug.LogError($"[StickerLogoAssigner:{data.carId}] No base sprite found " +
                           "(expected one filename with exactly 1 '_' character, e.g. 'LogoBmv_Siyah').");
            return false;
        }

        if (variants.Count != 5)
        {
            Debug.LogError($"[StickerLogoAssigner:{data.carId}] Found {variants.Count} variant sprites, " +
                           "expected 5.");
            return false;
        }

        // Sort variants alphabetically by filename (case-insensitive)
        variants.Sort((a, b) =>
        {
            string nameA = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(a));
            string nameB = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(b));
            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
        });

        // ── Compose final list: [base, variant0, variant1, variant2, variant3, variant4] ──
        List<Sprite> result = new List<Sprite>(6) { baseSprite };
        result.AddRange(variants);

        // ── Write into the serialized field via reflection ──
        SerializedObject so = new SerializedObject(data);
        SerializedProperty prop = so.FindProperty("stickerPreviewSprites");

        if (prop == null)
        {
            Debug.LogError($"[StickerLogoAssigner:{data.carId}] Could not find " +
                           "'stickerPreviewSprites' property on CarDataSO. Did the field get renamed?");
            return false;
        }

        prop.ClearArray();
        for (int i = 0; i < 6; i++)
        {
            prop.InsertArrayElementAtIndex(i);
            prop.GetArrayElementAtIndex(i).objectReferenceValue = result[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(data);

        Debug.Log($"[StickerLogoAssigner:{data.carId}] Assigned: " +
                  $"[{string.Join(", ", result.Select(s => s.name))}]");

        return true;
    }
}
#endif
