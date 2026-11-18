using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Exploration;
using Vhaldren.Gameplay.Mounts;
using Vhaldren.Gameplay.Survival;

namespace Vhaldren.Tests
{
    /// <summary>
    /// Exploracion del mapa grande y monturas (logica pura, sin escena): region por
    /// posicion, profundidad del agua, niebla de guerra, desbloqueo de monturas, aguante,
    /// recompensas, animacion procedural y multiplicador de distancia de la camara.
    /// </summary>
    public sealed class ExplorationAndMountsTests
    {
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void ForgetRegisteredMounts() => MountCatalog.ResetRegistered();

        [TearDown]
        public void CleanUp()
        {
            foreach (Object item in created)
                if (item != null)
                    Object.DestroyImmediate(item);
            created.Clear();
        }

        private static AtlasRegion Region(string id, float x, float z, float radius) =>
            new AtlasRegion { id = id, nombre = id, centro = new Vector2(x, z), radio = radius, nivel = 1 };

        private WorldAtlas NewAtlas()
        {
            GameObject go = new GameObject("Atlas de prueba");
            created.Add(go);
            return go.AddComponent<WorldAtlas>();
        }

        // ── Regiones ───────────────────────────────────────────────────

        [Test]
        public void RegionIsTheOneWhoseRadiusContainsThePoint()
        {
            List<AtlasRegion> regions = new List<AtlasRegion> { Region("bosque", 0f, 0f, 100f), Region("pantano", 500f, 0f, 120f) };
            Assert.That(ExplorationMath.FindRegionIndex(regions, new Vector2(10f, 20f)), Is.EqualTo(0));
            Assert.That(ExplorationMath.FindRegionIndex(regions, new Vector2(450f, -30f)), Is.EqualTo(1));
            Assert.That(ExplorationMath.FindRegionIndex(regions, new Vector2(250f, 0f)), Is.EqualTo(-1), "Entre regiones no hay ninguna.");
        }

        [Test]
        public void OverlappingRegionsPickTheClosestCentre()
        {
            List<AtlasRegion> regions = new List<AtlasRegion> { Region("grande", 0f, 0f, 400f), Region("claro", 120f, 0f, 60f) };
            Assert.That(ExplorationMath.FindRegionIndex(regions, new Vector2(110f, 5f)), Is.EqualTo(1));
            Assert.That(ExplorationMath.FindRegionIndex(regions, new Vector2(-50f, 0f)), Is.EqualTo(0));
        }

        [Test]
        public void BrokenInputFindsNoRegion()
        {
            List<AtlasRegion> regions = new List<AtlasRegion> { Region("a", 0f, 0f, 10f), null };
            Assert.That(ExplorationMath.FindRegionIndex(regions, new Vector2(float.NaN, 0f)), Is.EqualTo(-1));
            Assert.That(ExplorationMath.FindRegionIndex(null, Vector2.zero), Is.EqualTo(-1));
            Assert.That(ExplorationMath.FindRegionIndex(regions, Vector2.zero), Is.EqualTo(0));
        }

        [Test]
        public void AtlasReplacesRegionsWithTheSameIdAndDerivesBounds()
        {
            WorldAtlas atlas = NewAtlas();
            atlas.AddRegion("norte", "Norte", new Vector2(0f, 300f), 100f, 5, Color.gray, 0.01f, Color.white);
            atlas.AddRegion("norte", "Norte helado", new Vector2(0f, 300f), 150f, 9, Color.white, 0.02f);
            Assert.That(atlas.Regions.Count, Is.EqualTo(1));
            Assert.That(atlas.RegionAt(new Vector3(0f, 0f, 300f)).nombre, Is.EqualTo("Norte helado"));
            Assert.That(atlas.BoundsMin, Is.EqualTo(new Vector2(-150f, 150f)));
            Assert.That(atlas.BoundsMax, Is.EqualTo(new Vector2(150f, 450f)));
            atlas.SetBounds(new Vector2(1024f, 1024f), new Vector2(-1024f, -1024f));
            Assert.That(atlas.BoundsMin, Is.EqualTo(new Vector2(-1024f, -1024f)), "Los limites se ordenan.");
            Assert.That(atlas.Contains(new Vector2(1000f, -1000f)), Is.True);
            Assert.That(atlas.Contains(new Vector2(1100f, 0f)), Is.False);
        }

