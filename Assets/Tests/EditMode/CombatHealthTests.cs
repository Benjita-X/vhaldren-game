using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Tests
{
    public sealed class CombatHealthTests
    {
        [Test]
        public void DamageIsClampedAndDefeatFiresExactlyOnce()
        {
            GameObject obj = new GameObject("HealthTest");
            try
            {
                CombatHealth health = obj.AddComponent<CombatHealth>();
                health.ResetToFull();
                int defeats = 0;
                health.Defeated += () => defeats++;

                Assert.That(health.ApplyDamage(30f), Is.EqualTo(30f));
                Assert.That(health.CurrentHealth, Is.EqualTo(70f));
                Assert.That(health.ApplyDamage(300f), Is.EqualTo(70f));
                Assert.That(health.CurrentHealth, Is.Zero);
                Assert.That(health.ApplyDamage(5f), Is.Zero);
                Assert.That(defeats, Is.EqualTo(1));
                Assert.That(health.IsDefeated, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void NonFiniteAndNegativeAmountsCannotCorruptHealth()
        {
            GameObject obj = new GameObject("HealthInvalidTest");
            try
            {
                CombatHealth health = obj.AddComponent<CombatHealth>();
                health.ResetToFull();
                Assert.That(health.ApplyDamage(float.NaN), Is.Zero);
                Assert.That(health.ApplyDamage(float.PositiveInfinity), Is.Zero);
                Assert.That(health.ApplyDamage(-4f), Is.Zero);
                Assert.That(health.Heal(float.NegativeInfinity), Is.Zero);
                Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void HealAndResetAreBounded()
        {
            GameObject obj = new GameObject("HealthHealTest");
            try
            {
                CombatHealth health = obj.AddComponent<CombatHealth>();
                health.ResetToFull();
                health.ApplyDamage(60f);
                Assert.That(health.Heal(1000f), Is.EqualTo(60f));
                Assert.That(health.CurrentHealth, Is.EqualTo(100f));
                health.ApplyDamage(100f);
                Assert.That(health.Heal(50f), Is.Zero,
                    "El objetivo derrotado se recupera con ResetToFull, no con Heal.");
                health.ResetToFull();
                Assert.That(health.CurrentHealth, Is.EqualTo(100f));
                Assert.That(health.IsDefeated, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
