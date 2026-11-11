using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class PlayerAnimationDriverTests
    {
        [Test]
        public void MovingWhereYouLookIsForward()
        {
            Vector2 local = PlayerAnimationDriver.LocalMoveDirection(Vector3.forward * 6f, Vector3.forward, 6f);
            Assert.That(local.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(local.x, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void MovingForwardWhileLookingRightIsStrafingLeft()
        {
            Vector2 local = PlayerAnimationDriver.LocalMoveDirection(Vector3.forward * 6f, Vector3.right, 6f);
            Assert.That(local.x, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(PlayerAnimationDriver.ClassifyDodge(local), Is.EqualTo(PlayerAnimationDriver.DodgeDirection.Left));
        }

        [Test]
        public void MovingAwayFromAimIsBackward()
        {
            Vector2 local = PlayerAnimationDriver.LocalMoveDirection(Vector3.back * 3f, Vector3.forward, 6f);
            Assert.That(local.y, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(PlayerAnimationDriver.ClassifyDodge(local), Is.EqualTo(PlayerAnimationDriver.DodgeDirection.Backward));
        }

        [Test]
        public void StandingStillOrInvalidInputIsIdle()
        {
            Assert.That(PlayerAnimationDriver.LocalMoveDirection(Vector3.zero, Vector3.forward, 6f), Is.EqualTo(Vector2.zero));
            Assert.That(PlayerAnimationDriver.LocalMoveDirection(Vector3.forward, Vector3.zero, 6f), Is.EqualTo(Vector2.zero));
            Assert.That(PlayerAnimationDriver.LocalMoveDirection(new Vector3(float.NaN, 0f, 0f), Vector3.forward, 6f), Is.EqualTo(Vector2.zero));
            Assert.That(PlayerAnimationDriver.LocalMoveDirection(Vector3.forward * 20f, Vector3.forward, 6f).magnitude, Is.LessThanOrEqualTo(1.0001f));
        }
    }
}
