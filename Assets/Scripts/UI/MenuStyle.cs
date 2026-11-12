using UnityEngine;

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Estilo comun de los menus (fantasia oscura): paneles negros translucidos con marco
    /// fino dorado y esquinas (mismo arte que el HUD, ver <see cref="HudSkin"/>), titulos
    /// dorados y botones con borde y bisel. Todo se dibuja en un lienzo virtual de 900 px
    /// de alto que se escala a cualquier resolucion.
    /// </summary>
    public static class MenuStyle
    {
        public const float VirtualHeight = 900f;

        private static Texture2D panel, button, buttonHover, buttonActive, line;
        public static GUIStyle Title, Subtitle, Body, Small, SmallRight, Button, Tab, TabActive, Panel, Header, KeyButton;

        public static float Scale => Mathf.Max(0.5f, Screen.height / VirtualHeight);
        public static float Width => Screen.width / Scale;
        public static float Height => VirtualHeight;

        /// <summary>Activa el lienzo virtual. Llamar al principio de OnGUI.</summary>
        public static void Begin()
        {
            Ensure();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        }

        public static void End() => GUI.matrix = Matrix4x4.identity;

        /// <summary>Panel con fondo translúcido y el marco ornamentado del HUD.</summary>
        public static void FramedPanel(Rect rect)
        {
            Ensure();
            GUI.Box(rect, GUIContent.none, Panel);
            HudSkin.OrnateFrame(rect, 1f, false);
        }

        public static void Fill(Rect rect, Color color)
        {
            Ensure();
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, line);
            GUI.color = previous;
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void Ensure()
        {
            if (Title != null && panel != null)
                return;
            panel = Framed(new Color(0.035f, 0.03f, 0.045f, 0.9f), new Color(0.035f, 0.03f, 0.045f, 0.9f), new Color(0f, 0f, 0f, 0f));
            button = Framed(new Color(0.17f, 0.1f, 0.12f, 0.96f), new Color(0.09f, 0.055f, 0.07f, 0.96f), new Color(0.55f, 0.43f, 0.24f, 0.9f));
            buttonHover = Framed(new Color(0.42f, 0.13f, 0.15f, 0.98f), new Color(0.24f, 0.07f, 0.09f, 0.98f), new Color(0.93f, 0.78f, 0.45f, 1f));
            buttonActive = Framed(new Color(0.6f, 0.17f, 0.17f, 1f), new Color(0.38f, 0.1f, 0.1f, 1f), new Color(1f, 0.86f, 0.55f, 1f));
            line = Solid(Color.white);
            Color gold = new Color(0.93f, 0.78f, 0.45f);
            Color text = new Color(0.9f, 0.88f, 0.86f);

            Title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            Title.normal.textColor = gold;
            Subtitle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
            Subtitle.normal.textColor = new Color(0.75f, 0.68f, 0.62f);
            Header = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, richText = true };
            Header.normal.textColor = gold;
            Body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = true };
            Body.normal.textColor = text;
            Small = new GUIStyle(Body) { fontSize = 14 };
            SmallRight = new GUIStyle(Small) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 14), border = new RectOffset(6, 6, 6, 6) };
            Panel.normal.background = panel;
            Button = new GUIStyle(GUI.skin.button) { fontSize = 22, alignment = TextAnchor.MiddleCenter, richText = true, border = new RectOffset(6, 6, 6, 6) };
            Button.normal.background = button;
            Button.hover.background = buttonHover;
            Button.active.background = buttonActive;
            Button.normal.textColor = text;
            Button.hover.textColor = Color.white;
            Button.active.textColor = Color.white;
            Tab = new GUIStyle(Button) { fontSize = 17 };
            TabActive = new GUIStyle(Tab);
            TabActive.normal.background = buttonActive;
            TabActive.normal.textColor = gold;
            KeyButton = new GUIStyle(Button) { fontSize = 17 };
        }

        /// <summary>
        /// Textura de 9 cortes: degradado vertical (arriba más claro), borde fino de 1 px y
        /// bisel interior. Con border = 6 se estira sin deformar el marco.
        /// </summary>
        private static Texture2D Framed(Color top, Color bottom, Color edge)
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float v = y / (size - 1f);
                    Color c = Color.Lerp(bottom, top, v);
                    bool outer = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    bool inner = x == 2 || y == 2 || x == size - 3 || y == size - 3;
                    if (outer && edge.a > 0f)
                        c = edge;
                    else if (inner && edge.a > 0f && x > 1 && y > 1 && x < size - 2 && y < size - 2)
                        c = Color.Lerp(c, y >= size - 3 ? Color.white : Color.black, 0.12f);
                    pixels[y * size + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
