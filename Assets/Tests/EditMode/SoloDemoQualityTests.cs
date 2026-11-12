using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    /// <summary>Regresiones para curacion visible y sandbox individual.</summary>
    public sealed class SoloDemoQualityTests
    {
        [TestCase(75f, 100f, 35f, 25f)]
        [TestCase(45f, 100f, 35f, 35f)]
        [TestCase(100f, 100f, 35f, 0f)]
        [TestCase(1f, 100f, 35f, 35f)]
        [TestCase(0f, 100f, 35f, 0f)]
        [TestCase(80f, 100f, 0f, 0f)]
        public void HealingPreviewNeverPromisesMoreThanActualRecovery(
            float current, float maximum, float requested, float expected)
        {
            Assert.That(PrototypeGameSession.PreviewHealing(current, maximum, requested),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void HealingPreviewRejectsNonFiniteValues()
        {
            Assert.That(PrototypeGameSession.PreviewHealing(float.NaN, 100f, 35f), Is.Zero);
            Assert.That(PrototypeGameSession.PreviewHealing(40f, float.PositiveInfinity, 35f), Is.Zero);
            Assert.That(PrototypeGameSession.PreviewHealing(40f, 100f, float.NaN), Is.Zero);
        }

        [Test]
        public void CreativeBypassesCostsButNotPlacementRules()
        {
            BuildPieceDefinition door = BuildCatalog.Find("puerta");
            Vector3 spot = new Vector3(2000f, 500f, 2000f);
            Assert.That(BuildPlacement.Evaluate(door, true, spot, 0, spot,
                null, creativeMode: false), Is.EqualTo(PlacementResult.NeedsWorkbench));
            Assert.That(BuildPlacement.Evaluate(door, true, spot, 0, spot,
                null, creativeMode: true), Is.EqualTo(PlacementResult.Ok));
            Assert.That(BuildPlacement.Evaluate(door, false, spot, 0, spot,
                null, creativeMode: true), Is.EqualTo(PlacementResult.NoSurface));
            Assert.That(BuildPlacement.Evaluate(door, true, spot,
                0, spot + Vector3.forward * 100f, null, creativeMode: true),
                Is.EqualTo(PlacementResult.TooFar));
        }
    }
}
