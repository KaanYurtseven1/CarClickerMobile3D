// ════════════════════════════════════════════════════════════════
// CarDataSO.cs – ScriptableObject holding ALL data for one car.
// Create via:  Assets ▸ Create ▸ Garage ▸ Car Data
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCarData", menuName = "Garage/Car Data")]
public class CarDataSO : ScriptableObject
{
    // ─────────────────── Identity ───────────────────
    [Header("─── Identity ───")]
    [Tooltip("Unique ID matching the root Transform name under CarPlatform/Car (e.g. Mazda, Vw, Bmv).")]
    public string carId;

    [Tooltip("Optional override for the Transform name. If empty, carId is used.")]
    public string carRootName;

    [Tooltip("Display name shown in Car_Name TMP (e.g. MAZDA).")]
    public string displayCarName;

    [Tooltip("Display name shown in Model_Name TMP (e.g. RX-7).")]
    public string displayModelName;

    /// <summary>The name used to locate the car root Transform under carsParent.</summary>
    public string CarRootName => string.IsNullOrEmpty(carRootName) ? carId : carRootName;

    // ─────────────────── Base Stats ───────────────────
    [Header("─── Base Stats (0–15) ───")]
    [Tooltip("Base durability bar value for this car (0–15).")]
    [Range(0, 15)] public int baseDurability;

    [Tooltip("Base acceleration bar value for this car (0–15).")]
    [Range(0, 15)] public int baseAcceleration;

    [Tooltip("Base speed bar value for this car (0–15).")]
    [Range(0, 15)] public int baseSpeed;

    // ─────────────────── Base Material ───────────────────
    [Header("─── Base Material ───")]
    [Tooltip("Template material (URP/Lit). The generator copies its shader + properties and swaps _BaseMap.")]
    public Material baseMaterialTemplate;

    // ─────────────────── Colors ───────────────────
    [Header("─── Colors (6) ───")]
    [Tooltip("Exactly 6 entries. Index = colorIndex used by UI and runtime.")]
    public List<CarColorOption> colors = new List<CarColorOption>(6);

    // ─────────────────── Color Folders (Editor Only) ───────────────────
    [Header("─── Color Folders (6) — Editor Only ───")]
    [Tooltip("Drag the 6 Kaplama sub-folders here, in the SAME order as the Colors list above.")]
    public List<Object> colorFolders = new List<Object>(6);

    // ─────────────────── Sticker Keys ───────────────────
    [Header("─── Sticker Keys (6) ───")]
    [Tooltip("Variant keys matching textures in each color folder.\n" +
             "Index 0 = base (no suffix = \"\").  Indices 1-5 = alphabetical suffixes.\n" +
             "When autoDiscoverStickerKeys is ON, the generator overwrites this list.")]
    public List<string> stickerKeys = new List<string>(6);

    [Tooltip("When enabled (default), the material generator auto-discovers sticker keys " +
             "from colorFolders[0] instead of relying on manual entry.")]
    public bool autoDiscoverStickerKeys = true;

    // ─────────────────── Part Options ───────────────────
    [Header("─── Part Options (18) ───")]
    [Tooltip("One entry per mod part, in the SAME order as GarageDatabaseSO.globalPartKeys.")]
    public List<CarPartOption> partOptions = new List<CarPartOption>(18);

    // ─────────────────── Generated Materials ───────────────────
    [Header("─── Generated Materials (36) — Filled by Editor Tool ───")]
    [Tooltip("Flat array: index = colorIndex * 6 + stickerIndex. Populated by Tools ▸ Garage ▸ Generate Materials.")]
    public List<Material> generatedMaterialsFlat = new List<Material>(36);

    // ─────────────────── Sticker Preview Sprites (6) ───────────────────
    [Header("─── Sticker Preview Sprites (6) — Filled by Editor Tool ───")]
    [Tooltip("6 sticker logo sprites for the UI preview slots (colour-independent).\n" +
             "Index 0 = base, 1-5 = variants sorted alphabetically.\n" +
             "Populated by Tools ▸ Garage ▸ Assign Sticker Logos.")]
    [SerializeField] private List<Sprite> stickerPreviewSprites = new List<Sprite>(6);

