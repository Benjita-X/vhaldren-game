using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Marco de pantalla: viñeta oscura en los bordes con un velo carmesí tenue arriba.
    /// Con menos del 30 % de vida se tiñe de rojo y late. El centro (≈60 %) queda limpio.
    /// Se dibuja detrás del resto del HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenVignette : MonoBehaviour
    {
        public const float LowHealthThreshold = 0.3f;
        public const float BaseEdgeAlpha = 0.5f;

        [SerializeField] private CombatHealth health;

        public void Bind(CombatHealth target) => health = target;

        /// <summary>0 con vida ≥ 30 %; sube hasta 1 cuando la vida llega a 0.</summary>
        public static float LowHealthFactor(float health01)
        {
            if (float.IsNaN(health01))
                return 0f;
            health01 = Mathf.Clamp01(health01);
            return health01 >= LowHealthThreshold ? 0f : 1f - health01 / LowHealthThreshold;
        }

        /// <summary>Opacidad de los bordes oscuros: base 0,5, más con poca vida.</summary>
        public static float EdgeAlpha(float health01) => BaseEdgeAlpha + 0.3f * LowHealthFactor(health01);

        /// <summary>Opacidad del rojo de peligro (late con <paramref name="pulse"/> 0..1).</summary>
        public static float DangerAlpha(float health01, float pulse) =>
            LowHealthFactor(health01) * (0.42f + 0.22f * Mathf.Clamp01(pulse));

        public float CurrentHealth01 =>
            health != null && health.MaximumHealth > 0f ? health.CurrentHealth / health.MaximumHealth : 1f;

        private void OnGUI()
        {
            GUI.depth = 20;   // detrás de todo el HUD
            if (Event.current.type != EventType.Repaint || HudMode.Hidden)
                return;
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            float h01 = health != null && health.IsDefeated ? 0f : CurrentHealth01;
            HudSkin.DrawVignette(screen, new Color(0f, 0f, 0f, EdgeAlpha(h01)));
            HudSkin.DrawTopGlow(new Rect(0f, 0f, Screen.width, Screen.height * 0.16f),
                new Color(HudSkin.Crimson.r, HudSkin.Crimson.g * 0.6f, HudSkin.Crimson.b, 0.2f));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.2f);
            float danger = DangerAlpha(h01, pulse);
            if (danger > 0.002f)
                HudSkin.DrawVignette(screen, new Color(0.78f, 0.02f, 0.05f, danger));
        }
    }

    /// <summary>Modo del HUD: normal, recuadros de depuración (F3) u oculto (capturas sin HUD).</summary>
    public static class HudMode
    {
        /// <summary>Recuadros antiguos con datos técnicos (F3, reasignable).</summary>
        public static bool Debug { get; set; }

        /// <summary>Oculta todo el HUD (lo usan herramientas de captura).</summary>
        public static bool Hidden { get; set; }
    }
}
