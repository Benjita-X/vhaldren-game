using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class PlayerDashTests
    {
        private const float Duration = 0.18f;
        private const float Cooldown = 1.2f;
        private const float Buffer = 0.15f;

        [Test]
        public void StartsOnTheRequestedStepTowardThePreferredDirection()
        {
            PlayerDash dash = default;

            float dashTime = dash.Step(true, new Vector3(3f, 5f, 0f), Duration, Cooldown, Buffer, 1f / 60f);

            Assert.That(dashTime, Is.EqualTo(1f / 60f).Within(0.00001f));
            Assert.That(dash.IsActive, Is.True);
            Assert.That(dash.Direction, Is.EqualTo(Vector3.right));
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        [TestCase(1f / 1000f)]
        public void LastsExactlyItsDurationAtAnyFrameRate(float step)
        {
            PlayerDash dash = default;
            float total = dash.Step(true, Vector3.forward, Duration, Cooldown, Buffer, step);
            for (int i = 0; i < 2000 && dash.IsActive; i++)
                total += dash.Step(false, Vector3.forward, Duration, Cooldown, Buffer, step);

            Assert.That(total, Is.EqualTo(Duration).Within(0.0005f));
            Assert.That(dash.IsActive, Is.False);
        }

        [Test]
        public void KeepsItsDirectionEvenIfThePreferredDirectionChanges()
        {
            PlayerDash dash = default;
            dash.Step(true, Vector3.forward, Duration, Cooldown, Buffer, 0.05f);

            dash.Step(false, Vector3.left, Duration, Cooldown, Buffer, 0.05f);

            Assert.That(dash.Direction, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void CannotStartAgainDuringTheCooldown()
        {
            PlayerDash dash = default;
            Advance(ref dash, true, 0.5f);

            // Peticion a mitad de la espera: caduca antes de que la esquiva este lista.
            float dashTime = Advance(ref dash, true, 0.4f);

            Assert.That(dashTime, Is.Zero);
            Assert.That(dash.IsReady, Is.False);
        }

        [Test]
        public void IsReadyAgainAfterTheCooldown()
        {
            PlayerDash dash = default;
            Advance(ref dash, true, Cooldown + 0.05f);

            Assert.That(dash.IsReady, Is.True);
            Assert.That(dash.Step(true, Vector3.back, Duration, Cooldown, Buffer, 0.01f), Is.EqualTo(0.01f).Within(0.00001f));
            Assert.That(dash.Direction, Is.EqualTo(Vector3.back));
        }

        [Test]
        public void RequestMadeJustBeforeTheCooldownEndsIsNotLost()
        {
            PlayerDash dash = default;
            Advance(ref dash, true, Cooldown - 0.1f);

            // Se pulsa 0,1 s antes de tiempo (menos que el bufer de 0,15 s) y se suelta.
            float dashTime = Advance(ref dash, true, 0.3f);

            Assert.That(dashTime, Is.GreaterThan(0.1f), "La esquiva debe salir en cuanto termina la espera.");
        }

        [Test]
        public void RequestOlderThanTheBufferIsForgotten()
        {
            PlayerDash dash = default;
            Advance(ref dash, true, Cooldown - 0.4f);

            float dashTime = Advance(ref dash, true, 0.8f);

            Assert.That(dashTime, Is.Zero);
            Assert.That(dash.IsReady, Is.True);
        }

        [Test]
        public void DoesNotStartWithoutADirection()
        {
            PlayerDash dash = default;

            float dashTime = dash.Step(true, Vector3.up, Duration, Cooldown, 0f, 0.02f);

            Assert.That(dashTime, Is.Zero);
            Assert.That(dash.IsActive, Is.False);
            Assert.That(dash.IsReady, Is.True);
        }

        [Test]
        public void InvalidTimeOrSettingsNeverProduceInvalidState()
        {
            PlayerDash dash = default;

            Assert.That(dash.Step(true, Vector3.forward, Duration, Cooldown, Buffer, float.NaN), Is.Zero);
            Assert.That(dash.Step(true, Vector3.forward, Duration, Cooldown, Buffer, -1f), Is.Zero);
            Assert.That(dash.Step(true, Vector3.forward, float.NaN, float.PositiveInfinity, float.NaN, 0.02f), Is.Zero);
            Assert.That(float.IsNaN(dash.CooldownRemaining), Is.False);
            Assert.That(dash.IsActive, Is.False);
        }

        [Test]
        public void CancelStopsTheDashButKeepsTheCooldown()
        {
            PlayerDash dash = default;
            dash.Step(true, Vector3.forward, Duration, Cooldown, Buffer, 0.02f);

            dash.Cancel();

            Assert.That(dash.IsActive, Is.False);
            Assert.That(dash.IsReady, Is.False);
        }

        /// <summary>Pide (o no) en el primer paso y avanza el tiempo indicado; devuelve el tiempo esquivado.</summary>
        private static float Advance(ref PlayerDash dash, bool requestFirst, float seconds)
        {
            const float step = 1f / 120f;
            int steps = Mathf.RoundToInt(seconds / step);
            float total = 0f;
            for (int i = 0; i < steps; i++)
                total += dash.Step(requestFirst && i == 0, Vector3.forward, Duration, Cooldown, Buffer, step);
            return total;
        }
    }
}
