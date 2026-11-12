using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Estado de combate: causar o recibir daño mantiene al jugador en combate 6 s.
    /// En combate no hay regeneracion pasiva ni se puede cambiar de escuela.
    /// Fuera de combate se regeneran PV poco a poco.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class CombatState : MonoBehaviour
    {
        public const float CombatSeconds = 6f;
        public const float RegenPerSecond = 3f;

        private CombatHealth health;
        private PlayerDamageTracker tracker;
        private float inCombatUntil;
        private float regenBank;

        public bool InCombat => Time.time < inCombatUntil;
        public float SecondsLeft => Mathf.Max(0f, inCombatUntil - Time.time);

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            tracker = GetComponent<PlayerDamageTracker>();
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            health.Damaged += OnHurt;
            if (tracker == null)
                tracker = GetComponent<PlayerDamageTracker>();
            if (tracker != null)
                tracker.DamageRecorded += OnDealt;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Damaged -= OnHurt;
            if (tracker != null)
                tracker.DamageRecorded -= OnDealt;
        }

        private void OnHurt(float amount) => Mark();
        private void OnDealt(float amount, CombatDamageKind kind) => Mark();

        public void Mark() => inCombatUntil = Time.time + CombatSeconds;

        public void Leave() => inCombatUntil = 0f;

        /// <summary>Regeneracion fuera de combate. Separado de Update para poder probarlo.</summary>
        public float Regenerate(float seconds)
        {
            if (InCombat || health == null || health.IsDefeated || seconds <= 0f ||
                health.CurrentHealth >= health.MaximumHealth)
            {
                regenBank = 0f;
                return 0f;
            }
            // Curar en pulsos de 1 PV evita cientos de numeros verdes diminutos.
            regenBank += RegenPerSecond * seconds;
            if (regenBank < 1f)
                return 0f;
            float whole = Mathf.Floor(regenBank);
            regenBank -= whole;
            return health.Heal(whole);
        }

        private void Update() => Regenerate(Time.deltaTime);
    }
}
