using System;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>Esencia que contiene un enemigo. Cambia cada vez que reaparece.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class EssenceSource : MonoBehaviour
    {
        private static int seedCounter = 7919;
        private static readonly System.Collections.Generic.List<EssenceSource> all =
            new System.Collections.Generic.List<EssenceSource>();

        public static System.Collections.Generic.IReadOnlyList<EssenceSource> All => all;

        [SerializeField] private EssenceKind kind = EssenceKind.None;
        [SerializeField, Range(0, 100)] private int quality;

        private CombatHealth health;
        private System.Random random;

        public EssenceKind Kind => kind;
        public int Quality => quality;
        public CombatHealth Health => health;

        public static EssenceSource For(Component target)
        {
            if (target == null)
                return null;
            CombatHealth body = target.GetComponentInParent<CombatHealth>();
            if (body == null)
                return null;
            EssenceSource source = body.GetComponent<EssenceSource>();
            return source != null ? source : body.gameObject.AddComponent<EssenceSource>();
        }

        public void Configure(EssenceKind essence, int essenceQuality)
        {
            kind = essence;
            quality = Mathf.Clamp(essenceQuality, 0, 100);
        }

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            random = new System.Random(unchecked(seedCounter++ * 31 + name.GetHashCode()));
            if (kind == EssenceKind.None)
                Reroll();
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            health.Defeated += Reroll;
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            if (health != null)
                health.Defeated -= Reroll;
        }

        /// <summary>Al caer, la siguiente reaparicion traera otra esencia.</summary>
        public void Reroll()
        {
            PrototypeEnemy enemy = GetComponent<PrototypeEnemy>();
            bool boss = enemy != null && enemy.IsBoss;
            kind = EssenceCatalog.RollKind(random, boss);
            quality = EssenceCatalog.RollQuality(random, boss);
        }
    }
}
