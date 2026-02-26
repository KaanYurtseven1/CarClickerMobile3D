// ════════════════════════════════════════════════════════════════
// CarPartOption.cs – Serializable data class for a single mod-part entry.
// ════════════════════════════════════════════════════════════════
using System;
using UnityEngine;

[Serializable]
public class CarPartOption
{
    [Tooltip("Must match the child Transform name under the car root (e.g. Camurluk_1, Spoiler_3).")]
    public string partKey;

    [Tooltip("UI icon sprite for this part. Each car may have unique icons.")]
    public Sprite icon;
}
