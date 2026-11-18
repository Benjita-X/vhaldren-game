using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Exploration;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Mounts
{
    /// <summary>
    /// Bestia salvaje que se puede domar. El generador la coloca con
    /// <see cref="Configure"/>; muestra el modelo de la montura con animacion de reposo y
    /// paseos cortos alrededor de su sitio. Al acercarse: "Mantén E: domar nombre"; tras
    /// unos segundos manteniendo la tecla queda domada para siempre (PlayerPrefs) con un
    /// titulo grande, y desde entonces se invoca con G. Si ya estaba domada no aparece.
    /// </summary>
    public sealed class MountSpawn : ExplorationInteractable
    {
        public const float TameSeconds = 3f;

        private static readonly List<MountSpawn> all = new List<MountSpawn>(8);

        [SerializeField] private string idMontura = "lobo_bruma";
        [SerializeField] private string nombre = "";
        [SerializeField] private bool vuela;
        [Tooltip("m/s. Terrestres: velocidad en tierra (12-16). Voladoras: 18 o mas = velocidad de vuelo.")]
        [SerializeField] private float velocidad;
        [SerializeField, Min(0f)] private float radioPaseo = 5f;

        private GameObject wild;
        private QuadrupedGait gait;
        private bool tamed;
        private bool initialised;
        private Vector3 home;
        private Vector3 wanderTarget;
        private bool walking;
        private float walkSpeed = 1.6f;
        private float nextWander;
        private float tameProgress;
        private float lastProgress;
        private float vanishStart = -1f;
        private Vector3 wildScale = Vector3.one;
        private Transform player;
        private float nextGlint;

        public static IReadOnlyList<MountSpawn> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        public string MountId => MountUnlocks.Sanitize(idMontura);
        public string DisplayName => string.IsNullOrEmpty(nombre) ? MountCatalog.Get(MountId).Name : nombre;
        public bool Flies => vuela;
        public bool IsTamed => tamed;
        public GameObject WildModel => wild;

        /// <summary>
        /// Lo llama el generador. velocidad (m/s): terrestres = en tierra (12-16); voladoras =
        /// de vuelo si es 18 o mas (22-28), si no, en tierra. 0 = la del catalogo.
        /// </summary>
        public void Configure(string mountId, string nombre, bool vuela, float velocidad)
        {
            idMontura = string.IsNullOrEmpty(mountId) ? "lobo_bruma" : mountId;
            this.nombre = nombre ?? string.Empty;
            this.vuela = vuela;
            this.velocidad = velocidad;
            Register();
        }

        private void Register() => MountCatalog.Register(MountId, nombre, vuela, velocidad);

        public override bool CanInteract => base.CanInteract && !tamed && wild != null && vanishStart < 0f;
        public override string PromptVerb => "domar " + DisplayName;
        public override string PromptDetail => vuela ? "montura voladora" : "montura";
        public override float HoldSeconds => TameSeconds;
        public override Color PromptColor => new Color(0.75f, 0.95f, 0.7f);
        public override Vector3 InteractPoint => wild != null ? wild.transform.position : transform.position;

        private void Awake()
        {
            Register();
            InteractRadius = Mathf.Max(InteractRadius, 3.6f);
        }

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
            home = transform.position;
            tamed = MountUnlocks.IsUnlocked(MountId);
            if (tamed)
                return;
            wild = MountModelFactory.Create(MountId, transform, out _);
            wild.transform.position = home;
            wild.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            wildScale = wild.transform.localScale;
            gait = wild.GetComponent<QuadrupedGait>();
            if (gait == null)
                gait = wild.AddComponent<QuadrupedGait>();
            nextWander = Time.time + Random.Range(2f, 6f);
        }

        public override void OnHoldProgress(float progress01)
        {
            tameProgress = progress01;
            if (gait == null)
                return;
            // Se resiste: se encabrita dos veces mientras lo calmas.
            if ((lastProgress < 0.3f && progress01 >= 0.3f) || (lastProgress < 0.65f && progress01 >= 0.65f))
                gait.TriggerAttack(QuadrupedGait.AttackStyle.Claw, 0.6f);
            lastProgress = progress01;
        }

        public override void Interact(GameObject who) => Tame();

        /// <summary>Doma la bestia: desbloqueo guardado, titulo grande y la bestia se une al jugador.</summary>
        public void Tame()
        {
            Initialise();
            if (tamed)
                return;
            tamed = true;
            MountUnlocks.Unlock(MountId);
            ExplorationSave.Mark(ExplorationSave.Tamed, MountId);
            ExplorationBanner.Show("¡Montura domada!", DisplayName + " · pulsa " + GameInput.Label(GameAction.Mount) +
                " para montar · mantén " + GameInput.Label(GameAction.Mount) + " para elegir", new Color(0.75f, 0.95f, 0.7f), true, 3.4f);
            HudNotices.Post(DisplayName + " se une a ti", HudNotices.Reward, 3f);
            Vector3 at = InteractPoint + Vector3.up;
            Color accent = MountCatalog.Get(MountId).AccentColor;
            SpellImpactFlash.Spawn(InteractPoint + Vector3.up * 0.1f, accent, 3f, 0.6f, 2.6f);
            VfxHost host = VfxHost.Get();
            if (host != null)
                host.Glints(at, 1f, accent, 30, 0.18f, 1.8f, Vector3.up * 1.4f);
            vanishStart = Time.time;
        }

        private void Update()
        {
            if (!initialised || wild == null)
                return;
            if (vanishStart >= 0f)
            {
                float t = (Time.time - vanishStart) / 1.2f;
                wild.transform.localScale = wildScale * Mathf.Lerp(1f, 0.05f, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f)
                {
                    Destroy(wild);
                    wild = null;
                }
                return;
            }
            if (player == null)
                player = ExplorationContext.Player;
            float distance = player != null ? Vector3.Distance(player.position, wild.transform.position) : 999f;
            // Lejos del jugador, sin animar ni moverse (ahorra CPU en el mapa grande).
            bool near = distance < 110f;
            if (gait != null && gait.enabled != near)
                gait.enabled = near;
            if (!near)
                return;
            if (distance < 7f || tameProgress > 0f)
            {
                walking = false;
                Face(player.position - wild.transform.position, 140f);
                if (tameProgress > 0f && Time.time >= nextGlint)
                {
                    nextGlint = Time.time + 0.35f;
                    VfxHost host = VfxHost.Get();
                    if (host != null)
                        host.Glints(wild.transform.position + Vector3.up * 1.2f, 0.7f, new Color(0.75f, 0.95f, 0.7f), 4, 0.12f, 1f, Vector3.up * 0.6f);
                }
                return;
            }
            Wander();
        }

        private void Wander()
        {
            if (!walking && Time.time >= nextWander && radioPaseo > 0.5f)
            {
                Vector2 offset = Random.insideUnitCircle * radioPaseo;
                wanderTarget = home + new Vector3(offset.x, 0f, offset.y);
                walking = true;
                walkSpeed = Random.value < 0.25f ? 3.4f : 1.5f;
            }
            if (!walking)
                return;
            Vector3 position = wild.transform.position;
            Vector3 to = wanderTarget - position;
            to.y = 0f;
            if (to.magnitude < 0.3f)
            {
                walking = false;
                nextWander = Time.time + Random.Range(4f, 9f);
                return;
            }
            Face(to, 120f);
            Vector3 forward = wild.transform.forward;
            float aligned = Mathf.Clamp01(Vector3.Dot(forward, to.normalized));
            position += forward * (walkSpeed * aligned * Time.deltaTime);
            if (GroundProbe.TryGroundBelow(position, wild.transform, 2f, out float ground))
                position.y = Mathf.Lerp(position.y, ground, 1f - Mathf.Exp(-12f * Time.deltaTime));
            wild.transform.position = position;
        }

        private void Face(Vector3 direction, float degreesPerSecond)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            wild.transform.rotation = Quaternion.RotateTowards(wild.transform.rotation,
                Quaternion.LookRotation(direction.normalized, Vector3.up), degreesPerSecond * Time.deltaTime);
        }
    }
}
