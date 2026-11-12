using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vhaldren.Gameplay.Races;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Menu principal de la aplicacion: Nueva partida (con eleccion de raza), Opciones,
    /// Informacion, Creditos y Salir. Fondo: escena del menu con el obelisco rúnico.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string WorldScene = "WorldPrototype001";

        private enum Screen { Main, Race, Options, Codex, Credits }

        [SerializeField] private Transform showcase;

        private readonly OptionsPanel options = new OptionsPanel();
        private readonly CodexPanel codex = new CodexPanel();
        private Screen screen = Screen.Main;
        private RaceId selectedRace;
        private Vector2 raceScroll;
        private string captureFolder;

        public string CurrentScreen => screen.ToString();

        private void Start()
        {
            GameSettings.Apply();
            Time.timeScale = 1f;
            selectedRace = RaceSelection.Current;
            captureFolder = GameAutoCapture.RequestedFolder();
            if (!string.IsNullOrEmpty(captureFolder))
                StartCoroutine(CaptureMenus());
        }

        private void Update()
        {
            options.Tick();
            if (showcase != null)
                showcase.Rotate(0f, 12f * Time.unscaledDeltaTime, 0f, Space.World);
            if (screen != Screen.Main && !options.IsRebinding && GameInput.Down(GameAction.Pause))
                screen = screen == Screen.Race || screen == Screen.Options || screen == Screen.Codex || screen == Screen.Credits
                    ? Screen.Main : screen;
        }

        public void StartGame(RaceId race)
        {
            RaceSelection.Current = race;
            SceneManager.LoadScene(WorldScene);
        }

        private void OnGUI()
        {
            MenuStyle.Begin();
            float w = MenuStyle.Width, h = MenuStyle.Height;
            MenuStyle.Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, screen == Screen.Main ? 0.25f : 0.55f));
            Rect panel = new Rect(w * 0.5f - Mathf.Min(560f, w * 0.47f), 70f, Mathf.Min(1120f, w * 0.94f), h - 120f);
            switch (screen)
            {
                case Screen.Main: DrawMain(w, h); break;
                case Screen.Race: DrawRaces(panel); break;
                case Screen.Options: if (options.Draw(panel)) screen = Screen.Main; break;
                case Screen.Codex: if (codex.Draw(panel)) screen = Screen.Main; break;
                case Screen.Credits: DrawCredits(panel); break;
            }
            MenuStyle.End();
        }

        private void DrawMain(float w, float h)
        {
            GUI.Label(new Rect(0f, 90f, w, 90f), "VHALDREN", MenuStyle.Title);
            GUI.Label(new Rect(w * 0.5f - 400f, 180f, 800f, 40f), "Sangre, esencia y magia en un reino que no perdona", MenuStyle.Subtitle);
            float bw = 340f, x = 90f, y = 300f;
            if (GUI.Button(new Rect(x, y, bw, 56f), "Nueva partida", MenuStyle.Button)) screen = Screen.Race;
            y += 68f;
            GUI.enabled = false;
            GUI.Button(new Rect(x, y, bw, 56f), "Continuar  <size=14>(guardado: próximamente)</size>", MenuStyle.Button);
            GUI.enabled = true;
            y += 68f;
            if (GUI.Button(new Rect(x, y, bw, 56f), "Opciones", MenuStyle.Button)) screen = Screen.Options;
            y += 68f;
            if (GUI.Button(new Rect(x, y, bw, 56f), "Información", MenuStyle.Button)) screen = Screen.Codex;
            y += 68f;
            if (GUI.Button(new Rect(x, y, bw, 56f), "Créditos", MenuStyle.Button)) screen = Screen.Credits;
            y += 68f;
            if (GUI.Button(new Rect(x, y, bw, 56f), "Salir", MenuStyle.Button)) Quit();
            GUI.Label(new Rect(16f, h - 34f, 600f, 30f), "Versión de prueba · rama 017 · " + Application.version, MenuStyle.Small);
        }

        private void DrawRaces(Rect area)
        {
            GUI.Box(area, GUIContent.none, MenuStyle.Panel);
            GUI.Label(new Rect(area.x + 24f, area.y + 14f, 600f, 40f), "ELIGE TU LINAJE", MenuStyle.Header);
            float listWidth = 300f;
            Rect list = new Rect(area.x + 24f, area.y + 66f, listWidth, area.height - 150f);
            raceScroll = GUI.BeginScrollView(list, raceScroll, new Rect(0f, 0f, listWidth - 20f, RaceCatalog.All.Count * 54f));
            for (int i = 0; i < RaceCatalog.All.Count; i++)
            {
                RaceDefinition race = RaceCatalog.All[i];
                if (GUI.Button(new Rect(0f, i * 54f, listWidth - 24f, 48f), race.Name,
                        race.Id == selectedRace ? MenuStyle.TabActive : MenuStyle.Tab))
                    selectedRace = race.Id;
            }
            GUI.EndScrollView();

            StringBuilder text = new StringBuilder(512);
            CodexPanel.AppendRace(text, RaceCatalog.Get(selectedRace));
            GUI.Label(new Rect(list.xMax + 24f, area.y + 60f, area.width - listWidth - 96f, area.height - 150f), text.ToString(), MenuStyle.Body);

            if (GUI.Button(new Rect(area.x + 24f, area.yMax - 60f, 220f, 44f), "Volver", MenuStyle.Tab))
                screen = Screen.Main;
            if (GUI.Button(new Rect(area.xMax - 304f, area.yMax - 60f, 280f, 44f),
                    "Comenzar como " + RaceCatalog.Get(selectedRace).Name, MenuStyle.Button))
                StartGame(selectedRace);
        }

        private void DrawCredits(Rect area)
        {
            GUI.Box(area, GUIContent.none, MenuStyle.Panel);
            GUI.Label(new Rect(area.x + 24f, area.y + 14f, 600f, 40f), "CRÉDITOS", MenuStyle.Header);
            GUI.Label(new Rect(area.x + 24f, area.y + 70f, area.width - 48f, area.height - 150f),
                "<b>Vhaldren</b> — juego original hecho con Unity 6.\n\n" +
                "<b>Personajes y enemigos</b>: KayKit Character Packs (Adventurers, Skeletons) de Kay Lousberg — CC0.\n" +
                "<b>Accesorios de mazmorra</b>: KayKit Dungeon Remastered — CC0.\n" +
                "<b>Texturas</b>: Poly Haven (Rob Tuytel, Amal Kumar) — CC0.\n" +
                "<b>Modelos propios</b>: generados con Blender 5.3 por script (Tools/Blender).\n" +
                "<b>Sonidos</b>: sintetizados por el propio juego.\n\n" +
                "Inspirado en el género de supervivencia y acción con vampiros; mecánicas, nombres y diseños propios.",
                MenuStyle.Body);
            if (GUI.Button(new Rect(area.xMax - 244f, area.yMax - 60f, 220f, 44f), "Volver", MenuStyle.Button))
                screen = Screen.Main;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Verificacion automatica del ejecutable: captura los menus y entra al mundo.</summary>
        private IEnumerator CaptureMenus()
        {
            Directory.CreateDirectory(captureFolder);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("0a-menu-principal.png");
            screen = Screen.Race;
            selectedRace = RaceId.Vampire;
            yield return new WaitForSeconds(0.4f);
            yield return Shot("0b-razas.png");
            screen = Screen.Options;
            yield return new WaitForSeconds(0.4f);
            yield return Shot("0c-opciones.png");
            options.SelectTab(3);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("0c2-opciones-juego-camara.png");
            options.SelectTab(0);
            screen = Screen.Codex;
            yield return new WaitForSeconds(0.4f);
            yield return Shot("0d-informacion.png");
            StartGame(RaceId.Vampire);
        }

        private static IEnumerator Shot(string file)
        {
            yield return new WaitForEndOfFrame();
            Texture2D image = new Texture2D(UnityEngine.Screen.width, UnityEngine.Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(GameAutoCapture.RequestedFolder(), file), image.EncodeToPNG());
            Destroy(image);
        }
    }
}
