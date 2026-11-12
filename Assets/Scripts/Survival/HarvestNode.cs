using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Survival
{
    /// <summary>
    /// Arbol, roca, mineral o planta que se recolecta manteniendo E cerca.
    /// Al agotarse desaparece y vuelve a crecer pasado un tiempo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarvestNode : MonoBehaviour
    {
        private static readonly List<HarvestNode> all = new List<HarvestNode>();

        [SerializeField] private ResourceKind resource = ResourceKind.Wood;
        [SerializeField, Min(1)] private int capacity = 12;
        [SerializeField, Min(1)] private int perGather = 3;
        [SerializeField, Min(0.1f)] private float gatherSeconds = 0.8f;
        [SerializeField, Min(1f)] private float regrowSeconds = 90f;

        private int remaining;
        private float regrowAt = float.PositiveInfinity;

        public static IReadOnlyList<HarvestNode> All => all;
        public ResourceKind Resource => resource;
        // Las rocas requieren herramienta. La madera y la fibra pueden
        // recogerse a mano al comienzo (ramas y vegetacion suelta).
        public GatheringTool RequiredTool => resource == ResourceKind.Ore
            ? GatheringTool.StonePick
            : resource == ResourceKind.Stone
                ? GatheringTool.ImprovisedPick : GatheringTool.Hands;
        public int Remaining => remaining;
        public float GatherSeconds => gatherSeconds;
        public bool IsDepleted => remaining <= 0;

        public void Configure(ResourceKind kind, int totalAmount, int amountPerGather)
        {
            resource = kind;
            capacity = Mathf.Max(1, totalAmount);
            perGather = Mathf.Max(1, amountPerGather);
            remaining = capacity;
        }

        private void Awake() => remaining = Mathf.Max(1, capacity);

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable() => all.Remove(this);

        private void Update()
        {
            if (IsDepleted && Time.time >= regrowAt)
            {
                remaining = Mathf.Max(1, capacity);
                regrowAt = float.PositiveInfinity;
                SetVisible(true);
            }
        }

        /// <summary>Entrega un golpe de recoleccion al inventario. Devuelve lo que entro.</summary>
        public int Harvest(SurvivalInventory inventory)
        {
            if (inventory == null || IsDepleted)
                return 0;
            SurvivalToolbelt tools = inventory.GetComponent<SurvivalToolbelt>();
            // Enforce at the resource itself: no external script can harvest
            // a rock by calling Harvest() without unlocking the tool.
            if (RequiredTool != GatheringTool.Hands &&
                (tools == null || !tools.CanHarvest(this)))
                return 0;
            int added = inventory.Add(resource, Mathf.Min(perGather, remaining));
            remaining -= added;
            if (IsDepleted)
            {
                regrowAt = Time.time + Mathf.Max(1f, regrowSeconds);
                SetVisible(false);
            }
            return added;
        }

        public static HarvestNode Nearest(Vector3 position, float range)
        {
            HarvestNode best = null;
            float bestSquared = range * range;
            for (int i = 0; i < all.Count; i++)
            {
                HarvestNode node = all[i];
                if (node == null || node.IsDepleted)
                    continue;
                Vector3 offset = node.transform.position - position;
                offset.y = 0f;
                float squared = offset.sqrMagnitude;
                if (squared <= bestSquared)
                {
                    bestSquared = squared;
                    best = node;
                }
            }
            return best;
        }

        public static string ResourceName(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Wood: return "Madera";
                case ResourceKind.Stone: return "Piedra";
                case ResourceKind.Fiber: return "Fibra";
                case ResourceKind.Ore: return "Mineral";
                case ResourceKind.Food: return "Comida";
                case ResourceKind.Essence: return "Esencia";
                case ResourceKind.Hide: return "Piel";
                case ResourceKind.Crystal: return "Cristal";
                default: return kind.ToString();
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = visible;
        }
    }
}
