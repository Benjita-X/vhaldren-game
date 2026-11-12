using UnityEngine;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Tres respuestas distintas: escudo contra armas fisicas, barrera runica
    /// contra magia y parada de precision contra golpes de frente. La esquiva
    /// ya existente protege contra ambos tipos durante el dash activo.
    /// No modifica PlayerMotor, inputs ni salud.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class CombatDefense : MonoBehaviour
    {
        public enum GuardMode { None, PhysicalShield, ArcaneWard }

        public readonly struct DefenseResult
        {
            public readonly float Multiplier;
            public readonly bool Parried;
            public readonly bool Evaded;

            public DefenseResult(float multiplier, bool parried, bool evaded)
            {
                Multiplier = multiplier;
                Parried = parried;
                Evaded = evaded;
            }
        }

        [SerializeField, Min(1f)] private float maximumStamina = 100f;
        [SerializeField, Min(0f)] private float regenerationPerSecond = 26f;
        [SerializeField, Min(0f)] private float shieldDrainPerSecond = 15f;
        [SerializeField, Min(0f)] private float wardDrainPerSecond = 23f;
        [SerializeField, Range(0f, 1f)] private float shieldMeleeMultiplier = 0.22f;
        [SerializeField, Range(0f, 1f)] private float shieldArrowMultiplier = 0.48f;
        [SerializeField, Range(0f, 1f)] private float wardMagicMultiplier = 0.20f;
        [SerializeField, Range(0f, 1f)] private float wardExplosionMultiplier = 0.38f;
        [SerializeField, Min(0.05f)] private float parryWindow = 0.20f;
        [SerializeField, Min(0.1f)] private float parryCooldown = 1.5f;
        [SerializeField, Min(0f)] private float parryCost = 28f;
        [SerializeField, Range(-1f, 1f)] private float requiredFacingDot = 0.05f;

        private CombatHealth health;
        private PlayerMotor motor;
        private float stamina;
        private float parryUntil;
        private float nextParryAt;
        private GuardMode guard;
        private bool initialized;
        private bool guardBroken;
        private float counterReadyUntil;
        private bool counterPending;

        public GuardMode Guard => guard;
        public float Stamina => stamina;
        public float MaximumStamina => Mathf.Max(1f, maximumStamina);
        public float RegenerationPerSecond => Mathf.Max(0f, regenerationPerSecond);

        /// <summary>Resistencia maxima y recuperacion (razas). Conserva la proporcion actual.</summary>
        public void ConfigureStamina(float maximum, float regenPerSecond)
        {
            if (float.IsNaN(maximum) || maximum < 1f || float.IsNaN(regenPerSecond) || regenPerSecond < 0f)
                return;
            float ratio = stamina / MaximumStamina;
            maximumStamina = maximum;
            regenerationPerSecond = regenPerSecond;
            stamina = Mathf.Clamp(ratio * maximumStamina, 0f, maximumStamina);
        }

        /// <summary>Recupera resistencia al instante (poderes de raza).</summary>
        public float RestoreStamina(float amount)
        {
            float before = stamina;
            stamina = Mathf.Min(MaximumStamina, stamina + Mathf.Max(0f, amount));
            return stamina - before;
        }
        public bool ParryWindowActive => Time.time < parryUntil;
        public bool CounterReady => counterPending && Time.time < counterReadyUntil;
        public bool GuardBroken => guardBroken;
        public bool CanParry => !guardBroken &&
            (health == null || !health.IsDefeated) &&
            Time.time >= nextParryAt && stamina >= parryCost;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            motor = GetComponent<PlayerMotor>();
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            stamina = MaximumStamina;
            initialized = true;
        }

        private void Update()
        {
            Initialize();
            if (health != null && health.IsDefeated)
            {
                guard = GuardMode.None;
                parryUntil = 0f;
                return;
            }
            if (guard == GuardMode.None)
            {
                stamina = Mathf.MoveTowards(stamina, MaximumStamina,
                    Mathf.Max(0f, regenerationPerSecond) * Time.deltaTime);
                if (guardBroken && stamina >= MaximumStamina * 0.25f)
                    guardBroken = false;
                return;
            }

            float drain = guard == GuardMode.PhysicalShield
                ? shieldDrainPerSecond : wardDrainPerSecond;
            stamina = Mathf.Max(0f, stamina - Mathf.Max(0f, drain) * Time.deltaTime);
            if (stamina <= 0f)
            {
                guard = GuardMode.None;
                guardBroken = true;
                parryUntil = 0f;
            }
        }

        public bool SetGuard(GuardMode requested)
        {
            Initialize();
            if (health != null && health.IsDefeated)
            {
                guard = GuardMode.None;
                return false;
            }

            if (requested == GuardMode.None)
            {
                guard = GuardMode.None;
                return true;
            }

            if (guardBroken || stamina < 1f || ParryWindowActive)
                return false;
            guard = requested;
            return true;
        }

        /// <summary>Las habilidades gastan la misma resistencia que la defensa.</summary>
        /// <summary>Una parada bien hecha habilita un solo tajo de respuesta.</summary>
        public bool TryConsumeCounter()
        {
            if (!CounterReady)
                return false;
            counterPending = false;
            return true;
        }

        public bool TrySpend(float amount)
        {
            Initialize();
            if (health != null && health.IsDefeated)
                return false;
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f ||
                stamina < amount)
                return false;
            stamina -= amount;
            return true;
        }

        public bool TryParry()
        {
            Initialize();
            if (health != null && health.IsDefeated)
                return false;
            if (!CanParry || guardBroken)
                return false;

            stamina -= Mathf.Max(0f, parryCost);
            guard = GuardMode.None;
            parryUntil = Time.time + Mathf.Max(0.05f, parryWindow);
            nextParryAt = Time.time + Mathf.Max(0.1f, parryCooldown);
            return true;
        }

        public DefenseResult Resolve(CombatDamageKind kind, Vector3 attackSource)
        {
            if (health != null && health.IsDefeated)
                return new DefenseResult(1f, false, false);

            // No aplicamos daño durante el dash real: evita invalidar la esquiva
            // contra flechas y hechizos mientras continua en el mundo.
            if (motor != null && (motor.IsDashing || motor.DashMovedThisStep))
                return new DefenseResult(0f, false, true);

            if (!FacesSource(attackSource))
                return new DefenseResult(1f, false, false);

            if (ParryWindowActive &&
                (kind == CombatDamageKind.Melee || kind == CombatDamageKind.Arrow))
            {
                parryUntil = 0f; // Una parada efectiva por pulsacion.
                counterPending = true;
                counterReadyUntil = Time.time + 0.85f;
                return new DefenseResult(0f, true, false);
            }

            if (stamina <= 0f)
                return new DefenseResult(1f, false, false);

            if (guard == GuardMode.PhysicalShield)
            {
                if (kind == CombatDamageKind.Melee)
                    return new DefenseResult(shieldMeleeMultiplier, false, false);
                if (kind == CombatDamageKind.Arrow)
                    return new DefenseResult(shieldArrowMultiplier, false, false);
            }
            if (guard == GuardMode.ArcaneWard)
            {
                if (kind == CombatDamageKind.Magic)
                    return new DefenseResult(wardMagicMultiplier, false, false);
                if (kind == CombatDamageKind.Explosion)
                    return new DefenseResult(wardExplosionMultiplier, false, false);
            }

            return new DefenseResult(1f, false, false);
        }

        private bool FacesSource(Vector3 source)
        {
            Vector3 toward = source - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.0001f)
                return true;

            Vector3 forward = motor != null ? motor.CurrentFacing : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return false;
            return Vector3.Dot(forward.normalized, toward.normalized) >= requiredFacingDot;
        }

        private void OnDisable()
        {
            guard = GuardMode.None;
            parryUntil = 0f;
            counterPending = false;
        }
    }
}
