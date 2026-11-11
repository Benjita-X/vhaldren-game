using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Traduce el estado del motor a parametros del Animator: hacia donde se mueven las
    /// piernas respecto a donde mira el personaje, la esquiva y el disparo.
    /// Solo lee; nunca cambia el movimiento. Un avatar remoto podria usarlo igual.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        public static readonly int MoveXId = Animator.StringToHash("MoveX");
        public static readonly int MoveYId = Animator.StringToHash("MoveY");
        public static readonly int DodgeId = Animator.StringToHash("Dodge");
        public static readonly int DodgeDirId = Animator.StringToHash("DodgeDir");
        public static readonly int ShootId = Animator.StringToHash("Shoot");

        /// <summary>Direcciones de esquiva tal como las espera el Animator.</summary>
        public enum DodgeDirection { Forward = 0, Backward = 1, Left = 2, Right = 3 }

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private LocalPlayerShooter shooter;
        [Tooltip("Velocidad que corresponde a la animacion de correr completa.")]
        [SerializeField, Min(0.1f)] private float runSpeed = 6f;
        [Tooltip("Suavizado de la mezcla de piernas, en segundos.")]
        [SerializeField, Min(0f)] private float dampTime = 0.08f;

        private bool wasDashing;
        private int lastShots;

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();
            if (shooter == null)
                shooter = GetComponent<LocalPlayerShooter>();
            if (shooter != null)
                lastShots = shooter.ShotsFired;
        }

        private void LateUpdate()
        {
            if (motor == null || animator == null || animator.runtimeAnimatorController == null)
                return;

            Vector3 forward = visualRoot != null ? visualRoot.forward : transform.forward;
            Vector2 local = LocalMoveDirection(motor.ActualVelocity, forward, runSpeed);
            float damp = float.IsNaN(dampTime) ? 0f : Mathf.Max(0f, dampTime);
            animator.SetFloat(MoveXId, local.x, damp, Time.deltaTime);
            animator.SetFloat(MoveYId, local.y, damp, Time.deltaTime);

            bool dashing = motor.IsDashing || motor.DashMovedThisStep;
            if (dashing && !wasDashing)
            {
                Vector2 dodge = LocalMoveDirection(motor.ActualVelocity, forward, 0.001f);
                animator.SetInteger(DodgeDirId, (int)ClassifyDodge(dodge));
                animator.SetTrigger(DodgeId);
            }
            wasDashing = dashing;

            if (shooter != null && shooter.ShotsFired != lastShots)
            {
                lastShots = shooter.ShotsFired;
                animator.SetTrigger(ShootId);
            }
        }

        /// <summary>
        /// Velocidad del mundo expresada respecto a donde mira el personaje:
        /// x positivo = hacia su derecha, y positivo = hacia delante. Longitud maxima 1.
        /// </summary>
        public static Vector2 LocalMoveDirection(Vector3 velocityWorld, Vector3 facingWorld, float referenceSpeed)
        {
            velocityWorld.y = 0f;
            facingWorld.y = 0f;
            if (!IsFinite(velocityWorld) || !IsFinite(facingWorld) || float.IsNaN(referenceSpeed) ||
                referenceSpeed <= 0f || facingWorld.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            Vector3 forward = facingWorld.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 local = new Vector2(Vector3.Dot(velocityWorld, right), Vector3.Dot(velocityWorld, forward))
                            / referenceSpeed;
            return local.sqrMagnitude < 0.0004f ? Vector2.zero : Vector2.ClampMagnitude(local, 1f);
        }

        /// <summary>Direccion dominante de una esquiva expresada en espacio local.</summary>
        public static DodgeDirection ClassifyDodge(Vector2 local)
        {
            if (Mathf.Abs(local.x) > Mathf.Abs(local.y))
                return local.x < 0f ? DodgeDirection.Left : DodgeDirection.Right;
            return local.y < 0f ? DodgeDirection.Backward : DodgeDirection.Forward;
        }

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
}
