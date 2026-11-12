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
    public sealed class SurvivalBuildingPlayModeTests
    {
        private GameObject ground;
        private GameObject player;
        private GameObject extra;
        private PlayerBuilder builder;
        private SurvivalInventory inventory;

        private static int Index(string id)
        {
            for (int i = 0; i < BuildCatalog.Pieces.Count; i++)
                if (BuildCatalog.Pieces[i].Id == id)
                    return i;
            return -1;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "SueloPrueba";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            player = new GameObject("PlayerRoot");
            player.transform.position = new Vector3(0f, 1.08f, -6f);
            CharacterController body = player.AddComponent<CharacterController>();
            body.height = 2f;
            body.radius = 0.4f;
            player.AddComponent<CombatHealth>();
            player.AddComponent<PlayerActionModifiers>();
            inventory = player.AddComponent<SurvivalInventory>();
            builder = player.AddComponent<PlayerBuilder>();
            player.AddComponent<PlayerGatherer>();
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (BuiltPiece piece in Object.FindObjectsByType<BuiltPiece>(FindObjectsSortMode.None))
                Object.Destroy(piece.gameObject);
            if (extra != null) Object.Destroy(extra);
            if (player != null) Object.Destroy(player);
            if (ground != null) Object.Destroy(ground);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlacingChargesOnlyOnSuccessAndOccupiedSpaceIsRejected()
        {
            int wood = inventory.Count(ResourceKind.Wood);
            int stone = inventory.Count(ResourceKind.Stone);
            BuiltPiece first = builder.TryPlace(Index("cimiento"), new Vector3(1f, 0f, 1f), 0, out PlacementResult ok);
            Assert.That(ok, Is.EqualTo(PlacementResult.Ok));
            Assert.That(first, Is.Not.Null);
            Assert.That(inventory.Count(ResourceKind.Stone), Is.EqualTo(stone - 4));
            Assert.That(inventory.Count(ResourceKind.Wood), Is.EqualTo(wood - 1));

            BuiltPiece second = builder.TryPlace(Index("cimiento"), new Vector3(1f, 0f, 1f), 0, out PlacementResult blocked);
            Assert.That(second, Is.Null);
            Assert.That(blocked, Is.EqualTo(PlacementResult.Blocked));
            Assert.That(inventory.Count(ResourceKind.Stone), Is.EqualTo(stone - 4), "Un intento rechazado no cobra.");

            // Una pieza vecina que solo toca el borde si se puede colocar.
            Assert.That(builder.TryPlace(Index("cimiento"), new Vector3(3f, 0f, 1f), 0, out _), Is.Not.Null);
            yield break;
        }

        [UnityTest]
        public IEnumerator WallsMeetAtCornersAndStandOnFoundations()
        {
            builder.TryPlace(Index("cimiento"), new Vector3(1f, 0f, 1f), 0, out _);
            // Las paredes van encima del cimiento (0,3 m) y se cruzan en la esquina (2, 2).
            BuiltPiece a = builder.TryPlace(Index("pared"), new Vector3(1f, 0.3f, 2f), 0, out PlacementResult ra);
            BuiltPiece b = builder.TryPlace(Index("pared"), new Vector3(2f, 0.3f, 1f), 1, out PlacementResult rb);
            Assert.That(ra, Is.EqualTo(PlacementResult.Ok));
            Assert.That(rb, Is.EqualTo(PlacementResult.Ok), "Dos paredes en esquina no deben bloquearse.");
            Assert.That(a != null && b != null, Is.True);
            yield break;
        }

        [UnityTest]
        public IEnumerator DismantleAndUndoReturnAllMaterials()
        {
            int wood = inventory.Count(ResourceKind.Wood);
            BuiltPiece wall = builder.TryPlace(Index("pared"), new Vector3(1f, 0f, 2f), 0, out _);
            Assert.That(inventory.Count(ResourceKind.Wood), Is.EqualTo(wood - 4));
            Assert.That(builder.Dismantle(wall), Is.True);
            Assert.That(builder.Dismantle(wall), Is.False,
                "No se puede obtener un segundo reembolso antes de que Unity destruya la pieza.");
            Assert.That(inventory.Count(ResourceKind.Wood), Is.EqualTo(wood));

            Assert.That(builder.TryPlace(Index("suelo"), new Vector3(3f, 0f, 1f), 0, out _), Is.Not.Null);
            Assert.That(builder.Undo(), Is.True);
            Assert.That(inventory.Count(ResourceKind.Wood), Is.EqualTo(wood));
            Assert.That(builder.Undo(), Is.False, "No queda nada que deshacer.");
            yield return null;
            Assert.That(Object.FindObjectsByType<BuiltPiece>(FindObjectsSortMode.None).Length, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AdvancedPiecesNeedANearbyWorkbench()
        {
            builder.TryPlace(Index("cama"), new Vector3(1f, 0f, 1f), 0, out PlacementResult without);
            Assert.That(without, Is.EqualTo(PlacementResult.NeedsWorkbench));

            Assert.That(builder.TryPlace(Index("banco"), new Vector3(-3f, 0f, 1f), 0, out _), Is.Not.Null);
            inventory.Add(ResourceKind.Fiber, 10);
            builder.TryPlace(Index("cama"), new Vector3(1f, 0f, 1f), 0, out PlacementResult with);
            Assert.That(with, Is.EqualTo(PlacementResult.Ok));
            yield break;
        }

        [UnityTest]
        public IEnumerator FarAwayOrUnaffordablePiecesExplainWhy()
        {
            builder.TryPlace(Index("cimiento"), new Vector3(30f, 0f, 30f), 0, out PlacementResult far);
            Assert.That(far, Is.EqualTo(PlacementResult.TooFar));

            inventory.TrySpend(new[] { new ResourceIngredient(ResourceKind.Wood, inventory.Count(ResourceKind.Wood)) });
            builder.TryPlace(Index("pared"), new Vector3(1f, 0f, 2f), 0, out PlacementResult poor);
            Assert.That(poor, Is.EqualTo(PlacementResult.MissingResources));
            yield break;
        }

        [UnityTest]
        public IEnumerator GatheringFillsInventoryAndDepletesTheNode()
        {
            extra = new GameObject("RocaPrueba");
            extra.transform.position = player.transform.position + new Vector3(1.5f, -1.08f, 0f);
            HarvestNode node = extra.AddComponent<HarvestNode>();
            node.Configure(ResourceKind.Stone, 6, 3);
            yield return null;

            PlayerGatherer gatherer = player.GetComponent<PlayerGatherer>();
            int stone = inventory.Count(ResourceKind.Stone);
            Assert.That(node.RequiredTool, Is.EqualTo(GatheringTool.ImprovisedPick));
            Assert.That(gatherer.Tick(true, 1f), Is.Zero,
                "La piedra nunca se extrae a mano.");
            Assert.That(gatherer.TryCraftTool(GatheringTool.ImprovisedPick),
                Is.EqualTo(CraftingResult.Crafted));
            Assert.That(gatherer.Tick(true, 0.5f), Is.Zero,
                "Hace falta mantener E el tiempo completo.");
            Assert.That(gatherer.Tick(true, 0.5f), Is.EqualTo(3));
            Assert.That(gatherer.Tick(false, 1f), Is.Zero, "Soltar E reinicia el progreso.");
            gatherer.Tick(true, 1f);
            Assert.That(inventory.Count(ResourceKind.Stone), Is.EqualTo(stone + 6));
            Assert.That(node.IsDepleted, Is.True);
            Assert.That(gatherer.Tick(true, 1f), Is.Zero, "Un recurso agotado no da mas.");
        }

        [UnityTest]
        public IEnumerator ToolsFollowWoodFiberToStoneToOreProgression()
        {
            PlayerGatherer gatherer = player.GetComponent<PlayerGatherer>();
            SurvivalToolbelt belt = player.GetComponent<SurvivalToolbelt>();
            Assert.That(belt, Is.Not.Null);

            // Un yacimiento de mineral no acepta E sin pico de piedra.
            extra = new GameObject("MineralPrueba");
            extra.transform.position = player.transform.position + new Vector3(1.2f, -1.08f, 0f);
            HarvestNode vein = extra.AddComponent<HarvestNode>();
            vein.Configure(ResourceKind.Ore, 8, 2);
            yield return null;

            int ore = inventory.Count(ResourceKind.Ore);
            Assert.That(gatherer.Tick(true, 1f), Is.Zero);
            Assert.That(vein.Harvest(inventory), Is.Zero,
                "La proteccion debe estar en HarvestNode, no solo en el input.");
            Assert.That(inventory.Count(ResourceKind.Ore), Is.EqualTo(ore));

            Assert.That(gatherer.TryCraftTool(GatheringTool.StonePick),
                Is.EqualTo(CraftingResult.RequiresPreviousTool));
            Assert.That(gatherer.TryCraftTool(GatheringTool.ImprovisedPick),
                Is.EqualTo(CraftingResult.Crafted));
            Assert.That(gatherer.Tick(true, 1f), Is.Zero,
                "El pico improvisado no puede extraer mineral.");
            Assert.That(gatherer.TryCraftTool(GatheringTool.StonePick),
                Is.EqualTo(CraftingResult.Crafted));
            Assert.That(gatherer.Tick(true, 1f), Is.EqualTo(2));
            Assert.That(inventory.Count(ResourceKind.Ore), Is.EqualTo(ore + 2));
            Assert.That(gatherer.TryCraftTool(GatheringTool.StonePick),
                Is.EqualTo(CraftingResult.AlreadyOwned));
        }

        [UnityTest]
        public IEnumerator CraftingMissingResourcesDoesNotConsumeAnyMaterials()
        {
            PlayerGatherer gatherer = player.GetComponent<PlayerGatherer>();
            int wood = inventory.Count(ResourceKind.Wood);
            int fiber = inventory.Count(ResourceKind.Fiber);
            inventory.TrySpend(new[] { new ResourceIngredient(ResourceKind.Fiber, fiber) });
            Assert.That(gatherer.TryCraftTool(GatheringTool.ImprovisedPick),
                Is.EqualTo(CraftingResult.MissingMaterials));
            Assert.That(inventory.Count(ResourceKind.Wood), Is.EqualTo(wood),
                "Una receta fallida no cobra los ingredientes que si hay.");
            Assert.That(inventory.Count(ResourceKind.Fiber), Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChestsExpandStorageAndBuildModeBlocksAttacks()
        {
            int capacity = inventory.Capacity;
            builder.TryPlace(Index("cofre"), new Vector3(1f, 0f, 1f), 0, out _);
            Assert.That(inventory.Capacity, Is.EqualTo(capacity + SurvivalInventory.ChestCapacity));

            PlayerActionModifiers modifiers = player.GetComponent<PlayerActionModifiers>();
            builder.SetBuildMode(true);
            Assert.That(modifiers.AllowsAttack, Is.False);
            Assert.That(modifiers.AllowsMovement, Is.True, "Construir no debe dejarte inmovil.");
            builder.SetBuildMode(false);
            Assert.That(modifiers.AllowsAttack, Is.True);
            yield break;
        }

        [UnityTest]
        public IEnumerator ChestCannotBeDismantledIfRemovingStorageWouldOverflowInventory()
        {
            BuiltPiece chest = builder.TryPlace(Index("cofre"),
                new Vector3(1f, 0f, 1f), 0, out PlacementResult result);
            Assert.That(result, Is.EqualTo(PlacementResult.Ok));
            Assert.That(chest, Is.Not.Null);

            int total = inventory.TotalItems;
            Assert.That(inventory.Add(ResourceKind.Wood, 220), Is.EqualTo(220));
            Assert.That(inventory.TotalItems, Is.GreaterThan(240));
            int beforeRejected = inventory.TotalItems;
            Assert.That(builder.Dismantle(chest), Is.False,
                "No destruir cofre si los recursos desbordarian el inventario.");
            Assert.That(inventory.TotalItems, Is.EqualTo(beforeRejected));
            Assert.That(chest.IsBeingRemoved, Is.False);

            Assert.That(inventory.TrySpend(new[] {
                new ResourceIngredient(ResourceKind.Wood, 90) }), Is.True);
            int beforeRefund = inventory.TotalItems;
            Assert.That(builder.Dismantle(chest), Is.True);
            Assert.That(inventory.TotalItems, Is.EqualTo(beforeRefund + 10),
                "El reembolso incluye exactamente los 10 recursos pagados por el cofre.");
            Assert.That(builder.Dismantle(chest), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BedMovesTheRespawnPointAndDismantlingRestoresIt()
        {
            extra = new GameObject("SesionPrueba");
            extra.SetActive(false);
            PrototypeGameSession session = extra.AddComponent<PrototypeGameSession>();
            typeof(PrototypeGameSession).GetField("player",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(session, player.transform);
            extra.SetActive(true);
            yield return null;
            Vector3 original = session.RespawnPoint;

            builder.TryPlace(Index("banco"), new Vector3(-3f, 0f, 1f), 0, out _);
            BuiltPiece bed = builder.TryPlace(Index("cama"), new Vector3(1f, 0f, 1f), 0, out PlacementResult result);
            Assert.That(result, Is.EqualTo(PlacementResult.Ok));
            Assert.That(Vector3.Distance(session.RespawnPoint, original), Is.GreaterThan(1f));

            builder.Dismantle(bed);
            Assert.That(Vector3.Distance(session.RespawnPoint, original), Is.LessThan(0.01f));
        }
    }
}
