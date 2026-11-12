using UnityEngine;
using Vhaldren.Gameplay.Settings;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>Teclas de magia y esencia, compatibles con el Input System y el sistema clasico.</summary>
    internal static class MagicKeys
    {
        /// <summary>1, 2 o 3 lanzan el hechizo de la escuela activa. -1 si ninguna.</summary>
        public static int SpellSlotPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (GameInput.Down(GameAction.Spell1)) return 0;
            if (GameInput.Down(GameAction.Spell2)) return 1;
            if (GameInput.Down(GameAction.Spell3)) return 2;
            return -1;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
            return -1;
#else
            return -1;
#endif
        }

        public static bool NextSchool
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return GameInput.Down(GameAction.NextSchool);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Tab);
#else
                return false;
#endif
            }
        }

        public static bool AbsorbHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return GameInput.Held(GameAction.Absorb);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKey(KeyCode.H);
#else
                return false;
#endif
            }
        }

        public static bool PrimaryPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButtonDown(0);
#else
                return false;
#endif
            }
        }

        public static Vector2 Pointer
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector2.zero;
#endif
            }
        }
    }
}
