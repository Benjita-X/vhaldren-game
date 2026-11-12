using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Magic;

namespace Vhaldren.Gameplay.Races
{
    /// <summary>Linajes de Vhaldren (documento 016). Cada uno cambia como se juega.</summary>
    public enum RaceId
    {
        Human = 0,
        Arcanist = 1,     // Nacido de magia
        Vampire = 2,
        Werewolf = 3,
        Undead = 4,
        HalfDemon = 5,
        HalfCelestial = 6,
        Hybrid = 7
    }

    /// <summary>Poder activo propio de cada raza (tecla R fuera del modo construcción).</summary>
    public enum RacialPower
    {
        Determination,   // Humano: resistencia y -25 % daño
        ArcaneSurge,     // Nacido de magia: recarga todos los hechizos
        CrimsonMist,     // Vampiro: salto de niebla que cura
        LunarFury,       // Licantropo: +daño fisico y velocidad
        BoneWill,        // No muerto: escudo de hueso
        InfernalBurst,   // Semidemonio: estallido de fuego
        Grace,           // Semicelestial: curacion y limpieza
        Adaptation       // Hibrido: un poco de cura y de daño
    }

    /// <summary>Ficha de raza: estadisticas base, afinidades, ventajas y debilidades.</summary>
    public sealed class RaceDefinition
    {
        public RaceId Id;
        public string Name;
        public string Summary;
        public float Health = 1f;            // multiplicador de vida maxima
        public float Stamina = 100f;         // resistencia maxima
        public float StaminaRegen = 1f;      // multiplicador de recuperacion
        public float Speed = 1f;             // multiplicador de velocidad
        public float Physical = 1f;          // poder fisico (espada, ballesta)
        public float Spell = 1f;             // poder magico (todos los hechizos)
        public float MeleeResist, ArrowResist, MagicResist;
        public float OutOfCombatRegen = 1f;  // regeneracion fuera de combate
        public float[] Affinity = { 1f, 1f, 1f, 1f, 1f, 1f };   // por escuela (SpellSchool)
        public SpellSchool[] ExtraSchools = new SpellSchool[0];  // aprendidas desde el inicio
        public StatusKind[] Immunities = new StatusKind[0];
        public float DayDamageTaken = 1f;    // >1 = el sol te debilita
        public float NightDamageDealt = 1f;  // >1 = mas fuerte de noche
        public RacialPower Power;
        public string PowerName;
        public string PowerText;
        public float PowerCooldown;
        public string[] Strengths;
        public string[] Weaknesses;

        public float AffinityFor(SpellSchool school) => Affinity[Mathf.Clamp((int)school, 0, Affinity.Length - 1)];
    }

