using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    public enum CombatDamageKind
    {
        Melee = 0,
        Arrow = 1,
        Magic = 2,
        Explosion = 3
    }

    public readonly struct CombatDamageResult
    {
        public readonly float Applied;
        public readonly float Prevented;
        public readonly bool Parried;
        public readonly bool Evaded;

        public CombatDamageResult(float applied, float prevented, bool parried, bool evaded)
        {
            Applied = applied;
            Prevented = prevented;
            Parried = parried;
            Evaded = evaded;
        }
    }

    /// <summary>Componentes que alteran el daño recibido (maldicion, esencia, vulnerabilidad).</summary>
    public interface IDamageTakenModifier
    {
        float IncomingMultiplier { get; }
    }

    /// <summary>Escudos que absorben daño antes de restar PV. Devuelven el daño que sobra.</summary>
    public interface IDamageAbsorber
    {
        float Absorb(float amount);
    }

    /// <summary>
    /// Unica puerta opcional para ataques nuevos. Las armas eligen su clase de daño;
    /// la salud existente sigue enviando eventos del daño REAL aplicado.
    /// Los scripts antiguos que llaman CombatHealth.ApplyDamage siguen funcionando.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class CombatDamageReceiver : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.95f)] private float arrowResistance;
        [SerializeField, Range(0f, 0.95f)] private float magicResistance;
        [SerializeField, Range(0f, 0.95f)] private float meleeResistance;

        private CombatHealth health;
        private CombatDefense defense;

        private readonly System.Collections.Generic.List<IDamageTakenModifier> modifiers =
            new System.Collections.Generic.List<IDamageTakenModifier>();
        private readonly System.Collections.Generic.List<IDamageAbsorber> absorbers =
            new System.Collections.Generic.List<IDamageAbsorber>();
        public event Action<CombatDamageResult> Resolved;
        public float LastAppliedDamage { get; private set; }

        public void ConfigureResistances(float melee, float arrow, float magic)
        {
            meleeResistance = Mathf.Clamp(melee, 0f, 0.95f);
            arrowResistance = Mathf.Clamp(arrow, 0f, 0.95f);
            magicResistance = Mathf.Clamp(magic, 0f, 0.95f);
        }

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            defense = GetComponent<CombatDefense>();
        }

        public CombatDamageResult Receive(float requestedDamage, CombatDamageKind kind,
            Vector3 attackSource)
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            if (defense == null)
                defense = GetComponent<CombatDefense>();

            if (health == null || health.IsDefeated ||
                float.IsNaN(requestedDamage) || float.IsInfinity(requestedDamage) ||
                requestedDamage <= 0f)
            {
                LastAppliedDamage = 0f;
                return new CombatDamageResult(0f, 0f, false, false);
            }

            float amount = requestedDamage;
            float resistance = kind == CombatDamageKind.Melee ? meleeResistance :
                kind == CombatDamageKind.Arrow ? arrowResistance : magicResistance;
            amount *= 1f - Mathf.Clamp(resistance, 0f, 0.95f);

            bool parried = false;
            bool evaded = false;
            if (defense != null)
            {
                CombatDefense.DefenseResult protection = defense.Resolve(kind, attackSource);
                amount *= protection.Multiplier;
                parried = protection.Parried;
                evaded = protection.Evaded;
            }

            if (amount > 0f)
            {
                GetComponents(modifiers);
                for (int i = 0; i < modifiers.Count; i++)
                {
                    float factor = modifiers[i].IncomingMultiplier;
                    if (!float.IsNaN(factor) && !float.IsInfinity(factor))
                        amount *= Mathf.Clamp(factor, 0.1f, 3f);
                }
                GetComponents(absorbers);
                for (int i = 0; i < absorbers.Count && amount > 0f; i++)
                    amount = Mathf.Clamp(absorbers[i].Absorb(amount), 0f, amount);
            }

            amount = Mathf.Max(0f, amount);
            float applied = amount > 0f ? health.ApplyDamage(amount) : 0f;
            LastAppliedDamage = applied;
            float prevented = Mathf.Max(0f, requestedDamage - amount);
            CombatDamageResult result = new CombatDamageResult(applied, prevented, parried, evaded);
            if (prevented > 0f)
            {
                CombatDamageFeedback feedback = GetComponent<CombatDamageFeedback>();
                if (feedback != null)
                    feedback.ShowDefenseResult(prevented, parried, evaded);
            }
            if (applied > 0f)
                GameFeel.OnHit(this, applied, kind, attackSource);
            else if (prevented > 0f && !evaded)
                GameFeel.OnBlocked(this, parried);
            if (prevented > 0f || applied > 0f)
                Resolved?.Invoke(result);
            return result;
        }
    }
}
