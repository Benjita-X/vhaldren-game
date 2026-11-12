using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Survival
{
    public enum GatheringTool
    {
        Hands = 0,
        ImprovisedPick = 1,
        StonePick = 2
    }

    public enum CraftingResult
    {
        Crafted,
        AlreadyOwned,
        MissingMaterials,
        RequiresPreviousTool,
        InvalidTool
    }

    /// <summary>
    /// Primera cadena real de progresion: ramas/fibra -> pico improvisado
    /// -> piedra -> pico de piedra -> mineral. No concede recursos gratis.
    /// Las herramientas no se consumen en esta fase; el desgaste vendra
    /// cuando exista una UI de reparacion y herramientas visuales de calidad.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SurvivalInventory))]
    public sealed class SurvivalToolbelt : MonoBehaviour
    {
        private static readonly ResourceIngredient[] ImprovisedPickCost =
        {
            new ResourceIngredient(ResourceKind.Wood, 3),
            new ResourceIngredient(ResourceKind.Fiber, 2)
        };

        private static readonly ResourceIngredient[] StonePickCost =
        {
            new ResourceIngredient(ResourceKind.Wood, 3),
            new ResourceIngredient(ResourceKind.Stone, 5),
            new ResourceIngredient(ResourceKind.Fiber, 2)
        };

        private SurvivalInventory inventory;
        [SerializeField] private bool ownsImprovisedPick;
        [SerializeField] private bool ownsStonePick;

        public event Action EquipmentChanged;
        public bool OwnsImprovisedPick => ownsImprovisedPick;
        public bool OwnsStonePick => ownsStonePick;
        public GatheringTool BestTool => ownsStonePick ? GatheringTool.StonePick :
            ownsImprovisedPick ? GatheringTool.ImprovisedPick : GatheringTool.Hands;

        private void Awake() => inventory = GetComponent<SurvivalInventory>();

        public static IReadOnlyList<ResourceIngredient> Recipe(GatheringTool tool)
        {
            switch (tool)
            {
                case GatheringTool.ImprovisedPick: return ImprovisedPickCost;
                case GatheringTool.StonePick: return StonePickCost;
                default: return Array.Empty<ResourceIngredient>();
            }
        }

        public bool CanHarvest(HarvestNode node)
        {
            return node != null && BestTool >= node.RequiredTool;
        }

        public CraftingResult TryCraft(GatheringTool tool)
        {
            if (inventory == null)
                inventory = GetComponent<SurvivalInventory>();
            if (inventory == null)
                return CraftingResult.MissingMaterials;

            if (tool == GatheringTool.ImprovisedPick)
            {
                if (ownsImprovisedPick)
                    return CraftingResult.AlreadyOwned;
                if (!inventory.TrySpend(ImprovisedPickCost))
                    return CraftingResult.MissingMaterials;
                ownsImprovisedPick = true;
            }
            else if (tool == GatheringTool.StonePick)
            {
                if (ownsStonePick)
                    return CraftingResult.AlreadyOwned;
                if (!ownsImprovisedPick)
                    return CraftingResult.RequiresPreviousTool;
                if (!inventory.TrySpend(StonePickCost))
                    return CraftingResult.MissingMaterials;
                ownsStonePick = true;
            }
            else
                return CraftingResult.InvalidTool;

            EquipmentChanged?.Invoke();
            return CraftingResult.Crafted;
        }

        public static string DisplayName(GatheringTool tool)
        {
            switch (tool)
            {
                case GatheringTool.ImprovisedPick: return "Pico improvisado";
                case GatheringTool.StonePick: return "Pico de piedra";
                default: return "Manos";
            }
        }
    }
}
