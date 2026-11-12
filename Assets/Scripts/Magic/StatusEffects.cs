using System;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>Estados que dejan las escuelas de magia. Cada escuela tiene uno propio.</summary>
    public enum StatusKind
    {
        Ignition = 0,   // Fuego: daño por segundo.
        Chill = 1,      // Escarcha: ralentiza por acumulacion; a 3 cargas congela.
        Static = 2,     // Tormenta: el siguiente golpe de hechizo descarga daño extra.
        Curse = 3,      // Sombras: recibe mas daño.
        Bleed = 4,      // Sangre: quien golpea al marcado se cura.
        Dazzle = 5,     // Luz: el afectado causa menos daño.
        Exposed = 6     // Absorbiendo esencia: el jugador queda vulnerable.
    }

    /// <summary>
    /// Estados alterados de cualquier personaje con <see cref="CombatHealth"/>.
    /// Se añade solo la primera vez que alguien recibe un estado. Las reglas son
    /// deterministas y no dependen de los FPS: todo se calcula con tiempos absolutos.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth), typeof(CombatDamageReceiver))]
    public sealed class StatusEffects : MonoBehaviour, IDamageTakenModifier
    {
        public const float IgnitionDps = 5f;
        public const float IgnitionSeconds = 4f;
        public const int ChillStacksToFreeze = 3;
        public const float ChillSeconds = 3f;
        public const float FreezeSeconds = 1.4f;
        public const float StaticBonusDamage = 18f;
        public const float StaticSeconds = 6f;
        public const float CurseDamageTaken = 1.25f;
        public const float CurseSeconds = 5f;
        public const float BleedLifeSteal = 0.35f;
        public const float BleedSeconds = 6f;
        public const float DazzleDamageDealt = 0.65f;
        public const float DazzleSeconds = 4f;
        public const float ExposedDamageTaken = 1.3f;

        private static readonly int KindCount = Enum.GetValues(typeof(StatusKind)).Length;
        private static readonly System.Collections.Generic.List<StatusEffects> all =
            new System.Collections.Generic.List<StatusEffects>();

        public static System.Collections.Generic.IReadOnlyList<StatusEffects> All => all;

        private readonly float[] until = new float[KindCount];
        private CombatHealth health;
        private CombatDamageReceiver receiver;
        private PrototypeEnemy enemy;
        private Vector3 ignitionSource;
        private Transform ignitionOwner;
        private int chillStacks;
        private float frozenUntil;
        private float nextIgnitionTick;

        public event Action<StatusKind> Applied;
        public int ChillStacks => Has(StatusKind.Chill) ? chillStacks : 0;
        public bool IsFrozen => Time.time < frozenUntil;

        /// <summary>Multiplicador de daño recibido (maldicion, vulnerabilidad).</summary>
        public float IncomingMultiplier =>
            (Has(StatusKind.Curse) ? CurseDamageTaken : 1f) *
            (Has(StatusKind.Exposed) ? ExposedDamageTaken : 1f);

        /// <summary>Multiplicador del daño que causa este personaje (deslumbrado).</summary>
        public float OutgoingMultiplier => Has(StatusKind.Dazzle) ? DazzleDamageDealt : 1f;

        public static StatusEffects For(Component target)
        {
            if (target == null)
                return null;
            CombatHealth body = target.GetComponentInParent<CombatHealth>();
            if (body == null)
                return null;
            StatusEffects effects = body.GetComponent<StatusEffects>();
            return effects != null ? effects : body.gameObject.AddComponent<StatusEffects>();
        }

        /// <summary>Lee multiplicadores sin crear el componente si no existe.</summary>
        public static float IncomingFor(Component target)
        {
            StatusEffects effects = target != null ? target.GetComponent<StatusEffects>() : null;
            return effects != null ? effects.IncomingMultiplier : 1f;
        }

        public static float OutgoingFor(Component target)
        {
            StatusEffects effects = target != null ? target.GetComponent<StatusEffects>() : null;
            return effects != null ? effects.OutgoingMultiplier : 1f;
        }

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            receiver = GetComponent<CombatDamageReceiver>();
            enemy = GetComponent<PrototypeEnemy>();
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            health.Defeated += ClearAll;
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            if (health != null)
                health.Defeated -= ClearAll;
        }

        public bool Has(StatusKind kind) => Time.time < until[(int)kind];

        public float Remaining(StatusKind kind) => Mathf.Max(0f, until[(int)kind] - Time.time);

        /// <summary>
        /// Aplica o renueva un estado. Para estados con daño diferido conserva
        /// el origen real del atacante, de modo que el daño futuro pase por el
        /// mismo receptor central que armas y hechizos.
        /// </summary>
        public bool Apply(StatusKind kind, float seconds = -1f) =>
            ApplyInternal(kind, transform.position, null, seconds);

        public bool ApplyFrom(StatusKind kind, Vector3 source, float seconds = -1f) =>
            ApplyInternal(kind, source, null, seconds);

        public bool ApplyFrom(StatusKind kind, Component source, float seconds = -1f) =>
            ApplyInternal(kind,
                source != null ? source.transform.position : transform.position,
                source != null ? source.transform : null, seconds);

        private bool ApplyInternal(StatusKind kind, Vector3 source,
            Transform sourceOwner, float seconds)
        {
            if (health == null || health.IsDefeated)
                return false;
            Vhaldren.Gameplay.Races.PlayerRace race = GetComponent<Vhaldren.Gameplay.Races.PlayerRace>();
            if (race != null && race.IsImmune(kind))
                return false;   // inmunidad de raza (p. ej. semidemonio e Ignicion)
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                seconds = DefaultSeconds(kind);
            int index = (int)kind;
            bool wasActive = Time.time < until[index];
            until[index] = Mathf.Max(until[index], Time.time + seconds);

            switch (kind)
            {
                case StatusKind.Ignition:
                    ignitionSource = IsFinite(source) ? source : transform.position;
                    ignitionOwner = sourceOwner;
                    if (!wasActive)
                        nextIgnitionTick = Time.time + 1f;
                    break;
                case StatusKind.Chill:
                    chillStacks = wasActive ? chillStacks + 1 : 1;
                    if (chillStacks >= ChillStacksToFreeze)
                    {
                        chillStacks = 0;
                        until[index] = 0f;
                        Freeze(FreezeSeconds);
                    }
                    else if (enemy != null)
                    {
                        enemy.ApplySlow(1f - 0.25f * chillStacks, seconds);
                    }
                    break;
            }
            Applied?.Invoke(kind);
            return true;
        }

        /// <summary>Congela: el enemigo no se mueve ni ataca durante el tiempo indicado.</summary>
        public void Freeze(float seconds)
        {
            if (health == null || health.IsDefeated)
                return;
            frozenUntil = Mathf.Max(frozenUntil, Time.time + Mathf.Clamp(seconds, 0f, 3f));
            if (enemy != null)
            {
                enemy.ApplySlow(0.25f, seconds);
                enemy.InterruptAttack(seconds);
            }
        }

        /// <summary>
        /// Descarga la estatica por el receptor central. Respeta resistencias,
        /// barreras, modificadores de raza/equipo y feedback de daño.
        /// </summary>
        public float ConsumeStatic(Vector3 source)
        {
            if (!Has(StatusKind.Static))
                return 0f;
            until[(int)StatusKind.Static] = 0f;
            if (receiver == null)
                receiver = GetComponent<CombatDamageReceiver>();
            return receiver != null
                ? receiver.Receive(StaticBonusDamage, CombatDamageKind.Magic,
                    IsFinite(source) ? source : transform.position).Applied
                : 0f;
        }

        public void Clear(StatusKind kind)
        {
            until[(int)kind] = 0f;
            if (kind == StatusKind.Chill)
                chillStacks = 0;
            if (kind == StatusKind.Ignition)
                ignitionOwner = null;
        }

        public void ClearAll()
        {
            for (int i = 0; i < until.Length; i++)
                until[i] = 0f;
            chillStacks = 0;
            frozenUntil = 0f;
            ignitionOwner = null;
        }

        /// <summary>Avanza la quemadura. Separado de Update para poder probarlo.</summary>
        public float TickIgnition(float now)
        {
            if (health == null || health.IsDefeated || now >= until[(int)StatusKind.Ignition] + 0.001f)
                return 0f;
            float dealt = 0f;
            while (now >= nextIgnitionTick && nextIgnitionTick <= until[(int)StatusKind.Ignition] + 0.001f)
            {
                nextIgnitionTick += 1f;
                if (receiver == null)
                    receiver = GetComponent<CombatDamageReceiver>();
                if (receiver == null)
                    break;
                float applied = receiver.Receive(IgnitionDps,
                    CombatDamageKind.DamageOverTime,
                    IsFinite(ignitionSource) ? ignitionSource : transform.position).Applied;
                dealt += applied;
                if (applied > 0f && ignitionOwner != null)
                    ignitionOwner.GetComponentInParent<PlayerDamageTracker>()?
                        .Record(applied, CombatDamageKind.DamageOverTime);
                if (health.IsDefeated)
                    break;
            }
            return dealt;
        }

        private void Update()
        {
            if (Has(StatusKind.Ignition))
                TickIgnition(Time.time);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

        public static float DefaultSeconds(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Ignition: return IgnitionSeconds;
                case StatusKind.Chill: return ChillSeconds;
                case StatusKind.Static: return StaticSeconds;
                case StatusKind.Curse: return CurseSeconds;
                case StatusKind.Bleed: return BleedSeconds;
                case StatusKind.Dazzle: return DazzleSeconds;
                default: return 1f;
            }
        }

        public static string DisplayName(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Ignition: return "Ignición";
                case StatusKind.Chill: return "Escarcha";
                case StatusKind.Static: return "Estática";
                case StatusKind.Curse: return "Maldición";
                case StatusKind.Bleed: return "Sangría";
                case StatusKind.Dazzle: return "Deslumbrado";
                case StatusKind.Exposed: return "Expuesto";
                default: return kind.ToString();
            }
        }
    }
}
