using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Ejecuta intenciones de locomocion en espacio del mundo.
    /// No lee teclado, raton, camara ni red. No decide autoridad de red.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("Locomocion")]
        [SerializeField, Min(0f)] private float movementSpeed = 6f;
        [SerializeField, Min(0f)] private float turningSpeedDegrees = 1080f;
        [SerializeField] private float gravity = -25f;

        [Header("Respuesta horizontal")]
        [Tooltip("Activado: respuesta inmediata identica a la del Paso 001.")]
        [SerializeField] private bool instantHorizontalMovement = false;
        [Tooltip("Rapidez al arrancar o cambiar hacia un lado perpendicular.")]
        [SerializeField, Min(0f)] private float acceleration = 45f;
        [Tooltip("Rapidez al soltar WASD.")]
        [SerializeField, Min(0f)] private float braking = 75f;
        [Tooltip("Rapidez al invertir el sentido del movimiento.")]
        [SerializeField, Min(0f)] private float reversalAcceleration = 100f;

        [Header("Orientacion visual")]
        [Tooltip("Opcional: hijo visual. Si se asigna, solo el modelo gira al apuntar.")]
        [SerializeField] private Transform visualRoot;

        private CharacterController controller;
        private float verticalSpeed;
        private Vector3 horizontalVelocity;

        public Vector3 HorizontalVelocity => horizontalVelocity;
        public Vector3 CurrentFacing { get; private set; } = Vector3.forward;
        public Vector3 ActualVelocity { get; private set; }
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            // Nunca girar un objeto visual externo o la propia raiz fisica por error.
            if (visualRoot != null && (visualRoot == transform || !visualRoot.IsChildOf(transform)))
            {
                Debug.LogError(
                    "[Vhaldren] visualRoot debe ser un hijo del PlayerRoot, nunca la raiz ni un objeto ajeno.",
                    this);
                enabled = false;
                return;
            }

            Vector3 initialFacing = visualRoot != null ? visualRoot.forward : transform.forward;
            initialFacing.y = 0f;
            CurrentFacing = initialFacing.sqrMagnitude > 0.0001f ? initialFacing.normalized : Vector3.forward;
        }

        /// <summary>
        /// Una simulacion por paso. En local se llama desde Update.
        /// Posteriormente un controlador autorizado podra suministrar comandos de red.
        /// </summary>
        public void Simulate(PlayerMotionCommand command, float deltaTime)
        {
            if (!isActiveAndEnabled || controller == null || !controller.enabled ||
                !IsFinite(deltaTime) || deltaTime <= 0f)
                return;

            Vector3 move = IsFinite(command.MoveWorld) ? command.MoveWorld : Vector3.zero;
            move.y = 0f;
            move = Vector3.ClampMagnitude(move, 1f);

            // Mantener contacto estable con el suelo.
            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;

            // Suavizar solo el desplazamiento: apuntar con el mouse sigue siendo independiente.
            float speed = IsFinite(movementSpeed) ? Mathf.Max(0f, movementSpeed) : 0f;
            Vector3 targetHorizontalVelocity = move * speed;
            horizontalVelocity = PlayerHorizontalMotion.Step(
                horizontalVelocity,
                targetHorizontalVelocity,
                acceleration,
                braking,
                reversalAcceleration,
                deltaTime,
                instantHorizontalMovement
            );

            // Este prototipo no incluye saltos ni dash. El motor no controla entradas.
            verticalSpeed += gravity * deltaTime;
            Vector3 velocity = horizontalVelocity + Vector3.up * verticalSpeed;
            CollisionFlags flags = controller.Move(velocity * deltaTime);
            IsGrounded = (flags & CollisionFlags.Below) != 0;
            if (IsGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            ActualVelocity = controller.velocity;

            // Si el CharacterController golpeo una pared, no guardar inercia
            // bloqueada que pueda dispararse al liberar la colision.
            horizontalVelocity = new Vector3(ActualVelocity.x, 0f, ActualVelocity.z);

            Vector3 facing = IsFinite(command.FaceWorld) ? command.FaceWorld : Vector3.zero;
            facing.y = 0f;
            if (facing.sqrMagnitude <= 0.0001f)
                return;

            CurrentFacing = facing.normalized;

            // Si existe un hijo visual, no rotar la raiz fisica del jugador.
            Transform targetTransform = visualRoot != null ? visualRoot : transform;
            Quaternion targetRotation = Quaternion.LookRotation(CurrentFacing, Vector3.up);
            targetTransform.rotation = Quaternion.RotateTowards(
                targetTransform.rotation, targetRotation, turningSpeedDegrees * deltaTime
            );
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
