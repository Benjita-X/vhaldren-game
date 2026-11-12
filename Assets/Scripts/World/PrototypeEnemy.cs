using System;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Enemigo de prototipo local: persigue al jugador, golpea en corto alcance
    /// y reaparece. No usa NavMesh ni constituye IA autoritativa de red.
    /// Su CharacterController no comparte locomotor con el jugador.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatHealth))]
    public sealed class PrototypeEnemy : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private bool isBoss;
        [SerializeField, Min(0f)] private float aggroRange = 15f;
        [SerializeField, Min(0.1f)] private float leashRange = 28f;
        [SerializeField, Min(0f)] private float moveSpeed = 3.2f;
        [SerializeField, Min(0.5f)] private float attackRange = 1.65f;
        [SerializeField, Min(0f)] private float attackDamage = 10f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.3f;
        [SerializeField, Min(0.1f)] private float attackWindup = 0.48f;
        [SerializeField, Min(0f)] private float hitReactionSeconds = 0.18f;
        [SerializeField, Min(1f)] private float respawnSeconds = 18f;
        [SerializeField] private GameObject modelRoot;

        private CharacterController controller;
        private CombatHealth health;
        private CombatHealth targetHealth;
        private PlayerMotor targetMotor;
        private EnemyAttackTelegraph attackWarning;
        private CombatDamageKind activeAttackKind = CombatDamageKind.Melee;
        private int attackSequence;
        private Vector3 spawnPosition;
        private float fallingSpeed;
        private float nextAttackTime;
        private float respawnAt = float.PositiveInfinity;
        private float provokedUntil;
        private bool defeated;
        private bool returningHome;
        private bool windingUp;
        private float attackReleaseAt;
        private float staggerUntil;

        public bool IsBoss => isBoss;
        public bool IsDefeated => defeated;
        public bool IsAttackTelegraphed => windingUp;
        public event Action<PrototypeEnemy> Defeated;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<CombatHealth>();
            // La escena 008 ya esta versionada sin esta nueva interfaz.
            // Autoagregarla permite ver PV y dano sin regenerar ni borrar el mapa.
            CombatDamageFeedback feedback = GetComponent<CombatDamageFeedback>();
            if (feedback == null)
                feedback = gameObject.AddComponent<CombatDamageFeedback>();
            feedback.ConfigureForEnemy(isBoss);
            CombatDamageReceiver damageReceiver = GetComponent<CombatDamageReceiver>();
            if (damageReceiver == null)
                damageReceiver = gameObject.AddComponent<CombatDamageReceiver>();
            damageReceiver.ConfigureResistances(isBoss ? 0.12f : 0f,
                isBoss ? 0.16f : 0f, isBoss ? 0.26f : 0f);
            if (GetComponent<EnemyVisualPrototype>() == null)
                gameObject.AddComponent<EnemyVisualPrototype>();
            // Se agrega tambien a escenas 008 ya versionadas sin recompilar la escena.
            attackWarning = GetComponent<EnemyAttackTelegraph>();
            if (attackWarning == null)
                attackWarning = gameObject.AddComponent<EnemyAttackTelegraph>();
            EnemyHitReaction reaction = GetComponent<EnemyHitReaction>();
            if (reaction == null)
                reaction = gameObject.AddComponent<EnemyHitReaction>();
            reaction.BindVisual(modelRoot != null ? modelRoot.transform :
                transform.Find("Visual enemigo"));
            spawnPosition = transform.position;
            if (target == null)
            {
                GameObject player = GameObject.Find("PlayerRoot");
                if (player != null)
                    target = player.transform;
            }
            if (target != null)
            {
                targetHealth = target.GetComponent<CombatHealth>();
                targetMotor = target.GetComponent<PlayerMotor>();
            }
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            health.Defeated += HandleDefeated;
            health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            CancelAttack();
            if (health != null)
            {
                health.Defeated -= HandleDefeated;
                health.Damaged -= HandleDamaged;
            }
        }

        private void HandleDamaged(float damage)
        {
            provokedUntil = Time.time + 12f;
            returningHome = false;
            // Los guardianes menores dan una oportunidad real de contraatacar.
            // El jefe no queda bloqueado permanentemente por ataques de disparo rapido.
            if (!isBoss)
                InterruptAttack(hitReactionSeconds);
        }

        private void HandleDefeated()
        {
            if (defeated)
                return;
            defeated = true;
            CancelAttack();
            respawnAt = Time.time + Mathf.Max(1f, respawnSeconds);
            if (controller != null)
                controller.enabled = false;
            returningHome = false;
            if (modelRoot != null)
                modelRoot.SetActive(false);
            Defeated?.Invoke(this);
        }

        private void Update()
        {
            if (defeated)
            {
                if (Time.time >= respawnAt)
                    Respawn();
                return;
            }

            if (controller == null || !controller.enabled)
                return;

            // Durante la preparación, el enemigo permanece quieto. Si el jugador
            // se aleja, se esconde tras una pared o muere, el ataque se cancela.
            // En el instante del golpe se valida de nuevo todo: no hay daño gratis.
            bool holdPosition = windingUp || Time.time < staggerUntil;
            if (windingUp && !CanReachTarget())
                CancelAttack();

            // El golpe se resuelve en LateUpdate, después de LocalPlayerInput.Update.
            // Así una esquiva pulsada justo en este frame no depende del orden
            // arbitrario de Update entre enemigos y jugador.
            Vector3 move = Vector3.zero;
            if (holdPosition)
            {
                // Gravedad normal incluso durante la carga o un stagger.
            }
            else if (target != null && targetHealth != null && !targetHealth.IsDefeated)
            {
                Vector3 planar = target.position - transform.position;
                planar.y = 0f;
                float distance = planar.magnitude;
                Vector3 fromHome = transform.position - spawnPosition;
                fromHome.y = 0f;

                if (fromHome.sqrMagnitude > leashRange * leashRange)
                    returningHome = true;

                // Una vez fuera de su territorio, volver realmente al origen.
                // No alternar perseguir/volver cada frame al cruzar el limite.
                if (returningHome)
                {
                    if (fromHome.sqrMagnitude <= 0.75f * 0.75f)
                    {
                        returningHome = false;
                        provokedUntil = 0f;
                    }
                    else
                        move = -fromHome.normalized;
                }
                else if (distance <= aggroRange || Time.time < provokedUntil)
                {
                    if (distance > attackRange && distance > 0.001f)
                        move = planar / distance;
                    else if (Time.time >= nextAttackTime && HasLineOfSight())
                    {
                        BeginAttack();
                    }
                }
                else
                {
                    // Si pierde el aggro, vuelve a su zona en vez de quedarse
                    // parado a mitad del camino para siempre.
                    Vector3 home = spawnPosition - transform.position;
                    home.y = 0f;
                    if (home.sqrMagnitude > 1.5f * 1.5f)
                        move = home.normalized;
                }
            }
            else
            {
                // Si muere el jugador, los enemigos se retiran hasta su punto de origen.
                Vector3 home = spawnPosition - transform.position;
                home.y = 0f;
                if (home.sqrMagnitude > 1.5f * 1.5f)
                    move = home.normalized;
            }

            if (move.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(move, Vector3.up),
                    540f * Time.deltaTime);
            }

            fallingSpeed = controller.isGrounded ? -2f :
                Mathf.Max(-40f, fallingSpeed - 25f * Time.deltaTime);
            Vector3 velocity = move * Mathf.Max(0f, moveSpeed) +
                Vector3.up * fallingSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!windingUp || defeated || Time.time < attackReleaseAt)
                return;

            // Revalidar tras mover al jugador en Update: posicion, vida,
            // linea de vision y radio deben seguir siendo correctos.
            if (!CanReachTarget())
            {
                CancelAttack();
                return;
            }

            windingUp = false;
            if (attackWarning != null)
                attackWarning.Strike();

            // I-frames del dash. Nunca registrar un golpe si el jugador
            // esquivo correctamente en este mismo fotograma.
            CombatDamageReceiver defender = target.GetComponent<CombatDamageReceiver>();
            if (defender != null)
            {
                // El receptor central resuelve dash/parry/bloqueos y puede
                // mostrar ESQUIVA sin fingir que se descontaron PV.
                CombatDamageResult response = defender.Receive(
                    Mathf.Max(0f, attackDamage), activeAttackKind, transform.position);
                if (response.Parried)
                    InterruptAttack(0.70f);
            }
            else if (targetMotor == null ||
                (!targetMotor.IsDashing && !targetMotor.DashMovedThisStep))
            {
                // Compatibilidad con las salas de prueba antiguas.
                targetHealth.ApplyDamage(Mathf.Max(0f, attackDamage));
            }
        }

        private bool CanReachTarget()
        {
            if (target == null || targetHealth == null || targetHealth.IsDefeated)
                return false;
            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            Vector3 fromHomeNow = transform.position - spawnPosition;
            fromHomeNow.y = 0f;
            return delta.sqrMagnitude <= attackRange * attackRange &&
                fromHomeNow.sqrMagnitude <= leashRange * leashRange &&
                HasLineOfSight();
        }

        /// <summary>
        /// Interrumpe un ataque anunciado sin desplazar al enemigo. Util para
        /// habilidades futuras que apliquen aturdimiento o rompimiento de postura.
        /// </summary>
        public void InterruptAttack(float recoverySeconds = 0.18f)
        {
            CancelAttack();
            if (float.IsNaN(recoverySeconds) || float.IsInfinity(recoverySeconds))
                recoverySeconds = 0f;
            float safe = Mathf.Clamp(recoverySeconds, 0f, 3f);
            staggerUntil = Mathf.Max(staggerUntil, Time.time + safe);
            nextAttackTime = Mathf.Max(nextAttackTime, staggerUntil + 0.08f);
        }

        private void BeginAttack()
        {
            if (windingUp || defeated)
                return;
            float safeWindup = float.IsNaN(attackWindup) || float.IsInfinity(attackWindup)
                ? 0.48f : Mathf.Max(0.1f, attackWindup);
            if (isBoss)
                safeWindup = Mathf.Max(0.75f, safeWindup);

            windingUp = true;
            // El jefe alterna espada y ataque runico (1 de cada 3 golpes).
            // Escudo = defensa fisica; barrera C = defensa contra magia.
            attackSequence++;
            activeAttackKind = isBoss && attackSequence % 3 == 0
                ? CombatDamageKind.Magic : CombatDamageKind.Melee;
            attackReleaseAt = Time.time + safeWindup;
            nextAttackTime = Time.time + Mathf.Max(Mathf.Max(0.1f, attackCooldown),
                safeWindup + 0.2f);
            if (attackWarning != null)
                attackWarning.Begin(attackRange, safeWindup, isBoss,
                    activeAttackKind == CombatDamageKind.Magic);
        }

        private void CancelAttack()
        {
            windingUp = false;
            if (attackWarning != null)
                attackWarning.Cancel();
        }

        private static readonly RaycastHit[] SightHits = new RaycastHit[16];

        /// <summary>
        /// Hay vision si ningun obstaculo se interpone entre el centro del enemigo y el
        /// centro del objetivo. Se ignoran los colliders propios y los del objetivo, asi
        /// que no depende de acertar a una altura concreta de su capsula.
        /// </summary>
        private bool HasLineOfSight()
        {
            if (target == null || targetHealth == null)
                return false;
            Vector3 origin = controller != null && controller.enabled
                ? controller.bounds.center
                : transform.position + Vector3.up * 0.9f;
            Collider targetCollider = target.GetComponent<Collider>();
            Vector3 destination = targetCollider != null && targetCollider.enabled
                ? targetCollider.bounds.center
                : target.position;
            Vector3 difference = destination - origin;
            float distance = difference.magnitude;
            if (distance <= 0.001f)
                return true;
            Vector3 direction = difference / distance;
            int count = Physics.RaycastNonAlloc(origin, direction, SightHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            // RaycastNonAlloc puede dejar fuera una pared si el buffer se lleno.
            // No considerar despejada una linea de vision incompleta.
            if (count == SightHits.Length)
            {
                RaycastHit[] allHits = Physics.RaycastAll(origin, direction, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < allHits.Length; i++)
                {
                    Collider body = allHits[i].collider;
                    if (body == null)
                        continue;
                    Transform hit = body.transform;
                    if (!hit.IsChildOf(transform) && !hit.IsChildOf(target))
                        return false;
                }
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                Collider body = SightHits[i].collider;
                if (body == null)
                    continue;
                Transform hit = body.transform;
                if (!hit.IsChildOf(transform) && !hit.IsChildOf(target))
                    return false;
            }
            return true;
        }

        private void Respawn()
        {
            // El controlador desactivado permite restablecer la posicion limpiamente.
            transform.position = spawnPosition;
            fallingSpeed = 0f;
            nextAttackTime = Time.time + 1f;
            staggerUntil = 0f;
            CancelAttack();
            provokedUntil = 0f;
            returningHome = false;
            health.ResetToFull();
            if (modelRoot != null)
                modelRoot.SetActive(true);
            if (controller != null)
                controller.enabled = true;
            defeated = false;
            respawnAt = float.PositiveInfinity;
        }
    }
}
