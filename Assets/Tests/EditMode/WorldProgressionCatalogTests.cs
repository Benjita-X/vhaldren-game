using NUnit.Framework;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    public sealed class WorldProgressionCatalogTests
    {
        [Test]
        public void ThreeRealmsHaveDistinctScenesAndValidCaveArchetypes()
        {
            Assert.That(WorldProgressionCatalog.Realms.Count, Is.EqualTo(3));
            Assert.That(WorldProgressionCatalog.ValidateArchetypes(), Is.True);
            Assert.That(WorldProgressionCatalog.FindRealm(RealmKind.Earth).EntryScene,
                Is.EqualTo("WorldPrototype001"));
            Assert.That(WorldProgressionCatalog.FindRealm(RealmKind.Celestial).EntryScene,
                Is.Not.EqualTo(WorldProgressionCatalog.FindRealm(RealmKind.Infernal).EntryScene));
        }

        [Test]
        public void TransformationsRespectAncestryAndDoNotUnlockAllFormsForHybrids()
        {
            Assert.That(WorldProgressionCatalog.CanUnlockForm(LineageKind.Lycan, FormKind.Wolf),
                Is.True);
            Assert.That(WorldProgressionCatalog.CanUnlockForm(LineageKind.Lycan, FormKind.Bat),
                Is.False);
            Assert.That(WorldProgressionCatalog.CanUnlockForm(LineageKind.Vampire, FormKind.Bat),
                Is.True);
            Assert.That(WorldProgressionCatalog.CanUnlockForm(LineageKind.Vampire, FormKind.NightMist),
                Is.True);
            Assert.That(WorldProgressionCatalog.CanUnlockForm(
                LineageKind.HalfDemon, FormKind.InfernalAspect), Is.True);
            Assert.That(WorldProgressionCatalog.CanUnlockForm(
                LineageKind.Hybrid, FormKind.DireWolf), Is.False);
        }
    }
}
