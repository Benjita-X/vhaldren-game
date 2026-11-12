using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Vhaldren.Gameplay.Settings
{
    /// <summary>Todas las acciones de teclado del juego. El clic izquierdo (ballesta) y la rueda no se reasignan.</summary>
    public enum GameAction
    {
        MoveForward, MoveBack, MoveLeft, MoveRight, Dash,
        Sword, RunePulse, Frost, Shield, Ward, Parry, Potion,
        Spell1, Spell2, Spell3, NextSchool, Absorb, RacialPower,
        Equipment, Build, Rotate, Dismantle, Gather, CraftPick1, CraftPick2,
        Noticeboard, Accept, Offer, Buy, Help, Pause
    }

    /// <summary>
    /// Mapa de teclas reasignable, guardado en PlayerPrefs. Si asignas una tecla que
    /// ya usa otra accion, se intercambian (nunca quedan dos acciones con la misma tecla).
    /// </summary>
    public static class GameInput
    {
        private const string Prefix = "vh.tecla.";

        public struct Binding
        {
            public GameAction Action;
            public string Label;
            public string Group;
#if ENABLE_INPUT_SYSTEM
            public Key Default;
#endif
        }

        public static readonly Binding[] All = Build();
        private static readonly Dictionary<GameAction, int> current = new Dictionary<GameAction, int>();
        private static bool loaded;

        public static event Action Changed;

        private static Binding[] Build()
        {
            List<Binding> list = new List<Binding>();
#if ENABLE_INPUT_SYSTEM
            void Add(GameAction action, string label, string group, Key key) =>
                list.Add(new Binding { Action = action, Label = label, Group = group, Default = key });
#else
            void Add(GameAction action, string label, string group, int key) =>
                list.Add(new Binding { Action = action, Label = label, Group = group });
            const int W = 0, S = 0, A = 0, D = 0, Space = 0, V = 0, T = 0, F = 0, Z = 0, C = 0, X = 0, Q = 0,
                Digit1 = 0, Digit2 = 0, Digit3 = 0, Tab = 0, H = 0, R = 0, U = 0, B = 0, G = 0, E = 0, J = 0,
                K = 0, N = 0, Y = 0, O = 0, P = 0, F1 = 0, Escape = 0;
#endif
#if ENABLE_INPUT_SYSTEM
            Add(GameAction.MoveForward, "Avanzar", "Movimiento", Key.W);
            Add(GameAction.MoveBack, "Retroceder", "Movimiento", Key.S);
            Add(GameAction.MoveLeft, "Izquierda", "Movimiento", Key.A);
            Add(GameAction.MoveRight, "Derecha", "Movimiento", Key.D);
            Add(GameAction.Dash, "Esquivar", "Movimiento", Key.Space);
            Add(GameAction.Sword, "Tajo de espada", "Combate", Key.V);
            Add(GameAction.RunePulse, "Pulso rúnico", "Combate", Key.T);
            Add(GameAction.Frost, "Onda de escarcha", "Combate", Key.F);
            Add(GameAction.Shield, "Escudo (mantener)", "Combate", Key.Z);
            Add(GameAction.Ward, "Barrera mágica (mantener)", "Combate", Key.C);
            Add(GameAction.Parry, "Parada precisa", "Combate", Key.X);
            Add(GameAction.Potion, "Poción", "Combate", Key.Q);
            Add(GameAction.Spell1, "Hechizo 1", "Magia", Key.Digit1);
            Add(GameAction.Spell2, "Hechizo 2", "Magia", Key.Digit2);
            Add(GameAction.Spell3, "Hechizo 3 (definitivo)", "Magia", Key.Digit3);
            Add(GameAction.NextSchool, "Cambiar escuela", "Magia", Key.Tab);
            Add(GameAction.Absorb, "Absorber esencia (mantener)", "Magia", Key.H);
            Add(GameAction.RacialPower, "Poder de raza", "Magia", Key.R);
            Add(GameAction.Equipment, "Forja de equipo", "Supervivencia", Key.U);
            Add(GameAction.Build, "Modo construcción", "Supervivencia", Key.B);
            Add(GameAction.Rotate, "Girar pieza (construcción)", "Supervivencia", Key.R);
            Add(GameAction.Dismantle, "Desmontar (construcción)", "Supervivencia", Key.G);
            Add(GameAction.Gather, "Recolectar (mantener)", "Supervivencia", Key.E);
            Add(GameAction.CraftPick1, "Fabricar pico improvisado", "Supervivencia", Key.J);
            Add(GameAction.CraftPick2, "Fabricar pico de piedra", "Supervivencia", Key.K);
            Add(GameAction.Noticeboard, "Abrir tablón", "Pueblos", Key.N);
            Add(GameAction.Accept, "Aceptar / reclamar", "Pueblos", Key.Y);
            Add(GameAction.Offer, "Cambiar oferta", "Pueblos", Key.O);
            Add(GameAction.Buy, "Comprar", "Pueblos", Key.P);
            Add(GameAction.Help, "Ayuda en pantalla", "Interfaz", Key.F1);
            Add(GameAction.Pause, "Menú / pausa", "Interfaz", Key.Escape);
#endif
            return list.ToArray();
        }

        /// <summary>Girar y poder de raza comparten R: uno solo funciona en construcción.</summary>
        private static bool SharesKeyByDesign(GameAction a, GameAction b) =>
            (a == GameAction.Rotate && b == GameAction.RacialPower) || (a == GameAction.RacialPower && b == GameAction.Rotate) ||
            (a == GameAction.Dismantle && b == GameAction.RacialPower) || (a == GameAction.RacialPower && b == GameAction.Dismantle);

        private static int IndexOf(GameAction action)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Action == action)
                    return i;
            return -1;
        }

        private static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
