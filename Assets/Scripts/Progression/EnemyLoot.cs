using System.Text;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Progression
{
    /// <summary>
    /// Botin al derrotar a un enemigo. Cada tipo suelta materiales distintos, de modo
    /// que el equipo de cada calidad obliga a cazar en zonas concretas:
    /// cazador = piel (lobos, tiradores); hierro = mineral (bruto, ruinas);
    /// runico = cristal y esencia (acolitos, centinela, jefe).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class EnemyLoot : MonoBehaviour
    {
        private struct Drop
        {
            public ResourceKind Kind;
            public int Amount;
            public float Chance;

            public Drop(ResourceKind kind, int amount, float chance = 1f)
            {
                Kind = kind;
                Amount = amount;
                Chance = chance;
            }
        }

        private static int seed = 4242;
        private CombatHealth health;
        private System.Random random;
        private Drop[] drops;

        public int TimesLooted { get; private set; }

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            random = new System.Random(unchecked(seed++ * 17 + name.GetHashCode()));
            drops = TableFor(GetComponent<PrototypeEnemy>(), name);
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            health.Defeated += GiveLoot;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Defeated -= GiveLoot;
        }

        private void GiveLoot()
        {
            PlayerEquipment gear = PlayerEquipment.Local;
            SurvivalInventory inventory = gear != null ? gear.GetComponent<SurvivalInventory>() : null;
            if (inventory == null || drops == null)
                return;
            StringBuilder text = new StringBuilder("Botín:");
            bool any = false;
            for (int i = 0; i < drops.Length; i++)
            {
                if (random.NextDouble() > drops[i].Chance)
                    continue;
                int added = inventory.Add(drops[i].Kind, drops[i].Amount);
                if (added <= 0)
                    continue;
                any = true;
                text.Append(" +").Append(added).Append(' ').Append(HarvestNode.ResourceName(drops[i].Kind));
            }
            TimesLooted++;
            if (any)
                gear.Notify(text.ToString());
        }

        private static Drop[] TableFor(PrototypeEnemy enemy, string objectName)
        {
            string id = enemy != null && enemy.Archetype != null ? enemy.Archetype.Id : null;
            switch (id)
            {
                case "saqueador": return new[] { new Drop(ResourceKind.Fiber, 2), new Drop(ResourceKind.Wood, 2), new Drop(ResourceKind.Hide, 1, 0.5f), new Drop(ResourceKind.Essence, 1, 0.3f) };
                case "ballestero": return new[] { new Drop(ResourceKind.Wood, 3), new Drop(ResourceKind.Hide, 2) };
                case "lobo": return new[] { new Drop(ResourceKind.Hide, 2), new Drop(ResourceKind.Food, 1, 0.5f) };
                case "esqueleto": return new[] { new Drop(ResourceKind.Stone, 3), new Drop(ResourceKind.Ore, 1, 0.5f), new Drop(ResourceKind.Essence, 1, 0.5f) };
                case "acolito": return new[] { new Drop(ResourceKind.Essence, 2), new Drop(ResourceKind.Crystal, 1, 0.35f) };
                case "sanadora": return new[] { new Drop(ResourceKind.Food, 2), new Drop(ResourceKind.Essence, 1), new Drop(ResourceKind.Fiber, 3) };
                case "bruto": return new[] { new Drop(ResourceKind.Ore, 4), new Drop(ResourceKind.Stone, 4), new Drop(ResourceKind.Hide, 2) };
                case "centinela_runico": return new[] { new Drop(ResourceKind.Crystal, 2), new Drop(ResourceKind.Ore, 3), new Drop(ResourceKind.Essence, 2) };
            }
            if (enemy != null && enemy.IsBoss)
                return new[] { new Drop(ResourceKind.Crystal, 4), new Drop(ResourceKind.Essence, 6), new Drop(ResourceKind.Ore, 4) };
            string lower = objectName.ToLowerInvariant();
            if (lower.Contains("guardian"))
                return new[] { new Drop(ResourceKind.Essence, 2), new Drop(ResourceKind.Ore, 2) };
            if (lower.Contains("centinela"))
                return new[] { new Drop(ResourceKind.Ore, 2), new Drop(ResourceKind.Stone, 2) };
            return new[] { new Drop(ResourceKind.Hide, 1), new Drop(ResourceKind.Fiber, 2) };
        }
    }
}
