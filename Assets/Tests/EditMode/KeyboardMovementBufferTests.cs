using NUnit.Framework;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class KeyboardMovementBufferTests
    {
        [Test]
        public void TapPressedAndReleasedBeforeReadStillMovesForOneStep()
        {
            KeyboardMovementBuffer input = new KeyboardMovementBuffer();
            input.SetPressed(KeyboardMovementBuffer.Key.Forward, true);
            input.SetPressed(KeyboardMovementBuffer.Key.Forward, false);

            input.Consume(out float horizontal, out float vertical);

            Assert.That(horizontal, Is.Zero);
            Assert.That(vertical, Is.EqualTo(1f));
            input.Consume(out horizontal, out vertical);
            Assert.That(vertical, Is.Zero, "Un tap debe consumirse una sola vez.");
        }

        [Test]
        public void LastOppositeTapWinsWhenBothTapsOccurBeforeRead()
        {
            KeyboardMovementBuffer input = new KeyboardMovementBuffer();
            input.SetPressed(KeyboardMovementBuffer.Key.Left, true);
            input.SetPressed(KeyboardMovementBuffer.Key.Left, false);
            input.SetPressed(KeyboardMovementBuffer.Key.Right, true);
            input.SetPressed(KeyboardMovementBuffer.Key.Right, false);

            input.Consume(out float horizontal, out _);

            Assert.That(horizontal, Is.EqualTo(1f));
        }

        [Test]
        public void LatestPressWinsWhileOppositeKeysOverlap()
        {
            KeyboardMovementBuffer input = new KeyboardMovementBuffer();
            input.SetPressed(KeyboardMovementBuffer.Key.Left, true);
            input.SetPressed(KeyboardMovementBuffer.Key.Right, true);

            input.Consume(out float horizontal, out _);
            Assert.That(horizontal, Is.EqualTo(1f));

            input.SetPressed(KeyboardMovementBuffer.Key.Right, false);
            input.Consume(out horizontal, out _);
            Assert.That(horizontal, Is.EqualTo(-1f), "Al soltar la ganadora vuelve la tecla que sigue mantenida.");
        }

        [Test]
        public void HorizontalAndVerticalTapsCanCombineAsDiagonal()
        {
            KeyboardMovementBuffer input = new KeyboardMovementBuffer();
            input.SetPressed(KeyboardMovementBuffer.Key.Right, true);
            input.SetPressed(KeyboardMovementBuffer.Key.Right, false);
            input.SetPressed(KeyboardMovementBuffer.Key.Forward, true);
            input.SetPressed(KeyboardMovementBuffer.Key.Forward, false);

            input.Consume(out float horizontal, out float vertical);

            Assert.That(horizontal, Is.EqualTo(1f));
            Assert.That(vertical, Is.EqualTo(1f));
        }

        [Test]
        public void ResetClearsHeldKeysAndPendingTaps()
        {
            KeyboardMovementBuffer input = new KeyboardMovementBuffer();
            input.SetPressed(KeyboardMovementBuffer.Key.Back, true);
            input.SetPressed(KeyboardMovementBuffer.Key.Left, true);
            input.Reset();

            input.Consume(out float horizontal, out float vertical);

            Assert.That(horizontal, Is.Zero);
            Assert.That(vertical, Is.Zero);
        }
    }
}
