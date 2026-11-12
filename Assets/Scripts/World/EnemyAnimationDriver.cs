using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Anima el modelo KayKit de un enemigo: velocidad real para reposo/caminar,
    /// ataque al empezar el aviso del golpe y reaccion al recibir daño.
    /// Solo presentacion: no cambia tiempos, daño ni colisiones.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int HitId = Animator.StringToHash("Hit");

        private Animator animator;
        private PrototypeEnemy enemy;
        private CombatHealth health;
        private Vector3 lastPosition;
        private float smoothedSpeed;
        private bool wasTelegraphing;
        private float nextHitReaction;

        public int AttacksPlayed { get; private set; }

        public static EnemyAnimationDriver Attach(PrototypeEnemy owner, Transform modelRoot)
        {
            if (owner == null || modelRoot == null)
                return null;
            Animator found = modelRoot.GetComponentInChildren<Animator>(true);
            if (found == null || found.runtimeAnimatorController == null)
                return null;
            EnemyAnimationDriver driver = owner.GetComponent<EnemyAnimationDriver>();
            if (driver == null)
                driver = owner.gameObject.AddComponent<EnemyAnimationDriver>();
            driver.animator = found;
            return driver;
        }

        private void Awake()
        {
            enemy = GetComponent<PrototypeEnemy>();
            health = GetComponent<CombatHealth>();
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            if (health != null)
                health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Damaged -= OnDamaged;
        }

        private void OnDamaged(float amount)
        {
            if (animator == null || !animator.isActiveAndEnabled || Time.time < nextHitReaction ||
                (enemy != null && enemy.IsAttackTelegraphed))
                return;
            nextHitReaction = Time.time + 0.6f;
            animator.SetTrigger(HitId);
        }

        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled)
                return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            // Teletransportes (reaparicion) no deben verse como un sprint.
            float speed = delta.magnitude / dt;
            if (speed > 20f)
                speed = 0f;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-12f * dt));
            animator.SetFloat(SpeedId, smoothedSpeed);

            bool telegraphing = enemy != null && enemy.IsAttackTelegraphed;
            if (telegraphing && !wasTelegraphing)
            {
                animator.ResetTrigger(HitId);
                animator.SetTrigger(AttackId);
                AttacksPlayed++;
            }
            wasTelegraphing = telegraphing;
        }
    }
}
