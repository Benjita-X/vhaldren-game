using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Seis escuelas arcanas originales de Vhaldren, mas la Luz caida: el poder propio de
    /// Lucifer, que ninguna esencia ni maestro puede enseñar a otro linaje.
    /// </summary>
    public enum SpellSchool
    {
        Pyromancy = 0,   // Fuego
        Cryomancy = 1,   // Escarcha
        Fulmancy = 2,    // Tormenta
        Umbramancy = 3,  // Sombras
        Hemomancy = 4,   // Sangre
        Lumancy = 5,     // Luz
        FallenLight = 6  // Luz caida (exclusiva de Lucifer): luz dorada y fuego infernal
    }

    /// <summary>Como llega el hechizo al objetivo.</summary>
    public enum SpellDelivery
    {
        Bolt = 0,     // Proyectil hacia el cursor; golpea al primero.
        Nova = 1,     // Area alrededor del mago tras una preparacion.
        Zone = 2,     // Circulo avisado en el cursor que estalla tras un retraso.
        Chain = 3,    // Rayo instantaneo que salta entre enemigos cercanos.
        Blink = 4,    // Desplazamiento corto respetando colisiones; estalla al llegar.
        Barrier = 5,  // Escudo que absorbe daño segun el poder de hechizo.
        Leap = 6      // Salto con alas por encima de obstaculos; estalla y ata al caer.
    }

    /// <summary>Datos de un hechizo. Las mejoras futuras solo cambian estos datos.</summary>
    public sealed class SpellDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly SpellSchool School;
        public readonly SpellDelivery Delivery;
        public readonly float Damage;
        public readonly float Radius;
        public readonly float Range;
        public readonly float Windup;
        public readonly float Cooldown;
        public readonly float StaminaCost;
        public readonly StatusKind Status;
        public readonly int StatusStacks;
        public readonly float SelfHeal;
        public readonly float LifeSteal;
        public readonly int ChainTargets;
        public readonly int Charges;
        public readonly string Description;
        /// <summary>Enemigos que atraviesa un proyectil antes de detenerse (0 = el primero).</summary>
        public readonly int Pierce;

        public SpellDefinition(string id, string displayName, SpellSchool school, SpellDelivery delivery,
            float damage, float radius, float range, float windup, float cooldown, float staminaCost,
            StatusKind status, int statusStacks, string description,
            float selfHeal = 0f, float lifeSteal = 0f, int chainTargets = 0, int pierce = 0)
        {
            Pierce = Mathf.Max(0, pierce);
            Id = id;
            DisplayName = displayName;
            School = school;
            Delivery = delivery;
            Damage = damage;
            Radius = radius;
            Range = range;
            Windup = windup;
            Cooldown = cooldown;
            StaminaCost = staminaCost;
            Status = status;
            StatusStacks = statusStacks;
            Description = description;
            SelfHeal = selfHeal;
            LifeSteal = lifeSteal;
            ChainTargets = chainTargets;
            // Los hechizos basicos guardan dos cargas: se pueden encadenar y
            // cada carga se recupera por separado.
            Charges = delivery == SpellDelivery.Bolt ? 2 : 1;
        }
    }

    /// <summary>
    /// Grimorio completo: 7 escuelas x 3 hechizos (1 basico, 2 control, 3 definitivo).
    /// Las 6 primeras son comunes; la Luz caida es el kit exclusivo de Lucifer.
    /// Inspirado en el genero de magos duelistas, con nombres y efectos propios.
    /// </summary>
    public static class SpellCatalog
    {
        public const int SpellsPerSchool = 3;
        public const int SchoolCount = 7;
        /// <summary>Escuelas que cualquier linaje puede aprender (sin las exclusivas).</summary>
        public const int CommonSchoolCount = 6;

        private static readonly SpellDefinition[] spells =
        {
            // ── Piromancia ─────────────────────────────────────────────
            new SpellDefinition("saeta_ignea", "Saeta ígnea", SpellSchool.Pyromancy, SpellDelivery.Bolt,
                18f, 0.35f, 18f, 0.10f, 1.4f, 10f, StatusKind.Ignition, 1,
                "Proyectil de fuego que deja Ignición."),
            new SpellDefinition("anillo_llamas", "Anillo de llamas", SpellSchool.Pyromancy, SpellDelivery.Nova,
                20f, 3.6f, 0f, 0.35f, 7f, 22f, StatusKind.Ignition, 1,
                "Estalla a tu alrededor y quema a todos."),
            new SpellDefinition("meteoro", "Meteoro", SpellSchool.Pyromancy, SpellDelivery.Zone,
                60f, 3.8f, 14f, 1.20f, 20f, 35f, StatusKind.Ignition, 1,
                "Definitivo: un meteoro cae en el círculo avisado."),

            // ── Criomancia ────────────────────────────────────────────
            new SpellDefinition("lanza_hielo", "Lanza de hielo", SpellSchool.Cryomancy, SpellDelivery.Bolt,
                16f, 0.35f, 18f, 0.10f, 1.4f, 10f, StatusKind.Chill, 1,
                "Proyectil helado: suma una carga de Escarcha."),
            new SpellDefinition("prision_glacial", "Prisión glacial", SpellSchool.Cryomancy, SpellDelivery.Zone,
                12f, 2.6f, 12f, 0.60f, 8f, 22f, StatusKind.Chill, 2,
                "Suma dos cargas; con tres el enemigo se congela."),
            new SpellDefinition("ventisca", "Ventisca", SpellSchool.Cryomancy, SpellDelivery.Nova,
                30f, 5f, 0f, 0.50f, 20f, 35f, StatusKind.Chill, 3,
                "Definitivo: congela a todo lo que te rodea."),

            // ── Fulmancia (tormenta) ──────────────────────────────────
            new SpellDefinition("chispa", "Chispa", SpellSchool.Fulmancy, SpellDelivery.Bolt,
                14f, 0.35f, 20f, 0.05f, 1.1f, 9f, StatusKind.Static, 1,
                "Rápida; deja Estática que el siguiente hechizo descarga."),
            new SpellDefinition("cadena_rayos", "Cadena de rayos", SpellSchool.Fulmancy, SpellDelivery.Chain,
                22f, 6f, 10f, 0.15f, 7f, 22f, StatusKind.Static, 1,
                "Salta entre cuatro enemigos.", chainTargets: 4),
            new SpellDefinition("tempestad", "Tempestad", SpellSchool.Fulmancy, SpellDelivery.Zone,
                48f, 5f, 14f, 0.90f, 20f, 35f, StatusKind.Static, 1,
                "Definitivo: una tormenta golpea el área señalada."),

            // ── Umbramancia (sombras) ─────────────────────────────────
            new SpellDefinition("dardo_umbrio", "Dardo umbrío", SpellSchool.Umbramancy, SpellDelivery.Bolt,
                15f, 0.35f, 18f, 0.10f, 1.4f, 10f, StatusKind.Curse, 1,
                "Maldice: el objetivo recibe un 25% más de daño."),
            new SpellDefinition("paso_sombrio", "Paso sombrío", SpellSchool.Umbramancy, SpellDelivery.Blink,
                16f, 2.6f, 7f, 0f, 6f, 18f, StatusKind.Curse, 1,
                "Te desplazas entre sombras y maldices al llegar."),
            new SpellDefinition("eclipse", "Eclipse", SpellSchool.Umbramancy, SpellDelivery.Nova,
                42f, 4.5f, 0f, 0.60f, 20f, 35f, StatusKind.Curse, 1,
                "Definitivo: oscuridad que maldice a todos."),

            // ── Hemomancia (sangre) ───────────────────────────────────
            new SpellDefinition("saeta_carmesi", "Saeta carmesí", SpellSchool.Hemomancy, SpellDelivery.Bolt,
                15f, 0.35f, 18f, 0.10f, 1.4f, 10f, StatusKind.Bleed, 1,
                "Marca con Sangría: tus golpes al marcado te curan.", lifeSteal: 0.3f),
            new SpellDefinition("torrente_sangre", "Torrente sangriento", SpellSchool.Hemomancy, SpellDelivery.Nova,
                18f, 3.2f, 0f, 0.30f, 8f, 20f, StatusKind.Bleed, 1,
                "Absorbe la mitad del daño causado.", lifeSteal: 0.5f),
            new SpellDefinition("festin_carmesi", "Festín carmesí", SpellSchool.Hemomancy, SpellDelivery.Zone,
                50f, 3.6f, 12f, 0.90f, 20f, 35f, StatusKind.Bleed, 1,
                "Definitivo: drena vida de todos los atrapados.", lifeSteal: 0.4f),

            // ── Lumancia (luz) ────────────────────────────────────────
            new SpellDefinition("rayo_solar", "Rayo solar", SpellSchool.Lumancy, SpellDelivery.Bolt,
                16f, 0.35f, 18f, 0.10f, 1.4f, 10f, StatusKind.Dazzle, 1,
                "Deslumbra: el objetivo hace un 35% menos de daño."),
            new SpellDefinition("egida", "Égida", SpellSchool.Lumancy, SpellDelivery.Barrier,
                40f, 0f, 0f, 0f, 12f, 20f, StatusKind.Dazzle, 0,
                "Escudo de luz que absorbe daño durante 4 s."),
            new SpellDefinition("juicio_celestial", "Juicio celestial", SpellSchool.Lumancy, SpellDelivery.Zone,
                55f, 4f, 14f, 1.00f, 20f, 35f, StatusKind.Dazzle, 1,
                "Definitivo: luz que castiga y te cura.", selfHeal: 20f),

            // ── Luz caída (solo Lucifer) ──────────────────────────────
            new SpellDefinition("lanza_lucero", "Lanza del Lucero", SpellSchool.FallenLight, SpellDelivery.Bolt,
                17f, 0.4f, 20f, 0.12f, 1.5f, 10f, StatusKind.Ignition, 1,
                "Lanza de luz dorada y fuego infernal: atraviesa hasta tres enemigos y los prende.", pierce: 3),
            new SpellDefinition("salto_alas", "Salto de alas", SpellSchool.FallenLight, SpellDelivery.Leap,
                18f, 3.2f, 9f, 0.15f, 9f, 22f, StatusKind.Ignition, 1,
                "Vuelas hasta 9 m por encima de muros y vallas; al caer, cadenas de azufre atan 1,5 s y queman."),
            new SpellDefinition("caida_lucero", "Caída del Lucero", SpellSchool.FallenLight, SpellDelivery.Zone,
                58f, 5f, 15f, 1.40f, 24f, 35f, StatusKind.Ignition, 1,
                "Definitivo: arrancas una estrella del cielo; cae en el gran círculo avisado y lo arrasa."),
        };

        /// <summary>Escuela reservada a un linaje (la Luz caída solo la usa Lucifer).</summary>
        public static bool IsExclusive(SpellSchool school) => school == SpellSchool.FallenLight;

        public static IReadOnlyList<SpellDefinition> All => spells;

        public static SpellDefinition Get(SpellSchool school, int slot)
        {
            int index = (int)school * SpellsPerSchool + Mathf.Clamp(slot, 0, SpellsPerSchool - 1);
            return index >= 0 && index < spells.Length ? spells[index] : null;
        }

        public static SpellDefinition Find(string id)
        {
            for (int i = 0; i < spells.Length; i++)
                if (spells[i].Id == id)
                    return spells[i];
            return null;
        }

        public static string SchoolName(SpellSchool school)
        {
            switch (school)
            {
                case SpellSchool.Pyromancy: return "Piromancia";
                case SpellSchool.Cryomancy: return "Criomancia";
                case SpellSchool.Fulmancy: return "Fulmancia";
                case SpellSchool.Umbramancy: return "Umbramancia";
                case SpellSchool.Hemomancy: return "Hemomancia";
                case SpellSchool.Lumancy: return "Lumancia";
                case SpellSchool.FallenLight: return "Luz caída";
                default: return school.ToString();
            }
        }

        public static Color SchoolColor(SpellSchool school)
        {
            switch (school)
            {
                case SpellSchool.Pyromancy: return new Color(1f, 0.45f, 0.12f);
                case SpellSchool.Cryomancy: return new Color(0.45f, 0.88f, 1f);
                case SpellSchool.Fulmancy: return new Color(0.75f, 0.65f, 1f);
                case SpellSchool.Umbramancy: return new Color(0.55f, 0.25f, 0.85f);
                case SpellSchool.Hemomancy: return new Color(0.85f, 0.08f, 0.18f);
                case SpellSchool.Lumancy: return new Color(1f, 0.92f, 0.55f);
                case SpellSchool.FallenLight: return new Color(1f, 0.7f, 0.26f);
                default: return Color.white;
            }
        }
    }
}
