using UnityEngine;
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
        private PlayerMotor motor;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
#if !ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogError(
                "[Vhaldren] No hay sistema de entrada habilitado. Configura Active Input Handling en Player Settings.",
                this);
#endif
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

            if (viewCamera != null && TryReadPointer(out Vector2 pointer))
            {
                Ray ray = viewCamera.ScreenPointToRay(pointer);
                Plane plane = new Plane(Vector3.up, transform.position);
                if (plane.Raycast(ray, out float distance))
                {
                    Vector3 candidate = ray.GetPoint(distance) - transform.position;
                    if (candidate.sqrMagnitude > 0.0001f)
                        facing = candidate;
                }
            }

            motor.Simulate(new PlayerMotionCommand(move, facing), Time.deltaTime);
        }

        private static Vector2 ReadMovement()
        {
            Vector2 axes = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                axes.x = (keyboard.dKey.isPressed ? 1f : 0f)
                       - (keyboard.aKey.isPressed ? 1f : 0f);
                axes.y = (keyboard.wKey.isPressed ? 1f : 0f)
                       - (keyboard.sKey.isPressed ? 1f : 0f);
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            axes.x = (Input.GetKey(KeyCode.D) ? 1f : 0f)
                   - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            axes.y = (Input.GetKey(KeyCode.W) ? 1f : 0f)
                   - (Input.GetKey(KeyCode.S) ? 1f : 0f);
#endif
            return Vector2.ClampMagnitude(axes, 1f);
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
