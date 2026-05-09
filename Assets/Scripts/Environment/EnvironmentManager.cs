using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace CarClicker.Environment
{
    /// <summary>
    /// Applies an EnvironmentProfileSO to RenderSettings and the scene sun light.
    /// Supports instant snap or timed blend between Day and Night profiles.
    /// Inspector-driven only — no save system, no UI, no gameplay coupling.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnvironmentManager : MonoBehaviour
    {
        public enum EnvironmentMode { Day, Night }

        [System.Serializable]
        public class ModeChangedEvent : UnityEvent<bool> { } // true = Night, false = Day

        [Header("Scene References")]
        [Tooltip("Main directional light in the scene (Sun_Directional). Color/intensity/rotation are driven by the active profile.")]
        public Light sun;

        [Header("Profiles")]
        [Tooltip("Profile applied for Day mode.")]
        public EnvironmentProfileSO dayProfile;

        [Tooltip("Profile applied for Night mode.")]
        public EnvironmentProfileSO nightProfile;

        [Header("Startup")]
        [Tooltip("Mode applied on Start() if 'Apply On Start' is enabled.")]
        public EnvironmentMode startMode = EnvironmentMode.Day;

        [Tooltip("If true, the start mode is applied (snap) in Start().")]
        public bool applyOnStart = true;

        [Header("Blending")]
        [Tooltip("If true, SetMode/Toggle/SetDay/SetNight will blend over time. If false, they snap instantly.")]
        public bool enableBlend = false;

        [Tooltip("Duration of the blend in seconds (uses unscaledDeltaTime).")]
        [Min(0f)]
        public float blendDuration = 1.5f;

        [Header("Auto Cycle")]
        [Tooltip("If true, the system automatically loops between Day and Night using the durations below.")]
        public bool autoCycleEnabled = true;

        [Tooltip("How long Day mode is held before transitioning to Night (seconds, unscaled).")]
        [Min(0.1f)]
        public float dayDuration = 60f;

        [Tooltip("How long Night mode is held before transitioning to Day (seconds, unscaled).")]
        [Min(0.1f)]
        public float nightDuration = 45f;

        [Tooltip("If true, automatic transitions use BlendTo (smooth). If false, they snap with SetMode.")]
        public bool autoCycleUsesBlend = true;

        [Tooltip("If true, calling SetDay/SetNight/Toggle while auto-cycle is running restarts the wait timer for the new mode.")]
        public bool restartCycleOnManualChange = true;

        [Header("Events")]
        [Tooltip("Invoked when the active mode changes. Argument: true = Night, false = Day.")]
        public ModeChangedEvent OnModeChanged = new ModeChangedEvent();

        [Header("Runtime (read-only)")]
        [SerializeField] private EnvironmentMode currentMode = EnvironmentMode.Day;

        private Coroutine blendRoutine;
        private Coroutine cycleRoutine;

        public EnvironmentMode CurrentMode => currentMode;
        public bool IsAutoCycleRunning => cycleRoutine != null;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Start()
        {
            if (applyOnStart)
            {
                SetMode(startMode); // snap on start regardless of enableBlend
            }

            if (autoCycleEnabled)
            {
                StartAutoCycle();
            }
        }

        private void OnDisable()
        {
            StopAutoCycle();
            StopBlend();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        [ContextMenu("Set Day")]
        public void SetDay()
        {
            ManualTransitionTo(EnvironmentMode.Day);
        }

        [ContextMenu("Set Night")]
        public void SetNight()
        {
            ManualTransitionTo(EnvironmentMode.Night);
        }

        [ContextMenu("Toggle Day/Night")]
        public void Toggle()
        {
            EnvironmentMode next = currentMode == EnvironmentMode.Day
                ? EnvironmentMode.Night
                : EnvironmentMode.Day;

            ManualTransitionTo(next);
        }

        /// <summary>Shared path for SetDay/SetNight/Toggle. Honors enableBlend and restarts auto-cycle wait if requested.</summary>
        private void ManualTransitionTo(EnvironmentMode target)
        {
            if (enableBlend && Application.isPlaying) BlendTo(target);
            else SetMode(target);

            if (autoCycleEnabled && Application.isPlaying && restartCycleOnManualChange)
            {
                // Restart so the new mode gets its full duration before flipping.
                RestartAutoCycle();
            }
        }

        // ------------------------------------------------------------------
        // Auto Cycle
        // ------------------------------------------------------------------

        [ContextMenu("Start Auto Cycle")]
        public void StartAutoCycle()
        {
            if (!Application.isPlaying) return;
            if (cycleRoutine != null) return; // never start a duplicate
            autoCycleEnabled = true;
            cycleRoutine = StartCoroutine(AutoCycleRoutine());
        }

        [ContextMenu("Stop Auto Cycle")]
        public void StopAutoCycle()
        {
            if (cycleRoutine != null)
            {
                StopCoroutine(cycleRoutine);
                cycleRoutine = null;
            }
            autoCycleEnabled = false;
        }

        /// <summary>Stop and restart the cycle coroutine so the next wait reflects the (possibly changed) current mode.</summary>
        public void RestartAutoCycle()
        {
            if (!Application.isPlaying) return;
            if (cycleRoutine != null)
            {
                StopCoroutine(cycleRoutine);
                cycleRoutine = null;
            }
            autoCycleEnabled = true;
            cycleRoutine = StartCoroutine(AutoCycleRoutine());
        }

        private IEnumerator AutoCycleRoutine()
        {
            // Loop forever until the coroutine is stopped (component disabled,
            // StopAutoCycle called, or RestartAutoCycle replaces it).
            while (true)
            {
                float wait = currentMode == EnvironmentMode.Day ? dayDuration : nightDuration;
                if (wait < 0.1f) wait = 0.1f;

                // Unscaled wait — immune to Time.timeScale (tutorial freeze, pause menus, etc.).
                float t = 0f;
                while (t < wait)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                // If a blend is currently running (e.g. user just toggled), wait for it to finish
                // so we don't stack two transitions on top of each other.
                while (blendRoutine != null) yield return null;

                EnvironmentMode target = currentMode == EnvironmentMode.Day
                    ? EnvironmentMode.Night
                    : EnvironmentMode.Day;

                AutoTransitionTo(target);

                // Wait for the blend (if any) to complete before resuming the wait loop.
                while (blendRoutine != null) yield return null;
            }
        }

        /// <summary>Transition used by the auto cycle. Forces blend or snap based on autoCycleUsesBlend, independent of enableBlend.</summary>
        private void AutoTransitionTo(EnvironmentMode target)
        {
            if (target == currentMode) return;

            if (autoCycleUsesBlend && blendDuration > 0f)
            {
                // Use BlendTo path. BlendTo internally falls back to SetMode if blendDuration <= 0.
                BlendTo(target);
            }
            else
            {
                SetMode(target);
            }
        }

        /// <summary>Snap to a mode immediately. Stops any active blend.</summary>
        public void SetMode(EnvironmentMode mode)
        {
            StopBlend();

            EnvironmentProfileSO profile = GetProfile(mode);
            if (profile == null)
            {
                Debug.LogWarning($"[EnvironmentManager] No profile assigned for {mode}. Aborting SetMode.", this);
                return;
            }

            ApplyProfile(profile);

            currentMode = mode;
            OnModeChanged?.Invoke(mode == EnvironmentMode.Night);
        }

        /// <summary>Blend over blendDuration seconds toward the target mode. Skybox snaps at start.</summary>
        public void BlendTo(EnvironmentMode mode)
        {
            if (!Application.isPlaying)
            {
                // Coroutines do not run in edit mode; fall back to snap.
                SetMode(mode);
                return;
            }

            EnvironmentProfileSO target = GetProfile(mode);
            if (target == null)
            {
                Debug.LogWarning($"[EnvironmentManager] No profile assigned for {mode}. Aborting BlendTo.", this);
                return;
            }

            if (blendDuration <= 0f)
            {
                SetMode(mode);
                return;
            }

            StopBlend();
            blendRoutine = StartCoroutine(BlendRoutine(mode, target));
        }

        /// <summary>Apply a profile instantly to RenderSettings and the sun light.</summary>
        public void ApplyProfile(EnvironmentProfileSO profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("[EnvironmentManager] ApplyProfile called with null profile.", this);
                return;
            }

            // Skybox
            if (profile.skybox != null)
            {
                RenderSettings.skybox = profile.skybox;
                DynamicGI.UpdateEnvironment();
            }
            else
            {
                Debug.LogWarning($"[EnvironmentManager] Profile '{profile.name}' has no skybox assigned.", this);
            }

            // Ambient & reflections
            RenderSettings.ambientIntensity = profile.ambientIntensity;
            RenderSettings.reflectionIntensity = profile.reflectionIntensity;

            // Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = profile.fogMode;
            RenderSettings.fogColor = profile.fogColor;
            RenderSettings.fogStartDistance = profile.fogStart;
            RenderSettings.fogEndDistance = profile.fogEnd;

            // Sun
            if (sun != null)
            {
                sun.color = profile.sunColor;
                sun.intensity = profile.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(profile.sunRotation);
            }
            else
            {
                Debug.LogWarning("[EnvironmentManager] Sun light reference is missing — sun values not applied.", this);
            }
        }

        // ------------------------------------------------------------------
        // Internal
        // ------------------------------------------------------------------

        private EnvironmentProfileSO GetProfile(EnvironmentMode mode)
        {
            return mode == EnvironmentMode.Day ? dayProfile : nightProfile;
        }

        private void StopBlend()
        {
            if (blendRoutine != null)
            {
                StopCoroutine(blendRoutine);
                blendRoutine = null;
            }
        }

        private IEnumerator BlendRoutine(EnvironmentMode targetMode, EnvironmentProfileSO target)
        {
            // Snapshot starting values from the live RenderSettings + sun, so blends starting
            // mid-blend continue smoothly from wherever we currently are.
            Material   fromSkybox       = RenderSettings.skybox;
            float      fromAmbient      = RenderSettings.ambientIntensity;
            float      fromReflection   = RenderSettings.reflectionIntensity;
            Color      fromFogColor     = RenderSettings.fogColor;
            float      fromFogStart     = RenderSettings.fogStartDistance;
            float      fromFogEnd       = RenderSettings.fogEndDistance;
            FogMode    fromFogMode      = RenderSettings.fogMode;

            Color      fromSunColor     = sun != null ? sun.color : target.sunColor;
            float      fromSunIntensity = sun != null ? sun.intensity : target.sunIntensity;
            Quaternion fromSunRot       = sun != null ? sun.transform.rotation : Quaternion.Euler(target.sunRotation);
            Quaternion toSunRot         = Quaternion.Euler(target.sunRotation);

            // Skybox snaps at start of blend (cubemaps cannot be cross-faded cheaply on mobile).
            if (target.skybox != null && target.skybox != fromSkybox)
            {
                RenderSettings.skybox = target.skybox;
                DynamicGI.UpdateEnvironment();
            }

            // Fog mode also snaps (no meaningful interpolation between modes).
            if (target.fogMode != fromFogMode)
            {
                RenderSettings.fogMode = target.fogMode;
            }

            RenderSettings.fog = true;

            float t = 0f;
            while (t < blendDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / blendDuration);

                RenderSettings.ambientIntensity    = Mathf.Lerp(fromAmbient,    target.ambientIntensity,    k);
                RenderSettings.reflectionIntensity = Mathf.Lerp(fromReflection, target.reflectionIntensity, k);
                RenderSettings.fogColor            = Color.Lerp(fromFogColor,   target.fogColor,            k);
                RenderSettings.fogStartDistance    = Mathf.Lerp(fromFogStart,   target.fogStart,            k);
                RenderSettings.fogEndDistance      = Mathf.Lerp(fromFogEnd,     target.fogEnd,              k);

                if (sun != null)
                {
                    sun.color              = Color.Lerp(fromSunColor, target.sunColor, k);
                    sun.intensity          = Mathf.Lerp(fromSunIntensity, target.sunIntensity, k);
                    sun.transform.rotation = Quaternion.Slerp(fromSunRot, toSunRot, k);
                }

                yield return null;
            }

            // Snap to exact end values to avoid floating-point drift.
            RenderSettings.ambientIntensity    = target.ambientIntensity;
            RenderSettings.reflectionIntensity = target.reflectionIntensity;
            RenderSettings.fogColor            = target.fogColor;
            RenderSettings.fogStartDistance    = target.fogStart;
            RenderSettings.fogEndDistance      = target.fogEnd;

            if (sun != null)
            {
                sun.color              = target.sunColor;
                sun.intensity          = target.sunIntensity;
                sun.transform.rotation = toSunRot;
            }

            blendRoutine = null;
            currentMode = targetMode;
            OnModeChanged?.Invoke(targetMode == EnvironmentMode.Night);
        }
    }
}
