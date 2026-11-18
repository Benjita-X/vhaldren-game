using UnityEngine;

namespace Vhaldren.Gameplay.CameraSystem
{
    /// <summary>
    /// Camara local que sigue un objetivo sin depender del motor, los dispositivos ni
    /// el estado de red: la rueda, el giro y el cursor le llegan desde fuera
    /// (<c>LocalPlayerInput</c>).
    /// Por defecto es la camara fija de las salas de prueba. El mundo jugable la
    /// convierte en camara cenital de accion con <see cref="ConfigureActionView"/>:
    /// perspectiva inclinada con FOV estrecho, zoom sobre la misma linea, giro
    /// opcional alrededor del personaje y adelanto opcional hacia el cursor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class SimpleFollowCamera : MonoBehaviour
    {
        private static readonly Vector3 DefaultOffset = new Vector3(0f, 10f, -10f);
        private static readonly Vector3 DefaultLookOffset = new Vector3(0f, 0.5f, 0f);

        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField, Min(0f)] private float followSharpness = 12f;
        [Tooltip("Metros que el seguimiento suave puede quedarse atras como maximo (teleportes, esquivas).")]
        [SerializeField, Min(0f)] private float maxFollowLag = 1.5f;

        [Header("Zoom")]
        [Tooltip("Distancia minima, como fraccion de la distancia normal (1 = sin zoom).")]
        [SerializeField, Min(0.1f)] private float minZoom = 0.6f;
        [Tooltip("Distancia maxima, como fraccion de la distancia normal.")]
        [SerializeField, Min(0.1f)] private float maxZoom = 1.5f;
        [SerializeField, Min(0f)] private float zoomSharpness = 14f;

        [Header("Giro alrededor del personaje (opcional)")]
        [Tooltip("Desactivado: angulo fijo, como en las salas de prueba.")]
        [SerializeField] private bool allowRotation;
        [Tooltip("Rapidez con la que el giro alcanza el angulo pedido. 0 = inmediato.")]
        [SerializeField, Min(0f)] private float rotationSharpness = 16f;

        [Header("Adelanto hacia el cursor (opcional)")]
        [SerializeField] private bool lookAhead;
        [Tooltip("Metros que se desplaza el encuadre, como maximo, hacia donde esta el cursor.")]
        [SerializeField, Min(0f)] private float lookAheadDistance = 1.8f;
        [Tooltip("Rapidez del desplazamiento: baja para que el encuadre no maree.")]
        [SerializeField, Min(0f)] private float lookAheadSharpness = 3f;
        [Tooltip("Zona central (en medias alturas de pantalla) donde el cursor no adelanta la camara.")]
        [SerializeField, Range(0f, 0.9f)] private float lookAheadDeadZone = 0.15f;

        [Header("Obstaculos")]
        [Tooltip("Acerca temporalmente la camara si una pared tapa al personaje.")]
        [SerializeField] private bool avoidObstacles = true;
        [SerializeField, Min(0.01f)] private float obstructionRadius = 0.25f;
        [SerializeField, Min(0f)] private float obstructionPadding = 0.1f;
        [SerializeField] private LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
        [Tooltip("Solo el terreno acerca la camara. Arboles, muros y rocas se ocultan con " +
                 "CameraOcclusionHider en vez de acercarla (mundo jugable).")]
        [SerializeField] private bool obstructionTerrainOnly;

        [Header("Distancia extra (monturas y vuelo)")]
        [Tooltip("Rapidez con la que la camara se aleja o se acerca al cambiar el multiplicador de distancia.")]
        [SerializeField, Min(0f)] private float distanceMultiplierSharpness = 1.6f;

        private readonly RaycastHit[] obstructionHits = new RaycastHit[16];
        private float targetZoom = 1f;
        private float currentZoom = 1f;
        private float targetYaw;
        private float currentYaw;
        private Vector2 lookAheadInput;
        private Vector3 currentLookAhead;
        private Vector3 followFocus;
        private bool hasFollowFocus;
        // Distancia libre que deja la pared mas cercana; infinito = sin pared.
        private float obstructionLimit = float.PositiveInfinity;
        // Alejamiento extra por encima del zoom (1 = sin cambio). Lo piden monturas y vuelo.
        private float targetDistanceMultiplier = 1f;
        private float currentDistanceMultiplier = 1f;

