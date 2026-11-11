using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Estado de la esquiva: duracion, tiempo de espera y peticion guardada.
    /// No mueve nada ni lee dispositivos: dice cuanto tiempo de cada paso se pasa
    /// esquivando, y el motor convierte ese tiempo en desplazamiento. Asi la distancia
    /// recorrida es la misma a 30 que a 240 FPS.
    /// </summary>
    public struct PlayerDash
    {
        private float activeRemaining;
        private float cooldownRemaining;
        private float bufferRemaining;
        private bool pending;

        /// <summary>Direccion plana y normalizada de la esquiva en curso o de la ultima.</summary>
        public Vector3 Direction { get; private set; }

        public bool IsActive => activeRemaining > 0f;
        public bool IsReady => activeRemaining <= 0f && cooldownRemaining <= 0f;
        public float CooldownRemaining => cooldownRemaining;

        /// <summary>
        /// Avanza un paso. Devuelve los segundos de este paso que se pasan esquivando
        /// (entre 0 y deltaTime).
        /// </summary>
        /// <param name="requested">La esquiva se pidio en este paso.</param>
        /// <param name="preferredDirection">Hacia donde esquivar si empieza ahora.</param>
        /// <param name="duration">Cuanto dura la esquiva.</param>
        /// <param name="cooldown">Espera entre el inicio de una esquiva y el de la siguiente.</param>
        /// <param name="bufferWindow">
        /// Cuanto se recuerda una peticion hecha antes de tiempo. Pulsar un instante antes de
        /// que termine la espera no se pierde: la esquiva sale en cuanto puede.
        /// </param>
        public float Step(bool requested, Vector3 preferredDirection, float duration, float cooldown,
            float bufferWindow, float deltaTime)
        {
            if (!IsUsable(deltaTime) || deltaTime <= 0f)
                return 0f;

            duration = Sanitize(duration);
            cooldown = Sanitize(cooldown);
            bufferWindow = Sanitize(bufferWindow);

            if (requested)
            {
                pending = true;
                bufferRemaining = bufferWindow;
            }

            float stepRemaining = deltaTime;
            float dashTime = 0f;

            if (activeRemaining > 0f)
            {
                float used = Mathf.Min(activeRemaining, stepRemaining);
                activeRemaining -= used;
                stepRemaining -= used;
                dashTime += used;
            }

            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);

            if (pending && activeRemaining <= 0f && cooldownRemaining <= 0f && duration > 0f &&
                TryNormalizePlanar(preferredDirection, out Vector3 direction))
            {
                pending = false;
                Direction = direction;
                activeRemaining = duration;
                cooldownRemaining = cooldown;

                float used = Mathf.Min(activeRemaining, stepRemaining);
                activeRemaining -= used;
                dashTime += used;
            }
            else if (pending)
            {
                bufferRemaining -= deltaTime;
                if (bufferRemaining < 0f)
                    pending = false;
            }

            return dashTime;
        }

        /// <summary>Cancela la esquiva en curso y la peticion guardada; conserva la espera.</summary>
        public void Cancel()
        {
            activeRemaining = 0f;
            pending = false;
        }

        private static bool TryNormalizePlanar(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            value.y = 0f;
            float largest = Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.z));
            if (!IsUsable(largest) || largest <= 0f)
                return false;

            value /= largest;
            float squareMagnitude = value.sqrMagnitude;
            if (!IsUsable(squareMagnitude) || squareMagnitude <= 0.0001f)
                return false;

            normalized = value / Mathf.Sqrt(squareMagnitude);
            return true;
        }

        private static float Sanitize(float value) => IsUsable(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsUsable(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