        [Test]
        public void WorldToMapAndBackAreInverse()
        {
            Vector2 min = new Vector2(-1024f, -512f), max = new Vector2(1024f, 1536f);
            Vector2 uv = ExplorationMath.WorldToMap01(new Vector2(0f, 512f), min, max);
            Assert.That(uv.x, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(uv.y, Is.EqualTo(0.5f).Within(1e-5f));
            Vector2 back = ExplorationMath.Map01ToWorld(new Vector2(0.25f, 0.75f), min, max);
            Assert.That(Vector2.Distance(ExplorationMath.WorldToMap01(back, min, max), new Vector2(0.25f, 0.75f)), Is.LessThan(1e-5f));
            Assert.That(ExplorationMath.ScreenAngle(Vector3.right), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(ExplorationMath.ScreenAngle(Vector3.forward), Is.EqualTo(-90f).Within(1e-4f), "El norte apunta hacia arriba en pantalla.");
        }

        // ── Agua ───────────────────────────────────────────────────────

        [Test]
        public void ChannelDepthIsDeepestInTheMiddleAndZeroAtTheBank()
        {
            Assert.That(ExplorationMath.ChannelDepth(0f, 5f, 2.5f), Is.EqualTo(2.5f).Within(1e-5f));
            Assert.That(ExplorationMath.ChannelDepth(5f, 5f, 2.5f), Is.EqualTo(0f));
            Assert.That(ExplorationMath.ChannelDepth(9f, 5f, 2.5f), Is.EqualTo(0f));
            Assert.That(ExplorationMath.ChannelDepth(2.5f, 5f, 2f), Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(ExplorationMath.SurfaceDepth(10f, 12f), Is.EqualTo(0f), "Por encima del agua no hay profundidad negativa.");
        }

        [Test]
        public void DistanceToPolylineMeasuresTheNearestSegment()
        {
            List<Vector2> river = new List<Vector2> { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(100f, 100f) };
            Assert.That(ExplorationMath.DistanceToPolyline(river, new Vector2(50f, 3f)), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(ExplorationMath.DistanceToPolyline(river, new Vector2(104f, 50f)), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(ExplorationMath.DistanceToPolyline(river, new Vector2(-3f, -4f)), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(float.IsPositiveInfinity(ExplorationMath.DistanceToPolyline(new List<Vector2>(), Vector2.zero)), Is.True);
        }

        [Test]
        public void LakeDepthIsWaterLevelMinusFeet()
        {
            WorldAtlas atlas = NewAtlas();
            atlas.AddLake(new Vector2(100f, 100f), 30f, 18f, 4f);
            Assert.That(atlas.WaterDepthAt(new Vector3(100f, 16f, 100f)), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(atlas.WaterDepthAt(new Vector3(100f, 17.5f, 100f)), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(atlas.WaterDepthAt(new Vector3(100f, 19f, 100f)), Is.EqualTo(0f), "Un muelle por encima del agua esta seco.");
            Assert.That(atlas.WaterDepthAt(new Vector3(200f, 0f, 100f)), Is.EqualTo(0f), "Fuera del lago no hay agua.");
            Assert.That(atlas.WaterDepthAt(new Vector3(100f, 16f, 100f)) > DeepWaterGuard.MaxDepth, Is.True);
        }

        [Test]
        public void RiverWithoutWaterLevelUsesItsProfileExceptOnBridges()
        {
            WorldAtlas atlas = NewAtlas();
            atlas.AddRiver(new[] { new Vector2(0f, 0f), new Vector2(200f, 0f) }, 10f);
            Assert.That(atlas.WaterDepthAt(new Vector3(50f, 5f, 0f)), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(atlas.WaterDepthAt(new Vector3(50f, 5f, 4f)), Is.EqualTo(2f * (1f - 0.64f)).Within(1e-4f));
            Assert.That(atlas.WaterDepthAt(new Vector3(50f, 5f, 6f)), Is.EqualTo(0f));
            Assert.That(atlas.WaterDepthAt(new Vector3(50f, 8f, 0f), 5f), Is.EqualTo(0f), "Sobre un puente no se cuenta el cauce.");
            Assert.That(atlas.WaterDepthAt(new Vector3(50f, 5.2f, 0f), 5f), Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void SeaLevelFloodsEverythingBelowIt()
        {
            WorldAtlas atlas = NewAtlas();
            atlas.SetBounds(new Vector2(-100f, -100f), new Vector2(100f, 100f));
            Assert.That(atlas.WaterDepthAt(new Vector3(0f, 10f, 0f)), Is.EqualTo(0f));
            atlas.SetSeaLevel(12f);
            Assert.That(atlas.WaterDepthAt(new Vector3(0f, 10f, 0f)), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(atlas.WaterDepthAt(new Vector3(0f, 13f, 0f)), Is.EqualTo(0f));
        }

        [Test]
        public void DeepWaterGuardNeverActsWhileFlyingOrWithoutASafeSpot()
        {
            Assert.That(DeepWaterGuard.ShouldReturn(2f, false, true), Is.True);
            Assert.That(DeepWaterGuard.ShouldReturn(1.2f, false, true), Is.False, "Hasta ~1,3 m se puede vadear.");
            Assert.That(DeepWaterGuard.ShouldReturn(5f, true, true), Is.False);
            Assert.That(DeepWaterGuard.ShouldReturn(5f, false, false), Is.False);
        }

        // ── Niebla de guerra ───────────────────────────────────────────

        [Test]
        public void FogRevealsCellsAroundThePlayerOnlyOnce()
        {
            FogOfWarGrid fog = new FogOfWarGrid(64, 64, new Vector2(-512f, -512f), new Vector2(512f, 512f));
            List<int> changed = new List<int>();
            int first = fog.Reveal(Vector2.zero, 40f, changed);
            Assert.That(first, Is.GreaterThan(0));
            Assert.That(changed.Count, Is.EqualTo(first));
            Assert.That(fog.IsRevealedWorld(new Vector2(5f, -5f)), Is.True);
            Assert.That(fog.IsRevealedWorld(new Vector2(300f, 300f)), Is.False);
            Assert.That(fog.Reveal(Vector2.zero, 40f), Is.EqualTo(0), "Lo ya revelado no cuenta otra vez.");
            Assert.That(fog.RevealedCount, Is.EqualTo(first));
            Assert.That(fog.Fraction, Is.EqualTo(first / 4096f).Within(1e-6f));
        }

        [Test]
        public void FogIgnoresPointsOutsideTheMapAndBadInput()
        {
            FogOfWarGrid fog = new FogOfWarGrid(32, 32, Vector2.zero, new Vector2(320f, 320f));
            Assert.That(fog.Reveal(new Vector2(-500f, -500f), 30f), Is.EqualTo(0));
            Assert.That(fog.Reveal(new Vector2(float.NaN, 0f), 30f), Is.EqualTo(0));
            Assert.That(fog.Reveal(new Vector2(100f, 100f), -5f), Is.EqualTo(0));
            Assert.That(fog.IsRevealed(-1, 0), Is.False);
            Assert.That(fog.IsRevealed(32, 0), Is.False);
        }

        [Test]
        public void FogSurvivesSavingAndRejectsAnotherGrid()
        {
            FogOfWarGrid fog = new FogOfWarGrid(48, 40, Vector2.zero, new Vector2(480f, 400f));
            fog.Reveal(new Vector2(100f, 100f), 35f);
            fog.Reveal(new Vector2(400f, 300f), 20f);
            string saved = fog.Serialize();

            FogOfWarGrid loaded = new FogOfWarGrid(48, 40, Vector2.zero, new Vector2(480f, 400f));
            Assert.That(loaded.Deserialize(saved), Is.True);
            Assert.That(loaded.RevealedCount, Is.EqualTo(fog.RevealedCount));
            Assert.That(loaded.IsRevealedWorld(new Vector2(100f, 100f)), Is.True);
            Assert.That(loaded.IsRevealedWorld(new Vector2(250f, 50f)), Is.False);

            FogOfWarGrid other = new FogOfWarGrid(64, 64, Vector2.zero, new Vector2(480f, 400f));
            Assert.That(other.Deserialize(saved), Is.False, "Una rejilla de otro tamaño no se carga.");
            Assert.That(other.Deserialize("basura"), Is.False);
            Assert.That(other.RevealedCount, Is.EqualTo(0));
        }

        [Test]
        public void FogGridSizeFollowsTheMapSize()
        {
            FogOfWarGrid big = FogOfWarGrid.ForBounds(new Vector2(-1024f, -1024f), new Vector2(1024f, 1024f), 16f);
            Assert.That(big.Width, Is.EqualTo(128));
            Assert.That(big.Height, Is.EqualTo(128));
            FogOfWarGrid tiny = FogOfWarGrid.ForBounds(Vector2.zero, new Vector2(50f, 50f), 16f);
            Assert.That(tiny.Width, Is.EqualTo(32), "Nunca menos de 32 celdas por lado.");
        }

        // ── Monturas ───────────────────────────────────────────────────

        [Test]
        public void UnlockListIgnoresBlanksDuplicatesAndCase()
        {
            List<string> list = MountUnlocks.ParseList(" Grifo,,lobo_bruma, grifo ,LEON_CUMBRES");
            CollectionAssert.AreEqual(new[] { "grifo", "lobo_bruma", "leon_cumbres" }, list);
            Assert.That(MountUnlocks.ParseList(null), Is.Empty);
            Assert.That(MountUnlocks.AddToList("grifo", "fenix_ceniza"), Is.EqualTo("grifo,fenix_ceniza"));
            Assert.That(MountUnlocks.AddToList("grifo,fenix_ceniza", "Grifo"), Is.EqualTo("grifo,fenix_ceniza"));
            Assert.That(MountUnlocks.AddToList("", "oso acorazado"), Is.EqualTo("oso_acorazado"));
            Assert.That(MountUnlocks.ListContains("grifo,oso_acorazado", "OSO_ACORAZADO"), Is.True);
            Assert.That(MountUnlocks.ListContains("grifo", "lobo_bruma"), Is.False);
        }

        [Test]
        public void CatalogHasTheSixMountsAndNeverFails()
        {
            Assert.That(MountCatalog.BuiltInIds.Length, Is.EqualTo(6));
            foreach (string id in MountCatalog.BuiltInIds)
            {
                MountDefinition d = MountCatalog.Get(id);
                Assert.That(d.Id, Is.EqualTo(id));
                Assert.That(string.IsNullOrEmpty(d.Name), Is.False);
                Assert.That(d.GroundSpeed, Is.InRange(10f, 16f), id);
                if (d.Flies)
                    Assert.That(d.FlySpeed, Is.InRange(22f, 28f), id);
            }
            Assert.That(MountCatalog.Get("grifo").Flies, Is.True);
            Assert.That(MountCatalog.Get("fenix_ceniza").Flies, Is.True);
            Assert.That(MountCatalog.Get("lobo_bruma").Flies, Is.False);
            MountDefinition unknown = MountCatalog.Get("dragon_raro");
            Assert.That(unknown, Is.Not.Null);
            Assert.That(unknown.Name, Is.EqualTo("Dragon raro"));
        }

        [Test]
        public void ConfiguredSpeedIsInterpretedAndClamped()
        {
            MountDefinition ground = MountCatalog.Get("leon_cumbres").Clone();
            MountCatalog.ApplyConfiguredSpeed(ground, false, 40f);
            Assert.That(ground.GroundSpeed, Is.EqualTo(MountCatalog.MaxGroundSpeed));
            Assert.That(ground.Flies, Is.False);

            MountDefinition flyer = MountCatalog.Get("grifo").Clone();
            MountCatalog.ApplyConfiguredSpeed(flyer, true, 26f);
            Assert.That(flyer.FlySpeed, Is.EqualTo(26f));
            float groundBefore = flyer.GroundSpeed;
            MountCatalog.ApplyConfiguredSpeed(flyer, true, 12f);
            Assert.That(flyer.GroundSpeed, Is.EqualTo(12f), "Menos de 18 m/s en una voladora es su velocidad en tierra.");
            Assert.That(flyer.FlySpeed, Is.EqualTo(26f));
            MountCatalog.ApplyConfiguredSpeed(flyer, true, float.NaN);
            Assert.That(flyer.GroundSpeed, Is.EqualTo(12f), "Un valor roto no cambia nada.");
            Assert.That(groundBefore, Is.GreaterThan(0f));
        }

        [Test]
        public void StaminaDrainsExhaustsAndRecoversAfterAPause()
        {
            StaminaPool pool = new StaminaPool { Max = 100f, RegenPerSecond = 20f, RegenDelay = 1f, RecoverFraction = 0.3f };
            Assert.That(pool.TryConsume(60f), Is.True);
            Assert.That(pool.Current, Is.EqualTo(40f).Within(1e-4f));
            pool.Regenerate(0.5f);
            Assert.That(pool.Current, Is.EqualTo(40f).Within(1e-4f), "Justo despues de gastar no se recupera.");
            Assert.That(pool.TryConsume(50f), Is.True);
            Assert.That(pool.Exhausted, Is.True);
            Assert.That(pool.TryConsume(1f), Is.False, "Agotado: no se puede seguir corriendo.");
            pool.Regenerate(1f);   // pasa la pausa: recupera 20 por segundo
            Assert.That(pool.Current, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(pool.Exhausted, Is.True, "Hace falta recuperar el 30 % antes de volver a correr.");
            pool.Regenerate(0.6f);
            Assert.That(pool.Exhausted, Is.False);
            Assert.That(pool.CanUse, Is.True);
        }

        [Test]
        public void WheelStartsAtTheTopAndSpreadsEntries()
        {
            Vector2 center = new Vector2(400f, 300f);
            Assert.That(MountController.WheelSlot(0, 1, center, 150f), Is.EqualTo(center));
            Vector2 first = MountController.WheelSlot(0, 4, center, 150f);
            Assert.That(Vector2.Distance(first, new Vector2(400f, 150f)), Is.LessThan(0.01f), "La primera arriba.");
            Vector2 second = MountController.WheelSlot(1, 4, center, 150f);
            Assert.That(Vector2.Distance(second, new Vector2(550f, 300f)), Is.LessThan(0.01f));
        }

        // ── Animacion procedural ───────────────────────────────────────

        [Test]
        public void AttackCurveWindsUpStrikesAndReturns()
        {
            Assert.That(QuadrupedGait.AttackCurve(0f), Is.EqualTo(0f));
            Assert.That(QuadrupedGait.AttackCurve(1f), Is.EqualTo(0f));
            Assert.That(QuadrupedGait.AttackCurve(0.2f), Is.LessThan(0f), "Primero se prepara hacia atras.");
            Assert.That(QuadrupedGait.AttackCurve(0.55f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(QuadrupedGait.AttackCurve(0.9f), Is.InRange(0f, 0.5f));
        }

        [Test]
        public void FindDeepAcceptsCaseAndBlenderSuffixes()
        {
            GameObject root = new GameObject("raiz");
            created.Add(root);
            Transform body = new GameObject("Cuerpo.001").transform;
            body.SetParent(root.transform);
            Transform head = new GameObject("cabeza").transform;
            head.SetParent(body);
            Transform near = new GameObject("cola_alta").transform;
            near.SetParent(root.transform);
            Assert.That(QuadrupedGait.FindDeep(root.transform, "cuerpo"), Is.EqualTo(body));
            Assert.That(QuadrupedGait.FindDeep(root.transform, "cabeza"), Is.EqualTo(head));
            Assert.That(QuadrupedGait.FindDeep(root.transform, "cola"), Is.Null, "«cola_alta» no es la cola.");
            Assert.That(QuadrupedGait.FindDeep(root.transform, "raiz"), Is.Null, "La raiz no cuenta como parte.");
        }

        [Test]
        public void SubstituteModelsHaveTheNamedPartsTheGaitAnimates()
        {
            GameObject holder = new GameObject("Soporte");
            created.Add(holder);
            GameObject wolf = MountModelFactory.BuildSubstitute(MountCatalog.Get("lobo_bruma"), holder.transform);
            QuadrupedGait wolfGait = wolf.AddComponent<QuadrupedGait>();
            wolfGait.Rebind();
            Assert.That(wolfGait.PartCount, Is.EqualTo(7), "Cuerpo, cabeza, cuatro patas y cola.");
            Assert.That(wolfGait.HasWings, Is.False);
            Assert.That(MountModelFactory.EnsureSaddle(wolf).name, Is.EqualTo("silla"));

            GameObject griffin = MountModelFactory.BuildSubstitute(MountCatalog.Get("grifo"), holder.transform);
            QuadrupedGait griffinGait = griffin.AddComponent<QuadrupedGait>();
            griffinGait.Rebind();
            Assert.That(griffinGait.PartCount, Is.EqualTo(9));
            Assert.That(griffinGait.HasWings, Is.True);

            GameObject phoenix = MountModelFactory.BuildSubstitute(MountCatalog.Get("fenix_ceniza"), holder.transform);
            QuadrupedGait phoenixGait = phoenix.AddComponent<QuadrupedGait>();
            phoenixGait.Rebind();
            Assert.That(phoenixGait.HasWings, Is.True);
            Assert.That(QuadrupedGait.FindDeep(phoenix.transform, "silla"), Is.Not.Null);
            foreach (Collider c in holder.GetComponentsInChildren<Collider>())
                Assert.That(c.enabled, Is.False, "La montura es solo visual.");
        }

        // ── Recompensas, titulos, guardado y camara ───────────────────

        [Test]
        public void RewardTextIsParsedByKeywords()
        {
            ExplorationRewards.RewardSpec spec = ExplorationRewards.Parse("5 cristales y esencia antigua");
            Assert.That(spec.Kinds, Does.Contain(ResourceKind.Crystal));
            Assert.That(spec.Amounts[spec.Kinds.IndexOf(ResourceKind.Crystal)], Is.EqualTo(5));
            Assert.That(spec.Essence, Is.True);
            Assert.That(ExplorationRewards.Parse("mineral:3").Amounts[0], Is.EqualTo(3));
            Assert.That(ExplorationRewards.Parse("Pieles x4").Amounts[0], Is.EqualTo(4));
            Assert.That(ExplorationRewards.Parse("equipo rúnico").Gear, Is.True);
            Assert.That(ExplorationRewards.Parse("").IsEmpty, Is.True);
            Assert.That(ExplorationRewards.Parse("una vista preciosa").IsEmpty, Is.True);
        }

        [Test]
        public void BannerFadesInHoldsAndFadesOut()
        {
            Assert.That(ExplorationBanner.AlphaAt(0f, 3f), Is.EqualTo(0f));
            Assert.That(ExplorationBanner.AlphaAt(ExplorationBanner.FadeIn + 1f, 3f), Is.EqualTo(1f));
            Assert.That(ExplorationBanner.AlphaAt(ExplorationBanner.FadeIn + 3f + ExplorationBanner.FadeOut + 0.1f, 3f), Is.EqualTo(0f));
            Assert.That(ExplorationBanner.AlphaAt(ExplorationBanner.FadeIn * 0.5f, 3f), Is.InRange(0.1f, 0.9f));
        }

        [Test]
        public void SaveKeysAreSafeAndStable()
        {
            Assert.That(ExplorationSave.Sanitize("Bosque de Vhal!", "x"), Is.EqualTo("Bosque_de_Vhal_"));
            Assert.That(ExplorationSave.Sanitize("", "x"), Is.EqualTo("x"));
            Assert.That(ExplorationSave.PositionId("cofre", new Vector3(10.4f, 2.6f, -3.5f)), Is.EqualTo("cofre_10_3_-4"));
        }

        [Test]
        public void CameraDistanceMultiplierIsClampedAndDefaultsToOne()
        {
            GameObject go = new GameObject("Camara de prueba");
            created.Add(go);
            go.AddComponent<Camera>();
            SimpleFollowCamera follow = go.AddComponent<SimpleFollowCamera>();
            Assert.That(follow.DistanceMultiplier, Is.EqualTo(1f), "Sin montura la camara no cambia.");
            follow.SetDistanceMultiplier(1.75f, true);
            Assert.That(follow.DistanceMultiplier, Is.EqualTo(1.75f));
            follow.SetDistanceMultiplier(10f);
            Assert.That(follow.TargetDistanceMultiplier, Is.EqualTo(3f));
            follow.SetDistanceMultiplier(float.NaN);
            Assert.That(follow.TargetDistanceMultiplier, Is.EqualTo(3f));
            Assert.That(follow.Distance, Is.EqualTo(follow.DefaultDistance).Within(1e-4f), "El zoom del jugador no cambia.");
        }
    }
}
