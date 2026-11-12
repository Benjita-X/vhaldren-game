using System;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Reserva de esencia del jugador. Se vacia con el tiempo; por debajo del 25 %
    /// la bonificacion se reduce a la mitad y al vaciarse se pierde la esencia.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EssenceReservoir : MonoBehaviour, IDamageTakenModifier
    {
        private float pool;
        private EssenceKind kind = EssenceKind.None;
        private int quality;

        public event Action Changed;
        public EssenceKind Kind => kind;
        public int Quality => quality;
        public float Pool => pool;

        /// <summary>Fuerza 0..1 de la bonificacion actual.</summary>
        public float Strength
        {
            get
            {
                if (kind == EssenceKind.None || pool <= 0f)
                    return 0f;
                float q = quality / 100f;
                return pool < EssenceCatalog.LowPoolThreshold ? q * 0.5f : q;
            }
        }

        public float SpellPowerMultiplier =>
            1f + (kind == EssenceKind.Sage ? 0.35f : kind == EssenceKind.Arcane ? 0.15f : 0f) * Strength;

        public float CriticalChance =>
            (kind == EssenceKind.Fierce ? 0.30f : kind == EssenceKind.Arcane ? 0.10f : 0f) * Strength;

        public float LifeSteal =>
            (kind == EssenceKind.Sanguine ? 0.20f : kind == EssenceKind.Arcane ? 0.08f : 0f) * Strength;

        public float CooldownMultiplier =>
            1f - (kind == EssenceKind.Wild ? 0.30f : kind == EssenceKind.Arcane ? 0.10f : 0f) * Strength;

        public float IncomingMultiplier =>
            1f - (kind == EssenceKind.Titanic ? 0.25f : kind == EssenceKind.Arcane ? 0.10f : 0f) * Strength;

        /// <summary>Sustituye la esencia actual por la de la victima y llena la reserva.</summary>
        public void Absorb(EssenceKind essence, int essenceQuality)
        {
            kind = essence;
            quality = Mathf.Clamp(essenceQuality, 0, 100);
            pool = essence == EssenceKind.None ? 0f : EssenceCatalog.MaxPool;
            Changed?.Invoke();
        }

        /// <summary>Gasta reserva con el paso del tiempo. Separado de Update para probarlo.</summary>
        public void Drain(float seconds)
        {
            if (kind == EssenceKind.None || seconds <= 0f || float.IsNaN(seconds))
                return;
            float before = pool;
            pool = Mathf.Max(0f, pool - EssenceCatalog.DrainPerSecond * seconds);
            if (pool <= 0f)
            {
                kind = EssenceKind.None;
                quality = 0;
            }
            if (pool <= 0f || (before >= EssenceCatalog.LowPoolThreshold) != (pool >= EssenceCatalog.LowPoolThreshold))
                Changed?.Invoke();
        }

        private void Update() => Drain(Time.deltaTime);
    }
}