#if ENABLE_INPUT_SYSTEM
            foreach (Binding binding in All)
            {
                int value = (int)binding.Default;
                string saved = PlayerPrefs.GetString(Prefix + binding.Action, string.Empty);
                if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out Key parsed) && parsed != Key.None)
                    value = (int)parsed;
                current[binding.Action] = value;
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        public static Key KeyFor(GameAction action)
        {
            EnsureLoaded();
            return current.TryGetValue(action, out int value) ? (Key)value : Key.None;
        }

        /// <summary>Asigna una tecla. Si otra accion la usaba, recibe la tecla anterior.</summary>
        public static void Rebind(GameAction action, Key key)
        {
            EnsureLoaded();
            if (key == Key.None)
                return;
            Key previous = KeyFor(action);
            foreach (Binding other in All)
            {
                if (other.Action == action || KeyFor(other.Action) != key || SharesKeyByDesign(action, other.Action))
                    continue;
                current[other.Action] = (int)previous;
                PlayerPrefs.SetString(Prefix + other.Action, previous.ToString());
            }
            current[action] = (int)key;
            PlayerPrefs.SetString(Prefix + action, key.ToString());
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Nombre de control del Input System ("a", "space", "1"...).</summary>
        public static string ControlPath(GameAction action) => "<Keyboard>/" + ControlName(KeyFor(action));

        public static string ControlName(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                KeyControl control = keyboard[key];
                if (control != null)
                    return control.name;
            }
            string name = key.ToString();
            if (name.StartsWith("Digit", StringComparison.Ordinal))
                return name.Substring(5);
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static KeyControl Control(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            Key key = KeyFor(action);
            return keyboard == null || key == Key.None ? null : keyboard[key];
        }
#endif

        public static void ResetDefaults()
        {
            foreach (Binding binding in All)
                PlayerPrefs.DeleteKey(Prefix + binding.Action);
            PlayerPrefs.Save();
            loaded = false;
            current.Clear();
            EnsureLoaded();
            Changed?.Invoke();
        }

        public static bool Down(GameAction action)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl control = Control(action);
            return control != null && control.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool Held(GameAction action)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl control = Control(action);
            return control != null && control.isPressed;
#else
            return false;
#endif
        }

        /// <summary>Texto corto de la tecla para la interfaz ("V", "Espacio", "1").</summary>
        public static string Label(GameAction action)
        {
#if ENABLE_INPUT_SYSTEM
            Key key = KeyFor(action);
            switch (key)
            {
                case Key.Space: return "Espacio";
                case Key.Escape: return "Esc";
                case Key.Tab: return "Tab";
                case Key.LeftShift: return "Mayús";
                case Key.LeftCtrl: return "Ctrl";
                case Key.Enter: return "Intro";
                case Key.None: return "—";
            }
            string name = key.ToString();
            return name.StartsWith("Digit", StringComparison.Ordinal) ? name.Substring(5) : name;
#else
            return action.ToString();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>Primera tecla pulsada este frame (para la pantalla de reasignar).</summary>
        public static Key AnyKeyDown()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Key.None;
            foreach (KeyControl control in keyboard.allKeys)
                if (control != null && control.wasPressedThisFrame)
                    return control.keyCode;
            return Key.None;
        }
#endif
    }
}
