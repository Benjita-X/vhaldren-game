using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;
using Vhaldren.Gameplay.Player;
using System.Reflection;

namespace Vhaldren.Tests
{
    public sealed class PrototypeWorldPlayModeTests
    {
        private GameObject player;
        private GameObject enemyObject;
        private GameObject ground;
        private GameObject wall;
        private GameObject sessionObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (enemyObject != null)
                Object.Destroy(enemyObject);
            if (ground != null)
                Object.Destroy(ground);
            if (wall != null)
                Object.Destroy(wall);
            if (sessionObject != null)
                Object.Destroy(sessionObject);
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
            enemyObject.SetActive(false); // Asegurar que Awake lea el target asignado.
            enemyObject.transform.position = new Vector3(0f, 1.08f, distance);
            CharacterController enemyCollider = enemyObject.AddComponent<CharacterController>();
            enemyCollider.height = 2f;
            enemyCollider.radius = 0.4f;
            enemyHealth = enemyObject.AddComponent<CombatHealth>();
            enemyHealth.ResetToFull();
            PrototypeEnemy enemy = enemyObject.AddComponent<PrototypeEnemy>();
            typeof(PrototypeEnemy).GetField("target",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, player.transform);
            enemyObject.SetActive(true);
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
        public IEnumerator WallBlocksMeleeDamageUntilLineOfSightOpens()
        {
            CreateEnemy(1.55f, out _);
            CombatHealth playerHealth = player.GetComponent<CombatHealth>();

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.2f, 0.8f);
            wall.transform.localScale = new Vector3(4f, 2.4f, 0.2f);
            Physics.SyncTransforms();

