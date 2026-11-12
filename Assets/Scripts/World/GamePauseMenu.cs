using UnityEngine;
using UnityEngine.SceneManagement;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Menu de pausa (Esc): continuar, opciones (audio, graficos, controles, juego),
    /// informacion del juego, volver al menu principal o salir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePauseMenu : MonoBehaviour
    {
        public const string MenuScene = "MainMenu";

        private enum Page { Main, Options, Codex }

        private readonly OptionsPanel options = new OptionsPanel();
        private readonly CodexPanel codex = new CodexPanel();
        private bool paused;
        private Page page;

        public bool IsPaused => paused;

        private void Update()
        {
            options.Tick();
            if (options.IsRebinding || !GameInput.Down(GameAction.Pause))
                return;
            if (paused && page != Page.Main)
                page = Page.Main;
            else
                SetPaused(!paused);
        }

        public void SetPaused(bool value)
        {
            paused = value;
            page = Page.Main;
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
            MenuStyle.Begin();
            float w = MenuStyle.Width, h = MenuStyle.Height;
            MenuStyle.Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.6f));
            Rect panel = new Rect(w * 0.5f - Mathf.Min(560f, w * 0.47f), 60f, Mathf.Min(1120f, w * 0.94f), h - 110f);
            if (page == Page.Options)
            {
                if (options.Draw(panel))
                    page = Page.Main;
            }
            else if (page == Page.Codex)
            {
                if (codex.Draw(panel))
                    page = Page.Main;
            }
            else
            {
                float bw = 360f, x = (w - bw) * 0.5f, y = h * 0.5f - 200f;
                GUI.Label(new Rect(0f, y - 90f, w, 70f), "PAUSA", MenuStyle.Title);
                // Mismo marco fino que el HUD y los paneles de menú.
                HudSkin.OrnateFrame(new Rect(x - 34f, y - 26f, bw + 68f, 5f * 64f + 42f));
                if (GUI.Button(new Rect(x, y, bw, 54f), "Continuar", MenuStyle.Button)) SetPaused(false);
                y += 64f;
                if (GUI.Button(new Rect(x, y, bw, 54f), "Opciones", MenuStyle.Button)) page = Page.Options;
                y += 64f;
                if (GUI.Button(new Rect(x, y, bw, 54f), "Información", MenuStyle.Button)) page = Page.Codex;
                y += 64f;
                if (GUI.Button(new Rect(x, y, bw, 54f), "Menú principal", MenuStyle.Button))
                {
                    SetPaused(false);
                    if (Application.CanStreamedLevelBeLoaded(MenuScene))
                        SceneManager.LoadScene(MenuScene);
                }
                y += 64f;
                if (GUI.Button(new Rect(x, y, bw, 54f), "Salir del juego", MenuStyle.Button))
                {
                    SetPaused(false);
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
            }
            MenuStyle.End();
        }
    }
}
