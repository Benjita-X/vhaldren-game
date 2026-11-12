using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Races
{
    /// <summary>
    /// En cada enemigo: aplica el poder fisico o magico de la raza del jugador local
    /// al daño que recibe (los enemigos solo reciben daño de jugadores).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RacePowerTaken : MonoBehaviour, IDamageKindModifier
    {
        public float Multiplier(CombatDamageKind kind)
        {
            PlayerRace player = PlayerRace.Local;
            if (player == null)
                return 1f;
            bool magical = kind == CombatDamageKind.Magic ||
                kind == CombatDamageKind.Explosion ||
                kind == CombatDamageKind.DamageOverTime;
            return magical ? player.SpellMultiplier : player.PhysicalMultiplier;
        }
    }
}
