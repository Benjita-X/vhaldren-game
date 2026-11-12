using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Magic;

namespace Vhaldren.Tests
{
    public sealed class MagicCatalogTests
    {
        [Test]
        public void GrimoireHasSixSchoolsWithThreeValidSpellsEach()
        {
            Assert.That(SpellCatalog.All.Count, Is.EqualTo(SpellCatalog.SchoolCount * SpellCatalog.SpellsPerSchool));
            HashSet<string> ids = new HashSet<string>();
            for (int s = 0; s < SpellCatalog.SchoolCount; s++)
            {
                for (int slot = 0; slot < SpellCatalog.SpellsPerSchool; slot++)
                {
                    SpellDefinition spell = SpellCatalog.Get((SpellSchool)s, slot);
                    Assert.That(spell, Is.Not.Null);
                    Assert.That(spell.School, Is.EqualTo((SpellSchool)s), spell.Id + " esta en otra escuela.");
                    Assert.That(ids.Add(spell.Id), Is.True, "Id repetido: " + spell.Id);
                    Assert.That(spell.Cooldown, Is.GreaterThan(0f));
                    Assert.That(spell.StaminaCost, Is.GreaterThan(0f));
                    Assert.That(spell.Damage, Is.GreaterThan(0f));
                    Assert.That(spell.Charges, Is.GreaterThanOrEqualTo(1));
                    Assert.That(string.IsNullOrEmpty(spell.Description), Is.False);
                }
                // El hueco 3 es el definitivo: siempre la recarga mas larga de su escuela.
                Assert.That(SpellCatalog.Get((SpellSchool)s, 2).Cooldown,
                    Is.GreaterThan(SpellCatalog.Get((SpellSchool)s, 0).Cooldown));
            }
            Assert.That(SpellCatalog.Find("meteoro"), Is.Not.Null);
            Assert.That(SpellCatalog.Find("no-existe"), Is.Null);
        }

        [Test]
        public void BasicBoltsHaveTwoChargesAndOthersOne()
        {
            foreach (SpellDefinition spell in SpellCatalog.All)
                Assert.That(spell.Charges, Is.EqualTo(spell.Delivery == SpellDelivery.Bolt ? 2 : 1), spell.Id);
        }

