using System;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Esencia vital que se absorbe de enemigos debilitados. El tipo decide la
    /// bonificacion y la calidad (1-100 %) su intensidad.
    /// </summary>
    public enum EssenceKind
    {
        None = 0,
        Sage = 1,       // Sabia: mas poder de hechizo.
        Fierce = 2,     // Feroz: golpes criticos de hechizo.
        Titanic = 3,    // Titanica: recibe menos daño.
        Sanguine = 4,   // Sanguinea: roba vida con cada hechizo.
        Wild = 5,       // Salvaje: recarga de hechizos mas rapida.
        Arcane = 6      // Arcana: solo jefes; un poco de todo.
    }

    public static class EssenceCatalog
    {
        public const float MaxPool = 100f;
        public const float DrainPerSecond = 1f / 6f;   // reserva llena ≈ 10 minutos
        public const float LowPoolThreshold = 25f;

        public static string Name(EssenceKind kind)
        {
            switch (kind)
            {
                case EssenceKind.Sage: return "Sabia";
                case EssenceKind.Fierce: return "Feroz";
                case EssenceKind.Titanic: return "Titánica";
                case EssenceKind.Sanguine: return "Sanguínea";
                case EssenceKind.Wild: return "Salvaje";
                case EssenceKind.Arcane: return "Arcana";
                default: return "Sin esencia";
            }
        }

        public static string Bonus(EssenceKind kind)
        {
            switch (kind)
            {
                case EssenceKind.Sage: return "+poder de hechizo";
                case EssenceKind.Fierce: return "críticos de hechizo";
                case EssenceKind.Titanic: return "-daño recibido";
                case EssenceKind.Sanguine: return "robo de vida";
                case EssenceKind.Wild: return "-recarga";
                case EssenceKind.Arcane: return "todo un poco";
                default: return "sin bonificación";
            }
        }

        /// <summary>Escuela que se aprende al absorber por primera vez esta esencia.</summary>
        public static bool TeachesSchool(EssenceKind kind, out SpellSchool school)
        {
            switch (kind)
            {
                case EssenceKind.Sage: school = SpellSchool.Lumancy; return true;
                case EssenceKind.Fierce: school = SpellSchool.Umbramancy; return true;
                case EssenceKind.Sanguine: school = SpellSchool.Hemomancy; return true;
                default: school = SpellSchool.Pyromancy; return false;
            }
        }

        /// <summary>Calidad con sesgo a valores bajos: la esencia pura es rara.</summary>
        public static int RollQuality(System.Random random, bool boss)
        {
            double roll = random.NextDouble();
            if (boss)
                return 80 + (int)Math.Round(roll * 20.0);
            return Mathf.Clamp(5 + (int)Math.Round(Math.Pow(roll, 1.8) * 95.0), 5, 100);
        }

        public static EssenceKind RollKind(System.Random random, bool boss)
        {
            if (boss)
                return EssenceKind.Arcane;
            return (EssenceKind)(1 + random.Next(5));
        }
    }
}