        public Transform Target => target;
        /// <summary>Zoom aplicado ahora mismo: 1 es la distancia normal, menos es mas cerca.</summary>
        public float Zoom => currentZoom;
        public float TargetZoom => targetZoom;
        /// <summary>Distancia normal (zoom 1) entre el punto que mira y la camara, en metros.</summary>
        public float DefaultDistance => (SafeOffset() - SafeLookOffset()).magnitude;
        /// <summary>Distancia actual al punto que mira, con zoom y sin contar paredes, en metros.</summary>
        public float Distance => DefaultDistance * (IsFinite(currentZoom) && currentZoom > 0f ? currentZoom : 1f);
        public float MinDistance => DefaultDistance * LowZoom();
        public float MaxDistance => DefaultDistance * HighZoom();

        /// <summary>Inclinacion hacia abajo de la vista, en grados. El giro y el zoom no la cambian.</summary>
        public float PitchDegrees
        {
            get
            {
                Vector3 relative = SafeOffset() - SafeLookOffset();
                float planar = new Vector2(relative.x, relative.z).magnitude;
                return Mathf.Atan2(relative.y, planar) * Mathf.Rad2Deg;
            }
        }

        /// <summary>Si es falso, el giro del jugador se ignora y la camara vuelve al angulo por defecto.</summary>
        public bool AllowRotation
        {
            get => allowRotation;
            set
            {
                allowRotation = value;
                if (!value)
                    ResetYaw();
            }
        }

        /// <summary>Giro horizontal aplicado respecto al angulo por defecto, en grados (-180..180).</summary>
        public float Yaw => currentYaw;
        public float TargetYaw => targetYaw;

        public bool LookAheadEnabled
        {
            get => lookAhead;
            set
            {
                lookAhead = value;
                if (!value)
                    lookAheadInput = Vector2.zero;
            }
        }

        /// <summary>Desplazamiento actual del encuadre hacia el cursor (cero si esta desactivado).</summary>
        public Vector3 LookAheadOffset => currentLookAhead;

        /// <summary>
        /// Verdadero: solo el terreno acerca la camara (nunca lo atraviesa); el resto de
        /// obstaculos los oculta <see cref="CameraOcclusionHider"/>. Falso: cualquier pared.
        /// </summary>
        public bool ObstructionTerrainOnly
        {
            get => obstructionTerrainOnly;
            set => obstructionTerrainOnly = value;
        }

        /// <summary>Multiplicador de distancia aplicado ahora mismo (1 = normal). No cambia el zoom del jugador.</summary>
        public float DistanceMultiplier => currentDistanceMultiplier;
        public float TargetDistanceMultiplier => targetDistanceMultiplier;

        /// <summary>
        /// Aleja (mayor que 1) o acerca la camara sobre la misma linea, por encima del zoom.
        /// Lo usan las monturas y el vuelo; vuelve a 1 con suavidad al terminar.
        /// </summary>
        public void SetDistanceMultiplier(float value, bool immediate = false)
        {
            if (!IsFinite(value))
                return;
            targetDistanceMultiplier = Mathf.Clamp(value, 0.5f, 3f);
            if (immediate)
                currentDistanceMultiplier = targetDistanceMultiplier;
        }

        /// <summary>Acerca (negativo) o aleja (positivo) la camara dentro de sus limites.</summary>
        public void AddZoom(float delta)
        {
            if (IsFinite(delta))
                SetZoom(targetZoom + delta, false);
        }

        public void SetZoom(float value, bool immediate)
        {
            if (!IsFinite(value))
                return;

            targetZoom = Mathf.Clamp(value, LowZoom(), HighZoom());
            if (immediate)
                currentZoom = targetZoom;
        }

        /// <summary>Giro pedido por el jugador. Sin efecto si el giro no esta permitido.</summary>
        public void AddYaw(float degrees)
        {
            if (allowRotation && IsFinite(degrees))
                targetYaw = WrapAngle(targetYaw + degrees);
        }

        /// <summary>Fija el giro horizontal (por codigo: pruebas, capturas). Inmediato o suave.</summary>
        public void SetYaw(float degrees, bool immediate)
        {
            if (!IsFinite(degrees))
                return;
            targetYaw = WrapAngle(degrees);
            if (immediate)
                SnapToTarget();
        }

        /// <summary>Vuelve con suavidad al angulo por defecto.</summary>
        public void ResetYaw() => SetYaw(0f, false);

