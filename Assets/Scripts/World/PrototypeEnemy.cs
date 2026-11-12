using System;
using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Enemigo local de prototipo: percibe, persigue o investiga, avisa antes de atacar,
    /// regresa a su territorio y reaparece. No usa NavMesh ni es autoritativo para red.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatHealth))]
    public sealed class PrototypeEnemy : MonoBehaviour
    {
        private enum BrainState
        {
            Idle,
            Pursuing,
            Investigating,
            AttackWindup,
            AttackRecovery,
            ReturningHome,
            LeashReturn,
            Defeated
        }

        private static readonly HashSet<PrototypeEnemy> ActiveEnemies =
            new HashSet<PrototypeEnemy>();
        private static readonly RaycastHit[] SightHits = new RaycastHit[16];
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorProperty = Shader.PropertyToID("_Color");

        [Header("Percepción")]
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float aggroRange = 15f;
        [SerializeField, Range(1f, 360f)] private float viewAngle = 140f;
        [SerializeField, Min(0f)] private float memorySeconds = 6f;
        [SerializeField, Min(0.1f)] private float investigationArrivalDistance = 1.2f;

        [Header("Movimiento y territorio")]
        [SerializeField] private bool isBoss;
        [SerializeField, Min(0.1f)] private float leashRange = 28f;
        [SerializeField, Min(0f)] private float moveSpeed = 3.2f;

        [Header("Ataque")]
        [SerializeField, Min(0.5f)] private float attackRange = 1.65f;
        [SerializeField, Min(0f)] private float attackDamage = 10f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.3f;
        [SerializeField, Min(0.05f)] private float attackWindupSeconds = 0.5f;
        [SerializeField, Min(0f)] private float attackRecoverySeconds = 0.45f;
        [SerializeField, Range(10f, 180f)] private float attackArcDegrees = 100f;
        [SerializeField] private Color attackTelegraphColor = new Color(1f, 0.42f, 0.06f, 1f);

        [Header("Reaparición")]
        [SerializeField, Min(1f)] private float respawnSeconds = 18f;
        [SerializeField] private GameObject modelRoot;

        private CharacterController controller;
        private CombatHealth health;
        private CombatHealth targetHealth;
        private Renderer modelRenderer;
        private MaterialPropertyBlock modelPropertyBlock;
        private Vector3 spawnPosition;
        private Vector3 lastKnownTargetPosition;
        private Vector3 attackDirection;
        private Color originalBaseColor;
        private Color originalLegacyColor;
        private float fallingSpeed;
        private float attackTimer;
        private float nextAttackTime;
        private float respawnAt = float.PositiveInfinity;
        private float memoryExpiresAt;
        private bool hasLastKnownTargetPosition;
        private Vector3 pendingAttackOrigin;
        private bool hasPendingAttackOrigin;
        private bool hasBaseColor;
        private bool hasLegacyColor;
        private BrainState state;

        public bool IsBoss => isBoss;
        public bool IsDefeated => state == BrainState.Defeated;
        public bool IsReturningHome =>
            state == BrainState.ReturningHome || state == BrainState.LeashReturn;
        public bool IsInvestigating => state == BrainState.Investigating;
        public bool IsTelegraphingAttack => state == BrainState.AttackWindup;
        public bool HasLastKnownTargetPosition => hasLastKnownTargetPosition;
        public Vector3 LastKnownTargetPosition => lastKnownTargetPosition;
        public bool IsStaggered => state == BrainState.AttackRecovery && Time.time < StaggeredUntil;
        public float StaggeredUntil { get; private set; }
        public event Action<PrototypeEnemy> Defeated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveEnemies.Clear();
        }

        /// <summary>
        /// Public hook for weapons, footsteps, spells or world interactions to alert
        /// nearby enemies without making them read the source transform through walls.
        /// </summary>
        public static void EmitNoise(Vector3 source, float radius)
        {
            EmitNoise(source, radius, 1f);
        }

        /// <summary>
        /// Ruido con oclusion simple: si hay geometria entre el enemigo y el origen,
        /// el radio efectivo se multiplica por <paramref name="occludedRadiusScale"/>.
        /// El enemigo solo recuerda el ORIGEN del ruido, nunca la posicion del jugador.
        /// </summary>
        public static void EmitNoise(Vector3 source, float radius, float occludedRadiusScale)
        {
            if (!IsFinite(source) || !IsFinite(radius) || radius <= 0f)
                return;

            float occludedScale = IsFinite(occludedRadiusScale)
                ? Mathf.Clamp01(occludedRadiusScale) : 1f;
            float radiusSquared = radius * radius;
            foreach (PrototypeEnemy enemy in ActiveEnemies)
            {
                if (enemy != null && enemy.isActiveAndEnabled)
                    enemy.HearNoise(source, radiusSquared, occludedScale);
            }
        }

        /// <summary>
        /// Indica desde donde llego el proximo dano (punto de lanzamiento de un proyectil,
        /// centro de una explosion...). Se consume en el siguiente evento Damaged para que
        /// el enemigo investigue ese punto en lugar de leer la posicion actual del jugador.
        /// </summary>
        public void ReceiveAttackOrigin(Vector3 origin)
        {
            if (!IsFinite(origin) || state == BrainState.Defeated)
                return;
            pendingAttackOrigin = origin;
            hasPendingAttackOrigin = true;
        }

        /// <summary>
        /// Aturde al enemigo durante unos segundos usando su propia maquina de estados.
        /// Cancela un ataque en preparacion. No mueve al enemigo. Devuelve true si
        /// interrumpio un windup.
        /// </summary>
        public bool Stagger(float seconds)
        {
            if (state == BrainState.Defeated || !IsFinite(seconds) || seconds <= 0f)
                return false;

            bool interrupted = state == BrainState.AttackWindup;
            SetAttackTelegraphVisual(false);
            if (state == BrainState.LeashReturn)
                return interrupted;
            state = BrainState.AttackRecovery;
            attackTimer = Mathf.Max(attackTimer, seconds);
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + seconds);
            StaggeredUntil = Time.time + seconds;
            return interrupted;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<CombatHealth>();
            spawnPosition = transform.position;
            if (target == null)
            {
                GameObject player = GameObject.Find("PlayerRoot");
                if (player != null)
                    target = player.transform;
            }
            CacheTargetHealth();
            CacheModelRenderer();
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            ActiveEnemies.Add(this);
            if (health != null)
            {
                health.Defeated += HandleDefeated;
                health.Damaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            ActiveEnemies.Remove(this);
            if (health != null)
            {
                health.Defeated -= HandleDefeated;
                health.Damaged -= HandleDamaged;
            }
            SetAttackTelegraphVisual(false);
        }

        private void OnValidate()
        {
            if (!IsFinite(aggroRange) || aggroRange < 0f)
                aggroRange = 0f;
            if (!IsFinite(viewAngle))
                viewAngle = 140f;
            viewAngle = Mathf.Clamp(viewAngle, 1f, 360f);
            if (!IsFinite(memorySeconds) || memorySeconds < 0f)
                memorySeconds = 0f;
            if (!IsFinite(investigationArrivalDistance) || investigationArrivalDistance < 0.1f)
                investigationArrivalDistance = 1.2f;
            if (!IsFinite(attackWindupSeconds) || attackWindupSeconds < 0.05f)
                attackWindupSeconds = 0.5f;
            if (!IsFinite(attackRecoverySeconds) || attackRecoverySeconds < 0f)
                attackRecoverySeconds = 0f;
        }

        private void CacheTargetHealth()
        {
            targetHealth = target != null ? target.GetComponent<CombatHealth>() : null;
        }

        private void CacheModelRenderer()
        {
            modelRenderer = modelRoot != null ? modelRoot.GetComponent<Renderer>() : null;
            if (modelRenderer == null)
                return;

            modelPropertyBlock = new MaterialPropertyBlock();
            Material sharedMaterial = modelRenderer.sharedMaterial;
            if (sharedMaterial == null)
                return;
            hasBaseColor = sharedMaterial.HasProperty(BaseColorProperty);
            hasLegacyColor = sharedMaterial.HasProperty(LegacyColorProperty);
            if (hasBaseColor)
                originalBaseColor = sharedMaterial.GetColor(BaseColorProperty);
            if (hasLegacyColor)
                originalLegacyColor = sharedMaterial.GetColor(LegacyColorProperty);
        }

        private void HandleDamaged(float damage)
        {
            if (state == BrainState.Defeated || target == null || targetHealth == null ||
                targetHealth.IsDefeated)
                return;

            // Con origen informado (hechizos), se investiga ese punto. Sin el
            // (disparo hitscan heredado) se mantiene el comportamiento anterior.
            RememberPosition(hasPendingAttackOrigin ? pendingAttackOrigin : target.position);
            hasPendingAttackOrigin = false;
            if (state != BrainState.AttackWindup && state != BrainState.AttackRecovery &&
                state != BrainState.LeashReturn)
                state = BrainState.Investigating;
        }

        private void HearNoise(Vector3 source, float radiusSquared, float occludedScale)
        {
            if (state == BrainState.Defeated || state == BrainState.LeashReturn ||
                targetHealth == null || targetHealth.IsDefeated)
                return;

            Vector3 toNoise = source - transform.position;
            toNoise.y = 0f;
            if (toNoise.sqrMagnitude > radiusSquared)
                return;
            if (occludedScale < 1f &&
                toNoise.sqrMagnitude > radiusSquared * occludedScale * occludedScale)
            {
                // Fuera del radio amortiguado: solo se oye si no hay paredes entre medias.
                Vector3 ear = GetVisionOrigin();
                Vector3 heard = new Vector3(source.x, Mathf.Max(source.y + 0.5f, 0.5f), source.z);
                Vector3 ray = heard - ear;
                float distance = ray.magnitude;
                if (distance > 0.001f && !HasClearLine(ear, ray / distance, distance))
                    return;
            }

            RememberPosition(source);
            if (state != BrainState.AttackWindup && state != BrainState.AttackRecovery)
                state = BrainState.Investigating;
        }

        private void HandleDefeated()
        {
            if (state == BrainState.Defeated)
                return;
            state = BrainState.Defeated;
            hasLastKnownTargetPosition = false;
            hasPendingAttackOrigin = false;
            SetAttackTelegraphVisual(false);
            respawnAt = Time.time + Mathf.Max(1f, SafeNonNegative(respawnSeconds));
            if (controller != null)
                controller.enabled = false;
            if (modelRoot != null)
                modelRoot.SetActive(false);
            Defeated?.Invoke(this);
        }

        private void Update()
        {
            if (state == BrainState.Defeated)
            {
                if (Time.time >= respawnAt)
                    Respawn();
                return;
            }

            if (controller == null || !controller.enabled)
                return;

            if (targetHealth == null && target != null)
                CacheTargetHealth();

            bool seesTarget = TrySeeTarget(out Vector3 visibleTargetPosition);
            if (seesTarget)
                RememberPosition(target.position);

            Vector3 move = Vector3.zero;
            Vector3 facing = Vector3.zero;
            Vector3 fromHome = transform.position - spawnPosition;
            fromHome.y = 0f;

            if (state != BrainState.LeashReturn &&
                fromHome.sqrMagnitude > SafeNonNegative(leashRange) * SafeNonNegative(leashRange))
            {
                state = BrainState.LeashReturn;
                SetAttackTelegraphVisual(false);
            }

            if (state == BrainState.LeashReturn)
            {
                if (fromHome.sqrMagnitude <= 0.75f * 0.75f)
                {
                    ClearTargetMemory();
                    state = BrainState.Idle;
                }
                else
                {
                    move = -fromHome.normalized;
                    facing = move;
                }
            }
            else if (state == BrainState.AttackWindup)
            {
                if (!seesTarget || !IsWithinAttackRange(visibleTargetPosition))
                {
                    SetAttackTelegraphVisual(false);
                    state = seesTarget ? BrainState.Pursuing :
                        hasLastKnownTargetPosition ? BrainState.Investigating : BrainState.Idle;
                }
                else
                {
                    // El enemigo compromete su dirección al empezar el aviso. No puede
                    // corregir el golpe siguiendo una esquiva lateral durante el windup.
                    facing = attackDirection;
                    attackTimer -= Time.deltaTime;
                    if (attackTimer <= 0f)
                        ResolveTelegraphedAttack(seesTarget, visibleTargetPosition);
                }
            }
            else if (state == BrainState.AttackRecovery)
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                    state = BrainState.Idle;
            }
            else if (seesTarget)
            {
                Vector3 toTarget = visibleTargetPosition - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance <= SafeNonNegative(attackRange))
                {
                    facing = distance > 0.001f ? toTarget / distance : transform.forward;
                    if (Time.time >= nextAttackTime)
                        BeginTelegraphedAttack(visibleTargetPosition);
                    else
                    {
                        state = BrainState.AttackRecovery;
                        attackTimer = Mathf.Max(0f, nextAttackTime - Time.time);
                    }
                }
                else
                {
                    state = BrainState.Pursuing;
                    move = distance > 0.001f ? toTarget / distance : Vector3.zero;
                    facing = move;
                }
            }
            else if (HasUnexpiredTargetMemory())
            {
                Vector3 toLastKnown = lastKnownTargetPosition - transform.position;
                toLastKnown.y = 0f;
                float distance = toLastKnown.magnitude;
                state = BrainState.Investigating;
                if (distance > SafeNonNegative(investigationArrivalDistance))
                {
                    move = toLastKnown / distance;
                    facing = move;
                }
            }
            else
            {
                ClearTargetMemory();
                Vector3 home = spawnPosition - transform.position;
                home.y = 0f;
                if (home.sqrMagnitude > 1.5f * 1.5f)
                {
                    state = BrainState.ReturningHome;
                    move = home.normalized;
                    facing = move;
                }
                else
                    state = BrainState.Idle;
            }

            if (facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(facing, Vector3.up),
                    540f * Time.deltaTime);
            }

            fallingSpeed = controller.isGrounded ? -2f :
                Mathf.Max(-40f, fallingSpeed - 25f * Time.deltaTime);
            Vector3 velocity = move * SafeNonNegative(moveSpeed) + Vector3.up * fallingSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        private bool TrySeeTarget(out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;
            if (target == null || targetHealth == null || targetHealth.IsDefeated ||
                !TryGetTargetAimPoint(out Vector3 aimPoint))
                return false;

            Vector3 origin = GetVisionOrigin();
            Vector3 planarToTarget = aimPoint - origin;
            planarToTarget.y = 0f;
            float distance = planarToTarget.magnitude;
            if (distance > SafeNonNegative(aggroRange))
                return false;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
                return false;
            forward.Normalize();
            Vector3 direction = distance > 0.001f ? planarToTarget / distance : forward;
            float minimumDot = Mathf.Cos(Mathf.Clamp(viewAngle, 1f, 360f) * 0.5f * Mathf.Deg2Rad);
            if (Vector3.Dot(forward, direction) < minimumDot)
                return false;

            Vector3 ray = aimPoint - origin;
            float rayDistance = ray.magnitude;
            if (rayDistance > 0.001f && !HasClearLine(origin, ray / rayDistance, rayDistance))
                return false;

            targetPosition = target.position;
            return IsFinite(targetPosition);
        }

        private Vector3 GetVisionOrigin()
        {
            return controller != null && controller.enabled
                ? controller.bounds.center
                : transform.position + Vector3.up * 0.9f;
        }

        private bool TryGetTargetAimPoint(out Vector3 point)
        {
            point = target != null ? target.position : Vector3.zero;
            if (target == null)
                return false;
            Collider targetCollider = target.GetComponent<Collider>();
            if (targetCollider != null && targetCollider.enabled)
                point = targetCollider.bounds.center;
            else
                point += Vector3.up * 0.9f;
            return IsFinite(point);
        }

        private bool HasClearLine(Vector3 origin, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, SightHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = SightHits;
            if (count == SightHits.Length)
            {
                // Saturation is rare; use an exhaustive fallback so a full buffer cannot
                // accidentally turn a blocked sight line into a visible target.
                hits = Physics.RaycastAll(origin, direction, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                    continue;
                Transform hit = hitCollider.transform;
                if (hit == transform || hit.IsChildOf(transform) ||
                    hit == target || target != null && hit.IsChildOf(target))
                    continue;
                return false;
            }
            return true;
        }

        private void BeginTelegraphedAttack(Vector3 targetPosition)
        {
            float windup = Mathf.Max(0.05f, SafeNonNegative(attackWindupSeconds));
            float recovery = SafeNonNegative(attackRecoverySeconds);
            nextAttackTime = Time.time + Mathf.Max(
                Mathf.Max(0.1f, SafeNonNegative(attackCooldown)), windup + recovery);
            attackTimer = windup;
            attackDirection = PlanarDirectionTo(targetPosition);
            state = BrainState.AttackWindup;
            SetAttackTelegraphVisual(true);
        }

        private void ResolveTelegraphedAttack(bool seesTarget, Vector3 visibleTargetPosition)
        {
            SetAttackTelegraphVisual(false);
            if (seesTarget && targetHealth != null && !targetHealth.IsDefeated &&
                IsWithinAttackRange(visibleTargetPosition) &&
                IsWithinAttackArc(visibleTargetPosition) && TrySeeTarget(out _))
            {
                targetHealth.ApplyDamage(SafeNonNegative(attackDamage));
            }

            state = BrainState.AttackRecovery;
            attackTimer = Mathf.Max(SafeNonNegative(attackRecoverySeconds),
                Mathf.Max(0f, nextAttackTime - Time.time));
        }

        private bool IsWithinAttackRange(Vector3 targetPosition)
        {
            Vector3 planar = targetPosition - transform.position;
            planar.y = 0f;
            float range = SafeNonNegative(attackRange);
            return planar.sqrMagnitude <= range * range;
        }

        private bool IsWithinAttackArc(Vector3 targetPosition)
        {
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.001f)
                return true;

            float arc = IsFinite(attackArcDegrees) ? Mathf.Clamp(attackArcDegrees, 1f, 360f) : 100f;
            float minimumDot = Mathf.Cos(arc * 0.5f * Mathf.Deg2Rad);
            return Vector3.Dot(attackDirection, toTarget.normalized) >= minimumDot;
        }

        private Vector3 PlanarDirectionTo(Vector3 position)
        {
            Vector3 direction = position - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        }

        private void RememberPosition(Vector3 position)
        {
            if (!IsFinite(position))
                return;
            lastKnownTargetPosition = position;
            hasLastKnownTargetPosition = true;
            memoryExpiresAt = Time.time + SafeNonNegative(memorySeconds);
        }

        private bool HasUnexpiredTargetMemory() =>
            hasLastKnownTargetPosition && Time.time < memoryExpiresAt;

        private void ClearTargetMemory()
        {
            hasLastKnownTargetPosition = false;
            memoryExpiresAt = 0f;
        }

        private void SetAttackTelegraphVisual(bool visible)
        {
            if (modelRenderer == null || modelPropertyBlock == null)
                return;

            modelRenderer.GetPropertyBlock(modelPropertyBlock);
            if (hasBaseColor)
                modelPropertyBlock.SetColor(BaseColorProperty,
                    visible ? attackTelegraphColor : originalBaseColor);
            if (hasLegacyColor)
                modelPropertyBlock.SetColor(LegacyColorProperty,
                    visible ? attackTelegraphColor : originalLegacyColor);
            modelRenderer.SetPropertyBlock(modelPropertyBlock);
        }

        private void Respawn()
        {
            SetAttackTelegraphVisual(false);
            transform.position = spawnPosition;
            fallingSpeed = 0f;
            attackTimer = 0f;
            nextAttackTime = Time.time + 0.5f;
            ClearTargetMemory();
            state = BrainState.Idle;
            health.ResetToFull();
            if (modelRoot != null)
                modelRoot.SetActive(true);
            if (controller != null)
                controller.enabled = true;
            respawnAt = float.PositiveInfinity;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static float SafeNonNegative(float value) => IsFinite(value) ? Mathf.Max(0f, value) : 0f;
    }
}
