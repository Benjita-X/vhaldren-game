using System;
using UnityEngine;
using Vhaldren.Gameplay.Progression;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Region actual del jugador segun <see cref="WorldAtlas"/>: al cambiar muestra un titulo
    /// grande con el nombre y el nivel recomendado, la guarda como visitada y lleva con
    /// suavidad la niebla (color y densidad) y el tinte de la luz principal a los de la
    /// region. Tambien revisa los lugares de interes cercanos y dibuja los titulos de
    /// exploracion. Sin atlas en la escena no hace nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RegionTracker : MonoBehaviour
    {
        public const float CheckInterval = 0.25f;
        public const float AmbienceSharpness = 0.55f;
        public const float FlightFogFactor = 0.45f;

        private WorldAtlas atlas;
        private int regionIndex = -1;
        private AtlasRegion current;
        private float nextCheck;
        private float nextPlaceCheck;
        private float enabledAt;

        private bool captured;
        private bool originalFog;
        private Color originalFogColor;
        private float originalFogDensity;
        private FogMode originalFogMode;
        private bool hasTarget;
        private Color fogColor, targetFogColor;
        private float fogDensity, targetFogDensity;
        private Color lightTint = Color.white, targetLightTint = Color.white;

        private bool sunSearched;
        private Light sun;
        private Color sunBase;
        private Color sunWritten;
        private bool sunTinted;

        /// <summary>Region actual (null fuera de todas o sin atlas).</summary>
        public AtlasRegion CurrentRegion => current;

        /// <summary>Se avisa al entrar en otra region.</summary>
        public static event Action<AtlasRegion> RegionChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => RegionChanged = null;

        private void OnEnable() => enabledAt = Time.time;

        private void Update()
        {
            if (atlas == null)
            {
                atlas = WorldAtlas.Active;
                if (atlas == null)
                    return;
            }
            float now = Time.time;
            if (now >= nextCheck)
            {
                nextCheck = now + CheckInterval;
                int index = atlas.RegionIndexAt(transform.position);
                if (index >= 0 && index != regionIndex)
                    Enter(index);
            }
            if (now >= nextPlaceCheck)
            {
                nextPlaceCheck = now + 0.3f;
                PointOfInterest.CheckDiscoveries(gameObject);
            }
            UpdateFog(Time.deltaTime);
        }

        private void Enter(int index)
        {
            AtlasRegion region = atlas.Regions[index];
            if (region == null)
                return;
            bool first = regionIndex < 0;
            regionIndex = index;
            current = region;
            bool isNew = ExplorationSave.Mark(ExplorationSave.Regions, region.id);

            int gearLevel = PlayerEquipment.Local != null ? PlayerEquipment.Local.GearLevel : 1;
            int gap = region.nivel - gearLevel;
            Color levelColor = gap >= 6 ? new Color(1f, 0.42f, 0.38f) : gap >= 2 ? new Color(1f, 0.82f, 0.4f) : HudSkin.TextDim;
            string subtitle = (isNew ? "Región nueva · " : "") + "Nivel recomendado " + region.nivel +
                              (gap >= 6 ? " · peligro extremo" : gap >= 2 ? " · peligrosa" : "");
            ExplorationBanner.Show(region.nombre, subtitle, HudSkin.GoldBright, levelColor, true, 3f);

            if (!captured)
                Capture();
            targetFogColor = region.colorNiebla;
            targetFogDensity = Mathf.Clamp(region.densidadNiebla, 0f, 0.2f);
            targetLightTint = region.colorLuz.maxColorComponent > 0.01f ? region.colorLuz : Color.white;
            if (first && Time.time - enabledAt < 3f)
            {
                // Al empezar la partida el ambiente ya es el de la region, sin transicion.
                fogColor = targetFogColor;
                fogDensity = targetFogDensity;
                lightTint = targetLightTint;
            }
            hasTarget = true;
            RegionChanged?.Invoke(region);
        }

        private void Capture()
        {
            captured = true;
            originalFog = RenderSettings.fog;
            originalFogColor = RenderSettings.fogColor;
            originalFogDensity = RenderSettings.fogDensity;
            originalFogMode = RenderSettings.fogMode;
            fogColor = originalFogColor;
            fogDensity = originalFog && originalFogMode != FogMode.Linear ? originalFogDensity : 0f;
        }

        private void UpdateFog(float deltaTime)
        {
            if (!hasTarget)
                return;
            float blend = 1f - Mathf.Exp(-AmbienceSharpness * Mathf.Max(0f, deltaTime));
            float wantedDensity = targetFogDensity * (ExplorationUi.PlayerFlying ? FlightFogFactor : 1f);
            fogColor = Color.Lerp(fogColor, targetFogColor, blend);
            fogDensity = Mathf.Lerp(fogDensity, wantedDensity, blend * (ExplorationUi.PlayerFlying ? 2f : 1f));
            lightTint = Color.Lerp(lightTint, targetLightTint, blend);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        private void LateUpdate()
        {
            if (!hasTarget)
                return;
            if (!sunSearched)
            {
                sunSearched = true;
                sun = RenderSettings.sun;
                if (sun == null)
                    foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (light.type == LightType.Directional)
                        {
                            sun = light;
                            break;
                        }
            }
            if (sun == null)
                return;
            // El ciclo de dia y noche reescribe el color cada fotograma: esa es la base.
            if (!sunTinted || sun.color != sunWritten)
                sunBase = sun.color;
            Color tinted = new Color(sunBase.r * lightTint.r, sunBase.g * lightTint.g, sunBase.b * lightTint.b, sunBase.a);
            sun.color = tinted;
            sunWritten = tinted;
            sunTinted = true;
        }

        private void OnDisable()
        {
            if (captured)
            {
                RenderSettings.fog = originalFog;
                RenderSettings.fogColor = originalFogColor;
                RenderSettings.fogDensity = originalFogDensity;
                RenderSettings.fogMode = originalFogMode;
            }
            if (sun != null && sunTinted)
                sun.color = sunBase;
            sunTinted = false;
            captured = false;
            hasTarget = false;
        }

        private void OnGUI()
        {
            GUI.depth = -12;
            if (Event.current.type != EventType.Repaint || HudMode.Hidden || ExplorationUi.MapOpen)
                return;
            if (!ExplorationBanner.Showing && !HasQueued())
                return;
            MenuStyle.Begin();
            ExplorationBanner.Draw(MenuStyle.Width, MenuStyle.Height);
            MenuStyle.End();
        }

        private static bool HasQueued() => ExplorationBanner.Pending > 0;
    }
}
