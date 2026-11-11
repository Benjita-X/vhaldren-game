using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Vida independiente de entrada, camara, movimiento y red.
    /// Un servidor autoritativo podra decidir cuando llamar ApplyDamage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximumHealth = 100f;

        private float currentHealth;
        private bool initialized;

        public float MaximumHealth => IsFinite(maximumHealth) && maximumHealth >= 1f
            ? maximumHealth : 100f;
        public float CurrentHealth
        {
            get
            {
                InitializeIfNeeded();
                return currentHealth;
            }
        }
        public bool IsDefeated => CurrentHealth <= 0f;
        public event Action<float> Damaged;
        public event Action Defeated;

        private void Awake() => ResetToFull();

        private void OnValidate()
        {
            if (!IsFinite(maximumHealth) || maximumHealth < 1f)
                maximumHealth = 100f;
        }

        public float ApplyDamage(float amount)
        {
            InitializeIfNeeded();
            if (!IsFinite(amount) || amount <= 0f || currentHealth <= 0f)
                return 0f;

            float applied = Mathf.Min(currentHealth, amount);
            currentHealth = Mathf.Max(0f, currentHealth - applied);
            Damaged?.Invoke(applied);
            if (currentHealth <= 0f)
                Defeated?.Invoke();
            return applied;
        }

        public float Heal(float amount)
        {
            InitializeIfNeeded();
            if (!IsFinite(amount) || amount <= 0f || currentHealth <= 0f)
                return 0f;

            float applied = Mathf.Min(MaximumHealth - currentHealth, amount);
            currentHealth += applied;
            return applied;
        }

        public void ResetToFull()
        {
            currentHealth = MaximumHealth;
            initialized = true;
        }

        private void InitializeIfNeeded()
        {
            if (!initialized)
                ResetToFull();
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
