using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.NPC;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    /// <summary>Respuesta del combate (empujón, parón, números, reacción, sueño) y aldeanos.</summary>
    public sealed class CombatFeelAndNpcTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        // ── Respuesta del golpe ────────────────────────────────────────

        [Test]
        public void KnockbackGrowsWithTheHitAndShrinksWithTheTarget()
        {
            float light = GameFeel.KnockbackDistance(10f, 85f, CombatDamageKind.Melee, false, false, 1f);
            float heavy = GameFeel.KnockbackDistance(40f, 85f, CombatDamageKind.Melee, false, false, 1f);
            Assert.That(heavy, Is.GreaterThan(light * 2f), "Retroceso proporcional al golpe.");
            float wolf = GameFeel.KnockbackDistance(30f, 55f, CombatDamageKind.Melee, false, false, 1f);
            float brute = GameFeel.KnockbackDistance(30f, 200f, CombatDamageKind.Melee, false, false, 1f);
            Assert.That(wolf, Is.GreaterThan(brute * 2f), "Un bruto apenas se mueve; un lobo sale despedido.");
            Assert.That(GameFeel.KnockbackDistance(30f, 85f, CombatDamageKind.Melee, true, false, 1f),
                Is.LessThan(heavy * 0.5f), "El jefe resiste el empuje.");
            Assert.That(GameFeel.KnockbackDistance(30f, 85f, CombatDamageKind.Melee, false, true, 1f),
                Is.GreaterThan(GameFeel.KnockbackDistance(30f, 85f, CombatDamageKind.Melee, false, false, 1f)));
            Assert.That(GameFeel.KnockbackDistance(30f, 85f, CombatDamageKind.Magic, false, false, 3f),
                Is.GreaterThan(1.5f), "El estallido de alas (fuerza 3) empuja lejos.");
            Assert.That(GameFeel.KnockbackDistance(500f, 10f, CombatDamageKind.Explosion, false, true, 4f),
                Is.LessThanOrEqualTo(3f));
            Assert.That(GameFeel.KnockbackDistance(10f, 85f, CombatDamageKind.DamageOverTime, false, false, 1f),
                Is.Zero, "La quemadura no empuja cada segundo.");
        }

        [Test]
        public void HitStopIsShortForLightHitsAndLongerForCriticalsAndKills()
        {
            Assert.That(GameFeel.HitStopSeconds(5f, false, false), Is.LessThan(0.045f));
            Assert.That(GameFeel.HitStopSeconds(40f, false, false), Is.GreaterThan(GameFeel.HitStopSeconds(10f, false, false)));
            Assert.That(GameFeel.HitStopSeconds(5f, true, false), Is.GreaterThanOrEqualTo(0.11f));
            Assert.That(GameFeel.HitStopSeconds(5f, false, true), Is.GreaterThanOrEqualTo(0.12f));
            Assert.That(GameFeel.HitStopSeconds(999f, true, true), Is.LessThanOrEqualTo(0.15f), "Nunca congela de más.");
            Assert.That(GameFeel.DeathImpulse(80f, 85f, false, 1f, false),
                Is.GreaterThan(GameFeel.DeathImpulse(5f, 85f, false, 1f, false)), "Rematar fuerte lanza más lejos.");
            Assert.That(GameFeel.DeathImpulse(80f, 85f, false, 1f, true),
                Is.LessThan(GameFeel.DeathImpulse(80f, 85f, false, 1f, false)), "El jefe cae con peso.");
        }

        [Test]
        public void DamageNumbersAreRoundedAndCriticalsStandOut()
        {
            Assert.That(CombatDamageFeedback.PopupText(23.4f, false, false), Is.EqualTo("-23"));
            Assert.That(CombatDamageFeedback.PopupText(57.6f, true, false), Is.EqualTo("¡58!"));
            Assert.That(CombatDamageFeedback.PopupText(12f, false, true), Is.EqualTo("+12"));
            Assert.That(CombatDamageFeedback.PopupText(0.4f, false, false), Is.EqualTo("-0.4").Or.EqualTo("-0,4"));
            float normal = CombatDamageFeedback.PopupScale(20f, 85f, false, false);
            float critical = CombatDamageFeedback.PopupScale(20f, 85f, true, false);
            Assert.That(critical, Is.GreaterThan(normal * 1.3f), "Los críticos se leen mucho más grandes.");
            Assert.That(CombatDamageFeedback.PopupScale(60f, 85f, false, false), Is.GreaterThan(normal));
            Assert.That(CombatDamageFeedback.PopupScale(5f, 85f, false, true), Is.LessThan(normal), "La quemadura, discreta.");
        }

        [Test]
        public void HitReactionScalesWithDamage()
        {
            Assert.That(EnemyHitReaction.ReactionStrength(0f, 85f, false), Is.Zero);
            float small = EnemyHitReaction.ReactionStrength(8f, 85f, false);
            float big = EnemyHitReaction.ReactionStrength(40f, 85f, false);
            Assert.That(big, Is.GreaterThan(small));
            Assert.That(EnemyHitReaction.ReactionStrength(8f, 85f, true), Is.GreaterThan(small));
            Assert.That(EnemyHitReaction.ReactionStrength(999f, 85f, true), Is.LessThanOrEqualTo(1.8f));
        }

        [Test]
        public void FarEnemiesSleepAndWakeWithHysteresis()
        {
            Assert.That(PrototypeEnemy.ShouldSleep(80f, false, false), Is.True, "A más de 70 m duerme.");
            Assert.That(PrototypeEnemy.ShouldSleep(80f, false, true), Is.False, "No duerme a mitad de un ataque.");
            Assert.That(PrototypeEnemy.ShouldSleep(40f, false, false), Is.False);
            Assert.That(PrototypeEnemy.ShouldSleep(65f, true, false), Is.True, "Entre 60 y 70 m sigue como estaba.");
            Assert.That(PrototypeEnemy.ShouldSleep(65f, false, false), Is.False);
            Assert.That(PrototypeEnemy.ShouldSleep(50f, true, false), Is.False, "Al acercarse despierta.");
            Assert.That(PrototypeEnemy.ShouldSleep(float.NaN, true, false), Is.True);
            Assert.That(PrototypeEnemy.WakeDistance, Is.LessThan(PrototypeEnemy.SleepDistance));
            Assert.That(PrototypeEnemy.SleepDistance, Is.InRange(60f, 80f));
        }

        [Test]
        public void BestiaryMeetsTheBalanceTargets()
        {
            // Documento 023: enemigo común en 3-4 tajos (espada 35 con equipo inicial nivel 1)
            // y el jugador (150 PV) aguanta 6-9 golpes de saqueador.
            EnemyArchetype raider = EnemyArchetypes.Find("saqueador");
            float dealt = 35f * Vhaldren.Gameplay.Progression.GearCatalog.DamageDealtMultiplier(1, raider.Level);
            int swings = Mathf.CeilToInt(raider.Health / dealt);
            Assert.That(swings, Is.InRange(3, 4));
            float taken = raider.Damage * Vhaldren.Gameplay.Progression.GearCatalog.DamageTakenMultiplier(1, raider.Level);
            int hitsToFall = Mathf.CeilToInt(150f / taken);
            Assert.That(hitsToFall, Is.InRange(6, 9));
        }

        // ── Aldeanos ───────────────────────────────────────────────────

        [Test]
        public void TradesMapToRolesWithOrWithoutAccents()
        {
            Assert.That(NpcDialogue.RoleFor("Herrero"), Is.EqualTo(NpcRole.Smith));
            Assert.That(NpcDialogue.RoleFor("forjadora"), Is.EqualTo(NpcRole.Smith));
            Assert.That(NpcDialogue.RoleFor("Mercader"), Is.EqualTo(NpcRole.Merchant));
            Assert.That(NpcDialogue.RoleFor("comerciante"), Is.EqualTo(NpcRole.Merchant));
            Assert.That(NpcDialogue.RoleFor("Sanadora"), Is.EqualTo(NpcRole.Healer));
            Assert.That(NpcDialogue.RoleFor("curandero"), Is.EqualTo(NpcRole.Healer));
            Assert.That(NpcDialogue.RoleFor("Guía"), Is.EqualTo(NpcRole.Guide));
            Assert.That(NpcDialogue.RoleFor("guia de caminos"), Is.EqualTo(NpcRole.Guide));
            Assert.That(NpcDialogue.RoleFor("granjera"), Is.EqualTo(NpcRole.Villager));
            Assert.That(NpcDialogue.RoleFor(null), Is.EqualTo(NpcRole.Villager));
        }

        [Test]
        public void TextAppearsLetterByLetter()
        {
            Assert.That(NpcDialogue.VisibleCharacters(0f, 100), Is.Zero);
            Assert.That(NpcDialogue.VisibleCharacters(-3f, 100), Is.Zero);
            Assert.That(NpcDialogue.VisibleCharacters(0.5f, 100, 40f), Is.EqualTo(20));
            Assert.That(NpcDialogue.VisibleCharacters(10f, 100), Is.EqualTo(100));
            Assert.That(NpcDialogue.VisibleCharacters(float.PositiveInfinity, 37), Is.EqualTo(37));
            Assert.That(NpcDialogue.VisibleCharacters(1f, 0), Is.Zero);
        }

        [Test]
        public void GuideNamesDirectionsAndDistances()
        {
            Assert.That(NpcDialogue.DirectionName(new Vector3(0f, 0f, 10f)), Is.EqualTo("el norte"));
            Assert.That(NpcDialogue.DirectionName(new Vector3(10f, 0f, 0f)), Is.EqualTo("el este"));
            Assert.That(NpcDialogue.DirectionName(new Vector3(0f, 0f, -3f)), Is.EqualTo("el sur"));
            Assert.That(NpcDialogue.DirectionName(new Vector3(-5f, 0f, 5f)), Is.EqualTo("el noroeste"));
            string reply = NpcDialogue.GuideReply("el altar del Caballero de sangre", new Vector3(0f, 4f, 120f));
            StringAssert.Contains("120 m", reply);
            StringAssert.Contains("el norte", reply);
            StringAssert.Contains("altar", NpcDialogue.GuideReply("el altar", new Vector3(1f, 0f, 1f)));
        }

        [Test]
        public void EveryRoleHasPersonalityAndLoreMentionsTheWorld()
        {
            foreach (NpcRole role in System.Enum.GetValues(typeof(NpcRole)))
            {
                for (int seed = 0; seed < 6; seed++)
                {
                    Assert.That(NpcDialogue.Greeting(role, false, seed), Is.Not.Empty, role + " saludo");
                    Assert.That(NpcDialogue.Greeting(role, true, seed), Is.Not.Empty, role + " saludo a Lucifer");
                    Assert.That(NpcDialogue.Bark(role, null, seed % 2 == 0, seed), Is.Not.Empty);
                }
                Assert.That(NpcDialogue.Farewell(role, false), Is.Not.Empty);
                Assert.That(NpcDialogue.RoleOption(role), Is.Not.Empty);
                Assert.That(NpcDialogue.RoleTitle(role), Is.Not.Empty);
            }
            Assert.That(NpcDialogue.RoleOption(NpcRole.Villager), Is.EqualTo(NpcDialogue.AskWork));

            // La historia recorre las frases propias, la del oficio y la común (Cruce, Caballero, Luna).
            string[] own = { "Mi huerto da las mejores calabazas del Cruce." };
            Assert.That(NpcDialogue.AboutPlace(NpcRole.Villager, own, false, 0), Is.EqualTo(own[0]));
            System.Text.StringBuilder all = new System.Text.StringBuilder();
            for (int turn = 0; turn < 20; turn++)
            {
                string line = NpcDialogue.AboutPlace(NpcRole.Smith, own, true, turn);
                Assert.That(line, Is.Not.Empty);
                all.Append(line).Append(' ');
            }
            string lore = all.ToString();
            StringAssert.Contains("Cruce", lore);
            StringAssert.Contains("Caballero de sangre", lore);
            StringAssert.Contains("Luna carmesí", lore);
            StringAssert.Contains("Lucero", lore);
            StringAssert.Contains("Lucero", NpcDialogue.Greeting(NpcRole.Villager, true, 0));
        }

        [Test]
        public void ConfigureSetsUpTheVillagerForTheMapGenerator()
        {
            root = new GameObject("Aldeano");
            root.transform.position = new Vector3(200f, 0f, 200f);
            NpcVillager npc = root.AddComponent<NpcVillager>();
            npc.Configure("herrero_cruce", "Toren", "Herrero", new[] { "El yunque no espera." },
                new Vector3(10f, 2f, -4f), "Barbarian");
            Assert.That(npc.Id, Is.EqualTo("herrero_cruce"));
            Assert.That(npc.DisplayName, Is.EqualTo("Toren"));
            Assert.That(npc.Role, Is.EqualTo(NpcRole.Smith));
            Assert.That(npc.ModelKind, Is.EqualTo("Barbarian"));
            Assert.That(npc.Home, Is.EqualTo(new Vector3(10f, 2f, -4f)));
            Assert.That(npc.Lines.Length, Is.EqualTo(1));
            Assert.That(root.transform.position, Is.EqualTo(new Vector3(10f, 2f, -4f)), "Colocado lejos: empieza en su casa.");
            StringAssert.Contains("Toren", root.name);
            npc.SetGuideTarget(new Vector3(0f, 0f, 90f), "las ruinas");
            Assert.That(npc.HasGuideTarget, Is.True);
            Assert.That(npc.GuidePlace, Is.EqualTo("las ruinas"));
        }
    }
}
