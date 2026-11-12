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
    }
}
