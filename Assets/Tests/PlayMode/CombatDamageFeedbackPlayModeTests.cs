using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.World;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    public sealed class CombatDamageFeedbackPlayModeTests
    {
        private GameObject actor;
        private GameObject player;
        private GameObject cameraObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (actor != null)
                Object.Destroy(actor);
            if (player != null)
                Object.Destroy(player);
            if (cameraObject != null)
                Object.Destroy(cameraObject);
            yield return null;
        }

        private CombatHealth CreateHealthWithFeedback(out CombatDamageFeedback feedback)
        {
            actor = new GameObject("TrainingEnemy");
            actor.SetActive(false);
            CombatHealth health = actor.AddComponent<CombatHealth>();
            feedback = actor.AddComponent<CombatDamageFeedback>();
            actor.SetActive(true);
            return health;
        }

        [UnityTest]
        public IEnumerator DamageNumbersReportOnlyActualHealthLostIncludingLastHit()
        {
            CombatHealth health = CreateHealthWithFeedback(out CombatDamageFeedback feedback);
            yield return null;

            Assert.That(feedback.DamageEventsReceived, Is.Zero,
                "No mostrar dano solo por mirar o tener vida completa.");
            Assert.That(feedback.HealthFraction, Is.EqualTo(1f));
            Assert.That(health.ApplyDamage(25f), Is.EqualTo(25f));
            Assert.That(feedback.DamageEventsReceived, Is.EqualTo(1));
            Assert.That(feedback.LastDamageReceived, Is.EqualTo(25f));
            Assert.That(feedback.HealthFraction, Is.EqualTo(0.75f).Within(0.001f));

            Assert.That(health.ApplyDamage(500f), Is.EqualTo(75f));
            Assert.That(feedback.DamageEventsReceived, Is.EqualTo(2));
            Assert.That(feedback.LastDamageReceived, Is.EqualTo(75f),
                "La cifra flotante no debe incluir dano sobrante de un golpe letal.");
            Assert.That(feedback.TotalDamageReceived, Is.EqualTo(100f));
            Assert.That(feedback.HealthFraction, Is.Zero);

            Assert.That(health.ApplyDamage(20f), Is.Zero);
            Assert.That(feedback.DamageEventsReceived, Is.EqualTo(2),
                "Golpear a un objetivo derrotado no crea un numero falso.");

            health.ResetToFull();
            Assert.That(feedback.HealthFraction, Is.EqualTo(1f),
                "Una reaparicion debe restaurar la barra visual completa.");
        }

        [UnityTest]
        public IEnumerator DisablingAndEnablingFeedbackDoesNotDuplicateDamageEvents()
        {
            CombatHealth health = CreateHealthWithFeedback(out CombatDamageFeedback feedback);
            yield return null;

            health.ApplyDamage(10f);
            Assert.That(feedback.DamageEventsReceived, Is.EqualTo(1));
            feedback.enabled = false;
            yield return null;
            health.ApplyDamage(10f);
            Assert.That(feedback.DamageEventsReceived, Is.EqualTo(1),
                "El handler debe desuscribirse al desactivar la interfaz.");

            feedback.enabled = true;
            yield return null;
            health.ApplyDamage(10f);
            Assert.That(feedback.DamageEventsReceived, Is.EqualTo(2),
                "Reactivar no puede multiplicar las suscripciones.");
            Assert.That(feedback.TotalDamageReceived, Is.EqualTo(20f));
        }

        [UnityTest]
        public IEnumerator HealingNumbersReflectOnlyActualRecovery()
        {
            CombatHealth health = CreateHealthWithFeedback(out CombatDamageFeedback feedback);
            yield return null;

            Assert.That(health.ApplyDamage(70f), Is.EqualTo(70f));
            Assert.That(health.Heal(30f), Is.EqualTo(30f));
            Assert.That(feedback.HealingEventsReceived, Is.EqualTo(1));
            Assert.That(feedback.LastHealingReceived, Is.EqualTo(30f));
            Assert.That(feedback.HealthFraction, Is.EqualTo(0.6f).Within(0.001f));

            // Intento de overheal: 100 solicitados, solo 40 reales.
            Assert.That(health.Heal(100f), Is.EqualTo(40f));
            Assert.That(feedback.LastHealingReceived, Is.EqualTo(40f));
            Assert.That(feedback.HealingEventsReceived, Is.EqualTo(2));
            Assert.That(health.Heal(10f), Is.Zero);
            Assert.That(feedback.HealingEventsReceived, Is.EqualTo(2),
                "La vida llena no debe producir avisos verdes falsos.");
        }

        [UnityTest]
        public IEnumerator HitReactionChangesOnlyVisualThenRestoresScale()
        {
            CombatHealth health = CreateHealthWithFeedback(out _);
            GameObject model = new GameObject("Visual enemigo");
            model.transform.SetParent(actor.transform, false);
            model.transform.localScale = new Vector3(1.3f, 0.85f, 1.3f);
            Vector3 baseline = model.transform.localScale;
            Vector3 rootPosition = actor.transform.position;
            EnemyHitReaction reaction = actor.AddComponent<EnemyHitReaction>();
            reaction.BindVisual(model.transform);
            yield return null;

            health.ApplyDamage(12f);
            Assert.That(reaction.Recovering, Is.True);
            Assert.That(model.transform.localScale.x, Is.LessThan(baseline.x));
            Assert.That(actor.transform.position, Is.EqualTo(rootPosition),
                "El feedback visual no puede desplazar fisicamente a un enemigo.");

            yield return new WaitForSeconds(0.28f);
            Assert.That(reaction.Recovering, Is.False);
            Assert.That(Vector3.Distance(model.transform.localScale, baseline), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator RealShotDamagesExistingEnemyAndUpdatesVisibleCounters()
        {
            cameraObject = new GameObject("FeedbackTestCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 8f, -8f);

            actor = new GameObject("RealEnemy");
            actor.SetActive(false);
            actor.transform.position = new Vector3(0f, 1.08f, 4f);
            CharacterController body = actor.AddComponent<CharacterController>();
            body.height = 2f;
            body.radius = 0.42f;
            CombatHealth health = actor.AddComponent<CombatHealth>();
            PrototypeEnemy enemy = actor.AddComponent<PrototypeEnemy>();
            actor.SetActive(true);

            player = new GameObject("PlayerRoot");
            player.transform.position = new Vector3(3f, 1.08f, 0f);
            player.AddComponent<CombatHealth>();
            LocalPlayerShooter shooter = player.AddComponent<LocalPlayerShooter>();
            typeof(LocalPlayerShooter).GetField("viewCamera",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(shooter, camera);

            cameraObject.transform.LookAt(actor.transform.position);
            Physics.SyncTransforms();
            yield return null;

            Vector2 pointer = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Assert.That(shooter.FireAtScreenPosition(pointer), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(75f).Within(0.01f),
                "La bala debe descontar vida real de la capsula del enemigo.");
            CombatDamageFeedback feedback = actor.GetComponent<CombatDamageFeedback>();
            Assert.That(feedback, Is.Not.Null,
                "Los enemigos de escenas antiguas deben incorporar el feedback automaticamente.");
            Assert.That(feedback.LastDamageReceived, Is.EqualTo(25f));
            Assert.That(feedback.DamageEventsReceived, Is.EqualTo(1));
            Assert.That(shooter.HitsLanded, Is.EqualTo(1));
            Assert.That(shooter.TotalDamageDealt, Is.EqualTo(25f));
            Assert.That(shooter.LastDamageDealt, Is.EqualTo(25f));
        }
    }
}
