using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    public sealed class BestiaryPlayModeTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private GameObject player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "SueloBestiario";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            spawned.Add(ground);

            player = new GameObject("JugadorBestiario");
            player.transform.position = new Vector3(0f, 1.08f, 0f);
            CharacterController body = player.AddComponent<CharacterController>();
            body.height = 2f;
            body.radius = 0.4f;
            player.AddComponent<CombatHealth>();
            player.AddComponent<CombatDamageReceiver>();
            spawned.Add(player);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in spawned)
                if (go != null)
                    Object.Destroy(go);
            spawned.Clear();
            yield return null;
        }

        private PrototypeEnemy Spawn(string archetype, Vector3 position, bool boss = false)
        {
            GameObject root = new GameObject("Enemigo " + archetype);
            root.SetActive(false);
            root.transform.position = position;
            CharacterController body = root.AddComponent<CharacterController>();
            body.height = 2f;
            body.radius = 0.42f;
            root.AddComponent<CombatHealth>();
            PrototypeEnemy enemy = root.AddComponent<PrototypeEnemy>();
            FieldInfo target = typeof(PrototypeEnemy).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
            target.SetValue(enemy, player.transform);
            typeof(PrototypeEnemy).GetField("archetypeId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, archetype);
            typeof(PrototypeEnemy).GetField("isBoss", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, boss);
            root.SetActive(true);
            spawned.Add(root);
            Physics.SyncTransforms();
            return enemy;
        }

        [Test]
        public void BestiaryHasEveryRoleWithUniqueIds()
        {
            HashSet<string> ids = new HashSet<string>();
            HashSet<EnemyRole> roles = new HashSet<EnemyRole>();
            foreach (EnemyArchetype a in EnemyArchetypes.All)
            {
                Assert.That(ids.Add(a.Id), Is.True, a.Id);
                roles.Add(a.Role);
                Assert.That(a.Health, Is.GreaterThan(0f));
                Assert.That(a.AttackRange, Is.GreaterThan(1f));
                Assert.That(a.Essence, Is.Not.EqualTo(EssenceKind.None));
                if (a.IsRangedRole)
                    Assert.That(a.KiteDistance, Is.LessThan(a.AttackRange), a.Id + " debe poder disparar tras retroceder.");
            }
            foreach (EnemyRole role in System.Enum.GetValues(typeof(EnemyRole)))
                Assert.That(roles.Contains(role), Is.True, "Falta el rol " + role);
        }

        [UnityTest]
        public IEnumerator ArchetypeSetsStatsAndSkeletonsShrugOffArrows()
        {
            PrototypeEnemy skeleton = Spawn("esqueleto", new Vector3(20f, 1.08f, 20f));
            yield return null;
            CombatHealth health = skeleton.GetComponent<CombatHealth>();
            Assert.That(skeleton.Archetype.Id, Is.EqualTo("esqueleto"));
            Assert.That(health.MaximumHealth, Is.EqualTo(55f));
            Assert.That(skeleton.GetComponent<EnemyArchetypeVisual>().BuiltFor, Is.EqualTo("esqueleto"));
            CombatDamageResult arrow = skeleton.GetComponent<CombatDamageReceiver>()
                .Receive(20f, CombatDamageKind.Arrow, Vector3.zero);
            Assert.That(arrow.Applied, Is.EqualTo(10f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator CrossbowmanShootsFromRangeWithArrowDamage()
        {
            PrototypeEnemy archer = Spawn("ballestero", new Vector3(0f, 1.08f, 7.5f));
            CombatHealth playerHealth = player.GetComponent<CombatHealth>();
            float timeout = Time.time + 4f;
            while (playerHealth.CurrentHealth >= 100f && Time.time < timeout)
                yield return null;
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(92f).Within(0.01f), "Un virote de 8.");
            Assert.That(archer.ActiveAttackKind, Is.EqualTo(CombatDamageKind.Arrow));
            Vector3 delta = archer.transform.position - player.transform.position;
            delta.y = 0f;
            Assert.That(delta.magnitude, Is.GreaterThan(5f), "Dispara sin acercarse al cuerpo a cuerpo.");
        }

        [UnityTest]
        public IEnumerator AcolyteEmbersSetThePlayerOnFire()
        {
            Spawn("acolito", new Vector3(0f, 1.08f, 6f));
            CombatHealth playerHealth = player.GetComponent<CombatHealth>();
            float timeout = Time.time + 4f;
            while (playerHealth.CurrentHealth >= 100f && Time.time < timeout)
                yield return null;
            StatusEffects status = player.GetComponent<StatusEffects>();
            Assert.That(status != null && status.Has(StatusKind.Ignition), Is.True);
        }

        [UnityTest]
        public IEnumerator HealerRestoresWoundedAllies()
        {
            PrototypeEnemy healer = Spawn("sanadora", new Vector3(25f, 1.08f, 25f));
            PrototypeEnemy brute = Spawn("bruto", new Vector3(28f, 1.08f, 25f));
            yield return null;
            CombatHealth bruteHealth = brute.GetComponent<CombatHealth>();
            bruteHealth.ApplyDamage(70f);
            Assert.That(healer.HealAllies(), Is.EqualTo(1));
            Assert.That(bruteHealth.CurrentHealth, Is.EqualTo(70f + 140f * 0.14f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator BossEnragesAtHalfHealth()
        {
            PrototypeEnemy boss = Spawn("", new Vector3(30f, 1.08f, -30f), true);
            yield return null;
            CombatHealth health = boss.GetComponent<CombatHealth>();
            Assert.That(boss.IsEnraged, Is.False);
            health.ApplyDamage(health.MaximumHealth * 0.55f);
            yield return null;
            Assert.That(boss.IsEnraged, Is.True);
        }

        [UnityTest]
        public IEnumerator EachArchetypeUsuallyCarriesItsOwnEssence()
        {
            PrototypeEnemy acolyte = Spawn("acolito", new Vector3(-25f, 1.08f, -25f));
            yield return null;
            EssenceSource source = EssenceSource.For(acolyte);
            int sage = 0;
            for (int i = 0; i < 400; i++)
            {
                source.Reroll();
                if (source.Kind == EssenceKind.Sage)
                    sage++;
            }
            Assert.That(sage, Is.GreaterThan(260), "75 % de esencia propia + azar.");
        }
    }
}
