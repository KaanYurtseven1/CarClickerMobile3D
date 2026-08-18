using UnityEngine;

namespace CarClicker.Environment
{
    /// <summary>
    /// Data-only ScriptableObject describing one environment preset (Day, Night, etc.).
    /// Applied at runtime by EnvironmentManager to RenderSettings + the scene sun light.
    /// No logic here — pure inspector-driven values.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnvProfile_New",
        menuName = "CarClicker/Environment/Environment Profile",
        order = 0)]
    public class EnvironmentProfileSO : ScriptableObject
    {
        [Header("Skybox")]
        [Tooltip("Material assigned to RenderSettings.skybox when this profile activates.")]
        public Material skybox;

        [Header("Sun (Directional Light)")]
        [Tooltip("Color applied to the scene's main directional light.")]
        [ColorUsage(showAlpha: false, hdr: false)]
        public Color sunColor = Color.white;

        [Tooltip("Intensity of the main directional light.")]
        [Min(0f)]
        public float sunIntensity = 1f;

        [Tooltip("Euler rotation (X, Y, Z) applied to the main directional light's transform.")]
        public Vector3 sunRotation = new Vector3(50f, 30f, 0f);

        [Header("Ambient & Reflections")]
        [Tooltip("RenderSettings.ambientIntensity (skybox-source ambient multiplier).")]
        [Min(0f)]
        public float ambientIntensity = 1f;

        [Tooltip("RenderSettings.reflectionIntensity (skybox-source reflection multiplier).")]
        [Range(0f, 1f)]
        public float reflectionIntensity = 1f;

        [Header("Fog")]
        [Tooltip("RenderSettings.fogColor — should match the skybox horizon band to avoid seams.")]
        [ColorUsage(showAlpha: false, hdr: false)]
        public Color fogColor = new Color(0.78f, 0.83f, 0.88f, 1f);

        [Tooltip("RenderSettings.fogStartDistance (Linear mode only).")]
        [Min(0f)]
        public float fogStart = 45f;

        [Tooltip("RenderSettings.fogEndDistance (Linear mode only).")]
        [Min(0f)]
        public float fogEnd = 120f;

        [Tooltip("RenderSettings.fogMode. Linear is recommended for this project.")]
        public FogMode fogMode = FogMode.Linear;
    }
}
