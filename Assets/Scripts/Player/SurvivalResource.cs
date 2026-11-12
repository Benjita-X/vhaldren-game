using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Nodo recolectable: arbol (hacha), roca (pico) o arbusto de bayas (cualquier mano).
    /// Entrega materiales solo si caben en el inventario; agotado, desaparece su raiz.
    /// </summary>
    public sealed class SurvivalResource : MonoBehaviour
    {
        public enum Kind { Wood, Stone, Berries }

        [SerializeField] private Kind kind = Kind.Wood;
        [SerializeField, Min(1)] private int remaining = 12;
        [SerializeField, Min(1)] private int yieldPerHit = 2;
        [Tooltip("Objeto que se elimina al agotarse (p. ej. el arbol completo, no solo el tronco).")]
        [SerializeField] private Transform removeRoot;

        private Vector3 baseScale;
        private float shakeUntil;

        public Kind ResourceKind => kind;
        public int Remaining => remaining;

        public void Configure(Kind resourceKind, int amount, int perHit, Transform root)
        {
            kind = resourceKind;
            remaining = Mathf.Max(1, amount);
            yieldPerHit = Mathf.Max(1, perHit);
            removeRoot = root;
        }

        public SurvivalInventory.Item Produces => kind == Kind.Wood ? SurvivalInventory.Item.Wood
            : kind == Kind.Stone ? SurvivalInventory.Item.Stone : SurvivalInventory.Item.Food;

        public SurvivalInventory.Item RequiredTool => kind == Kind.Wood ? SurvivalInventory.Item.Axe
            : kind == Kind.Stone ? SurvivalInventory.Item.Pickaxe : SurvivalInventory.Item.Empty;

        /// <summary>Devuelve un texto de resultado para mostrar al jugador.</summary>
        public string Harvest(SurvivalInventory inventory, SurvivalInventory.Item tool)
        {
            if (inventory == null || remaining <= 0)
                return "";
            SurvivalInventory.Item required = RequiredTool;
            if (required != SurvivalInventory.Item.Empty && tool != required)
                return "Necesitas " + SurvivalInventory.DisplayName(required) + " para esto";

            int accepted = inventory.Add(Produces, Mathf.Min(yieldPerHit, remaining));
            if (accepted <= 0)
                return "Inventario lleno";
            remaining -= accepted;
            Shake();
            SpellImpactFlash.Spawn(transform.position + Vector3.up * 0.8f,
                SurvivalInventory.ItemColor(Produces), 0.25f, 0.18f);
            if (remaining <= 0)
            {
                Transform root = removeRoot != null ? removeRoot : transform;
                Destroy(root.gameObject);
            }
            return "+" + accepted + " " + SurvivalInventory.DisplayName(Produces) +
                (remaining > 0 ? "  (quedan " + remaining + ")" : "  (agotado)");
        }

        private void Awake() => baseScale = transform.localScale;

        private void Shake()
        {
            if (baseScale == Vector3.zero)
                baseScale = transform.localScale;
            shakeUntil = Time.time + 0.18f;
        }

        private void Update()
        {
            if (shakeUntil <= 0f)
                return;
            if (Time.time >= shakeUntil)
            {
                transform.localScale = baseScale;
                shakeUntil = 0f;
                return;
            }
            float k = 1f + Mathf.Sin(Time.time * 70f) * 0.06f;
            transform.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z * k);
        }
    }
}
