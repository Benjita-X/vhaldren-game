using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class PlayerHorizontalMotionTests
    {
        [Test]
        public void AcceleratesAtConfiguredRate()
        {
            Vector3 result = PlayerHorizontalMotion.Step(
                Vector3.zero, Vector3.right * 6f, 10f, 20f, 30f, 0.1f, false);

            Assert.That(result.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.y, Is.Zero);
            Assert.That(result.z, Is.Zero);
        }

        [Test]
        public void BrakesWithoutOvershootingZero()
        {
            Vector3 result = PlayerHorizontalMotion.Step(
                Vector3.right * 2f, Vector3.zero, 20f, 20f, 30f, 0.2f, false);

            Assert.That(result, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void UsesReversalRateForOneHundredEightyDegreeTurn()
        {
            Vector3 result = PlayerHorizontalMotion.Step(
                Vector3.right * 6f, Vector3.left * 6f, 10f, 20f, 20f, 0.25f, false);

            Assert.That(result.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.z, Is.Zero);
        }

        [Test]
        public void UsesAccelerationForPerpendicularDirectionChange()
        {
            Vector3 result = PlayerHorizontalMotion.Step(
                Vector3.forward * 2f, Vector3.right * 6f, 10f, 20f, 100f, 0.1f, false);
            Vector3 change = result - Vector3.forward * 2f;

            Assert.That(change.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void InstantModeReplacesVelocityAndIgnoresDeltaTime()
        {
            Vector3 result = PlayerHorizontalMotion.Step(
                Vector3.right * 6f, Vector3.forward * 3f + Vector3.up * 8f,
                0f, 0f, 0f, float.NaN, true);

            Assert.That(result, Is.EqualTo(Vector3.forward * 3f));
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaTimePreservesFiniteCurrentVelocity(float deltaTime)
        {
            Vector3 current = new Vector3(2f, 0f, -1f);
            Vector3 result = PlayerHorizontalMotion.Step(
                current, Vector3.right * 6f, 10f, 20f, 30f, deltaTime, false);

            Assert.That(result, Is.EqualTo(current));
        }

        [Test]
        public void NonFiniteVectorsAreRecoveredWithoutPropagatingNaN()
        {
            Vector3 recoveredCurrent = PlayerHorizontalMotion.Step(
                new Vector3(float.NaN, 0f, float.PositiveInfinity),
                Vector3.right,
                10f, 10f, 10f, 0.1f, false);
            Vector3 recoveredTarget = PlayerHorizontalMotion.Step(
                Vector3.right,
                new Vector3(float.NegativeInfinity, 0f, 1f),
                10f, 10f, 10f, 0.1f, false);

            Assert.That(recoveredCurrent.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(recoveredCurrent.z, Is.Zero);
            Assert.That(recoveredTarget, Is.EqualTo(Vector3.zero));
            AssertFinite(recoveredCurrent);
            AssertFinite(recoveredTarget);
        }

        [Test]
        public void ExcessivelyLargeFiniteVectorsAreRejectedSafely()
        {
            Vector3 result = PlayerHorizontalMotion.Step(
                Vector3.zero, Vector3.right * float.MaxValue, 10f, 10f, 10f, 0.1f, true);

            Assert.That(result, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void InvalidAccelerationDoesNotCreateAnUnboundedStep()
        {
            Vector3 current = Vector3.right * 2f;
            Vector3 result = PlayerHorizontalMotion.Step(
                current, Vector3.forward * 6f, float.NaN, 10f, 10f, 0.1f, false);

            Assert.That(result, Is.EqualTo(current));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void AccelerationProducesSameSpeedAtDifferentFrameRates(int framesPerSecond)
        {
            Vector3 velocity = Vector3.zero;
            float deltaTime = 1f / framesPerSecond;
            int frames = Mathf.RoundToInt(0.4f * framesPerSecond);

            for (int i = 0; i < frames; i++)
            {
                velocity = PlayerHorizontalMotion.Step(
                    velocity, Vector3.right * 6f, 8f, 20f, 30f, deltaTime, false);
            }

            Assert.That(velocity.x, Is.EqualTo(3.2f).Within(0.001f));
        }

        [Test]
        public void HorizontalSolverIgnoresVerticalVelocity()
        {
            Vector3 result = PlayerHorizontalMotion.Step(
                new Vector3(1f, 200f, 0f), new Vector3(3f, -100f, 0f),
                10f, 10f, 10f, 0.1f, false);

            Assert.That(result.y, Is.Zero);
            Assert.That(result.x, Is.EqualTo(2f).Within(0.0001f));
        }

        private static void AssertFinite(Vector3 value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
        }
    }
}
