using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Menu de pausa de la version de escritorio. Esc pausa el juego y permite
    /// continuar, alternar pantalla completa o salir de la aplicacion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePauseMenu : MonoBehaviour
    {
        private bool paused;
        private GUIStyle title;

        public bool IsPaused => paused;

        private void Update()
        {
            if (EscapePressed())
                SetPaused(!paused);
        }

        public void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        private void OnDisable()
        {
            if (paused)
                SetPaused(false);
        }

        private void OnGUI()
        {
            if (!paused)
                return;
            if (title == null)
                title = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, richText = true };
            float width = 320f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.5f - 130f;
            GUI.Box(new Rect(x - 20f, y - 20f, width + 40f, 290f), GUIContent.none);
            GUI.Label(new Rect(x, y, width, 40f), "<b>VHALDREN — PAUSA</b>", title);
            if (GUI.Button(new Rect(x, y + 60f, width, 44f), "Continuar (Esc)"))
                SetPaused(false);
            if (GUI.Button(new Rect(x, y + 112f, width, 44f), Screen.fullScreen ? "Modo ventana" : "Pantalla completa"))
                Screen.fullScreen = !Screen.fullScreen;
            if (GUI.Button(new Rect(x, y + 164f, width, 44f), "Salir del juego"))
            {
                SetPaused(false);
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            GUI.Label(new Rect(x, y + 216f, width, 30f), "F1: controles");
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
