using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// El movimiento tambien es game feel: pasos con polvo y sonido mientras caminas,
    /// y una nube con soplido al esquivar. Solo lee el motor; no cambia el movimiento.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMovementFeedback : MonoBehaviour
    {
        public const float StepDistance = 1.7f;

        private PlayerMotor motor;
        private CharacterController body;
        private Vector3 lastPosition;
        private float travelled;
        private bool wasDashing;

        public int Steps { get; private set; }
        public int Dashes { get; private set; }

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            body = GetComponent<CharacterController>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            Tick(delta, motor != null && motor.IsDashing, motor == null || motor.IsGrounded);
        }

        /// <summary>Avanza el contador de pasos. Separado de Update para poder probarlo.</summary>
        public void Tick(Vector3 planarDelta, bool dashing, bool grounded)
        {
            Vector3 feet = body != null ? new Vector3(transform.position.x, body.bounds.min.y, transform.position.z)
                : transform.position;
            if (dashing && !wasDashing)
            {
                Dashes++;
                Vector3 back = planarDelta.sqrMagnitude > 0.0001f ? -planarDelta.normalized : -transform.forward;
                GameFeel.Dust(feet, back + Vector3.up * 0.3f, 14, 3f);
                if (GameFeel.Enabled)
                    GameAudio.Play(GameSound.Whoosh, feet, 0.7f);
            }
            wasDashing = dashing;

            float step = planarDelta.magnitude;
            if (!grounded || dashing || step > 1.5f)
                return;   // teletransportes o reaparicion no son pasos
            travelled += step;
            if (travelled < StepDistance)
                return;
            travelled = 0f;
            Steps++;
            GameFeel.Dust(feet, Vector3.up, 3, 0.8f);
            if (GameFeel.Enabled)
                GameAudio.Play(GameSound.Footstep, feet, 0.35f, 0.15f);
        }
    }
}
