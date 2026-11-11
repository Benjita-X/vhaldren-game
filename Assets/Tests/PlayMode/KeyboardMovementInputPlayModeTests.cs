using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class KeyboardMovementInputPlayModeTests : InputTestFixture
    {
        [Test]
        public void NormalizedMouseWheelDeltaIsNotDividedByNativeWindowsUnits()
        {
            InputSettings settings = InputSystem.settings;
            InputSettings.ScrollDeltaBehavior originalBehavior = settings.scrollDeltaBehavior;
            settings.scrollDeltaBehavior = InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState
                {
                    position = new Vector2(100f, 100f),
                    scroll = new Vector2(0f, 1f)
                });
                InputSystem.Update();

                Assert.That(ReadScroll(), Is.EqualTo(1f).Within(0.0001f),
                    "Unity normaliza el delta de rueda; 1 debe equivaler a un paso de zoom.");
            }
            finally
            {
                settings.scrollDeltaBehavior = originalBehavior;
            }
        }

        [Test]
        public void NativePlatformWheelDeltaNormalizesToOneStep()
        {
            InputSettings settings = InputSystem.settings;
            InputSettings.ScrollDeltaBehavior originalBehavior = settings.scrollDeltaBehavior;
            settings.scrollDeltaBehavior = InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            bool windows = Application.platform == RuntimePlatform.WindowsEditor ||
                           Application.platform == RuntimePlatform.WindowsPlayer;
            try
            {
                float nativeDelta = windows ? 120f : 1f;
                InputSystem.QueueStateEvent(mouse, new MouseState
                {
                    position = new Vector2(100f, 100f),
                    scroll = new Vector2(0f, nativeDelta)
                });
                InputSystem.Update();

                Assert.That(ReadScroll(), Is.EqualTo(1f).Within(0.0001f),
                    "El delta nativo debe convertirse a un paso de zoom en todas las plataformas.");
            }
            finally
            {
                settings.scrollDeltaBehavior = originalBehavior;
            }
        }

        [Test]
        public void OppositeTapsInOneInputUpdateKeepTheFinalPressDirection()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            GameObject player = new GameObject("InputTapTestPlayer");
            player.AddComponent<CharacterController>();
            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            LocalPlayerInput input = player.AddComponent<LocalPlayerInput>();

            try
            {
                // Cuatro eventos llegan antes de una sola lectura de Update: A down/up,
                // luego D down/up. Leer solo isPressed/wasPressedThisFrame pierde el orden.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                InvokeUpdate(input);
                Assert.That(motor.HorizontalVelocity.x, Is.EqualTo(6f).Within(0.001f),
                    "D fue la ultima pulsacion y debe llegar al motor como movimiento a la derecha.");
                Assert.That(player.transform.position.x, Is.GreaterThan(0f),
                    "La pulsacion rapida debe producir desplazamiento, no solo una lectura de entrada.");

                InvokeUpdate(input);
                Assert.That(motor.HorizontalVelocity, Is.EqualTo(Vector3.zero),
                    "El tap debe consumirse una sola vez.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void SpaceTapPressedAndReleasedBeforeMotorUpdateStillStartsDash()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            GameObject player = new GameObject("DashTapTestPlayer");
            player.AddComponent<CharacterController>();
            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            LocalPlayerInput input = player.AddComponent<LocalPlayerInput>();

            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                // Un toque muy corto no debe perderse antes de leer el comando.
                InvokeUpdate(input);
                Assert.That(motor.DashMovedThisStep, Is.True,
                    "Un tap de Espacio debe iniciar la esquiva aunque se suelte antes de Update.");
                Assert.That(motor.IsDashReady, Is.False,
                    "La esquiva debe activar la espera despues del toque.");
                Assert.That(ReadDashPressed(input), Is.False,
                    "La pulsacion guardada debe consumirse una sola vez.");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void LosingFocusClearsBufferedSpaceTap()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            GameObject player = new GameObject("DashFocusTestPlayer");
            player.AddComponent<CharacterController>();
            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            LocalPlayerInput input = player.AddComponent<LocalPlayerInput>();

            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(LocalPlayerInput).GetMethod("OnApplicationFocus", flags)
                    .Invoke(input, new object[] { false });
                InvokeUpdate(input);
                Assert.That(motor.DashMovedThisStep, Is.False,
                    "No se debe lanzar una esquiva pendiente cuando la ventana pierde el foco.");
                Assert.That(motor.IsDashReady, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static bool ReadDashPressed(LocalPlayerInput input)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo readDash = typeof(LocalPlayerInput).GetMethod("ReadDashPressed", flags);
            return (bool)readDash.Invoke(input, null);
        }

        private static void InvokeUpdate(LocalPlayerInput input)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            MethodInfo update = typeof(LocalPlayerInput).GetMethod("Update", flags);
            update.Invoke(input, null);
        }

        private static float ReadScroll()
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            MethodInfo readScroll = typeof(LocalPlayerInput).GetMethod("ReadScroll", flags);
            return (float)readScroll.Invoke(null, null);
        }
    }
}
