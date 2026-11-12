using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.UI;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    /// <summary>HUD 022: recargas, avisos, viñeta, tamaño de la barra y flechas fuera de pantalla.</summary>
    public sealed class HudTests
    {
        [TearDown]
        public void TearDown() => HudNotices.Clear();

        [Test]
        public void BarCooldownFractionFollowsSecondsLeft()
        {
            Assert.That(ActionBarHud.CooldownFraction(0f, 5f), Is.EqualTo(0f));
            Assert.That(ActionBarHud.CooldownFraction(2.5f, 5f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(ActionBarHud.CooldownFraction(1f, 4f), Is.EqualTo(0.25f).Within(1e-4f),
                "El sombreado baja con los segundos restantes.");
            Assert.That(ActionBarHud.CooldownFraction(9f, 5f), Is.EqualTo(1f));
            Assert.That(ActionBarHud.CooldownFraction(float.NaN, 5f), Is.EqualTo(0f));
            Assert.That(ActionBarHud.CooldownFraction(2f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void NoticesShowAtMostTwoAndExpireAfterAboutTwoSeconds()
        {
            HudNotices.Clear();
            HudNotices.PostAt("Primero", Color.white, 0f);
            HudNotices.PostAt("Segundo", Color.white, 0.1f);
            HudNotices.PostAt("Tercero", Color.white, 0.2f);
            Assert.That(HudNotices.CountAt(0.3f), Is.EqualTo(2));
            Assert.That(HudNotices.IsShowing("Primero", 0.3f), Is.False, "El más antiguo deja sitio al nuevo.");
            Assert.That(HudNotices.IsShowing("Tercero", 0.3f), Is.True);
            Assert.That(HudNotices.CountAt(1.9f), Is.EqualTo(2));
            Assert.That(HudNotices.CountAt(2.25f), Is.EqualTo(0), "Unos 2 s y desaparecen.");
        }

        [Test]
        public void RepeatedNoticeRenewsInsteadOfStacking()
        {
            HudNotices.Clear();
            HudNotices.PostAt("Espada: recarga", Color.white, 0f);
            HudNotices.PostAt("Espada: recarga", Color.white, 1.5f);
            Assert.That(HudNotices.CountAt(1.6f), Is.EqualTo(1));
            Assert.That(HudNotices.IsShowing("Espada: recarga", 3f), Is.True, "Renovado: dura 2 s desde la repetición.");
            Assert.That(HudNotices.IsShowing("Espada: recarga", 3.6f), Is.False);
        }

        [Test]
        public void NoticeFadesOutAtTheEnd()
        {
            HudNotices.Clear();
            HudNotices.PostAt("Botín", Color.white, 0f);
            HudNotices.Notice notice = HudNotices.ActiveAt(0f)[0];
            Assert.That(HudNotices.AlphaOf(notice, 1f), Is.EqualTo(1f));
            Assert.That(HudNotices.AlphaOf(notice, 1.9f), Is.LessThan(0.5f));
            Assert.That(HudNotices.AlphaOf(notice, 0.05f), Is.LessThan(1f), "Entra con un fundido breve.");
        }

        [Test]
        public void VignetteIntensifiesBelowThirtyPercentHealth()
        {
            Assert.That(ScreenVignette.LowHealthFactor(1f), Is.EqualTo(0f));
            Assert.That(ScreenVignette.LowHealthFactor(0.3f), Is.EqualTo(0f));
            Assert.That(ScreenVignette.LowHealthFactor(0.15f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(ScreenVignette.LowHealthFactor(0f), Is.EqualTo(1f));
            Assert.That(ScreenVignette.EdgeAlpha(0.1f), Is.GreaterThan(ScreenVignette.EdgeAlpha(0.8f)));
            Assert.That(ScreenVignette.DangerAlpha(0.8f, 1f), Is.EqualTo(0f), "Con vida alta no hay rojo.");
            Assert.That(ScreenVignette.DangerAlpha(0.1f, 0f), Is.GreaterThan(0.2f));
            Assert.That(ScreenVignette.LowHealthFactor(float.NaN), Is.EqualTo(0f));
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(1024, 768)]
        [TestCase(2560, 1080)]
        [TestCase(800, 600)]
        public void ActionBarNeverExceedsFortyFivePercentOfTheWidth(int width, int height)
        {
            Assert.That(ActionBarHud.WidthFraction(width, height), Is.LessThanOrEqualTo(ActionBarHud.MaxWidthFraction + 1e-4f));
        }

        [Test]
        public void OffscreenArrowPointsTowardTheEnemy()
        {
            Vector2 screen = new Vector2(1280f, 720f);
            Assert.That(WorldHud.OffscreenEdge(new Vector3(0.5f, 0.5f, 10f), screen, 40f, out _, out _), Is.False,
                "Un enemigo visible no necesita flecha.");

            Assert.That(WorldHud.OffscreenEdge(new Vector3(1.4f, 0.5f, 10f), screen, 40f, out Vector2 right, out float rightAngle), Is.True);
            Assert.That(right.x, Is.EqualTo(screen.x - 40f).Within(0.5f));
            Assert.That(right.y, Is.EqualTo(screen.y * 0.5f).Within(0.5f));
            Assert.That(rightAngle, Is.EqualTo(0f).Within(0.5f));

            Assert.That(WorldHud.OffscreenEdge(new Vector3(0.5f, -0.4f, 10f), screen, 40f, out Vector2 below, out float belowAngle), Is.True);
            Assert.That(below.y, Is.EqualTo(screen.y - 40f).Within(0.5f), "Abajo en pantalla (y hacia abajo).");
            Assert.That(belowAngle, Is.EqualTo(90f).Within(0.5f));

            Assert.That(WorldHud.OffscreenEdge(new Vector3(0.5f, 0.8f, -5f), screen, 40f, out Vector2 behind, out float behindAngle), Is.True,
                "Detrás de la cámara cuenta como fuera de pantalla.");
            Assert.That(behindAngle, Is.EqualTo(90f).Within(0.5f), "Detrás de la cámara la dirección se invierte.");
            Assert.That(behind.x, Is.InRange(40f, screen.x - 40f));
        }

        [Test]
        public void OffscreenArrowNeverSitsOnTopOfTheActionBar()
        {
            Rect bar = new Rect(400f, 600f, 480f, 120f);
            Vector2 moved = WorldHud.AvoidRect(new Vector2(640f, 680f), bar);
            Assert.That(moved.y, Is.LessThan(bar.yMin), "Una flecha abajo en el centro sube por encima de la barra.");
            Assert.That(moved.x, Is.EqualTo(640f));
            Vector2 aside = WorldHud.AvoidRect(new Vector2(100f, 680f), bar);
            Assert.That(aside, Is.EqualTo(new Vector2(100f, 680f)), "Fuera de la barra no se mueve.");
            Assert.That(WorldHud.OffscreenEdge(new Vector3(0.5f, 0.99f, 10f), new Vector2(1280f, 720f), 40f, out _, out _), Is.False,
                "Un enemigo que aún se ve en el borde no lleva flecha.");
        }

        [Test]
        public void DebugBoxesHaveTheirOwnRebindableKey()
        {
            GameInput.ResetDefaults();
            Assert.That(GameInput.KeyFor(GameAction.DebugHud), Is.EqualTo(Key.F3));
            Assert.That(GameInput.Label(GameAction.DebugHud), Is.EqualTo("F3"));
            GameInput.Rebind(GameAction.DebugHud, Key.F4);
            Assert.That(GameInput.KeyFor(GameAction.DebugHud), Is.EqualTo(Key.F4));
            GameInput.ResetDefaults();
        }

        [Test]
        public void HelpIsHiddenOnStartByDefault()
        {
            const string key = "vh.op.ayuda";
            bool had = PlayerPrefs.HasKey(key);
            float previous = PlayerPrefs.GetFloat(key, 0f);
            PlayerPrefs.DeleteKey(key);
            try
            {
                Assert.That(GameSettings.ShowHelpOnStart, Is.False);
            }
            finally
            {
                if (had)
                    PlayerPrefs.SetFloat(key, previous);
            }
        }

        [Test]
        public void ForgedCrossbowGlowsMoreWithEachTier()
        {
            CharacterLoadout.TierLook(0, out _, out Color basic);
            CharacterLoadout.TierLook(1, out _, out Color bronze);
            CharacterLoadout.TierLook(3, out Color topAlbedo, out Color top);
            Assert.That(basic.maxColorComponent, Is.EqualTo(0f), "Calidad básica: sin brillo.");
            Assert.That(top.maxColorComponent, Is.GreaterThan(bronze.maxColorComponent));
            Assert.That(topAlbedo, Is.Not.EqualTo(Color.white), "La calidad máxima cambia el color.");
        }
    }
}
