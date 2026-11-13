using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Tests
{
    public sealed class PlayerDamageTrackerTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void TracksActualDamageByWeaponAndIgnoresMisses()
        {
            root = new GameObject("DamageTrackerTest");
            PlayerDamageTracker tracker = root.AddComponent<PlayerDamageTracker>();
            Assert.That(tracker.Record(25f, CombatDamageKind.Arrow), Is.True);
            Assert.That(tracker.Record(35f, CombatDamageKind.Melee), Is.True);
            Assert.That(tracker.Record(22f, CombatDamageKind.Magic), Is.True);
            Assert.That(tracker.Record(5f, CombatDamageKind.DamageOverTime), Is.True);
            Assert.That(tracker.TotalHits, Is.EqualTo(4));
            Assert.That(tracker.TotalDamage, Is.EqualTo(87f));
            Assert.That(tracker.DamageFor(CombatDamageKind.Arrow), Is.EqualTo(25f));
            Assert.That(tracker.DamageFor(CombatDamageKind.Melee), Is.EqualTo(35f));
            Assert.That(tracker.DamageFor(CombatDamageKind.Magic), Is.EqualTo(22f));
            Assert.That(tracker.DamageFor(CombatDamageKind.DamageOverTime), Is.EqualTo(5f));
            Assert.That(tracker.LastDamage, Is.EqualTo(5f));
            Assert.That(tracker.LastKind, Is.EqualTo(CombatDamageKind.DamageOverTime));
            Assert.That(tracker.Record(0f, CombatDamageKind.Arrow), Is.False);
            Assert.That(tracker.Record(float.NaN, CombatDamageKind.Melee), Is.False);
            Assert.That(tracker.Record(float.PositiveInfinity, CombatDamageKind.Arrow), Is.False);
            Assert.That(tracker.TotalHits, Is.EqualTo(4));
        }

        [Test]
        public void AppliedDamageEventFiresOnlyForValidAmounts()
        {
            root = new GameObject("DamageTrackerTest");
            PlayerDamageTracker tracker = root.AddComponent<PlayerDamageTracker>();
            int fired = 0;
            float value = 0f;
            CombatDamageKind kind = CombatDamageKind.Melee;
            tracker.DamageRecorded += (damage, category) =>
            {
                fired++;
                value = damage;
                kind = category;
            };

            tracker.Record(-10f, CombatDamageKind.Arrow);
            tracker.Record(0f, CombatDamageKind.Magic);
            tracker.Record(12.5f, CombatDamageKind.Explosion);
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(value, Is.EqualTo(12.5f));
            Assert.That(kind, Is.EqualTo(CombatDamageKind.Explosion));
            Assert.That(tracker.DamageFor(CombatDamageKind.Explosion), Is.EqualTo(12.5f));
            Assert.That(tracker.HitsFor(CombatDamageKind.Explosion), Is.EqualTo(1));
        }
    }
}
