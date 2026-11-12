using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class MagicPlayModeTests
    {
        private GameObject ground;
        private GameObject player;
        private GameObject target;
        private GameObject wall;
        private PlayerSpellbook book;
        private PlayerFeeding feeding;
        private CombatHealth playerHealth;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "SueloMagia";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);

            player = new GameObject("MagoPrueba");
            player.transform.position = new Vector3(0f, 1.08f, 0f);
            CharacterController body = player.AddComponent<CharacterController>();
            body.height = 2f;
            body.radius = 0.4f;
            playerHealth = player.AddComponent<CombatHealth>();
            player.AddComponent<CombatDefense>();
            player.AddComponent<CombatDamageReceiver>();
            player.AddComponent<PlayerActionModifiers>();
            player.AddComponent<PlayerDamageTracker>();
            book = player.AddComponent<PlayerSpellbook>();
            feeding = player.AddComponent<PlayerFeeding>();
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (SpellProjectile p in Object.FindObjectsByType<SpellProjectile>(FindObjectsSortMode.None))
                Object.Destroy(p.gameObject);
            if (wall != null) Object.Destroy(wall);
            if (target != null) Object.Destroy(target);
            if (player != null) Object.Destroy(player);
            if (ground != null) Object.Destroy(ground);
            yield return null;
        }

        private CombatHealth MakeTarget(Vector3 position)
        {
            target = new GameObject("ObjetivoMagia");
            target.transform.position = position;
            CapsuleCollider capsule = target.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.45f;
            CombatHealth health = target.AddComponent<CombatHealth>();
            target.AddComponent<CombatDamageReceiver>();
            target.AddComponent<EssenceSource>().Configure(EssenceKind.Sage, 70);
            Physics.SyncTransforms();
            return health;
        }

        private void AddOwnColliderClutter(int count, float maxZ)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject clutter = new GameObject("Collider propio " + i);
                clutter.transform.SetParent(player.transform, false);
                clutter.transform.localPosition = new Vector3(
                    (i % 3 - 1) * 0.04f, 0f,
                    Mathf.Lerp(0.18f, maxZ, (i + 1f) / count));
                BoxCollider box = clutter.AddComponent<BoxCollider>();
                box.size = new Vector3(0.28f, 0.7f, 0.12f);
            }
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator FireBoltTravelsHitsOnceAndIgnites()
        {
            CombatHealth victim = MakeTarget(new Vector3(0f, 1.08f, 5f));
            Assert.That(book.TryCast(0, victim.transform.position), Is.True);
            yield return new WaitForSeconds(0.6f);
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f - 18f).Within(0.01f));
            StatusEffects status = target.GetComponent<StatusEffects>();
            Assert.That(status != null && status.Has(StatusKind.Ignition), Is.True);
            Assert.That(player.GetComponent<PlayerDamageTracker>().DamageFor(CombatDamageKind.Magic),
                Is.EqualTo(18f).Within(0.01f), "Solo se registra el daño aplicado.");
            Assert.That(book.TargetsHit, Is.EqualTo(1));
            // La quemadura sigue haciendo daño por segundo.
            float before = victim.CurrentHealth;
            yield return new WaitForSeconds(1.1f);
            Assert.That(victim.CurrentHealth, Is.LessThan(before));
        }

        [UnityTest]
        public IEnumerator BoltsHaveTwoChargesThenExplainTheCooldown()
        {
            Vector3 aim = new Vector3(0f, 1.08f, 10f);
            Assert.That(book.TryCast(0, aim), Is.True);
            yield return new WaitForSeconds(0.15f);
            Assert.That(book.TryCast(0, aim), Is.True);
            yield return new WaitForSeconds(0.15f);
            Assert.That(book.TryCast(0, aim), Is.False);
            StringAssert.Contains("recarga", book.LastMessage);
            book.ResetCooldowns();
            Assert.That(book.TryCast(0, aim), Is.True);
        }

        [UnityTest]
        public IEnumerator ThreeChillStacksFreezeAndCurseAmplifiesDamage()
        {
            CombatHealth victim = MakeTarget(new Vector3(0f, 1.08f, 3f));
            yield return null;
            StatusEffects status = StatusEffects.For(victim);
            status.Apply(StatusKind.Chill);
            status.Apply(StatusKind.Chill);
            Assert.That(status.ChillStacks, Is.EqualTo(2));
            Assert.That(status.IsFrozen, Is.False);
            status.Apply(StatusKind.Chill);
            Assert.That(status.IsFrozen, Is.True, "La tercera carga congela.");
            Assert.That(status.ChillStacks, Is.Zero);

            status.Apply(StatusKind.Curse);
            CombatDamageResult hit = target.GetComponent<CombatDamageReceiver>()
                .Receive(20f, CombatDamageKind.Magic, player.transform.position);
            Assert.That(hit.Applied, Is.EqualTo(25f).Within(0.01f));

            status.Apply(StatusKind.Dazzle);
            Assert.That(StatusEffects.OutgoingFor(victim), Is.EqualTo(StatusEffects.DazzleDamageDealt));
        }

        [UnityTest]
        public IEnumerator StaticDischargesThroughReceiverAndRespectsMagicResistance()
        {
            CombatHealth victim = MakeTarget(new Vector3(0f, 1.08f, 3f));
            CombatDamageReceiver receiver = target.GetComponent<CombatDamageReceiver>();
            receiver.ConfigureResistances(0f, 0f, 0.5f);
            yield return null;
            StatusEffects.For(victim).Apply(StatusKind.Static);
            SpellDefinition frost = SpellCatalog.Find("lanza_hielo");
            float dealt = book.ApplySpellHit(receiver, frost, 1f, player.transform.position);
            float expected = (frost.Damage + StatusEffects.StaticBonusDamage) * 0.5f;
            Assert.That(dealt, Is.EqualTo(expected).Within(0.01f),
                "La descarga extra debe respetar la misma resistencia mágica que el hechizo.");
            Assert.That(StatusEffects.For(victim).Has(StatusKind.Static), Is.False);
        }

        [UnityTest]
        public IEnumerator IgnitionTicksThroughReceiverAndRespectsMagicResistance()
        {
            CombatHealth victim = MakeTarget(new Vector3(0f, 1.08f, 3f));
            CombatDamageReceiver receiver = target.GetComponent<CombatDamageReceiver>();
            receiver.ConfigureResistances(0f, 0f, 0.5f);
            StatusEffects status = StatusEffects.For(victim);
            Assert.That(status.ApplyFrom(StatusKind.Ignition, player.transform.position), Is.True);

            float dealt = status.TickIgnition(Time.time + 1.01f);
            Assert.That(dealt, Is.EqualTo(StatusEffects.IgnitionDps * 0.5f).Within(0.01f));
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f - dealt).Within(0.01f));
            PlayerDamageTracker tracker = player.GetComponent<PlayerDamageTracker>();
            Assert.That(tracker.DamageFor(CombatDamageKind.DamageOverTime),
                Is.EqualTo(dealt).Within(0.01f),
                "La quemadura también debe aparecer en el daño total del jugador.");
            yield break;
        }

        [UnityTest]
        public IEnumerator SaturatedProjectileQueryStillStopsAtNearestWall()
        {
            AddOwnColliderClutter(28, 1.4f);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.08f, 2.5f);
            wall.transform.localScale = new Vector3(5f, 3f, 0.35f);
            Physics.SyncTransforms();

            SpellDefinition fire = SpellCatalog.Find("saeta_ignea");
            SpellProjectile projectile = SpellProjectile.Launch(book, fire, 1f,
                player.transform.position + Vector3.up * 0.15f, Vector3.forward);
            projectile.Step(1f);

            Assert.That(projectile.IsDone, Is.True);
            Assert.That(projectile.LastImpactPoint.z, Is.LessThan(2.5f),
                "Aunque el buffer se llene con colliders propios, la pared real sigue siendo el primer impacto.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaturatedAreaLineOfSightCannotDamageThroughWall()
        {
            CombatHealth victim = MakeTarget(new Vector3(0f, 1.08f, 3.1f));
            AddOwnColliderClutter(28, 1.1f);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.08f, 1.8f);
            wall.transform.localScale = new Vector3(5f, 3f, 0.3f);
            Physics.SyncTransforms();

            Assert.That(book.TryCast(1, victim.transform.position), Is.True);
            yield return new WaitForSeconds(0.5f);
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f).Within(0.01f),
                "Un Nova no puede atravesar una pared aunque RaycastNonAlloc se sature.");
        }

        [UnityTest]
        public IEnumerator AegisBarrierAbsorbsBeforeHealth()
        {
            book.Unlock(SpellSchool.Lumancy);
            Assert.That(book.SelectSchool(SpellSchool.Lumancy), Is.True);
            Assert.That(book.TryCast(1, player.transform.position), Is.True);
            yield return null;
            ArcaneBarrier barrier = player.GetComponent<ArcaneBarrier>();
            Assert.That(barrier != null && barrier.Active, Is.True);
            player.GetComponent<CombatDamageReceiver>().Receive(30f, CombatDamageKind.Melee, Vector3.forward * 3f);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f).Within(0.01f));
            player.GetComponent<CombatDamageReceiver>().Receive(30f, CombatDamageKind.Melee, Vector3.forward * 3f);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(80f).Within(0.01f), "Égida absorbe 40 en total.");
        }

        [UnityTest]
        public IEnumerator SchoolsLockedUntilLearnedAndNoSwitchingInCombat()
        {
            Assert.That(book.UnlockedCount, Is.EqualTo(3));
            Assert.That(book.SelectSchool(SpellSchool.Hemomancy), Is.False);
            Assert.That(book.SelectSchool(SpellSchool.Cryomancy), Is.True);
            player.GetComponent<CombatState>().Mark();
            Assert.That(book.SelectSchool(SpellSchool.Fulmancy), Is.False);
            StringAssert.Contains("combate", book.LastMessage);
            Assert.That(book.School, Is.EqualTo(SpellSchool.Cryomancy));
            player.GetComponent<CombatState>().Leave();
            Assert.That(book.NextSchool(), Is.True);
            Assert.That(book.School, Is.EqualTo(SpellSchool.Fulmancy));
            yield break;
        }

        [UnityTest]
        public IEnumerator AbsorbingAWeakEnemyAdoptsItsEssenceAndTeachesASchool()
        {
            CombatHealth victim = MakeTarget(new Vector3(0f, 1.08f, 1.8f));
            yield return null;
            feeding.Tick(true, false, 0.1f);
            Assert.That(feeding.IsChanneling, Is.False, "Con vida alta no se puede absorber.");
            feeding.Tick(false, false, 0.1f);

            victim.ApplyDamage(75f);
            playerHealth.ApplyDamage(30f);
            feeding.Tick(true, false, 0.1f);
            Assert.That(feeding.IsChanneling, Is.True);
            Assert.That(player.GetComponent<PlayerActionModifiers>().AllowsAttack, Is.False);
            feeding.Tick(true, false, 1.2f);
            feeding.Tick(true, false, 1.2f);

            Assert.That(feeding.IsChanneling, Is.False);
            Assert.That(victim.IsDefeated, Is.True);
            EssenceReservoir reservoir = player.GetComponent<EssenceReservoir>();
            Assert.That(reservoir.Kind, Is.EqualTo(EssenceKind.Sage));
            Assert.That(reservoir.Quality, Is.EqualTo(70));
            Assert.That(book.IsUnlocked(SpellSchool.Lumancy), Is.True, "La esencia Sabia enseña Lumancia.");
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(85f).Within(0.01f), "Absorber cura un 15%.");
            Assert.That(player.GetComponent<PlayerActionModifiers>().AllowsAttack, Is.True);
        }

        [UnityTest]
        public IEnumerator ExecutingDuringAbsorbKillsWithoutEssenceAndReleasingCancels()
        {
            CombatHealth victim = MakeTarget(new Vector3(0f, 1.08f, 1.8f));
            yield return null;
            victim.ApplyDamage(80f);
            feeding.Tick(true, false, 0.1f);
            Assert.That(feeding.IsChanneling, Is.True);
            feeding.Tick(false, false, 0.1f);
            Assert.That(feeding.IsChanneling, Is.False);
            StringAssert.Contains("soltaste", feeding.LastMessage);

            feeding.Tick(false, false, 0.1f);
            feeding.Tick(true, false, 0.1f);
            Assert.That(feeding.IsChanneling, Is.True);
            feeding.Tick(true, true, 0.1f);
            Assert.That(victim.IsDefeated, Is.True);
            Assert.That(player.GetComponent<EssenceReservoir>().Kind, Is.EqualTo(EssenceKind.None));
            Assert.That(feeding.Executed, Is.EqualTo(1));
            yield return null;
            feeding.Tick(false, false, 0.1f);
            Assert.That(player.GetComponent<PlayerActionModifiers>().AllowsAttack, Is.True,
                "El bloqueo de ataque se libera al frame siguiente.");
        }

        [UnityTest]
        public IEnumerator HeavyHitInterruptsAbsorbing()
        {
            CombatHealth victim = MakeTarget(new Vector3(0f, 1.08f, 1.8f));
            yield return null;
            victim.ApplyDamage(80f);
            feeding.Tick(true, false, 0.1f);
            Assert.That(feeding.IsChanneling, Is.True);
            player.GetComponent<CombatDamageReceiver>().Receive(10f, CombatDamageKind.Melee, Vector3.forward);
            Assert.That(feeding.IsChanneling, Is.False, "Expuesto: 10 x 1,3 = 13 PV supera el 12%.");
            Assert.That(victim.IsDefeated, Is.False);
        }

        [UnityTest]
        public IEnumerator ShadowStepStopsAtWallsAndRecastReturns()
        {
            book.Unlock(SpellSchool.Umbramancy);
            book.SelectSchool(SpellSchool.Umbramancy);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, 4f);
            wall.transform.localScale = new Vector3(6f, 3f, 0.5f);
            Physics.SyncTransforms();
            Vector3 start = player.transform.position;
            Assert.That(book.TryCast(1, start + Vector3.forward * 7f), Is.True);
            yield return null;
            Assert.That(player.transform.position.z, Is.LessThan(3.8f), "No atraviesa la pared.");
            Assert.That(player.transform.position.z, Is.GreaterThan(2f));
            Assert.That(book.TryCast(1, start), Is.True, "Relanzar te devuelve.");
            yield return null;
            Assert.That(Vector3.Distance(player.transform.position, start), Is.LessThan(0.3f));
        }

        [UnityTest]
        public IEnumerator OutOfCombatRegeneratesButNotInCombat()
        {
            playerHealth.ApplyDamage(20f);
            CombatState state = player.GetComponent<CombatState>();
            Assert.That(state.InCombat, Is.True, "Recibir daño entra en combate.");
            Assert.That(state.Regenerate(2f), Is.Zero);
            state.Leave();
            Assert.That(state.Regenerate(1f), Is.EqualTo(CombatState.RegenPerSecond).Within(0.01f));
            yield break;
        }
    }
}
