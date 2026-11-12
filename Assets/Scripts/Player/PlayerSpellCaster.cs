using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Dos habilidades locales diferenciadas que reutilizan la punteria de LocalPlayerShooter:
    /// <list type="bullet">
    /// <item>Clic derecho: Orbe arcano. Proyectil rapido y silencioso que viaja hacia el
    /// cursor; dano individual; hace ruido sobre todo donde impacta (sirve de senuelo).</item>
    /// <item>E: Estallido de sangre. Aviso en el suelo durante un instante y luego dano a
    /// todos los enemigos dentro del radio con linea despejada; interrumpe ataques
    /// enemigos en preparacion. Ruidoso y con espera larga.</item>
    /// </list>
    /// Ninguna bloquea el movimiento ni mueve enemigos. No autoritativo para red.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpellCaster : MonoBehaviour
    {
        private const int AreaCapacity = 32;

        [Header("Orbe arcano (clic derecho)")]
        [SerializeField, Min(1f)] private float projectileSpeed = 21f;
        [SerializeField, Range(0.05f, 1f)] private float projectileRadius = 0.22f;
        [SerializeField, Min(1f)] private float projectileRange = 32f;
        [SerializeField, Min(0f)] private float projectileDamage = 34f;
        [SerializeField, Min(0.05f)] private float projectileCooldown = 0.6f;
        [Tooltip("Ruido al lanzar: bajo, el orbe es sigiloso en origen.")]
        [SerializeField, Min(0f)] private float projectileLaunchNoise = 8f;
        [Tooltip("Ruido en el punto de impacto: los enemigos cercanos van a investigarlo.")]
        [SerializeField, Min(0f)] private float projectileImpactNoise = 14f;
        [SerializeField] private Color projectileCore = new Color(0.85f, 0.95f, 1f, 1f);
        [SerializeField] private Color projectileGlow = new Color(0.45f, 0.3f, 1f, 1f);

        [Header("Estallido de sangre (E)")]
        [SerializeField, Min(1f)] private float areaCastRange = 10f;
        [SerializeField, Min(0.5f)] private float areaRadius = 4f;
        [SerializeField, Min(0.1f)] private float areaWindup = 0.6f;
        [SerializeField, Min(0f)] private float areaDamage = 30f;
        [SerializeField, Min(0.5f)] private float areaCooldown = 8f;
        [SerializeField, Min(0f)] private float areaStaggerSeconds = 0.9f;
        [SerializeField, Min(0f)] private float areaNoise = 22f;
        [SerializeField] private Color areaColor = new Color(0.95f, 0.12f, 0.2f, 1f);

        [Header("Comun")]
        [Tooltip("Fraccion del radio de ruido que atraviesa paredes.")]
        [SerializeField, Range(0f, 1f)] private float occludedNoiseScale = 0.45f;
        [SerializeField] private LayerMask spellLayers = Physics.DefaultRaycastLayers;

        private static readonly Collider[] AreaHits = new Collider[AreaCapacity];
        private static readonly RaycastHit[] LineHits = new RaycastHit[16];
        private readonly HashSet<CombatHealth> areaVictims = new HashSet<CombatHealth>();

        private LocalPlayerShooter shooter;
        private PlayerActionModifiers actionModifiers;
        private CharacterController body;
        private Camera viewCamera;
        private float projectileReadyAt;
        private float areaReadyAt;
        private bool areaCharging;
        private float areaDetonateAt;
        private Vector3 areaCenter;
        private GameObject areaTelegraph;
        private LineRenderer areaEdge;
        private Renderer areaFill;
        private Transform areaFillTransform;

        public float ProjectileCooldown => Mathf.Max(0.05f, projectileCooldown);
        public float AreaCooldown => Mathf.Max(0.5f, areaCooldown);
        public float ProjectileCooldownRemaining => Mathf.Max(0f, projectileReadyAt - Time.time);
        public float AreaCooldownRemaining => Mathf.Max(0f, areaReadyAt - Time.time);
        public bool IsAreaCharging => areaCharging;
        public float AreaRadius => Mathf.Max(0.5f, areaRadius);
        public Vector3 PendingAreaCenter => areaCenter;
        public int ProjectilesCast { get; private set; }
        public int AreaCasts { get; private set; }
        public int LastAreaVictimCount { get; private set; }

        private void Awake()
        {
            shooter = GetComponent<LocalPlayerShooter>();
            actionModifiers = GetComponent<PlayerActionModifiers>();
            body = GetComponent<CharacterController>();
            viewCamera = Camera.main;
        }

        private void OnDisable()
        {
            // Al morir o desactivarse, un estallido pendiente no detona.
            CancelAreaCharge();
        }

        private void OnDestroy()
        {
            if (areaTelegraph != null)
                Destroy(areaTelegraph);
        }

        private void Update()
        {
            if (areaCharging)
                UpdateAreaCharge();

            if (shooter == null)
                shooter = GetComponent<LocalPlayerShooter>();
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (shooter == null || !TryReadPointer(out Vector2 pointer))
                return;

            if ((GetComponent<SurvivalInventory>() != null && SurvivalInventory.Pointer().y < 70) || SurvivalInventory.BlocksInput(gameObject) || (TryGetComponent<SurvivalBuilder>(out var builder) && builder.IsBuilding)) return;
            bool wantsProjectile = WasProjectilePressed();
            bool wantsArea = WasAreaPressed();
            if (!wantsProjectile && !wantsArea)
                return;
            if (!shooter.TryGetAimPoint(pointer, out Vector3 aimPoint))
                return;

            if (wantsProjectile)
                TryCastProjectile(aimPoint);
            if (wantsArea)
                TryCastArea(aimPoint);
        }

        /// <summary>Lanza el orbe hacia un punto del mundo si no esta en espera.</summary>
        public bool TryCastProjectile(Vector3 aimPoint)
        {
            if (!CanAct() || Time.time < projectileReadyAt || !IsFinite(aimPoint))
                return false;

            Vector3 origin = shooter != null ? shooter.ShotOriginPosition : CenterPosition();
            Vector3 direction = aimPoint - origin;
            // Camara cenital: si el cursor esta en el suelo, el orbe vuela horizontal hacia
            // ese punto en vez de clavarse en el suelo antes de llegar. Si el cursor esta
            // sobre algo mas alto que la mano (un enemigo grande, un muro), apunta directo.
            if (aimPoint.y < origin.y)
                direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = transform.forward;

            ArcaneProjectile.Launch(origin, direction.normalized, transform,
                new ArcaneProjectile.Settings
                {
                    Speed = projectileSpeed,
                    Radius = projectileRadius,
                    Range = projectileRange,
                    Damage = projectileDamage,
                    ImpactNoiseRadius = projectileImpactNoise,
                    OccludedNoiseScale = occludedNoiseScale,
                    CoreColor = projectileCore,
                    GlowColor = projectileGlow,
                    Layers = spellLayers
                });
            PrototypeEnemy.EmitNoise(origin, projectileLaunchNoise, occludedNoiseScale);
            projectileReadyAt = Time.time + ProjectileCooldown;
            ProjectilesCast++;
            return true;
        }

        /// <summary>
        /// Coloca el aviso del estallido en el suelo, limitado al alcance y sin
        /// atravesar paredes. Detona tras areaWindup segundos.
        /// </summary>
        public bool TryCastArea(Vector3 aimPoint)
        {
            if (!CanAct() || areaCharging || Time.time < areaReadyAt || !IsFinite(aimPoint))
                return false;

            Vector3 center = CenterPosition();
            Vector3 planar = aimPoint - center;
            planar.y = 0f;
            float distance = Mathf.Min(planar.magnitude, Mathf.Max(1f, areaCastRange));
            Vector3 direction = planar.sqrMagnitude > 0.0001f ? planar.normalized : Vector3.zero;
            if (direction != Vector3.zero && distance > 0f &&
                TryFirstBlocker(center, direction, distance, out float blockedAt))
            {
                // Una pared en medio: el estallido se queda de este lado.
                distance = Mathf.Max(0f, blockedAt - 0.35f);
            }
            Vector3 point = center + direction * distance;
            areaCenter = GroundAt(point);

            areaCharging = true;
            areaDetonateAt = Time.time + Mathf.Max(0.1f, areaWindup);
            areaReadyAt = Time.time + AreaCooldown;
            AreaCasts++;
            ShowAreaTelegraph();
            return true;
        }

        /// <summary>Recompensa: reduce la espera restante del estallido.</summary>
        public void ReduceAreaCooldown(float seconds)
        {
            if (IsFinite(seconds) && seconds > 0f)
                areaReadyAt = Mathf.Max(Time.time, areaReadyAt - seconds);
        }

        private void UpdateAreaCharge()
        {
            float total = Mathf.Max(0.1f, areaWindup);
            float t = 1f - Mathf.Clamp01((areaDetonateAt - Time.time) / total);
            if (areaFillTransform != null)
            {
                float diameter = AreaRadius * 2f * Mathf.Lerp(0.05f, 1f, t);
                areaFillTransform.localScale = new Vector3(diameter, 0.01f, diameter);
                Color fill = areaColor;
                fill.a = Mathf.Lerp(0.15f, 0.45f, t);
                PrototypeVfx.SetColor(areaFill, fill);
            }
            if (areaEdge != null)
            {
                // El borde parpadea mas rapido al acercarse la detonacion.
                float blink = 0.6f + 0.4f * Mathf.Sin(Time.time * Mathf.Lerp(10f, 40f, t));
                Color edge = Color.Lerp(areaColor, Color.white, 0.35f);
                edge.a = blink;
                areaEdge.startColor = edge;
                areaEdge.endColor = edge;
            }

            if (Time.time >= areaDetonateAt)
                DetonateArea();
        }

        private void DetonateArea()
        {
            areaCharging = false;
            HideAreaTelegraph();

            float radius = AreaRadius;
            Vector3 center = areaCenter;
            Vector3 blastEye = center + Vector3.up * 0.6f;
            Vector3 casterEye = CenterPosition();
            areaVictims.Clear();
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.down * 0.5f,
                center + Vector3.up * 3f, radius, AreaHits, spellLayers,
                QueryTriggerInteraction.Ignore);
            Collider[] hits = AreaHits;
            if (count == hits.Length)
            {
                hits = Physics.OverlapCapsule(center + Vector3.down * 0.5f,
                    center + Vector3.up * 3f, radius, spellLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;
                CombatHealth health = hit.GetComponentInParent<CombatHealth>();
                if (health == null || health.IsDefeated || health.transform.IsChildOf(transform) ||
                    areaVictims.Contains(health))
                    continue;

                // Mismo criterio que el anillo dibujado: el borde del collider debe
                // quedar dentro del radio en el plano.
                Vector3 closest = hit.ClosestPoint(blastEye);
                Vector3 planar = closest - center;
                planar.y = 0f;
                if (planar.sqrMagnitude > radius * radius)
                    continue;
                // La onda no atraviesa paredes: hace falta linea despejada al objetivo.
                Vector3 targetCenter = hit.bounds.center;
                if (!IsLineClear(blastEye, targetCenter, health.transform))
                    continue;
                areaVictims.Add(health);
            }

            LastAreaVictimCount = 0;
            float damage = Mathf.Max(0f, IsFinite(areaDamage) ? areaDamage : 0f);
            foreach (CombatHealth victim in areaVictims)
            {
                PrototypeEnemy enemy = victim.GetComponent<PrototypeEnemy>();
                if (enemy != null)
                {
                    // Solo delata al jugador si el enemigo podria verlo desde donde esta;
                    // si no, investiga el centro de la explosion.
                    Vector3 enemyEye = victim.transform.position + Vector3.up * 0.6f;
                    bool seesCaster = IsLineClear(enemyEye, casterEye, victim.transform);
                    enemy.ReceiveAttackOrigin(seesCaster ? transform.position : center);
                }
                if (victim.ApplyDamage(damage) > 0f)
                    LastAreaVictimCount++;
                if (enemy != null && !enemy.IsDefeated)
                    enemy.Stagger(areaStaggerSeconds);
            }

            SpellImpactFlash.Spawn(center + Vector3.up * 0.3f, areaColor, radius * 0.45f, 0.45f,
                radius);
            PrototypeEnemy.EmitNoise(center, areaNoise, occludedNoiseScale);
        }

        private void ShowAreaTelegraph()
        {
            if (areaTelegraph == null)
            {
                areaTelegraph = new GameObject("Aviso estallido");
                areaEdge = PrototypeVfx.CreateRing("Borde", areaTelegraph.transform, AreaRadius,
                    0.12f, areaColor);
                GameObject fill = PrototypeVfx.CreateShape(PrimitiveType.Cylinder, "Relleno",
                    areaTelegraph.transform, Vector3.zero, new Vector3(0.1f, 0.01f, 0.1f),
                    areaColor);
                areaFill = fill.GetComponent<Renderer>();
                areaFillTransform = fill.transform;
            }
            areaTelegraph.transform.position = areaCenter + Vector3.up * 0.04f;
            PrototypeVfx.SetRingRadius(areaEdge, AreaRadius);
            areaFillTransform.localPosition = Vector3.zero;
            areaTelegraph.SetActive(true);
            UpdateAreaCharge();
        }

        private void HideAreaTelegraph()
        {
            if (areaTelegraph != null)
                areaTelegraph.SetActive(false);
        }

        private void CancelAreaCharge()
        {
            areaCharging = false;
            HideAreaTelegraph();
        }

        private bool CanAct() =>
            isActiveAndEnabled && (actionModifiers == null || actionModifiers.AllowsAttack);

        private Vector3 CenterPosition() =>
            body != null && body.enabled ? body.bounds.center : transform.position;

        private Vector3 GroundAt(Vector3 point)
        {
            Vector3 from = point + Vector3.up * 2f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, LineHits, 8f, spellLayers,
                QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            Vector3 ground = new Vector3(point.x, 0f, point.z);
            for (int i = 0; i < count; i++)
            {
                Collider c = LineHits[i].collider;
                if (c == null || c.transform == transform || c.transform.IsChildOf(transform) ||
                    c.GetComponentInParent<CombatHealth>() != null)
                    continue;
                if (LineHits[i].distance < best)
                {
                    best = LineHits[i].distance;
                    ground = LineHits[i].point;
                }
            }
            return ground;
        }

        /// <summary>Primer obstaculo estatico (sin CombatHealth) en la linea dada.</summary>
        private bool TryFirstBlocker(Vector3 origin, Vector3 direction, float distance,
            out float blockedAt)
        {
            blockedAt = distance;
            int count = Physics.RaycastNonAlloc(origin, direction, LineHits, distance,
                spellLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider c = LineHits[i].collider;
                if (c == null || c.transform == transform || c.transform.IsChildOf(transform) ||
                    c.GetComponentInParent<CombatHealth>() != null)
                    continue;
                if (LineHits[i].distance < blockedAt)
                {
                    blockedAt = LineHits[i].distance;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Linea despejada entre dos puntos ignorando al jugador y al objetivo indicado.
        /// Otros cuerpos con vida no bloquean (la onda alcanza a varios enemigos juntos).
        /// </summary>
        private bool IsLineClear(Vector3 from, Vector3 to, Transform ignoredTarget)
        {
            Vector3 ray = to - from;
            float distance = ray.magnitude;
            if (distance <= 0.001f)
                return true;
            int count = Physics.RaycastNonAlloc(from, ray / distance, LineHits, distance,
                spellLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = LineHits;
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(from, ray / distance, distance, spellLayers,
                    QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (c == null)
                    continue;
                Transform t = c.transform;
                if (t == transform || t.IsChildOf(transform) ||
                    ignoredTarget != null && (t == ignoredTarget || t.IsChildOf(ignoredTarget)) ||
                    c.GetComponentInParent<CombatHealth>() != null)
                    continue;
                return false;
            }
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool WasProjectilePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        private static bool WasAreaPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        private static bool TryReadPointer(out Vector2 position)
        {
            position = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
                return false;
            position = Mouse.current.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
#else
            return false;
#endif
            return position.x >= 0f && position.y >= 0f &&
                position.x < Screen.width && position.y < Screen.height;
        }
    }
}
