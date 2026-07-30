using UnityEngine;

namespace LastMile
{
    /// <summary>
    /// Singleton that owns the shift countdown and drives the directional light's
    /// rotation, intensity, and color across the dusk -> night -> dawn arc.
    /// The sun is the core pacing pillar of Last Mile: every other system paces
    /// itself against this timer.
    ///
    /// Uses a single real directional light for all scene illumination and shadows —
    /// URP only ever renders real-time shadows from whichever light is pinned as the
    /// scene's Sun Source (Window > Rendering > Lighting > Environment), regardless of
    /// any other light's own Shadow Type setting, so introducing a second "moon" light
    /// never actually gets shadows no matter how its Shadows field is set. Instead this
    /// one light swaps between playing the sun's role and the moon's role — matching
    /// Super Simple Skybox's own reference setup, where only the Sun light ever casts
    /// shadows and the Moon is purely a non-shadow-casting fill light.
    ///
    /// The Super Simple Skybox's sun/moon disc positions are driven directly via its
    /// global shader properties (_SunDirection, _MoonDirection, _MainLightMatrix) rather
    /// than through its Sun/Moon MonoBehaviours, so no extra "proxy" Light GameObjects
    /// are needed purely for visuals.
    /// </summary>
    public class NightCycleManager : MonoBehaviour
    {
        // Minimum shift duration, in seconds, to avoid divide-by-zero when
        // computing NormalizedTime.
        private const float MinimumShiftDurationSeconds = 10f;

        // Super Simple Skybox's global shader property IDs (OccaSoftware.SuperSimpleSkybox.Runtime.ShaderParams).
        // Set directly here instead of via the package's Sun/Moon MonoBehaviours so this
        // manager can drive the skybox's sun/moon disc positions independently of which
        // one is currently providing real scene light.
        private static readonly int SunDirectionShaderId = Shader.PropertyToID("_SunDirection");
        private static readonly int MoonDirectionShaderId = Shader.PropertyToID("_MoonDirection");
        private static readonly int MainLightMatrixShaderId = Shader.PropertyToID("_MainLightMatrix");

        [Header("References")]
        [Tooltip("The single directional light used for all real scene illumination and shadows across the whole cycle. Must be set as this scene's Sun Source (Window > Rendering > Lighting > Environment) — URP only casts real-time shadows from that light, so this manager never introduces a second shadow-casting light.")]
        [SerializeField] private Light sunLight;

        [Header("Shift Timing")]
        [Tooltip("Total length of a shift, in seconds, from dusk to sunrise.")]
        [SerializeField] private float shiftDurationSeconds = 600f;

        [Tooltip("NormalizedTime (0-1) at which the dawn lethal exposure window begins.")]
        [SerializeField] private float dawnWindowStartNormalized = 0.9f;

        [Header("Sun Motion")]
        [Tooltip("Compass heading (degrees) the sun/moon arc starts at (NormalizedTime 0, dusk). 0 = arcs along +Z.")]
        [SerializeField] private float sunRiseAzimuthDegrees = -90f;

        [Tooltip("Compass heading (degrees) the sun/moon arc ends at (NormalizedTime 1, sunrise). Different from the rise azimuth so the sun/moon sweep continuously across the sky instead of bobbing up and down at a fixed heading.")]
        [SerializeField] private float sunSetAzimuthDegrees = 90f;

        [Tooltip("Sun elevation in degrees above the horizon (negative = below horizon/night), evaluated across NormalizedTime. Default arcs from dusk, down through the depths of night, then up through sunrise right at the end.")]
        [SerializeField]
        private AnimationCurve sunElevationCurve = new AnimationCurve(
            new Keyframe(0f, -15f),
            new Keyframe(0.5f, -55f),
            new Keyframe(0.9f, -12f),
            new Keyframe(1f, 8f));

