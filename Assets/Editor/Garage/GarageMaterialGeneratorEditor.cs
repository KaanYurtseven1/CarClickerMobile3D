// ════════════════════════════════════════════════════════════════
// GarageMaterialGeneratorEditor.cs
//
// Editor tool that auto-generates 36 URP/Lit Material assets
// per car from the baked skin textures in the Kaplama folders.
//
// KEY FEATURE:  Auto-discovers sticker variant keys PER CAR
//               by scanning the first color folder, extracting
//               the suffix after the color token in each texture
//               name.  No manual stickerKeys entry required.
//
// Menu items:
//   Tools ▸ Garage ▸ Generate Materials ▸ For Selected CarDataSO
//   Tools ▸ Garage ▸ Generate Materials ▸ For GarageDatabaseSO (All Cars)
//   Tools ▸ Garage ▸ Validate Database
// ════════════════════════════════════════════════════════════════
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GarageMaterialGeneratorEditor
{
    private const string BASE_MAP = "_BaseMap";

    // ═══════════════════ Menu Items ═══════════════════

    [MenuItem("Tools/Garage/Generate Materials/For Selected CarDataSO")]
    private static void GenerateForSelected()
    {
        CarDataSO selected = Selection.activeObject as CarDataSO;
        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "Garage Material Generator",
                "Select a CarDataSO asset in the Project window first.",
                "OK");
            return;
        }

        int count = GenerateForCar(selected);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MaterialGenerator] Done — {count}/36 materials generated/updated for '{selected.carId}'.");
    }

    [MenuItem("Tools/Garage/Generate Materials/For Selected CarDataSO", true)]
    private static bool ValidateForSelected() => Selection.activeObject is CarDataSO;

    // ───────────────────────────────────────────────────

    [MenuItem("Tools/Garage/Generate Materials/For GarageDatabaseSO (All Cars)")]
    private static void GenerateForAll()
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
                "Garage Material Generator",
                "Select a GarageDatabaseSO asset in the Project window,\n" +
                "or ensure exactly one exists in the project.",
                "OK");
            return;
        }

        int totalMats = 0;
        int totalCars = 0;

        for (int i = 0; i < db.cars.Count; i++)
        {
            CarDataSO data = db.cars[i];
            if (data == null)
            {
                Debug.LogWarning($"[MaterialGenerator] db.cars[{i}] is null — skipping.");
                continue;
            }

            EditorUtility.DisplayProgressBar(
                "Generating Materials",
                $"Processing {data.carId} ({i + 1}/{db.cars.Count})…",
                (float)i / db.cars.Count);

            int count = GenerateForCar(data);
            totalMats += count;
            totalCars++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MaterialGenerator] ══ COMPLETE ══  {totalMats} materials across {totalCars} car(s).");
    }

    [MenuItem("Tools/Garage/Generate Materials/For GarageDatabaseSO (All Cars)", true)]
    private static bool ValidateForAll()
    {
        if (Selection.activeObject is GarageDatabaseSO) return true;
        return AssetDatabase.FindAssets("t:GarageDatabaseSO").Length > 0;
    }

    // ───────────────────────────────────────────────────

    [MenuItem("Tools/Garage/Validate Database")]
    private static void ValidateDatabase()
    {
        GarageDatabaseSO db = Selection.activeObject as GarageDatabaseSO;
        if (db == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:GarageDatabaseSO");
            if (guids.Length == 1)
                db = AssetDatabase.LoadAssetAtPath<GarageDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (db == null)
        {
            EditorUtility.DisplayDialog("Validate", "Select or create a GarageDatabaseSO first.", "OK");
            return;
        }

        db.ValidateDatabase();
    }

    // ═══════════════════ Core Generation ═══════════════════

    /// <summary>
    /// Generates / updates all 36 material assets for one car.
    /// Auto-discovers sticker variant keys when <c>autoDiscoverStickerKeys</c>
    /// is enabled or the existing list is incomplete.
    /// Returns the number of materials successfully created or updated.
    /// </summary>
    private static int GenerateForCar(CarDataSO data)
    {
        // ── Basic validation (relaxed — stickerKeys may be empty; we will fill them) ──
        if (string.IsNullOrEmpty(data.carId))
        {
            Debug.LogError("[MaterialGenerator] carId is empty — aborting.");
            return 0;
        }
        if (data.baseMaterialTemplate == null)
        {
            Debug.LogError($"[MaterialGenerator:{data.carId}] baseMaterialTemplate is null — aborting.");
            return 0;
        }
        if (data.colorFolders == null || data.colorFolders.Count != 6)
        {
            Debug.LogError($"[MaterialGenerator:{data.carId}] colorFolders.Count = " +
                           $"{data.colorFolders?.Count ?? 0}, expected 6 — aborting.");
            return 0;
        }

        // ════════════════════════════════════════════════════
        //  STEP 1 — Auto-discover sticker variant keys
        // ════════════════════════════════════════════════════
        bool needsDiscovery = data.autoDiscoverStickerKeys
            || data.stickerKeys == null
            || data.stickerKeys.Count != 6
            || data.stickerKeys.Any(k => k == null);

        if (needsDiscovery)
        {
            List<string> discovered = DiscoverStickerKeysFromFolder(data, 0);
            if (discovered == null)
            {
                Debug.LogError($"[MaterialGenerator:{data.carId}] Sticker key auto-discovery FAILED — aborting.");
                return 0;
            }

            data.stickerKeys = discovered;
            Debug.Log($"[MaterialGenerator:{data.carId}] Auto-discovered sticker keys: " +
                      $"[{string.Join(", ", discovered.Select(k => k == "" ? "(Base)" : k))}]");
        }

        // ════════════════════════════════════════════════════
        //  STEP 2 — Validate all 6 folders share the same key set
        // ════════════════════════════════════════════════════
        HashSet<string> referenceKeySet =
            new HashSet<string>(data.stickerKeys, StringComparer.OrdinalIgnoreCase);

        for (int c = 0; c < 6; c++)
        {
            if (data.colorFolders[c] == null) continue;
            string fp = AssetDatabase.GetAssetPath(data.colorFolders[c]);
            if (string.IsNullOrEmpty(fp) || !AssetDatabase.IsValidFolder(fp)) continue;

            string colorToken = ExtractColorTokenFromFolderName(data.carId, Path.GetFileName(fp));
            List<Texture2D> textures = FindTexturesInFolder(fp);

            if (textures.Count != 6)
            {
                Debug.LogError($"[MaterialGenerator:{data.carId}] Folder '{Path.GetFileName(fp)}' " +
                               $"has {textures.Count} textures (expected 6) — aborting.");
                return 0;
            }

            HashSet<string> folderKeys =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tex in textures)
                folderKeys.Add(ExtractVariantKey(tex.name, colorToken));

            var missing = new List<string>();
            var extra   = new List<string>();

            foreach (string rk in referenceKeySet)
                if (!folderKeys.Contains(rk))
                    missing.Add(rk == "" ? "(Base)" : rk);

            foreach (string fk in folderKeys)
                if (!referenceKeySet.Contains(fk))
                    extra.Add(fk == "" ? "(Base)" : fk);

            if (missing.Count > 0 || extra.Count > 0)
            {
                Debug.LogError(
                    $"[MaterialGenerator:{data.carId}] Key mismatch in folder " +
                    $"'{Path.GetFileName(fp)}'!\n" +
                    $"  Missing: [{string.Join(", ", missing)}]\n" +
                    $"  Extra:   [{string.Join(", ", extra)}]\n" +
                    "  Aborting generation for this car.");
                return 0;
            }
        }

        // ════════════════════════════════════════════════════
        //  STEP 3 — Generate / update 36 materials
        // ════════════════════════════════════════════════════
        Material template   = data.baseMaterialTemplate;
        string outputFolder = GetOutputFolder(data);
        EnsureFolderExists(outputFolder);

        // Ensure the flat list has exactly 36 slots
        while (data.generatedMaterialsFlat.Count < 36)
            data.generatedMaterialsFlat.Add(null);
        while (data.generatedMaterialsFlat.Count > 36)
            data.generatedMaterialsFlat.RemoveAt(data.generatedMaterialsFlat.Count - 1);

        int generated = 0;

        for (int c = 0; c < 6; c++)
        {
            if (c >= data.colorFolders.Count || data.colorFolders[c] == null)
            {
                Debug.LogWarning($"[MaterialGenerator:{data.carId}] colorFolders[{c}] is null — skipping.");
                continue;
            }

            string folderPath = AssetDatabase.GetAssetPath(data.colorFolders[c]);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[MaterialGenerator:{data.carId}] colorFolders[{c}] invalid — skipping.");
                continue;
            }

            string colorFolderName = Path.GetFileName(folderPath);
            string colorToken      = ExtractColorTokenFromFolderName(data.carId, colorFolderName);

            // Build key → texture map for this folder
            List<Texture2D> textures = FindTexturesInFolder(folderPath);
            var keyToTex = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            foreach (var tex in textures)
            {
                string vk = ExtractVariantKey(tex.name, colorToken);
                if (!keyToTex.ContainsKey(vk))
                    keyToTex[vk] = tex;
                else
                    Debug.LogWarning($"[MaterialGenerator:{data.carId}] Duplicate key " +
                                     $"'{(vk == "" ? "(Base)" : vk)}' in '{colorFolderName}'.");
            }

            for (int s = 0; s < 6; s++)
            {
                string variantKey = data.stickerKeys[s];
                int flat = c * 6 + s;

                if (!keyToTex.TryGetValue(variantKey, out Texture2D tex))
                {
                    string displayKey = variantKey == "" ? "(Base)" : variantKey;
                    Debug.LogWarning($"[MaterialGenerator:{data.carId}] No texture for " +
                                     $"C{c}({colorFolderName}) S{s}({displayKey}).");
                    continue;
                }

                // ── Deterministic material name ──
                string safeKey = variantKey == "" ? "Base" : SanitizeFileName(variantKey);
                string matName = $"{data.carId}__C{c}__S{s}__{safeKey}";
                string matPath = $"{outputFolder}/{matName}.mat";

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                if (mat != null)
                {
                    // Overwrite existing material in-place
                    mat.shader = template.shader;
                    mat.CopyPropertiesFromMaterial(template);
                    mat.SetTexture(BASE_MAP, tex);
                    EditorUtility.SetDirty(mat);
                }
                else
                {
                    mat = new Material(template) { name = matName };
                    mat.SetTexture(BASE_MAP, tex);
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                data.generatedMaterialsFlat[flat] = mat;
                generated++;
            }
        }

        EditorUtility.SetDirty(data);
        Debug.Log($"[MaterialGenerator:{data.carId}] {generated}/36 materials in '{outputFolder}'.");
        return generated;
    }

    // ═══════════════════ Auto-Discovery ═══════════════════

    /// <summary>
    /// Scans the textures inside <c>colorFolders[folderIndex]</c> and returns a
    /// list of 6 variant keys.  Index 0 is always <c>""</c> (base / no suffix).
    /// Indices 1-5 are the remaining keys sorted alphabetically (case-insensitive).
    /// Returns <c>null</c> on failure.
    /// </summary>
    private static List<string> DiscoverStickerKeysFromFolder(CarDataSO data,
                                                               int folderIndex)
    {
        if (folderIndex >= data.colorFolders.Count || data.colorFolders[folderIndex] == null)
        {
            Debug.LogError($"[Discovery:{data.carId}] colorFolders[{folderIndex}] is null.");
            return null;
        }

        string folderPath = AssetDatabase.GetAssetPath(data.colorFolders[folderIndex]);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"[Discovery:{data.carId}] colorFolders[{folderIndex}] " +
                           $"is not a valid folder: '{folderPath}'.");
            return null;
        }

        string folderName = Path.GetFileName(folderPath);
        string colorToken = ExtractColorTokenFromFolderName(data.carId, folderName);

        if (string.IsNullOrEmpty(colorToken))
        {
            Debug.LogError($"[Discovery:{data.carId}] Could not extract color token " +
                           $"from folder '{folderName}'.");
            return null;
        }

        List<Texture2D> textures = FindTexturesInFolder(folderPath);
        if (textures.Count != 6)
        {
            Debug.LogError($"[Discovery:{data.carId}] Folder '{folderName}' has " +
                           $"{textures.Count} textures, expected exactly 6.");
            return null;
        }

        var uniqueKeys  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderedKeys = new List<string>();

        foreach (var tex in textures)
        {
            string vk = ExtractVariantKey(tex.name, colorToken);
            if (!uniqueKeys.Add(vk))
            {
                Debug.LogError($"[Discovery:{data.carId}] Duplicate variant key " +
                               $"'{(vk == "" ? "(Base)" : vk)}' in folder '{folderName}'. " +
                               "Check texture names.");
                return null;
            }
            orderedKeys.Add(vk);
        }

        if (uniqueKeys.Count != 6)
        {
            Debug.LogError($"[Discovery:{data.carId}] Expected 6 unique keys, " +
                           $"got {uniqueKeys.Count} in '{folderName}'.");
            return null;
        }

        // Sort: "" (base) always first, then alphabetical for the rest.
        orderedKeys.Sort((a, b) =>
        {
            if (a == "" && b == "") return 0;
            if (a == "") return -1;
            if (b == "") return  1;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });

        return orderedKeys;
    }

    // ═══════════════════ Variant Key Extraction ═══════════════════

    /// <summary>
    /// Extracts the color token from a Kaplama sub-folder name.
    /// <para>Examples:</para>
    /// <list type="bullet">
    ///   <item><c>Mazda_Default_Kirmizi</c> → <c>Kirmizi</c></item>
    ///   <item><c>Mazda_Mavi</c>            → <c>Mavi</c></item>
    ///   <item><c>Nardo_Default_Gri</c>     → <c>Gri</c></item>
    ///   <item><c>Bugatti_Default_Mavi</c>  → <c>Mavi</c></item>
    /// </list>
    /// </summary>
    private static string ExtractColorTokenFromFolderName(string carId,
                                                           string folderName)
    {
        // Remove "<CarId>_" prefix (case-insensitive)
        string remainder = folderName;
        if (remainder.StartsWith(carId + "_", StringComparison.OrdinalIgnoreCase))
            remainder = remainder.Substring(carId.Length + 1);

        // Remove "Default_" prefix if present
        if (remainder.StartsWith("Default_", StringComparison.OrdinalIgnoreCase))
            remainder = remainder.Substring("Default_".Length);

        // What remains is the color name (e.g. "Kirmizi", "Mavi", "Gri").
        return remainder.Trim();
    }

    /// <summary>
    /// Extracts the variant (sticker) key from a texture name given the
    /// known <paramref name="colorToken"/>.
    /// <para>Algorithm:</para>
    /// <list type="number">
    ///   <item>Split texture name by <c>_</c>.</item>
    ///   <item>Find the first token matching <paramref name="colorToken"/>
    ///         (case-insensitive).</item>
    ///   <item>Join all tokens AFTER it with <c>_</c>.</item>
    ///   <item>If nothing remains → base key <c>""</c>.</item>
    /// </list>
    /// <para>Examples (colorToken = "Kirmizi"):</para>
    /// <list type="bullet">
    ///   <item><c>Mazda_Kirmizi</c>               → <c>""</c></item>
    ///   <item><c>Mazda_Kirmizi_98</c>            → <c>"98"</c></item>
    ///   <item><c>Mazda_Kirmizi_Alev</c>          → <c>"Alev"</c></item>
    ///   <item><c>Mazda_Kirmizi_Asimetrik_X</c>   → <c>"Asimetrik_X"</c></item>
    /// </list>
    /// </summary>
    private static string ExtractVariantKey(string textureName, string colorToken)
    {
        string[] tokens = textureName.Split('_');

        // Find the color token position
        int colorPos = -1;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (string.Equals(tokens[i], colorToken, StringComparison.OrdinalIgnoreCase))
            {
                colorPos = i;
                break;
            }
        }

        if (colorPos >= 0 && colorPos < tokens.Length - 1)
        {
            // Everything after the color token is the variant key
            return string.Join("_", tokens, colorPos + 1,
                               tokens.Length - colorPos - 1);
        }

        if (colorPos >= 0)
        {
            // Color token is the last token → base variant
            return "";
        }

        // ── Fallback: color token not found ──
        // Heuristic: 2 tokens = base; else last token = key.
        Debug.LogWarning(
            $"[VariantKey] Color token '{colorToken}' not found in texture " +
            $"'{textureName}'. Falling back to heuristic.");

        if (tokens.Length <= 2)
            return "";

        return tokens[tokens.Length - 1];
    }

    // ═══════════════════ Utilities ═══════════════════

    /// <summary>
    /// Finds all <see cref="Texture2D"/> assets that are direct children of
    /// the given folder (ignores sub-folders).
    /// </summary>
    private static List<Texture2D> FindTexturesInFolder(string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D",
                                                   new[] { folderPath });
        var list = new List<Texture2D>();
        string normalizedFolder = folderPath.Replace("\\", "/");

        foreach (string guid in guids)
        {
            string texPath = AssetDatabase.GUIDToAssetPath(guid);
            string texDir  = Path.GetDirectoryName(texPath).Replace("\\", "/");
            if (!texDir.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase))
                continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null) list.Add(tex);
        }

        return list;
    }

    private static string GetOutputFolder(CarDataSO data)
    {
        if (data.outputMaterialFolder != null)
        {
            string path = AssetDatabase.GetAssetPath(data.outputMaterialFolder);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                return path;
        }
        return $"Assets/GeneratedMaterials/{data.carId}";
    }

    private static void EnsureFolderExists(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;

        string[] parts = assetPath.Split('/');
        string current = parts[0]; // "Assets"

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
            name = name.Replace(c, '_');
        return name;
    }
}
#endif
