using NUnit.Framework;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class OpposingKeysAxisTests
    {
        [Test]
        public void SingleKeyGivesItsDirection()
        {
            OpposingKeysAxis axis = default;

            Assert.That(axis.Resolve(true, false), Is.EqualTo(-1f));
            Assert.That(axis.Resolve(false, false), Is.Zero);
            Assert.That(axis.Resolve(false, true), Is.EqualTo(1f));
        }

        [Test]
        public void LastPressedKeyWinsWhileBothAreHeld()
        {
            OpposingKeysAxis axis = default;

            Assert.That(axis.Resolve(true, false), Is.EqualTo(-1f));
            // Se pulsa D antes de soltar A: debe ir a la derecha, no detenerse.
            Assert.That(axis.Resolve(true, true), Is.EqualTo(1f));
            Assert.That(axis.Resolve(true, true), Is.EqualTo(1f));
        }

        [Test]
        public void ReleasingTheWinnerReturnsToTheKeyStillHeld()
        {
            OpposingKeysAxis axis = default;
            axis.Resolve(true, false);
            axis.Resolve(true, true);

            Assert.That(axis.Resolve(true, false), Is.EqualTo(-1f));
        }

        [Test]
        public void RapidAlternationNeverStopsWhileAKeyIsHeld()
        {
            OpposingKeysAxis axis = default;
            // Secuencia real al machacar A y D: siempre hay al menos una tecla pulsada.
            bool[,] frames =
            {
                { true, false }, { true, true }, { false, true }, { true, true },
                { true, false }, { true, true }, { false, true }, { true, true }
            };

            for (int i = 0; i < frames.GetLength(0); i++)
            {
                float value = axis.Resolve(frames[i, 0], frames[i, 1]);
                Assert.That(value, Is.Not.Zero, "Lectura " + i + " dejo al personaje quieto.");
            }
        }

        [Test]
        public void RePressingAHeldDirectionTakesPriorityAgain()
        {
            OpposingKeysAxis axis = default;
            axis.Resolve(true, false);
            axis.Resolve(true, true);
            axis.Resolve(false, true);

            Assert.That(axis.Resolve(true, true), Is.EqualTo(-1f));
        }

        [Test]
        public void BothPressedOnTheSameReadingStayNeutralUntilOneIsReleased()
        {
            OpposingKeysAxis axis = default;

            Assert.That(axis.Resolve(true, true), Is.Zero);
            Assert.That(axis.Resolve(false, true), Is.EqualTo(1f));
        }

        [Test]
        public void ResetForgetsHeldKeys()
        {
            OpposingKeysAxis axis = default;
            axis.Resolve(true, false);
            axis.Resolve(true, true);
            axis.Reset();

            Assert.That(axis.Resolve(false, false), Is.Zero);
            Assert.That(axis.Resolve(true, false), Is.EqualTo(-1f));
        }
    }
}
