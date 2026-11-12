using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Settings;

namespace Vhaldren.Tests
{
    /// <summary>
    /// Camara cenital de accion: WASD relativo al giro, adelanto hacia el cursor, teclas
    /// de giro y orientacion de exploracion frente a combate (logica pura, sin escena).
    /// </summary>
    public sealed class TopDownCameraTests
    {
        // Camara del mundo sin girar: 54 grados hacia abajo, mirando hacia +Z.
        private static readonly Vector3 UnrotatedForward = Quaternion.Euler(54f, 0f, 0f) * Vector3.forward;

        [TearDown]
        public void ResetKeys() => GameInput.ResetDefaults();

        [Test]
        public void WithoutCameraRotationWasdMatchesTheWorldAxesAsBefore()
        {
            AssertPlanar(LocalPlayerInput.ResolveMove(new Vector2(0f, 1f), UnrotatedForward), Vector3.forward);
            AssertPlanar(LocalPlayerInput.ResolveMove(new Vector2(1f, 0f), UnrotatedForward), Vector3.right);
            AssertPlanar(LocalPlayerInput.ResolveMove(new Vector2(-1f, -1f), UnrotatedForward),
                new Vector3(-1f, 0f, -1f).normalized);
            // Exactamente el marco del mundo de antes (el mismo que sin camara).
            Vector3 before = new Vector3(0.3f, 0f, 0.8f);
            Assert.That(Vector3.Distance(LocalPlayerInput.ResolveMove(new Vector2(0.3f, 0.8f), Vector3.forward), before),
                Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(LocalPlayerInput.ResolveMove(new Vector2(0.3f, 0.8f), UnrotatedForward), before),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void CameraTurned90DegreesMakesWMoveTowardPlusX()
        {
            Vector3 turned = Quaternion.Euler(54f, 90f, 0f) * Vector3.forward;
            AssertPlanar(LocalPlayerInput.ResolveMove(new Vector2(0f, 1f), turned), Vector3.right);
            AssertPlanar(LocalPlayerInput.ResolveMove(new Vector2(0f, -1f), turned), Vector3.left);
            AssertPlanar(LocalPlayerInput.ResolveMove(new Vector2(1f, 0f), turned), Vector3.back);
            AssertPlanar(LocalPlayerInput.ResolveMove(new Vector2(-1f, 0f), turned), Vector3.forward);
        }

        [Test]
        public void VerticalOrBrokenCameraDirectionFallsBackToWorldAxes()
        {
            AssertPlanar(LocalPlayerInput.ResolveMove(new Vector2(0f, 1f), Vector3.down), Vector3.forward);
            AssertPlanar(LocalPlayerInput.ResolveMove(new Vector2(1f, 0f), new Vector3(float.NaN, 0f, 1f)), Vector3.right);
        }

        [Test]
        public void LookAheadIsZeroInTheDeadZoneGrowsSmoothlyAndIsCapped()
        {
            Assert.That(SimpleFollowCamera.LookAheadFor(new Vector2(0.1f, 0f), 0f, 1.8f, 0.15f), Is.EqualTo(Vector3.zero),
                "Con el cursor cerca del centro no se adelanta.");
            AssertVector(SimpleFollowCamera.LookAheadFor(new Vector2(0f, 3f), 0f, 1.8f, 0.15f), new Vector3(0f, 0f, 1.8f));
            Assert.That(SimpleFollowCamera.LookAheadFor(new Vector2(0.575f, 0f), 0f, 1.8f, 0.15f).magnitude,
                Is.EqualTo(0.9f).Within(0.001f), "A mitad de camino, la mitad del adelanto.");
            // Con la camara girada 90 grados, arriba en pantalla es +X en el mundo.
            AssertVector(SimpleFollowCamera.LookAheadFor(new Vector2(0f, 1f), 90f, 1.8f, 0.15f), new Vector3(1.8f, 0f, 0f));
            Assert.That(SimpleFollowCamera.LookAheadFor(new Vector2(float.NaN, 1f), 0f, 1.8f, 0.15f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PointerOffsetIsMeasuredInHalfScreenHeights()
        {
            Vector2 offset = LocalPlayerInput.PointerOffsetFromCenter(new Vector2(1600f, 450f), 1600f, 900f);
            Assert.That(offset.x, Is.EqualTo(800f / 450f).Within(0.0001f));
            Assert.That(offset.y, Is.EqualTo(0f).Within(0.0001f));
            Vector2 top = LocalPlayerInput.PointerOffsetFromCenter(new Vector2(800f, 900f), 1600f, 900f);
            Assert.That(top.y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CameraKeysUseFreeKeysWithReadableLabels()
        {
            GameInput.ResetDefaults();
            Assert.That(GameInput.KeyFor(GameAction.CameraRotateLeft), Is.EqualTo(Key.Insert));
            Assert.That(GameInput.KeyFor(GameAction.CameraRotateRight), Is.EqualTo(Key.Delete));
            Assert.That(GameInput.KeyFor(GameAction.CameraReset), Is.EqualTo(Key.Home));
            Assert.That(GameInput.Label(GameAction.CameraRotateLeft), Is.EqualTo("Insert"));
            Assert.That(GameInput.Label(GameAction.CameraRotateRight), Is.EqualTo("Supr"));
            Assert.That(GameInput.Label(GameAction.CameraReset), Is.EqualTo("Inicio"));
        }

        [Test]
        public void ExplorationFacesWhereThePlayerWalksAndIgnoresTheCursor()
        {
            // Camina hacia +X con el cursor detras (-Z): explorando mira hacia +X.
            AssertPlanar(LocalPlayerInput.ChooseFacing(false, Vector3.right, Vector3.back, Vector3.forward, 1000f),
                Vector3.right);
            // Quieto conserva la orientacion que tenia.
            Assert.That(LocalPlayerInput.ChooseFacing(false, Vector3.zero, Vector3.back, Vector3.left, 1000f),
                Is.EqualTo(Vector3.left));
            // Giro suave: como mucho los grados de un paso, sin inclinarse.
            Vector3 limited = LocalPlayerInput.ChooseFacing(false, Vector3.back, Vector3.left, Vector3.forward, 30f);
            Assert.That(Vector3.Angle(Vector3.forward, limited), Is.EqualTo(30f).Within(0.01f));
            Assert.That(limited.y, Is.Zero);
            // Media vuelta exacta: tambien gira (no se queda bloqueado).
            Vector3 halfTurn = LocalPlayerInput.TurnTowards(Vector3.forward, Vector3.back, 45f);
            Assert.That(Vector3.Angle(Vector3.forward, halfTurn), Is.EqualTo(45f).Within(0.01f));
        }

        [Test]
        public void CombatFacesTheCursorSoTheLegsStrafeAndBackpedal()
        {
            Vector3 facing = LocalPlayerInput.ChooseFacing(true, Vector3.right, Vector3.back, Vector3.forward, 30f);
            Assert.That(facing, Is.EqualTo(Vector3.back), "En combate se mira al cursor.");
            // Caminar hacia +X mirando a -Z: paso lateral puro en la mezcla de piernas.
            Vector2 sideways = PlayerAnimationDriver.LocalMoveDirection(Vector3.right * 6f, facing, 6f);
            Assert.That(Mathf.Abs(sideways.x), Is.EqualTo(1f).Within(0.01f));
            Assert.That(sideways.y, Is.EqualTo(0f).Within(0.01f));
            // Alejarse del cursor: retroceso.
            Vector2 backwards = PlayerAnimationDriver.LocalMoveDirection(Vector3.forward * 6f, facing, 6f);
            Assert.That(backwards.y, Is.LessThan(-0.9f));
        }

        [Test]
        public void FacingModeFollowsCombatStateAndRecentActions()
        {
            const float now = 100f;
            float never = float.NegativeInfinity;
            Assert.That(LocalPlayerInput.UsesCombatFacing(now, never, never), Is.False, "Explorando.");
            Assert.That(LocalPlayerInput.UsesCombatFacing(now, now, never), Is.True, "En combate.");
            Assert.That(LocalPlayerInput.UsesCombatFacing(now, now - 1.9f, never), Is.True, "Justo despues del combate.");
            Assert.That(LocalPlayerInput.UsesCombatFacing(now, now - 2.1f, never), Is.False);
            Assert.That(LocalPlayerInput.UsesCombatFacing(now, never, now - 1.4f), Is.True, "Tras atacar o disparar.");
            Assert.That(LocalPlayerInput.UsesCombatFacing(now, never, now - 1.6f), Is.False);
        }

        private static void AssertPlanar(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.y, Is.EqualTo(0f).Within(0.0001f), "Movimiento sin componente vertical.");
            AssertVector(actual, expected);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f),
                "Esperado " + expected.ToString("F3") + ", obtenido " + actual.ToString("F3"));
        }
    }
}
