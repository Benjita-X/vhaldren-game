using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    public sealed class Integration017PlayModeTests
    {
        private GameObject player;
        private GameObject sessionObject;
        private GameObject board;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (sessionObject != null) Object.Destroy(sessionObject);
            if (board != null) Object.Destroy(board);
            if (player != null) Object.Destroy(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PotionReturnsExactMissingHealthAndDoesNotWasteFullPotion()
        {
            player = new GameObject("PlayerRoot");
            CombatHealth health = player.AddComponent<CombatHealth>();
            sessionObject = new GameObject("TestSession");
            sessionObject.SetActive(false);
            PrototypeGameSession session = sessionObject.AddComponent<PrototypeGameSession>();
            typeof(PrototypeGameSession).GetField("player",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, player.transform);
            sessionObject.SetActive(true);
            yield return null;

            health.ApplyDamage(25f);
            Assert.That(health.CurrentHealth, Is.EqualTo(75f));
            Assert.That(session.TryDrinkPotion(), Is.EqualTo(25f),
                "Si faltan 25 PV, una pocion de hasta 35 debe curar 25.");
            Assert.That(session.LastPotionHealed, Is.EqualTo(25f));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(session.TryDrinkPotion(), Is.Zero,
                "No consumir ni anunciar una segunda pocion en cooldown.");
        }

        [UnityTest]
        public IEnumerator BuildingModeDisablesDynamicallyAddedAbilitiesAndRestoresThem()
        {
            player = new GameObject("PlayerRoot");
            player.AddComponent<CombatHealth>();
            player.AddComponent<PlayerActionModifiers>();
            player.AddComponent<SurvivalInventory>();
            PlayerBuilder builder = player.AddComponent<PlayerBuilder>();

            // Reproduce el orden de Awake/OnEnable de PrototypeGameSession:
            // sistemas de habilidades añadidos DESPUES de PlayerBuilder.Awake.
            player.AddComponent<CombatDefense>();
            PlayerCombatAbilities abilities = player.AddComponent<PlayerCombatAbilities>();
            PlayerDefensiveInput defenseInput = player.AddComponent<PlayerDefensiveInput>();
            yield return null;

            Assert.That(abilities.enabled, Is.True);
            Assert.That(defenseInput.enabled, Is.True);
            builder.SetBuildMode(true);
            Assert.That(abilities.enabled, Is.False);
            Assert.That(defenseInput.enabled, Is.False);
            Assert.That(abilities.TryRunePulse(), Is.False,
                "Los hechizos no pueden ejecutarse mientras se construye.");
            builder.SetBuildMode(false);
            Assert.That(abilities.enabled, Is.True);
            Assert.That(defenseInput.enabled, Is.True);
            Assert.That(abilities.TryRunePulse(), Is.True,
                "Al salir de construccion se restauran los controles de magia.");
        }

        [UnityTest]
        public IEnumerator VillageQuestRewardsRequireRealInventoryAndDoNotDuplicate()
        {
            player = new GameObject("PlayerRoot");
            SurvivalInventory inventory = player.AddComponent<SurvivalInventory>();
            VillageQuestJournal journal = player.AddComponent<VillageQuestJournal>();
            yield return null;

            Assert.That(journal.State, Is.EqualTo(VillageQuestState.Available));
            Assert.That(journal.TryClaim(), Is.False);
            Assert.That(journal.TryAccept(), Is.True);
            int wood = inventory.Count(ResourceKind.Wood);
            Assert.That(journal.Progress, Is.GreaterThanOrEqualTo(6));
            Assert.That(journal.TryClaim(), Is.True);
            Assert.That(inventory.Count(ResourceKind.Wood), Is.EqualTo(wood - 6));
            Assert.That(journal.Reputation(SettlementCulture.Mixed), Is.EqualTo(2));
            Assert.That(journal.TryClaim(), Is.False,
                "No reclamar dos veces el mismo capitulo.");

            Assert.That(journal.TryAccept(), Is.True);
            int stone = inventory.Count(ResourceKind.Stone);
            Assert.That(journal.TryClaim(), Is.True);
            Assert.That(inventory.Count(ResourceKind.Stone), Is.EqualTo(stone - 5));
            Assert.That(journal.Reputation(SettlementCulture.Human), Is.EqualTo(2));

            Assert.That(journal.TryAccept(), Is.True);
            Assert.That(journal.RecordAction(VillageObjective.CastMagic), Is.False);
            Assert.That(journal.RecordAction(VillageObjective.DefeatGuardians), Is.True);
            Assert.That(journal.State, Is.EqualTo(VillageQuestState.Active));
            Assert.That(journal.RecordAction(VillageObjective.DefeatGuardians), Is.True);
            Assert.That(journal.State, Is.EqualTo(VillageQuestState.ReadyToClaim));
            Assert.That(journal.TryClaim(), Is.True);
            Assert.That(journal.CompletedChapters, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator QuestBoardInteractsOnlyNearPlayer()
        {
            player = new GameObject("PlayerRoot");
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.AddComponent<SurvivalInventory>();
            VillageQuestJournal journal = player.AddComponent<VillageQuestJournal>();
            board = new GameObject("TablonTest");
            board.transform.position = new Vector3(0f, 0f, 4f);
            VillageNoticeboard notice = board.AddComponent<VillageNoticeboard>();
            yield return null;

            Assert.That(notice.PlayerNearby, Is.True);
            Assert.That(notice.TryInteract(), Is.True);
            Assert.That(journal.State, Is.EqualTo(VillageQuestState.ReadyToClaim));
            player.transform.position = new Vector3(40f, 0f, 0f);
            Assert.That(notice.TryInteract(), Is.False);
        }
    }
}
