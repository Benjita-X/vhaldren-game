using System;
using System.Collections.Generic;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Contrato de contenido. Define QUE existe en la progresion futura;
    /// no pretende que las escenas, modelos o misiones ya esten construidos.
    /// Tierra es el primer mundo jugable. Cielo e Infierno se integraran
    /// como regiones/escenas independientes con sus propios recursos.
    /// </summary>
    public enum RealmKind { Earth, Celestial, Infernal }
    public enum SettlementCulture
    {
        Human, Arcane, Vampire, Lycan, Undead, Demon, Celestial, Mixed
    }
    public enum CaveTheme
    {
        Limestone, Crystal, AbandonedMine, WerewolfDen,
        VampireCrypt, Necropolis, FloodedGrotto, DeepForest,
        SkySanctum, InfernalFissure, AncientVault
    }
    public enum LineageKind
    {
        Human, Mageborn, Vampire, Lycan, Revenant, HalfDemon, HalfCelestial, Hybrid
    }
    public enum FormKind
    {
        Humanoid, Wolf, DireWolf, Bat, NightMist,
        InfernalAspect, CelestialAspect, UndeadAspect
    }

    public sealed class RealmDefinition
    {
        public readonly RealmKind Realm;
        public readonly string DisplayName;
        public readonly string EntryScene;
        public readonly int MinimumProgressLevel;
        public readonly string KeyResource;

        public RealmDefinition(RealmKind realm, string name, string scene,
            int level, string keyResource)
        {
            Realm = realm;
            DisplayName = name;
            EntryScene = scene;
            MinimumProgressLevel = level;
            KeyResource = keyResource;
        }
    }

    public sealed class CaveDefinition
    {
        public readonly string Id;
        public readonly RealmKind Realm;
        public readonly CaveTheme Theme;
        public readonly int RecommendedLevel;
        public readonly bool NeedsBoss;
        public readonly string RewardTag;

        public CaveDefinition(string id, RealmKind realm, CaveTheme theme,
            int level, bool boss, string reward)
        {
            Id = id;
            Realm = realm;
            Theme = theme;
            RecommendedLevel = level;
            NeedsBoss = boss;
            RewardTag = reward;
        }
    }

    public sealed class SettlementDefinition
    {
        public readonly string Id;
        public readonly RealmKind Realm;
        public readonly SettlementCulture Culture;
        public readonly bool HasWanderingMerchantStop;
        public readonly bool HasMagicMentor;

        public SettlementDefinition(string id, RealmKind realm,
            SettlementCulture culture, bool merchantStop, bool magicMentor)
        {
            Id = id;
            Realm = realm;
            Culture = culture;
            HasWanderingMerchantStop = merchantStop;
            HasMagicMentor = magicMentor;
        }
    }

    public static class WorldProgressionCatalog
    {
        public static readonly IReadOnlyList<RealmDefinition> Realms =
            Array.AsReadOnly(new[]
            {
                new RealmDefinition(RealmKind.Earth, "Tierra",
                    "WorldPrototype001", 1, "Madera, piedra, cristales"),
                new RealmDefinition(RealmKind.Celestial, "Alto Reino",
                    "CelestialEntry", 30, "Esencia astral"),
                new RealmDefinition(RealmKind.Infernal, "Reino Abisal",
                    "InfernalEntry", 30, "Obsidiana y brasa abisal")
            });

        public static readonly IReadOnlyList<SettlementDefinition> SettlementArchetypes =
            Array.AsReadOnly(new[]
            {
                new SettlementDefinition("earth-crossroads", RealmKind.Earth,
                    SettlementCulture.Mixed, true, false),
                new SettlementDefinition("earth-arcane-academy", RealmKind.Earth,
                    SettlementCulture.Arcane, true, true),
                new SettlementDefinition("earth-lycan-village", RealmKind.Earth,
                    SettlementCulture.Lycan, false, false),
                new SettlementDefinition("earth-vampire-citadel", RealmKind.Earth,
                    SettlementCulture.Vampire, true, true),
                new SettlementDefinition("earth-necropolis", RealmKind.Earth,
                    SettlementCulture.Undead, false, true),
                new SettlementDefinition("earth-human-hamlet", RealmKind.Earth,
                    SettlementCulture.Human, true, false),
                new SettlementDefinition("celestial-sky-city", RealmKind.Celestial,
                    SettlementCulture.Celestial, true, true),
                new SettlementDefinition("infernal-obsidian-market", RealmKind.Infernal,
                    SettlementCulture.Demon, true, true)
            });

        // Arquetipos de cuevas que luego alimentaran el director de
        // contenido. No son cuevas generadas ni posiciones inventadas.
        public static readonly IReadOnlyList<CaveDefinition> CaveArchetypes =
            Array.AsReadOnly(new[]
            {
                new CaveDefinition("earth-limestone", RealmKind.Earth,
                    CaveTheme.Limestone, 1, false, "piedra-comun"),
                new CaveDefinition("earth-mine", RealmKind.Earth,
                    CaveTheme.AbandonedMine, 5, true, "minerales"),
                new CaveDefinition("earth-forest", RealmKind.Earth,
                    CaveTheme.DeepForest, 7, false, "plantas-raras"),
                new CaveDefinition("earth-wolf", RealmKind.Earth,
                    CaveTheme.WerewolfDen, 9, true, "linaje-lobo"),
                new CaveDefinition("earth-crystal", RealmKind.Earth,
                    CaveTheme.Crystal, 13, true, "magia-runa"),
                new CaveDefinition("earth-vampire", RealmKind.Earth,
                    CaveTheme.VampireCrypt, 16, true, "sangre-antigua"),
                new CaveDefinition("earth-necropolis", RealmKind.Earth,
                    CaveTheme.Necropolis, 20, true, "necromancia"),
                new CaveDefinition("earth-flooded", RealmKind.Earth,
                    CaveTheme.FloodedGrotto, 18, false, "alquimia"),
                new CaveDefinition("celestial-sanctum", RealmKind.Celestial,
                    CaveTheme.SkySanctum, 30, true, "esencia-astral"),
                new CaveDefinition("celestial-vault", RealmKind.Celestial,
                    CaveTheme.AncientVault, 36, true, "runa-celestial"),
                new CaveDefinition("infernal-fissure", RealmKind.Infernal,
                    CaveTheme.InfernalFissure, 30, true, "obsidiana"),
                new CaveDefinition("infernal-vault", RealmKind.Infernal,
                    CaveTheme.AncientVault, 38, true, "magia-abisal")
            });

        private static readonly Dictionary<LineageKind, FormKind[]> Forms =
            new Dictionary<LineageKind, FormKind[]>
            {
                { LineageKind.Human, new[] { FormKind.Humanoid } },
                { LineageKind.Mageborn, new[] { FormKind.Humanoid } },
                { LineageKind.Vampire, new[] {
                    FormKind.Humanoid, FormKind.Bat, FormKind.NightMist } },
                { LineageKind.Lycan, new[] {
                    FormKind.Humanoid, FormKind.Wolf, FormKind.DireWolf } },
                { LineageKind.Revenant, new[] {
                    FormKind.Humanoid, FormKind.UndeadAspect } },
                { LineageKind.HalfDemon, new[] {
                    FormKind.Humanoid, FormKind.InfernalAspect } },
                { LineageKind.HalfCelestial, new[] {
                    FormKind.Humanoid, FormKind.CelestialAspect } },
                // La genealogia mixta se determina mediante desbloqueos
                // narrativos; no concede todas las formas gratuitamente.
                { LineageKind.Hybrid, new[] { FormKind.Humanoid } }
            };

        public static bool CanUnlockForm(LineageKind lineage, FormKind form)
        {
            if (!Forms.TryGetValue(lineage, out FormKind[] available))
                return false;
            return Array.IndexOf(available, form) >= 0;
        }

        public static IReadOnlyList<FormKind> AllowedForms(LineageKind lineage)
        {
            return Forms.TryGetValue(lineage, out FormKind[] forms)
                ? Array.AsReadOnly(forms)
                : Array.AsReadOnly(Array.Empty<FormKind>());
        }

        public static RealmDefinition FindRealm(RealmKind kind)
        {
            for (int i = 0; i < Realms.Count; i++)
                if (Realms[i].Realm == kind)
                    return Realms[i];
            return null;
        }

        public static bool ValidateArchetypes()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CaveDefinition cave in CaveArchetypes)
            {
                if (cave == null || string.IsNullOrWhiteSpace(cave.Id) ||
                    !ids.Add(cave.Id) || FindRealm(cave.Realm) == null ||
                    cave.RecommendedLevel <= 0)
                    return false;
            }
            if (Realms.Count != 3 || ids.Count != CaveArchetypes.Count)
                return false;

            ids.Clear();
            foreach (SettlementDefinition town in SettlementArchetypes)
            {
                if (town == null || string.IsNullOrWhiteSpace(town.Id) ||
                    !ids.Add(town.Id) || FindRealm(town.Realm) == null)
                    return false;
            }
            return ids.Count == SettlementArchetypes.Count;
        }
    }
}
