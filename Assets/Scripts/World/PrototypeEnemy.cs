using System;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

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
        [SerializeField, Min(1f)] private float respawnSeconds = 18f;
        [SerializeField] private GameObject modelRoot;

        private CharacterController controller;
        private CombatHealth health;
        private CombatHealth targetHealth;
        private Vector3 spawnPosition;
        private float fallingSpeed;
        private float nextAttackTime;
        private float respawnAt = float.PositiveInfinity;
        private float provokedUntil;
        private bool defeated;
        private bool returningHome;

        public bool IsBoss => isBoss;
        public bool IsDefeated => defeated;
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
            spawnPosition = transform.position;
            if (target == null)
            {
                GameObject player = GameObject.Find("PlayerRoot");
                if (player != null)
                    target = player.transform;
            }
            if (target != null)
                targetHealth = target.GetComponent<CombatHealth>();
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
        }

        private void HandleDefeated()
        {
            if (defeated)
                return;
            defeated = true;
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

            Vector3 move = Vector3.zero;
            if (target != null && targetHealth != null && !targetHealth.IsDefeated)
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
                        nextAttackTime = Time.time + Mathf.Max(0.1f, attackCooldown);
                        targetHealth.ApplyDamage(Mathf.Max(0f, attackDamage));
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
            int count = Physics.RaycastNonAlloc(origin, difference / distance, SightHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = SightHits[i].collider.transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(target))
                    continue;
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