        [Test]
        public void EssenceAbsorbFillsPoolAndDrainLosesItAtZero()
        {
            GameObject owner = new GameObject("Esencia");
            try
            {
                EssenceReservoir reservoir = owner.AddComponent<EssenceReservoir>();
                Assert.That(reservoir.Strength, Is.Zero);
                Assert.That(reservoir.SpellPowerMultiplier, Is.EqualTo(1f));

                reservoir.Absorb(EssenceKind.Sage, 100);
                Assert.That(reservoir.Pool, Is.EqualTo(EssenceCatalog.MaxPool));
                Assert.That(reservoir.SpellPowerMultiplier, Is.EqualTo(1.35f).Within(0.001f));

                // Con poca reserva la bonificacion baja a la mitad.
                reservoir.Drain((EssenceCatalog.MaxPool - 20f) / EssenceCatalog.DrainPerSecond);
                Assert.That(reservoir.Pool, Is.EqualTo(20f).Within(0.01f));
                Assert.That(reservoir.SpellPowerMultiplier, Is.EqualTo(1.175f).Within(0.001f));

                reservoir.Drain(10000f);
                Assert.That(reservoir.Kind, Is.EqualTo(EssenceKind.None));
                Assert.That(reservoir.Strength, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void EachEssenceGivesOnlyItsOwnBonus()
        {
            GameObject owner = new GameObject("Esencia");
            try
            {
                EssenceReservoir r = owner.AddComponent<EssenceReservoir>();
                r.Absorb(EssenceKind.Titanic, 100);
                Assert.That(r.IncomingMultiplier, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(r.CriticalChance, Is.Zero);
                r.Absorb(EssenceKind.Fierce, 50);
                Assert.That(r.CriticalChance, Is.EqualTo(0.15f).Within(0.001f));
                Assert.That(r.IncomingMultiplier, Is.EqualTo(1f));
                r.Absorb(EssenceKind.Wild, 100);
                Assert.That(r.CooldownMultiplier, Is.EqualTo(0.7f).Within(0.001f));
                r.Absorb(EssenceKind.Sanguine, 100);
                Assert.That(r.LifeSteal, Is.EqualTo(0.2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void QualityRollsStayInRangeAndBossesAreArcane()
        {
            System.Random random = new System.Random(5);
            int low = 0;
            for (int i = 0; i < 2000; i++)
            {
                int q = EssenceCatalog.RollQuality(random, false);
                Assert.That(q, Is.InRange(5, 100));
                if (q < 50) low++;
                EssenceKind kind = EssenceCatalog.RollKind(random, false);
                Assert.That(kind, Is.Not.EqualTo(EssenceKind.None).And.Not.EqualTo(EssenceKind.Arcane));
                Assert.That(EssenceCatalog.RollQuality(random, true), Is.InRange(80, 100));
            }
            Assert.That(low, Is.GreaterThan(1000), "La esencia pura debe ser rara.");
            Assert.That(EssenceCatalog.RollKind(random, true), Is.EqualTo(EssenceKind.Arcane));
        }

        [Test]
        public void ThreeEssencesTeachTheLockedSchools()
        {
            Assert.That(EssenceCatalog.TeachesSchool(EssenceKind.Sage, out SpellSchool a) && a == SpellSchool.Lumancy);
            Assert.That(EssenceCatalog.TeachesSchool(EssenceKind.Fierce, out SpellSchool b) && b == SpellSchool.Umbramancy);
            Assert.That(EssenceCatalog.TeachesSchool(EssenceKind.Sanguine, out SpellSchool c) && c == SpellSchool.Hemomancy);
            Assert.That(EssenceCatalog.TeachesSchool(EssenceKind.Titanic, out _), Is.False);
        }
    

        [Test]
        public void DayNightClockNightsAndCrimsonMoon()
        {
            const float hour = Vhaldren.Gameplay.World.DayNightCycle.DaySeconds / 24f;
            Assert.That(Vhaldren.Gameplay.World.DayNightCycle.HourAt(0f), Is.EqualTo(8f).Within(0.001f));
            Assert.That(Vhaldren.Gameplay.World.DayNightCycle.IsNightHour(12f), Is.False);
            Assert.That(Vhaldren.Gameplay.World.DayNightCycle.IsNightHour(22f), Is.True);
            Assert.That(Vhaldren.Gameplay.World.DayNightCycle.IsNightHour(3f), Is.True);
            Assert.That(Vhaldren.Gameplay.World.DayNightCycle.DayAt(hour * 15f), Is.EqualTo(1), "23:00 del primer dia.");
            Assert.That(Vhaldren.Gameplay.World.DayNightCycle.DayAt(hour * 17f), Is.EqualTo(2), "01:00 ya es el dia 2.");
            // Noche del dia 4 (22:00) y su madrugada (02:00 del dia 5): Luna carmesi.
            float day4Night = (3f * 24f + 14f) * hour;
            Assert.That(Vhaldren.Gameplay.World.DayNightCycle.IsCrimsonNight(day4Night), Is.True);
            Assert.That(Vhaldren.Gameplay.World.DayNightCycle.IsCrimsonNight(day4Night + 4f * hour), Is.True);
            Assert.That(Vhaldren.Gameplay.World.DayNightCycle.IsCrimsonNight(day4Night - 24f * hour), Is.False);
        }

        [Test]
        public void GearLevelsAndLevelGapMultipliers()
        {
            Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.GearLevel(0, 0, 0), Is.EqualTo(1));
            Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.GearLevel(3, 3, 3), Is.EqualTo(24));
            Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.GearLevel(1, 1, 0), Is.EqualTo(6));
            Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.DamageDealtMultiplier(10, 10), Is.EqualTo(1f));
            Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.DamageDealtMultiplier(1, 26), Is.EqualTo(0.35f));
            Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.DamageTakenMultiplier(1, 26), Is.EqualTo(2f));
            Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.DamageDealtMultiplier(24, 26), Is.EqualTo(0.92f).Within(0.001f));
            Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.DamageTakenMultiplier(30, 5), Is.EqualTo(0.65f));
            for (int t = 1; t <= Vhaldren.Gameplay.Progression.GearCatalog.MaxTier; t++)
            {
                Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.Get(t).Level,
                    Is.GreaterThan(Vhaldren.Gameplay.Progression.GearCatalog.Get(t - 1).Level));
                Assert.That(Vhaldren.Gameplay.Progression.GearCatalog.Get(t).Cost.Length, Is.GreaterThan(0));
            }
        }
    }
}
