using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Piedra de viaje. E la activa (aviso, cristal encendido y luz) y pasa a ser punto de
    /// reaparicion; las activadas permiten viajar rapido desde el mapa (M). E en una ya
    /// activada abre el mapa. Sin modelo propio crea un pilar con un cristal flotante.
    /// </summary>
    public sealed class Waystone : ExplorationInteractable
    {
        private static readonly List<Waystone> all = new List<Waystone>(16);
        private static readonly Color DimCrystal = new Color(0.32f, 0.42f, 0.52f);
        private static readonly Color LitCrystal = new Color(0.45f, 0.92f, 1f);

        [SerializeField] private string idPiedra = "";
        [SerializeField] private string nombre = "Piedra de viaje";

        private bool activated;
        private bool initialised;
        private Transform crystal;
        private Renderer crystalRenderer;
        private Light glow;
        private float crystalBaseY;
        private float activatedAt = -10f;

        public static IReadOnlyList<Waystone> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        public string StoneId => !string.IsNullOrEmpty(idPiedra) ? idPiedra : ExplorationSave.PositionId("piedra", transform.position);
        public string DisplayName => string.IsNullOrEmpty(nombre) ? "Piedra de viaje" : nombre;
        public bool IsActivated => activated;

        /// <summary>Punto de llegada al viajar: delante de la piedra.</summary>
        public Vector3 TravelPoint
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.01f)
                    forward = Vector3.forward;
                return transform.position + forward.normalized * 2.6f;
            }
        }

        /// <summary>Lo llama el generador: id unico (para guardar) y nombre visible.</summary>
        public void Configure(string id, string nombre)
        {
            idPiedra = id ?? string.Empty;
            this.nombre = string.IsNullOrEmpty(nombre) ? "Piedra de viaje" : nombre;
        }

        public override string PromptVerb => activated ? "viajar desde " + DisplayName : "activar " + DisplayName;
        public override string PromptDetail => activated ? "piedra activada" : "piedra de viaje";
        public override Color PromptColor => LitCrystal;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!all.Contains(this))
                all.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            all.Remove(this);
        }

        private void Start() => Initialise();

        private void Initialise()
        {
            if (initialised)
                return;
            initialised = true;
            float top = 0f;
            if (!ExplorationProps.HasRenderers(gameObject))
            {
                Color stone = new Color(0.36f, 0.35f, 0.38f);
                ExplorationProps.Shape(PrimitiveType.Cylinder, "base", transform, new Vector3(0f, 0.15f, 0f), new Vector3(1.6f, 0.15f, 1.6f), stone * 0.8f);
                ExplorationProps.Shape(PrimitiveType.Cube, "pilar", transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.6f, 2.1f, 0.6f), stone, new Vector3(0f, 45f, 0f));
                ExplorationProps.Shape(PrimitiveType.Cube, "runa", transform, new Vector3(0f, 1.4f, 0.43f), new Vector3(0.2f, 0.6f, 0.02f), LitCrystal * 0.6f, default, true);
                top = 2.3f;
            }
            else
                top = ExplorationProps.RendererBounds(gameObject).max.y - transform.position.y;
            crystalBaseY = top + 0.7f;
            crystal = ExplorationProps.Shape(PrimitiveType.Cube, "cristal", transform, new Vector3(0f, crystalBaseY, 0f),
                new Vector3(0.32f, 0.5f, 0.32f), DimCrystal, new Vector3(0f, 0f, 0f), true);
            crystalRenderer = crystal.GetComponent<Renderer>();
            activated = ExplorationSave.Has(ExplorationSave.Waystones, StoneId);
            ApplyLook();
        }

        private void ApplyLook()
        {
            if (crystalRenderer != null)
                PrototypeVfx.SetColor(crystalRenderer, activated ? LitCrystal : DimCrystal);
            if (activated && glow == null && crystal != null)
            {
                glow = PrototypeVfx.CreatePointLight(crystal, LitCrystal, 9f, 1.5f);
                glow.renderMode = LightRenderMode.Auto;
            }
        }

        public override void Interact(GameObject who)
        {
            Initialise();
            if (activated)
            {
                ExplorationUi.MapOpenRequested = true;
                return;
            }
            Activate(true);
        }

        /// <summary>Activa la piedra (aviso, efecto y punto de reaparicion).</summary>
        public void Activate(bool announce)
        {
            Initialise();
            if (activated)
                return;
            activated = true;
            activatedAt = Time.time;
            ExplorationSave.Mark(ExplorationSave.Waystones, StoneId);
            ApplyLook();
            if (ExplorationContext.Session != null)
                ExplorationContext.Session.SetRespawnPoint(TravelPoint + Vector3.up * 1.1f);
            if (!announce)
                return;
            Vector3 at = crystal != null ? crystal.position : transform.position + Vector3.up * 2f;
            SpellImpactFlash.Spawn(transform.position + Vector3.up * 0.2f, LitCrystal, 3.2f, 0.7f, 3f);
            VfxHost host = VfxHost.Get();
            if (host != null)
            {
                host.Ring(VfxElement.Neutral, transform.position + Vector3.up * 0.2f, Vector3.up, 1.6f, LitCrystal, 36, 2.5f, 3f, 0.14f, 1f);
                host.Glints(at, 0.6f, LitCrystal, 24, 0.16f, 1.8f, Vector3.up * 1.5f);
            }
            ExplorationBanner.Show("Piedra de viaje activada", DisplayName + " · viaja aquí desde el mapa (" +
                Settings.GameInput.Label(Settings.GameAction.Map) + ")", LitCrystal, false, 2.6f);
            HudNotices.Post("Reaparecerás junto a " + DisplayName, HudNotices.Reward, 2.6f);
        }

        private void Update()
        {
            if (crystal == null)
                return;
            float t = Time.time;
            float pulse = activated ? 1f + Mathf.Clamp01(1f - (t - activatedAt)) * 0.6f : 1f;
            crystal.localPosition = new Vector3(0f, crystalBaseY + Mathf.Sin(t * 1.4f) * 0.12f, 0f);
            crystal.localRotation = Quaternion.Euler(0f, t * (activated ? 50f : 15f), 0f);
            crystal.localScale = new Vector3(0.32f, 0.5f, 0.32f) * pulse;
            if (glow != null)
                glow.intensity = 1.3f + Mathf.Sin(t * 2.2f) * 0.25f;
        }
    }
}
