using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Races;

namespace Vhaldren.Tests
{
    public sealed class RacePlayModeTests
    {
        private GameObject ground, player, target, wall;

        private PlayerRace MakePlayer(RaceId race)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            player = new GameObject("JugadorRaza");
            player.transform.position = new Vector3(0f, 1.08f, 0f);
            player.AddComponent<CharacterController>();
            player.AddComponent<CombatHealth>();
            player.AddComponent<CombatDefense>();
            player.AddComponent<CombatDamageReceiver>();
            player.AddComponent<PlayerActionModifiers>();
            player.AddComponent<PlayerDamageTracker>();
            player.AddComponent<PlayerSpellbook>();
            PlayerRace component = player.AddComponent<PlayerRace>();
            component.Configure(race);
            return component;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (target != null) Object.Destroy(target);
            if (wall != null) Object.Destroy(wall);
            if (player != null) Object.Destroy(player);
            if (ground != null) Object.Destroy(ground);
            yield return null;
            Assert.That(PlayerRace.Local, Is.Null);
        }

        [UnityTest]
        public IEnumerator WerewolfIsTougherFasterAndHitsHarderPhysically()
        {
            PlayerRace race = MakePlayer(RaceId.Werewolf);
            yield return null;
            Assert.That(player.GetComponent<CombatHealth>().MaximumHealth, Is.EqualTo(120f).Within(0.01f));
            Assert.That(player.GetComponent<CombatDefense>().MaximumStamina, Is.EqualTo(120f).Within(0.01f));
            Assert.That(player.GetComponent<PlayerActionModifiers>().SpeedMultiplier, Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(race.PhysicalMultiplier, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(race.SpellMultiplier, Is.EqualTo(0.8f).Within(0.001f));

            target = new GameObject("Muneco");
            target.AddComponent<CapsuleCollider>();
            target.AddComponent<CombatHealth>();
            target.AddComponent<RacePowerTaken>();
            CombatDamageReceiver receiver = target.AddComponent<CombatDamageReceiver>();
            Assert.That(receiver.Receive(20f, CombatDamageKind.Melee, Vector3.zero).Applied, Is.EqualTo(25f).Within(0.01f));
            Assert.That(receiver.Receive(20f, CombatDamageKind.Magic, Vector3.zero).Applied, Is.EqualTo(16f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator HalfDemonIgnoresBurnsAndArcanistLearnsMoreSchools()
        {
            MakePlayer(RaceId.HalfDemon);
            yield return null;
            StatusEffects status = StatusEffects.For(player.GetComponent<CombatHealth>());
            Assert.That(status.Apply(StatusKind.Ignition), Is.False, "Inmune a Ignicion.");
            Assert.That(status.Apply(StatusKind.Curse), Is.True);
            Object.Destroy(player);
            Object.Destroy(ground);
            yield return null;

            MakePlayer(RaceId.Arcanist);
            yield return null;
            Assert.That(player.GetComponent<PlayerSpellbook>().UnlockedCount, Is.EqualTo(5));
            Assert.That(player.GetComponent<CombatHealth>().MaximumHealth, Is.EqualTo(85f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator InfernalBurstCannotHitThroughWallEvenWhenRayBufferSaturates()
        {
            PlayerRace race = MakePlayer(RaceId.HalfDemon);
            target = new GameObject("ObjetivoDetrasPared");
            target.transform.position = new Vector3(0f, 1.08f, 3f);
            target.AddComponent<CapsuleCollider>();
            CombatHealth targetHealth = target.AddComponent<CombatHealth>();
            target.AddComponent<CombatDamageReceiver>();

            for (int i = 0; i < 24; i++)
            {
                GameObject clutter = new GameObject("Collider propio " + i);
                clutter.transform.SetParent(player.transform, false);
                clutter.transform.localPosition = new Vector3(
                    (i % 3 - 1) * 0.04f, 0f, 0.2f + i * 0.035f);
                BoxCollider box = clutter.AddComponent<BoxCollider>();
                box.size = new Vector3(0.25f, 0.8f, 0.10f);
            }

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.08f, 1.7f);
            wall.transform.localScale = new Vector3(5f, 3f, 0.3f);
            Physics.SyncTransforms();
            yield return null;

            Assert.That(race.TryUsePower(), Is.True);
            Assert.That(targetHealth.CurrentHealth, Is.EqualTo(targetHealth.MaximumHealth).Within(0.01f),
                "El estallido infernal no puede dañar a través del escenario.");
        }

        [UnityTest]
        public IEnumerator RacialPowersWorkAndRespectCooldown()
        {
            PlayerRace race = MakePlayer(RaceId.Undead);
            yield return null;
            Assert.That(race.TryUsePower(), Is.True);
            ArcaneBarrier barrier = player.GetComponent<ArcaneBarrier>();
            Assert.That(barrier, Is.Not.Null);
            Assert.That(barrier.Remaining, Is.EqualTo(110f * 0.3f).Within(0.01f));
            Assert.That(race.TryUsePower(), Is.False, "En recarga.");
            StringAssert.Contains("recarga", race.LastMessage);
        }
    }
}
