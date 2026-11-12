using UnityEngine;

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Estilo comun de los menus (fantasia oscura): paneles negros translucidos, titulos
    /// dorados y botones con borde. Todo se dibuja en un lienzo virtual de 900 px de alto
    /// que se escala a cualquier resolucion.
    /// </summary>
    public static class MenuStyle
    {
        public const float VirtualHeight = 900f;

        private static Texture2D panel, button, buttonHover, buttonActive, line;
        public static GUIStyle Title, Subtitle, Body, Small, Button, Tab, TabActive, Panel, Header, KeyButton;

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
            panel = Solid(new Color(0.04f, 0.035f, 0.05f, 0.9f));
            button = Solid(new Color(0.16f, 0.1f, 0.12f, 0.95f));
            buttonHover = Solid(new Color(0.36f, 0.12f, 0.14f, 0.98f));
            buttonActive = Solid(new Color(0.55f, 0.16f, 0.16f, 1f));
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
            Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 14) };
            Panel.normal.background = panel;
            Button = new GUIStyle(GUI.skin.button) { fontSize = 22, alignment = TextAnchor.MiddleCenter, richText = true };
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
    }
}
