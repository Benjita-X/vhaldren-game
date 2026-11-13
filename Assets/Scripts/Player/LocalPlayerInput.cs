using UnityEngine;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Adaptador exclusivo del jugador local. Nunca activar en avatares remotos.
    /// Usa el nuevo Input System cuando esta habilitado y el legado como alternativa.
    /// Orientacion: explorando, el personaje mira hacia donde camina; en combate (o justo
    /// despues de atacar, disparar, lanzar un hechizo o defenderse) mira al cursor y las
    /// piernas pasan a pasos laterales y retroceso.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class LocalPlayerInput : MonoBehaviour
    {
        /// <summary>Segundos que se sigue mirando al cursor despues de que termine el combate.</summary>
        public const float CombatFacingAfterCombat = 2f;
        /// <summary>Segundos que se sigue mirando al cursor despues de una accion propia.</summary>
        public const float CombatFacingAfterAction = 1.5f;

        [SerializeField] private Camera viewCamera;
        [Tooltip("Radio en metros alrededor del personaje donde el cursor no cambia hacia donde mira. " +
                 "Evita que el modelo gire de golpe al pasar el mouse por encima de el.")]
        [SerializeField, Min(0f)] private float aimDeadZone = 0.35f;
        [Tooltip("Cuanto cambia el zoom por cada paso de la rueda del mouse.")]
        [SerializeField, Min(0f)] private float zoomStep = 0.1f;
        [Tooltip("Grados que gira la camara por cada pixel arrastrado con el boton central (sensibilidad 1).")]
        [SerializeField, Min(0f)] private float dragDegreesPerPixel = 0.3f;
        [Tooltip("Grados por segundo al mantener las teclas de giro de camara (sensibilidad 1).")]
        [SerializeField, Min(0f)] private float keyRotationSpeed = 90f;
        [Tooltip("Explorando, grados por segundo con los que el personaje gira hacia donde camina.")]
        [SerializeField, Min(0f)] private float explorationTurnSpeed = 720f;

        private PlayerMotor motor;
        private LocalPlayerShooter shooter;
        private OpposingKeysAxis horizontalKeys;
        private OpposingKeysAxis verticalKeys;
        private readonly KeyboardMovementBuffer keyboardMovement = new KeyboardMovementBuffer();
        private SimpleFollowCamera followCamera;
        private Camera followCameraSource;
        private CombatState combatState;
        private CombatDefense defense;
        private PlayerSpellbook spellbook;
        private int observedSpells = -1;
        private int observedShots = -1;
        private float lastCombatTime = float.NegativeInfinity;
        private float lastActionTime = float.NegativeInfinity;

        /// <summary>True mientras el personaje mira al cursor (combate); false si mira hacia donde camina.</summary>
        public bool CombatFacing { get; private set; }

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
            GameInput.Changed -= RebuildMovementActions;
            DisableMovementActions();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void OnEnable()
        {
            CreateMovementActions();
            EnableMovementActions();
            SynchronizeHeldKeys();
            GameInput.Changed += RebuildMovementActions;
        }

        /// <summary>Al reasignar teclas en el menu, recrear las acciones con las nuevas.</summary>
        private void RebuildMovementActions()
        {
            ForgetHeldKeys();
            DisableMovementActions();
            DisposeMovementActions();
            CreateMovementActions();
            if (isActiveAndEnabled)
                EnableMovementActions();
        }

        private void OnDestroy()
        {
            DisposeMovementActions();
        }

        private void CreateMovementActions()
        {
            if (moveLeftAction != null)
                return;

            // Teclas del mapa reasignable (por defecto WASD y Espacio).
            moveLeftAction = new InputAction("MoveLeft", InputActionType.Button, GameInput.ControlPath(GameAction.MoveLeft));
            moveRightAction = new InputAction("MoveRight", InputActionType.Button, GameInput.ControlPath(GameAction.MoveRight));
            moveBackAction = new InputAction("MoveBack", InputActionType.Button, GameInput.ControlPath(GameAction.MoveBack));
            moveForwardAction = new InputAction("MoveForward", InputActionType.Button, GameInput.ControlPath(GameAction.MoveForward));
            dashAction = new InputAction("Dash", InputActionType.Button, GameInput.ControlPath(GameAction.Dash));
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

            keyboardMovement.SetPressed(KeyboardMovementBuffer.Key.Left, GameInput.Held(GameAction.MoveLeft));
            keyboardMovement.SetPressed(KeyboardMovementBuffer.Key.Right, GameInput.Held(GameAction.MoveRight));
            keyboardMovement.SetPressed(KeyboardMovementBuffer.Key.Back, GameInput.Held(GameAction.MoveBack));
            keyboardMovement.SetPressed(KeyboardMovementBuffer.Key.Forward, GameInput.Held(GameAction.MoveForward));
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

        /// <summary>
        /// WASD relativo al giro horizontal de la camara. Con la camara sin girar (mirando
        /// hacia +Z) coincide con los ejes del mundo: W = +Z, D = +X, como siempre.
        /// Sin camara, o mirando en vertical, se usa ese mismo marco del mundo.
        /// </summary>
        public static Vector3 ResolveMove(Vector2 axes, Vector3 cameraForward)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (float.IsNaN(forward.x) || float.IsNaN(forward.z) ||
                float.IsInfinity(forward.x) || float.IsInfinity(forward.z) ||
                forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return Vector3.ClampMagnitude(right * axes.x + forward * axes.y, 1f);
        }

        /// <summary>Posicion del cursor respecto al centro de la pantalla, en medias alturas.</summary>
        public static Vector2 PointerOffsetFromCenter(Vector2 pointer, float screenWidth, float screenHeight)
        {
            float halfHeight = Mathf.Max(1f, screenHeight * 0.5f);
            return new Vector2((pointer.x - screenWidth * 0.5f) / halfHeight,
                (pointer.y - screenHeight * 0.5f) / halfHeight);
        }

        private void Update()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            if (viewCamera != followCameraSource)
            {
                followCameraSource = viewCamera;
                followCamera = viewCamera != null ? viewCamera.GetComponent<SimpleFollowCamera>() : null;
            }

            // Giro opcional de la camara antes de leer WASD: se mueve segun lo que se ve.
            if (followCamera != null && followCamera.AllowRotation && Time.timeScale > 0f)
                RotateCamera();

            Vector2 axes = ReadMovement();
            // Sin camara se usa un marco mundial estable, pero se sigue simulando:
            // gravedad, contacto y frenado no deben depender de una referencia visual.
            Vector3 move = ResolveMove(axes, viewCamera != null ? viewCamera.transform.forward : Vector3.forward);
            // Preservar la ultima orientacion si el cursor no corta el plano.
            Vector3 aimFacing = motor.CurrentFacing;

            bool pointerInside = false;
            if (viewCamera != null && TryReadPointer(out Vector2 pointer))
            {
                pointerInside = true;
                // Rueda hacia delante = acercar. Solo con el cursor dentro de la ventana de juego.
                float scroll = ReadScroll();
                if (followCamera != null && scroll != 0f)
                    followCamera.AddZoom(-scroll * zoomStep);
                if (followCamera != null && followCamera.LookAheadEnabled)
                    followCamera.SetLookAheadInput(Time.timeScale > 0f
                        ? PointerOffsetFromCenter(pointer, Screen.width, Screen.height)
                        : Vector2.zero);

                aimFacing = AimFacing(pointer, aimFacing);
            }
            // Con el cursor fuera de la ventana el encuadre vuelve a centrarse.
            if (!pointerInside && followCamera != null && followCamera.LookAheadEnabled)
                followCamera.SetLookAheadInput(Vector2.zero);

            ObserveCombat();
            CombatFacing = UsesCombatFacing(Time.time, lastCombatTime, lastActionTime);
            Vector3 facing = ChooseFacing(CombatFacing, move, aimFacing, motor.CurrentFacing,
                explorationTurnSpeed * Time.deltaTime);
            motor.Simulate(new PlayerMotionCommand(move, facing, ReadDashPressed()), Time.deltaTime);
        }

        /// <summary>
        /// Combate si <see cref="CombatState"/> estuvo activo hace menos de
        /// <see cref="CombatFacingAfterCombat"/> s o si hubo una accion propia (ataque,
        /// disparo, hechizo, defensa) hace menos de <see cref="CombatFacingAfterAction"/> s.
        /// </summary>
        public static bool UsesCombatFacing(float now, float lastCombatTime, float lastActionTime) =>
            now - lastCombatTime < CombatFacingAfterCombat || now - lastActionTime < CombatFacingAfterAction;

        /// <summary>
        /// Orientacion que se pide al motor. En combate, la del cursor. Explorando, gira hacia
        /// donde se camina como mucho <paramref name="maxTurnDegrees"/> por paso (giro suave);
        /// quieto, conserva la actual.
        /// </summary>
        public static Vector3 ChooseFacing(bool combat, Vector3 move, Vector3 aimFacing,
            Vector3 currentFacing, float maxTurnDegrees)
        {
            if (combat)
                return aimFacing;
            Vector3 planarMove = new Vector3(move.x, 0f, move.z);
            if (!(planarMove.sqrMagnitude > 0.0001f))
                return currentFacing;
            return TurnTowards(currentFacing, planarMove, maxTurnDegrees);
        }

        /// <summary>Gira en el plano hacia <paramref name="desired"/> sin pasar de los grados indicados.</summary>
        public static Vector3 TurnTowards(Vector3 current, Vector3 desired, float maxDegrees)
        {
            current.y = 0f;
            desired.y = 0f;
            if (!(desired.sqrMagnitude > 0.000001f) || float.IsInfinity(desired.sqrMagnitude))
                return current;
            Vector3 target = desired.normalized;
            if (!(current.sqrMagnitude > 0.000001f) || float.IsInfinity(current.sqrMagnitude) ||
                float.IsNaN(maxDegrees) || float.IsPositiveInfinity(maxDegrees))
                return target;
            Vector3 from = current.normalized;
            float angle = Vector3.SignedAngle(from, target, Vector3.up);
            float limit = Mathf.Max(0f, maxDegrees);
            if (Mathf.Abs(angle) <= limit)
                return target;
            return Quaternion.AngleAxis(Mathf.Sign(angle) * limit, Vector3.up) * from;
        }

        /// <summary>
        /// Accion de combate propia (espada, pulso, escarcha): pasa a mirar al cursor y orienta
        /// al instante la direccion logica, para que el golpe salga hacia el cursor aunque el
        /// personaje estuviera mirando hacia donde caminaba.
        /// </summary>
        public void NotifyCombatAction()
        {
            lastActionTime = Time.time;
            CombatFacing = true;
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (motor == null || viewCamera == null || !TryReadPointer(out Vector2 pointer))
                return;
            motor.SetFacing(AimFacing(pointer, motor.CurrentFacing));
        }

        /// <summary>Direccion hacia el punto del mundo bajo el cursor (con zona muerta).</summary>
        private Vector3 AimFacing(Vector2 pointer, Vector3 currentFacing)
        {
            if (shooter != null && shooter.TryGetAimPoint(pointer, out Vector3 aimPoint))
                return ResolveFacing(transform.position, aimPoint, currentFacing, aimDeadZone);

            Ray ray = viewCamera.ScreenPointToRay(pointer);
            Plane plane = new Plane(Vector3.up, transform.position);
            return plane.Raycast(ray, out float distance)
                ? ResolveFacing(transform.position, ray.GetPoint(distance), currentFacing, aimDeadZone)
                : currentFacing;
        }

        /// <summary>
        /// Combate y acciones que no avisan por si mismas: estado de combate, hechizos
        /// lanzados, disparos de ballesta (incluidos los guardados en el buffer) y defensa.
        /// Los componentes se buscan aqui porque la sesion los añade despues de Awake.
        /// </summary>
        private void ObserveCombat()
        {
            float now = Time.time;
            if (combatState == null)
                combatState = GetComponent<CombatState>();
            if (combatState != null && combatState.InCombat)
                lastCombatTime = now;

            if (spellbook == null)
            {
                spellbook = GetComponent<PlayerSpellbook>();
                if (spellbook != null)
                    observedSpells = spellbook.SpellsCast;
            }
            if (spellbook != null && spellbook.SpellsCast != observedSpells)
            {
                observedSpells = spellbook.SpellsCast;
                lastActionTime = now;
            }

            if (shooter != null)
            {
                // Peticion aceptada = disparo inmediato o guardado en el buffer.
                int shots = shooter.ShotsFired + shooter.BufferedShots;
                if (observedShots >= 0 && shots > observedShots)
                    lastActionTime = now;
                observedShots = shots;
            }

            if (defense == null)
                defense = GetComponent<CombatDefense>();
            if (defense != null &&
                (defense.Guard != CombatDefense.GuardMode.None || defense.ParryWindowActive))
                lastActionTime = now;
        }

        /// <summary>
        /// Boton central arrastrando o teclas de giro (reasignables): gira la camara
        /// alrededor del personaje. La tecla de reinicio vuelve al angulo por defecto.
        /// </summary>
        private void RotateCamera()
        {
            if (ReadCameraResetPressed())
            {
                followCamera.ResetYaw();
                return;
            }

            float dragPixels = ReadCameraDragPixels();
            float keys = ReadCameraRotateKeys();
            if (dragPixels == 0f && keys == 0f)
                return;
            float sensitivity = GameSettings.CameraRotationSensitivity;
            float degrees = dragPixels * dragDegreesPerPixel * sensitivity +
                            keys * keyRotationSpeed * sensitivity * Time.deltaTime;
            if (degrees != 0f && !float.IsNaN(degrees) && !float.IsInfinity(degrees))
                followCamera.AddYaw(degrees);
        }

        /// <summary>Pixeles horizontales arrastrados con el boton central en este frame.</summary>
        private static float ReadCameraDragPixels()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.middleButton.isPressed)
                return 0f;
            float pixels = mouse.delta.ReadValue().x;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetMouseButton(2))
                return 0f;
            float pixels = Input.GetAxisRaw("Mouse X") * 10f;
#else
            float pixels = 0f;
#endif
            return float.IsNaN(pixels) || float.IsInfinity(pixels) ? 0f : Mathf.Clamp(pixels, -400f, 400f);
        }

        /// <summary>-1 girar a la izquierda, +1 a la derecha, 0 ninguna o ambas.</summary>
        private static float ReadCameraRotateKeys()
        {
#if ENABLE_INPUT_SYSTEM
            return (GameInput.Held(GameAction.CameraRotateRight) ? 1f : 0f) -
                   (GameInput.Held(GameAction.CameraRotateLeft) ? 1f : 0f);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return (Input.GetKey(KeyCode.Delete) ? 1f : 0f) - (Input.GetKey(KeyCode.Insert) ? 1f : 0f);
#else
            return 0f;
#endif
        }

        private static bool ReadCameraResetPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.CameraReset);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Home);
#else
            return false;
#endif
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
