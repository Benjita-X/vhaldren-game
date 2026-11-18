using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.Survival;

namespace Vhaldren.Gameplay.Mounts
{
    /// <summary>
    /// Correr a pie: mantener Mayús (reasignable) mientras se camina da +45 % de velocidad
    /// gastando aguante de viaje. Usa un modificador de <see cref="PlayerActionModifiers"/>,
    /// asi que la esquiva no cambia. Inactivo en montura, construyendo o aturdido.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSprint : MonoBehaviour
    {
        public const float SpeedBonus = 1.45f;
        public const float DrainPerSecond = 17f;

        private PlayerMotor motor;
        private PlayerActionModifiers modifiers;
        private TravelStamina stamina;
        private PlayerBuilder builder;
        private CombatHealth health;
        private int token;

        public bool IsSprinting => token != 0;

        private void Lookup()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (modifiers == null) modifiers = GetComponent<PlayerActionModifiers>();
            if (stamina == null) stamina = GetComponent<TravelStamina>();
            if (builder == null) builder = GetComponent<PlayerBuilder>();
            if (health == null) health = GetComponent<CombatHealth>();
        }

        private void Update()
        {
            Lookup();
            bool sprint = GameInput.Held(GameAction.Sprint) && CanSprint() &&
                          stamina.Use(DrainPerSecond, Time.deltaTime);
            if (sprint && token == 0)
                token = modifiers.AddModifier(PlayerActionModifiers.LockedAction.None, SpeedBonus);
            else if (!sprint && token != 0)
                Release();
        }

        private bool CanSprint()
        {
            if (motor == null || !motor.isActiveAndEnabled || modifiers == null || stamina == null ||
                !modifiers.AllowsMovement || Time.timeScale <= 0f)
                return false;
            IPlayerLocomotionOverride external = motor.LocomotionOverride;
            if (external != null && external.OverridesLocomotion)
                return false;
            if ((builder != null && builder.IsBuilding) || (health != null && health.IsDefeated))
                return false;
            Vector3 velocity = motor.ActualVelocity;
            return velocity.x * velocity.x + velocity.z * velocity.z > 0.25f;
        }

        private void Release()
        {
            if (token != 0 && modifiers != null)
                modifiers.RemoveModifier(token);
            token = 0;
        }

        private void OnDisable() => Release();
    }
}
