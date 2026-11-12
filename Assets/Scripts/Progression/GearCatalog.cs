using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Survival;

namespace Vhaldren.Gameplay.Progression
{
    /// <summary>Huecos de equipo. Cada uno sube por cuatro calidades.</summary>
    public enum GearSlot
    {
        Weapon = 0,    // Arma: solo nivel.
        Armor = 1,     // Armadura: nivel y vida maxima.
        Talisman = 2   // Talisman: nivel y poder de hechizo.
    }

    /// <summary>Una calidad de equipo: nivel que aporta y receta para fabricarla.</summary>
    public sealed class GearTier
    {
        public readonly int Tier;
        public readonly string Suffix;
        public readonly int Level;
        public readonly bool NeedsWorkbench;
        public readonly ResourceIngredient[] Cost;

        public GearTier(int tier, string suffix, int level, bool needsWorkbench, params ResourceIngredient[] cost)
        {
            Tier = tier;
            Suffix = suffix;
            Level = level;
            NeedsWorkbench = needsWorkbench;
            Cost = cost;
        }
    }

    /// <summary>
    /// Progresion por equipo. El nivel de equipo es la media de los tres huecos y se
    /// compara con el nivel del enemigo: luchar por encima de tu nivel es castigado.
    /// Los materiales salen de enemigos concretos, asi que hay que cazar en orden.
    /// </summary>
    public static class GearCatalog
    {
        public const int MaxTier = 3;
        public const float LevelStep = 0.04f;   // 4 % por nivel de diferencia

        private static readonly GearTier[] tiers =
        {
            new GearTier(0, "de viajero", 1, false),
            new GearTier(1, "de cazador", 8, false,
                new ResourceIngredient(ResourceKind.Hide, 4), new ResourceIngredient(ResourceKind.Wood, 6),
                new ResourceIngredient(ResourceKind.Fiber, 4)),
            new GearTier(2, "de hierro", 16, true,
                new ResourceIngredient(ResourceKind.Ore, 6), new ResourceIngredient(ResourceKind.Wood, 4),
                new ResourceIngredient(ResourceKind.Hide, 3)),
            new GearTier(3, "rúnico", 24, true,
                new ResourceIngredient(ResourceKind.Crystal, 3), new ResourceIngredient(ResourceKind.Ore, 6),
                new ResourceIngredient(ResourceKind.Essence, 4)),
        };

        private static readonly float[] armorHealth = { 0f, 20f, 45f, 80f };
        private static readonly float[] talismanPower = { 1f, 1.06f, 1.12f, 1.2f };

        public static IReadOnlyList<GearTier> Tiers => tiers;

        public static GearTier Get(int tier) => tiers[Mathf.Clamp(tier, 0, MaxTier)];

        public static float ArmorHealthBonus(int tier) => armorHealth[Mathf.Clamp(tier, 0, MaxTier)];

        public static float TalismanSpellPower(int tier) => talismanPower[Mathf.Clamp(tier, 0, MaxTier)];

        public static string SlotName(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Weapon: return "Arma";
                case GearSlot.Armor: return "Armadura";
                default: return "Talismán";
            }
        }

        public static int GearLevel(int weaponTier, int armorTier, int talismanTier) =>
            Mathf.RoundToInt((Get(weaponTier).Level + Get(armorTier).Level + Get(talismanTier).Level) / 3f);

        /// <summary>Daño que haces a un enemigo segun la diferencia de nivel.</summary>
        public static float DamageDealtMultiplier(int gearLevel, int enemyLevel) =>
            Mathf.Clamp(1f + LevelStep * (gearLevel - enemyLevel), 0.35f, 1.4f);

        /// <summary>Daño que te hace un enemigo segun la diferencia de nivel.</summary>
        public static float DamageTakenMultiplier(int gearLevel, int enemyLevel) =>
            Mathf.Clamp(1f - LevelStep * (gearLevel - enemyLevel), 0.65f, 2f);
    }
}
