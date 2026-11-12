using System;
using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Survival;

namespace Vhaldren.Gameplay.World
{
    public enum VillageTradeResult
    {
        Purchased,
        NoOffer,
        ReputationTooLow,
        NoStock,
        MissingMaterials,
        InventoryFull
    }

    public sealed class VillageTradeOffer
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly ResourceKind PriceResource;
        public readonly int BasePrice;
        public readonly ResourceKind Reward;
        public readonly int RewardAmount;
        public readonly SettlementCulture Faction;
        public readonly int ReputationNeeded;
        public readonly int StockPerEvent;

        public VillageTradeOffer(string id, string displayName,
            ResourceKind priceResource, int basePrice,
            ResourceKind reward, int rewardAmount,
            SettlementCulture faction, int reputationNeeded, int stockPerEvent)
        {
            Id = id;
            DisplayName = displayName;
            PriceResource = priceResource;
            BasePrice = Mathf.Max(1, basePrice);
            Reward = reward;
            RewardAmount = Mathf.Max(1, rewardAmount);
            Faction = faction;
            ReputationNeeded = Mathf.Max(0, reputationNeeded);
            StockPerEvent = Mathf.Max(1, stockPerEvent);
        }
    }

    /// <summary>
    /// Primera economia por faccion. Los descuentos dependen de reputacion
    /// y evento de aldea, pero no regalan recursos, equipo ficticio ni magia.
    /// Las armaduras y mercaderes animados usaran estas reglas cuando existan
    /// sistemas de equipo, precios en monedas y redes de rutas.
    /// </summary>
    public static class VillageTradeCatalog
    {
        public static readonly IReadOnlyList<VillageTradeOffer> Offers =
            Array.AsReadOnly(new[]
            {
                new VillageTradeOffer("caravan-rations", "Raciones de caravana",
                    ResourceKind.Fiber, 5, ResourceKind.Food, 2,
                    SettlementCulture.Mixed, 0, 4),
                new VillageTradeOffer("tanner-hide", "Fibras del curtidor",
                    ResourceKind.Hide, 2, ResourceKind.Fiber, 7,
                    SettlementCulture.Mixed, 2, 3),
                new VillageTradeOffer("arcane-fragment", "Fragmento de cristal",
                    ResourceKind.Essence, 3, ResourceKind.Crystal, 1,
                    SettlementCulture.Arcane, 3, 2)
            });

        public static int Price(VillageTradeOffer offer,
            VillageEvent activeEvent, int factionReputation)
        {
            if (offer == null)
                return 0;

            int amount = offer.BasePrice;
            if (factionReputation >= 5)
                amount--;
            if (activeEvent == VillageEvent.RefugeeCaravan &&
                offer.Faction == SettlementCulture.Mixed)
                amount--;
            if (activeEvent == VillageEvent.ArcaneDisturbance &&
                offer.Faction == SettlementCulture.Arcane)
                amount--;
            return Mathf.Max(1, amount);
        }
    }
}
