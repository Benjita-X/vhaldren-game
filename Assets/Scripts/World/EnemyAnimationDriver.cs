using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Anima el modelo KayKit de un enemigo: velocidad real para reposo/caminar,
    /// ataque al empezar el aviso del golpe, reaccion al recibir daño y caida al morir
    /// (estado "Muerte" con el clip Death_A de cada modelo).
    /// Solo presentacion: no cambia tiempos, daño ni colisiones.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationDriver : MonoBehaviour
    {
        public const string DeathTrigger = "Death";
        public const string DeathState = "Muerte";
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DeathId = Animator.StringToHash(DeathTrigger);
        private static readonly int DeathStateId = Animator.StringToHash(DeathState);

        private Animator animator;
        private PrototypeEnemy enemy;
        private CombatHealth health;
        private Vector3 lastPosition;
        private float smoothedSpeed;
        private bool wasTelegraphing;
        private float nextHitReaction;
        private bool deathChecked;
        private bool hasDeathState;
        private bool dead;

        public int AttacksPlayed { get; private set; }
        public int DeathsPlayed { get; private set; }
        public bool IsDead => dead;
        public Animator Animator => animator;

        /// <summary>
        /// El controlador tiene el disparador "Death". Se consulta cuando el Animator ya
        /// esta activo (en Awake del enemigo puede no estar inicializado todavia).
        /// </summary>
        public bool HasDeathAnimation
        {
            get
            {
                if (!deathChecked && animator != null && animator.isActiveAndEnabled)
                {
                    deathChecked = true;
                    hasDeathState = HasTrigger(animator, DeathTrigger);
                }
                return hasDeathState;
            }
        }

        /// <summary>True si el Animator esta en (o entrando a) el estado de muerte.</summary>
        public bool InDeathState
        {
            get
            {
                if (animator == null || !animator.isActiveAndEnabled)
                    return false;
                if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == DeathStateId)
                    return true;
                return animator.IsInTransition(0) &&
                       animator.GetNextAnimatorStateInfo(0).shortNameHash == DeathStateId;
            }
        }

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
            driver.deathChecked = false;
            // Fuera de camara no se escriben huesos (la maquina de estados sigue): ahorra CPU
            // en el mapa grande sin cambiar tiempos ni la muerte.
            found.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            return driver;
        }

        private static bool HasTrigger(Animator target, string name)
        {
            // Animator.parameters crea un arreglo: se consulta una sola vez por enemigo.
            AnimatorControllerParameter[] parameters = target.parameters;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == name)
                    return true;
            return false;
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
            if (dead || animator == null || !animator.isActiveAndEnabled || Time.time < nextHitReaction ||
                (enemy != null && enemy.IsAttackTelegraphed) || (health != null && health.IsDefeated))
                return;
            nextHitReaction = Time.time + 0.6f;
            animator.SetTrigger(HitId);
        }

        /// <summary>
        /// Reproduce la caida. Devuelve false si el modelo no tiene estado de muerte
        /// (entonces el enemigo usa una caida procedural).
        /// </summary>
        public bool PlayDeath()
        {
            if (animator == null || !animator.isActiveAndEnabled || !HasDeathAnimation)
                return false;
            dead = true;
            // La muerte gana a cualquier golpe o ataque pedido en el mismo fotograma.
            animator.ResetTrigger(HitId);
            animator.ResetTrigger(AttackId);
            animator.SetFloat(SpeedId, 0f);
            animator.SetTrigger(DeathId);
            DeathsPlayed++;
            return true;
        }

        /// <summary>True mientras el enemigo duerme lejos del jugador (Animator apagado).</summary>
        public bool Sleeping { get; private set; }

        /// <summary>
        /// Lejos del jugador el enemigo duerme: el Animator se apaga y no gasta CPU. Al
        /// despertar se reanuda sin confundir el salto de posicion con una carrera.
        /// </summary>
        public void SetSleeping(bool sleeping)
        {
            if (Sleeping == sleeping)
                return;
            Sleeping = sleeping;
            lastPosition = transform.position;
            smoothedSpeed = 0f;
            if (animator != null && !dead)
                animator.enabled = !sleeping;
        }

        /// <summary>Al reaparecer: vuelve al reposo como si nada hubiera pasado.</summary>
        public void ResetAfterRespawn()
        {
            dead = false;
            smoothedSpeed = 0f;
            wasTelegraphing = false;
            nextHitReaction = 0f;
            lastPosition = transform.position;
            if (animator == null)
                return;
            if (animator.isActiveAndEnabled)
            {
                animator.ResetTrigger(DeathId);
                animator.ResetTrigger(HitId);
                animator.ResetTrigger(AttackId);
                animator.Rebind();
                animator.Update(0f);
            }
        }

        private void LateUpdate()
        {
            if (dead || animator == null || !animator.isActiveAndEnabled)
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
