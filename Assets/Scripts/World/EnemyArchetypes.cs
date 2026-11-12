using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Magic;

namespace Vhaldren.Gameplay.World
{
    /// <summary>Como pelea un enemigo. Cada rol cambia distancia, tipo de daño y tactica.</summary>
    public enum EnemyRole
    {
        Melee = 0,     // Se acerca y golpea.
        Ranged = 1,    // Mantiene distancia y dispara (daño de flecha: escudo Z).
        Caster = 2,    // Mantiene distancia y lanza magia (barrera C).
        Beast = 3,     // Muy rapido, golpes cortos y poca vida.
        Brute = 4,     // Lento, mucha vida, golpe pesado muy telegrafiado.
        Support = 5,   // Cura a sus aliados cercanos y huye del cuerpo a cuerpo.
        Elite = 6      // Alterna golpe fisico y magico, resistente.
    }

    /// <summary>Ficha de un tipo de enemigo original de Vhaldren.</summary>
    public sealed class EnemyArchetype
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly EnemyRole Role;
        public readonly float Health;
        public readonly float Damage;
        public readonly float Speed;
        public readonly float AttackRange;
        public readonly float Windup;
        public readonly float Cooldown;
        public readonly float Aggro;
        public readonly float MeleeResist;
        public readonly float ArrowResist;
        public readonly float MagicResist;
        public readonly bool AppliesStatus;
        public readonly StatusKind OnHit;
        public readonly float KiteDistance;
        public readonly EssenceKind Essence;
        public readonly Color Tint;
        public readonly string Description;
        public readonly int Level;

        public EnemyArchetype(string id, string displayName, EnemyRole role, float health, float damage,
            float speed, float attackRange, float windup, float cooldown, float aggro,
            float meleeResist, float arrowResist, float magicResist, bool appliesStatus, StatusKind onHit,
            float kiteDistance, EssenceKind essence, Color tint, string description, int level = 1)
        {
            Level = level;
            Id = id;
            DisplayName = displayName;
            Role = role;
            Health = health;
            Damage = damage;
            Speed = speed;
            AttackRange = attackRange;
            Windup = windup;
            Cooldown = cooldown;
            Aggro = aggro;
            MeleeResist = meleeResist;
            ArrowResist = arrowResist;
            MagicResist = magicResist;
            AppliesStatus = appliesStatus;
            OnHit = onHit;
            KiteDistance = kiteDistance;
            Essence = essence;
            Tint = tint;
            Description = description;
        }

        public bool IsRangedRole => Role == EnemyRole.Ranged || Role == EnemyRole.Caster || Role == EnemyRole.Support;
    }

    /// <summary>
    /// Bestiario de Vhaldren. Cubre los roles clasicos del genero (bandidos, tiradores,
    /// magos, bestias, brutos, curanderos y elites) con diseños y nombres propios.
    /// Cada tipo suele llevar una esencia concreta: cazar el tipo correcto es la forma
    /// de aprender una escuela de magia.
    /// </summary>
    public static class EnemyArchetypes
    {
        private static readonly EnemyArchetype[] all =
        {
            new EnemyArchetype("saqueador", "Saqueador del camino", EnemyRole.Melee,
                60f, 9f, 3.3f, 1.75f, 0.45f, 1.3f, 15f, 0f, 0f, 0f, false, StatusKind.Curse, 0f,
                EssenceKind.Fierce, new Color(0.45f, 0.30f, 0.18f),
                "Bandido de cuchillo rápido. Esencia Feroz.", 5),
            new EnemyArchetype("ballestero", "Ballestero renegado", EnemyRole.Ranged,
                45f, 8f, 3.0f, 9f, 0.75f, 2.2f, 17f, 0f, 0.1f, 0f, false, StatusKind.Curse, 5f,
                EssenceKind.Fierce, new Color(0.35f, 0.38f, 0.20f),
                "Retrocede y dispara; la línea roja avisa del tiro. Escudo Z lo bloquea.", 6),
            new EnemyArchetype("acolito", "Acólito ceniciento", EnemyRole.Caster,
                50f, 7f, 2.9f, 8f, 0.85f, 2.6f, 16f, 0f, 0f, 0.25f, true, StatusKind.Ignition, 5f,
                EssenceKind.Sage, new Color(0.55f, 0.22f, 0.10f),
                "Lanza brasas que queman. Barrera C lo bloquea. Esencia Sabia.", 12),
            new EnemyArchetype("lobo", "Lobo de bruma", EnemyRole.Beast,
                40f, 6f, 5.2f, 1.6f, 0.30f, 0.9f, 18f, 0f, 0f, 0f, false, StatusKind.Curse, 0f,
                EssenceKind.Wild, new Color(0.42f, 0.44f, 0.48f),
                "Caza en manada y muy rápido. Esencia Salvaje.", 7),
            new EnemyArchetype("bruto", "Bruto de las canteras", EnemyRole.Brute,
                140f, 22f, 2.4f, 2.3f, 0.95f, 2.6f, 14f, 0.3f, 0.2f, 0f, false, StatusKind.Curse, 0f,
                EssenceKind.Titanic, new Color(0.40f, 0.36f, 0.33f),
                "Golpe lento y devastador: esquiva o para con X. Esencia Titánica.", 16),
            new EnemyArchetype("esqueleto", "Esqueleto de la cripta", EnemyRole.Melee,
                55f, 9f, 3.0f, 1.75f, 0.5f, 1.4f, 15f, 0f, 0.5f, 0f, false, StatusKind.Curse, 0f,
                EssenceKind.Titanic, new Color(0.85f, 0.83f, 0.74f),
                "Las flechas apenas le hacen daño: usa magia o espada.", 10),
            new EnemyArchetype("sanadora", "Sanadora del culto", EnemyRole.Support,
                45f, 5f, 3.0f, 7f, 0.8f, 3.0f, 16f, 0f, 0f, 0.2f, true, StatusKind.Curse, 6f,
                EssenceKind.Sanguine, new Color(0.30f, 0.55f, 0.30f),
                "Cura a sus aliados cada 5 s y maldice. Mátala primero. Esencia Sanguínea.", 12),
            new EnemyArchetype("centinela_runico", "Centinela rúnico", EnemyRole.Elite,
                180f, 16f, 2.8f, 2.4f, 0.7f, 1.8f, 16f, 0.2f, 0.3f, 0.35f, true, StatusKind.Curse, 0f,
                EssenceKind.Sage, new Color(0.25f, 0.35f, 0.60f),
                "Élite: alterna espada y runas. Resiste todo un poco.", 20),
        };

        public static IReadOnlyList<EnemyArchetype> All => all;

        public static EnemyArchetype Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Id == id)
                    return all[i];
            return null;
        }
    }
}
