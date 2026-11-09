using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Entrada temporal de teclado y raton para la primera prueba local.
    /// Requiere Active Input Handling = Both o Input Manager (Old).
    /// Al introducir LAN, habilitar este componente SOLO en el jugador local.
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
        }

        private void Update()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            if (viewCamera == null)
                return;

            float horizontal = (Input.GetKey(KeyCode.D) ? 1f : 0f)
                             - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float vertical = (Input.GetKey(KeyCode.W) ? 1f : 0f)
                           - (Input.GetKey(KeyCode.S) ? 1f : 0f);

            Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 worldMovement = forward * vertical + right * horizontal;

            // Girar hacia el cursor usando un plano a la altura del personaje.
            Vector3 worldFacing = transform.forward;
            Ray cursorRay = viewCamera.ScreenPointToRay(Input.mousePosition);
            Plane aimPlane = new Plane(Vector3.up, transform.position);

            if (aimPlane.Raycast(cursorRay, out float distance))
                worldFacing = cursorRay.GetPoint(distance) - transform.position;

            motor.Simulate(worldMovement, worldFacing, Time.deltaTime);
        }
    }
}
