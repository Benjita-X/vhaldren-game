using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    public sealed class PrototypeWorldPlayModeTests
    {
        private GameObject player;
        private GameObject enemyObject;
        private GameObject ground;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (enemyObject != null)
                Object.Destroy(enemyObject);
            if (ground != null)
                Object.Destroy(ground);
            if (player != null)
                Object.Destroy(player);
            yield return null;
        }

        private PrototypeEnemy CreateEnemy(float distance, out CombatHealth enemyHealth)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            player = new GameObject("PlayerRoot");
            player.transform.position = new Vector3(0f, 1.08f, 0f);
            CharacterController playerCollider = player.AddComponent<CharacterController>();
            playerCollider.height = 2f;
            playerCollider.radius = 0.4f;
            player.AddComponent<CombatHealth>();

            enemyObject = new GameObject("PrototypeEnemyTest");
            enemyObject.transform.position = new Vector3(0f, 1.08f, distance);
            CharacterController enemyCollider = enemyObject.AddComponent<CharacterController>();
            enemyCollider.height = 2f;
            enemyCollider.radius = 0.4f;
            enemyHealth = enemyObject.AddComponent<CombatHealth>();
            enemyHealth.ResetToFull();
            PrototypeEnemy enemy = enemyObject.AddComponent<PrototypeEnemy>();
            Physics.SyncTransforms();
            return enemy;
        }

        [UnityTest]
        public IEnumerator EnemyDefeatDisablesCollisionAndNotifiesExactlyOnce()
        {
            PrototypeEnemy enemy = CreateEnemy(10f, out CombatHealth health);
            int calls = 0;
            enemy.Defeated += _ => calls++;
            yield return null;

            Assert.That(health.ApplyDamage(200f), Is.EqualTo(100f));
            Assert.That(enemy.IsDefeated, Is.True);
            Assert.That(enemyObject.GetComponent<CharacterController>().enabled, Is.False);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(health.ApplyDamage(1f), Is.Zero);
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EnemyApproachesPlayerInsideAggroRange()
        {
            PrototypeEnemy enemy = CreateEnemy(8f, out _);
            float initialDistance = Vector3.Distance(enemy.transform.position, player.transform.position);
            // El enemigo debe iniciar persecucion sin NavMesh.
            for (int i = 0; i < 30; i++)
                yield return null;
            float distance = Vector3.Distance(enemy.transform.position, player.transform.position);
            Assert.That(distance, Is.LessThan(initialDistance - 0.1f));
        }
    }
}
