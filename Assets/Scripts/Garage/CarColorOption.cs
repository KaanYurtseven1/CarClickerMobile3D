// ════════════════════════════════════════════════════════════════
// CarColorOption.cs – Serializable data class for a single color entry.
// ════════════════════════════════════════════════════════════════
using System;
using UnityEngine;

[Serializable]
public class CarColorOption
{
    [Tooltip("Display label (e.g., Kırmızı, Mavi, Siyah)")]
    public string label;

    [Tooltip("Hex color code: RRGGBB or #RRGGBB.  Leave empty for placeholder (white).")]
    public string hexColor;

    /// <summary>
    /// Parses <see cref="hexColor"/> and returns a Unity Color.
    /// Falls back to <c>Color.white</c> if hex is missing or invalid.
    /// </summary>
    public Color GetColor()
    {
        if (string.IsNullOrWhiteSpace(hexColor))
            return Color.white;

        string hex = hexColor.Trim();
        if (!hex.StartsWith("#"))
            hex = "#" + hex;

        if (ColorUtility.TryParseHtmlString(hex, out Color c))
            return c;

        Debug.LogWarning($"[CarColorOption] Failed to parse hex '{hexColor}' for label '{label}'. Defaulting to white.");
        return Color.white;
    }
}
