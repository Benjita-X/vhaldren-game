using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Progression;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Races
{
    /// <summary>
    /// Aplica la raza del jugador: vida, resistencia, velocidad, poder fisico y magico,
    /// resistencias, afinidad por escuela, inmunidades, efectos de dia y noche y su
    /// poder activo (R). El poder fisico y magico se aplica en el receptor de daño del
    /// enemigo, asi cubre espada, ballesta, hechizos y habilidades antiguas.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class PlayerRace : MonoBehaviour, IDamageTakenModifier
    {
        private static PlayerRace local;

        [SerializeField] private RaceId race = RaceId.Human;

        private readonly Collider[] nearby = new Collider[32];
        private readonly RaycastHit[] lineHits = new RaycastHit[16];
        private readonly System.Collections.Generic.HashSet<CombatDamageReceiver> burstHit =
            new System.Collections.Generic.HashSet<CombatDamageReceiver>();
        private CombatHealth health;
        private CombatDefense defense;
        private PlayerActionModifiers modifiers;
        private RaceDefinition data;
        private float baseHealth;
        private float baseStamina = 100f;
        private float baseRegen = 26f;
        private int speedToken;
        private float appliedSpeed = -1f;
        private float powerReadyAt;
        private float buffUntil;
        private float guardUntil;
        private float furyUntil;
        private string message = string.Empty;
        private float messageUntil;
        private bool announcedLineage;

        public static PlayerRace Local => local;
        public RaceDefinition Data => data ?? RaceCatalog.Get(race);
        public RaceId Race => race;
        public int PowersUsed { get; private set; }
        public float PowerCooldownRemaining => Mathf.Max(0f, powerReadyAt - Time.time);
        public string LastMessage => message;

        private static bool IsNight => DayNightCycle.Active != null && DayNightCycle.Active.IsNight;

        /// <summary>Multiplicador de daño fisico que hace el jugador (raza, noche, poderes).</summary>
        public float PhysicalMultiplier =>
            Data.Physical * (IsNight ? Data.NightDamageDealt : 1f) *
            (Time.time < furyUntil ? 1.35f : 1f) * (Time.time < buffUntil ? 1.2f : 1f);

        /// <summary>Multiplicador de daño magico que hace el jugador.</summary>
        public float SpellMultiplier =>
            Data.Spell * (IsNight && Data.Id == RaceId.Vampire ? Data.NightDamageDealt : 1f) *
            (Time.time < buffUntil ? 1.2f : 1f);

        public float IncomingMultiplier =>
            (IsNight || DayNightCycle.Active == null ? 1f : Data.DayDamageTaken) *
            (Time.time < guardUntil ? 0.75f : 1f);

        public bool IsImmune(StatusKind kind)
        {
            foreach (StatusKind immune in Data.Immunities)
                if (immune == kind)
                    return true;
            return false;
        }

        public static float AffinityFor(SpellSchool school) =>
            local != null ? local.Data.AffinityFor(school) : 1f;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            defense = GetComponent<CombatDefense>();
            modifiers = GetComponent<PlayerActionModifiers>();
            baseHealth = health.MaximumHealth;
            if (defense != null)
            {
                baseStamina = defense.MaximumStamina;
                baseRegen = defense.RegenerationPerSecond;
            }
        }

        private void OnEnable() => local = this;

        private void OnDisable()
        {
            if (local == this)
                local = null;
            if (modifiers != null && speedToken != 0)
                modifiers.RemoveModifier(speedToken);
            speedToken = 0;
            appliedSpeed = -1f;
        }

        private bool started;

        private void Start()
        {
            started = true;
            Apply(race);
        }

        /// <summary>Elige la raza; se aplica al empezar (o al momento si ya empezo).</summary>
        public void Configure(RaceId id)
        {
            race = id;
            data = RaceCatalog.Get(id);
            if (started)
                Apply(id);
        }

        /// <summary>Cambia de raza y aplica todas sus estadisticas.</summary>
        public void Apply(RaceId id)
        {
            race = id;
            data = RaceCatalog.Get(id);
            float maxHealth = baseHealth * data.Health;
            PlayerEquipment equipment = GetComponent<PlayerEquipment>();
            if (equipment != null)
                equipment.SetBaseMaxHealth(maxHealth, true);
            else
                health.SetMaximumHealth(maxHealth, true);
            if (defense != null)
                defense.ConfigureStamina(data.Stamina * baseStamina / 100f, baseRegen * data.StaminaRegen);
            GetComponent<CombatDamageReceiver>()?.ConfigureResistances(data.MeleeResist, data.ArrowResist, data.MagicResist);
            PlayerSpellbook book = GetComponent<PlayerSpellbook>();
            if (book != null)
                foreach (SpellSchool school in data.ExtraSchools)
                    book.Unlock(school);
            ApplyLineage(id, book);
            appliedSpeed = -1f;
            UpdateSpeed();
        }

        /// <summary>
        /// Lucifer: concede su escuela exclusiva (Luz caída, activa desde el inicio), le pone
        /// alas, tinte y halo. Cualquier otro linaje los pierde si los tenía.
        /// </summary>
        private void ApplyLineage(RaceId id, PlayerSpellbook book)
        {
            LuciferAvatar avatar = GetComponent<LuciferAvatar>();
            if (id == RaceId.Lucifer)
            {
                if (book != null)
                    book.GrantExclusive(SpellSchool.FallenLight, true);
                if (avatar == null)
                    gameObject.AddComponent<LuciferAvatar>();
                if (Application.isPlaying && !announcedLineage)
                {
                    announcedLineage = true;
                    Vhaldren.Gameplay.UI.HudNotices.Post("Lucifer despierta con las alas rotas · " +
                        GameInput.Label(GameAction.Spell1) + " Lanza del Lucero · " +
                        GameInput.Label(GameAction.Spell2) + " Salto de alas · " +
                        GameInput.Label(GameAction.Spell3) + " Caída del Lucero · " +
                        GameInput.Label(GameAction.RacialPower) + " Estallido de alas",
                        new Color(1f, 0.82f, 0.45f), 8f);
                }
            }
            else
            {
                if (book != null && book.HasExclusive(SpellSchool.FallenLight))
                    book.RevokeExclusive(SpellSchool.FallenLight);
                if (avatar != null)
                    Destroy(avatar);
            }
        }

        private void UpdateSpeed()
        {
            if (modifiers == null)
                modifiers = GetComponent<PlayerActionModifiers>();
            if (modifiers == null)
                return;
            float speed = Data.Speed * (Time.time < furyUntil ? 1.25f : 1f) *
                          (IsNight && Data.Id == RaceId.Werewolf ? 1.1f : 1f);
            if (Mathf.Approximately(speed, appliedSpeed))
                return;
            if (speedToken != 0)
                modifiers.RemoveModifier(speedToken);
            speedToken = modifiers.AddModifier(PlayerActionModifiers.LockedAction.None, speed);
            appliedSpeed = speed;
        }

        private void Update()
        {
            UpdateSpeed();
            if (Time.timeScale <= 0f || health.IsDefeated)
                return;
            PlayerBuilder builder = GetComponent<PlayerBuilder>();
            if (builder != null && builder.IsBuilding)
                return;   // en construccion R gira las piezas
            if (Vhaldren.Gameplay.NPC.NpcDialogue.IsOpen)
                return;   // hablando con un aldeano
            if (GameInput.Down(GameAction.RacialPower))
                TryUsePower();
        }

        /// <summary>Usa el poder de la raza si no esta recargando.</summary>
        public bool TryUsePower()
        {
            if (health == null || health.IsDefeated)
                return false;
            if (modifiers != null && !modifiers.AllowsAttack)
                return Say(Data.PowerName + ": cierra construcción o diálogo");
            if (Time.time < powerReadyAt)
                return Say(Data.PowerName + ": recarga " + PowerCooldownRemaining.ToString("0") + " s");
            powerReadyAt = Time.time + Data.PowerCooldown;
            PowersUsed++;
            Color tint = PowerColor();
            switch (Data.Power)
            {
                case RacialPower.Determination:
                    defense?.RestoreStamina(60f);
                    guardUntil = Time.time + 5f;
                    break;
                case RacialPower.ArcaneSurge:
                    GetComponent<PlayerSpellbook>()?.ResetCooldowns();
                    break;
                case RacialPower.CrimsonMist:
                {
                    PlayerSpellbook book = GetComponent<PlayerSpellbook>();
                    Vector3 aim = book != null ? book.AimPoint() : transform.position + transform.forward * 6f;
                    Vector3 delta = aim - transform.position;
                    delta.y = 0f;
                    delta = Vector3.ClampMagnitude(delta, 6f);
                    SpellImpactFlash.Spawn(transform.position + Vector3.up, tint, 0.9f, 0.35f, 1.2f);
                    CharacterController body = GetComponent<CharacterController>();
                    if (body != null && body.enabled)
                        body.Move(delta);
                    else
                        transform.position += delta;
                    Physics.SyncTransforms();
                    health.Heal(health.MaximumHealth * 0.1f);
                    break;
                }
                case RacialPower.LunarFury:
                    furyUntil = Time.time + 6f;
                    break;
                case RacialPower.BoneWill:
                {
                    ArcaneBarrier barrier = GetComponent<ArcaneBarrier>();
                    if (barrier == null)
                        barrier = gameObject.AddComponent<ArcaneBarrier>();
                    barrier.Raise(health.MaximumHealth * 0.3f, 5f, tint);
                    break;
                }
                case RacialPower.InfernalBurst:
                    Burst(4f, 35f, tint);
                    break;
                case RacialPower.Grace:
                    health.Heal(health.MaximumHealth * 0.3f);
                    GetComponent<StatusEffects>()?.ClearAll();
                    break;
                case RacialPower.Adaptation:
                    health.Heal(health.MaximumHealth * 0.12f);
                    buffUntil = Time.time + 5f;
                    break;
                case RacialPower.WingBurst:
                    // Lucifer: las alas se abren de golpe, empujan lejos y prenden.
                    FallenLightKit.WingBurstVfx(transform);
                    Burst(FallenLightKit.WingBurstRadius, FallenLightKit.WingBurstDamage, tint,
                        FallenLightKit.WingBurstForce, FallenLightKit.WingBurstStagger);
                    break;
            }
            SpellImpactFlash.Spawn(transform.position + Vector3.up * 0.2f, tint, 1.3f, 0.45f, 2.2f);
            if (GameFeel.Enabled)
                GameAudio.Play(GameSound.SpellCast, transform.position, 0.9f, 0.02f);
            GameFeel.Shake(0.08f, 0.15f);
            return Say(Data.PowerName + "!");
        }

        private void Burst(float radius, float damage, Color tint, float force = 1f, float stagger = 0f)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, nearby,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider[] scan = nearby;
            if (count >= nearby.Length)
            {
                scan = Physics.OverlapSphere(transform.position, radius,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = scan.Length;
            }
            burstHit.Clear();
            for (int i = 0; i < count; i++)
            {
                CombatDamageReceiver receiver = scan[i] != null ? scan[i].GetComponentInParent<CombatDamageReceiver>() : null;
                if (receiver == null || receiver.transform.IsChildOf(transform) || !burstHit.Add(receiver))
                    continue;
                if (!Unobstructed(receiver))
                    continue;
                CombatDamageResult result;
                using (CombatHitContext.Push(false, force))
                    result = receiver.Receive(damage, CombatDamageKind.Magic, transform.position);
                if (result.Applied > 0f)
                {
                    StatusEffects.For(receiver)?.ApplyFrom(StatusKind.Ignition, this);
                    GetComponent<PlayerDamageTracker>()?.Record(result.Applied, CombatDamageKind.Magic);
                    if (stagger > 0f)
                        receiver.GetComponent<PrototypeEnemy>()?.InterruptAttack(stagger);
                }
            }
        }

        private bool Unobstructed(CombatDamageReceiver target)
        {
            if (target == null)
                return false;
            Vector3 start = transform.position + Vector3.up * 0.55f;
            Collider targetCollider = target.GetComponent<Collider>();
            Vector3 end = targetCollider != null
                ? targetCollider.bounds.center
                : target.transform.position + Vector3.up * 0.55f;
            Vector3 ray = end - start;
            float distance = ray.magnitude;
            if (distance <= 0.01f)
                return true;

            Vector3 direction = ray / distance;
            int count = Physics.RaycastNonAlloc(start, direction, lineHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] complete = null;
            if (count >= lineHits.Length)
            {
                complete = Physics.RaycastAll(start, direction, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = complete.Length;
            }

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = complete != null ? complete[i] : lineHits[i];
                if (candidate.collider == null)
                    continue;
                Transform obstacle = candidate.collider.transform;
                if (obstacle.IsChildOf(transform) || obstacle.IsChildOf(target.transform))
                    continue;
                // Otros combatientes no bloquean la onda; el escenario sí.
                if (obstacle.GetComponentInParent<CombatDamageReceiver>() != null)
                    continue;
                return false;
            }
            return true;
        }

        /// <summary>Color del poder de raza (icono de la barra y avisos).</summary>
        public Color HudColor => PowerColor();

        private Color PowerColor()
        {
            switch (Data.Id)
            {
                case RaceId.Vampire: return new Color(0.8f, 0.05f, 0.15f);
                case RaceId.Werewolf: return new Color(0.85f, 0.65f, 0.3f);
                case RaceId.Undead: return new Color(0.75f, 0.85f, 0.7f);
                case RaceId.HalfDemon: return new Color(1f, 0.35f, 0.05f);
                case RaceId.HalfCelestial: return new Color(1f, 0.95f, 0.6f);
                case RaceId.Arcanist: return new Color(0.5f, 0.6f, 1f);
                case RaceId.Lucifer: return new Color(1f, 0.74f, 0.3f);
                default: return new Color(0.9f, 0.9f, 0.95f);
            }
        }

        private bool Say(string value)
        {
            message = value;
            messageUntil = Time.time + 2f;
            bool used = value.EndsWith("!");
            Vhaldren.Gameplay.UI.HudNotices.Post(value, used
                ? Color.Lerp(PowerColor(), Color.white, 0.45f)
                : Vhaldren.Gameplay.UI.HudNotices.Warning);
            return used;
        }

        /// <summary>Linea para la barra de hechizos: raza y estado del poder.</summary>
        public string HudText()
        {
            string state = PowerCooldownRemaining <= 0f ? "<color=#8f8>listo</color>" :
                "<color=#f99>" + PowerCooldownRemaining.ToString("0") + "s</color>";
            string text = Data.Name + " · " + GameInput.Label(GameAction.RacialPower) + ": " + Data.PowerName + " " + state;
            return Time.time < messageUntil ? text + "  " + message : text;
        }
    }
}
