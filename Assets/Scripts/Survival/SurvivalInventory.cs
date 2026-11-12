using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Survival
{
    public enum ResourceKind
    {
        Wood,
        Stone,
        Fiber,
        Ore,
        Food,
        Essence,
        Hide,
        Crystal
    }

    [Serializable]
    public struct ResourceIngredient
    {
        public ResourceKind Kind;
        [Min(1)] public int Amount;

        public ResourceIngredient(ResourceKind kind, int amount)
        {
            Kind = kind;
            Amount = amount;
        }
    }

    /// <summary>
    /// Almacen local atomico para recoleccion y construccion de prueba.
    /// Costes duplicados se agregan antes de gastar; si falta un solo
    /// ingrediente no se consume ninguno.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalInventory : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxItems = 240;
        [SerializeField] private bool grantDemoSupplies = true;

        private readonly Dictionary<ResourceKind, int> counts = new Dictionary<ResourceKind, int>();
        private readonly Dictionary<ResourceKind, int> pendingCosts = new Dictionary<ResourceKind, int>();
        private bool initialized;

        public event Action Changed;
        public int TotalItems { get; private set; }
        public const int ChestCapacity = 120;
        /// <summary>Cada cofre construido amplia el almacen compartido de la base.</summary>
        public int Capacity => Mathf.Max(1, maxItems) + ChestCapacity * BuiltPiece.CountWithFunction(BuildFunction.Chest);

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            if (grantDemoSupplies)
            {
                // Recursos de muestra para probar de inmediato sin destruir
                // la progresion futura: se pueden desactivar desde Inspector.
                Add(ResourceKind.Wood, 24);
                Add(ResourceKind.Stone, 16);
                Add(ResourceKind.Fiber, 12);
            }
        }

        public int Count(ResourceKind kind)
        {
            return counts.TryGetValue(kind, out int value) ? value : 0;
        }

        public int Add(ResourceKind kind, int amount)
        {
            if (amount <= 0 || !Enum.IsDefined(typeof(ResourceKind), kind))
                return 0;
            int accepted = Mathf.Min(amount, Mathf.Max(0, Capacity - TotalItems));
            if (accepted <= 0)
                return 0;
            counts[kind] = Count(kind) + accepted;
            TotalItems += accepted;
            Changed?.Invoke();
            return accepted;
        }

        /// <summary>
        /// Una sola transaccion para el comercio: consume y entrega con
        /// una notificacion. Evita gastar materiales si el inventario no
        /// puede aceptar la recompensa. Tambien maneja cost/reward iguales.
        /// </summary>
        public bool TryExchange(ResourceKind priceKind, int price,
            ResourceKind rewardKind, int reward)
        {
            if (!Enum.IsDefined(typeof(ResourceKind), priceKind) ||
                !Enum.IsDefined(typeof(ResourceKind), rewardKind) ||
                price < 0 || reward <= 0 || Count(priceKind) < price)
                return false;

            long future = (long)TotalItems - price + reward;
            if (future < 0 || future > Capacity)
                return false;

            long priceLeft = (long)Count(priceKind) - price;
            long rewardTotal = (long)Count(rewardKind) + reward;
            if (priceKind == rewardKind)
            {
                long combined = (long)Count(priceKind) - price + reward;
                if (combined < 0 || combined > int.MaxValue)
                    return false;
                counts[priceKind] = (int)combined;
            }
            else
            {
                if (priceLeft < 0 || rewardTotal > int.MaxValue)
                    return false;
                counts[priceKind] = (int)priceLeft;
                counts[rewardKind] = (int)rewardTotal;
            }
            TotalItems = (int)future;
            Changed?.Invoke();
            return true;
        }

        public bool CanAfford(IReadOnlyList<ResourceIngredient> ingredients)
        {
            if (!CollectCosts(ingredients))
                return false;
            foreach (KeyValuePair<ResourceKind, int> cost in pendingCosts)
                if (Count(cost.Key) < cost.Value)
                    return false;
            return true;
        }

        public bool TrySpend(IReadOnlyList<ResourceIngredient> ingredients)
        {
            if (!CollectCosts(ingredients))
                return false;

            foreach (KeyValuePair<ResourceKind, int> cost in pendingCosts)
                if (Count(cost.Key) < cost.Value)
                    return false;

            foreach (KeyValuePair<ResourceKind, int> cost in pendingCosts)
            {
                counts[cost.Key] = Count(cost.Key) - cost.Value;
                TotalItems -= cost.Value;
            }

            if (pendingCosts.Count > 0)
                Changed?.Invoke();
            return true;
        }

        public void Refund(IReadOnlyList<ResourceIngredient> ingredients, float fraction = 1f)
        {
            TryRefund(ingredients, fraction);
        }

        /// <summary>
        /// Reembolso indivisible. Si desmontar un cofre reduciria la
        /// capacidad por debajo del total final, no se devuelve nada y
        /// el caller conserva la construccion. Notificar una sola vez
        /// al finalizar evita colecciones modificadas desde eventos.
        /// </summary>
        public bool TryRefund(IReadOnlyList<ResourceIngredient> ingredients,
            float fraction = 1f, int capacityReduction = 0)
        {
            if (ingredients == null || float.IsNaN(fraction) ||
                float.IsInfinity(fraction) || capacityReduction < 0 ||
                !CollectCosts(ingredients))
                return false;

            float safe = Mathf.Clamp01(fraction);
            long count = 0;
            foreach (KeyValuePair<ResourceKind, int> ingredient in pendingCosts)
                count += Mathf.FloorToInt(ingredient.Value * safe);

            long futureCapacity = (long)Capacity - capacityReduction;
            if (futureCapacity < 0 || (long)TotalItems + count > futureCapacity)
                return false;

            foreach (KeyValuePair<ResourceKind, int> ingredient in pendingCosts)
            {
                int amount = Mathf.FloorToInt(ingredient.Value * safe);
                if (amount <= 0)
                    continue;
                counts[ingredient.Key] = Count(ingredient.Key) + amount;
                TotalItems += amount;
            }
            if (count > 0)
                Changed?.Invoke();
            return true;
        }

        private bool CollectCosts(IReadOnlyList<ResourceIngredient> ingredients)
        {
            pendingCosts.Clear();
            if (ingredients == null)
                return false;

            for (int i = 0; i < ingredients.Count; i++)
            {
                ResourceIngredient ingredient = ingredients[i];
                if (ingredient.Amount <= 0 ||
                    !Enum.IsDefined(typeof(ResourceKind), ingredient.Kind))
                    return false;

                pendingCosts.TryGetValue(ingredient.Kind, out int total);
                if (ingredient.Amount > int.MaxValue - total)
                    return false;
                pendingCosts[ingredient.Kind] = total + ingredient.Amount;
            }
            return true;
        }
    }
}
