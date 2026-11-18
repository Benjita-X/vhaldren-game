using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.NPC
{
    /// <summary>
    /// Aldeano de un poblado. Usa un modelo KayKit (campo <see cref="modelPrefab"/>, que
    /// rellena el generador del mapa desde el editor) o, si falta, un cuerpo de sustituto.
    /// Pasea cerca de su casa con animación de caminar y reposo, se detiene y se gira hacia
    /// el jugador cuando se acerca, suelta frases al verlo pasar y abre un diálogo con E
    /// (lo gestiona <see cref="NpcInteractor"/> en el jugador). Lejos del jugador duerme.
    /// No tiene vida: los hechizos y armas lo ignoran.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcVillager : MonoBehaviour
    {
        public const float TalkRange = 3.2f;
        public const float NoticeRange = 6.5f;
        public const float SleepDistance = 65f;
        public const float WakeDistance = 55f;
        public const float HealCooldown = 45f;

        private static readonly List<NpcVillager> all = new List<NpcVillager>();
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly RaycastHit[] rayHits = new RaycastHit[8];
        private static Material fallbackMaterial;

        /// <summary>Herramientas de mano preferidas por oficio (el resto de armas KayKit se ocultan).</summary>
        private static readonly string[][] ToolsByRole =
        {
            new string[0],                                        // Aldeano
            new[] { "1H_Axe", "1H_Sword", "2H_Axe" },             // Herrero
            new[] { "Mug", "Spellbook" },                         // Mercader
            new[] { "Spellbook_open", "1H_Wand", "Spellbook" },   // Sanadora
            new[] { "2H_Staff", "Knife", "1H_Crossbow" }          // Guía
        };

        [Tooltip("Prefab KayKit del aldeano (Knight, Barbarian, Mage, Rogue o RogueHooded). Lo rellena el generador del mapa.")]
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private string npcId = "";
        [SerializeField] private string displayName = "Aldeano";
        [SerializeField] private string trade = "aldeano";
        [SerializeField] private string modelKind = "Knight";
        [SerializeField] private string[] lines = new string[0];
        [SerializeField] private Vector3 home;
        [SerializeField] private bool hasHome;
        [SerializeField, Min(0.5f)] private float wanderRadius = 5f;
        [SerializeField, Min(0.2f)] private float walkSpeed = 1.25f;
        [SerializeField] private string guidePlace = "";
        [SerializeField] private Vector3 guideTarget;
        [SerializeField] private bool hasGuideTarget;

        private NpcRole role = NpcRole.Villager;
        private Transform model;
        private Animator animator;
        private bool hasSpeedParameter;
        private bool built;
        private bool asleep;
        private bool walking;
        private bool talking;
        private Vector3 wanderTarget;
        private float idleUntil;
        private float walkTimeout;
        private float smoothedSpeed;
        private float nextSleepCheck;
        private float nextObstacleCheck;
        private float nextGroundSnap;
        private float groundY;
        private bool groundKnown;
        private Transform player;
        private float nextPlayerLookup;
        private string bark;
        private float barkUntil;
        private float nextBarkAllowed;
        private bool playerWasNear;

        public static IReadOnlyList<NpcVillager> All => all;

        public string Id => npcId;
        public string DisplayName => displayName;
        public string Trade => trade;
        public NpcRole Role => role;
        public string ModelKind => modelKind;
        public string[] Lines => lines;
        public Vector3 Home => hasHome ? home : transform.position;
        public GameObject ModelPrefab => modelPrefab;
        public Transform Model => model;
        public bool IsAsleep => asleep;
        public bool IsWalking => walking;
        public bool IsTalking => talking;
        public bool HasGuideTarget => hasGuideTarget;
        public Vector3 GuideTarget => guideTarget;
        public string GuidePlace => guidePlace;
        public Color Tint => NpcDialogue.PortraitColor(role);
        /// <summary>Frase en el bocadillo (null si no habla ahora).</summary>
        public string CurrentBark => Time.time < barkUntil ? bark : null;
        /// <summary>Instante a partir del cual la sanadora puede volver a curar.</summary>
        public float HealReadyAt { get; set; }
        /// <summary>Veces que el jugador le ha preguntado por el lugar (alterna las historias).</summary>
        public int LoreTurn { get; set; }
        public int Conversations { get; private set; }

        /// <summary>
        /// Configura al aldeano (lo llama el generador del mapa). <paramref name="casa"/> es el
        /// centro de su paseo; <paramref name="modeloKayKit"/> el tipo de modelo (Knight,
        /// Barbarian, Mage, Rogue, RogueHooded) que se usa si no hay prefab asignado.
        /// </summary>
        public void Configure(string id, string nombre, string oficio, string[] frases, Vector3 casa, string modeloKayKit)
        {
            npcId = id ?? string.Empty;
            displayName = string.IsNullOrEmpty(nombre) ? "Aldeano" : nombre;
            trade = string.IsNullOrEmpty(oficio) ? "aldeano" : oficio;
            lines = frases ?? new string[0];
            home = casa;
            hasHome = true;
            string kind = string.IsNullOrEmpty(modeloKayKit) ? "Knight" : modeloKayKit;
            bool rebuild = built && kind != modelKind;
            modelKind = kind;
            role = NpcDialogue.RoleFor(trade);
            gameObject.name = "NPC " + displayName + " (" + trade + ")";
            // Si lo colocaron lejos de su casa (o en el origen), empieza en la puerta.
            Vector3 flat = transform.position - casa;
            flat.y = 0f;
            if (flat.sqrMagnitude > 15f * 15f || transform.position == Vector3.zero)
                transform.position = casa;
            groundKnown = false;
            if (rebuild)
                RebuildBody();
        }

        /// <summary>Asigna el prefab KayKit (desde el generador o el editor) y rehace el cuerpo.</summary>
        public void SetModelPrefab(GameObject prefab)
        {
            modelPrefab = prefab;
            if (built)
                RebuildBody();
        }

        /// <summary>Lugar que marca el guía (si no, marca el altar del jefe o el tablón del Cruce).</summary>
        public void SetGuideTarget(Vector3 point, string placeName)
        {
            guideTarget = point;
            guidePlace = string.IsNullOrEmpty(placeName) ? "el lugar marcado" : placeName;
            hasGuideTarget = true;
        }

        /// <summary>Radio del paseo alrededor de su casa y velocidad al caminar.</summary>
        public void SetWander(float radius, float speed)
        {
            wanderRadius = Mathf.Max(0.5f, radius);
            walkSpeed = Mathf.Max(0.2f, speed);
        }

        /// <summary>Muestra una frase en un bocadillo sobre su cabeza.</summary>
        public void Say(string text, float seconds = 3f)
        {
            if (string.IsNullOrEmpty(text))
                return;
            bark = text;
            barkUntil = Time.time + Mathf.Max(0.5f, seconds);
        }

        internal void BeginTalk()
        {
            talking = true;
            walking = false;
            Conversations++;
            barkUntil = 0f;
        }

        internal void EndTalk()
        {
            talking = false;
            idleUntil = Time.time + Random.Range(1.5f, 3f);
        }

        // ── Ciclo de vida ──────────────────────────────────────────────

        private void Awake()
        {
            role = NpcDialogue.RoleFor(trade);
            if (!hasHome)
            {
                home = transform.position;
                hasHome = true;
            }
        }

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable() => all.Remove(this);

        private void Start()
        {
            BuildBody();
            idleUntil = Time.time + Random.Range(0.5f, 3f);
            nextSleepCheck = Time.time + Random.value * 0.5f;
        }

        private void RebuildBody()
        {
            if (model != null)
                Destroy(model.gameObject);
            model = null;
            animator = null;
            built = false;
            BuildBody();
        }

        private void BuildBody()
        {
            if (built)
                return;
            built = true;
            role = NpcDialogue.RoleFor(trade);
            GameObject prefab = modelPrefab != null ? modelPrefab : Resources.Load<GameObject>("NPC/" + modelKind);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, transform, false);
                instance.name = "Modelo " + modelKind;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                StripGameplay(instance);
                model = instance.transform;
                ChooseTool(model);
                animator = instance.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    hasSpeedParameter = HasFloat(animator, "Speed");
                }
            }
            else
            {
                model = BuildFallbackBody();
            }

            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.height = 1.8f;
                capsule.radius = 0.32f;
            }
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private static void StripGameplay(GameObject instance)
        {
            // Solo el aspecto: nada de colisiones, vida ni IA que traiga el prefab.
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                Destroy(c);
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour is Vhaldren.Gameplay.World.PrototypeEnemy || behaviour is CombatDamageReceiver ||
                    behaviour is CombatHealth || behaviour is CombatDamageFeedback)
                    Destroy(behaviour);
        }

        /// <summary>Deja en las manos solo la herramienta de su oficio (los KayKit traen todas a la vez).</summary>
        private void ChooseTool(Transform root)
        {
            string[] wanted = ToolsByRole[Mathf.Clamp((int)role, 0, ToolsByRole.Length - 1)];
            bool shownRight = false, shownLeft = false;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "handslot.r" && t.name != "handslot.l")
                    continue;
                bool rightHand = t.name == "handslot.r";
                foreach (Transform item in t)
                {
                    bool keep = false;
                    if ((rightHand && !shownRight) || (!rightHand && !shownLeft))
                        for (int i = 0; i < wanted.Length && !keep; i++)
                            keep = item.name == wanted[i] && (rightHand || item.name.Contains("Spellbook") || item.name == "Mug");
                    item.gameObject.SetActive(keep);
                    if (keep)
                    {
                        if (rightHand) shownRight = true;
                        else shownLeft = true;
                    }
                }
            }
        }

        private Transform BuildFallbackBody()
        {
            Transform root = new GameObject("Cuerpo de sustituto").transform;
            root.SetParent(transform, false);
            Color tint = NpcDialogue.PortraitColor(role);
            Part(root, PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0f), new Vector3(0.62f, 0.85f, 0.62f), tint * 0.8f);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 1.82f, 0f), Vector3.one * 0.42f, new Color(0.86f, 0.72f, 0.6f));
            Part(root, PrimitiveType.Cube, new Vector3(0f, 1.25f, -0.22f), new Vector3(0.6f, 0.75f, 0.06f), tint * 0.55f);
            return root;
        }

        private static void Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Collider c = part.GetComponent<Collider>();
            if (c != null)
                DestroyImmediate(c);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Renderer r = part.GetComponent<Renderer>();
            if (fallbackMaterial == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader != null)
                    fallbackMaterial = new Material(shader) { name = "Aldeano (sustituto)" };
            }
            if (r != null && fallbackMaterial != null)
            {
                r.sharedMaterial = fallbackMaterial;
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }

        private static bool HasFloat(Animator target, string name)
        {
            if (target.runtimeAnimatorController == null)
                return false;
            foreach (AnimatorControllerParameter p in target.parameters)
                if (p.type == AnimatorControllerParameterType.Float && p.name == name)
                    return true;
            return false;
        }

        // ── Comportamiento ─────────────────────────────────────────────

        private void Update()
        {
            if (!built)
                return;
            if (player == null && Time.time >= nextPlayerLookup)
            {
                nextPlayerLookup = Time.time + 2f;
                FindPlayer();
            }

            if (Time.time >= nextSleepCheck)
            {
                nextSleepCheck = Time.time + 0.5f;
                float distance = player != null ? PlanarDistance(player.position) : 0f;
                bool sleep = asleep ? distance > WakeDistance : distance > SleepDistance;
                if (sleep != asleep)
                    SetAsleep(sleep);
            }
            if (asleep)
                return;

            float dt = Time.deltaTime;
            float speed = 0f;
            bool near = player != null && PlanarDistance(player.position) <= NoticeRange;
            if (talking || near)
            {
                walking = false;
                FaceTowards(player != null ? player.position : transform.position + transform.forward, dt);
                if (near && !talking && !playerWasNear && Time.time >= nextBarkAllowed)
                {
                    nextBarkAllowed = Time.time + Random.Range(18f, 30f);
                    Say(NpcDialogue.Bark(role, lines, Vhaldren.Gameplay.Races.PlayerRace.Local != null &&
                        Vhaldren.Gameplay.Races.PlayerRace.Local.Race == Vhaldren.Gameplay.Races.RaceId.Lucifer,
                        Random.Range(0, 1000)), 3f);
                }
            }
            else if (walking)
            {
                speed = Walk(dt);
            }
            else if (Time.time >= idleUntil)
            {
                PickWanderTarget();
            }
            playerWasNear = near;

            if (speed > 0f || Time.time >= nextGroundSnap)
            {
                nextGroundSnap = Time.time + 0.5f;
                SnapToGround(speed > 0f ? 12f * dt : 1f);
            }

            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-10f * dt));
            if (animator != null && hasSpeedParameter && animator.isActiveAndEnabled)
                animator.SetFloat(SpeedId, smoothedSpeed * 1.6f);
            else if (animator == null && model != null)
                model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 0.05f * Mathf.Clamp01(smoothedSpeed), 0f);
        }

        private void FindPlayer()
        {
            Vhaldren.Gameplay.Races.PlayerRace race = Vhaldren.Gameplay.Races.PlayerRace.Local;
            GameObject found = race != null ? race.gameObject : GameObject.Find("PlayerRoot");
            player = found != null ? found.transform : null;
            // Si el líder no instaló el interactor, se instala aquí: hablar siempre funciona.
            if (player != null && player.GetComponent<NpcInteractor>() == null)
                NpcBootstrap.Install(player);
        }

        private float PlanarDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private void SetAsleep(bool value)
        {
            asleep = value;
            if (animator != null)
                animator.enabled = !value;
            if (value)
            {
                walking = false;
                barkUntil = 0f;
            }
        }

        private void FaceTowards(Vector3 point, float dt)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0004f)
                return;
            Quaternion target = Quaternion.LookRotation(d.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-7f * dt));
        }

        private void PickWanderTarget()
        {
            Vector3 center = Home;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = new Vector3(center.x + offset.x, transform.position.y, center.z + offset.y);
                Vector3 delta = candidate - transform.position;
                float length = delta.magnitude;
                if (length < 1f)
                    continue;
                if (Blocked(delta / length, length))
                    continue;
                wanderTarget = candidate;
                walking = true;
                walkTimeout = Time.time + length / Mathf.Max(0.2f, walkSpeed) + 3f;
                return;
            }
            idleUntil = Time.time + Random.Range(2f, 4f);
        }

        private float Walk(float dt)
        {
            Vector3 delta = wanderTarget - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 0.25f || Time.time >= walkTimeout)
            {
                walking = false;
                idleUntil = Time.time + Random.Range(2.5f, 6.5f);
                return 0f;
            }
            Vector3 direction = delta / distance;
            if (Time.time >= nextObstacleCheck)
            {
                nextObstacleCheck = Time.time + 0.25f;
                if (Blocked(direction, 0.8f))
                {
                    walking = false;
                    idleUntil = Time.time + Random.Range(1f, 2.5f);
                    return 0f;
                }
            }
            FaceTowards(transform.position + direction, dt);
            float step = Mathf.Min(distance, walkSpeed * dt);
            transform.position += direction * step;
            return walkSpeed;
        }

        private bool Blocked(Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.6f, direction, rayHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = rayHits[i].collider;
                if (hit != null && !hit.transform.IsChildOf(transform))
                    return true;
            }
            return false;
        }

        private void SnapToGround(float blend)
        {
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * 1.5f, Vector3.down, rayHits, 5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            float y = transform.position.y;
            for (int i = 0; i < count; i++)
            {
                Collider hit = rayHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<CombatHealth>() != null)
                    continue;
                if (rayHits[i].distance < best)
                {
                    best = rayHits[i].distance;
                    y = rayHits[i].point.y;
                }
            }
            if (best == float.PositiveInfinity)
                return;
            groundY = y;
            groundKnown = true;
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(p.y, groundY, Mathf.Clamp01(blend));
            transform.position = p;
        }
    }
}
