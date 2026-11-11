using UnityEngine;

namespace Vhaldren.Gameplay.CameraSystem
{
    /// <summary>
    /// Camara local de prueba. Sigue un objetivo sin depender del motor,
    /// los dispositivos ni el estado de red.
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

        [Header("Zoom")]
        [Tooltip("Distancia minima, como fraccion de la distancia normal (1 = sin zoom).")]
        [SerializeField, Min(0.1f)] private float minZoom = 0.6f;
        [Tooltip("Distancia maxima, como fraccion de la distancia normal.")]
        [SerializeField, Min(0.1f)] private float maxZoom = 1.5f;
        [SerializeField, Min(0f)] private float zoomSharpness = 14f;

        [Header("Obstaculos")]
        [Tooltip("Acerca temporalmente la camara si una pared tapa al personaje.")]
        [SerializeField] private bool avoidObstacles = true;
        [SerializeField, Min(0.01f)] private float obstructionRadius = 0.25f;
        [SerializeField, Min(0f)] private float obstructionPadding = 0.1f;
        [SerializeField] private LayerMask obstructionLayers = Physics.DefaultRaycastLayers;

        private float targetZoom = 1f;
        private float currentZoom = 1f;

        public Transform Target => target;
        /// <summary>Zoom aplicado ahora mismo: 1 es la distancia normal, menos es mas cerca.</summary>
        public float Zoom => currentZoom;
        public float TargetZoom => targetZoom;

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

            float low = IsFinite(minZoom) ? Mathf.Max(0.1f, minZoom) : 0.6f;
            float high = IsFinite(maxZoom) ? Mathf.Max(low, maxZoom) : Mathf.Max(low, 1.5f);
            targetZoom = Mathf.Clamp(value, low, high);
            if (immediate)
                currentZoom = targetZoom;
        }

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 safeOffset = IsFinite(offset) ? offset : DefaultOffset;
            Vector3 safeLookOffset = IsFinite(lookOffset) ? lookOffset : DefaultLookOffset;

            float safeZoomSharpness = IsFinite(zoomSharpness) ? Mathf.Max(0f, zoomSharpness) : 0f;
            float zoomBlend = safeZoomSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-safeZoomSharpness * Time.deltaTime);
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, zoomBlend);

            Vector3 desiredPosition = target.position + ZoomedOffset(safeOffset, safeLookOffset);
            if (!IsFinite(desiredPosition))
                return;
            desiredPosition = AvoidObstacles(desiredPosition, safeLookOffset, out bool obstructed);

            // Sharpness = 0 significa seguimiento inmediato, nunca camara congelada.
            float safeSharpness = IsFinite(followSharpness) ? Mathf.Max(0f, followSharpness) : 0f;
            float blend = safeSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-safeSharpness * Time.deltaTime);
            Vector3 currentPosition = IsFinite(transform.position) ? transform.position : desiredPosition;
            // Al aparecer una pared, evitar atravesarla incluso durante el suavizado.
            // Cuando desaparece, recuperar el zoom con el seguimiento habitual.
            transform.position = obstructed
                ? desiredPosition
                : Vector3.Lerp(currentPosition, desiredPosition, blend);
            KeepFixedAngle(safeOffset, safeLookOffset);
        }

        /// <summary>
        /// El zoom acerca la camara al punto que mira, sobre la misma linea: cambia la
        /// distancia, no el angulo ni el centro de la imagen.
        /// </summary>
        private Vector3 ZoomedOffset(Vector3 safeOffset, Vector3 safeLookOffset)
        {
            float zoom = IsFinite(currentZoom) && currentZoom > 0f ? currentZoom : 1f;
            return safeLookOffset + (safeOffset - safeLookOffset) * zoom;
        }

        private void KeepFixedAngle(Vector3 safeOffset, Vector3 safeLookOffset)
        {
            // La orientacion no depende del retraso del seguimiento.
            Vector3 forward = safeLookOffset - safeOffset;
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
            if (target == null)
                return;

            Vector3 safeOffset = IsFinite(offset) ? offset : DefaultOffset;
            Vector3 safeLookOffset = IsFinite(lookOffset) ? lookOffset : DefaultLookOffset;
            currentZoom = targetZoom;
            Vector3 desiredPosition = target.position + ZoomedOffset(safeOffset, safeLookOffset);
            if (!IsFinite(desiredPosition))
                return;

            transform.position = AvoidObstacles(desiredPosition, safeLookOffset, out _);
            KeepFixedAngle(safeOffset, safeLookOffset);
        }

        private Vector3 AvoidObstacles(Vector3 desiredPosition, Vector3 safeLookOffset, out bool obstructed)
        {
            obstructed = false;
            if (!avoidObstacles || target == null)
                return desiredPosition;

            Vector3 focus = target.position + safeLookOffset;
            Vector3 offsetToCamera = desiredPosition - focus;
            float distance = offsetToCamera.magnitude;
            if (!IsFinite(distance) || distance <= 0.001f)
                return desiredPosition;

            Vector3 direction = offsetToCamera / distance;
            float radius = IsFinite(obstructionRadius) ? Mathf.Max(0.01f, obstructionRadius) : 0.25f;
            if (!Physics.SphereCast(focus, radius, direction, out RaycastHit hit, distance,
                obstructionLayers, QueryTriggerInteraction.Ignore))
                return desiredPosition;

            obstructed = true;
            float padding = IsFinite(obstructionPadding) ? Mathf.Max(0f, obstructionPadding) : 0.1f;
            return focus + direction * Mathf.Max(0f, hit.distance - padding);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
