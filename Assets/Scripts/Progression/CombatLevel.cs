using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Progression
{
    /// <summary>
    /// Nivel de un enemigo. Compara con el nivel de equipo del jugador local: si el
    /// jugador va por debajo, el enemigo recibe menos daño y pega mas fuerte.
    /// Sin <see cref="PlayerEquipment"/> activo (salas de prueba) no cambia nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatLevel : MonoBehaviour, IDamageTakenModifier
    {
        [SerializeField, Range(1, 40)] private int level = 1;

        public int Level => level;

        public void Configure(int value) => level = Mathf.Clamp(value, 1, 40);

        /// <summary>El daño que recibe este enemigo viene del jugador.</summary>
        public float IncomingMultiplier
        {
            get
            {
                PlayerEquipment gear = PlayerEquipment.Local;
                return gear != null ? GearCatalog.DamageDealtMultiplier(gear.GearLevel, level) : 1f;
            }
        }

        /// <summary>Multiplicador del daño que hace este enemigo al jugador.</summary>
        public float OutgoingMultiplier
        {
            get
            {
                PlayerEquipment gear = PlayerEquipment.Local;
                return gear != null ? GearCatalog.DamageTakenMultiplier(gear.GearLevel, level) : 1f;
            }
        }

        public static float OutgoingFor(Component enemy)
        {
            CombatLevel combatLevel = enemy != null ? enemy.GetComponent<CombatLevel>() : null;
            return combatLevel != null ? combatLevel.OutgoingMultiplier : 1f;
        }

        /// <summary>Nivel de un enemigo: el de su tipo, o uno por zona para los clasicos.</summary>
        public static int LevelFor(PrototypeEnemy enemy)
        {
            if (enemy == null)
                return 1;
            if (enemy.Archetype != null)
                return enemy.Archetype.Level;
            if (enemy.IsBoss)
                return 26;
            string name = enemy.name.ToLowerInvariant();
            if (name.Contains("guardian"))
                return 13;
            if (name.Contains("centinela"))
                return 9;
            return 6;
        }
    }
}
