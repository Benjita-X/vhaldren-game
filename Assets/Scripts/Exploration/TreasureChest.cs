using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Cofre del mundo. E (recolectar) lo abre: la tapa gira sobre su bisagra (hijo
    /// "tapa" o cualquier hijo cuyo nombre contenga "lid", como chest_lid de KayKit), da
    /// botin segun rareza (0 comun, 1 raro, 2 epico) y queda abierto para siempre
    /// (PlayerPrefs). Sin modelo propio, crea un cofre sencillo de primitivas.
    /// </summary>
    public sealed class TreasureChest : ExplorationInteractable
    {
        public const float OpenAngle = 105f;
        public const float OpenSeconds = 0.6f;

        [SerializeField, Range(0, 2)] private int rareza;
        [SerializeField] private string contenido = "";
        [Tooltip("Opcional: id fijo para guardar. Vacio = se deduce de la posicion.")]
        [SerializeField] private string idCofre = "";

        private bool opened;
        private bool initialised;
        private Transform lid;
        private Vector3 lidClosedPosition;
        private Quaternion lidClosedRotation;
        private Vector3 hinge;
        private float openStart = -10f;
        private float nextGlint;
        private Transform player;

        public int Rarity => rareza;
        public string Contents => contenido;
        public bool IsOpen => opened;
        public string ChestId => !string.IsNullOrEmpty(idCofre) ? idCofre : ExplorationSave.PositionId("cofre", transform.position);

        /// <summary>Lo llama el generador: rareza 0 comun, 1 raro, 2 epico; contenido extra opcional ("3 cristales").</summary>
        public void Configure(int rareza, string contenido)
        {
            this.rareza = Mathf.Clamp(rareza, 0, 2);
            this.contenido = contenido ?? string.Empty;
        }

        public void SetId(string id) => idCofre = id ?? string.Empty;

        public override bool CanInteract => base.CanInteract && !opened;
        public override string PromptVerb => "abrir el cofre";
        public override string PromptDetail => "cofre " + ExplorationRewards.RarityNames[Mathf.Clamp(rareza, 0, 2)];
        public override Color PromptColor => ExplorationRewards.RarityColor(rareza);

        private void Start() => Initialise();

        private void Initialise()
        {
            if (initialised)
                return;
            initialised = true;
            if (!ExplorationProps.HasRenderers(gameObject))
                BuildFallback();
            lid = QuadrupedFind("tapa") ?? ExplorationProps.FindContaining(transform, "lid");
            if (lid != null)
            {
                lidClosedPosition = lid.localPosition;
                lidClosedRotation = lid.localRotation;
                Bounds bounds = ExplorationProps.RendererBounds(lid.gameObject);
                Vector3 forward = transform.forward;
                float halfDepth = Mathf.Abs(bounds.extents.x * forward.x) + Mathf.Abs(bounds.extents.z * forward.z);
                hinge = bounds.center - forward * halfDepth;
            }
            opened = ExplorationSave.Has(ExplorationSave.Chests, ChestId);
            if (opened)
                PoseLid(1f);
        }

        private Transform QuadrupedFind(string childName) => Mounts.QuadrupedGait.FindDeep(transform, childName);

        private void BuildFallback()
        {
            Color wood = new Color(0.42f, 0.26f, 0.14f);
            Color band = rareza == 2 ? new Color(0.85f, 0.68f, 0.3f) : rareza == 1 ? new Color(0.45f, 0.55f, 0.7f) : new Color(0.3f, 0.3f, 0.32f);
            ExplorationProps.Shape(PrimitiveType.Cube, "base", transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.95f, 0.5f, 0.6f), wood);
            ExplorationProps.Shape(PrimitiveType.Cube, "banda", transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.99f, 0.08f, 0.64f), band);
            Transform pivot = ExplorationProps.Pivot("tapa", transform, new Vector3(0f, 0.5f, -0.3f));
            ExplorationProps.Shape(PrimitiveType.Cube, "tapa_madera", pivot, new Vector3(0f, 0.09f, 0.3f), new Vector3(0.97f, 0.18f, 0.62f), wood);
            ExplorationProps.Shape(PrimitiveType.Cube, "cierre", pivot, new Vector3(0f, 0.05f, 0.62f), new Vector3(0.14f, 0.16f, 0.05f), band);
        }

        public override void Interact(GameObject who)
        {
            Initialise();
            if (opened)
                return;
            opened = true;
            openStart = Time.time;
            ExplorationSave.Mark(ExplorationSave.Chests, ChestId);
            ExplorationRewards.GrantChest(who, rareza, contenido);
            Color color = ExplorationRewards.RarityColor(rareza);
            Vector3 top = transform.position + Vector3.up * 0.7f;
            SpellImpactFlash.Spawn(top, color, 1.6f + rareza * 0.6f, 0.45f, 0f);
            VfxHost host = VfxHost.Get();
            if (host != null)
            {
                host.Glints(top, 0.5f, color, 14 + rareza * 10, 0.16f, 1.4f, Vector3.up * 1.2f);
                host.Burst(VfxElement.Neutral, top, Vector3.up, color, 12 + rareza * 8, 3.5f, 0.1f, 0.8f, 0.5f);
            }
            if (GameFeel.Enabled)
                GameAudio.Play(GameSound.Metal, transform.position, 0.5f, 0.1f);
        }

        private void PoseLid(float open01)
        {
            if (lid == null)
                return;
            lid.localPosition = lidClosedPosition;
            lid.localRotation = lidClosedRotation;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(open01), 3f);
            lid.RotateAround(hinge, transform.right, -OpenAngle * eased);
        }

        private void Update()
        {
            if (!initialised)
                return;
            float t = Time.time - openStart;
            if (opened && t <= OpenSeconds + 0.05f)
                PoseLid(t / OpenSeconds);
            if (opened || Time.time < nextGlint)
                return;
            // Destellos de aviso en los cofres raros y epicos cuando el jugador esta cerca.
            nextGlint = Time.time + 1.6f;
            if (player == null)
                player = ExplorationContext.Player;
            if (rareza == 0 || player == null || (player.position - transform.position).sqrMagnitude > 28f * 28f)
                return;
            VfxHost host = VfxHost.Get();
            if (host != null)
                host.Glints(transform.position + Vector3.up * 0.75f, 0.45f, ExplorationRewards.RarityColor(rareza), 3, 0.12f, 1.3f, Vector3.up * 0.4f);
        }
    }
}
