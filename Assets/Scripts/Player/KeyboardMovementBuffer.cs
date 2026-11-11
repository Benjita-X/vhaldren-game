namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Conserva el orden real de las pulsaciones de las cuatro teclas de movimiento.
    /// Los eventos permiten distinguir pulsaciones y liberaciones que ocurren dentro
    /// del mismo frame, algo que se pierde al consultar solo isPressed/wasPressedThisFrame.
    /// </summary>
    public sealed class KeyboardMovementBuffer
    {
        public enum Key
        {
            Left,
            Right,
            Back,
            Forward
        }

        private bool leftHeld;
        private bool rightHeld;
        private bool backHeld;
        private bool forwardHeld;

        private int horizontalLastPressed;
        private int verticalLastPressed;
        private int horizontalTap;
        private int verticalTap;

        /// <summary>Actualiza el estado a partir de un evento de pulsar o soltar.</summary>
        public void SetPressed(Key key, bool pressed)
        {
            switch (key)
            {
                case Key.Left:
                    SetAxisKey(ref leftHeld, pressed, -1, ref horizontalLastPressed, ref horizontalTap);
                    break;
                case Key.Right:
                    SetAxisKey(ref rightHeld, pressed, 1, ref horizontalLastPressed, ref horizontalTap);
                    break;
                case Key.Back:
                    SetAxisKey(ref backHeld, pressed, -1, ref verticalLastPressed, ref verticalTap);
                    break;
                case Key.Forward:
                    SetAxisKey(ref forwardHeld, pressed, 1, ref verticalLastPressed, ref verticalTap);
                    break;
            }
        }

        /// <summary>
        /// Devuelve el estado sostenido. Si una tecla se pulsó y soltó desde la última
        /// lectura, conserva un único paso de movimiento para que el tap no desaparezca.
        /// </summary>
        public void Consume(out float horizontal, out float vertical)
        {
            horizontal = ResolveAxis(leftHeld, rightHeld, horizontalLastPressed, horizontalTap);
            vertical = ResolveAxis(backHeld, forwardHeld, verticalLastPressed, verticalTap);
            horizontalTap = 0;
            verticalTap = 0;

            if (!leftHeld && !rightHeld)
                horizontalLastPressed = 0;
            if (!backHeld && !forwardHeld)
                verticalLastPressed = 0;
        }

        /// <summary>Olvida las teclas si el componente o la aplicación pierde el foco.</summary>
        public void Reset()
        {
            leftHeld = rightHeld = backHeld = forwardHeld = false;
            horizontalLastPressed = verticalLastPressed = 0;
            horizontalTap = verticalTap = 0;
        }

        private static void SetAxisKey(
            ref bool held,
            bool pressed,
            int direction,
            ref int lastPressed,
            ref int tap)
        {
            if (pressed && !held)
            {
                lastPressed = direction;
                tap = direction;
            }

            held = pressed;
        }

        private static float ResolveAxis(bool negativeHeld, bool positiveHeld, int lastPressed, int tap)
        {
            if (negativeHeld && positiveHeld)
                return lastPressed;
            if (negativeHeld)
                return -1f;
            if (positiveHeld)
                return 1f;
            return tap;
        }
    }
}
