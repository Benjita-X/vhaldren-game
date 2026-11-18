using UnityEngine;
using Vhaldren.Gameplay.Survival;

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
        public static readonly int HarvestId = Animator.StringToHash("Harvest");
        public static readonly int HarvestStyleId = Animator.StringToHash("HarvestStyle");
        public static readonly int SwordActionId = Animator.StringToHash("SwordAction");
        public static readonly int SpellActionId = Animator.StringToHash("SpellAction");
        public static readonly int FrostActionId = Animator.StringToHash("FrostAction");
        public static readonly int ShieldActionId = Animator.StringToHash("ShieldAction");
        public static readonly int WardActionId = Animator.StringToHash("WardAction");
        public static readonly int ParryActionId = Animator.StringToHash("ParryAction");

        public enum CombatAction { Sword, Spell, Frost, Shield, Ward, Parry }

        /// <summary>Direcciones de esquiva tal como las espera el Animator.</summary>
        public enum DodgeDirection { Forward = 0, Backward = 1, Left = 2, Right = 3 }

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private LocalPlayerShooter shooter;
        [Tooltip("Velocidad que corresponde a la animacion de correr completa.")]
        [SerializeField, Min(0.1f)] private float runSpeed = 6f;
        [Tooltip("Suavizado de la mezcla de piernas, en segundos.")]
        [SerializeField, Min(0f)] private float dampTime = 0.12f;

        private bool wasDashing;
        private int lastShots;

        /// <summary>
        /// Jinete sentado en una montura: las piernas quedan en reposo (sin correr ni
        /// esquivar) aunque el cuerpo se desplace deprisa. Lo activa el control de monturas.
        /// </summary>
        public bool Seated { get; set; }

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
            Vector2 local = Seated ? Vector2.zero : LocalMoveDirection(motor.ActualVelocity, forward, runSpeed);
            float damp = float.IsNaN(dampTime) ? 0f : Mathf.Max(0f, dampTime);
            animator.SetFloat(MoveXId, local.x, damp, Time.deltaTime);
            animator.SetFloat(MoveYId, local.y, damp, Time.deltaTime);

            bool dashing = !Seated && (motor.IsDashing || motor.DashMovedThisStep);
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
        /// La animacion de recolectar no modifica PlayerMotor ni pierde entradas.
        /// El controlador KayKit actualizado incluye PickUp y Chop. En escenas
        /// con el controlador antiguo omite el trigger sin lanzar warnings.
        /// </summary>
        public bool TriggerHarvest(ResourceKind resource)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            bool hasHarvest = false;
            bool hasStyle = false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == HarvestId &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                    hasHarvest = true;
                if (parameter.nameHash == HarvestStyleId &&
                    parameter.type == AnimatorControllerParameterType.Int)
                    hasStyle = true;
            }
            if (!hasHarvest || !hasStyle)
                return false;

            int style = resource == ResourceKind.Stone || resource == ResourceKind.Ore
                ? 1 : 0;
            animator.SetInteger(HarvestStyleId, style);
            animator.SetTrigger(HarvestId);
            return true;
        }

        /// <summary>
        /// Lanza el clip de combate que corresponde a la accion aceptada.
        /// Si el controlador es antiguo, omite el trigger sin bloquear controles.
        /// Actualizarlo desde Vhaldren/Personaje/Preparar personaje KayKit.
        /// </summary>
        public bool PlayCombatAction(CombatAction action)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            int trigger;
            switch (action)
            {
                case CombatAction.Sword: trigger = SwordActionId; break;
                case CombatAction.Spell: trigger = SpellActionId; break;
                case CombatAction.Frost: trigger = FrostActionId; break;
                case CombatAction.Shield: trigger = ShieldActionId; break;
                case CombatAction.Ward: trigger = WardActionId; break;
                case CombatAction.Parry: trigger = ParryActionId; break;
                default: return false;
            }
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == trigger &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(trigger);
                    return true;
                }
            }
            return false;
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
