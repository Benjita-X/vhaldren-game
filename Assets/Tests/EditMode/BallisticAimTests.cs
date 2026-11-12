using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class BallisticAimTests
    {
        private const float Speed = 34f;
        private const float GravityScale = 0.45f;

        /// <summary>Simula la flecha con la misma integracion que PhysicalArrowProjectile.</summary>
        private static float HeightWhenReaching(Vector3 origin, Vector3 direction, float horizontalDistance)
        {
            Vector3 position = origin;
            Vector3 velocity = direction * Speed;
            const float dt = 0.002f;
            for (int i = 0; i < 20000; i++)
            {
                velocity += Physics.gravity * GravityScale * dt;
                position += velocity * dt;
                Vector3 planar = position - origin;
                planar.y = 0f;
                if (planar.magnitude >= horizontalDistance)
                    return position.y;
            }
            return float.NaN;
        }

        [Test]
        public void ArrowArcsButReachesChestHeightAtTheCursor()
        {
            Vector3 origin = new Vector3(0f, 1.3f, 0f);
            Vector3 groundPoint = new Vector3(0f, 0f, 20f);   // cursor sobre el suelo
            Vector3 direction = LocalPlayerShooter.BallisticDirection(origin, groundPoint, Speed, GravityScale, Vector3.forward);
            Assert.That(direction.y, Is.GreaterThan(0f), "Sale algo hacia arriba para compensar la caida.");
            float height = HeightWhenReaching(origin, direction, 20f);
            Assert.That(height, Is.EqualTo(0.95f).Within(0.12f), "Llega a la altura del pecho, no al suelo.");
        }

        [Test]
        public void AimingAtABodyHitsThatPoint()
        {
            Vector3 origin = new Vector3(0f, 1.3f, 0f);
            Vector3 chest = new Vector3(6f, 1.4f, 9f);
            Vector3 direction = LocalPlayerShooter.BallisticDirection(origin, chest, Speed, GravityScale, Vector3.forward);
            Vector3 planar = chest - origin;
            planar.y = 0f;
            float height = HeightWhenReaching(origin, direction, planar.magnitude);
            Assert.That(height, Is.EqualTo(1.4f).Within(0.08f));
        }

        [Test]
        public void WithoutGravityItShootsStraight()
        {
            Vector3 fallback = new Vector3(0f, -0.2f, 1f).normalized;
            Assert.That(LocalPlayerShooter.BallisticDirection(Vector3.zero, Vector3.forward * 10f, Speed, 0f, fallback),
                Is.EqualTo(fallback));
        }
    }
}
