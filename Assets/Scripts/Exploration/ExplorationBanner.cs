using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Titulos grandes de exploracion (nueva region, lugar secreto, montura domada): uno
    /// cada vez, en cola, con fundido de entrada y salida. Se dibujan bajo los avisos de
    /// <see cref="HudNotices"/>. Tiempo sin escalar.
    /// </summary>
    public static class ExplorationBanner
    {
        public const float FadeIn = 0.9f;
        public const float FadeOut = 1.3f;

        public struct Banner
        {
            public string Title;
            public string Subtitle;
            public Color Tint;
            public Color SubtitleTint;
            public bool Large;
            public float Hold;
        }

        private static readonly Queue<Banner> queue = new Queue<Banner>(4);
        private static Banner current;
        private static float currentStart = -100f;
        private static bool showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();

        public static bool Showing => showing;
        public static int Pending => queue.Count;
        public static string CurrentTitle => showing ? current.Title : null;

        public static void Show(string title, string subtitle, Color tint, bool large, float hold = 3f) =>
            Show(title, subtitle, tint, HudSkin.TextDim, large, hold);

        public static void Show(string title, string subtitle, Color tint, Color subtitleTint, bool large, float hold = 3f)
        {
            if (string.IsNullOrEmpty(title))
                return;
            Banner banner = new Banner
            {
                Title = title, Subtitle = subtitle, Tint = tint, SubtitleTint = subtitleTint,
                Large = large, Hold = Mathf.Clamp(hold, 0.5f, 10f)
            };
            // Una region nueva sustituye a otra region que aun esperaba en la cola.
            if (queue.Count >= 3)
                queue.Dequeue();
            queue.Enqueue(banner);
        }

        public static void Clear()
        {
            queue.Clear();
            showing = false;
            currentStart = -100f;
        }

        /// <summary>Opacidad de un titulo en el instante t desde su inicio.</summary>
        public static float AlphaAt(float t, float hold)
        {
            if (t < 0f)
                return 0f;
            if (t < FadeIn)
                return Mathf.SmoothStep(0f, 1f, t / FadeIn);
            if (t < FadeIn + hold)
                return 1f;
            return Mathf.SmoothStep(1f, 0f, (t - FadeIn - hold) / FadeOut);
        }

        private static void Advance(float now)
        {
            if (showing && now - currentStart > FadeIn + current.Hold + FadeOut)
                showing = false;
            if (!showing && queue.Count > 0)
            {
                current = queue.Dequeue();
                currentStart = now;
                showing = true;
            }
        }

        /// <summary>Dibuja el titulo actual (lienzo virtual de MenuStyle ya activo).</summary>
        public static void Draw(float width, float height)
        {
            float now = Time.unscaledTime;
            Advance(now);
            if (!showing)
                return;
            float t = now - currentStart;
            float alpha = AlphaAt(t, current.Hold);
            if (alpha <= 0.01f)
                return;
            // Sube un poco mientras aparece: entrada suave.
            float rise = (1f - Mathf.Clamp01(t / FadeIn)) * 10f;
            int size = current.Large ? 46 : 30;
            float y = Mathf.Round(height * 0.19f) + rise;
            string title = current.Title.ToUpperInvariant();
            float tw = Mathf.Max(HudSkin.TextWidth(title, size, true), HudSkin.TextWidth(current.Subtitle ?? "", 17)) + 160f;
            tw = Mathf.Min(tw, width * 0.9f);
            float bandHeight = current.Large ? 104f : 82f;
            Rect band = new Rect((width - tw) * 0.5f, y - 12f, tw, bandHeight);
            HudSkin.SoftPanel(band, 0.6f * alpha);
            Color gold = new Color(HudSkin.Gold.r, HudSkin.Gold.g, HudSkin.Gold.b, 0.85f * alpha);
            float lineWidth = Mathf.Min(tw - 60f, 520f);
            HudSkin.FadeLine(new Rect(width * 0.5f - lineWidth * 0.5f, y - 4f, lineWidth, 1.5f), gold);
            Color tint = current.Tint;
            tint.a = alpha;
            HudSkin.Label(new Rect(0f, y, width, size + 8f), title, size, tint, TextAnchor.MiddleCenter, true);
            float lineY = y + size + 12f;
            HudSkin.FadeLine(new Rect(width * 0.5f - lineWidth * 0.5f, lineY, lineWidth, 1.5f), gold);
            HudSkin.Diamond(new Vector2(width * 0.5f - lineWidth * 0.5f - 8f, lineY + 0.75f), 8f, gold);
            HudSkin.Diamond(new Vector2(width * 0.5f + lineWidth * 0.5f + 8f, lineY + 0.75f), 8f, gold);
            HudSkin.Diamond(new Vector2(width * 0.5f, lineY + 0.75f), 10f, gold);
            if (!string.IsNullOrEmpty(current.Subtitle))
            {
                Color sub = current.SubtitleTint;
                sub.a = alpha;
                HudSkin.Label(new Rect(0f, lineY + 6f, width, 24f), current.Subtitle, 17, sub);
            }
        }
    }
}
