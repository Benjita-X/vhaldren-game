using UnityEngine;
using Vhaldren.Gameplay.Combat;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Prototipo local de disparo hitscan. Apunta al primer punto del mundo bajo el cursor;
    /// el proyectil de prueba aplica dano local al primer objetivo valido.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalPlayerShooter : MonoBehaviour
    {
        private const int AimHitCapacity = 32;
        private const int MaxAimHitCapacity = 256;

        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform shotOrigin;
        [SerializeField] private Transform aimMarker;
        [SerializeField, Min(1f)] private float maxRange = 100f;
        [Tooltip("Dano de prueba. No es un ataque validado por servidor.")]
        [SerializeField, Min(0f)] private float testDamage = 25f;
        [SerializeField] private LayerMask aimLayers = Physics.DefaultRaycastLayers;
        [Header("Arma visible — modo mundo")]
        private const float AimSurfaceMargin = 0.35f;
        [SerializeField] private bool usePhysicalArrows;
        [SerializeField, Min(1f)] private float arrowSpeed = 27f;
        [SerializeField, Min(0f)] private float arrowGravity = 0.16f;
        [SerializeField, Min(0.05f)] private float arrowCooldown = 0.38f;

        private float nextArrowAt;
        private const int ShotBufferCapacity = 2;
        private readonly Vector3[] bufferedAimPoints = new Vector3[ShotBufferCapacity];
        private readonly float[] bufferedUntil = new float[ShotBufferCapacity];
        private int bufferedCount;
#if ENABLE_INPUT_SYSTEM
        private InputAction fireAction;
#endif

        private RaycastHit[] aimHits = new RaycastHit[AimHitCapacity];
        private LineRenderer tracer;
        private PlayerActionModifiers actionModifiers;
        private PlayerDamageTracker damageTracker;
        private Material tracerMaterial;
        private float tracerTimeRemaining;

        public Vector3 LastAimPoint { get; private set; }
        public Vector3 LastImpactPoint { get; private set; }
        public int ShotsFired { get; private set; }
        public int HitsLanded { get; private set; }
        /// <summary>Suma de dano realmente descontado de CombatHealth, sin overkill.</summary>
        public float TotalDamageDealt { get; private set; }
        public float LastDamageDealt { get; private set; }
        public bool UsesPhysicalArrows => usePhysicalArrows;
        public int BufferedShots => bufferedCount;
        public int RejectedShotRequests { get; private set; }

        /// <summary>Activar flechas en el mundo sin cambiar el disparo legacy de tests.</summary>
        public void SetPhysicalArrows(bool enabled)
        {
            usePhysicalArrows = enabled;
            if (!enabled)
                bufferedCount = 0;
            if (!Application.isPlaying || shotOrigin == null)
                return;

            CrossbowVisual model = GetComponent<CrossbowVisual>();
            if (enabled)
            {
                if (model == null)
                    model = gameObject.AddComponent<CrossbowVisual>();
                model.Attach(shotOrigin);
            }
            else if (model != null)
                model.Hide();
        }

        /// <summary>Respuesta diferida cuando la flecha impacta de verdad.</summary>
        public void RegisterProjectileImpact(Vector3 impactPoint, float appliedDamage)
        {
            LastImpactPoint = impactPoint;
            if (!float.IsNaN(appliedDamage) && !float.IsInfinity(appliedDamage) &&
                appliedDamage > 0f)
            {
                HitsLanded++;
                LastDamageDealt = appliedDamage;
                TotalDamageDealt += appliedDamage;
                if (damageTracker == null)
                    damageTracker = GetComponent<PlayerDamageTracker>();
                if (damageTracker != null)
                    damageTracker.Record(appliedDamage, CombatDamageKind.Arrow);
            }
        }

        private void Awake()
        {
            actionModifiers = GetComponent<PlayerActionModifiers>();
            damageTracker = GetComponent<PlayerDamageTracker>();
            if (viewCamera == null)
                viewCamera = Camera.main;

            tracer = GetComponent<LineRenderer>();
            if (tracer == null)
                tracer = gameObject.AddComponent<LineRenderer>();

            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.startWidth = 0.045f;
            tracer.endWidth = 0.012f;
            tracer.startColor = new Color(1f, 0.82f, 0.35f, 1f);
            tracer.endColor = new Color(1f, 0.25f, 0.12f, 0.8f);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                tracerMaterial = new Material(shader);
                tracer.sharedMaterial = tracerMaterial;
            }
            tracer.enabled = false;
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            // InputAction recibe cada evento performed, incluso si hay
            // varios clics entre dos Update. Se evita perder el segundo clic
            // por depender solo de wasPressedThisFrame.
            if (fireAction == null)
            {
                fireAction = new InputAction("CrossbowFire", InputActionType.Button,
                    "<Mouse>/leftButton");
                fireAction.performed += OnFirePerformed;
            }
            fireAction.Enable();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void OnFirePerformed(InputAction.CallbackContext context)
        {
            if (Mouse.current != null)
                RequestShot(Mouse.current.position.ReadValue());
        }
#endif

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (fireAction != null)
                fireAction.Disable();
#endif
            bufferedCount = 0;
            if (tracer != null)
                tracer.enabled = false;
            tracerTimeRemaining = 0f;
        }

        private void OnDestroy()
        {
#if ENABLE_INPUT_SYSTEM
            if (fireAction != null)
            {
                fireAction.performed -= OnFirePerformed;
                fireAction.Dispose();
                fireAction = null;
            }
#endif
            if (tracerMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(tracerMaterial);
                else
                    DestroyImmediate(tracerMaterial);
            }
        }

        private void Update()
        {
            ProcessBufferedShot();
            if (viewCamera == null)
                viewCamera = Camera.main;

            if (tracerTimeRemaining > 0f)
            {
                tracerTimeRemaining -= Time.deltaTime;
                if (tracerTimeRemaining <= 0f)
                    tracer.enabled = false;
            }

            if (viewCamera == null || !TryReadPointer(out Vector2 pointer))
                return;

            if (TryGetAimPoint(pointer, out Vector3 aimPoint) && aimMarker != null)
                aimMarker.position = aimPoint + Vector3.up * 0.025f;

#if !ENABLE_INPUT_SYSTEM
            if (WasFirePressed())
                RequestShot(pointer);
#endif
        }

        /// <summary>Resuelve el punto exacto del mundo que atraviesa el pixel indicado.</summary>
        public bool TryGetAimPoint(Vector2 screenPosition, out Vector3 aimPoint)
        {
            aimPoint = Vector3.zero;
            if (viewCamera == null || !IsInsideGameViewport(screenPosition))
                return false;

            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            if (TryNearestWorldHit(ray, SafeMaxRange(), out Vector3 worldPoint))
            {
                aimPoint = worldPoint;
                return true;
            }

            // Si el cursor cae en cielo o fuera del suelo, conserva un punto util delante del jugador.
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float distance) && distance > 0f && distance <= SafeMaxRange())
            {
                aimPoint = ray.GetPoint(distance);
                return true;
            }

            aimPoint = ray.origin + ray.direction * SafeMaxRange();
            return true;
        }

        /// <summary>
        /// Entrada del jugador con buffer de dos disparos. La mira se guarda
        /// en coordenadas del mundo en el instante del clic. Cada peticion
        /// aceptada conserva su propio impacto y crea su propio proyectil.
        /// </summary>
        public bool RequestShot(Vector2 screenPosition)
        {
            if (actionModifiers != null && !actionModifiers.AllowsAttack)
            {
                RejectedShotRequests++;
                return false;
            }
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (!TryGetAimPoint(screenPosition, out Vector3 aimPoint))
            {
                RejectedShotRequests++;
                return false;
            }

            if (!usePhysicalArrows || (bufferedCount == 0 && Time.time >= nextArrowAt))
                return FireAtAimPoint(aimPoint);

            if (bufferedCount >= ShotBufferCapacity)
            {
                RejectedShotRequests++;
                return false;
            }

            bufferedAimPoints[bufferedCount] = aimPoint;
            bufferedUntil[bufferedCount] = Time.time +
                Mathf.Max(0.8f, Mathf.Max(0.05f, arrowCooldown) * 3.5f);
            bufferedCount++;
            return true;
        }

        private void ProcessBufferedShot()
        {
            if (bufferedCount <= 0 || Time.time < nextArrowAt)
                return;
            if (!usePhysicalArrows || (actionModifiers != null &&
                !actionModifiers.AllowsAttack))
            {
                RejectedShotRequests += bufferedCount;
                bufferedCount = 0;
                return;
            }

            // Cada Update procesa como maximo un disparo; respetar su propia
            // recarga y nunca juntar dos peticiones en el mismo proyectil.
            Vector3 aimPoint = bufferedAimPoints[0];
            float expiry = bufferedUntil[0];
            for (int i = 1; i < bufferedCount; i++)
            {
                bufferedAimPoints[i - 1] = bufferedAimPoints[i];
                bufferedUntil[i - 1] = bufferedUntil[i];
            }
            bufferedCount--;
            if (Time.time > expiry || !FireAtAimPoint(aimPoint))
                RejectedShotRequests++;
        }

        /// <summary>Dispara hacia el punto resuelto por la camara.</summary>
        public bool FireAtScreenPosition(Vector2 screenPosition)
        {
            if (actionModifiers != null && !actionModifiers.AllowsAttack)
                return false;
            if (!TryGetAimPoint(screenPosition, out Vector3 aimPoint))
                return false;
            return FireAtAimPoint(aimPoint);
        }

        private bool FireAtAimPoint(Vector3 aimPoint)
        {
            if (actionModifiers != null && !actionModifiers.AllowsAttack)
                return false;

            Vector3 origin = shotOrigin != null
                ? shotOrigin.position
                : transform.position + Vector3.up * 0.65f;
            Vector3 direction = aimPoint - origin;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;

            direction.Normalize();
            // El punto apuntado esta justo en la superficie del objetivo: sin un margen,
            // el rayo puede quedarse a milimetros y no tocarlo (fallo por precision).
            float range = Mathf.Min(SafeMaxRange(), Vector3.Distance(origin, aimPoint) + AimSurfaceMargin);
            if (range <= 0f)
                return false;

            if (usePhysicalArrows)
            {
                if (Time.time < nextArrowAt)
                    return false;

                float safeDamage = float.IsNaN(testDamage) || float.IsInfinity(testDamage)
                    ? 0f : Mathf.Max(0f, testDamage);
                PhysicalArrowProjectile arrow = PhysicalArrowProjectile.Spawn(
                    transform, origin, direction, safeDamage,
                    Mathf.Max(1f, arrowSpeed), SafeMaxRange(), this,
                    Mathf.Max(0f, arrowGravity));
                if (arrow == null)
                    return false;
                if (GameFeel.Enabled)
                    GameAudio.Play(GameSound.BowString, origin, 0.7f);

                nextArrowAt = Time.time + Mathf.Max(0.05f, arrowCooldown);
                LastAimPoint = aimPoint;
                LastImpactPoint = origin;
                ShotsFired++;
                // Nada de tracer instantaneo: se ve el cuerpo de la flecha real.
                return true;
            }

            LastAimPoint = aimPoint;
            LastImpactPoint = aimPoint;
            // Se ignora el propio cuerpo sin dejar de buscar: si el primer impacto era el
            // jugador (al apuntar hacia atras mientras el modelo aun gira), el tiro
            // atravesaba la pared que hubiera detras.
            if (TryNearestWorldHit(new Ray(origin, direction), range,
                out Vector3 obstaclePoint, out Collider hitCollider))
            {
                LastImpactPoint = obstaclePoint;
                // La logica de vida queda en el objetivo, no en el motor del jugador.
                // En cooperativo el servidor debera validar este impacto.
                if (hitCollider != null)
                {
                    CombatHealth health = hitCollider.GetComponentInParent<CombatHealth>();
                    float safeDamage = float.IsNaN(testDamage) || float.IsInfinity(testDamage)
                        ? 0f : Mathf.Max(0f, testDamage);
                    if (health != null && !health.transform.IsChildOf(transform))
                    {
                        float applied = health.ApplyDamage(safeDamage);
                        if (applied > 0f)
                        {
                            HitsLanded++;
                            LastDamageDealt = applied;
                            TotalDamageDealt += applied;
                            if (damageTracker == null)
                                damageTracker = GetComponent<PlayerDamageTracker>();
                            if (damageTracker != null)
                                damageTracker.Record(applied, CombatDamageKind.Arrow);
                        }
                    }
                }
            }

            ShotsFired++;
            ShowTracer(origin, LastImpactPoint);
            return true;
        }

        /// <summary>Primer impacto del rayo que no pertenece al propio jugador.</summary>
        private bool TryNearestWorldHit(Ray ray, float range, out Vector3 point)
        {
            return TryNearestWorldHit(ray, range, out point, out _);
        }

        private bool TryNearestWorldHit(Ray ray, float range, out Vector3 point,
            out Collider closestCollider)
        {
            point = Vector3.zero;
            closestCollider = null;
            int hitCount = Physics.RaycastNonAlloc(ray, aimHits, range, aimLayers,
                QueryTriggerInteraction.Ignore);
            // RaycastNonAlloc no garantiza orden ni exhaustividad al llenar el buffer.
            // Ampliar una sola vez el buffer reutilizable antes de recurrir a RaycastAll.
            while (hitCount == aimHits.Length && aimHits.Length < MaxAimHitCapacity)
            {
                System.Array.Resize(ref aimHits, aimHits.Length * 2);
                hitCount = Physics.RaycastNonAlloc(ray, aimHits, range, aimLayers,
                    QueryTriggerInteraction.Ignore);
            }
            RaycastHit[] hits = aimHits;
            if (hitCount == hits.Length)
            {
                // Limitar el buffer persistente y garantizar exhaustividad incluso
                // si hay mas de 256 colliders; solo en este caso se asigna memoria.
                hits = Physics.RaycastAll(ray, range, aimLayers, QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
            }

            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform == transform ||
                    hitCollider.transform.IsChildOf(transform))
                    continue;

                if (hits[i].distance < nearestDistance)
                {
                    nearestDistance = hits[i].distance;
                    point = hits[i].point;
                    closestCollider = hitCollider;
                    found = true;
                }
            }
            return found;
        }

        private void ShowTracer(Vector3 origin, Vector3 end)
        {
            tracer.SetPosition(0, origin);
            tracer.SetPosition(1, end);
            tracer.enabled = true;
            tracerTimeRemaining = 0.2f;
        }

        private float SafeMaxRange() => Mathf.Max(1f, maxRange);

        private static bool WasFirePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
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
            return IsInsideGameViewport(position);
        }

        private static bool IsInsideGameViewport(Vector2 position) =>
            position.x >= 0f && position.y >= 0f && position.x < Screen.width && position.y < Screen.height;
    }
}
