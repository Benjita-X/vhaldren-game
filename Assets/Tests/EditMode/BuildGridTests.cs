using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Survival;

namespace Vhaldren.Tests
{
    public sealed class BuildGridTests
    {
        [Test]
        public void CatalogHasTenUniquePiecesWithValidCosts()
        {
            Assert.That(BuildCatalog.Pieces.Count, Is.EqualTo(10), "Las teclas 1-0 deben cubrir todo el catalogo.");
            HashSet<string> ids = new HashSet<string>();
            foreach (BuildPieceDefinition piece in BuildCatalog.Pieces)
            {
                Assert.That(ids.Add(piece.Id), Is.True, "Id repetido: " + piece.Id);
                Assert.That(piece.Cost.Count, Is.GreaterThan(0), piece.Id + " debe costar algo.");
                foreach (ResourceIngredient cost in piece.Cost)
                    Assert.That(cost.Amount, Is.GreaterThan(0));
                Assert.That(piece.Size.x > 0f && piece.Size.y > 0f && piece.Size.z > 0f, Is.True);
            }
            Assert.That(BuildCatalog.Find("cofre"), Is.Not.Null);
            Assert.That(BuildCatalog.Find("no-existe"), Is.Null);
        }

        [Test]
        public void RotationWrapsAroundInBothDirections()
        {
            Assert.That(BuildGrid.NormalizeRotation(4), Is.EqualTo(0));
            Assert.That(BuildGrid.NormalizeRotation(-1), Is.EqualTo(3));
            Assert.That(BuildGrid.NormalizeRotation(7), Is.EqualTo(3));
        }

        [Test]
        public void NormalPiecesSnapToCellCenterAndKeepAimedHeight()
        {
            BuildPieceDefinition foundation = BuildCatalog.Find("cimiento");
            Vector3 snapped = BuildGrid.Snap(new Vector3(0.3f, 1.7f, -0.2f), foundation, 0, false);
            Assert.That(snapped, Is.EqualTo(new Vector3(1f, 1.7f, -1f)));
        }

        [Test]
        public void WallsSnapToCellEdgesAccordingToRotation()
        {
            BuildPieceDefinition wall = BuildCatalog.Find("pared");
            // Largo en X: centro de celda en X, linea de rejilla en Z.
            Assert.That(BuildGrid.Snap(new Vector3(0.3f, 0f, 1.7f), wall, 0, false), Is.EqualTo(new Vector3(1f, 0f, 2f)));
            // Girada 90 grados: linea de rejilla en X, centro de celda en Z.
            Assert.That(BuildGrid.Snap(new Vector3(1.7f, 0f, 0.3f), wall, 1, false), Is.EqualTo(new Vector3(2f, 0f, 1f)));
        }

        [Test]
        public void FreePlacementRoundsToQuarterMeterAndRejectsNaN()
        {
            BuildPieceDefinition chest = BuildCatalog.Find("cofre");
            Assert.That(BuildGrid.Snap(new Vector3(0.37f, 0f, -0.88f), chest, 0, true), Is.EqualTo(new Vector3(0.25f, 0f, -1f)));
            Assert.That(BuildGrid.Snap(new Vector3(float.NaN, 0f, 0f), chest, 0, false), Is.EqualTo(Vector3.zero));
        }
    }
}
