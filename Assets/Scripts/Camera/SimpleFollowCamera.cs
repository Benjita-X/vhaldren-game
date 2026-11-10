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
        private static readonly Vector3 DefaultOffset = new Vector3(0f, 13f, -12f);
        private static readonly Vector3 DefaultLookOffset = new Vector3(0f, 0.5f, 0f);

        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 13f, -12f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField, Min(0f)] private float followSharpness = 12f;

        public Transform Target => target;

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
            Vector3 desiredPosition = target.position + safeOffset;
            if (!IsFinite(desiredPosition))
                return;

            // Sharpness = 0 significa seguimiento inmediato, nunca camara congelada.
            float safeSharpness = IsFinite(followSharpness) ? Mathf.Max(0f, followSharpness) : 0f;
            float blend = safeSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-safeSharpness * Time.deltaTime);
            Vector3 currentPosition = IsFinite(transform.position) ? transform.position : desiredPosition;
            transform.position = Vector3.Lerp(currentPosition, desiredPosition, blend);
            KeepFixedAngle(safeOffset, safeLookOffset);
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
            Vector3 desiredPosition = target.position + safeOffset;
            if (!IsFinite(desiredPosition))
                return;

            transform.position = desiredPosition;
            Vector3 safeLookOffset = IsFinite(lookOffset) ? lookOffset : DefaultLookOffset;
            KeepFixedAngle(safeOffset, safeLookOffset);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