    public static class RaceCatalog
    {
        private static readonly RaceDefinition[] races =
        {
            new RaceDefinition
            {
                Id = RaceId.Human, Name = "Humano",
                Summary = "Versátil: sin grandes ventajas ni debilidades; aprende magia con facilidad.",
                ExtraSchools = new[] { SpellSchool.Lumancy },
                Power = RacialPower.Determination, PowerName = "Determinación", PowerCooldown = 30f,
                PowerText = "Recupera 60 de resistencia y recibe un 25 % menos de daño durante 5 s.",
                Strengths = new[] { "Empieza con 4 escuelas", "Ninguna debilidad" },
                Weaknesses = new[] { "Sin bonificaciones fuertes" }
            },
            new RaceDefinition
            {
                Id = RaceId.Arcanist, Name = "Nacido de magia",
                Summary = "Afinidad ritual: magia muy poderosa a cambio de un cuerpo frágil.",
                Health = 0.85f, Stamina = 90f, StaminaRegen = 1.15f, Physical = 0.85f, Spell = 1.25f, MagicResist = 0.15f,
                Affinity = new[] { 1f, 1.05f, 1.15f, 1.05f, 1f, 1.1f },
                ExtraSchools = new[] { SpellSchool.Lumancy, SpellSchool.Umbramancy },
                Power = RacialPower.ArcaneSurge, PowerName = "Sobrecarga arcana", PowerCooldown = 50f,
                PowerText = "Recarga al instante todos tus hechizos.",
                Strengths = new[] { "+25 % poder mágico", "Empieza con 5 escuelas", "+15 % resistencia mágica" },
                Weaknesses = new[] { "-15 % vida", "-15 % daño físico" }
            },
            new RaceDefinition
            {
                Id = RaceId.Vampire, Name = "Vampiro",
                Summary = "Cazador nocturno: más rápido y fuerte de noche; el sol lo debilita.",
                Stamina = 110f, Speed = 1.08f, Physical = 1.1f, Spell = 1.05f,
                Affinity = new[] { 0.9f, 1f, 1f, 1.15f, 1.3f, 0.7f },
                ExtraSchools = new[] { SpellSchool.Hemomancy },
                DayDamageTaken = 1.15f, NightDamageDealt = 1.15f,
                Power = RacialPower.CrimsonMist, PowerName = "Niebla carmesí", PowerCooldown = 22f,
                PowerText = "Te disuelves en niebla, saltas 6 m hacia el cursor y te curas un 10 %.",
                Strengths = new[] { "+30 % Hemomancia", "+15 % daño de noche", "+8 % velocidad" },
                Weaknesses = new[] { "De día recibe +15 % daño", "-30 % Lumancia" }
            },
            new RaceDefinition
            {
                Id = RaceId.Werewolf, Name = "Licántropo",
                Summary = "Fuerza y aguante salvajes; la magia le cuesta. La luna lo vuelve feroz.",
                Health = 1.2f, Stamina = 120f, StaminaRegen = 1.15f, Speed = 1.1f, Physical = 1.25f, Spell = 0.8f,
                MeleeResist = 0.1f, NightDamageDealt = 1.2f,
                Affinity = new[] { 0.9f, 0.95f, 0.9f, 1f, 1f, 0.9f },
                Power = RacialPower.LunarFury, PowerName = "Furia lunar", PowerCooldown = 35f,
                PowerText = "+35 % daño físico y +25 % velocidad durante 6 s.",
                Strengths = new[] { "+20 % vida", "+25 % daño físico", "+20 % daño de noche", "+10 % velocidad" },
                Weaknesses = new[] { "-20 % poder mágico", "Solo 3 escuelas al empezar" }
            },
            new RaceDefinition
            {
                Id = RaceId.Undead, Name = "No muerto",
                Summary = "Cuerpo que no siente: aguanta flechas y maldiciones, pero apenas se regenera.",
                Health = 1.1f, Speed = 0.95f, Spell = 1.05f, ArrowResist = 0.3f, OutOfCombatRegen = 0.4f,
                Affinity = new[] { 0.9f, 1.1f, 1f, 1.25f, 1.05f, 0.6f },
                ExtraSchools = new[] { SpellSchool.Umbramancy },
                Immunities = new[] { StatusKind.Curse, StatusKind.Bleed },
                Power = RacialPower.BoneWill, PowerName = "Voluntad de hueso", PowerCooldown = 40f,
                PowerText = "Escudo de hueso que absorbe un 30 % de tu vida máxima durante 5 s.",
                Strengths = new[] { "+30 % resistencia a flechas", "Inmune a Maldición y Sangría", "+25 % Umbramancia" },
                Weaknesses = new[] { "Regenera un 60 % menos", "-40 % Lumancia", "-5 % velocidad" }
            },
            new RaceDefinition
            {
                Id = RaceId.HalfDemon, Name = "Semidemonio",
                Summary = "Sangre infernal: domina el fuego y no se quema; la luz lo hiere.",
                Health = 1.05f, Physical = 1.1f, Spell = 1.1f,
                Affinity = new[] { 1.3f, 0.85f, 1f, 1.05f, 1.1f, 0.6f },
                ExtraSchools = new[] { SpellSchool.Hemomancy },
                Immunities = new[] { StatusKind.Ignition },
                Power = RacialPower.InfernalBurst, PowerName = "Estallido infernal", PowerCooldown = 25f,
                PowerText = "Explosión de fuego de 4 m: 35 de daño y Ignición.",
                Strengths = new[] { "+30 % Piromancia", "Inmune a Ignición", "+10 % daño físico y mágico" },
                Weaknesses = new[] { "-40 % Lumancia", "-15 % Criomancia" }
            },
            new RaceDefinition
            {
                Id = RaceId.HalfCelestial, Name = "Semicelestial",
                Summary = "Linaje de luz: protección y curación; la sombra le es ajena.",
                StaminaRegen = 1.1f, Physical = 0.95f, Spell = 1.1f, MagicResist = 0.15f, OutOfCombatRegen = 1.5f,
                Affinity = new[] { 1f, 1.05f, 1f, 0.6f, 0.7f, 1.35f },
                ExtraSchools = new[] { SpellSchool.Lumancy },
                Immunities = new[] { StatusKind.Dazzle },
                Power = RacialPower.Grace, PowerName = "Gracia", PowerCooldown = 40f,
                PowerText = "Te curas un 30 % y eliminas todos tus estados negativos.",
                Strengths = new[] { "+35 % Lumancia", "+50 % regeneración", "+15 % resistencia mágica" },
                Weaknesses = new[] { "-40 % Umbramancia", "-30 % Hemomancia" }
            },
            new RaceDefinition
            {
                Id = RaceId.Hybrid, Name = "Híbrido",
                Summary = "Mezcla de linajes: pequeñas ventajas de todo, ninguna completa.",
                Health = 1.05f, Stamina = 105f, StaminaRegen = 1.05f, Speed = 1.03f, Physical = 1.05f, Spell = 1.05f,
                Power = RacialPower.Adaptation, PowerName = "Adaptación", PowerCooldown = 30f,
                PowerText = "Te curas un 12 % y haces un 20 % más de daño durante 5 s.",
                Strengths = new[] { "+5 % en casi todo" },
                Weaknesses = new[] { "No recibe las ventajas completas de ninguna raza" }
            },
        };

        public static IReadOnlyList<RaceDefinition> All => races;

        public static RaceDefinition Get(RaceId id) => races[Mathf.Clamp((int)id, 0, races.Length - 1)];
    }

    /// <summary>Raza elegida en el menu principal (se guarda entre partidas).</summary>
    public static class RaceSelection
    {
        private const string Key = "vh.raza";

        public static RaceId Current
        {
            get => (RaceId)Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, RaceCatalog.All.Count - 1);
            set
            {
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
            }
        }
    }
}
