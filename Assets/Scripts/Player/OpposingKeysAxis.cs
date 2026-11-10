namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Resuelve un eje gobernado por dos teclas opuestas (A/D o S/W).
    /// Si ambas estan pulsadas manda la ultima que se pulso, en vez de anularse:
    /// al alternar rapido A y D los dedos se solapan unos milisegundos y, con la
    /// suma clasica (-1 + 1 = 0), el personaje se quedaba quieto con teclas pulsadas.
    /// No lee dispositivos: recibe el estado de las teclas y es comprobable sin Unity.
    /// </summary>
    public struct OpposingKeysAxis
    {
        private bool negativeWasHeld;
        private bool positiveWasHeld;
        private int lastPressed;

        /// <summary>Devuelve -1, 0 o +1 para el estado actual de las dos teclas.</summary>
        public float Resolve(bool negativeHeld, bool positiveHeld)
        {
            bool negativePressedNow = negativeHeld && !negativeWasHeld;
            bool positivePressedNow = positiveHeld && !positiveWasHeld;

            // Si las dos bajan en la misma lectura no hay "ultima": se conserva la anterior.
            if (negativePressedNow != positivePressedNow)
                lastPressed = negativePressedNow ? -1 : 1;

            negativeWasHeld = negativeHeld;
            positiveWasHeld = positiveHeld;

            if (negativeHeld && positiveHeld)
                return lastPressed;
            if (negativeHeld)
                return -1f;
            if (positiveHeld)
                return 1f;

            lastPressed = 0;
            return 0f;
        }

        /// <summary>Olvida el estado, por ejemplo al perder el foco de la ventana.</summary>
        public void Reset()
        {
            negativeWasHeld = false;
            positiveWasHeld = false;
            lastPressed = 0;
        }
    }
}
