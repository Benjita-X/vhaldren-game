using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Settings
{
    /// <summary>
    /// Opciones del jugador, guardadas en PlayerPrefs y aplicadas al momento:
    /// audio (general y efectos), graficos (calidad, pantalla completa, sincronizacion,
    /// limite de FPS) y juego (temblor, paron de impacto, numeros de daño, ayuda al empezar).
    /// </summary>
    public static class GameSettings
    {
        private const string P = "vh.op.";

        public static event Action Changed;

        public static float MasterVolume { get => Get("volumen", 0.8f); set => Set("volumen", Mathf.Clamp01(value)); }
        public static float EffectsVolume { get => Get("efectos", 0.9f); set => Set("efectos", Mathf.Clamp01(value)); }
        public static float ShakeIntensity { get => Get("temblor", 1f); set => Set("temblor", Mathf.Clamp01(value)); }
        public static bool HitStop { get => Get("paron", 1f) > 0.5f; set => Set("paron", value ? 1f : 0f); }
        public static bool DamageNumbers { get => Get("numeros", 1f) > 0.5f; set => Set("numeros", value ? 1f : 0f); }
        public static bool ShowHelpOnStart { get => Get("ayuda", 1f) > 0.5f; set => Set("ayuda", value ? 1f : 0f); }
        public static bool VSync { get => Get("vsync", 1f) > 0.5f; set => Set("vsync", value ? 1f : 0f); }
        public static int FrameCap { get => Mathf.RoundToInt(Get("fps", 120f)); set => Set("fps", Mathf.Clamp(value, 30, 240)); }
        public static int Quality
        {
            get => Mathf.Clamp(Mathf.RoundToInt(Get("calidad", QualitySettings.GetQualityLevel())), 0, QualitySettings.names.Length - 1);
            set => Set("calidad", Mathf.Clamp(value, 0, QualitySettings.names.Length - 1));
        }
        public static bool Fullscreen { get => Screen.fullScreen; set { Screen.fullScreen = value; Changed?.Invoke(); } }

        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 240 };

        private static float Get(string key, float fallback) => PlayerPrefs.GetFloat(P + key, fallback);

        private static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(P + key, value);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Aplica todo lo guardado (al arrancar y tras cada cambio).</summary>
        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (QualitySettings.GetQualityLevel() != Quality)
                QualitySettings.SetQualityLevel(Quality, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : FrameCap;
        }

        public static void ResetAll()
        {
            foreach (string key in new[] { "volumen", "efectos", "temblor", "paron", "numeros", "ayuda", "vsync", "fps", "calidad" })
                PlayerPrefs.DeleteKey(P + key);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }
    }
}