            yield return new WaitForSeconds(0.25f);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f),
                "Un enemigo no puede golpear a traves de una pared.");

            Object.Destroy(wall);
            wall = null;
            yield return null; // Dejar que se actualice la escena de fisicas.
            // Los ataques ahora tienen aviso antes del impacto.
            yield return new WaitForSeconds(0.8f);

            Assert.That(playerHealth.CurrentHealth, Is.LessThan(100f),
                "El enemigo debe poder golpear si esta cerca y hay linea de vision.");
        }

        [UnityTest]
        public IEnumerator EnemyMustWarnBeforeDealingDamage()
        {
            PrototypeEnemy enemy = CreateEnemy(1.40f, out _);
            CombatHealth playerHealth = player.GetComponent<CombatHealth>();

            yield return null; // Primer Update del enemigo: inicia su aviso.
            Assert.That(enemy.IsAttackTelegraphed, Is.True,
                "Un golpe no puede resolverse en el mismo frame en que empieza el aviso.");
            Assert.That(enemyObject.GetComponent<EnemyAttackTelegraph>(), Is.Not.Null);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f));

            yield return new WaitForSeconds(0.16f);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f),
                "La vida debe quedar intacta mientras el jugador aun puede reaccionar.");

            yield return new WaitForSeconds(0.45f);
            Assert.That(playerHealth.CurrentHealth, Is.LessThan(100f),
                "Tras el aviso el enemigo debe golpear si el objetivo sigue en alcance.");
        }

        [UnityTest]
        public IEnumerator EscapingTelegraphRangePreventsDamage()
        {
            PrototypeEnemy enemy = CreateEnemy(1.40f, out _);
            CombatHealth playerHealth = player.GetComponent<CombatHealth>();
            yield return null;
            Assert.That(enemy.IsAttackTelegraphed, Is.True);
            float initialHealth = playerHealth.CurrentHealth;

            player.transform.position = new Vector3(0f, 1.08f, -4f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.6f);

            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(initialHealth),
                "Salir del alcance durante la carga debe evitar el golpe.");
            Assert.That(enemy.IsAttackTelegraphed, Is.False,
                "El ataque debe cancelarse al perder el objetivo.");
        }

        [UnityTest]
        public IEnumerator HittingOrdinaryEnemyInterruptsButDoesNotTeleportIt()
        {
            PrototypeEnemy enemy = CreateEnemy(1.40f, out CombatHealth health);
            yield return null;
            Assert.That(enemy.IsAttackTelegraphed, Is.True);
            Vector3 before = enemy.transform.position;
            CombatHealth playerHealth = player.GetComponent<CombatHealth>();

            health.ApplyDamage(10f);
            Assert.That(enemy.IsAttackTelegraphed, Is.False,
                "El ataque anunciado por un guardian se cancela al recibir dano.");
            Assert.That(Vector3.Distance(before, enemy.transform.position),
                Is.LessThan(0.02f), "El stagger no debe teletransportar al enemigo.");

            yield return new WaitForSeconds(0.6f);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f),
                "El ataque cancelado no debe ejecutarse fuera de su estado.");
        }

        [UnityTest]
        public IEnumerator DashProtectsOnImpactWithoutForcingPlayerToLeaveReach()
        {
            PrototypeEnemy enemy = CreateEnemy(1.40f, out _);
            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            typeof(PlayerMotor).GetField("dashDistance",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(motor, 0f);
            typeof(PrototypeEnemy).GetField("targetMotor",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, motor);
            CombatHealth playerHealth = player.GetComponent<CombatHealth>();

            yield return null;
            Assert.That(enemy.IsAttackTelegraphed, Is.True);

            // Forzar el instante de resolucion para comprobar solo la regla de
            // invulnerabilidad, sin depender de FPS ni temporizadores de PlayMode.
            typeof(PrototypeEnemy).GetField("attackReleaseAt",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, Time.time - 0.01f);
            motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward, true),
                1f / 60f);
            Assert.That(motor.IsDashing, Is.True);
            Assert.That(Vector3.Distance(player.transform.position,
                enemy.transform.position), Is.LessThan(1.65f));

            yield return null;
            Assert.That(enemy.IsAttackTelegraphed, Is.False);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f),
                "Una esquiva activa evita daño aunque el personaje siga en el alcance.");
        }

        [UnityTest]
        public IEnumerator EnemyReturnsHomeAfterCrossingItsLeashBoundary()
        {
            PrototypeEnemy enemy = CreateEnemy(8f, out _);
            typeof(PrototypeEnemy).GetField("leashRange",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 3f);
            enemy.transform.position = new Vector3(0f, 1.08f, 3.5f);
            Physics.SyncTransforms();

            // El objetivo esta en z=0; perseguirlo moveria al enemigo hacia -Z.
            // El retorno correcto lo lleva hacia su punto de origen en z=8.
            yield return new WaitForSeconds(0.75f);
            Assert.That(enemy.transform.position.z, Is.GreaterThan(5f),
                "Debe seguir regresando a su territorio, incluso al cruzar la frontera.");
            bool returning = (bool)typeof(PrototypeEnemy).GetField("returningHome",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);
            Assert.That(returning, Is.True,
                "No debe cambiar a persecucion antes de regresar realmente a casa.");
        }

        [UnityTest]
        public IEnumerator PlayerCannotShootWhenDeadAndCanShootAgainAfterRespawning()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);

            player = new GameObject("PlayerRoot");
            player.transform.position = new Vector3(0f, 1.08f, 0f);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            CombatHealth health = player.AddComponent<CombatHealth>();
            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            LocalPlayerShooter shooter = player.AddComponent<LocalPlayerShooter>();

            // Preparar la sesion inactiva para no depender de GameObject.Find
            // ni del orden de inicializacion de otras escenas de Test Runner.
            sessionObject = new GameObject("SessionTest");
            sessionObject.SetActive(false);
            PrototypeGameSession session = sessionObject.AddComponent<PrototypeGameSession>();
            typeof(PrototypeGameSession).GetField("player",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, player.transform);
            typeof(PrototypeGameSession).GetField("playerRespawnDelay",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, 0.1f);
            sessionObject.SetActive(true);
            yield return null;

            health.ApplyDamage(1000f);
            Assert.That(health.IsDefeated, Is.True);
            Assert.That(motor.enabled, Is.False);
            Assert.That(shooter.enabled, Is.False,
                "Un personaje derrotado no debe conservar ataques activos.");

            yield return new WaitForSeconds(0.18f);
            Assert.That(health.IsDefeated, Is.False);
            Assert.That(motor.enabled, Is.True);
            Assert.That(shooter.enabled, Is.True);
            Assert.That(Vector3.Distance(player.transform.position,
                new Vector3(0f, 1.08f, 0f)), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator EnemyApproachesPlayerInsideAggroRange()
        {
            PrototypeEnemy enemy = CreateEnemy(8f, out _);
            float initialDistance = Vector3.Distance(enemy.transform.position, player.transform.position);
            // El enemigo debe iniciar persecucion sin NavMesh. Se espera tiempo real,
            // no frames: en modo batch 30 frames duran apenas milisegundos.
            yield return new WaitForSeconds(0.5f);
            float distance = Vector3.Distance(enemy.transform.position, player.transform.position);
            Assert.That(distance, Is.LessThan(initialDistance - 0.1f));
        }
    }
}
