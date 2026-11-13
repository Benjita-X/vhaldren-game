using UnityEngine;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.UI;
using Vhaldren.Gameplay.World;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Ayuda de controles dentro del juego. Oculta por defecto (opción «ayuda al empezar»):
    /// al entrar solo aparece el aviso breve «F1: controles». F1 la abre y la cierra.
    /// Panel compacto en dos columnas con el marco del HUD y las teclas asignadas ahora.
    /// No gestiona acciones ni interfiere con el Input System.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerControlsGuide : MonoBehaviour
    {
        [SerializeField] private bool expandedByDefault = true;
        private bool expanded;
        private VillageNoticeboard noticeboard;
        private float autoHideAt;
        private bool hintShown;

        public bool Expanded => expanded;

        private void Awake()
        {
            expanded = expandedByDefault && GameSettings.ShowHelpOnStart;
            autoHideAt = Time.time + 16f;
        }

        private void Start()
        {
            noticeboard = FindAnyObjectByType<VillageNoticeboard>();
            if (!expanded && !hintShown)
            {
                hintShown = true;
                HudNotices.Post(GameInput.Label(GameAction.Help) + ": controles", HudSkin.GoldBright, 6f);
            }
        }

        private void Update()
        {
            if (WasHelpPressed())
            {
                expanded = !expanded;
                autoHideAt = float.PositiveInfinity;
            }
            else if (expanded && Time.time > autoHideAt)
                expanded = false;
        }

        /// <summary>Abre o cierra la ayuda (capturas y pruebas).</summary>
        public void SetExpanded(bool value)
        {
            expanded = value;
            autoHideAt = float.PositiveInfinity;
        }

        private void OnGUI()
        {
            if (!expanded || Event.current.type != EventType.Repaint || HudMode.Hidden)
                return;
            // La ayuda nunca debe tapar el comercio/dialogo del pueblo.
            if (noticeboard != null && noticeboard.MenuOpen)
                return;

            MenuStyle.Begin();
            float w = MenuStyle.Width;
            Rect panel = new Rect(w - 560f, 80f, 538f, 318f);
            MenuStyle.FramedPanel(panel);
            HudSkin.Label(new Rect(panel.x + 20f, panel.y + 10f, 300f, 24f), "CONTROLES", 18, HudSkin.GoldBright, TextAnchor.MiddleLeft, true);
            HudSkin.Label(new Rect(panel.xMax - 220f, panel.y + 10f, 200f, 24f), GameInput.Label(GameAction.Help) + ": cerrar", 13, HudSkin.TextDim, TextAnchor.MiddleRight);
            float colW = (panel.width - 52f) * 0.5f;
            GUI.Label(new Rect(panel.x + 20f, panel.y + 42f, colW, panel.height - 50f), LeftColumn(), MenuStyle.Small);
            GUI.Label(new Rect(panel.x + 32f + colW, panel.y + 42f, colW, panel.height - 50f), RightColumn(), MenuStyle.Small);
            MenuStyle.End();
        }

        private static string K(GameAction a) => "<color=#f2d38a><b>" + GameInput.Label(a) + "</b></color>";

        private static string LeftColumn()
        {
            string camera = "Rueda: zoom";
            if (GameSettings.CameraRotation)
                camera += "\nBotón central, " + K(GameAction.CameraRotateLeft) + "/" + K(GameAction.CameraRotateRight) +
                          ": girar · " + K(GameAction.CameraReset) + ": inicial";
            return "<b>MOVIMIENTO</b>\n" +
                   K(GameAction.MoveForward) + K(GameAction.MoveLeft) + K(GameAction.MoveBack) + K(GameAction.MoveRight) + " mover · " +
                   K(GameAction.Dash) + " esquivar\nRatón apuntar · Clic ballesta\n" + camera + "\n\n" +
                   "<b>COMBATE</b>\n" + K(GameAction.Sword) + " espada · " + K(GameAction.RunePulse) + " pulso · " + K(GameAction.Frost) + " escarcha\n" +
                   K(GameAction.Shield) + " escudo · " + K(GameAction.Ward) + " barrera · " + K(GameAction.Parry) + " parada\n" +
                   K(GameAction.Potion) + " poción\n\n" +
                   "<b>MAGIA Y RAZA</b>\n" + K(GameAction.Spell1) + K(GameAction.Spell2) + K(GameAction.Spell3) + " hechizos · " +
                   K(GameAction.NextSchool) + " escuela\n" + K(GameAction.Absorb) + " (mantener) absorber esencia\n" +
                   K(GameAction.RacialPower) + " poder de raza";
        }

        private static string RightColumn()
        {
            return "<b>SUPERVIVENCIA</b>\n" + K(GameAction.Gather) + " recolectar · " + K(GameAction.Build) + " construir\n" +
                   K(GameAction.CraftPick1) + "/" + K(GameAction.CraftPick2) + " picos · " + K(GameAction.Equipment) + " forja\n" +
                   "Piezas " + K(GameAction.BuildSlot1) + "…" + K(GameAction.BuildSlot10) + " · " + K(GameAction.Rotate) + " girar · " +
                   K(GameAction.Dismantle) + " desmontar\n\n" +
                   "<b>PUEBLOS</b>\n" + K(GameAction.Noticeboard) + " tablón · " + K(GameAction.Accept) + " aceptar\n" +
                   K(GameAction.Offer) + " oferta · " + K(GameAction.Buy) + " comprar\n\n" +
                   "<b>INTERFAZ</b>\n" + K(GameAction.Pause) + " menú (opciones e información)\n" +
                   K(GameAction.Help) + " esta ayuda · " + K(GameAction.DebugHud) + " datos técnicos";
        }

        private static bool WasHelpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.Help);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F1);
#else
            return false;
#endif
        }
    }
}
