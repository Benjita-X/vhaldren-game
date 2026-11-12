using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Settings
{
    /// <summary>
    /// Opciones del jugador, guardadas en PlayerPrefs y aplicadas al momento:
    /// audio (general y efectos), graficos (calidad, pantalla completa, sincronizacion,
    /// limite de FPS) y juego (temblor, paron de impacto, numeros de daño, ayuda al empezar
    /// y camara: distancia, giro, sensibilidad de giro y adelanto hacia el cursor).
    /// </summary>
    public static class GameSettings
    {
        private const string P = "vh.op.";

        /// <summary>Distancia de la camara del mundo al personaje, en metros (la rueda la ajusta en este rango).</summary>
        public const float CameraDistanceMin = 14f;
        public const float CameraDistanceMax = 30f;
        public const float CameraDistanceDefault = 22f;
        public const float CameraSensitivityMin = 0.2f;
        public const float CameraSensitivityMax = 3f;

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

        /// <summary>Distancia por defecto de la camara cenital, en metros (14-30, por defecto 22).</summary>
        public static float CameraDistance
        {
            get => ClampFinite(Get("camdistancia", CameraDistanceDefault), CameraDistanceMin, CameraDistanceMax, CameraDistanceDefault);
            set => Set("camdistancia", ClampFinite(value, CameraDistanceMin, CameraDistanceMax, CameraDistanceDefault));
        }
        /// <summary>Girar la camara con el boton central o las teclas de giro (por defecto, si).</summary>
        public static bool CameraRotation { get => Get("camgiro", 1f) > 0.5f; set => Set("camgiro", value ? 1f : 0f); }
        /// <summary>Multiplicador de la velocidad de giro de la camara (0,2-3, por defecto 1).</summary>
        public static float CameraRotationSensitivity
        {
            get => ClampFinite(Get("camsensibilidad", 1f), CameraSensitivityMin, CameraSensitivityMax, 1f);
            set => Set("camsensibilidad", ClampFinite(value, CameraSensitivityMin, CameraSensitivityMax, 1f));
        }
        /// <summary>Adelantar ligeramente el encuadre hacia el cursor (por defecto, no).</summary>
        public static bool CameraLookAhead { get => Get("camadelanto", 0f) > 0.5f; set => Set("camadelanto", value ? 1f : 0f); }

        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 240 };

        private static float Get(string key, float fallback) => PlayerPrefs.GetFloat(P + key, fallback);

        private static float ClampFinite(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

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
            foreach (string key in new[] { "volumen", "efectos", "temblor", "paron", "numeros", "ayuda", "vsync", "fps", "calidad",
                         "camdistancia", "camgiro", "camsensibilidad", "camadelanto" })
                PlayerPrefs.DeleteKey(P + key);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }
    }
}
