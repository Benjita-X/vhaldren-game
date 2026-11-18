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
        private Vector3 corpseVelocity;
        private Vector3 corpseFallAxis = Vector3.forward;
        private float corpseFeetOffset = 1f;
        private float rootUntil;
        private bool asleep;
        private float nextSleepCheck;
        private CombatDamageFeedback feedbackView;
        private Light[] sleepLights;
        private static readonly Collider[] allies = new Collider[24];
        private static readonly RaycastHit[] GroundHits = new RaycastHit[8];
        private static Transform sharedPlayer;
        private static float nextSharedLookup;

        /// <summary>
        /// Mas alla de esta distancia al jugador el enemigo duerme: no piensa, no anima y no
        /// dibuja su barra (el mapa grande reparte campamentos lejos unos de otros).
        /// </summary>
        public const float SleepDistance = 70f;
        /// <summary>Distancia a la que despierta (menor que la de dormir: sin parpadeos).</summary>
        public const float WakeDistance = 60f;

        /// <summary>Segundos que el cuerpo queda a la vista tras morir antes de ocultarse.</summary>
        public const float CorpseSeconds = 1.8f;
        private const float SinkSeconds = 0.45f;
        private EnemyAnimationDriver animationDriver;
        private float defeatedAt;
        private float hideModelAt = float.PositiveInfinity;
        private bool proceduralDeath;
        private bool landedDust;
        private bool modelRestSaved;
        private Vector3 modelRestPosition;
        private Quaternion modelRestRotation;

        public bool IsBoss => isBoss;
        public bool IsDefeated => defeated;
        /// <summary>Dormido por estar lejos del jugador (no gasta CPU).</summary>
        public bool IsAsleep => asleep;
        /// <summary>Atado (cadenas de Lucifer): no se mueve, pero puede golpear si te acercas.</summary>
        public bool IsRooted => !defeated && Time.time < rootUntil;
        public bool IsSlowed => !defeated && Time.time < slowUntil;
        public bool IsAttackTelegraphed => windingUp;
        /// <summary>A quién persigue y golpea (el jugador en el mundo).</summary>
        public Transform Target => target;
        public bool IsStaggered => !defeated && Time.time < staggerUntil;
        public EnemyArchetype Archetype => archetype;
        public bool IsEnraged => enraged;
        public CombatDamageKind ActiveAttackKind => activeAttackKind;
        public float AttackRange => attackRange;
        public float AttackDamage => attackDamage;
        public int SupportPulses { get; private set; }
        /// <summary>El cuerpo sigue a la vista (cayendo o en el suelo) tras la derrota.</summary>
        public bool IsCorpseVisible => defeated && modelRoot != null && modelRoot.activeSelf;
        /// <summary>Sin estado de muerte en su Animator (lobos, cuerpos provisionales): cae de lado por codigo.</summary>
        public bool UsesProceduralDeath => proceduralDeath;
        public EnemyAnimationDriver AnimationDriver => animationDriver;
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
            feedbackView = feedback;
            // Terreno con pendientes: sube cuestas de hasta 50° y peldaños de 35 cm.
            if (controller.slopeLimit < 50f)
                controller.slopeLimit = 50f;
            if (controller.stepOffset < 0.35f && controller.height >= 1.2f)
                controller.stepOffset = 0.35f;
            // Repartir las comprobaciones de sueño entre fotogramas.
            nextSleepCheck = Time.time + UnityEngine.Random.value * 0.5f;
            CombatDamageReceiver damageReceiver = GetComponent<CombatDamageReceiver>();
            if (damageReceiver == null)
                damageReceiver = gameObject.AddComponent<CombatDamageReceiver>();
            damageReceiver.ConfigureResistances(isBoss ? 0.12f : 0f,
                isBoss ? 0.16f : 0f, isBoss ? 0.26f : 0f);
            if (GetComponent<EnemyVisualPrototype>() == null)
                gameObject.AddComponent<EnemyVisualPrototype>();
            // Modelo KayKit animado (si la escena lo trae bajo "Visual enemigo").
            animationDriver = EnemyAnimationDriver.Attach(this,
                modelRoot != null ? modelRoot.transform : transform.Find("Visual enemigo"));
            if (modelRoot != null)
            {
                modelRestPosition = modelRoot.transform.localPosition;
                modelRestRotation = modelRoot.transform.localRotation;
                modelRestSaved = true;
            }
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
            if (asleep)
                SetAsleep(false);
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
            defeatedAt = Time.time;
            CancelAttack();
            respawnAt = Time.time + Mathf.Max(1f, respawnSeconds);
            corpseVelocity = Vector3.zero;
            corpseFallAxis = Vector3.forward;
            rootUntil = 0f;
            if (asleep)
                SetAsleep(false);
            if (controller != null)
                corpseFeetOffset = -(controller.center.y - controller.height * 0.5f) * Mathf.Abs(transform.lossyScale.y);
            // Las colisiones se apagan al instante: el cuerpo que cae ya no bloquea ni recibe golpes.
            if (controller != null)
                controller.enabled = false;
            returningHome = false;
            knockback = Vector3.zero;
            if (modelRoot != null)
            {
                // Animacion de muerte (Death_A) si el modelo la tiene; si no, cae de lado.
                proceduralDeath = animationDriver == null || !animationDriver.PlayDeath();
                landedDust = false;
                hideModelAt = Time.time + CorpseSeconds;
                if (TryGetComponent(out EnemyArchetypeVisual roleVisual))
                    roleVisual.SetDefeated(true);
            }
            Defeated?.Invoke(this);
        }

        /// <summary>
        /// Cuerpo tras la derrota: cae (por animacion o de lado), levanta polvo al tocar
        /// el suelo, se hunde un poco y se oculta a los <see cref="CorpseSeconds"/>.
        /// </summary>
        private void UpdateCorpse()
        {
            if (modelRoot == null || !modelRoot.activeSelf || hideModelAt == float.PositiveInfinity)
                return;
            float age = Time.time - defeatedAt;
            SlideCorpse(Time.deltaTime);
            Transform model = modelRoot.transform;
            Vector3 position = modelRestPosition;
            Quaternion rotation = modelRestRotation;
            float halfHeight = controller != null ? controller.height * 0.5f : 1f;
            if (proceduralDeath)
            {
                float fall = Mathf.Clamp01(age / 0.42f);
                fall *= fall;   // cae acelerando, como un peso muerto
                // Cae hacia donde lo empujo el golpe final (de lado si no hubo empujon).
                Vector3 axis = corpseFallAxis.sqrMagnitude > 0.01f ? corpseFallAxis.normalized : Vector3.forward;
                rotation = Quaternion.AngleAxis(82f * fall, axis) * modelRestRotation;
                position += Vector3.down * (halfHeight * 0.62f * fall);
            }
            if (!landedDust && age >= (proceduralDeath ? 0.42f : 0.55f))
            {
                landedDust = true;
                if (GameFeel.Enabled)
                {
                    Vector3 feet = transform.position + Vector3.down * (halfHeight - 0.1f);
                    VfxHost.Get()?.Smoke(feet, 0.5f, new Color(0.5f, 0.46f, 0.4f, 0.5f), 10, 0.55f, 0.7f, Vector3.up * 0.35f);
                }
            }
            float sinkStart = CorpseSeconds - SinkSeconds;
            if (age > sinkStart)
                position += Vector3.down * (Mathf.Clamp01((age - sinkStart) / SinkSeconds) * 0.75f);
            model.localPosition = position;
            model.localRotation = rotation;

            if (Time.time >= hideModelAt)
            {
                hideModelAt = float.PositiveInfinity;
                if (GameFeel.Enabled)
                {
                    Vector3 feet = transform.position + Vector3.down * (halfHeight - 0.15f);
                    VfxHost.Get()?.Smoke(feet, 0.45f, new Color(0.16f, 0.13f, 0.12f, 0.55f), 12, 0.7f, 0.8f, Vector3.up * 0.5f);
                }
                modelRoot.SetActive(false);
                RestoreModelPose();
            }
        }

        /// <summary>
        /// El cuerpo despedido se desliza y frena; se detiene contra paredes y sigue el
        /// suelo (pendientes) para no quedar flotando ni enterrado.
        /// </summary>
        private void SlideCorpse(float deltaTime)
        {
            if (corpseVelocity.sqrMagnitude < 0.0025f || deltaTime <= 0f)
                return;
            Vector3 step = corpseVelocity * deltaTime;
            float length = step.magnitude;
            Vector3 heading = step / Mathf.Max(0.0001f, length);
            if (Physics.Raycast(transform.position, heading, out RaycastHit wall, length + 0.45f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                !wall.collider.transform.IsChildOf(transform))
            {
                corpseVelocity = Vector3.zero;
                return;
            }
            Vector3 next = transform.position + step;
            if (FindGroundBelow(next, 1.5f, 4f, out float groundY))
                next.y = Mathf.Lerp(next.y, groundY + corpseFeetOffset, 0.6f);
            transform.position = next;
            corpseVelocity = Vector3.MoveTowards(corpseVelocity, Vector3.zero, 14f * deltaTime);
        }

        /// <summary>Altura del suelo bajo un punto, ignorando el propio cuerpo.</summary>
        private bool FindGroundBelow(Vector3 point, float above, float depth, out float groundY)
        {
            groundY = point.y;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * above, Vector3.down, GroundHits, above + depth,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider hit = GroundHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<CombatHealth>() != null)
                    continue;
                if (GroundHits[i].distance < best)
                {
                    best = GroundHits[i].distance;
                    groundY = GroundHits[i].point.y;
                }
            }
            return best < float.PositiveInfinity;
        }

        /// <summary>Decide si un enemigo debe dormir (con histeresis para no parpadear).</summary>
        public static bool ShouldSleep(float planarDistance, bool currentlyAsleep, bool busy)
        {
            if (float.IsNaN(planarDistance) || float.IsInfinity(planarDistance))
                return currentlyAsleep;
            if (currentlyAsleep)
                return planarDistance > WakeDistance;
            return !busy && planarDistance > SleepDistance;
        }

        /// <summary>Comprueba cada medio segundo si debe dormir o despertar. True = dormido.</summary>
        private bool UpdateSleep()
        {
            if (Time.time < nextSleepCheck)
                return asleep;
            nextSleepCheck = Time.time + 0.5f;
            if (target == null)
                TryAcquirePlayer();
            if (target == null)
            {
                if (asleep)
                    SetAsleep(false);
                return false;
            }
            Vector3 planar = target.position - transform.position;
            planar.y = 0f;
            bool busy = windingUp || Time.time < staggerUntil || knockback.sqrMagnitude > 0.01f ||
                        (controller != null && controller.enabled && !controller.isGrounded);
            bool sleep = ShouldSleep(planar.magnitude, asleep, busy);
            if (sleep != asleep)
                SetAsleep(sleep);
            return asleep;
        }

        /// <summary>
        /// En un mapa generado el jugador puede aparecer despues que los enemigos: se busca
        /// una sola vez cada 2 s para todos (no un Find por enemigo).
        /// </summary>
        private void TryAcquirePlayer()
        {
            if (sharedPlayer == null && Time.time >= nextSharedLookup)
            {
                nextSharedLookup = Time.time + 2f;
                GameObject found = GameObject.Find("PlayerRoot");
                sharedPlayer = found != null ? found.transform : null;
            }
            if (sharedPlayer == null)
                return;
            target = sharedPlayer;
            targetHealth = target.GetComponent<CombatHealth>();
            targetMotor = target.GetComponent<PlayerMotor>();
        }

        private void SetAsleep(bool value)
        {
            if (asleep == value)
                return;
            asleep = value;
            if (animationDriver != null)
                animationDriver.SetSleeping(value);
            if (feedbackView != null)
                feedbackView.enabled = !value;
            if (value)
            {
                CancelAttack();
                knockback = Vector3.zero;
                provokedUntil = 0f;
                // Nadie lo ve a 70 m: si volvia a casa, llega de golpe.
                if (returningHome)
                {
                    returningHome = false;
                    TeleportTo(spawnPosition);
                }
                sleepLights = GetComponentsInChildren<Light>(false);
                for (int i = 0; i < sleepLights.Length; i++)
                    if (sleepLights[i] != null)
                        sleepLights[i].enabled = false;
            }
            else
            {
                if (sleepLights != null)
                    for (int i = 0; i < sleepLights.Length; i++)
                        if (sleepLights[i] != null)
                            sleepLights[i].enabled = true;
                sleepLights = null;
                fallingSpeed = 0f;
                SnapToGround();
                nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 0.4f);
            }
        }

        /// <summary>Apoya el cuerpo en el suelo (al despertar o tras caer del mapa).</summary>
        private void SnapToGround()
        {
            if (controller == null || !controller.enabled)
                return;
            float feet = -(controller.center.y - controller.height * 0.5f) * Mathf.Abs(transform.lossyScale.y);
            if (FindGroundBelow(transform.position, 2f, 6f, out float groundY))
            {
                float wanted = groundY + feet + controller.skinWidth;
                if (Mathf.Abs(wanted - transform.position.y) > 0.05f)
                    TeleportTo(new Vector3(transform.position.x, wanted, transform.position.z));
            }
        }

        private void TeleportTo(Vector3 position)
        {
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
                controller.enabled = false;
            transform.position = position;
            if (wasEnabled)
                controller.enabled = true;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Ata al enemigo en el sitio (cadenas de azufre): no se desplaza durante unos
        /// segundos, aunque sigue golpeando si el jugador se pone a su alcance.
        /// </summary>
        public void ApplyRoot(float seconds)
        {
            if (defeated || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                return;
            rootUntil = Mathf.Max(rootUntil, Time.time + Mathf.Min(seconds, 3f));
        }

        private void RestoreModelPose()
        {
            if (modelRoot == null || !modelRestSaved)
                return;
            modelRoot.transform.localPosition = modelRestPosition;
            modelRoot.transform.localRotation = modelRestRotation;
        }

        private void Update()
        {
            if (defeated)
            {
                UpdateCorpse();
                if (Time.time >= respawnAt)
                    Respawn();
                return;
            }

            if (controller == null || !controller.enabled)
                return;

            // Lejos del jugador duerme: ni IA, ni gravedad, ni animacion.
            if (UpdateSleep())
                return;

            // Si cayo fuera del terreno (hueco del mapa), vuelve a su sitio.
            if (transform.position.y < spawnPosition.y - 40f)
            {
                TeleportTo(spawnPosition);
                fallingSpeed = 0f;
                SnapToGround();
            }

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

            // Atado: no se desplaza, pero sigue mirando a su objetivo.
            Vector3 face = move;
            if (Time.time < rootUntil)
            {
                if (target != null)
                {
                    face = target.position - transform.position;
                    face.y = 0f;
                }
                move = Vector3.zero;
            }
            if (face.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(face, Vector3.up),
                    540f * Time.deltaTime);
            }

            float currentSpeed = Mathf.Max(0f, moveSpeed) * (enraged ? 1.25f : 1f) *
                (Time.time < slowUntil ? slowMultiplier : 1f);
            // En el suelo empuja hacia abajo segun la velocidad: al bajar una cuesta sigue
            // pegado al terreno en vez de ir dando saltitos.
            fallingSpeed = controller.isGrounded
                ? -(2f + (move.sqrMagnitude > 0.001f ? currentSpeed * 1.2f : 0f))
                : Mathf.Max(-40f, fallingSpeed - 25f * Time.deltaTime);
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
                    StatusEffects.For(target)?.ApplyFrom(archetype.OnHit, this);
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

        /// <summary>Direccion y velocidad con la que sale despedido el cuerpo al morir.</summary>
        public Vector3 CorpseVelocity => corpseVelocity;

        /// <summary>
        /// Muerte con impulso: el cuerpo se desliza en la direccion del golpe que remata
        /// (sin atravesar paredes) antes de caer. Solo tras la derrota.
        /// </summary>
        public void LaunchCorpse(Vector3 direction, float speed)
        {
            if (!defeated || float.IsNaN(speed) || speed <= 0f)
                return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            corpseVelocity = direction.normalized * Mathf.Min(speed, 12f);
            corpseFallAxis = Vector3.Cross(Vector3.up, transform.InverseTransformDirection(direction.normalized));
        }

        /// <summary>Empuja al enemigo unos centimetros en la direccion del golpe.</summary>
        public void ApplyKnockback(Vector3 direction, float distance)
        {
            if (defeated || float.IsNaN(distance) || distance <= 0f)
                return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            // v^2 = 2·a·d con frenado de 30 m/s^2.
            knockback = direction.normalized * Mathf.Sqrt(60f * Mathf.Min(distance, 3f));
        }

        /// <summary>
        /// Un campamento pelea en grupo: al atacar a uno, sus compañeros a 10 m acuden.
        /// Asi no se puede limpiar un asentamiento enemigo de uno en uno sin preparacion.
        /// </summary>
        private void AlertAllies()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, 10f, allies,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider[] scan = allies;
            if (count >= allies.Length)
            {
                scan = Physics.OverlapSphere(transform.position, 10f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = scan.Length;
            }
            for (int i = 0; i < count; i++)
            {
                PrototypeEnemy ally = scan[i] != null ? scan[i].GetComponentInParent<PrototypeEnemy>() : null;
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
            Collider[] scan = allies;
            if (count >= allies.Length)
            {
                scan = Physics.OverlapSphere(transform.position, 8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = scan.Length;
            }
            int healed = 0;
            for (int i = 0; i < count; i++)
            {
                PrototypeEnemy ally = scan[i] != null ? scan[i].GetComponentInParent<PrototypeEnemy>() : null;
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
                Material beam = VfxLibrary.AdditiveBeam;
                aimLine.sharedMaterial = beam != null ? beam : PrototypeVfx.SharedMaterial;
                aimLine.useWorldSpace = true;
                aimLine.positionCount = 2;
                aimLine.widthMultiplier = 0.09f;
                aimLine.textureMode = LineTextureMode.Stretch;
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
            // Haz reutilizable (sin crear ni destruir objetos por disparo): virote recto y
            // palido, o brasas del acolito con un leve zigzag y chispas al llegar.
            Vector3 from = transform.position + Vector3.up * 0.4f;
            Vector3 to = target.position + Vector3.up * 0.4f;
            LightningArc.Strike(from, to, magic ? new Color(1f, 0.5f, 0.12f) : new Color(0.95f, 0.88f, 0.7f),
                magic ? 0.12f : 0.05f, magic ? 0.18f : 0.12f, magic ? 0.12f : 0f);
            if (magic)
                VfxHost.Get()?.Burst(VfxElement.Fire, to, from - to, new Color(1f, 0.8f, 0.45f), 10, 3.5f, 0.14f, 0.45f, 0.8f, 0.1f);
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
            corpseVelocity = Vector3.zero;
            corpseFallAxis = Vector3.forward;
            rootUntil = 0f;
            CancelAttack();
            provokedUntil = 0f;
            returningHome = false;
            health.ResetToFull();
            // Si reaparece antes de ocultarse el cuerpo, se cancela el ocultado pendiente.
            hideModelAt = float.PositiveInfinity;
            proceduralDeath = false;
            if (modelRoot != null)
            {
                RestoreModelPose();
                modelRoot.SetActive(true);
            }
            if (animationDriver != null)
                animationDriver.ResetAfterRespawn();
            if (TryGetComponent(out EnemyArchetypeVisual roleVisual))
                roleVisual.SetDefeated(false);
            if (controller != null)
                controller.enabled = true;
            defeated = false;
            respawnAt = float.PositiveInfinity;
        }
    }
}