    /// <summary>Read-only access to the 6 sticker logo sprites used by the sticker preview UI.</summary>
    public IReadOnlyList<Sprite> StickerPreviewSprites => stickerPreviewSprites;

    // ─────────────────── Output Folder (Editor Only) ───────────────────
    [Header("─── Output Folder — Editor Only ───")]
    [Tooltip("Optional folder where generated materials are saved. Defaults to Assets/GeneratedMaterials/<carId>/.")]
    public Object outputMaterialFolder;

    // ══════════════════ Runtime API ══════════════════

    /// <summary>
    /// Returns the pre-generated material for the given combination.
    /// Falls back to <see cref="baseMaterialTemplate"/> with a warning when missing.
    /// </summary>
    public Material GetMaterial(int colorIndex, int stickerIndex)
    {
        if (colorIndex < 0 || colorIndex >= 6 || stickerIndex < 0 || stickerIndex >= 6)
        {
            Debug.LogWarning($"[CarDataSO:{carId}] Invalid indices: color={colorIndex}, sticker={stickerIndex}.");
            return baseMaterialTemplate;
        }

        int flat = colorIndex * 6 + stickerIndex;
        if (flat < generatedMaterialsFlat.Count && generatedMaterialsFlat[flat] != null)
            return generatedMaterialsFlat[flat];

        Debug.LogWarning($"[CarDataSO:{carId}] Missing material at flat[{flat}] " +
                         $"(color={colorIndex}, sticker={stickerIndex}). Using base template.");
        return baseMaterialTemplate;
    }

    /// <summary>
    /// Reads the <c>_BaseMap</c> texture from the generated material.  Returns null when unavailable.
    /// </summary>
    public Texture2D GetSkinTexture(int colorIndex, int stickerIndex)
    {
        Material mat = GetMaterial(colorIndex, stickerIndex);
        if (mat == null) return null;
        return mat.GetTexture("_BaseMap") as Texture2D;
    }

    // ══════════════════ Validation ══════════════════

    /// <summary>
    /// Quick validation.  Returns <c>true</c> when the asset is properly configured.
    /// </summary>
    public bool Validate(out string error)
    {
        error = string.Empty;

        if (string.IsNullOrEmpty(carId))
        { error = "carId is empty."; return false; }

        if (baseMaterialTemplate == null)
        { error = $"[{carId}] baseMaterialTemplate is null."; return false; }

        if (colors == null || colors.Count != 6)
        { error = $"[{carId}] colors.Count = {colors?.Count ?? 0}, expected 6."; return false; }

        if (colorFolders == null || colorFolders.Count != 6)
        { error = $"[{carId}] colorFolders.Count = {colorFolders?.Count ?? 0}, expected 6."; return false; }

        // stickerKeys validated only when autoDiscoverStickerKeys is OFF
        if (!autoDiscoverStickerKeys && (stickerKeys == null || stickerKeys.Count != 6))
        { error = $"[{carId}] stickerKeys.Count = {stickerKeys?.Count ?? 0}, expected 6 (autoDiscover is OFF)."; return false; }

        if (partOptions == null || partOptions.Count != 18)
        { error = $"[{carId}] partOptions.Count = {partOptions?.Count ?? 0}, expected 18."; return false; }

        return true;
    }

    // ══════════════════ Editor Validation ══════════════════

    private void OnValidate()
    {
        if (stickerPreviewSprites == null || stickerPreviewSprites.Count != 6)
        {
            Debug.LogWarning($"[CarDataSO:{carId}] stickerPreviewSprites.Count = " +
                             $"{stickerPreviewSprites?.Count ?? 0}, expected 6. " +
                             "Run Tools ▸ Garage ▸ Assign Sticker Logos to populate.");
        }
    }
}
