using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Movimiento del personaje sin leer teclado ni raton.
    /// La capa de entrada (y mas adelante la capa de red) decide que comandos enviar.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField, Min(0f)] private float movementSpeed = 6f;
        [SerializeField, Min(0f)] private float turningSpeedDegrees = 1080f;
        [SerializeField] private float gravity = -25f;

        private CharacterController controller;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        /// <summary>
        /// Ejecutar una vez por frame de simulacion.
        /// worldMovement y worldFacing son direcciones en coordenadas del mundo.
        /// </summary>
        public void Simulate(Vector3 worldMovement, Vector3 worldFacing, float deltaTime)
        {
            if (!isActiveAndEnabled || deltaTime <= 0f)
                return;

            worldMovement.y = 0f;
            worldMovement = Vector3.ClampMagnitude(worldMovement, 1f);

            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;

            verticalSpeed += gravity * deltaTime;
            Vector3 velocity = worldMovement * movementSpeed + Vector3.up * verticalSpeed;
            controller.Move(velocity * deltaTime);

            // Apuntar no debe alterar la direccion de desplazamiento.
            worldFacing.y = 0f;
            if (worldFacing.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(worldFacing.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, turningSpeedDegrees * deltaTime
                );
            }
        }
    }
}