        /// <summary>
        /// Posicion del cursor respecto al centro de la pantalla, en medias alturas
        /// (ver <see cref="LookAheadFor"/>). Solo se usa con el adelanto activado.
        /// </summary>
        public void SetLookAheadInput(Vector2 screenOffset)
        {
            lookAheadInput = lookAhead && IsFinite(screenOffset) ? screenOffset : Vector2.zero;
        }

        /// <summary>
        /// Encuadre cenital de accion: inclinacion hacia abajo, distancia normal y limites del
        /// zoom en metros, y campo de vision vertical. El zoom acerca sobre la misma linea.
        /// Si cambia el encuadre, el zoom vuelve a la nueva distancia normal (al instante o
        /// suavemente, sin saltos); si no cambia, conserva el zoom que eligio el jugador.
        /// </summary>
        public void ConfigureActionView(float pitchDegrees, float defaultDistance,
            float minDistance, float maxDistance, float verticalFieldOfView, bool immediate)
        {
            if (!IsFinite(pitchDegrees) || !IsFinite(defaultDistance) ||
                !IsFinite(minDistance) || !IsFinite(maxDistance))
                return;

            float distance = Mathf.Max(1f, defaultDistance);
            float pitch = Mathf.Clamp(pitchDegrees, 5f, 89f) * Mathf.Deg2Rad;
            Vector3 safeLook = SafeLookOffset();
            float distanceSeenNow = Distance;
            Vector3 nextOffset = safeLook +
                new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch)) * distance;
            bool reframed = (nextOffset - SafeOffset()).sqrMagnitude > 0.0001f;

            offset = nextOffset;
            lookOffset = safeLook;
            minZoom = Mathf.Max(0.1f, Mathf.Min(minDistance, distance) / distance);
            maxZoom = Mathf.Max(minZoom, Mathf.Max(maxDistance, distance) / distance);

            if (reframed)
            {
                targetZoom = 1f;
                currentZoom = immediate ? 1f : Mathf.Clamp(distanceSeenNow / distance, 0.05f, 20f);
            }
            SetZoom(targetZoom, immediate);

            Camera view = GetComponent<Camera>();
            if (view != null && IsFinite(verticalFieldOfView) && verticalFieldOfView > 0f)
                view.fieldOfView = Mathf.Clamp(verticalFieldOfView, 10f, 120f);
            if (immediate)
                SnapToTarget();
        }

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
            SnapToTarget();
        }

        /// <summary>
        /// Desplazamiento del encuadre hacia el cursor, sobre el suelo. <paramref name="screenOffset"/>
        /// es la posicion del cursor respecto al centro de la pantalla en medias alturas
        /// (arriba = hacia donde mira la camara). Dentro de la zona muerta no hay adelanto;
        /// fuera crece hasta <paramref name="maxDistance"/> en el borde de media altura.
        /// </summary>
        public static Vector3 LookAheadFor(Vector2 screenOffset, float yawDegrees, float maxDistance, float deadZone)
        {
            if (!IsFinite(screenOffset) || !IsFinite(yawDegrees) || !IsFinite(maxDistance) || maxDistance <= 0f)
                return Vector3.zero;
            float magnitude = screenOffset.magnitude;
            float zone = IsFinite(deadZone) ? Mathf.Clamp(deadZone, 0f, 0.9f) : 0f;
            if (!IsFinite(magnitude) || magnitude <= zone || magnitude <= 0.0001f)
                return Vector3.zero;

            float strength = Mathf.Clamp01((magnitude - zone) / (1f - zone));
            Vector2 direction = screenOffset / magnitude;
            Vector3 planar = new Vector3(direction.x, 0f, direction.y);
            if (yawDegrees != 0f)
                planar = Quaternion.Euler(0f, yawDegrees, 0f) * planar;
            return planar * (strength * maxDistance);
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            float deltaTime = Time.deltaTime;
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Blend(zoomSharpness, deltaTime));
            if (currentDistanceMultiplier != targetDistanceMultiplier)
            {
                currentDistanceMultiplier = Mathf.Lerp(currentDistanceMultiplier, targetDistanceMultiplier,
                    Blend(distanceMultiplierSharpness, deltaTime));
                if (!IsFinite(currentDistanceMultiplier) ||
                    Mathf.Abs(currentDistanceMultiplier - targetDistanceMultiplier) < 0.0005f)
                    currentDistanceMultiplier = targetDistanceMultiplier;
            }
            StepYaw(Blend(rotationSharpness, deltaTime));

            Vector3 focus = target.position + SafeLookOffset();
            if (!IsFinite(focus))
                return;

            Vector3 lookAheadGoal = lookAhead
                ? LookAheadFor(lookAheadInput, currentYaw, lookAheadDistance, lookAheadDeadZone)
                : Vector3.zero;
            currentLookAhead = Vector3.Lerp(currentLookAhead, lookAheadGoal, Blend(lookAheadSharpness, deltaTime));
            if (!IsFinite(currentLookAhead))
                currentLookAhead = Vector3.zero;

            // Se suaviza el punto que se sigue, no la posicion: asi el giro orbita alrededor
            // del personaje y la vista nunca deja de mirarlo con el angulo fijado.
            // Sharpness = 0 significa seguimiento inmediato, nunca camara congelada.
            Vector3 goal = focus + currentLookAhead;
            float followBlend = Blend(followSharpness, deltaTime);
            if (!hasFollowFocus || !IsFinite(followFocus) || followBlend >= 1f)
            {
                followFocus = goal;
            }
            else
            {
                followFocus = Vector3.Lerp(followFocus, goal, followBlend);
                // Retraso acotado: tras un teleporte la camara no se queda atras y su linea de
                // vista no se aleja de la del personaje (no roza muros bajos por un angulo raso).
                Vector3 lag = followFocus - goal;
                float maxLag = IsFinite(maxFollowLag) ? Mathf.Max(0f, maxFollowLag) : 1.5f;
                if (lag.sqrMagnitude > maxLag * maxLag)
                    followFocus = goal + lag.normalized * maxLag;
            }
            hasFollowFocus = true;

            Vector3 relative = ViewOffset();
            Vector3 desiredPosition = followFocus + relative;
            if (!IsFinite(desiredPosition))
                return;
            // Las paredes se buscan en la linea ideal personaje-camara (sin el retraso del
            // suavizado); si alguna tapa, la camara se pone en esa linea, delante de ella.
            Vector3 limited = AvoidObstacles(focus, focus + relative, false);
            transform.position = float.IsPositiveInfinity(obstructionLimit) ? desiredPosition : limited;
            KeepFixedAngle(relative);
            // Temblor de impactos (game feel): se suma cada frame sin desplazar el seguimiento.
            transform.position += Vhaldren.Gameplay.Combat.GameFeel.CameraOffset;
        }

        private void StepYaw(float blend)
        {
            float remaining = Mathf.DeltaAngle(currentYaw, targetYaw);
            if (!IsFinite(remaining) || blend >= 1f || Mathf.Abs(remaining) < 0.01f)
                currentYaw = targetYaw;
            else
                currentYaw = WrapAngle(currentYaw + remaining * blend);
        }

        /// <summary>
        /// Vector del punto que mira a la camara: desplazamiento con zoom (sobre la misma
        /// linea, sin cambiar el angulo) y girado alrededor del eje vertical.
        /// </summary>
        private Vector3 ViewOffset()
        {
            float zoom = IsFinite(currentZoom) && currentZoom > 0f ? currentZoom : 1f;
            if (currentDistanceMultiplier != 1f && IsFinite(currentDistanceMultiplier) && currentDistanceMultiplier > 0f)
                zoom *= currentDistanceMultiplier;
            Vector3 relative = (SafeOffset() - SafeLookOffset()) * zoom;
            if (currentYaw != 0f && IsFinite(currentYaw))
                relative = Quaternion.Euler(0f, currentYaw, 0f) * relative;
            return relative;
        }

        private void KeepFixedAngle(Vector3 relative)
        {
            // La orientacion no depende del retraso del seguimiento.
            Vector3 forward = -relative;
            if (!IsFinite(forward))
                return;

            float largestComponent = Mathf.Max(
                Mathf.Abs(forward.x), Mathf.Max(Mathf.Abs(forward.y), Mathf.Abs(forward.z)));
            if (largestComponent <= 0f)
                return;

            forward /= largestComponent;
            float squareMagnitude = forward.sqrMagnitude;
            if (IsFinite(squareMagnitude) && squareMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(forward / Mathf.Sqrt(squareMagnitude), Vector3.up);
        }

        private void SnapToTarget()
        {
            currentZoom = targetZoom;
            currentYaw = targetYaw;
            currentDistanceMultiplier = targetDistanceMultiplier;
            if (target == null)
                return;

            Vector3 focus = target.position + SafeLookOffset();
            if (!IsFinite(focus))
                return;
            currentLookAhead = lookAhead
                ? LookAheadFor(lookAheadInput, currentYaw, lookAheadDistance, lookAheadDeadZone)
                : Vector3.zero;
            followFocus = focus + currentLookAhead;
            hasFollowFocus = true;

            Vector3 relative = ViewOffset();
            Vector3 desiredPosition = followFocus + relative;
            if (!IsFinite(desiredPosition))
                return;

            Vector3 limited = AvoidObstacles(focus, focus + relative, true);
            transform.position = float.IsPositiveInfinity(obstructionLimit) ? desiredPosition : limited;
            KeepFixedAngle(relative);
        }

        /// <summary>
        /// Una pared entre el personaje y la camara la acerca al instante, nunca la atraviesa.
        /// Cuando desaparece, la distancia se recupera con el seguimiento habitual.
        /// </summary>
        private Vector3 AvoidObstacles(Vector3 focus, Vector3 desiredPosition, bool immediate)
        {
            if (!avoidObstacles)
            {
                obstructionLimit = float.PositiveInfinity;
                return desiredPosition;
            }

            Vector3 offsetToCamera = desiredPosition - focus;
            float distance = offsetToCamera.magnitude;
            if (!IsFinite(distance) || distance <= 0.001f)
                return desiredPosition;

            Vector3 direction = offsetToCamera / distance;
            float radius = IsFinite(obstructionRadius) ? Mathf.Max(0.01f, obstructionRadius) : 0.25f;
            float allowed = float.PositiveInfinity;
            if (TryNearestObstruction(focus, radius, direction, distance, out float hitDistance))
            {
                float padding = IsFinite(obstructionPadding) ? Mathf.Max(0f, obstructionPadding) : 0.1f;
                allowed = Mathf.Max(0f, hitDistance - padding);
            }

            if (immediate || allowed <= obstructionLimit)
            {
                obstructionLimit = allowed;
            }
            else
            {
                obstructionLimit = Mathf.Lerp(obstructionLimit, Mathf.Min(allowed, distance),
                    Blend(followSharpness, Time.deltaTime));
                if (float.IsPositiveInfinity(allowed) && obstructionLimit >= distance - 0.01f)
                    obstructionLimit = float.PositiveInfinity;
            }

            return obstructionLimit >= distance
                ? desiredPosition
                : focus + direction * obstructionLimit;
        }

        private bool TryNearestObstruction(Vector3 focus, float radius, Vector3 direction, float distance,
            out float hitDistance)
        {
            hitDistance = 0f;
            if (!obstructionTerrainOnly)
            {
                if (!Physics.SphereCast(focus, radius, direction, out RaycastHit hit, distance,
                    obstructionLayers, QueryTriggerInteraction.Ignore))
                    return false;
                hitDistance = hit.distance;
                return true;
            }

            int count = Physics.SphereCastNonAlloc(focus, radius, direction, obstructionHits, distance,
                obstructionLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = obstructionHits;
            if (count >= obstructionHits.Length)
            {
                hits = Physics.SphereCastAll(focus, radius, direction, distance,
                    obstructionLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (hits[i].collider is TerrainCollider && hits[i].distance < nearest)
                    nearest = hits[i].distance;
            if (float.IsPositiveInfinity(nearest))
                return false;
            hitDistance = nearest;
            return true;
        }

        private float LowZoom() => IsFinite(minZoom) ? Mathf.Max(0.1f, minZoom) : 0.6f;

        private float HighZoom()
        {
            float low = LowZoom();
            return IsFinite(maxZoom) ? Mathf.Max(low, maxZoom) : Mathf.Max(low, 1.5f);
        }

        private Vector3 SafeOffset() => IsFinite(offset) ? offset : DefaultOffset;

        private Vector3 SafeLookOffset() => IsFinite(lookOffset) ? lookOffset : DefaultLookOffset;

        private static float Blend(float sharpness, float deltaTime)
        {
            float safe = IsFinite(sharpness) ? Mathf.Max(0f, sharpness) : 0f;
            return safe <= 0f ? 1f : 1f - Mathf.Exp(-safe * deltaTime);
        }

        private static float WrapAngle(float degrees) => Mathf.Repeat(degrees + 180f, 360f) - 180f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