        [Tooltip("Sun light intensity across NormalizedTime, evaluated 0-1. Used both for the skybox's sun disc brightness and as this shift's real light intensity whenever the sun is the dominant light source (see moonBaseIntensity).")]
        [SerializeField]
        private AnimationCurve sunIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.5f, 0.08f),
            new Keyframe(0.9f, 0.15f),
            new Keyframe(0.95f, 0.55f),
            new Keyframe(1f, 1.2f));

        [Tooltip("Sun light color across NormalizedTime: dusk orange -> night blue-black -> dawn pale orange.")]
        [SerializeField] private Gradient sunColorGradient;

        [Header("Moon")]
        [Tooltip("Compass heading (degrees) added to the sun's current azimuth to place the moon on the opposite side of the sky. 180 = directly antipodal.")]
        [SerializeField] private float moonAzimuthOffsetDegrees = 180f;

        [Tooltip("Moon elevation in degrees above the horizon, evaluated across NormalizedTime on its own arc (independent of the sun's shallow elevation curve) so it rises near the horizon, arcs up overhead near midnight, and sets near dawn — a normal moon arc instead of a side-to-side sweep.")]
        [SerializeField]
        private AnimationCurve moonElevationCurve = new AnimationCurve(
            new Keyframe(0f, -8f),
            new Keyframe(0.15f, 30f),
            new Keyframe(0.5f, 75f),
            new Keyframe(0.85f, 30f),
            new Keyframe(1f, -8f));

        [Tooltip("Moon light intensity while it's above the horizon; fades out across the moon fade elevation range below. Kept bright enough (well above a 'realistic' dim moon) so night reads as moodily lit rather than pitch black, and so its shadows are actually visible — since sunLight itself takes on this value whenever the moon is dominant.")]
        [SerializeField] private float moonBaseIntensity = 1.1f;

        [Tooltip("Moon light color — a dim, cool fill so night isn't pure black.")]
        [SerializeField] private Color moonColor = new Color(0.55f, 0.65f, 1f, 1f);

        [Tooltip("Moon elevation (degrees, on the moon's own arc) at or below which the moon is fully faded out/invisible. Kept a few degrees below the horizon so the fade isn't an abrupt cutoff right at moonrise/moonset.")]
        [SerializeField] private float moonFadeStartElevationDegrees = -5f;

        [Tooltip("Moon elevation (degrees, on the moon's own arc) at or above which the moon is fully visible. A few degrees above the horizon so it ramps in smoothly right after moonrise.")]
        [SerializeField] private float moonFadeEndElevationDegrees = 6f;

        [Header("Night Visibility")]
        [Tooltip("Multiplies the scene's ambient/indirect light (RenderSettings.ambientIntensity) across NormalizedTime. Keeps the skybox's moody night colors intact while still filling shadowed surfaces with enough ambient light to read clearly — without this, faces not directly hit by the sun/moon would go pure black even with a bright moon.")]
        [SerializeField]
        private AnimationCurve ambientIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.5f, 0.7f),
            new Keyframe(0.9f, 0.7f),
            new Keyframe(1f, 1f));

        [Tooltip("Shadow strength applied to sunLight at all times. Kept below 1 so shadowed areas retain some ambient fill instead of going pure black, while still reading clearly as shadows.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float activeShadowStrength = 0.65f;

        [Header("Skybox")]
        [Tooltip("Optional skybox material to assign to RenderSettings.skybox at startup (e.g. the Super Simple Skybox procedural material). Its sun/moon disc directions are driven directly by this manager via Shader.SetGlobalVector — leave unassigned to keep whatever skybox is already set in Lighting settings.")]
        [SerializeField] private Material skyboxMaterial;

        // Debug/test hook — lets designers fast-forward a shift in the editor.
        private float timeScale = 1f;
        private bool isShiftActive;
        private bool hasFiredSunrise;

        public static NightCycleManager Instance { get; private set; }

        public float TimeRemainingSeconds { get; private set; }

        /// <summary>0 = dusk (shift start), 1 = sunrise.</summary>
        public float NormalizedTime { get; private set; }

        /// <summary>True once NormalizedTime has crossed dawnWindowStartNormalized.</summary>
        public bool IsDawnLethalWindow { get; private set; }

        /// <summary>The light actually illuminating and casting shadows for the scene, exposed for other systems (e.g. exposure raycasts).</summary>
        public Light SunLight => sunLight;

        /// <summary>Total length of the current/last-started shift, in seconds.</summary>
        public float ShiftDurationSeconds => shiftDurationSeconds;

        /// <summary>NormalizedTime (0-1) at which the dawn lethal exposure window begins.</summary>
        public float DawnWindowStartNormalized => dawnWindowStartNormalized;

        /// <summary>True while a shift is actively counting down (false before StartShift or after sunrise).</summary>
        public bool IsShiftActive => isShiftActive;

        /// <summary>Fires on every tick with the seconds remaining in the shift.</summary>
        public event System.Action<float> OnTimeRemainingChanged;

        /// <summary>Fires once when the dawn lethal exposure window begins.</summary>
        public event System.Action OnDawnWindowEntered;

        /// <summary>Fires once when the timer reaches zero.</summary>
        public event System.Action OnSunrise;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[NightCycleManager] Duplicate instance detected — destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (sunLight == null)
            {
                // Fall back to Unity's canonical "sun" reference — the Light assigned
                // as Sun Source in Window > Rendering > Lighting > Environment — so
                // scenes don't require a manual drag-and-drop of the directional light.
                sunLight = RenderSettings.sun;
            }

            if (sunLight == null)
            {
                Debug.LogError("[NightCycleManager] sunLight is not assigned and no RenderSettings.sun is set. Assign the scene's directional light in the Inspector, or mark it as the Sun Source in Lighting settings.");
            }
            else
            {
                // sunLight must be the scene's pinned Sun Source for URP to ever grant it
                // real-time shadows — force it here so a stale Lighting settings reference
                // can't silently break shadows for this light.
                RenderSettings.sun = sunLight;
                sunLight.shadows = LightShadows.Soft;
            }

            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
            }
        }

        /// <summary>Resets and begins the shift countdown.</summary>
        public void StartShift(float durationSeconds)
        {
            shiftDurationSeconds = Mathf.Max(durationSeconds, MinimumShiftDurationSeconds);
            TimeRemainingSeconds = shiftDurationSeconds;
            NormalizedTime = 0f;
            IsDawnLethalWindow = false;
            hasFiredSunrise = false;
            isShiftActive = true;

            ApplySunState();
            OnTimeRemainingChanged?.Invoke(TimeRemainingSeconds);
        }

        /// <summary>Multiplier on countdown speed, for fast-iteration testing in the editor.</summary>
        public void SetTimeScale(float scale)
        {
            timeScale = Mathf.Max(scale, 0f);
        }

        /// <summary>
        /// Debug/test hook — jumps the shift directly to a given point on the dusk-to-dawn
        /// arc (0 = dusk, 1 = sunrise) without waiting for real time to pass. Requires a
        /// shift to have been started first, since it scales against shiftDurationSeconds.
        /// </summary>
        public void SkipToNormalizedTime(float normalizedTime)
        {
            if (!isShiftActive && !hasFiredSunrise)
            {
                Debug.LogWarning("[NightCycleManager] SkipToNormalizedTime called before StartShift — call StartShift first.");
                return;
            }

            normalizedTime = Mathf.Clamp01(normalizedTime);
            TimeRemainingSeconds = shiftDurationSeconds * (1f - normalizedTime);
            isShiftActive = TimeRemainingSeconds > 0f;

            Tick();
        }

        private void Update()
        {
            if (!isShiftActive)
            {
                return;
            }

            TimeRemainingSeconds -= Time.deltaTime * timeScale;
            if (TimeRemainingSeconds < 0f)
            {
                TimeRemainingSeconds = 0f;
            }

            Tick();
        }

        // Recomputes NormalizedTime from TimeRemainingSeconds, applies the resulting sun
        // state, and fires all timer events. Shared by the real-time Update() tick and
        // the debug SkipToNormalizedTime() jump so both paths behave identically.
        private void Tick()
        {
            NormalizedTime = Mathf.Clamp01(1f - (TimeRemainingSeconds / shiftDurationSeconds));

            ApplySunState();

            OnTimeRemainingChanged?.Invoke(TimeRemainingSeconds);

            if (!IsDawnLethalWindow && NormalizedTime >= dawnWindowStartNormalized)
            {
                IsDawnLethalWindow = true;
                OnDawnWindowEntered?.Invoke();
            }

            if (!hasFiredSunrise && TimeRemainingSeconds <= 0f)
            {
                hasFiredSunrise = true;
                isShiftActive = false;
                OnSunrise?.Invoke();
            }
        }

        private void ApplySunState()
        {
            if (sunLight == null)
            {
                return;
            }

            // --- Sun arc: drives the skybox's sun disc position and is one candidate
            // for sunLight's real rotation/intensity/color this tick.
            float sunElevationDegrees = sunElevationCurve.Evaluate(NormalizedTime);
            float sunElevationRadians = sunElevationDegrees * Mathf.Deg2Rad;
            float sunAzimuthDegrees = Mathf.LerpUnclamped(sunRiseAzimuthDegrees, sunSetAzimuthDegrees, NormalizedTime);
            float sunAzimuthRadians = sunAzimuthDegrees * Mathf.Deg2Rad;

            // Direction from the scene toward the sun's current position in the sky.
            // elevation > 0 is above the horizon (day), elevation < 0 is below it (night).
            Vector3 directionToSun = new Vector3(
                Mathf.Sin(sunAzimuthRadians) * Mathf.Cos(sunElevationRadians),
                Mathf.Sin(sunElevationRadians),
                Mathf.Cos(sunAzimuthRadians) * Mathf.Cos(sunElevationRadians));

            float sunIntensityValue = sunIntensityCurve.Evaluate(NormalizedTime);
            Color sunColorValue = sunColorGradient != null ? sunColorGradient.Evaluate(NormalizedTime) : Color.white;
            // Built directly from elevation/azimuth (the standard Unity day-night convention)
            // instead of Quaternion.LookRotation(-direction, Vector3.up) — LookRotation is
            // undefined/unstable whenever its forward vector nears parallel to the up-hint,
            // which happens right around the moon's near-zenith peak (elevation -> 90).
            // +180 on azimuth: Quaternion.Euler(pitch, yaw, 0)'s forward vector negates the
            // elevation component correctly but NOT the azimuth component, so without this
            // offset the light's forward horizontally points TOWARD the body's compass
            // heading instead of away from it, casting shadows on the wrong side.
            Quaternion sunRotation = Quaternion.Euler(sunElevationDegrees, sunAzimuthDegrees + 180f, 0f);

            // --- Moon arc: drives the skybox's moon disc position and is the other
            // candidate for sunLight's real rotation/intensity/color this tick.
            float moonElevationDegrees = moonElevationCurve.Evaluate(NormalizedTime);
            float moonElevationRadians = moonElevationDegrees * Mathf.Deg2Rad;
            float moonAzimuthDegrees = sunAzimuthDegrees + moonAzimuthOffsetDegrees;
            float moonAzimuthRadians = moonAzimuthDegrees * Mathf.Deg2Rad;

            Vector3 directionToMoon = new Vector3(
                Mathf.Sin(moonAzimuthRadians) * Mathf.Cos(moonElevationRadians),
                Mathf.Sin(moonElevationRadians),
                Mathf.Cos(moonAzimuthRadians) * Mathf.Cos(moonElevationRadians));

            // Fades OUT below the horizon (fadeStart) and IN once above it (fadeEnd) — i.e.
            // the moon should be visible/dominant while it's actually up in the sky, not
            // while it's below the horizon. (Previously inverted via "1 - InverseLerp(...)",
            // which made moonIntensityValue evaluate to 0 across the moon's entire overhead
            // arc, forcing the sun — parked deep below the horizon at night — to incorrectly
            // win "dominance" and point sunLight's forward vector up into the sky.)
            float moonVisibility = Mathf.Clamp01(Mathf.InverseLerp(moonFadeStartElevationDegrees, moonFadeEndElevationDegrees, moonElevationDegrees));
            float moonIntensityValue = moonBaseIntensity * moonVisibility;
            Quaternion moonRotation = Quaternion.Euler(moonElevationDegrees, moonAzimuthDegrees + 180f, 0f);

            // --- Feed the Super Simple Skybox's disc positions directly (bypassing its
            // Sun/Moon MonoBehaviours) so the sky visuals stay independent of which body
            // is currently providing sunLight's real illumination.
            Shader.SetGlobalVector(SunDirectionShaderId, directionToSun);
            Shader.SetGlobalVector(MoonDirectionShaderId, directionToMoon);
            Shader.SetGlobalMatrix(MainLightMatrixShaderId, Matrix4x4.Rotate(sunRotation));

            // --- sunLight is the ONE real light for the whole cycle: whichever of the
            // sun/moon arcs is currently brighter wins its rotation/intensity/color. This
            // guarantees it's always the scene's single Main Light with shadows enabled,
            // instead of trying to hand shadows off between two separate lights (which
            // URP silently ignores for whichever one isn't the pinned Sun Source).
            bool sunIsDominant = sunIntensityValue >= moonIntensityValue;
            sunLight.transform.rotation = sunIsDominant ? sunRotation : moonRotation;
            sunLight.intensity = sunIsDominant ? sunIntensityValue : moonIntensityValue;
            sunLight.color = sunIsDominant ? sunColorValue : moonColor;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = activeShadowStrength;

            // Re-assert every tick, not just in Awake — Unity/URP can silently reset the
            // Sun Source reference (e.g. on a lighting rebuild), and losing it means no
            // real-time shadows with no visible error.
            if (RenderSettings.sun != sunLight)
            {
                RenderSettings.sun = sunLight;
            }

            RenderSettings.ambientIntensity = ambientIntensityCurve.Evaluate(NormalizedTime);
        }
    }
}
