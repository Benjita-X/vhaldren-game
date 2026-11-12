using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    public sealed class Consolidated018PlayModeTests
    {
        private GameObject owner;
        private GameObject sceneObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (sceneObject != null)
                Object.Destroy(sceneObject);
            if (owner != null)
                Object.Destroy(owner);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CreativeBuildingDoesNotConsumeOrGenerateSurvivalResources()
        {
            owner = new GameObject("PlayerRoot");
            owner.transform.position = new Vector3(0f, 1.08f, -6f);
            owner.AddComponent<PlayerActionModifiers>();
            owner.AddComponent<CombatHealth>();
            SurvivalInventory inventory = owner.AddComponent<SurvivalInventory>();
            PlayerBuilder builder = owner.AddComponent<PlayerBuilder>();

            sceneObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sceneObject.transform.position = new Vector3(0f, -0.5f, 0f);
            sceneObject.transform.localScale = new Vector3(24f, 1f, 24f);
            yield return null;

            int stone = inventory.Count(ResourceKind.Stone);
            int wood = inventory.Count(ResourceKind.Wood);
            // Explicitly toggle by assigning the testing-only backing field
            // via the auto property's compiler-generated field. Only this
            // test uses reflection; gameplay toggles creative mode with F9.
            typeof(PlayerBuilder).GetField("<CreativeMode>k__BackingField",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(builder, true);

            BuiltPiece piece = builder.TryPlace(0,
                new Vector3(1f, 0f, 1f), 0, out PlacementResult outcome);
            Assert.That(outcome, Is.EqualTo(PlacementResult.Ok));
            Assert.That(piece, Is.Not.Null);
            Assert.That(piece.MaterialsPaid, Is.False,
                "Una pieza creada sin gastar nada nunca debe reembolsar madera o piedra.");
            Assert.That(inventory.Count(ResourceKind.Stone), Is.EqualTo(stone));
            Assert.That(inventory.Count(ResourceKind.Wood), Is.EqualTo(wood));
            Assert.That(builder.Dismantle(piece), Is.True);
            Assert.That(builder.Dismantle(piece), Is.False);
            Assert.That(inventory.Count(ResourceKind.Stone), Is.EqualTo(stone));
            Assert.That(inventory.Count(ResourceKind.Wood), Is.EqualTo(wood));
        }

        [UnityTest]
        public IEnumerator VillageConsequencesAreAppliedOnceAndNeverLeaveValidRange()
        {
            owner = new GameObject("PlayerRoot");
            owner.AddComponent<SurvivalInventory>();
            VillageQuestJournal journal = owner.AddComponent<VillageQuestJournal>();
            sceneObject = new GameObject("TownDirector");
            SettlementState settlement = sceneObject.AddComponent<SettlementState>();
            settlement.Bind(journal);
            yield return null;

            int startingSecurity = settlement.Security;
            int startingSupplies = settlement.Supplies;
            Assert.That(journal.TryAccept(), Is.True);
            Assert.That(journal.TryClaim(), Is.True,
                "El inventario de prueba incluye madera inicial.");
            Assert.That(settlement.Supplies, Is.GreaterThan(startingSupplies));
            Assert.That(settlement.Security, Is.EqualTo(startingSecurity));

            int afterQuest = settlement.Supplies;
            // Changed puede dispararse mas veces, sin aplicar la misma recompensa.
            journal.TryClaim();
            Assert.That(settlement.Supplies, Is.EqualTo(afterQuest));

            for (int i = 0; i < 25; i++)
                settlement.ApplyEvent(VillageEvent.MoonlitHunt);
            Assert.That(settlement.Security, Is.EqualTo(0));
            Assert.That(settlement.CurrentSituation(), Does.Contain("RUTAS EN PELIGRO"));
        }
    }
}
