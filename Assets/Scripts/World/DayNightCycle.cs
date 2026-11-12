using UnityEngine;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Ciclo de dia y noche: un dia completo dura 20 minutos reales. De noche los
    /// enemigos pegan un 15 % mas y su esencia sale con +15 de calidad; cada cuarta
    /// noche sale la Luna carmesi (+30 % de daño enemigo, +30 de calidad).
    /// Mueve la luz direccional y la luz ambiente; restaura la escena al desactivarse.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayNightCycle : MonoBehaviour
    {
        public const float DaySeconds = 1200f;
        public const float StartHour = 8f;
        public const float NightStart = 21f;
        public const float NightEnd = 5.5f;
        public const int CrimsonEvery = 4;

        private static DayNightCycle active;

        private float elapsed;
        private Light sun;
        private float sunIntensity;
        private Color sunColor;
        private Quaternion sunRotation;
        private Color ambient;
        private bool captured;

        public static DayNightCycle Active => active;
        public float Hour => HourAt(elapsed);
        public int Day => DayAt(elapsed);
        public bool IsNight => IsNightHour(Hour);
        public bool IsCrimsonMoon => IsNight && IsCrimsonNight(elapsed);

        /// <summary>Multiplicador del daño enemigo segun la hora (1 de dia).</summary>
        public static float EnemyDamageMultiplier =>
            active == null || !active.IsNight ? 1f : active.IsCrimsonMoon ? 1.3f : 1.15f;

        /// <summary>Calidad extra de la esencia absorbida segun la hora.</summary>
        public static int EssenceQualityBonus =>
            active == null || !active.IsNight ? 0 : active.IsCrimsonMoon ? 30 : 15;

        public static float HourAt(float seconds) =>
            Mathf.Repeat(StartHour + Mathf.Max(0f, seconds) / DaySeconds * 24f, 24f);

        public static int DayAt(float seconds) =>
            1 + Mathf.FloorToInt((StartHour + Mathf.Max(0f, seconds) / DaySeconds * 24f) / 24f);

        public static bool IsNightHour(float hour) => hour >= NightStart || hour < NightEnd;

        /// <summary>La noche que empieza el dia N pertenece a la Luna carmesi si N es multiplo de 4.</summary>
        public static bool IsCrimsonNight(float seconds)
        {
            float hour = HourAt(seconds);
            int day = DayAt(seconds);
            int nightOfDay = hour < NightEnd ? day - 1 : day;
            return nightOfDay > 0 && nightOfDay % CrimsonEvery == 0;
        }

        public void SetElapsed(float seconds) => elapsed = Mathf.Max(0f, seconds);

        public string Clock
        {
            get
            {
                float hour = Hour;
                int h = Mathf.FloorToInt(hour);
                int m = Mathf.FloorToInt((hour - h) * 60f);
                string phase = IsCrimsonMoon ? "LUNA CARMESÍ" : IsNight ? "noche" : "día";
                return "Día " + Day + " · " + h.ToString("00") + ":" + m.ToString("00") + " " + phase;
            }
        }

        private void OnEnable()
        {
            active = this;
            Capture();
        }

        private void OnDisable()
        {
            if (active == this)
                active = null;
            if (captured && sun != null)
            {
                sun.intensity = sunIntensity;
                sun.color = sunColor;
                sun.transform.rotation = sunRotation;
            }
            if (captured)
                RenderSettings.ambientLight = ambient;
        }

        private void Capture()
        {
            if (captured)
                return;
            sun = RenderSettings.sun;
            if (sun == null)
                foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional)
                    {
                        sun = light;
                        break;
                    }
            if (sun != null)
            {
                sunIntensity = sun.intensity;
                sunColor = sun.color;
                sunRotation = sun.transform.rotation;
            }
            ambient = RenderSettings.ambientLight;
            captured = true;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            ApplyLighting();
        }

        private void ApplyLighting()
        {
            float hour = Hour;
            // 1 a mediodia, 0 a medianoche, suavizado.
            float daylight = Mathf.Clamp01(Mathf.Cos((hour - 13f) / 24f * Mathf.PI * 2f) * 0.85f + 0.5f);
            bool crimson = IsCrimsonMoon;
            Color night = crimson ? new Color(0.75f, 0.25f, 0.3f) : new Color(0.45f, 0.55f, 0.85f);
            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(0.38f, sunIntensity, daylight);
                sun.color = Color.Lerp(night, sunColor, daylight);
                float angle = Mathf.Lerp(25f, 60f, daylight);
                sun.transform.rotation = Quaternion.Euler(angle, hour / 24f * 360f, 0f);
            }
            RenderSettings.ambientLight = Color.Lerp(ambient * (crimson ? new Color(0.85f, 0.45f, 0.5f) : new Color(0.55f, 0.6f, 0.8f)),
                ambient, daylight);
        }
    }
}
