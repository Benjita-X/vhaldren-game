using UnityEngine;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.World;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Ayuda legible dentro del juego, no solo comentarios de codigo.
    /// Se puede ocultar con F1 y volver a consultar en cualquier momento.
    /// No gestiona acciones ni interfiere con el Input System.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerControlsGuide : MonoBehaviour
    {
        [SerializeField] private bool expandedByDefault = true;
        private bool expanded;
        private GUIStyle textStyle;
        private VillageNoticeboard noticeboard;
        private float autoHideAt;

        private void Awake()
        {
            expanded = expandedByDefault && Vhaldren.Gameplay.Settings.GameSettings.ShowHelpOnStart;
            autoHideAt = Time.time + 16f;
        }

        private void Start()
        {
            noticeboard = FindAnyObjectByType<VillageNoticeboard>();
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

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            // La ayuda nunca debe tapar el comercio/dialogo del pueblo.
            if (noticeboard != null && noticeboard.MenuOpen)
                return;

            float width = Mathf.Min(365f, Mathf.Max(160f, Screen.width - 24f));
            float x = Mathf.Max(8f, Screen.width - width - 12f);
            GUI.Box(new Rect(x, 12f, width, 27f), "F1 — AYUDA Y CONTROLES");

            if (!expanded)
                return;

            if (textStyle == null)
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    wordWrap = true,
                    richText = true,
                    alignment = TextAnchor.UpperLeft
                };

            GUI.Box(new Rect(x, 43f, width, 448f), "");
            string guide = GuideText();
            GUI.Label(new Rect(x + 10f, 50f, width - 20f, 438f), guide, textStyle);
        }

        /// <summary>Ayuda con las teclas asignadas ahora mismo (cambian en Opciones).</summary>
        private static string GuideText()
        {
            string K(GameAction a) => "<b>" + GameInput.Label(a) + "</b>";
            string camera = "Rueda: zoom";
            if (GameSettings.CameraRotation)
                camera += " | Botón central o " + K(GameAction.CameraRotateLeft) + "/" + K(GameAction.CameraRotateRight) +
                          ": girar cámara | " + K(GameAction.CameraReset) + ": ángulo inicial";
            return "<b>MOVIMIENTO</b>\n" + K(GameAction.MoveForward) + K(GameAction.MoveLeft) + K(GameAction.MoveBack) + K(GameAction.MoveRight) +
                   ": mover | " + K(GameAction.Dash) + ": esquivar\nRatón: apuntar | Clic: ballesta\n" + camera + "\n\n" +
                   "<b>HABILIDADES</b>\n" + K(GameAction.Sword) + " espada | " + K(GameAction.RunePulse) + " pulso | " + K(GameAction.Frost) + " escarcha\n" +
                   K(GameAction.Shield) + " escudo | " + K(GameAction.Ward) + " barrera | " + K(GameAction.Parry) + " parada | " + K(GameAction.Potion) + " poción\n\n" +
                   "<b>MAGIA Y RAZA</b>\n" + K(GameAction.Spell1) + "/" + K(GameAction.Spell2) + "/" + K(GameAction.Spell3) + " hechizos | " + K(GameAction.NextSchool) + " escuela\n" +
                   K(GameAction.Absorb) + " (mantener) absorber esencia | " + K(GameAction.RacialPower) + " poder de raza\n" +
                   K(GameAction.Equipment) + " forja de equipo\n\n" +
                   "<b>SUPERVIVENCIA</b>\n" + K(GameAction.Gather) + " recolectar | " + K(GameAction.Build) + " construir | " + K(GameAction.CraftPick1) + "/" + K(GameAction.CraftPick2) + " picos\n" +
                   "Piezas: " + K(GameAction.BuildSlot1) + "…" + K(GameAction.BuildSlot10) + " | " + K(GameAction.Rotate) + " girar | " + K(GameAction.Dismantle) + " desmontar\n\n" +
                   "<b>PUEBLOS</b>\n" + K(GameAction.Noticeboard) + " tablón | " + K(GameAction.Accept) + " aceptar | " + K(GameAction.Offer) + " oferta | " + K(GameAction.Buy) + " comprar\n\n" +
                   K(GameAction.Pause) + ": menú (opciones, información) | " + K(GameAction.Help) + ": ocultar ayuda";
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
