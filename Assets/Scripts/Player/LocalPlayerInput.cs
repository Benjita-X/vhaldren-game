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
        private readonly KeyboardMovementBuffer keyboardMovement = new KeyboardMovementBuffer();
        private SimpleFollowCamera followCamera;
        private Camera followCameraSource;

#if ENABLE_INPUT_SYSTEM
        private InputAction moveLeftAction;
        private InputAction moveRightAction;
        private InputAction moveBackAction;
        private InputAction moveForwardAction;
        private InputAction dashAction;
        private bool dashRequested;
#endif

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
#if ENABLE_INPUT_SYSTEM
            DisableMovementActions();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void OnEnable()
        {
            CreateMovementActions();
            EnableMovementActions();
            SynchronizeHeldKeys();
        }

        private void OnDestroy()
        {
            DisposeMovementActions();
        }

        private void CreateMovementActions()
        {
            if (moveLeftAction != null)
                return;

            moveLeftAction = new InputAction("MoveLeft", InputActionType.Button, "<Keyboard>/a");
            moveRightAction = new InputAction("MoveRight", InputActionType.Button, "<Keyboard>/d");
            moveBackAction = new InputAction("MoveBack", InputActionType.Button, "<Keyboard>/s");
            moveForwardAction = new InputAction("MoveForward", InputActionType.Button, "<Keyboard>/w");
            dashAction = new InputAction("Dash", InputActionType.Button, "<Keyboard>/space");
            dashAction.performed += HandleDashPressed;

            moveLeftAction.performed += HandleMovementKeyPressed;
            moveRightAction.performed += HandleMovementKeyPressed;
            moveBackAction.performed += HandleMovementKeyPressed;
            moveForwardAction.performed += HandleMovementKeyPressed;
            moveLeftAction.canceled += HandleMovementKeyReleased;
            moveRightAction.canceled += HandleMovementKeyReleased;
            moveBackAction.canceled += HandleMovementKeyReleased;
            moveForwardAction.canceled += HandleMovementKeyReleased;
        }

        private void EnableMovementActions()
        {
            moveLeftAction.Enable();
            moveRightAction.Enable();
            moveBackAction.Enable();
            moveForwardAction.Enable();
            dashAction.Enable();
        }

        private void DisableMovementActions()
        {
            moveLeftAction?.Disable();
            moveRightAction?.Disable();
            moveBackAction?.Disable();
            moveForwardAction?.Disable();
            dashAction?.Disable();
        }

        private void DisposeMovementActions()
        {
            moveLeftAction?.Dispose();
            moveRightAction?.Dispose();
            moveBackAction?.Dispose();
            moveForwardAction?.Dispose();
            dashAction?.Dispose();
            moveLeftAction = moveRightAction = moveBackAction = moveForwardAction = null;
            dashAction = null;
        }

        private void HandleDashPressed(InputAction.CallbackContext context)
        {
            // Igual que WASD, conservar una pulsacion corta hasta que el motor la lea.
            dashRequested = true;
        }

        private void HandleMovementKeyPressed(InputAction.CallbackContext context)
        {
            keyboardMovement.SetPressed(GetMovementKey(context.action), true);
        }

        private void HandleMovementKeyReleased(InputAction.CallbackContext context)
        {
            keyboardMovement.SetPressed(GetMovementKey(context.action), false);
        }

        private KeyboardMovementBuffer.Key GetMovementKey(InputAction action)
        {
            if (action == moveLeftAction) return KeyboardMovementBuffer.Key.Left;
            if (action == moveRightAction) return KeyboardMovementBuffer.Key.Right;
            if (action == moveBackAction) return KeyboardMovementBuffer.Key.Back;
            return KeyboardMovementBuffer.Key.Forward;
        }

        private void SynchronizeHeldKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            keyboardMovement.SetPressed(KeyboardMovementBuffer.Key.Left, keyboard.aKey.isPressed);
            keyboardMovement.SetPressed(KeyboardMovementBuffer.Key.Right, keyboard.dKey.isPressed);
            keyboardMovement.SetPressed(KeyboardMovementBuffer.Key.Back, keyboard.sKey.isPressed);
            keyboardMovement.SetPressed(KeyboardMovementBuffer.Key.Forward, keyboard.wKey.isPressed);
        }
#endif

        private void OnApplicationFocus(bool hasFocus)
        {
            // Una tecla soltada fuera de la ventana no debe seguir contando como pulsada.
            if (!hasFocus)
                ForgetHeldKeys();
#if ENABLE_INPUT_SYSTEM
            else
                SynchronizeHeldKeys();
#endif
        }

        private void ForgetHeldKeys()
        {
            keyboardMovement.Reset();
            horizontalKeys.Reset();
            verticalKeys.Reset();
#if ENABLE_INPUT_SYSTEM
            dashRequested = false;
#endif
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
            if (SurvivalInventory.BlocksInput(gameObject))
            {
                ReadDashPressed();
                motor.Simulate(new PlayerMotionCommand(Vector3.zero, motor.CurrentFacing, false), Time.deltaTime);
                return;
            }
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
#if ENABLE_INPUT_SYSTEM
            keyboardMovement.Consume(out float horizontal, out float vertical);
            Vector2 axes = new Vector2(horizontal, vertical);
#elif ENABLE_LEGACY_INPUT_MANAGER
            bool left = Input.GetKey(KeyCode.A) || Input.GetKeyDown(KeyCode.A);
            bool right = Input.GetKey(KeyCode.D) || Input.GetKeyDown(KeyCode.D);
            bool back = Input.GetKey(KeyCode.S) || Input.GetKeyDown(KeyCode.S);
            bool forward = Input.GetKey(KeyCode.W) || Input.GetKeyDown(KeyCode.W);
            // Teclas opuestas a la vez: manda la ultima pulsada (ver OpposingKeysAxis).
            Vector2 axes = new Vector2(
                horizontalKeys.Resolve(left, right),
                verticalKeys.Resolve(back, forward));
#else
            Vector2 axes = Vector2.zero;
#endif
            return Vector2.ClampMagnitude(axes, 1f);
        }

        private bool ReadDashPressed()
        {
#if ENABLE_INPUT_SYSTEM
            bool requested = dashRequested;
            dashRequested = false;
            return requested;
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
            {
                // Unity normaliza por defecto a -1..1. Solo si alguien eligio el modo
                // nativo, Windows devuelve unidades de 120 por paso de rueda.
                steps = Mouse.current.scroll.ReadValue().y;
                bool windows = Application.platform == RuntimePlatform.WindowsEditor ||
                               Application.platform == RuntimePlatform.WindowsPlayer;
                if (windows && InputSystem.settings.scrollDeltaBehavior ==
                    InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange)
                    steps /= 120f;
            }
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
