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
        [SerializeField] private bool instantHorizontalMovement = true;
        [Tooltip("Rapidez al arrancar o cambiar hacia un lado perpendicular.")]
        [SerializeField, Min(0f)] private float acceleration = 45f;
        [Tooltip("Rapidez al soltar WASD.")]
        [SerializeField, Min(0f)] private float braking = 75f;
        [Tooltip("Rapidez al invertir el sentido del movimiento.")]
        [SerializeField, Min(0f)] private float reversalAcceleration = 100f;

        [Header("Esquiva")]
        [Tooltip("Metros que recorre la esquiva.")]
        [SerializeField, Min(0f)] private float dashDistance = 4.5f;
        [Tooltip("Segundos que dura. Menos tiempo = mas brusca.")]
        [SerializeField, Min(0.01f)] private float dashDuration = 0.18f;
        [Tooltip("Segundos entre el inicio de una esquiva y el de la siguiente.")]
        [SerializeField, Min(0f)] private float dashCooldown = 1.2f;
        [Tooltip("Si se pulsa un poco antes de que termine la espera, la esquiva sale en cuanto puede.")]
        [SerializeField, Min(0f)] private float dashBufferTime = 0.15f;

        [Header("Orientacion visual")]
        [Tooltip("Opcional: hijo visual. Si se asigna, solo el modelo gira al apuntar.")]
        [SerializeField] private Transform visualRoot;

        private CharacterController controller;
        private float verticalSpeed;
        private Vector3 horizontalVelocity;
        private PlayerDash dash;

        public Vector3 HorizontalVelocity => horizontalVelocity;
        public bool IsDashing => dash.IsActive;
        public bool IsDashReady => dash.IsReady;
        public float DashCooldownRemaining => dash.CooldownRemaining;
        public Vector3 CurrentFacing { get; private set; } = Vector3.forward;
        public Vector3 ActualVelocity { get; private set; }
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            // Con el valor por defecto (1 mm) el CharacterController descarta los pasos
            // pequenos. A muchos FPS todos los pasos de arranque son menores que eso y
            // el personaje no llegaba a moverse nunca. Cero es el valor que recomienda Unity.
            controller.minMoveDistance = 0f;

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

        private void OnDisable()
        {
            dash.Cancel();
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
            move = ClampPlanarMagnitude(move, 1f);

            // Mantener contacto estable con el suelo.
            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;

            // Suavizar solo el desplazamiento: apuntar con el mouse sigue siendo independiente.
            float speed = IsFinite(movementSpeed) ? Mathf.Max(0f, movementSpeed) : 0f;
            Vector3 targetHorizontalVelocity = move * speed;

            Vector3 facing = IsFinite(command.FaceWorld) ? command.FaceWorld : Vector3.zero;
            facing.y = 0f;
            facing = NormalizePlanar(facing);

            // La esquiva va hacia donde se camina; quieto, hacia donde se apunta.
            Vector3 dashPreference = move.sqrMagnitude > 0.0001f
                ? move
                : facing != Vector3.zero ? facing : CurrentFacing;
            float dashTime = dash.Step(
                command.Dash, dashPreference, dashDuration, dashCooldown, dashBufferTime, deltaTime);

            Vector3 stepHorizontalVelocity;
            if (dashTime > 0f)
            {
                // El paso puede repartirse entre esquiva y marcha normal: asi la distancia
                // no depende de cuantos frames dure. Al terminar se retoma la velocidad pedida.
                horizontalVelocity = targetHorizontalVelocity;
                Vector3 dashDisplacement = dash.Direction * (SafeDashSpeed() * dashTime);
                Vector3 walkDisplacement = targetHorizontalVelocity * (deltaTime - dashTime);
                stepHorizontalVelocity = (dashDisplacement + walkDisplacement) / deltaTime;
                if (!IsFinite(stepHorizontalVelocity))
                    stepHorizontalVelocity = targetHorizontalVelocity;
            }
            else
            {
                horizontalVelocity = PlayerHorizontalMotion.Step(
                    horizontalVelocity,
                    targetHorizontalVelocity,
                    acceleration,
                    braking,
                    reversalAcceleration,
                    deltaTime,
                    instantHorizontalMovement
                );
                stepHorizontalVelocity = horizontalVelocity;
            }

            // Este prototipo no incluye saltos. El motor no controla entradas.
            // Una configuracion corrupta no debe propagar NaN/Infinity al CharacterController.
            float safeGravity = IsFinite(gravity) ? gravity : 0f;
            float gravityStep = safeGravity * deltaTime;
            if (IsFinite(gravityStep))
                verticalSpeed += gravityStep;
            else
                verticalSpeed = 0f;
            if (!IsFinite(verticalSpeed))
                verticalSpeed = 0f;
            Vector3 velocity = stepHorizontalVelocity + Vector3.up * verticalSpeed;
            Vector3 previousPosition = transform.position;
            CollisionFlags flags = controller.Move(velocity * deltaTime);
            IsGrounded = (flags & CollisionFlags.Below) != 0;
            if (IsGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            // Medir el desplazamiento con el mismo delta del comando mantiene el motor
            // consistente si la simulacion se ejecuta a un tick distinto del frame de Unity.
            ActualVelocity = (transform.position - previousPosition) / deltaTime;
            if (!IsFinite(ActualVelocity))
                ActualVelocity = Vector3.zero;

            // Solo si choco de lado se sustituye la velocidad por la que de verdad logro:
            // asi no queda inercia bloqueada que se dispare al liberar la pared. Sin choque
            // se conserva la velocidad calculada; medirla en cada paso la contaminaba con
            // el redondeo de la posicion y con los pasos que el controlador descarta.
            if (dashTime <= 0f && (flags & CollisionFlags.Sides) != 0)
            {
                Vector3 achieved = new Vector3(ActualVelocity.x, 0f, ActualVelocity.z);
                horizontalVelocity = Vector3.ClampMagnitude(achieved, horizontalVelocity.magnitude);
            }

            if (facing == Vector3.zero)
                return;

            CurrentFacing = facing;

            // Si existe un hijo visual, no rotar la raiz fisica del jugador.
            Transform targetTransform = visualRoot != null ? visualRoot : transform;
            Quaternion targetRotation = Quaternion.LookRotation(CurrentFacing, Vector3.up);
            float safeTurningSpeed = IsFinite(turningSpeedDegrees)
                ? Mathf.Max(0f, turningSpeedDegrees)
                : 0f;
            float maxTurn = Mathf.Min(safeTurningSpeed * deltaTime, 360f);
            targetTransform.rotation = Quaternion.RotateTowards(
                targetTransform.rotation, targetRotation, maxTurn
            );
        }

        private float SafeDashSpeed()
        {
            if (!IsFinite(dashDistance) || !IsFinite(dashDuration) || dashDuration <= 0f)
                return 0f;
            float dashSpeed = Mathf.Max(0f, dashDistance) / dashDuration;
            return IsFinite(dashSpeed) ? dashSpeed : 0f;
        }

        private static Vector3 ClampPlanarMagnitude(Vector3 value, float maxMagnitude)
        {
            float largestComponent = Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.z));
            if (largestComponent <= 0f || largestComponent <= maxMagnitude)
                return Vector3.ClampMagnitude(value, maxMagnitude);

            // Escalar antes de normalizar evita overflow en sqrMagnitude para entradas finitas enormes.
            Vector3 scaled = value / largestComponent;
            return scaled.normalized * maxMagnitude;
        }

        private static Vector3 NormalizePlanar(Vector3 value)
        {
            float largestComponent = Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.z));
            if (!IsFinite(largestComponent) || largestComponent <= 0f)
                return Vector3.zero;

            Vector3 scaled = value / largestComponent;
            float squareMagnitude = scaled.sqrMagnitude;
            if (!IsFinite(squareMagnitude) || squareMagnitude <= 0.0001f)
                return Vector3.zero;
            return scaled / Mathf.Sqrt(squareMagnitude);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
