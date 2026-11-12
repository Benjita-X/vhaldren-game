using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Progression;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    public sealed class ProgressionPlayModeTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private GameObject player;
        private SurvivalInventory inventory;
        private PlayerEquipment equipment;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            spawned.Add(ground);
            player = new GameObject("JugadorProgresion");
            player.transform.position = new Vector3(0f, 1.08f, 0f);
            player.AddComponent<CharacterController>();
            player.AddComponent<CombatHealth>();
            player.AddComponent<CombatDamageReceiver>();
            player.AddComponent<PlayerActionModifiers>();
            inventory = player.AddComponent<SurvivalInventory>();
            equipment = player.AddComponent<PlayerEquipment>();
            spawned.Add(player);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (BuiltPiece piece in Object.FindObjectsByType<BuiltPiece>(FindObjectsSortMode.None))
                Object.Destroy(piece.gameObject);
            foreach (GameObject go in spawned)
                if (go != null)
                    Object.Destroy(go);
            spawned.Clear();
            yield return null;
            Assert.That(PlayerEquipment.Local, Is.Null, "No debe quedar equipo activo para otras pruebas.");
        }

        private PrototypeEnemy Spawn(string archetype, Vector3 position, int level)
        {
            GameObject root = new GameObject("Enemigo " + archetype);
            root.SetActive(false);
            root.transform.position = position;
            root.AddComponent<CharacterController>();
            root.AddComponent<CombatHealth>();
            PrototypeEnemy enemy = root.AddComponent<PrototypeEnemy>();
            typeof(PrototypeEnemy).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, player.transform);
            typeof(PrototypeEnemy).GetField("archetypeId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, archetype);
            root.SetActive(true);
            root.AddComponent<CombatLevel>().Configure(level);
            root.AddComponent<EnemyLoot>();
            spawned.Add(root);
            Physics.SyncTransforms();
            return enemy;
        }

        private void Give(GearTier tier)
        {
            foreach (ResourceIngredient cost in tier.Cost)
                inventory.Add(cost.Kind, cost.Amount);
        }

        [UnityTest]
        public IEnumerator ForgingSpendsMaterialsOnlyWhenItSucceedsAndHigherTiersNeedAWorkbench()
        {
            Assert.That(equipment.GearLevel, Is.EqualTo(1));
            Assert.That(equipment.TryUpgrade(GearSlot.Weapon), Is.EqualTo(PlayerEquipment.UpgradeResult.MissingResources));

            Give(GearCatalog.Get(1));
            int hide = inventory.Count(ResourceKind.Hide);
            Assert.That(equipment.TryUpgrade(GearSlot.Weapon), Is.EqualTo(PlayerEquipment.UpgradeResult.Ok));
            Assert.That(inventory.Count(ResourceKind.Hide), Is.EqualTo(hide - 4));
            Assert.That(equipment.TierOf(GearSlot.Weapon), Is.EqualTo(1));
            Assert.That(equipment.GearLevel, Is.EqualTo(GearCatalog.GearLevel(1, 0, 0)));

            Give(GearCatalog.Get(2));
            int ore = inventory.Count(ResourceKind.Ore);
            Assert.That(equipment.TryUpgrade(GearSlot.Weapon), Is.EqualTo(PlayerEquipment.UpgradeResult.NeedsWorkbench));
            Assert.That(inventory.Count(ResourceKind.Ore), Is.EqualTo(ore), "Un intento rechazado no cobra.");

            GameObject bench = BuildPieceFactory.Create(BuildCatalog.Find("banco"), new Vector3(3f, 0f, 0f), 0, false);
            spawned.Add(bench);
            yield return null;
            Assert.That(equipment.TryUpgrade(GearSlot.Weapon), Is.EqualTo(PlayerEquipment.UpgradeResult.Ok));
            Assert.That(equipment.TierOf(GearSlot.Weapon), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ArmorRaisesMaxHealthWithoutFreeHealing()
        {
            CombatHealth health = player.GetComponent<CombatHealth>();
            health.ApplyDamage(50f);
            Give(GearCatalog.Get(1));
            Assert.That(equipment.TryUpgrade(GearSlot.Armor), Is.EqualTo(PlayerEquipment.UpgradeResult.Ok));
            Assert.That(health.MaximumHealth, Is.EqualTo(100f + GearCatalog.ArmorHealthBonus(1)));
            Assert.That(health.CurrentHealth / health.MaximumHealth, Is.EqualTo(0.5f).Within(0.001f));
            yield break;
        }

        [UnityTest]
        public IEnumerator FightingAboveYourLevelHurtsBothWays()
        {
            PrototypeEnemy elite = Spawn("centinela_runico", new Vector3(30f, 1.08f, 30f), 20);
            yield return null;
            CombatHealth eliteHealth = elite.GetComponent<CombatHealth>();
            CombatDamageResult weak = elite.GetComponent<CombatDamageReceiver>()
                .Receive(100f, CombatDamageKind.Melee, Vector3.zero);
            // Nivel 1 contra 20: 35 % del daño, y luego su resistencia fisica del 20 %.
            Assert.That(weak.Applied, Is.EqualTo(100f * 0.8f * 0.35f).Within(0.01f));
            Assert.That(CombatLevel.OutgoingFor(elite), Is.EqualTo(GearCatalog.DamageTakenMultiplier(1, 20)));
            Assert.That(CombatLevel.OutgoingFor(elite), Is.GreaterThan(1.7f));
            Assert.That(eliteHealth.IsDefeated, Is.False);
        }

        [UnityTest]
        public IEnumerator EnemiesDropTheirOwnMaterials()
        {
            PrototypeEnemy wolf = Spawn("lobo", new Vector3(20f, 1.08f, -20f), 7);
            yield return null;
            int hide = inventory.Count(ResourceKind.Hide);
            CombatHealth health = wolf.GetComponent<CombatHealth>();
            health.ApplyDamage(health.CurrentHealth);
            Assert.That(inventory.Count(ResourceKind.Hide), Is.EqualTo(hide + 2));
            Assert.That(wolf.GetComponent<EnemyLoot>().TimesLooted, Is.EqualTo(1));
            StringAssert.Contains("Piel", equipment.LastMessage);
        }

        [UnityTest]
        public IEnumerator HittingOneCampMemberAlertsTheRest()
        {
            PrototypeEnemy a = Spawn("saqueador", new Vector3(30f, 1.08f, -30f), 5);
            PrototypeEnemy b = Spawn("saqueador", new Vector3(34f, 1.08f, -30f), 5);
            PrototypeEnemy far = Spawn("saqueador", new Vector3(-30f, 1.08f, 30f), 5);
            yield return null;
            Assert.That(b.IsProvoked, Is.False);
            a.GetComponent<CombatHealth>().ApplyDamage(5f);
            Assert.That(a.IsProvoked, Is.True);
            Assert.That(b.IsProvoked, Is.True, "El compañero a 4 m acude.");
            Assert.That(far.IsProvoked, Is.False);
        }
    }
}
