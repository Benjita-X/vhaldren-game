using System;
using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Survival;

namespace Vhaldren.Gameplay.World
{
    public enum VillageObjective
    {
        DeliverWood,
        DeliverStone,
        DefeatGuardians,
        CastMagic,
        BuildCampfire,
        DefeatBoss
    }

    public enum VillageQuestState
    {
        Available,
        Active,
        ReadyToClaim,
        Complete,
        FinishedCampaign
    }

    public enum VillageEvent
    {
        RefugeeCaravan,
        ArcaneDisturbance,
        MoonlitHunt,
        UndeadOmen,
        CelestialEnvoys,
        InfernalDelegation
    }

    public sealed class VillageQuest
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly VillageObjective Objective;
        public readonly int Required;
        public readonly ResourceKind Reward;
        public readonly int RewardAmount;
        public readonly SettlementCulture Faction;
        public readonly int Reputation;

        public VillageQuest(string id, string name, string description,
            VillageObjective objective, int required, ResourceKind reward,
            int rewardAmount, SettlementCulture faction, int reputation)
        {
            Id = id;
            Name = name;
            Description = description;
            Objective = objective;
            Required = required;
            Reward = reward;
            RewardAmount = rewardAmount;
            Faction = faction;
            Reputation = reputation;
        }
    }

    /// <summary>
    /// Cadena corta pero jugable del primer pueblo. Las entregas verifican
    /// inventario REAL. El combate y los hechizos solo progresan si ocurren
    /// despues de aceptar el encargo. No genera recompensas sin requisitos.
    ///
    /// El journal separa datos de misión, facciones y eventos de presentación,
    /// para que los NPC de futuros pueblos usen el mismo contrato.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SurvivalInventory))]
    public sealed class VillageQuestJournal : MonoBehaviour
    {
        private static readonly VillageQuest[] Story =
        {
            new VillageQuest("cruce-01", "Refugio para los desplazados",
                "Entregar 6 de madera al consejo del Cruce.",
                VillageObjective.DeliverWood, 6, ResourceKind.Fiber, 3,
                SettlementCulture.Mixed, 2),
            new VillageQuest("cruce-02", "Piedra para el puente",
                "Conseguir un pico y entregar 5 de piedra.",
                VillageObjective.DeliverStone, 5, ResourceKind.Essence, 2,
                SettlementCulture.Human, 2),
            new VillageQuest("cruce-03", "Guardianes del camino",
                "Derrotar a 2 guardianes despues de aceptar la mision.",
                VillageObjective.DefeatGuardians, 2, ResourceKind.Essence, 3,
                SettlementCulture.Mixed, 3),
            new VillageQuest("cruce-04", "Prueba de la academia",
                "Canalizar 2 hechizos durante el encargo.",
                VillageObjective.CastMagic, 2, ResourceKind.Crystal, 2,
                SettlementCulture.Arcane, 3),
            new VillageQuest("cruce-05", "Fuego para el pueblo",
                "Construir una fogata tras aceptar el encargo.",
                VillageObjective.BuildCampfire, 1, ResourceKind.Food, 5,
                SettlementCulture.Mixed, 3),
            new VillageQuest("cruce-06", "Juramento del altar",
                "Derrotar al guardian del altar y traer seguridad al Cruce.",
                VillageObjective.DefeatBoss, 1, ResourceKind.Essence, 6,
                SettlementCulture.Mixed, 6)
        };

        private readonly Dictionary<SettlementCulture, int> reputation =
            new Dictionary<SettlementCulture, int>();
        private readonly Dictionary<string, int> tradeCounts =
            new Dictionary<string, int>();
        private SurvivalInventory inventory;
        private int stage;
        private int eventProgress;
        private bool active;
        private float nextEventAt;
        private VillageEvent currentEvent;

        public static IReadOnlyList<VillageQuest> Chapters => Story;
        public VillageQuest Current => stage >= 0 && stage < Story.Length
            ? Story[stage] : null;
        public VillageQuestState State
        {
            get
            {
                if (Current == null)
                    return VillageQuestState.FinishedCampaign;
                if (!active)
                    return VillageQuestState.Available;
                return Progress >= Current.Required
                    ? VillageQuestState.ReadyToClaim : VillageQuestState.Active;
            }
        }

        public int Progress
        {
            get
            {
                if (Current == null)
                    return 0;
                if (Current.Objective == VillageObjective.DeliverWood)
                    return inventory != null ? inventory.Count(ResourceKind.Wood) : 0;
                if (Current.Objective == VillageObjective.DeliverStone)
                    return inventory != null ? inventory.Count(ResourceKind.Stone) : 0;
                return eventProgress;
            }
        }

        public int CompletedChapters => stage;
        public VillageEvent CurrentEvent => currentEvent;
        public string EventTitle
        {
            get
            {
                switch (currentEvent)
                {
                    case VillageEvent.RefugeeCaravan: return "Caravana de refugiados";
                    case VillageEvent.ArcaneDisturbance: return "Anomalía arcana";
                    case VillageEvent.MoonlitHunt: return "Cacería bajo la luna";
                    case VillageEvent.UndeadOmen: return "Señales de los no muertos";
                    case VillageEvent.CelestialEnvoys: return "Enviados del Alto Reino";
                    default: return "Delegación del Reino Abisal";
                }
            }
        }

        public event Action Changed;
        public int Reputation(SettlementCulture culture)
        {
            return reputation.TryGetValue(culture, out int value) ? value : 0;
        }

        private void Awake()
        {
            inventory = GetComponent<SurvivalInventory>();
            nextEventAt = Time.time + 90f;
        }

        private void OnEnable()
        {
            if (inventory != null)
                inventory.Changed += NotifyInventoryChanged;
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.Changed -= NotifyInventoryChanged;
        }

        private void NotifyInventoryChanged() => Changed?.Invoke();

        private void Update()
        {
            if (Time.time < nextEventAt)
                return;

            // Ciclo de eventos reproducible por el indice de capítulo y
            // el estado local. Los eventos son narrativos en esta fase:
            // las invasiones reales deben tener enemigos y navegación.
            currentEvent = (VillageEvent)((stage + (int)currentEvent + 1) % 6);
            nextEventAt = Time.time + 90f;
            tradeCounts.Clear(); // Llega una nueva caravana con stock renovado.
            Changed?.Invoke();
        }

        public bool TryAccept()
        {
            if (State != VillageQuestState.Available)
                return false;
            active = true;
            eventProgress = 0;
            Changed?.Invoke();
            return true;
        }

        public bool RecordAction(VillageObjective objective, int amount = 1)
        {
            if (State != VillageQuestState.Active ||
                Current == null || Current.Objective != objective ||
                amount <= 0 || objective == VillageObjective.DeliverWood ||
                objective == VillageObjective.DeliverStone)
                return false;

            eventProgress = Mathf.Min(Current.Required, eventProgress + amount);
            Changed?.Invoke();
            return true;
        }

        public int TradeStock(int offerIndex)
        {
            if (offerIndex < 0 || offerIndex >= VillageTradeCatalog.Offers.Count)
                return 0;
            VillageTradeOffer offer = VillageTradeCatalog.Offers[offerIndex];
            tradeCounts.TryGetValue(offer.Id, out int used);
            return Mathf.Max(0, offer.StockPerEvent - used);
        }

        public VillageTradeResult TryTrade(int offerIndex)
        {
            if (inventory == null || offerIndex < 0 ||
                offerIndex >= VillageTradeCatalog.Offers.Count)
                return VillageTradeResult.NoOffer;

            VillageTradeOffer offer = VillageTradeCatalog.Offers[offerIndex];
            int reputationLevel = Reputation(offer.Faction);
            if (reputationLevel < offer.ReputationNeeded)
                return VillageTradeResult.ReputationTooLow;
            if (TradeStock(offerIndex) <= 0)
                return VillageTradeResult.NoStock;

            int price = VillageTradeCatalog.Price(offer,
                currentEvent, reputationLevel);
            if (inventory.Count(offer.PriceResource) < price)
                return VillageTradeResult.MissingMaterials;
            if ((long)inventory.TotalItems - price + offer.RewardAmount >
                inventory.Capacity)
                return VillageTradeResult.InventoryFull;

            if (!inventory.TryExchange(offer.PriceResource, price,
                offer.Reward, offer.RewardAmount))
                return VillageTradeResult.MissingMaterials;

            tradeCounts.TryGetValue(offer.Id, out int previous);
            tradeCounts[offer.Id] = previous + 1;
            Changed?.Invoke();
            return VillageTradeResult.Purchased;
        }

        public bool TryClaim()
        {
            if (State != VillageQuestState.ReadyToClaim || inventory == null)
                return false;

            VillageQuest quest = Current;
            if (quest == null)
                return false;

            int spent = quest.Objective == VillageObjective.DeliverWood ||
                quest.Objective == VillageObjective.DeliverStone ? quest.Required : 0;
            ResourceKind delivered = quest.Objective == VillageObjective.DeliverWood
                ? ResourceKind.Wood : ResourceKind.Stone;

            // Comprobar la capacidad futura antes de consumir materiales:
            // no se pierden entregas ni recompensas si la mochila esta llena.
            if ((long)inventory.TotalItems - spent + quest.RewardAmount >
                inventory.Capacity)
                return false;

            if (spent > 0)
            {
                if (!inventory.TryExchange(delivered, spent,
                    quest.Reward, quest.RewardAmount))
                    return false;
            }
            else if (inventory.Add(quest.Reward, quest.RewardAmount) !=
                quest.RewardAmount)
                return false;

            reputation[quest.Faction] = Reputation(quest.Faction) + quest.Reputation;
            stage++;
            active = false;
            eventProgress = 0;
            Changed?.Invoke();
            return true;
        }
    }
}
