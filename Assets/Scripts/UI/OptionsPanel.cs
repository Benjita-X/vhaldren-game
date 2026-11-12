using UnityEngine;
using Vhaldren.Gameplay.Settings;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Menu de opciones con pestañas: Audio, Graficos, Controles (reasignar teclas) y
    /// Juego. Lo usan el menu principal y el de pausa. Tick() debe llamarse en Update
    /// para capturar la tecla nueva al reasignar.
    /// </summary>
    public sealed class OptionsPanel
    {
        private static readonly string[] Tabs = { "Audio", "Gráficos", "Controles", "Juego" };
        private int tab;
        private Vector2 scroll;
        private bool waiting;
        private GameAction waitingFor;
        private int resolutionIndex = -1;

        public bool IsRebinding => waiting;

        /// <summary>Abre una pestaña (0 Audio, 1 Graficos, 2 Controles, 3 Juego).</summary>
        public void SelectTab(int index)
        {
            tab = Mathf.Clamp(index, 0, Tabs.Length - 1);
            scroll = Vector2.zero;
            waiting = false;
        }

        /// <summary>Captura la tecla pulsada mientras se reasigna (Esc cancela).</summary>
        public void Tick()
        {
#if ENABLE_INPUT_SYSTEM
            if (!waiting)
                return;
            Key key = GameInput.AnyKeyDown();
            if (key == Key.None)
                return;
            if (key != Key.Escape || waitingFor == GameAction.Pause)
                GameInput.Rebind(waitingFor, key);
            waiting = false;
#endif
        }

        /// <summary>Dibuja el panel. Devuelve true si el jugador pulso "Volver".</summary>
        public bool Draw(Rect area)
        {
            GUI.Box(area, GUIContent.none, MenuStyle.Panel);
            GUI.Label(new Rect(area.x + 24f, area.y + 14f, 400f, 40f), "OPCIONES", MenuStyle.Header);
            float tabWidth = 150f;
            for (int i = 0; i < Tabs.Length; i++)
                if (GUI.Button(new Rect(area.x + 24f + i * (tabWidth + 8f), area.y + 62f, tabWidth, 38f), Tabs[i],
                        i == tab ? MenuStyle.TabActive : MenuStyle.Tab))
                {
                    tab = i;
                    scroll = Vector2.zero;
                    waiting = false;
                }

            Rect content = new Rect(area.x + 24f, area.y + 116f, area.width - 48f, area.height - 190f);
            switch (tab)
            {
                case 0: DrawAudio(content); break;
                case 1: DrawGraphics(content); break;
                case 2: DrawControls(content); break;
                default: DrawGameplay(content); break;
            }

            if (GUI.Button(new Rect(area.x + 24f, area.yMax - 60f, 220f, 44f), "Restablecer", MenuStyle.Tab))
            {
                if (tab == 2)
                    GameInput.ResetDefaults();
                else
                    GameSettings.ResetAll();
            }
            return GUI.Button(new Rect(area.xMax - 244f, area.yMax - 60f, 220f, 44f), "Volver", MenuStyle.Button);
        }

        private static float Slider(Rect row, string label, float value)
        {
            GUI.Label(new Rect(row.x, row.y, 280f, row.height), label, MenuStyle.Body);
            float next = GUI.HorizontalSlider(new Rect(row.x + 290f, row.y + 12f, 320f, 20f), value, 0f, 1f);
            GUI.Label(new Rect(row.x + 630f, row.y, 80f, row.height), Mathf.RoundToInt(next * 100f) + " %", MenuStyle.Body);
            return next;
        }

        private static float RangeSlider(Rect row, string label, float value, float min, float max, string valueText)
        {
            GUI.Label(new Rect(row.x, row.y, 280f, row.height), label, MenuStyle.Body);
            float next = GUI.HorizontalSlider(new Rect(row.x + 290f, row.y + 12f, 320f, 20f), value, min, max);
            GUI.Label(new Rect(row.x + 630f, row.y, 120f, row.height), valueText, MenuStyle.Body);
            return next;
        }

        private static bool Toggle(Rect row, string label, bool value)
        {
            GUI.Label(new Rect(row.x, row.y, 380f, row.height), label, MenuStyle.Body);
            return GUI.Button(new Rect(row.x + 390f, row.y, 160f, row.height - 6f), value ? "Sí" : "No", MenuStyle.Tab) ? !value : value;
        }

        private static int Stepper(Rect row, string label, string valueText, int value, int min, int max)
        {
            GUI.Label(new Rect(row.x, row.y, 280f, row.height), label, MenuStyle.Body);
            if (GUI.Button(new Rect(row.x + 290f, row.y, 44f, row.height - 6f), "<", MenuStyle.Tab) && value > min)
                value--;
            GUI.Label(new Rect(row.x + 340f, row.y, 300f, row.height), valueText, MenuStyle.Body);
            if (GUI.Button(new Rect(row.x + 650f, row.y, 44f, row.height - 6f), ">", MenuStyle.Tab) && value < max)
                value++;
            return value;
        }

        private static void DrawAudio(Rect area)
        {
            float y = area.y;
            float master = Slider(new Rect(area.x, y, area.width, 40f), "Volumen general", GameSettings.MasterVolume);
            if (!Mathf.Approximately(master, GameSettings.MasterVolume)) GameSettings.MasterVolume = master;
            y += 50f;
            float effects = Slider(new Rect(area.x, y, area.width, 40f), "Efectos (golpes, magia, pasos)", GameSettings.EffectsVolume);
            if (!Mathf.Approximately(effects, GameSettings.EffectsVolume)) GameSettings.EffectsVolume = effects;
            y += 60f;
            GUI.Label(new Rect(area.x, y, area.width, 60f),
                "<i>La música llegará en una versión próxima. Los efectos están sintetizados por el propio juego.</i>", MenuStyle.Small);
        }

        private void DrawGraphics(Rect area)
        {
            float y = area.y;
            string[] names = QualitySettings.names;
            int quality = Stepper(new Rect(area.x, y, area.width, 40f), "Calidad gráfica",
                names.Length > 0 ? names[GameSettings.Quality] : "-", GameSettings.Quality, 0, Mathf.Max(0, names.Length - 1));
            if (quality != GameSettings.Quality) GameSettings.Quality = quality;
            y += 50f;

            Resolution[] resolutions = Screen.resolutions;
            if (resolutions.Length > 0)
            {
                if (resolutionIndex < 0 || resolutionIndex >= resolutions.Length)
                {
                    resolutionIndex = resolutions.Length - 1;
                    for (int i = 0; i < resolutions.Length; i++)
                        if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                            resolutionIndex = i;
                }
                Resolution r = resolutions[resolutionIndex];
                int next = Stepper(new Rect(area.x, y, area.width, 40f), "Resolución", r.width + " × " + r.height,
                    resolutionIndex, 0, resolutions.Length - 1);
                if (next != resolutionIndex)
                {
                    resolutionIndex = next;
                    Screen.SetResolution(resolutions[next].width, resolutions[next].height, Screen.fullScreenMode);
                }
                y += 50f;
            }

            bool full = Toggle(new Rect(area.x, y, area.width, 40f), "Pantalla completa", GameSettings.Fullscreen);
            if (full != GameSettings.Fullscreen) GameSettings.Fullscreen = full;
            y += 50f;
            bool vsync = Toggle(new Rect(area.x, y, area.width, 40f), "Sincronización vertical (VSync)", GameSettings.VSync);
            if (vsync != GameSettings.VSync) GameSettings.VSync = vsync;
            y += 50f;
            int capIndex = System.Array.IndexOf(GameSettings.FrameCaps, GameSettings.FrameCap);
            if (capIndex < 0) capIndex = 2;
            int nextCap = Stepper(new Rect(area.x, y, area.width, 40f), "Límite de FPS (sin VSync)",
                GameSettings.FrameCaps[capIndex] + " FPS", capIndex, 0, GameSettings.FrameCaps.Length - 1);
            if (nextCap != capIndex) GameSettings.FrameCap = GameSettings.FrameCaps[nextCap];
        }

        private void DrawControls(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 30f),
                waiting ? "<color=#f5c96b>Pulsa la nueva tecla para «" + LabelOf(waitingFor) + "» (Esc cancela)</color>"
                        : "Haz clic en una tecla para cambiarla. Si ya estaba en uso, se intercambian. Clic = ballesta, rueda = zoom, botón central (arrastrar) = girar cámara; Mayús libre y Ctrl+Z deshacer son atajos fijos.",
                MenuStyle.Small);
            Rect view = new Rect(area.x, area.y + 36f, area.width, area.height - 36f);
            float rowHeight = 38f;
            int groups = 0;
            string lastGroup = null;
            foreach (GameInput.Binding b in GameInput.All)
                if (b.Group != lastGroup) { groups++; lastGroup = b.Group; }
            float total = GameInput.All.Length * rowHeight + groups * 34f;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, view.width - 24f, total));
            float y = 0f;
            lastGroup = null;
            foreach (GameInput.Binding binding in GameInput.All)
            {
                if (binding.Group != lastGroup)
                {
                    lastGroup = binding.Group;
                    GUI.Label(new Rect(0f, y, 400f, 32f), "<b>" + binding.Group.ToUpperInvariant() + "</b>", MenuStyle.Body);
                    y += 34f;
                }
                GUI.Label(new Rect(16f, y, 420f, rowHeight), binding.Label, MenuStyle.Body);
                bool active = waiting && waitingFor == binding.Action;
                if (GUI.Button(new Rect(450f, y, 180f, rowHeight - 6f), active ? "…" : GameInput.Label(binding.Action),
                        active ? MenuStyle.TabActive : MenuStyle.KeyButton))
                {
                    waiting = true;
                    waitingFor = binding.Action;
                }
                y += rowHeight;
            }
            GUI.EndScrollView();
        }

        private static string LabelOf(GameAction action)
        {
            foreach (GameInput.Binding b in GameInput.All)
                if (b.Action == action)
                    return b.Label;
            return action.ToString();
        }

        private static void DrawGameplay(Rect area)
        {
            float y = area.y;
            float shake = Slider(new Rect(area.x, y, area.width, 40f), "Temblor de cámara", GameSettings.ShakeIntensity);
            if (!Mathf.Approximately(shake, GameSettings.ShakeIntensity)) GameSettings.ShakeIntensity = shake;
            y += 50f;
            bool stop = Toggle(new Rect(area.x, y, area.width, 40f), "Parón al golpear (hit stop)", GameSettings.HitStop);
            if (stop != GameSettings.HitStop) GameSettings.HitStop = stop;
            y += 50f;
            bool numbers = Toggle(new Rect(area.x, y, area.width, 40f), "Números de daño y curación", GameSettings.DamageNumbers);
            if (numbers != GameSettings.DamageNumbers) GameSettings.DamageNumbers = numbers;
            y += 50f;
            bool help = Toggle(new Rect(area.x, y, area.width, 40f), "Mostrar ayuda (F1) al empezar", GameSettings.ShowHelpOnStart);
            if (help != GameSettings.ShowHelpOnStart) GameSettings.ShowHelpOnStart = help;

            y += 58f;
            GUI.Label(new Rect(area.x, y, area.width, 30f), "<b>CÁMARA</b>", MenuStyle.Body);
            y += 36f;
            float distance = GameSettings.CameraDistance;
            float nextDistance = Mathf.Round(RangeSlider(new Rect(area.x, y, area.width, 40f), "Distancia de la cámara",
                distance, GameSettings.CameraDistanceMin, GameSettings.CameraDistanceMax, distance.ToString("0") + " m"));
            if (!Mathf.Approximately(nextDistance, distance)) GameSettings.CameraDistance = nextDistance;
            y += 50f;
            bool rotation = Toggle(new Rect(area.x, y, area.width, 40f), "Permitir girar la cámara", GameSettings.CameraRotation);
            if (rotation != GameSettings.CameraRotation) GameSettings.CameraRotation = rotation;
            GUI.Label(new Rect(area.x + 560f, y, Mathf.Max(120f, area.width - 560f), 40f),
                "<i>Botón central (arrastrar) o " + GameInput.Label(GameAction.CameraRotateLeft) + "/" +
                GameInput.Label(GameAction.CameraRotateRight) + "; " + GameInput.Label(GameAction.CameraReset) +
                ": ángulo inicial</i>", MenuStyle.Small);
            y += 50f;
            float sensitivity = GameSettings.CameraRotationSensitivity;
            float nextSensitivity = Mathf.Round(RangeSlider(new Rect(area.x, y, area.width, 40f), "Sensibilidad de giro",
                sensitivity, GameSettings.CameraSensitivityMin, GameSettings.CameraSensitivityMax,
                "× " + sensitivity.ToString("0.0")) * 10f) / 10f;
            if (!Mathf.Approximately(nextSensitivity, sensitivity)) GameSettings.CameraRotationSensitivity = nextSensitivity;
            y += 50f;
            bool ahead = Toggle(new Rect(area.x, y, area.width, 40f), "Adelantar cámara hacia el cursor", GameSettings.CameraLookAhead);
            if (ahead != GameSettings.CameraLookAhead) GameSettings.CameraLookAhead = ahead;
        }
    }
}
