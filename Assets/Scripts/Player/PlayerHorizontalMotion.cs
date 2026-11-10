using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Calcula la velocidad horizontal sin depender de teclado, camara, fisica ni red.
    /// Es determinista para los mismos argumentos y un deltaTime valido.
    /// </summary>
    public static class PlayerHorizontalMotion
    {
        public static Vector3 Step(
            Vector3 current,
            Vector3 desired,
            float acceleration,
            float braking,
            float reversalAcceleration,
            float deltaTime,
            bool instant)
        {
            if (!IsFinite(current))
                current = Vector3.zero;
            if (!IsFinite(desired))
                desired = Vector3.zero;

            // El estado vertical pertenece a PlayerMotor.
            current.y = 0f;
            desired.y = 0f;

            if (instant)
                return desired;

            if (!IsFinite(deltaTime) || deltaTime <= 0f)
                return current;

            // Frenado sin entrada; respuesta extra al invertir la direccion.
            float rate = desired.sqrMagnitude <= 0.000001f
                ? braking
                : Vector3.Dot(current, desired) < -0.0001f
                    ? reversalAcceleration
                    : acceleration;

            if (!IsFinite(rate) || rate <= 0f)
                return current;

            float maxChange = rate * deltaTime;
            if (!IsFinite(maxChange))
                return desired;

            // Nunca supera el destino; el tiempo, no los FPS, determina el cambio.
            return Vector3.MoveTowards(current, desired, maxChange);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
