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
    /// <summary>
    /// Muerte de enemigos (animacion, ocultado diferido, reaparicion limpia), avisos que se
    /// llenan y efectos de hechizo por capas que salen de reservas en vez de crearse cada vez.
    /// </summary>
    public sealed class EnemyDeathAndVfxPlayModeTests
    {
        private const string KayKitFolder = "Assets/ThirdParty/KayKitEnemies/";
        private static readonly int MoveStateId = Animator.StringToHash("Movimiento");
        private readonly List<GameObject> spawned = new List<GameObject>();
        private GameObject player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "SueloEfectos";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(120f, 1f, 120f);
            spawned.Add(ground);

            player = new GameObject("JugadorEfectos");
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
            foreach (SpellProjectile p in Object.FindObjectsByType<SpellProjectile>(FindObjectsSortMode.None))
                Object.Destroy(p.gameObject);
            foreach (GameObject go in spawned)
                if (go != null)
                    Object.Destroy(go);
            spawned.Clear();
            yield return null;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        /// <summary>Enemigo con el esqueleto KayKit animado real (si el editor puede cargarlo).</summary>
        private PrototypeEnemy SpawnEnemy(Vector3 position, float respawnSeconds, bool kaykit, out GameObject visual)
        {
            GameObject root = new GameObject("EnemigoEfectos");
            root.SetActive(false);
            root.transform.position = position;
            CharacterController body = root.AddComponent<CharacterController>();
            body.height = 2f;
            body.radius = 0.34f;
            root.AddComponent<CombatHealth>();

            visual = new GameObject("Visual enemigo");
            visual.transform.SetParent(root.transform, false);
            if (kaykit)
            {
#if UNITY_EDITOR
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(KayKitFolder + "Skeleton_Warrior_Enemigo.prefab");
                RuntimeAnimatorController variant = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    KayKitFolder + "Skeleton_Warrior_Melee.overrideController");
                Assert.That(prefab, Is.Not.Null);
                Assert.That(variant, Is.Not.Null);
                GameObject model = Object.Instantiate(prefab, visual.transform);
                model.name = "Modelo texturizado";
                model.transform.localPosition = new Vector3(0f, -1f, 0f);
                model.GetComponent<Animator>().runtimeAnimatorController = variant;
#else
                Assert.Ignore("Solo en el editor se cargan los assets KayKit.");
#endif
            }
            else
            {
                // Cuerpo provisional sin Animator (como el lobo).
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(capsule.GetComponent<Collider>());
                capsule.transform.SetParent(visual.transform, false);
            }

            PrototypeEnemy enemy = root.AddComponent<PrototypeEnemy>();
            SetField(enemy, "target", player.transform);
            SetField(enemy, "modelRoot", visual);
            SetField(enemy, "respawnSeconds", respawnSeconds);
            root.SetActive(true);
            spawned.Add(root);
            Physics.SyncTransforms();
            return enemy;
        }

        [UnityTest]
        public IEnumerator KayKitEnemyPlaysDeathThenHidesBodyAndRespawnsClean()
        {
            PrototypeEnemy enemy = SpawnEnemy(new Vector3(40f, 1.08f, 40f), 3f, true, out GameObject visual);
            yield return null;
            yield return null;
            Vector3 restPosition = visual.transform.localPosition;
            CombatHealth health = enemy.GetComponent<CombatHealth>();
            health.ApplyDamage(9999f);

            Assert.That(enemy.IsDefeated, Is.True);
            Assert.That(enemy.GetComponent<CharacterController>().enabled, Is.False,
                "Las colisiones se apagan en el mismo instante de la muerte.");
            Assert.That(visual.activeSelf, Is.True, "El cuerpo no desaparece de golpe.");
            Assert.That(enemy.IsCorpseVisible, Is.True);
            Assert.That(enemy.UsesProceduralDeath, Is.False, "El esqueleto KayKit usa su clip Death_A.");
            EnemyAnimationDriver driver = enemy.AnimationDriver;
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.DeathsPlayed, Is.EqualTo(1));

            yield return new WaitForSeconds(0.3f);
            Assert.That(driver.InDeathState, Is.True, "El Animator esta en el estado Muerte.");
            Assert.That(visual.activeSelf, Is.True);

            yield return new WaitForSeconds(PrototypeEnemy.CorpseSeconds - 0.3f + 0.2f);
            Assert.That(visual.activeSelf, Is.False, "A los ~1,8 s el cuerpo se oculta.");
            Assert.That(enemy.IsCorpseVisible, Is.False);

            yield return new WaitForSeconds(1.2f);
            Assert.That(enemy.IsDefeated, Is.False, "Reaparecio.");
            Assert.That(visual.activeSelf, Is.True);
            Assert.That(enemy.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(driver.IsDead, Is.False);
            Assert.That(Vector3.Distance(visual.transform.localPosition, restPosition), Is.LessThan(0.001f),
                "El cuerpo hundido vuelve a su sitio.");
            yield return null;
            Assert.That(driver.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(MoveStateId),
                "Tras reaparecer vuelve al reposo/caminar, no a la pose de muerto.");
        }

        [UnityTest]
        public IEnumerator RespawnBeforeTheBodyHidesCancelsThePendingHide()
        {
            PrototypeEnemy enemy = SpawnEnemy(new Vector3(-40f, 1.08f, 40f), 1f, true, out GameObject visual);
            yield return null;
            yield return null;
            enemy.GetComponent<CombatHealth>().ApplyDamage(9999f);
            yield return new WaitForSeconds(1.15f);
            Assert.That(enemy.IsDefeated, Is.False, "Reaparece al segundo.");
            Assert.That(visual.activeSelf, Is.True);
            yield return new WaitForSeconds(PrototypeEnemy.CorpseSeconds);
            Assert.That(visual.activeSelf, Is.True, "El ocultado pendiente de la muerte anterior no se aplica.");
            Assert.That(enemy.AnimationDriver.InDeathState, Is.False);
        }

        [UnityTest]
        public IEnumerator BodyWithoutAnimatorTopplesSidewaysAndRestoresOnRespawn()
        {
            PrototypeEnemy enemy = SpawnEnemy(new Vector3(40f, 1.08f, -40f), 2.5f, false, out GameObject visual);
            yield return null;
            Quaternion rest = visual.transform.localRotation;
            enemy.GetComponent<CombatHealth>().ApplyDamage(9999f);
            Assert.That(enemy.UsesProceduralDeath, Is.True);
            yield return new WaitForSeconds(0.6f);
            Assert.That(Quaternion.Angle(rest, visual.transform.localRotation), Is.GreaterThan(60f), "Cae de lado.");
            Assert.That(visual.transform.localPosition.y, Is.LessThan(-0.3f), "Y queda tumbado en el suelo.");
            yield return new WaitForSeconds(1.5f);
            Assert.That(visual.activeSelf, Is.False);
            yield return new WaitForSeconds(0.7f);
            Assert.That(enemy.IsDefeated, Is.False);
            Assert.That(visual.activeSelf, Is.True);
            Assert.That(Quaternion.Angle(rest, visual.transform.localRotation), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator EnemyWarningDiscFillsUntilTheStrikeLands()
        {
            PrototypeEnemy enemy = SpawnEnemy(new Vector3(0f, 1.08f, 1.4f), 18f, false, out _);
            EnemyAttackTelegraph telegraph = enemy.GetComponent<EnemyAttackTelegraph>();
            yield return null;
            Assert.That(enemy.IsAttackTelegraphed, Is.True);
            Assert.That(telegraph.IsVisible, Is.True);
            Assert.That(telegraph.Fill, Is.LessThan(0.3f), "Empieza vacio.");
            yield return new WaitForSeconds(0.24f);
            Assert.That(enemy.IsAttackTelegraphed, Is.True);
            Assert.That(telegraph.Fill, Is.InRange(0.3f, 0.85f), "A mitad de la carga, medio lleno.");
            float timeout = Time.time + 1f;
            while (enemy.IsAttackTelegraphed && Time.time < timeout)
                yield return null;
            Assert.That(telegraph.Fill, Is.EqualTo(1f), "El golpe llega con el disco lleno.");
            Assert.That(player.GetComponent<CombatHealth>().CurrentHealth, Is.LessThan(100f), "Y el golpe se resolvio.");
            // Tras el destello (0,16 s) se apaga; el siguiente ataque tarda mas en empezar.
            yield return new WaitForSeconds(0.3f);
            Assert.That(enemy.IsAttackTelegraphed, Is.False);
            Assert.That(telegraph.IsVisible, Is.False, "El destello del golpe se apaga solo.");
        }

        [UnityTest]
        public IEnumerator ZoneSpellChargesInTheHandAndFillsItsGroundDisc()
        {
            PlayerSpellbook book = AddSpellbook();
            yield return null;
            Assert.That(book.TryCast(2, player.transform.position + Vector3.forward * 6f), Is.True, "Meteoro.");
            yield return new WaitForSeconds(0.1f);
            SpellCastCharge charge = player.GetComponent<SpellCastCharge>();
            Assert.That(charge, Is.Not.Null);
            Assert.That(charge.IsCharging, Is.True);
            Assert.That(charge.LiveParticles, Is.GreaterThan(0), "Particulas convergiendo en la mano.");
            GroundTelegraph disc = ActiveTelegraph();
            Assert.That(disc, Is.Not.Null, "El meteoro avisa con un disco en el suelo.");
            Assert.That(disc.Radius, Is.EqualTo(SpellCatalog.Find("meteoro").Radius).Within(0.01f));
            float early = disc.Fill;
            yield return new WaitForSeconds(0.5f);
            Assert.That(disc.Fill, Is.GreaterThan(early + 0.25f), "El relleno crece durante la preparacion.");
            yield return new WaitForSeconds(0.75f);
            Assert.That(charge.IsCharging, Is.False);
            Assert.That(charge.Releases, Is.EqualTo(1), "Destello al salir el hechizo.");
            yield return new WaitForSeconds(GroundTelegraph.FinishSeconds + 0.1f);
            Assert.That(ActiveTelegraph(), Is.Null, "El aviso vuelve a su reserva.");
        }

        [UnityTest]
        public IEnumerator ProjectilesAndImpactsAreReusedInsteadOfRecreated()
        {
            PlayerSpellbook book = AddSpellbook();
            yield return null;
            Vector3 aim = player.transform.position + Vector3.forward * 30f;
            Assert.That(book.TryCast(0, aim), Is.True);
            yield return new WaitForSeconds(0.2f);
            SpellProjectile flying = Object.FindAnyObjectByType<SpellProjectile>();
            Assert.That(flying, Is.Not.Null);
            Assert.That(flying.Visual.Lit, Is.True, "Nucleo, halo, cuerpo, estela y brasas activos.");
            Assert.That(VfxHost.Get().LiveParticles, Is.GreaterThan(0), "Deja brasas por el camino.");
            // Alcance 18 m a 24 m/s + estela: menos de 1,2 s hasta volver a la reserva.
            yield return new WaitForSeconds(1.2f);
            int projectiles = CountAll<SpellProjectile>();
            int impacts = CountAll<SpellImpactFlash>();
            for (int round = 0; round < 3; round++)
            {
                book.ResetCooldowns();
                Assert.That(book.TryCast(0, aim), Is.True);
                yield return new WaitForSeconds(1.2f);
            }
            Assert.That(CountAll<SpellProjectile>(), Is.EqualTo(projectiles), "Sin proyectiles nuevos: se reutilizan.");
            Assert.That(CountAll<SpellImpactFlash>(), Is.EqualTo(impacts), "Sin impactos nuevos: se reutilizan.");
        }

        [UnityTest]
        public IEnumerator EveryElementBurstsWithItsOwnParticles()
        {
            VfxHost host = VfxHost.Get();
            HashSet<VfxElement> seen = new HashSet<VfxElement>();
            foreach (SpellSchool school in System.Enum.GetValues(typeof(SpellSchool)))
                // La Luz caída de Lucifer mezcla a propósito luz y fuego infernal (reutiliza su elemento).
                if (!SpellCatalog.IsExclusive(school))
                Assert.That(seen.Add(SpellVfx.ElementOf(school)), Is.True, school + " necesita su propio elemento.");
            foreach (VfxElement element in System.Enum.GetValues(typeof(VfxElement)))
            {
                int before = host.LiveParticles;
                SpellImpactFlash flash = SpellImpactFlash.Spawn(new Vector3(10f, 1f, 10f), Color.cyan, 0.8f, 0.3f, 2f, element);
                Assert.That(flash, Is.Not.Null);
                Assert.That(flash.Element, Is.EqualTo(element));
                Assert.That(flash.HasGroundWave, Is.True);
                Assert.That(host.LiveParticles, Is.GreaterThan(before), element + " emite particulas.");
            }
            yield return null;
        }

        private PlayerSpellbook AddSpellbook()
        {
            player.AddComponent<CombatDefense>();
            player.AddComponent<Vhaldren.Gameplay.Player.PlayerActionModifiers>();
            player.AddComponent<PlayerDamageTracker>();
            return player.AddComponent<PlayerSpellbook>();
        }

        private static GroundTelegraph ActiveTelegraph()
        {
            foreach (GroundTelegraph telegraph in Object.FindObjectsByType<GroundTelegraph>(FindObjectsSortMode.None))
                if (telegraph.Showing)
                    return telegraph;
            return null;
        }

        private static int CountAll<T>() where T : Component =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    }
}
