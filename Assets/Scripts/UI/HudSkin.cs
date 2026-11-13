using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Arte del HUD generado por código (original de Vhaldren): viñeta, degradados,
    /// marcos finos con esquinas, orbe, arco de resistencia, glifos de los iconos y
    /// textos con contorno. Todo se dibuja con IMGUI en el lienzo virtual de
    /// <see cref="MenuStyle"/>, así que escala a cualquier resolución.
    /// </summary>
    public static class HudSkin
    {
        // Paleta: oro envejecido, carmesí profundo y negro azulado.
        public static readonly Color Gold = new Color(0.78f, 0.62f, 0.34f);
        public static readonly Color GoldBright = new Color(0.96f, 0.82f, 0.5f);
        public static readonly Color Crimson = new Color(0.62f, 0.07f, 0.11f);
        public static readonly Color Ink = new Color(0.02f, 0.015f, 0.03f);
        public static readonly Color Text = new Color(0.93f, 0.9f, 0.86f);
        public static readonly Color TextDim = new Color(0.72f, 0.68f, 0.64f);

        public enum Glyph { Crossbow, Sword, Pulse, Frost, Potion, Rune1, Rune2, Rune3, Fang, School }

        private static Texture2D white, vignette, topGlow, softPanel, fadeLine, corner, diamond, disc, ring,
            glass, iconBase, bevel, glow, arrow, dot;
        private static Texture2D[] cornerFlips;
        private static readonly Dictionary<Glyph, Texture2D> glyphs = new Dictionary<Glyph, Texture2D>();
        private static readonly Texture2D[] arcs = new Texture2D[ArcSteps + 1];
        private const int ArcSteps = 48;
        private static GUIStyle label, shadow;

        public static Texture2D White { get { Ensure(); return white; } }
        public static Texture2D Vignette { get { Ensure(); return vignette; } }
        public static Texture2D Disc { get { Ensure(); return disc; } }
        public static Texture2D Arrow { get { Ensure(); return arrow; } }
        public static Texture2D Dot { get { Ensure(); return dot; } }

        // ── Primitivas ─────────────────────────────────────────────────

        public static void Draw(Rect rect, Texture texture, Color color)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f)
                return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
        }

        public static void Fill(Rect rect, Color color)
        {
            Ensure();
            Draw(rect, white, color);
        }

        /// <summary>Textura recortada (u0,v0)-(u1,v1): sirve para llenar el orbe de abajo arriba.</summary>
        public static void DrawPart(Rect rect, Texture texture, Rect uv, Color color)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f)
                return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(rect, texture, uv);
            GUI.color = previous;
        }

        /// <summary>Viñeta: bordes oscuros que no tocan el centro (alpha 0 en el 55 % central).</summary>
        public static void DrawVignette(Rect screen, Color color)
        {
            Ensure();
            Draw(screen, vignette, color);
        }

        /// <summary>Velo superior: un degradado tenue que se desvanece hacia abajo.</summary>
        public static void DrawTopGlow(Rect rect, Color color)
        {
            Ensure();
            Draw(rect, topGlow, color);
        }

        /// <summary>Fondo suave detrás de barras y avisos: oscuro en el centro, sin bordes duros.</summary>
        public static void SoftPanel(Rect rect, float alpha)
        {
            Ensure();
            Draw(rect, softPanel, new Color(Ink.r, Ink.g, Ink.b, alpha));
        }

        /// <summary>Línea fina que se desvanece en los extremos.</summary>
        public static void FadeLine(Rect rect, Color color)
        {
            Ensure();
            Draw(rect, fadeLine, color);
        }

        /// <summary>
        /// Marco ornamentado y fino: doble línea (oro envejecido y carmesí), esquinas con
        /// rombo y un degradado oscuro detrás. Solo para la barra de acción y los paneles.
        /// </summary>
        public static void OrnateFrame(Rect r, float alpha = 1f, bool background = true)
        {
            Ensure();
            if (background)
            {
                Draw(r, softPanel, new Color(Ink.r, Ink.g, Ink.b, 0.62f * alpha));
            }
            Color gold = new Color(Gold.r, Gold.g, Gold.b, 0.85f * alpha);
            Color crimson = new Color(Crimson.r, Crimson.g, Crimson.b, 0.75f * alpha);
            const float t = 1.5f;
            // Líneas exteriores (oro) e interiores (carmesí), con los extremos desvanecidos.
            FadeLine(new Rect(r.x + 10f, r.y, r.width - 20f, t), gold);
            FadeLine(new Rect(r.x + 10f, r.yMax - t, r.width - 20f, t), gold);
            Draw(new Rect(r.x, r.y + 10f, t, r.height - 20f), white, gold);
            Draw(new Rect(r.xMax - t, r.y + 10f, t, r.height - 20f), white, gold);
            FadeLine(new Rect(r.x + 18f, r.y + 4f, r.width - 36f, 1f), crimson);
            FadeLine(new Rect(r.x + 18f, r.yMax - 5f, r.width - 36f, 1f), crimson);
            // Esquinas decorativas.
            const float c = 18f;
            Draw(new Rect(r.x - 3f, r.y - 3f, c, c), cornerFlips[0], gold);
            Draw(new Rect(r.xMax - c + 3f, r.y - 3f, c, c), cornerFlips[1], gold);
            Draw(new Rect(r.x - 3f, r.yMax - c + 3f, c, c), cornerFlips[2], gold);
            Draw(new Rect(r.xMax - c + 3f, r.yMax - c + 3f, c, c), cornerFlips[3], gold);
        }

        /// <summary>Rombo pequeño (separadores, escuela activa, adornos).</summary>
        public static void Diamond(Vector2 center, float size, Color color)
        {
            Ensure();
            Draw(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), diamond, color);
        }

        public static void Circle(Vector2 center, float diameter, Color color)
        {
            Ensure();
            Draw(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), dot, color);
        }

        // ── Iconos ─────────────────────────────────────────────────────

        /// <summary>
        /// Icono de habilidad: cuadro con el color de la escuela o arma, bisel, borde fino,
        /// glifo propio y, si está lista, un brillo en el borde. <paramref name="cooldown01"/>
        /// es la fracción de recarga que falta (1 = recién usada): el sombreado baja con ella.
        /// </summary>
        public static void Icon(Rect r, Color tint, Glyph glyph, float cooldown01, bool ready, bool usable, float glowPulse)
        {
            Ensure();
            Color baseColor = usable ? tint : Color.Lerp(tint, new Color(0.3f, 0.3f, 0.32f), 0.65f);
            if (ready && usable)
            {
                // Halo exterior suave en el color del icono.
                float a = 0.35f + 0.25f * glowPulse;
                Draw(new Rect(r.x - 7f, r.y - 7f, r.width + 14f, r.height + 14f), glow, new Color(tint.r, tint.g, tint.b, a));
            }
            Draw(r, iconBase, new Color(baseColor.r * 0.95f, baseColor.g * 0.95f, baseColor.b * 0.95f, 1f));
            Texture2D g = GlyphTexture(glyph);
            float pad = r.width * 0.16f;
            Draw(new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, r.height - pad * 2f), g,
                new Color(1f, 0.97f, 0.9f, usable ? 0.92f : 0.45f));
            Draw(r, bevel, Color.white);
            if (cooldown01 > 0.001f)
            {
                float h = r.height * Mathf.Clamp01(cooldown01);
                Fill(new Rect(r.x, r.yMax - h, r.width, h), new Color(0.02f, 0.01f, 0.03f, 0.72f));
                // Filo luminoso del sombreado.
                Fill(new Rect(r.x, r.yMax - h, r.width, 1f), new Color(tint.r, tint.g, tint.b, 0.8f));
            }
            Color border = ready && usable ? Color.Lerp(Gold, GoldBright, glowPulse) : new Color(Gold.r * 0.55f, Gold.g * 0.55f, Gold.b * 0.55f);
            Border(r, 1.25f, border);
        }

        public static void Border(Rect r, float t, Color color)
        {
            Ensure();
            Draw(new Rect(r.x, r.y, r.width, t), white, color);
            Draw(new Rect(r.x, r.yMax - t, r.width, t), white, color);
            Draw(new Rect(r.x, r.y, t, r.height), white, color);
            Draw(new Rect(r.xMax - t, r.y, t, r.height), white, color);
        }

        // ── Orbe de vida y arco de resistencia ─────────────────────────

        /// <summary>Orbe de cristal: fondo, líquido que baja con la vida, brillo y aro dorado.</summary>
        public static void Orb(Rect r, float fill01, Color liquid, float pulse)
        {
            Ensure();
            Draw(r, disc, new Color(0.04f, 0.02f, 0.03f, 0.92f));
            float f = Mathf.Clamp01(fill01);
            if (f > 0f)
            {
                Rect part = new Rect(r.x, r.y + r.height * (1f - f), r.width, r.height * f);
                Color c = Color.Lerp(liquid, liquid * 1.35f, pulse);
                c.a = 1f;
                DrawPart(part, glass, new Rect(0f, 0f, 1f, f), c);
                // Superficie del líquido.
                if (f < 0.995f)
                    FadeLine(new Rect(r.x + r.width * 0.12f, part.y, r.width * 0.76f, 1.5f), new Color(1f, 0.6f, 0.55f, 0.6f));
            }
            Draw(new Rect(r.x + r.width * 0.2f, r.y + r.height * 0.1f, r.width * 0.36f, r.height * 0.24f), dot, new Color(1f, 1f, 1f, 0.14f));
            Draw(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), ring, new Color(Gold.r, Gold.g, Gold.b, 0.95f));
        }

        /// <summary>Arco que rodea el orbe (300°, empieza abajo a la izquierda y sube en sentido horario).</summary>
        public static void Arc(Rect r, float fill01, Color color)
        {
            Ensure();
            int step = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fill01) * ArcSteps), 0, ArcSteps);
            Draw(r, ArcTexture(ArcSteps), new Color(0f, 0f, 0f, 0.55f));
            if (step > 0)
                Draw(r, ArcTexture(step), color);
        }

        // ── Texto ──────────────────────────────────────────────────────

        /// <summary>Texto sin caja: contorno oscuro suave para leerse sobre el mundo.</summary>
        public static void Label(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
        {
            if (string.IsNullOrEmpty(text))
                return;
            EnsureStyles();
            label.fontSize = size;
            label.alignment = anchor;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            shadow.fontSize = size;
            shadow.alignment = anchor;
            shadow.fontStyle = label.fontStyle;
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f * color.a);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, shadow);
            GUI.Label(new Rect(r.x - 1f, r.y + 1f, r.width, r.height), text, shadow);
            GUI.Label(new Rect(r.x + 1f, r.y - 1f, r.width, r.height), text, shadow);
            GUI.Label(new Rect(r.x, r.y + 1.5f, r.width, r.height), text, shadow);
            GUI.color = color;
            GUI.Label(r, text, label);
            GUI.color = previous;
        }

        public static float TextWidth(string text, int size, bool bold = false)
        {
            EnsureStyles();
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            return label.CalcSize(new GUIContent(text)).x;
        }

        // ── Generación ─────────────────────────────────────────────────

        private static void EnsureStyles()
        {
            if (label != null)
                return;
            label = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            label.normal.textColor = Color.white;
            shadow = new GUIStyle(label);
            shadow.normal.textColor = Color.white;
        }

        private static Texture2D New(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private static void Ensure()
        {
            if (white != null)
                return;
            white = New(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();

            // Viñeta elíptica: transparente en el centro, sube suave hacia los bordes.
            vignette = New(128, 128);
            Fill(vignette, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx * 0.92f + dy * dy * 1.08f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.38f, d));
                return new Color(1f, 1f, 1f, a);
            });

            topGlow = New(4, 64);
            Fill(topGlow, (u, v) => new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01((v - 0.0f)), 2.2f)));

            softPanel = New(64, 32);
            Fill(softPanel, (u, v) =>
            {
                float ex = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) / 0.18f);
                float ey = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / 0.35f);
                float a = Mathf.Clamp01(ex) * Mathf.Clamp01(ey) * Mathf.Lerp(1f, 0.82f, v);
                return new Color(1f, 1f, 1f, a);
            });

            fadeLine = New(64, 1);
            Fill(fadeLine, (u, v) => new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) / 0.25f)));

            corner = New(24, 24);
            Fill(corner, (u, v) =>
            {
                // Escuadra en L (arriba-izquierda) con un rombo en el vértice.
                float x = u * 24f, y = (1f - v) * 24f;
                float a = 0f;
                if ((y >= 3f && y <= 4.6f && x >= 3f && x <= 22f) || (x >= 3f && x <= 4.6f && y >= 3f && y <= 22f))
                    a = 1f;
                if ((y >= 7f && y <= 8f && x >= 7f && x <= 14f) || (x >= 7f && x <= 8f && y >= 7f && y <= 14f))
                    a = Mathf.Max(a, 0.7f);
                float dd = Mathf.Abs(x - 4f) + Mathf.Abs(y - 4f);
                if (dd <= 3.4f)
                    a = 1f;
                return new Color(1f, 1f, 1f, a);
            });
            cornerFlips = new[] { corner, Flip(corner, true, false), Flip(corner, false, true), Flip(corner, true, true) };

            diamond = New(32, 32);
            Fill(diamond, (u, v) =>
            {
                float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01((0.5f - d) * 24f));
            });

            disc = New(128, 128);
            Fill(disc, (u, v) => new Color(1f, 1f, 1f, Edge(Radius(u, v), 0.5f)));
            dot = New(32, 32);
            Fill(dot, (u, v) => new Color(1f, 1f, 1f, Edge(Radius(u, v), 0.5f, 0.08f)));

            ring = New(128, 128);
            Fill(ring, (u, v) =>
            {
                float r = Radius(u, v);
                float a = Edge(r, 0.5f) * (1f - Edge(r, 0.465f));
                return new Color(1f, 1f, 1f, a);
            });

            // Líquido del orbe: degradado vertical y sombra lateral para dar volumen.
            glass = New(128, 128);
            Fill(glass, (u, v) =>
            {
                float r = Radius(u, v);
                float side = 1f - Mathf.Pow(Mathf.Clamp01(r / 0.5f), 3f) * 0.45f;
                float shade = Mathf.Lerp(0.55f, 1.05f, v) * side;
                return new Color(shade, shade, shade, Edge(r, 0.47f));
            });

            iconBase = New(64, 64);
            Fill(iconBase, (u, v) =>
            {
                float shade = Mathf.Lerp(0.38f, 1f, Mathf.Pow(v, 0.8f));
                float dx = u - 0.5f, dy = v - 0.62f;
                shade += 0.12f * Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.4f);
                return new Color(shade, shade, shade, 1f);
            });

            bevel = New(64, 64);
            Fill(bevel, (u, v) =>
            {
                float x = u * 64f, y = (1f - v) * 64f;
                if (x < 2.5f || y < 2.5f)
                    return new Color(1f, 1f, 1f, 0.28f);
                if (x > 61.5f || y > 61.5f)
                    return new Color(0f, 0f, 0f, 0.45f);
                if (x < 4f || y < 4f)
                    return new Color(1f, 1f, 1f, 0.08f);
                return new Color(0f, 0f, 0f, 0f);
            });

            glow = New(64, 64);
            Fill(glow, (u, v) =>
            {
                float x = Mathf.Abs(u - 0.5f) * 2f, y = Mathf.Abs(v - 0.5f) * 2f;
                float d = Mathf.Max(x, y);
                float a = Mathf.Clamp01((1f - d) / 0.2f) * Mathf.Clamp01((d - 0.62f) / 0.2f);
                return new Color(1f, 1f, 1f, a * a);
            });

            // Flecha de aviso (apunta hacia la derecha; se gira al dibujarla).
            arrow = New(32, 32);
            Fill(arrow, (u, v) =>
            {
                float x = u, y = Mathf.Abs(v - 0.5f) * 2f;
                float inside = x > 0.18f && y < (0.95f - x) * 1.25f ? 1f : 0f;
                float notch = x < 0.42f && y < (0.42f - x) * 1.4f ? 0f : 1f;
                return new Color(1f, 1f, 1f, inside * notch);
            });
        }

        private static Texture2D ArcTexture(int step)
        {
            if (arcs[step] != null)
                return arcs[step];
            Texture2D t = New(96, 96);
            float sweep = 300f * step / ArcSteps;
            Fill(t, (u, v) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Edge(r, 0.5f) * (1f - Edge(r, 0.44f));
                if (a <= 0f)
                    return new Color(1f, 1f, 1f, 0f);
                // Ángulo medido desde abajo-izquierda (210°) en sentido horario.
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // -180..180, 0 = derecha, 90 = arriba
                float fromStart = Mathf.Repeat(240f - ang, 360f);
                return new Color(1f, 1f, 1f, fromStart <= sweep ? a : 0f);
            });
            arcs[step] = t;
            return t;
        }

        private static Texture2D GlyphTexture(Glyph g)
        {
            Ensure();
            if (glyphs.TryGetValue(g, out Texture2D t) && t != null)
                return t;
            t = New(64, 64);
            System.Func<float, float, float> shape = GlyphShape(g);
            Fill(t, (u, v) => new Color(1f, 1f, 1f, Mathf.Clamp01(shape(u, 1f - v))));
            glyphs[g] = t;
            return t;
        }

        /// <summary>Glifos geométricos propios (sin iconos de terceros): x,y en 0..1, y hacia abajo.</summary>
        private static System.Func<float, float, float> GlyphShape(Glyph g)
        {
            switch (g)
            {
                case Glyph.Crossbow:
                    return (x, y) => Mathf.Max(Mathf.Max(
                        Segment(x, y, 0.5f, 0.15f, 0.5f, 0.9f, 0.055f),                       // culata
                        ArcStroke(x, y, 0.5f, 0.72f, 0.42f, 200f, 340f, 0.05f)),              // arco
                        Segment(x, y, 0.14f, 0.5f, 0.86f, 0.5f, 0.02f));                      // cuerda
                case Glyph.Sword:
                    return (x, y) => Mathf.Max(Mathf.Max(
                        Segment(x, y, 0.25f, 0.75f, 0.82f, 0.18f, 0.06f),
                        Segment(x, y, 0.18f, 0.58f, 0.42f, 0.82f, 0.05f)),
                        Segment(x, y, 0.12f, 0.88f, 0.26f, 0.74f, 0.07f));
                case Glyph.Pulse:
                    return (x, y) => Mathf.Max(Mathf.Max(
                        RingStroke(x, y, 0.5f, 0.5f, 0.36f, 0.045f),
                        RingStroke(x, y, 0.5f, 0.5f, 0.2f, 0.04f)),
                        DiscShape(x, y, 0.5f, 0.5f, 0.07f));
                case Glyph.Frost:
                    return (x, y) => Mathf.Max(Mathf.Max(
                        Segment(x, y, 0.5f, 0.12f, 0.5f, 0.88f, 0.045f),
                        Segment(x, y, 0.17f, 0.31f, 0.83f, 0.69f, 0.045f)),
                        Mathf.Max(Segment(x, y, 0.17f, 0.69f, 0.83f, 0.31f, 0.045f), DiscShape(x, y, 0.5f, 0.5f, 0.09f)));
                case Glyph.Potion:
                    return (x, y) => Mathf.Max(Mathf.Max(
                        DiscShape(x, y, 0.5f, 0.62f, 0.27f),
                        Segment(x, y, 0.5f, 0.14f, 0.5f, 0.38f, 0.085f)),
                        Segment(x, y, 0.36f, 0.14f, 0.64f, 0.14f, 0.04f));
                case Glyph.Rune1:
                    return (x, y) => Mathf.Max(DiamondStroke(x, y, 0.5f, 0.5f, 0.36f, 0.05f), DiscShape(x, y, 0.5f, 0.5f, 0.08f));
                case Glyph.Rune2:
                    return (x, y) => Mathf.Max(DiamondStroke(x, y, 0.5f, 0.5f, 0.38f, 0.045f), DiamondStroke(x, y, 0.5f, 0.5f, 0.18f, 0.045f));
                case Glyph.Rune3:
                    return (x, y) =>
                    {
                        float a = DiscShape(x, y, 0.5f, 0.5f, 0.1f);
                        for (int i = 0; i < 8; i++)
                        {
                            float ang = i * Mathf.PI / 4f;
                            float len = i % 2 == 0 ? 0.4f : 0.26f;
                            a = Mathf.Max(a, Segment(x, y, 0.5f, 0.5f, 0.5f + Mathf.Cos(ang) * len, 0.5f + Mathf.Sin(ang) * len, 0.035f));
                        }
                        return a;
                    };
                case Glyph.Fang:
                    return (x, y) => Mathf.Clamp01(DiscShape(x, y, 0.5f, 0.5f, 0.36f) - DiscShape(x, y, 0.64f, 0.4f, 0.3f));
                default:
                    return (x, y) => DiamondStroke(x, y, 0.5f, 0.5f, 0.3f, 0.07f);
            }
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by, float w)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float h = Mathf.Clamp01((px * dx + py * dy) / Mathf.Max(1e-5f, dx * dx + dy * dy));
            float ex = px - dx * h, ey = py - dy * h;
            return Mathf.Clamp01((w - Mathf.Sqrt(ex * ex + ey * ey)) * 64f + 0.5f);
        }

        private static float RingStroke(float x, float y, float cx, float cy, float r, float w)
        {
            float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);
            return Mathf.Clamp01((w - d) * 64f + 0.5f);
        }

        private static float ArcStroke(float x, float y, float cx, float cy, float r, float fromDeg, float toDeg, float w)
        {
            float ang = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg;
            ang = Mathf.Repeat(ang, 360f);
            if (ang < fromDeg || ang > toDeg)
                return 0f;
            return RingStroke(x, y, cx, cy, r, w);
        }

        private static float DiscShape(float x, float y, float cx, float cy, float r)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01((r - d) * 64f + 0.5f);
        }

        private static float DiamondStroke(float x, float y, float cx, float cy, float r, float w)
        {
            float d = Mathf.Abs(Mathf.Abs(x - cx) + Mathf.Abs(y - cy) - r);
            return Mathf.Clamp01((w - d) * 64f + 0.5f);
        }

        private static float Radius(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Edge(float r, float radius, float soft = 0.012f) =>
            Mathf.Clamp01((radius - r) / soft);

        private static void Fill(Texture2D texture, System.Func<float, float, Color> f)
        {
            int w = texture.width, h = texture.height;
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            texture.SetPixels(pixels);
            texture.Apply();
        }

        private static Texture2D Flip(Texture2D source, bool horizontal, bool vertical)
        {
            int w = source.width, h = source.height;
            Texture2D t = New(w, h);
            Color[] src = source.GetPixels();
            Color[] dst = new Color[src.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    dst[y * w + x] = src[(vertical ? h - 1 - y : y) * w + (horizontal ? w - 1 - x : x)];
            t.SetPixels(dst);
            t.Apply();
            return t;
        }
    }
}
