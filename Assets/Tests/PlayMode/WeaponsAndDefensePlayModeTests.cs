using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class WeaponsAndDefensePlayModeTests
    {
        private GameObject owner;
        private GameObject target;
        private GameObject blocker;
        private GameObject floor;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (owner != null) Object.Destroy(owner);
            if (target != null) Object.Destroy(target);
            if (blocker != null) Object.Destroy(blocker);
            if (floor != null) Object.Destroy(floor);
            yield return null;
        }

        private CombatDamageReceiver PrepareDefense(out CombatHealth health,
            out CombatDefense defense)
        {
            target = new GameObject("Defender");
            target.transform.position = Vector3.zero;
            health = target.AddComponent<CombatHealth>();
            defense = target.AddComponent<CombatDefense>();
            CombatDamageReceiver receiver = target.AddComponent<CombatDamageReceiver>();
            return receiver;
        }

        [UnityTest]
        public IEnumerator ShieldBlocksSwordAndArrowsButNotMagic()
        {
            CombatDamageReceiver receiver = PrepareDefense(out CombatHealth health,
                out CombatDefense defense);
            Assert.That(defense.SetGuard(CombatDefense.GuardMode.PhysicalShield), Is.True);
            Vector3 front = Vector3.forward * 4f;
            CombatDamageResult melee = receiver.Receive(20f, CombatDamageKind.Melee, front);
            CombatDamageResult arrow = receiver.Receive(20f, CombatDamageKind.Arrow, front);
            CombatDamageResult magic = receiver.Receive(20f, CombatDamageKind.Magic, front);
            Assert.That(melee.Applied, Is.EqualTo(4.4f).Within(0.01f));
            Assert.That(arrow.Applied, Is.EqualTo(9.6f).Within(0.01f));
            Assert.That(magic.Applied, Is.EqualTo(20f).Within(0.01f));
            Assert.That(health.CurrentHealth, Is.EqualTo(66f).Within(0.02f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WardReducesMagicButNotSword()
        {
            CombatDamageReceiver receiver = PrepareDefense(out _, out CombatDefense defense);
            Assert.That(defense.SetGuard(CombatDefense.GuardMode.ArcaneWard), Is.True);
            Vector3 front = Vector3.forward * 4f;
            Assert.That(receiver.Receive(25f, CombatDamageKind.Magic, front).Applied,
                Is.EqualTo(5f).Within(0.01f));
            Assert.That(receiver.Receive(25f, CombatDamageKind.Melee, front).Applied,
                Is.EqualTo(25f).Within(0.01f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ParryWorksOnlyOnceFromFrontAndBackstabBypassesShield()
        {
            CombatDamageReceiver receiver = PrepareDefense(out _, out CombatDefense defense);
            Assert.That(defense.TryParry(), Is.True);
            CombatDamageResult blocked = receiver.Receive(18f,
                CombatDamageKind.Arrow, Vector3.forward * 4f);
            Assert.That(blocked.Applied, Is.Zero);
            Assert.That(blocked.Parried, Is.True);
            Assert.That(receiver.Receive(18f, CombatDamageKind.Arrow,
                Vector3.forward * 4f).Applied, Is.EqualTo(18f).Within(0.01f));

            Assert.That(defense.SetGuard(CombatDefense.GuardMode.PhysicalShield), Is.True);
            Assert.That(receiver.Receive(18f, CombatDamageKind.Melee,
                Vector3.back * 4f).Applied, Is.EqualTo(18f).Within(0.01f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DashEvadesArrowAndMagic()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.55f, 0f);
            floor.transform.localScale = new Vector3(30f, 1f, 30f);
            target = new GameObject("PlayerRoot");
            target.transform.position = new Vector3(0f, 1.08f, 0f);
            CharacterController body = target.AddComponent<CharacterController>();
            body.height = 2f;
            target.AddComponent<CombatHealth>();
            PlayerMotor motor = target.AddComponent<PlayerMotor>();
            target.AddComponent<CombatDefense>();
            CombatDamageReceiver receiver = target.AddComponent<CombatDamageReceiver>();

            motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward, true),
                1f / 60f);
            Assert.That(motor.IsDashing, Is.True);
            Assert.That(receiver.Receive(18f, CombatDamageKind.Arrow,
                Vector3.forward * 3f).Evaded, Is.True);
            Assert.That(receiver.Receive(18f, CombatDamageKind.Magic,
                Vector3.forward * 3f).Applied, Is.Zero);
            Assert.That(target.GetComponent<CombatHealth>().CurrentHealth, Is.EqualTo(100f));
            yield return null;
        }

        private CombatHealth SetupArrowRange(out Vector3 origin)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.55f, 0f);
            floor.transform.localScale = new Vector3(30f, 1f, 30f);

            owner = new GameObject("Archer");
            owner.transform.position = new Vector3(0f, 1.0f, 0f);
            origin = owner.transform.position;

            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "EnemyTarget";
            target.transform.position = new Vector3(0f, 1.0f, 4f);
            target.transform.localScale = new Vector3(0.8f, 1.4f, 0.8f);
            CombatHealth health = target.AddComponent<CombatHealth>();
            target.AddComponent<CombatDamageReceiver>();
            Physics.SyncTransforms();
            return health;
        }

        [UnityTest]
        public IEnumerator CrossbowArrowTravelsBeforeApplyingDamage()
        {
            CombatHealth health = SetupArrowRange(out Vector3 origin);
            PhysicalArrowProjectile arrow = PhysicalArrowProjectile.Spawn(
                owner.transform, origin, Vector3.forward, 25f, 9f, 12f, null, 0f);
            Assert.That(arrow, Is.Not.Null);
            Assert.That(arrow.IsInFlight, Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f),
                "Disparar no aplica daño instantaneo.");
            yield return new WaitForSeconds(0.15f);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f),
                "Antes de llegar al objetivo no debe registrar impacto.");
            yield return new WaitForSeconds(0.55f);
            Assert.That(health.CurrentHealth, Is.EqualTo(75f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator CrossbowArrowCannotPassThroughStoneWall()
        {
            CombatHealth health = SetupArrowRange(out Vector3 origin);
            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = new Vector3(0f, 1f, 2f);
            blocker.transform.localScale = new Vector3(2f, 3f, 0.25f);
            Physics.SyncTransforms();

            PhysicalArrowProjectile.Spawn(owner.transform, origin,
                Vector3.forward, 30f, 12f, 12f, null, 0f);
            yield return new WaitForSeconds(0.6f);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f),
                "La flecha se detiene en la primera pared, no puede herir al enemigo detras.");
        }

        [UnityTest]
        public IEnumerator HeavySwordAbilityUsesMeleeDamageAndSingleTargetHit()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.55f, 0f);
            floor.transform.localScale = new Vector3(30f, 1f, 30f);

            owner = new GameObject("PlayerRoot");
            owner.transform.position = new Vector3(0f, 1.08f, 0f);
            CharacterController body = owner.AddComponent<CharacterController>();
            body.height = 2f;
            owner.AddComponent<CombatHealth>();
            owner.AddComponent<PlayerMotor>();
            owner.AddComponent<CombatDefense>();
            PlayerCombatAbilities skills = owner.AddComponent<PlayerCombatAbilities>();

            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0f, 1.1f, 1.9f);
            target.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);
            CombatHealth health = target.AddComponent<CombatHealth>();
            target.AddComponent<CombatDamageReceiver>();
            Physics.SyncTransforms();
            Assert.That(skills.TrySwordSweep(), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));

            yield return new WaitForSeconds(0.24f);
            Assert.That(health.CurrentHealth, Is.LessThan(100f),
                "V debe golpear despues de preparar el tajo, no al pulsar.");
            Assert.That(skills.SwordTargetsHit, Is.EqualTo(1));
        }
    }
}
