using UnityEngine;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Piedra de saber: E abre un panel de lectura con su titulo y texto (historia del
    /// mundo). Se cierra con E, M o alejandose. Sin modelo propio crea una estela con runa.
    /// </summary>
    public sealed class LoreStone : ExplorationInteractable
    {
        [SerializeField] private string titulo = "Inscripción";
        [SerializeField, TextArea(3, 12)] private string texto = "";

        private bool initialised;
        private bool read;

        public string Title => string.IsNullOrEmpty(titulo) ? "Inscripción" : titulo;
        public string Text => texto ?? string.Empty;
        public bool HasBeenRead => read;
        public string LoreId => ExplorationSave.PositionId("saber", transform.position);

        /// <summary>Lo llama el generador: titulo y texto del panel de lectura.</summary>
        public void Configure(string titulo, string texto)
        {
            this.titulo = titulo ?? string.Empty;
            this.texto = texto ?? string.Empty;
        }

        public override string PromptVerb => "leer «" + Title + "»";
        public override string PromptDetail => read ? "ya leída" : "piedra de saber";
        public override Color PromptColor => new Color(0.85f, 0.8f, 1f);

        private void Start()
        {
            if (initialised)
                return;
            initialised = true;
            read = ExplorationSave.Has(ExplorationSave.Lore, LoreId);
            if (ExplorationProps.HasRenderers(gameObject))
                return;
            Color stone = new Color(0.3f, 0.3f, 0.33f);
            ExplorationProps.Shape(PrimitiveType.Cube, "estela", transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.75f, 1.7f, 0.22f), stone);
            ExplorationProps.Shape(PrimitiveType.Cube, "remate", transform, new Vector3(0f, 1.75f, 0f), new Vector3(0.85f, 0.12f, 0.3f), stone * 0.8f);
            ExplorationProps.Shape(PrimitiveType.Cube, "runa", transform, new Vector3(0f, 1.05f, 0.115f), new Vector3(0.3f, 0.5f, 0.01f),
                new Color(0.7f, 0.6f, 1f), default, true);
        }

        public override void Interact(GameObject who)
        {
            if (LoreReader.IsOpen && LoreReader.Source == this)
            {
                LoreReader.Close();
                return;
            }
            LoreReader.Open(this);
            if (!read)
            {
                read = true;
                ExplorationSave.Mark(ExplorationSave.Lore, LoreId);
            }
        }
    }

    /// <summary>Panel de lectura de las piedras de saber (lo dibuja <see cref="PlayerInteractor"/>).</summary>
    public static class LoreReader
    {
        private static LoreStone source;
        private static float openedAt;

        public static bool IsOpen => source != null;
        public static LoreStone Source => source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => source = null;

        public static void Open(LoreStone stone)
        {
            source = stone;
            openedAt = Time.unscaledTime;
            ExplorationUi.ReadingLore = stone != null;
        }

        public static void Close()
        {
            source = null;
            ExplorationUi.ReadingLore = false;
        }

        public static void Draw(float width, float height)
        {
            if (source == null)
                return;
            float alpha = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.25f);
            float w = Mathf.Min(680f, width * 0.86f);
            GUIStyle body = MenuStyle.Body;
            float textHeight = body != null ? body.CalcHeight(new GUIContent(source.Text), w - 72f) : 120f;
            float h = Mathf.Clamp(textHeight + 150f, 220f, height * 0.78f);
            Rect panel = new Rect((width - w) * 0.5f, height * 0.42f - h * 0.5f, w, h);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            MenuStyle.FramedPanel(panel);
            HudSkin.Label(new Rect(panel.x, panel.y + 18f, panel.width, 32f), source.Title, 24, HudSkin.GoldBright, TextAnchor.MiddleCenter, true);
            HudSkin.FadeLine(new Rect(panel.x + 60f, panel.y + 56f, panel.width - 120f, 1.5f), HudSkin.Gold);
            HudSkin.Diamond(new Vector2(panel.center.x, panel.y + 56.75f), 9f, HudSkin.Gold);
            GUI.Label(new Rect(panel.x + 36f, panel.y + 72f, panel.width - 72f, panel.height - 118f), source.Text, body);
            HudSkin.Label(new Rect(panel.x, panel.yMax - 36f, panel.width, 20f),
                GameInput.Label(GameAction.Gather) + ": cerrar", 14, HudSkin.TextDim);
            GUI.color = previous;
        }
    }
}
