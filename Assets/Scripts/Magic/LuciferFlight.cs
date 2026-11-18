using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Estado del Salto de alas de Lucifer: bloquea mover, atacar y esquivar mientras dura,
    /// apaga el CharacterController en el aire (cruza muros y vallas) y lo hace intocable.
    /// Si algo corta el salto a medias (se desactiva el grimorio, reaparece...), <see cref="Land"/>
    /// devuelve siempre el control: nunca deja al jugador sin colisiones ni bloqueado.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LuciferFlight : MonoBehaviour, IDamageAbsorber
    {
        private PlayerActionModifiers modifiers;
        private CharacterController body;
        private int lockToken;
        private bool bodyDisabled;

        /// <summary>En el aire: los golpes no le alcanzan.</summary>
        public bool Airborne { get; private set; }
        public bool Busy => lockToken != 0 || Airborne;
        public int Landings { get; private set; }

        public static LuciferFlight For(Component owner)
        {
            if (owner == null)
                return null;
            LuciferFlight flight = owner.GetComponent<LuciferFlight>();
            return flight != null ? flight : owner.gameObject.AddComponent<LuciferFlight>();
        }

        /// <summary>Empieza el salto: bloquea acciones (también durante el impulso).</summary>
        public void Begin()
        {
            if (modifiers == null)
                modifiers = GetComponent<PlayerActionModifiers>();
            if (modifiers != null && lockToken == 0)
                lockToken = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Movement |
                                                  PlayerActionModifiers.LockedAction.Attack |
                                                  PlayerActionModifiers.LockedAction.Dash |
                                                  PlayerActionModifiers.LockedAction.Facing);
        }

        /// <summary>Despega: sin colisiones durante el arco.</summary>
        public void TakeOff(CharacterController controller)
        {
            body = controller;
            if (body != null && body.enabled)
            {
                body.enabled = false;
                bodyDisabled = true;
            }
            Airborne = true;
        }

        /// <summary>Aterriza (o cancela): colisiones y control de vuelta.</summary>
        public void Land()
        {
            if (bodyDisabled && body != null)
                body.enabled = true;
            bool wasAirborne = Airborne;
            bodyDisabled = false;
            Airborne = false;
            if (lockToken != 0 && modifiers != null)
                modifiers.RemoveModifier(lockToken);
            lockToken = 0;
            if (wasAirborne)
                Landings++;
        }

        public float Absorb(float amount) => Airborne ? 0f : amount;

        private void OnDisable() => Land();
    }
}
