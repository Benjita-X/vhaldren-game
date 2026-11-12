using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    public sealed class GameFeelPlayModeTests
    {
        private GameObject ground;
        private GameObject player;
        private GameObject enemyObject;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameFeel.Enabled = true;
            GameAudio.Muted = true;
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            player = new GameObject("JugadorFeel");
            player.transform.position = new Vector3(0f, 1.08f, -15f);
            player.AddComponent<CharacterController>();
            player.AddComponent<CombatHealth>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameFeel.Enabled = false;
            GameAudio.Muted = false;
            Object.Destroy(ground);
            Object.Destroy(player);
            if (enemyObject != null) Object.Destroy(enemyObject);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "El hit stop nunca deja el juego congelado.");
        }

        private PrototypeEnemy MakeEnemy(Vector3 position)
        {
            enemyObject = new GameObject("EnemigoFeel");
            enemyObject.SetActive(false);
            enemyObject.transform.position = position;
            enemyObject.AddComponent<CharacterController>();
            enemyObject.AddComponent<CombatHealth>();
            PrototypeEnemy enemy = enemyObject.AddComponent<PrototypeEnemy>();
            typeof(PrototypeEnemy).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, player.transform);
            enemyObject.SetActive(true);
            Physics.SyncTransforms();
            return enemy;
        }

        [UnityTest]
        public IEnumerator RealHitFreezesBrieflyPushesBackAndRestoresTime()
        {
            PrototypeEnemy enemy = MakeEnemy(new Vector3(0f, 1.08f, 0f));
            yield return null;
            int stops = GameFeel.HitStops;
            Vector3 before = enemy.transform.position;
            enemy.GetComponent<CombatDamageReceiver>().Receive(30f, CombatDamageKind.Melee, new Vector3(0f, 1.08f, -3f));
            Assert.That(GameFeel.HitStops, Is.EqualTo(stops + 1));
            Assert.That(Time.timeScale, Is.LessThan(0.1f), "Parón de impacto activo.");
            Assert.That(enemy.Knockback.z, Is.GreaterThan(0f), "Empujado lejos del atacante.");
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            // Durante el aturdimiento del golpe (0,18 s) solo actua el empujon.
            yield return new WaitForSeconds(0.15f);
            Assert.That(enemy.transform.position.z, Is.GreaterThan(before.z + 0.1f));
        }

        [UnityTest]
        public IEnumerator BlockedHitsDoNotFreezeAndAreaHitsDoNotChainStops()
        {
            PrototypeEnemy enemy = MakeEnemy(new Vector3(0f, 1.08f, 0f));
            yield return null;
            CombatDamageReceiver receiver = enemy.GetComponent<CombatDamageReceiver>();
            int stops = GameFeel.HitStops;
            receiver.Receive(5f, CombatDamageKind.Magic, Vector3.zero);
            receiver.Receive(5f, CombatDamageKind.Magic, Vector3.zero);
            receiver.Receive(5f, CombatDamageKind.Magic, Vector3.zero);
            Assert.That(GameFeel.HitStops, Is.EqualTo(stops + 1), "Un area que golpea muchas veces = un solo parón.");
            yield return new WaitForSecondsRealtime(0.2f);
        }

        [UnityTest]
        public IEnumerator PauseIsNeverUndoneByAHitStop()
        {
            PrototypeEnemy enemy = MakeEnemy(new Vector3(0f, 1.08f, 0f));
            yield return null;
            enemy.GetComponent<CombatDamageReceiver>().Receive(20f, CombatDamageKind.Melee, Vector3.zero);
            Time.timeScale = 0f;   // el jugador pausa durante el parón
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Time.timeScale, Is.Zero, "La pausa se respeta.");
            Time.timeScale = 1f;
        }

        [Test]
        public void EverySoundIsSynthesizedAndAudible()
        {
            foreach (GameSound sound in System.Enum.GetValues(typeof(GameSound)))
            {
                AudioClip clip = GameAudio.Clip(sound);
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.length, Is.GreaterThan(0.05f), sound.ToString());
                float[] data = new float[clip.samples];
                clip.GetData(data, 0);
                float peak = 0f;
                foreach (float sample in data)
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                Assert.That(peak, Is.InRange(0.05f, 1f), sound + " debe oirse sin saturar.");
            }
        }

        [UnityTest]
        public IEnumerator FootstepsEveryFewMetersAndDustOnDash()
        {
            PlayerMovementFeedback feedback = player.AddComponent<PlayerMovementFeedback>();
            yield return null;
            int steps = feedback.Steps;
            for (int i = 0; i < 20; i++)
                feedback.Tick(new Vector3(0.2f, 0f, 0f), false, true);
            Assert.That(feedback.Steps - steps, Is.EqualTo(2), "4 m = 2 pasos de 1,7 m.");
            feedback.Tick(new Vector3(0.3f, 0f, 0f), true, true);
            feedback.Tick(new Vector3(0.3f, 0f, 0f), true, true);
            Assert.That(feedback.Dashes, Is.EqualTo(1), "Una esquiva = una nube de polvo.");
        }
    }
}
