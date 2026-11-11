using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class PlayerActionModifiersTests
    {
        [Test]
        public void IndependentLocksDoNotOverrideEachOther()
        {
            GameObject player = new GameObject("ActionModifiersTest");
            try
            {
                PlayerActionModifiers modifiers = player.AddComponent<PlayerActionModifiers>();
                int stun = modifiers.AddModifier(
                    PlayerActionModifiers.LockedAction.Movement |
                    PlayerActionModifiers.LockedAction.Dash);
                int silence = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack);

                Assert.That(modifiers.AllowsMovement, Is.False);
                Assert.That(modifiers.AllowsDash, Is.False);
                Assert.That(modifiers.AllowsAttack, Is.False);
                Assert.That(modifiers.AllowsFacing, Is.True);
                Assert.That(modifiers.RemoveModifier(stun), Is.True);
                Assert.That(modifiers.AllowsMovement, Is.True);
                Assert.That(modifiers.AllowsDash, Is.True);
                Assert.That(modifiers.AllowsAttack, Is.False,
                    "Terminar el stun no debe quitar el silencio de otra habilidad.");
                Assert.That(modifiers.RemoveModifier(silence), Is.True);
                Assert.That(modifiers.AllowsAttack, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void SprintAndSlowStackRegardlessOfRegistrationOrder()
        {
            GameObject player = new GameObject("ActionSpeedTest");
            try
            {
                PlayerActionModifiers modifiers = player.AddComponent<PlayerActionModifiers>();
                int sprint = modifiers.AddModifier(speedMultiplier: 2f);
                int slow = modifiers.AddModifier(speedMultiplier: 0.5f);
                Assert.That(modifiers.SpeedMultiplier, Is.EqualTo(1f).Within(0.00001f));
                modifiers.RemoveModifier(sprint);
                modifiers.RemoveModifier(slow);

                slow = modifiers.AddModifier(speedMultiplier: 0.5f);
                sprint = modifiers.AddModifier(speedMultiplier: 2f);
                Assert.That(modifiers.SpeedMultiplier, Is.EqualTo(1f).Within(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void DisablingClearsModifiersAndDoesNotReuseOldTokens()
        {
            GameObject player = new GameObject("ActionLifecycleTest");
            try
            {
                PlayerActionModifiers modifiers = player.AddComponent<PlayerActionModifiers>();
                int oldToken = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack);
                // En EditMode Unity no llama OnDisable al cambiar enabled; se invoca
                // directamente lo mismo que hace OnDisable.
                modifiers.ClearModifiers();
                int newToken = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Movement);
                Assert.That(newToken, Is.Not.EqualTo(oldToken));
                Assert.That(modifiers.RemoveModifier(oldToken), Is.False);
                Assert.That(modifiers.AllowsMovement, Is.False);
                Assert.That(modifiers.RemoveModifier(newToken), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void InvalidModifiersCannotPoisonSimulation()
        {
            GameObject player = new GameObject("ActionInvalidTest");
            try
            {
                PlayerActionModifiers modifiers = player.AddComponent<PlayerActionModifiers>();
                modifiers.AddModifier(speedMultiplier: float.NaN);
                modifiers.AddModifier(speedMultiplier: float.PositiveInfinity);
                Assert.That(modifiers.SpeedMultiplier, Is.EqualTo(1f));
                modifiers.AddModifier(speedMultiplier: -10f);
                Assert.That(modifiers.SpeedMultiplier, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
