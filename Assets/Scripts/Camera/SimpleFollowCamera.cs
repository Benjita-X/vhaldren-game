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

            Vector3 desiredPosition = target.position + offset;
            float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            KeepFixedAngle();
        }

        private void KeepFixedAngle()
        {
            // La orientacion no depende del retraso del seguimiento.
            Vector3 forward = lookOffset - offset;
            if (forward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void SnapToTarget()
        {
            if (target == null)
                return;

            transform.position = target.position + offset;
            KeepFixedAngle();
        }
    }
}
