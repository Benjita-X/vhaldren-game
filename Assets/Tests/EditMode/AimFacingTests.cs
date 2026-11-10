using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class AimFacingTests
    {
        private static readonly Vector3 Origin = new Vector3(2f, 1f, -3f);

        [Test]
        public void FacesThePointUnderTheCursorIgnoringHeight()
        {
            Vector3 facing = LocalPlayerInput.ResolveFacing(
                Origin, Origin + new Vector3(4f, -1f, 0f), Vector3.forward, 0.35f);

            Assert.That(facing.y, Is.Zero);
            Assert.That(facing.normalized.x, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void KeepsCurrentFacingInsideDeadZone()
        {
            // El cursor sobre el propio personaje: un punto del suelo a pocos centimetros.
            Vector3 facing = LocalPlayerInput.ResolveFacing(
                Origin, Origin + new Vector3(-0.1f, -1f, 0.05f), Vector3.right, 0.35f);

            Assert.That(facing, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void PointDirectlyBelowDoesNotProduceAZeroDirection()
        {
            Vector3 facing = LocalPlayerInput.ResolveFacing(
                Origin, Origin + Vector3.down, Vector3.back, 0f);

            Assert.That(facing, Is.EqualTo(Vector3.back));
        }

        [Test]
        public void NonFiniteAimPointKeepsCurrentFacing()
        {
            Vector3 facing = LocalPlayerInput.ResolveFacing(
                Origin, new Vector3(float.NaN, 0f, float.PositiveInfinity), Vector3.left, 0.35f);

            Assert.That(facing, Is.EqualTo(Vector3.left));
        }
    }
}
