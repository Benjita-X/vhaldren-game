using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Races;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Tests
{
    public sealed class MenuAndRaceTests
    {
        [TearDown]
        public void ResetKeys() => GameInput.ResetDefaults();

        [Test]
        public void NineLineagesWithLuciferFirstAndCompleteSheets()
        {
            // Las 8 razas de siempre más Lucifer, que va primero y es el linaje por defecto.
            Assert.That(RaceCatalog.All.Count, Is.EqualTo(9));
            Assert.That(RaceCatalog.All[0].Id, Is.EqualTo(RaceId.Lucifer), "Lucifer encabeza el menú.");
            Assert.That(RaceCatalog.Default, Is.EqualTo(RaceId.Lucifer));
            foreach (RaceId id in System.Enum.GetValues(typeof(RaceId)))
            {
                Assert.That(RaceCatalog.IsDefined(id), Is.True, id.ToString());
                Assert.That(RaceCatalog.Get(id).Id, Is.EqualTo(id), "Get busca por identificador, no por posición.");
            }
            HashSet<RacialPower> powers = new HashSet<RacialPower>();
            foreach (RaceDefinition race in RaceCatalog.All)
            {
                Assert.That(string.IsNullOrEmpty(race.Name), Is.False);
                Assert.That(race.Affinity.Length, Is.EqualTo(SpellCatalog.SchoolCount), race.Name);
                Assert.That(race.Health, Is.InRange(0.7f, 1.4f));
                Assert.That(race.PowerCooldown, Is.GreaterThan(10f));
                Assert.That(race.Strengths.Length, Is.GreaterThan(0));
                Assert.That(race.Weaknesses.Length, Is.GreaterThan(0), race.Name + " necesita alguna debilidad.");
                Assert.That(powers.Add(race.Power), Is.True, "Cada raza tiene su propio poder.");
            }
        }

        [Test]
        public void RebindingSwapsKeysAndResetRestoresDefaults()
        {
            GameInput.ResetDefaults();
            Assert.That(GameInput.KeyFor(GameAction.Sword), Is.EqualTo(Key.V));
            GameInput.Rebind(GameAction.Sword, Key.T);
            Assert.That(GameInput.KeyFor(GameAction.Sword), Is.EqualTo(Key.T));
            Assert.That(GameInput.KeyFor(GameAction.RunePulse), Is.EqualTo(Key.V), "La accion que usaba T recibe la V.");
            GameInput.ResetDefaults();
            Assert.That(GameInput.KeyFor(GameAction.Sword), Is.EqualTo(Key.V));
            Assert.That(GameInput.KeyFor(GameAction.RunePulse), Is.EqualTo(Key.T));
        }

        [Test]
        public void DefaultSharedKeysOnlyExistInMutuallyExclusiveContexts()
        {
            GameInput.ResetDefaults();
            Dictionary<Key, GameAction> used = new Dictionary<Key, GameAction>();
            foreach (GameInput.Binding binding in GameInput.All)
            {
                Key key = GameInput.KeyFor(binding.Action);
                if (used.TryGetValue(key, out GameAction other))
                {
                    bool buildAndSpell =
                        IsBuildSlot(other) && IsSpell(binding.Action) ||
                        IsBuildSlot(binding.Action) && IsSpell(other);
                    bool buildAndRace = key == Key.R &&
                        (other == GameAction.RacialPower || binding.Action == GameAction.RacialPower);
                    // G: montar fuera de construcción, desmontar pieza dentro (nunca a la vez).
                    bool mountAndDismantle = key == Key.G &&
                        (other == GameAction.Mount || binding.Action == GameAction.Mount);
                    Assert.That(buildAndSpell || buildAndRace || mountAndDismantle, Is.True,
                        binding.Action + " choca con " + other + " fuera de un contexto exclusivo.");
                }
                else
                    used[key] = binding.Action;
            }
        }

        [Test]
        public void ConstructionSlotsAreRebindableWithoutStealingSpellKeys()
        {
            GameInput.ResetDefaults();
            Assert.That(GameInput.KeyFor(GameAction.BuildSlot1), Is.EqualTo(Key.Digit1));
            Assert.That(GameInput.KeyFor(GameAction.Spell1), Is.EqualTo(Key.Digit1));
            GameInput.Rebind(GameAction.BuildSlot1, Key.Digit4);
            Assert.That(GameInput.KeyFor(GameAction.BuildSlot1), Is.EqualTo(Key.Digit4));
            Assert.That(GameInput.KeyFor(GameAction.BuildSlot4), Is.EqualTo(Key.Digit1),
                "Las ranuras de construcción se intercambian entre sí.");
            Assert.That(GameInput.KeyFor(GameAction.Spell1), Is.EqualTo(Key.Digit1),
                "El hechizo conserva su tecla porque magia y construcción son contextos excluyentes.");
        }

        private static bool IsBuildSlot(GameAction action) =>
            action >= GameAction.BuildSlot1 && action <= GameAction.BuildSlot10;

        private static bool IsSpell(GameAction action) =>
            action >= GameAction.Spell1 && action <= GameAction.Spell3;

        [Test]
        public void CodexHasContentForEveryTab()
        {
            for (int tab = 0; tab < 8; tab++)
                Assert.That(CodexPanel.Build(tab).Length, Is.GreaterThan(200), "Pestaña " + tab);
            StringAssert.Contains("Meteoro", CodexPanel.Build(2));
            StringAssert.Contains("VAMPIRO", CodexPanel.Build(5));
            StringAssert.Contains("Bruto de las canteras", CodexPanel.Build(6));
        }
    }
}
