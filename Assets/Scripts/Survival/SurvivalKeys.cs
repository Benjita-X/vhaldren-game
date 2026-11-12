using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Vhaldren.Gameplay.Survival
{
    /// <summary>Teclas de supervivencia, compatibles con el Input System y el sistema clasico.</summary>
    internal static class SurvivalKeys
    {
        public static bool ToggleBuild => Down(KeyCode.B);
        public static bool Rotate => Down(KeyCode.R);
        public static bool Dismantle => Down(KeyCode.G);
        public static bool GatherHeld => Held(KeyCode.E);
        public static bool CraftImprovisedPick => Down(KeyCode.J);
        public static bool CraftStonePick => Down(KeyCode.K);
        public static bool FreePlacementHeld => Held(KeyCode.LeftShift);
        public static bool Undo => Held(KeyCode.LeftControl) && Down(KeyCode.Z);

        /// <summary>1..9 y 0 eligen pieza. Devuelve -1 si no se pulso ninguna.</summary>
        public static int PieceSlotPressed()
        {
            for (int i = 0; i < 9; i++)
                if (Down(KeyCode.Alpha1 + i))
                    return i;
            if (Down(KeyCode.Alpha0))
                return 9;
            return -1;
        }

        public static bool PlacePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static Vector2 Pointer
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        private static bool Down(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl key = Map(code);
            return key != null && key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(code);
#endif
        }

        private static bool Held(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl key = Map(code);
            return key != null && key.isPressed;
#else
            return Input.GetKey(code);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static KeyControl Map(KeyCode code)
        {
            Keyboard k = Keyboard.current;
            if (k == null)
                return null;
            switch (code)
            {
                case KeyCode.B: return k.bKey;
                case KeyCode.R: return k.rKey;
                case KeyCode.G: return k.gKey;
                case KeyCode.E: return k.eKey;
                case KeyCode.J: return k.jKey;
                case KeyCode.K: return k.kKey;
                case KeyCode.Z: return k.zKey;
                case KeyCode.LeftShift: return k.leftShiftKey;
                case KeyCode.LeftControl: return k.leftCtrlKey;
                case KeyCode.Alpha0: return k.digit0Key;
                case KeyCode.Alpha1: return k.digit1Key;
                case KeyCode.Alpha2: return k.digit2Key;
                case KeyCode.Alpha3: return k.digit3Key;
                case KeyCode.Alpha4: return k.digit4Key;
                case KeyCode.Alpha5: return k.digit5Key;
                case KeyCode.Alpha6: return k.digit6Key;
                case KeyCode.Alpha7: return k.digit7Key;
                case KeyCode.Alpha8: return k.digit8Key;
                case KeyCode.Alpha9: return k.digit9Key;
                default: return null;
            }
        }
#endif
    }
}
