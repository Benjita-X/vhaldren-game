using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Avisos breves arriba en el centro: habilidades, raza, forja, esencia y botín pasan
    /// por aquí. Como máximo <see cref="MaxVisible"/> a la vez, unos 2 s cada uno y con
    /// fundido. Un aviso repetido no se apila: renueva el que ya está. Usa tiempo sin
    /// escalar para que el parón de impacto no congele los avisos.
    /// </summary>
    public static class HudNotices
    {
        public const int MaxVisible = 2;
        public const float Duration = 2f;
        public const float FadeSeconds = 0.35f;

        public struct Notice
        {
            public string Text;
            public Color Tint;
            public float Start;
            public float Until;
        }

        private static readonly List<Notice> items = new List<Notice>(4);

        public static Color Neutral => HudSkin.Text;
        public static Color Warning => new Color(1f, 0.62f, 0.5f);
        public static Color Reward => new Color(0.98f, 0.84f, 0.48f);

        public static void Post(string text) => Post(text, Neutral);

        public static void Post(string text, Color tint, float duration = Duration) =>
            PostAt(text, tint, Time.unscaledTime, duration);

        /// <summary>Versión con reloj explícito (para pruebas).</summary>
        public static void PostAt(string text, Color tint, float now, float duration = Duration)
        {
            if (string.IsNullOrEmpty(text))
                return;
            Prune(now);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].Text != text)
                    continue;
                // El mismo aviso otra vez: se renueva y pasa a ser el más reciente.
                Notice same = items[i];
                items.RemoveAt(i);
                same.Until = now + Mathf.Max(0.2f, duration);
                same.Tint = tint;
                items.Add(same);
                return;
            }
            items.Add(new Notice { Text = text, Tint = tint, Start = now, Until = now + Mathf.Max(0.2f, duration) });
            while (items.Count > MaxVisible)
                items.RemoveAt(0);
        }

        public static int CountAt(float now)
        {
            Prune(now);
            return items.Count;
        }

        public static IReadOnlyList<Notice> ActiveAt(float now)
        {
            Prune(now);
            return items;
        }

        public static bool IsShowing(string text, float now)
        {
            Prune(now);
            for (int i = 0; i < items.Count; i++)
                if (items[i].Text == text)
                    return true;
            return false;
        }

        /// <summary>Opacidad: entra en 0,15 s y se funde en los últimos <see cref="FadeSeconds"/>.</summary>
        public static float AlphaOf(Notice notice, float now)
        {
            float fadeIn = Mathf.Clamp01((now - notice.Start) / 0.15f);
            float fadeOut = Mathf.Clamp01((notice.Until - now) / FadeSeconds);
            return Mathf.Min(fadeIn, fadeOut);
        }

        public static void Clear() => items.Clear();

        private static void Prune(float now)
        {
            for (int i = items.Count - 1; i >= 0; i--)
                if (now >= items[i].Until)
                    items.RemoveAt(i);
        }

        /// <summary>Dibuja los avisos (lienzo virtual ya activo). El más reciente, abajo.</summary>
        public static void Draw(float width, float top)
        {
            float now = Time.unscaledTime;
            Prune(now);
            for (int i = 0; i < items.Count; i++)
            {
                Notice n = items[i];
                float alpha = AlphaOf(n, now);
                if (alpha <= 0.01f)
                    continue;
                float y = top + i * 30f;
                float textWidth = Mathf.Min(width * 0.6f, HudSkin.TextWidth(n.Text, 18) + 90f);
                Rect band = new Rect((width - textWidth) * 0.5f, y, textWidth, 28f);
                HudSkin.SoftPanel(band, 0.55f * alpha);
                HudSkin.FadeLine(new Rect(band.x + 20f, band.yMax - 1f, band.width - 40f, 1f),
                    new Color(HudSkin.Gold.r, HudSkin.Gold.g, HudSkin.Gold.b, 0.55f * alpha));
                Color c = n.Tint;
                c.a = alpha;
                HudSkin.Label(band, n.Text, 18, c);
            }
        }
    }
}
