using UnityEngine;
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
            expanded = expandedByDefault;
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

            GUI.Box(new Rect(x, 43f, width, 392f), "");
            string guide =
                "<b>MOVIMIENTO</b>\nWASD: mover | Espacio: esquivar\n" +
                "Ratón: apuntar | Clic izquierdo: ballesta\n\n" +
                "<b>HABILIDADES</b>\nV: espada | T: pulso rúnico | F: escarcha\n" +
                "Z: escudo físico | C: barrera mágica\n" +
                "X: parada precisa | Q: poción\n\n" +
                "<b>MAGIA Y ESENCIA</b>\n1/2/3: hechizos de la escuela | Tab: escuela\n" +
                "H (mantener): absorber esencia de un enemigo débil\n" +
                "Clic mientras absorbes: ejecutar (sin esencia)\n\n" +
                "<b>SUPERVIVENCIA</b>\nE: recolectar | J/K: fabricar picos\n" +
                "B: construir | 1–0: pieza | R: girar\n" +
                "G: desmontar | Ctrl+Z: deshacer\n\n" +
                "<b>PUEBLOS</b>\nN: abrir tablón | Y: aceptar/reclamar\n" +
                "O: cambiar oferta | P: comerciar\n\n" +
                "F1: ocultar o mostrar esta ayuda";
            GUI.Label(new Rect(x + 10f, 50f, width - 20f, 382f), guide, textStyle);
        }

        private static bool WasHelpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F1);
#else
            return false;
#endif
        }
    }
}
