using UnityEngine;
using Vhaldren.Gameplay.CameraSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Adaptador exclusivo del jugador local. Nunca activar en avatares remotos.
    /// Usa el nuevo Input System cuando esta habilitado y el legado como alternativa.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class LocalPlayerInput : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("Radio en metros alrededor del personaje donde el cursor no cambia hacia donde mira. " +
                 "Evita que el modelo gire de golpe al pasar el mouse por encima de el.")]
        [SerializeField, Min(0f)] private float aimDeadZone = 0.35f;
        [Tooltip("Cuanto cambia el zoom por cada paso de la rueda del mouse.")]
        [SerializeField, Min(0f)] private float zoomStep = 0.1f;

        private PlayerMotor motor;
        private LocalPlayerShooter shooter;
        private OpposingKeysAxis horizontalKeys;
        private OpposingKeysAxis verticalKeys;
        private SimpleFollowCamera followCamera;
        private Camera followCameraSource;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            shooter = GetComponent<LocalPlayerShooter>();
#if !ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogError(
                "[Vhaldren] No hay sistema de entrada habilitado. Configura Active Input Handling en Player Settings.",
                this);
#endif
        }

        private void OnDisable()
        {
            ForgetHeldKeys();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Una tecla soltada fuera de la ventana no debe seguir contando como pulsada.
            if (!hasFocus)
                ForgetHeldKeys();
        }

        private void ForgetHeldKeys()
        {
            horizontalKeys.Reset();
            verticalKeys.Reset();
        }

        /// <summary>
        /// Direccion plana hacia el punto apuntado. Dentro de la zona muerta conserva la
        /// orientacion actual: tan cerca del personaje un pixel de mouse la invertiria.
        /// </summary>
        public static Vector3 ResolveFacing(
            Vector3 origin, Vector3 aimPoint, Vector3 currentFacing, float deadZone)
        {
            Vector3 planar = aimPoint - origin;
            planar.y = 0f;
            float radius = float.IsNaN(deadZone) ? 0f : Mathf.Max(deadZone, 0.01f);
            float squareDistance = planar.sqrMagnitude;
            if (float.IsNaN(squareDistance) || float.IsInfinity(squareDistance) ||
                squareDistance <= radius * radius)
                return currentFacing;
            return planar;
        }

        private void Update()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            Vector2 axes = ReadMovement();
            // Sin camara se usa un marco mundial estable, pero se sigue simulando:
            // gravedad, contacto y frenado no deben depender de una referencia visual.
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (viewCamera != null)
            {
                forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f)
                    forward = Vector3.forward;
                forward.Normalize();
                right = Vector3.Cross(Vector3.up, forward);
            }
            Vector3 move = Vector3.ClampMagnitude(right * axes.x + forward * axes.y, 1f);
            // Preservar la ultima orientacion si el cursor no corta el plano.
            Vector3 facing = motor.CurrentFacing;

            if (viewCamera != followCameraSource)
            {
                followCameraSource = viewCamera;
                followCamera = viewCamera != null ? viewCamera.GetComponent<SimpleFollowCamera>() : null;
            }

            if (viewCamera != null && TryReadPointer(out Vector2 pointer))
            {
                // Rueda hacia delante = acercar. Solo con el cursor dentro de la ventana de juego.
                float scroll = ReadScroll();
                if (followCamera != null && scroll != 0f)
                    followCamera.AddZoom(-scroll * zoomStep);

                if (shooter != null && shooter.TryGetAimPoint(pointer, out Vector3 aimPoint))
                {
                    facing = ResolveFacing(transform.position, aimPoint, facing, aimDeadZone);
                }
                else
                {
                    Ray ray = viewCamera.ScreenPointToRay(pointer);
                    Plane plane = new Plane(Vector3.up, transform.position);
                    if (plane.Raycast(ray, out float distance))
                        facing = ResolveFacing(transform.position, ray.GetPoint(distance), facing, aimDeadZone);
                }
            }

            motor.Simulate(new PlayerMotionCommand(move, facing, ReadDashPressed()), Time.deltaTime);
        }

        private Vector2 ReadMovement()
        {
            bool left = false, right = false, back = false, forward = false;
            // "Pulsada ahora" o "bajo en este frame": un toque mas corto que un frame
            // (se pulsa y se suelta entre dos lecturas) tambien cuenta como un paso.
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                left = keyboard.aKey.isPressed || keyboard.aKey.wasPressedThisFrame;
                right = keyboard.dKey.isPressed || keyboard.dKey.wasPressedThisFrame;
                back = keyboard.sKey.isPressed || keyboard.sKey.wasPressedThisFrame;
                forward = keyboard.wKey.isPressed || keyboard.wKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            left = Input.GetKey(KeyCode.A) || Input.GetKeyDown(KeyCode.A);
            right = Input.GetKey(KeyCode.D) || Input.GetKeyDown(KeyCode.D);
            back = Input.GetKey(KeyCode.S) || Input.GetKeyDown(KeyCode.S);
            forward = Input.GetKey(KeyCode.W) || Input.GetKeyDown(KeyCode.W);
#endif
            // Teclas opuestas a la vez: manda la ultima pulsada (ver OpposingKeysAxis).
            Vector2 axes = new Vector2(
                horizontalKeys.Resolve(left, right),
                verticalKeys.Resolve(back, forward));
            return Vector2.ClampMagnitude(axes, 1f);
        }

        private static bool ReadDashPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        /// <summary>Pasos de rueda en este frame; positivo hacia delante.</summary>
        private static float ReadScroll()
        {
            float steps = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                steps = Mouse.current.scroll.ReadValue().y / 120f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            steps = Input.mouseScrollDelta.y;
#endif
            return float.IsNaN(steps) ? 0f : Mathf.Clamp(steps, -3f, 3f);
        }

        private static bool TryReadPointer(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
                return false;
            screenPosition = Mouse.current.position.ReadValue();
            return IsInsideGameViewport(screenPosition);
#elif ENABLE_LEGACY_INPUT_MANAGER
            screenPosition = Input.mousePosition;
            return IsInsideGameViewport(screenPosition);
#else
            return false;
#endif
        }

        private static bool IsInsideGameViewport(Vector2 screenPosition)
        {
            return screenPosition.x >= 0f && screenPosition.y >= 0f &&
                   screenPosition.x < Screen.width && screenPosition.y < Screen.height;
        }
    }
}
