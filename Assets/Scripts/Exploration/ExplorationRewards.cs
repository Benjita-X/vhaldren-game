using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Progression;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Recompensas de exploracion con los sistemas que ya existen: materiales en
    /// <see cref="SurvivalInventory"/>, esencia en <see cref="EssenceReservoir"/> y, como
    /// "equipo", los materiales exactos para forjar la siguiente calidad de la pieza mas
    /// floja (<see cref="GearCatalog"/>). El texto de recompensa del diseño se interpreta
    /// por palabras clave: "5 cristales", "mineral:3", "esencia", "equipo"...
    /// </summary>
    public static class ExplorationRewards
    {
        public struct RewardSpec
        {
            public List<ResourceKind> Kinds;
            public List<int> Amounts;
            public bool Gear;
            public bool Essence;
            public bool IsEmpty => (Kinds == null || Kinds.Count == 0) && !Gear && !Essence;
        }

        public static readonly string[] RarityNames = { "común", "raro", "épico" };

        public static Color RarityColor(int rarity)
        {
            switch (Mathf.Clamp(rarity, 0, 2))
            {
                case 1: return new Color(0.45f, 0.72f, 1f);
                case 2: return new Color(0.82f, 0.5f, 1f);
                default: return new Color(0.9f, 0.84f, 0.7f);
            }
        }

        /// <summary>Interpreta un texto de recompensa (logica pura, probada en EditMode).</summary>
        public static RewardSpec Parse(string text)
        {
            RewardSpec spec = new RewardSpec { Kinds = new List<ResourceKind>(), Amounts = new List<int>() };
            if (string.IsNullOrEmpty(text))
                return spec;
            List<string> tokens = new List<string>();
            StringBuilder token = new StringBuilder();
            foreach (char raw in text.ToLowerInvariant())
            {
                char c = Fold(raw);
                if (char.IsLetterOrDigit(c))
                    token.Append(c);
                else if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }
            }
            if (token.Length > 0)
                tokens.Add(token.ToString());

            for (int i = 0; i < tokens.Count; i++)
            {
                string word = tokens[i];
                if (word.StartsWith("equip") || word.StartsWith("arma") || word.StartsWith("talisman") ||
                    word.StartsWith("forj") || word.StartsWith("reliquia"))
                {
                    spec.Gear = true;
                    continue;
                }
                if (!TryKind(word, out ResourceKind kind))
                    continue;
                if (kind == ResourceKind.Essence)
                    spec.Essence = true;
                int amount = Number(tokens, i - 1);
                if (amount <= 0)
                    amount = Number(tokens, i + 1);
                if (amount <= 0)
                    amount = DefaultAmount(kind);
                int existing = spec.Kinds.IndexOf(kind);
                if (existing >= 0)
                    spec.Amounts[existing] += amount;
                else
                {
                    spec.Kinds.Add(kind);
                    spec.Amounts.Add(Mathf.Clamp(amount, 1, 99));
                }
            }
            return spec;
        }

        private static char Fold(char c)
        {
            switch (c)
            {
                case 'á': return 'a';
                case 'é': return 'e';
                case 'í': return 'i';
                case 'ó': return 'o';
                case 'ú': case 'ü': return 'u';
                default: return c;
            }
        }

        private static int Number(List<string> tokens, int index)
        {
            if (index < 0 || index >= tokens.Count)
                return 0;
            string t = tokens[index];
            if (t.StartsWith("x") && t.Length > 1)
                t = t.Substring(1);
            return int.TryParse(t, out int value) ? value : 0;
        }

        private static bool TryKind(string word, out ResourceKind kind)
        {
            kind = ResourceKind.Wood;
            if (word.StartsWith("crist")) kind = ResourceKind.Crystal;
            else if (word.StartsWith("miner") || word.StartsWith("hierr") || word.StartsWith("metal")) kind = ResourceKind.Ore;
            else if (word.StartsWith("piel") || word.StartsWith("cuero")) kind = ResourceKind.Hide;
            else if (word.StartsWith("mader")) kind = ResourceKind.Wood;
            else if (word.StartsWith("piedr")) kind = ResourceKind.Stone;
            else if (word.StartsWith("fibr")) kind = ResourceKind.Fiber;
            else if (word.StartsWith("comid") || word.StartsWith("carne") || word.StartsWith("aliment") || word.StartsWith("provis")) kind = ResourceKind.Food;
            else if (word.StartsWith("esenc")) kind = ResourceKind.Essence;
            else return false;
            return true;
        }

        private static int DefaultAmount(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Crystal: return 3;
                case ResourceKind.Essence: return 2;
                case ResourceKind.Ore: return 5;
                case ResourceKind.Hide: return 4;
                case ResourceKind.Food: return 3;
                default: return 6;
            }
        }

        /// <summary>Recompensa de un lugar secreto o con recompensa. tier 0..2 escala lo que se da por defecto.</summary>
        public static void GrantPlace(GameObject player, string reward, int tier)
        {
            RewardSpec spec = Parse(reward);
            if (spec.IsEmpty)
            {
                spec.Kinds.Add(ResourceKind.Crystal);
                spec.Amounts.Add(2 + tier);
                spec.Kinds.Add(ResourceKind.Essence);
                spec.Amounts.Add(2 + tier);
                spec.Essence = true;
            }
            StringBuilder text = new StringBuilder("Hallazgo:");
            bool any = Apply(player, spec, 70 + tier * 10, text);
            HudNotices.Post(any ? text.ToString() : "Inventario lleno: no puedes llevarte el hallazgo",
                any ? HudNotices.Reward : HudNotices.Warning, 3.2f);
        }

        /// <summary>Botin de cofre segun rareza (0 comun, 1 raro, 2 epico) mas lo indicado en <paramref name="contents"/>.</summary>
        public static void GrantChest(GameObject player, int rarity, string contents, System.Random random = null)
        {
            rarity = Mathf.Clamp(rarity, 0, 2);
            System.Random rng = random ?? new System.Random();
            RewardSpec spec = Parse(contents);
            void Add(ResourceKind kind, int min, int max)
            {
                int amount = rng.Next(min, max + 1);
                int index = spec.Kinds.IndexOf(kind);
                if (index >= 0) spec.Amounts[index] += amount;
                else { spec.Kinds.Add(kind); spec.Amounts.Add(amount); }
            }
            switch (rarity)
            {
                case 0:
                    ResourceKind[] common = { ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Fiber, ResourceKind.Food };
                    int first = rng.Next(common.Length), second = (first + 1 + rng.Next(common.Length - 1)) % common.Length;
                    Add(common[first], 4, 7);
                    Add(common[second], 3, 6);
                    if (rng.NextDouble() < 0.3) Add(ResourceKind.Hide, 1, 2);
                    break;
                case 1:
                    Add(ResourceKind.Ore, 3, 5);
                    Add(ResourceKind.Hide, 2, 4);
                    Add(ResourceKind.Essence, 1, 2);
                    if (rng.NextDouble() < 0.4) Add(ResourceKind.Crystal, 1, 2);
                    break;
                default:
                    Add(ResourceKind.Crystal, 3, 5);
                    Add(ResourceKind.Essence, 3, 4);
                    Add(ResourceKind.Ore, 4, 6);
                    spec.Gear = true;
                    spec.Essence = true;
                    break;
            }
            StringBuilder text = new StringBuilder("Cofre ").Append(RarityNames[rarity]).Append(':');
            bool any = Apply(player, spec, rarity == 2 ? 85 : 65, text);
            HudNotices.Post(any ? text.ToString() : "Inventario lleno: el botín se queda en el cofre",
                any ? RarityColor(rarity) : HudNotices.Warning, 3.4f);
        }

        /// <summary>Aplica una recompensa. Devuelve true si el jugador recibio algo.</summary>
        private static bool Apply(GameObject player, RewardSpec spec, int essenceQuality, StringBuilder text)
        {
            if (player == null)
                return false;
            SurvivalInventory inventory = player.GetComponent<SurvivalInventory>();
            bool any = false;
            if (inventory != null)
            {
                for (int i = 0; i < spec.Kinds.Count; i++)
                {
                    int added = inventory.Add(spec.Kinds[i], spec.Amounts[i]);
                    if (added <= 0)
                        continue;
                    any = true;
                    text.Append(" +").Append(added).Append(' ').Append(HarvestNode.ResourceName(spec.Kinds[i]));
                }
                if (spec.Gear && GiveForgeMaterials(player, inventory, text))
                    any = true;
            }
            if (spec.Essence)
            {
                EssenceReservoir reservoir = player.GetComponent<EssenceReservoir>();
                if (reservoir != null)
                {
                    EssenceKind kind = (EssenceKind)Random.Range(1, 6);
                    int quality = Mathf.Clamp(essenceQuality + Random.Range(-10, 11), 30, 100);
                    reservoir.Absorb(kind, quality);
                    text.Append(" · esencia ").Append(EssenceCatalog.Name(kind)).Append(' ').Append(quality).Append(" %");
                    any = true;
                }
            }
            return any;
        }

        /// <summary>Materiales para forjar la siguiente calidad de la pieza mas floja.</summary>
        private static bool GiveForgeMaterials(GameObject player, SurvivalInventory inventory, StringBuilder text)
        {
            PlayerEquipment gear = player.GetComponent<PlayerEquipment>();
            if (gear == null)
                return false;
            GearSlot weakest = GearSlot.Weapon;
            int lowest = int.MaxValue;
            foreach (GearSlot slot in new[] { GearSlot.Weapon, GearSlot.Armor, GearSlot.Talisman })
            {
                int tier = gear.TierOf(slot);
                if (tier < lowest)
                {
                    lowest = tier;
                    weakest = slot;
                }
            }
            if (lowest >= GearCatalog.MaxTier)
            {
                int added = inventory.Add(ResourceKind.Crystal, 4);
                if (added > 0)
                    text.Append(" +").Append(added).Append(' ').Append(HarvestNode.ResourceName(ResourceKind.Crystal));
                return added > 0;
            }
            GearTier next = GearCatalog.Get(lowest + 1);
            bool any = false;
            foreach (ResourceIngredient ingredient in next.Cost)
                if (inventory.Add(ingredient.Kind, ingredient.Amount) > 0)
                    any = true;
            if (any)
                text.Append(" · materiales para forjar ").Append(GearCatalog.SlotName(weakest)).Append(' ').Append(next.Suffix);
            return any;
        }
    }
}
