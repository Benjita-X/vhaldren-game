using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Races;

namespace Vhaldren.Tests
{
    /// <summary>Lucifer, el Lucero caído: linaje por defecto, kit de Luz caída y alas.</summary>
    public sealed class LuciferKitTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void LuciferSheetHasItsOwnPowerImmunityAndKit()
        {
            RaceDefinition lucifer = RaceCatalog.Get(RaceId.Lucifer);
            StringAssert.Contains("Lucifer", lucifer.Name);
            Assert.That(lucifer.MenuName, Is.EqualTo("Lucifer"), "El botón de comenzar usa el nombre corto.");
            Assert.That(lucifer.Power, Is.EqualTo(RacialPower.WingBurst));
            Assert.That(lucifer.PowerName, Is.EqualTo("Estallido de alas"));
            Assert.That(lucifer.PowerCooldown, Is.InRange(15f, 30f));
            Assert.That(System.Array.IndexOf(lucifer.Immunities, StatusKind.Ignition), Is.GreaterThanOrEqualTo(0),
                "El fuego infernal no quema a quien lo trae.");
            Assert.That(lucifer.ExtraSchools, Is.Empty, "Su escuela exclusiva no se aprende: la concede el linaje.");
            Assert.That(lucifer.AffinityFor(SpellSchool.FallenLight), Is.EqualTo(1f));
            Assert.That(RaceCatalog.Get(RaceId.Vampire).AffinityFor((SpellSchool)99), Is.EqualTo(1f),
                "Una escuela fuera de rango no toma la afinidad de otra.");
        }

