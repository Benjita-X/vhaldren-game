using System;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Magic;

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
        [Tooltip("Tipo de enemigo del bestiario (EnemyArchetypes). Vacio = enemigo clasico 008.")]
        [SerializeField] private string archetypeId = "";

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
        private float slowUntil;
        private float slowMultiplier = 1f;
        private bool windingUp;
        private float attackReleaseAt;
        private float staggerUntil;
        private EnemyArchetype archetype;
        private Vector3 knockback;
        private float nextSupportPulse;
        private bool enraged;
        private LineRenderer aimLine;
        private static readonly Collider[] allies = new Collider[24];

        public bool IsBoss => isBoss;
        public bool IsDefeated => defeated;
        public bool IsSlowed => !defeated && Time.time < slowUntil;
        public bool IsAttackTelegraphed => windingUp;
        public bool IsStaggered => !defeated && Time.time < staggerUntil;
        public EnemyArchetype Archetype => archetype;
        public bool IsEnraged => enraged;
        public CombatDamageKind ActiveAttackKind => activeAttackKind;
        public float AttackRange => attackRange;
        public float AttackDamage => attackDamage;
        public int SupportPulses { get; private set; }
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
            // Modelo KayKit animado (si la escena lo trae bajo "Visual enemigo").
            EnemyAnimationDriver.Attach(this, modelRoot != null ? modelRoot.transform : transform.Find("Visual enemigo"));
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
            if (!string.IsNullOrEmpty(archetypeId))
                ApplyArchetype(archetypeId);
        }

        /// <summary>
        /// Convierte a este enemigo en un tipo del bestiario: vida, daño, velocidad,
        /// alcance, resistencias, tactica y aspecto. Seguro de llamar en tiempo de juego.
        /// </summary>
        public bool ApplyArchetype(string id)
        {
            EnemyArchetype data = EnemyArchetypes.Find(id);
            if (data == null)
                return false;
            archetype = data;
            archetypeId = id;
            moveSpeed = data.Speed;
            attackDamage = data.Damage;
            attackRange = data.AttackRange;
            attackWindup = data.Windup;
            attackCooldown = data.Cooldown;
            aggroRange = data.Aggro;
            if (health == null)
                health = GetComponent<CombatHealth>();
            health?.SetMaximumHealth(data.Health);
            CombatDamageReceiver receiver = GetComponent<CombatDamageReceiver>();
            receiver?.ConfigureResistances(data.MeleeResist, data.ArrowResist, data.MagicResist);
            EnemyArchetypeVisual visual = GetComponent<EnemyArchetypeVisual>();
            if (visual == null)
                visual = gameObject.AddComponent<EnemyArchetypeVisual>();
            visual.Build(data);
            nextSupportPulse = Time.time + 5f;
            return true;
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
            AlertAllies();
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

            UpdateRoleAbilities();

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
                    float kite = archetype != null ? archetype.KiteDistance : 0f;
                    // Tiradores, magos y sanadoras retroceden mientras recargan.
                    if (kite > 0f && distance < kite && Time.time < nextAttackTime && distance > 0.001f)
                        move = -planar / distance;
                    else if (distance > attackRange && distance > 0.001f)
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
            float currentSpeed = Mathf.Max(0f, moveSpeed) * (enraged ? 1.25f : 1f) *
                (Time.time < slowUntil ? slowMultiplier : 1f);
            // Empujon del golpe: se frena rapido y respeta colisiones (CharacterController).
            Vector3 push = knockback;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 30f * Time.deltaTime);
            Vector3 velocity = push + move * currentSpeed +
                Vector3.up * fallingSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        private void LateUpdate()
        {
            UpdateAimLine();
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
                // Deslumbrado (Lumancia) reduce el daño que causa este enemigo.
                float outgoing = Mathf.Max(0f, attackDamage) *
                    Vhaldren.Gameplay.Magic.StatusEffects.OutgoingFor(this) *
                    DayNightCycle.EnemyDamageMultiplier *
                    Vhaldren.Gameplay.Progression.CombatLevel.OutgoingFor(this);
                CombatDamageResult response = defender.Receive(
                    outgoing, activeAttackKind, transform.position);
                ShowAimLine(false);
                if (archetype != null && archetype.IsRangedRole)
                    DrawTracer(activeAttackKind == CombatDamageKind.Magic);
                if (response.Applied > 0f && archetype != null && archetype.AppliesStatus)
                    StatusEffects.For(target)?.ApplyFrom(archetype.OnHit, transform.position);
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
        /// <summary>
        /// Ralentizacion temporal por magia sin tocar las estadisticas base.
        /// Reaplicar no reduce el multiplicador de forma acumulativa.
        /// </summary>
        public void ApplySlow(float multiplier, float seconds)
        {
            if (defeated || float.IsNaN(multiplier) || float.IsInfinity(multiplier) ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                return;
            float safeMultiplier = Mathf.Clamp(multiplier, 0.25f, 1f);
            if (Time.time < slowUntil)
                slowMultiplier = Mathf.Min(slowMultiplier, safeMultiplier);
            else
                slowMultiplier = safeMultiplier;
            slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
        }

        public void InterruptAttack(float recoverySeconds = 0.18f)
        {
            CancelAttack();
            if (float.IsNaN(recoverySeconds) || float.IsInfinity(recoverySeconds))
                recoverySeconds = 0f;
            float safe = Mathf.Clamp(recoverySeconds, 0f, 3f);
            staggerUntil = Mathf.Max(staggerUntil, Time.time + safe);
            nextAttackTime = Mathf.Max(nextAttackTime, staggerUntil + 0.08f);
        }

        public bool IsProvoked => !defeated && Time.time < provokedUntil;
        public Vector3 Knockback => knockback;

        /// <summary>Empuja al enemigo unos centimetros en la direccion del golpe.</summary>
        public void ApplyKnockback(Vector3 direction, float distance)
        {
            if (defeated || float.IsNaN(distance) || distance <= 0f)
                return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            // v^2 = 2·a·d con frenado de 30 m/s^2.
            knockback = direction.normalized * Mathf.Sqrt(60f * Mathf.Min(distance, 2f));
        }

        /// <summary>
        /// Un campamento pelea en grupo: al atacar a uno, sus compañeros a 10 m acuden.
        /// Asi no se puede limpiar un asentamiento enemigo de uno en uno sin preparacion.
        /// </summary>
        private void AlertAllies()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, 10f, allies,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                PrototypeEnemy ally = allies[i] != null ? allies[i].GetComponentInParent<PrototypeEnemy>() : null;
                if (ally == null || ally == this || ally.defeated || ally.returningHome)
                    continue;
                ally.provokedUntil = Mathf.Max(ally.provokedUntil, Time.time + 10f);
            }
        }

        private CombatDamageKind AttackKindFor(int sequence)
        {
            if (archetype != null)
            {
                switch (archetype.Role)
                {
                    case EnemyRole.Ranged: return CombatDamageKind.Arrow;
                    case EnemyRole.Caster:
                    case EnemyRole.Support: return CombatDamageKind.Magic;
                    case EnemyRole.Elite: return sequence % 2 == 0 ? CombatDamageKind.Magic : CombatDamageKind.Melee;
                }
                return CombatDamageKind.Melee;
            }
            // El jefe alterna espada y ataque runico (1 de cada 3; enfurecido, 1 de cada 2).
            if (isBoss)
                return sequence % (enraged ? 2 : 3) == 0 ? CombatDamageKind.Magic : CombatDamageKind.Melee;
            return CombatDamageKind.Melee;
        }

        /// <summary>Habilidades propias del rol: curacion de grupo y furia del jefe.</summary>
        private void UpdateRoleAbilities()
        {
            if (health == null || health.IsDefeated)
                return;
            if (isBoss && !enraged && health.CurrentHealth <= health.MaximumHealth * 0.5f)
            {
                enraged = true;
                SpellImpactFlash.Spawn(transform.position + Vector3.up, new Color(0.9f, 0.1f, 0.2f), 1.6f, 0.6f, 3f);
            }
            if (archetype != null && archetype.Role == EnemyRole.Support && Time.time >= nextSupportPulse &&
                (Time.time < provokedUntil || HasTargetNearby()))
            {
                nextSupportPulse = Time.time + 5f;
                HealAllies();
            }
        }

        private bool HasTargetNearby()
        {
            if (target == null)
                return false;
            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= aggroRange * aggroRange;
        }

        /// <summary>Cura a los aliados heridos a 8 m (14 % de su vida). Devuelve cuantos.</summary>
        public int HealAllies()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, 8f, allies,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int healed = 0;
            for (int i = 0; i < count; i++)
            {
                PrototypeEnemy ally = allies[i] != null ? allies[i].GetComponentInParent<PrototypeEnemy>() : null;
                if (ally == null || ally == this || ally.defeated || ally.health == null)
                    continue;
                if (ally.health.Heal(ally.health.MaximumHealth * 0.14f) > 0f)
                {
                    healed++;
                    SpellImpactFlash.Spawn(ally.transform.position + Vector3.up, new Color(0.35f, 1f, 0.45f), 0.6f, 0.35f);
                }
            }
            SupportPulses++;
            SpellImpactFlash.Spawn(transform.position, new Color(0.35f, 1f, 0.45f), 0.9f, 0.5f, 8f);
            return healed;
        }

        private void ShowAimLine(bool visible)
        {
            if (!visible)
            {
                if (aimLine != null)
                    aimLine.enabled = false;
                return;
            }
            if (aimLine == null)
            {
                GameObject line = new GameObject("Linea de tiro — sin colision");
                line.transform.SetParent(transform, false);
                aimLine = line.AddComponent<LineRenderer>();
                aimLine.sharedMaterial = PrototypeVfx.SharedMaterial;
                aimLine.useWorldSpace = true;
                aimLine.positionCount = 2;
                aimLine.widthMultiplier = 0.06f;
                aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Color tint = activeAttackKind == CombatDamageKind.Magic
                ? new Color(0.3f, 0.7f, 1f, 0.8f) : new Color(1f, 0.2f, 0.15f, 0.8f);
            aimLine.startColor = aimLine.endColor = tint;
            aimLine.enabled = true;
            UpdateAimLine();
        }

        private void UpdateAimLine()
        {
            if (aimLine == null || !aimLine.enabled || target == null)
                return;
            aimLine.SetPosition(0, transform.position + Vector3.up * 0.4f);
            aimLine.SetPosition(1, target.position + Vector3.up * 0.4f);
        }

        private void DrawTracer(bool magic)
        {
            if (target == null)
                return;
            GameObject tracer = new GameObject("Proyectil enemigo — sin colision");
            LineRenderer line = tracer.AddComponent<LineRenderer>();
            line.sharedMaterial = PrototypeVfx.SharedMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = magic ? 0.18f : 0.08f;
            line.startColor = line.endColor = magic ? new Color(1f, 0.55f, 0.15f) : new Color(0.9f, 0.85f, 0.7f);
            line.SetPosition(0, transform.position + Vector3.up * 0.4f);
            line.SetPosition(1, target.position + Vector3.up * 0.4f);
            Destroy(tracer, 0.12f);
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
            activeAttackKind = AttackKindFor(attackSequence);
            attackReleaseAt = Time.time + safeWindup;
            float cooldown = Mathf.Max(0.1f, attackCooldown) * (enraged ? 0.7f : 1f);
            nextAttackTime = Time.time + Mathf.Max(cooldown, safeWindup + 0.2f);
            bool ranged = archetype != null && archetype.IsRangedRole;
            if (attackWarning != null)
                attackWarning.Begin(ranged ? 1.1f : attackRange, safeWindup, isBoss,
                    activeAttackKind == CombatDamageKind.Magic);
            if (ranged)
                ShowAimLine(true);
            // Aviso sonoro del golpe: se oye venir antes de que impacte.
            if (GameFeel.Enabled)
                GameAudio.Play(activeAttackKind == CombatDamageKind.Magic ? GameSound.SpellCast : GameSound.Whoosh,
                    transform.position, isBoss ? 0.9f : 0.45f, 0.12f);
        }

        private void CancelAttack()
        {
            windingUp = false;
            ShowAimLine(false);
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
            slowUntil = 0f;
            slowMultiplier = 1f;
            staggerUntil = 0f;
            enraged = false;
            knockback = Vector3.zero;
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