        [Test]
        public void SelectionDefaultsToLuciferAndIgnoresUnknownValues()
        {
            bool had = PlayerPrefs.HasKey(RaceSelection.Key);
            int previous = PlayerPrefs.GetInt(RaceSelection.Key, 0);
            try
            {
                PlayerPrefs.DeleteKey(RaceSelection.Key);
                Assert.That(RaceSelection.Current, Is.EqualTo(RaceId.Lucifer));
                PlayerPrefs.SetInt(RaceSelection.Key, 99);
                Assert.That(RaceSelection.Current, Is.EqualTo(RaceId.Lucifer));
                RaceSelection.Current = RaceId.Werewolf;
                Assert.That(RaceSelection.Current, Is.EqualTo(RaceId.Werewolf), "Las demás razas siguen disponibles.");
            }
            finally
            {
                if (had)
                    PlayerPrefs.SetInt(RaceSelection.Key, previous);
                else
                    PlayerPrefs.DeleteKey(RaceSelection.Key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void FallenLightNumbersAreBalancedWithTheOtherSchools()
        {
            SpellDefinition lance = SpellCatalog.Get(SpellSchool.FallenLight, 0);
            SpellDefinition leap = SpellCatalog.Get(SpellSchool.FallenLight, 1);
            SpellDefinition star = SpellCatalog.Get(SpellSchool.FallenLight, 2);
            Assert.That(lance.Id, Is.EqualTo("lanza_lucero"));
            Assert.That(leap.Delivery, Is.EqualTo(SpellDelivery.Leap));
            Assert.That(star.Delivery, Is.EqualTo(SpellDelivery.Zone));
            // Básicos 15-18 de daño, definitivos 48-60 (como el resto del grimorio).
            Assert.That(lance.Damage, Is.InRange(15f, 18f));
            Assert.That(lance.Charges, Is.EqualTo(2));
            Assert.That(lance.Pierce, Is.EqualTo(3), "Atraviesa hasta tres enemigos.");
            Assert.That(star.Damage, Is.InRange(48f, 60f));
            Assert.That(star.Radius, Is.GreaterThan(SpellCatalog.Find("meteoro").Radius), "Área grande.");
            Assert.That(star.Windup, Is.GreaterThanOrEqualTo(1.2f), "Se avisa con tiempo para esquivarla.");
            Assert.That(leap.Cooldown, Is.InRange(lance.Cooldown + 1f, star.Cooldown - 1f));
            foreach (SpellDefinition spell in new[] { lance, leap, star })
            {
                Assert.That(spell.Status, Is.EqualTo(StatusKind.Ignition), spell.Id + " quema.");
                Assert.That(spell.StaminaCost, Is.InRange(8f, 40f));
            }
            Assert.That(FallenLightKit.WingBurstDamage, Is.InRange(20f, 40f));
            Assert.That(FallenLightKit.WingBurstForce, Is.GreaterThan(1.5f), "El estallido de alas empuja lejos.");
            Assert.That(FallenLightKit.LeapRootSeconds, Is.InRange(1f, 2f));
        }

        [Test]
        public void LanceLosesStrengthEachTimeItPierces()
        {
            Assert.That(FallenLance.PierceScale(0), Is.EqualTo(1f));
            Assert.That(FallenLance.PierceScale(1), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(FallenLance.PierceScale(2), Is.EqualTo(0.65f).Within(0.001f));
            for (int i = 1; i < 8; i++)
            {
                Assert.That(FallenLance.PierceScale(i), Is.LessThanOrEqualTo(FallenLance.PierceScale(i - 1)));
                Assert.That(FallenLance.PierceScale(i), Is.GreaterThanOrEqualTo(0.5f));
            }
        }

        [Test]
        public void WingLeapArcStartsAndEndsOnTheGroundAndPeaksInTheMiddle()
        {
            Vector3 start = new Vector3(0f, 1f, 0f);
            Vector3 end = new Vector3(0f, 1f, 8f);
            float apex = FallenLightKit.LeapApex(8f);
            Assert.That(Vector3.Distance(FallenLightKit.LeapArc(start, end, 0f, apex), start), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(FallenLightKit.LeapArc(start, end, 1f, apex), end), Is.LessThan(0.001f));
            Vector3 middle = FallenLightKit.LeapArc(start, end, 0.5f, apex);
            Assert.That(middle.y, Is.EqualTo(1f + apex).Within(0.001f));
            Assert.That(apex, Is.GreaterThan(2f), "Sube lo bastante para pasar por encima de vallas y muros bajos.");
            Assert.That(FallenLightKit.LeapDuration(8f), Is.InRange(0.4f, 0.8f));

            // Hacia el cursor: mínimo 2,5 m, máximo el alcance; cursor encima = alcance completo.
            Assert.That(FallenLightKit.LeapDistance(Vector3.zero, new Vector3(1f, 0f, 0f), 9f), Is.EqualTo(2.5f));
            Assert.That(FallenLightKit.LeapDistance(Vector3.zero, new Vector3(0f, 0f, 30f), 9f), Is.EqualTo(9f));
            Assert.That(FallenLightKit.LeapDistance(Vector3.zero, new Vector3(0.1f, 3f, 0f), 9f), Is.EqualTo(9f));
        }

        [Test]
        public void FallingStarHitsHardestInTheCentre()
        {
            Assert.That(FallenLightKit.StarFalloff(0f, 5f), Is.EqualTo(1f));
            Assert.That(FallenLightKit.StarFalloff(5f, 5f), Is.EqualTo(FallenLightKit.StarEdgeDamage).Within(0.001f));
            Assert.That(FallenLightKit.StarFalloff(9f, 5f), Is.EqualTo(FallenLightKit.StarEdgeDamage).Within(0.001f));
            Assert.That(FallenLightKit.StarFalloff(2.5f, 5f), Is.InRange(FallenLightKit.StarEdgeDamage, 1f));
        }

        [Test]
        public void WingsFoldBehindTheBackAndOpenSideways()
        {
            // Ala izquierda modelada hacia -X y derecha hacia +X (raíz en el origen).
            LuciferAvatar.WingAngles(0f, 0f, 0f, 0f, out float foldSweep, out float foldDroop);
            Vector3 leftFolded = LuciferAvatar.WingPose(true, foldSweep, foldDroop) * Vector3.left;
            Vector3 rightFolded = LuciferAvatar.WingPose(false, foldSweep, foldDroop) * Vector3.right;
            Assert.That(leftFolded.z, Is.LessThan(-0.5f), "Plegada: hacia atrás.");
            Assert.That(rightFolded.z, Is.LessThan(-0.5f));
            Assert.That(leftFolded.y, Is.LessThan(0f), "Plegada: algo caída.");
            Assert.That(Mathf.Abs(leftFolded.x + rightFolded.x), Is.LessThan(0.001f), "Simétricas.");

            LuciferAvatar.WingAngles(1f, 0f, 0f, 0f, out float openSweep, out float openDroop);
            Vector3 leftOpen = LuciferAvatar.WingPose(true, openSweep, openDroop) * Vector3.left;
            Vector3 rightOpen = LuciferAvatar.WingPose(false, openSweep, openDroop) * Vector3.right;
            Assert.That(leftOpen.x, Is.LessThan(-0.9f), "Abierta: hacia el lado.");
            Assert.That(rightOpen.x, Is.GreaterThan(0.9f));
            Assert.That(leftOpen.y, Is.GreaterThan(0f), "Abierta: levantada.");
        }

        [Test]
        public void WingsBeatWhenFlappingAndBreatheAtRest()
        {
            LuciferAvatar.WingAngles(1f, Mathf.PI * 0.5f, 1f, 0f, out _, out float down);
            LuciferAvatar.WingAngles(1f, -Mathf.PI * 0.5f, 1f, 0f, out _, out float up);
            Assert.That(down - up, Is.GreaterThan(60f), "El aleteo recorre más de 60°.");
            LuciferAvatar.WingAngles(0f, 0f, 0f, Mathf.PI * 0.5f, out _, out float inhale);
            LuciferAvatar.WingAngles(0f, 0f, 0f, -Mathf.PI * 0.5f, out _, out float exhale);
            Assert.That(inhale - exhale, Is.InRange(2f, 10f), "En reposo respiran poco.");
            Assert.That(LuciferAvatar.RestoredFraction(0f), Is.EqualTo(0.2f).Within(0.001f), "Empieza con las alas rotas.");
            Assert.That(LuciferAvatar.RestoredFraction(10000f), Is.EqualTo(1f));
        }

        [Test]
        public void OnlyLuciferCanLearnFallenLight()
        {
            root = new GameObject("Grimorio");
            PlayerSpellbook book = root.AddComponent<PlayerSpellbook>();
            Assert.That(book.Unlock(SpellSchool.FallenLight), Is.False, "Ninguna esencia enseña la Luz caída.");
            Assert.That(book.IsUnlocked(SpellSchool.FallenLight), Is.False);
            Assert.That(book.AvailableSchoolCount, Is.EqualTo(6));

            book.GrantExclusive(SpellSchool.FallenLight, true);
            Assert.That(book.IsUnlocked(SpellSchool.FallenLight), Is.True);
            Assert.That(book.School, Is.EqualTo(SpellSchool.FallenLight), "Lucifer empieza con su kit activo.");
            Assert.That(book.AvailableSchoolCount, Is.EqualTo(7));

            book.RevokeExclusive(SpellSchool.FallenLight);
            Assert.That(book.IsUnlocked(SpellSchool.FallenLight), Is.False);
            Assert.That(book.School, Is.Not.EqualTo(SpellSchool.FallenLight));
        }

        [Test]
        public void HitContextScopesNestAndRestore()
        {
            Assert.That(CombatHitContext.Critical, Is.False);
            Assert.That(CombatHitContext.Force, Is.EqualTo(1f));
            using (CombatHitContext.Push(true, 2.5f))
            {
                Assert.That(CombatHitContext.Critical, Is.True);
                Assert.That(CombatHitContext.Force, Is.EqualTo(2.5f));
                using (CombatHitContext.Push(false, 99f))
                {
                    Assert.That(CombatHitContext.Critical, Is.False);
                    Assert.That(CombatHitContext.Force, Is.EqualTo(CombatHitContext.MaxForce), "La fuerza tiene tope.");
                }
                Assert.That(CombatHitContext.Critical, Is.True);
                Assert.That(CombatHitContext.Force, Is.EqualTo(2.5f));
            }
            Assert.That(CombatHitContext.Critical, Is.False);
            Assert.That(CombatHitContext.Force, Is.EqualTo(1f));
        }
    }
}
