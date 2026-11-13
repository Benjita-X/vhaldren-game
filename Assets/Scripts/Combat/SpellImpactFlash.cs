using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Impacto por capas y reutilizable (sale de una reserva, no se crea ni se destruye):
    /// destello blanco que vira al color del elemento, halo, onda en el aire, onda
    /// expansiva en el suelo, particulas del elemento, luz que decae y, en areas, una
    /// marca en el suelo que se desvanece. Solo visual: el dano lo resuelve quien lo crea.
    /// </summary>
    public sealed class SpellImpactFlash : MonoBehaviour
    {
        private static readonly Func<Transform, SpellImpactFlash> Factory = Create;

        private MeshRenderer core;
        private MeshRenderer glow;
        private MeshRenderer airWave;
        private MeshRenderer groundWave;
        private Light flashLight;
        private MaterialPropertyBlock block;
        private VfxPool<SpellImpactFlash> pool;

        private VfxElement element;
        private Color color;
        private Color coreColor;
        private float maxRadius;
        private float ringRadius;
        private float duration;
        private float lifetime;
        private float elapsed;
        private float baseIntensity;
        private bool darkCore;

        public VfxElement Element => element;
        public bool HasGroundWave => groundWave != null && groundWave.enabled;
        public bool Playing => isActiveAndEnabled && elapsed < lifetime;

        /// <summary>Crea el efecto en el mundo. ringRadius &lt;= 0 desactiva la onda del suelo.</summary>
        public static SpellImpactFlash Spawn(Vector3 position, Color color, float maxRadius,
            float duration, float ringRadius = 0f)
        {
            return Spawn(position, color, maxRadius, duration, ringRadius, VfxElement.Neutral);
        }

        /// <summary>Igual, con particulas y marca propias del elemento (brasas, hielo, rayos...).</summary>
        public static SpellImpactFlash Spawn(Vector3 position, Color color, float maxRadius,
            float duration, float ringRadius, VfxElement element)
        {
            VfxHost host = VfxHost.Get();
            if (host == null)
                return null;
            VfxPool<SpellImpactFlash> reserve = host.Pool(Factory);
            SpellImpactFlash flash = reserve.Take();
            flash.pool = reserve;
            flash.Play(host, position, color, Mathf.Max(0.1f, maxRadius), Mathf.Max(0.05f, duration),
                ringRadius, element);
            return flash;
        }

        private static SpellImpactFlash Create(Transform parent)
        {
            GameObject root = new GameObject("FX impacto");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            SpellImpactFlash flash = root.AddComponent<SpellImpactFlash>();
            flash.block = new MaterialPropertyBlock();
            flash.glow = VfxLibrary.CreateQuad("Halo", root.transform, false, VfxLibrary.AdditiveGlow);
            flash.airWave = VfxLibrary.CreateQuad("Onda en el aire", root.transform, false, VfxLibrary.GroundAdditive);
            flash.core = VfxLibrary.CreateQuad("Destello", root.transform, false, VfxLibrary.AdditiveGlow);
            flash.groundWave = VfxLibrary.CreateQuad("Onda en el suelo", root.transform, true, VfxLibrary.GroundAdditive);
            flash.glow.sortingOrder = 0;
            flash.airWave.sortingOrder = 1;
            flash.core.sortingOrder = 2;
            flash.flashLight = PrototypeVfx.CreatePointLight(root.transform, Color.white, 4f, 3f);
            return flash;
        }

        private void Play(VfxHost host, Vector3 position, Color tint, float radius, float life,
            float groundRing, VfxElement kind)
        {
            VfxPalette palette = VfxLibrary.Palette(kind, tint);
            element = kind;
            color = kind == VfxElement.Neutral ? VfxLibrary.WithAlpha(tint, 1f) : palette.Glow;
            darkCore = palette.DarkCore;
            coreColor = darkCore ? palette.Core : Color.white;
            maxRadius = radius;
            ringRadius = Mathf.Max(0f, groundRing);
            duration = life;
            lifetime = Mathf.Max(life * 1.7f, ringRadius > 0f ? 0.6f : 0.38f);
            elapsed = 0f;

            transform.position = position;
            core.sharedMaterial = darkCore ? VfxLibrary.AlphaDrop : VfxLibrary.AdditiveGlow;

            Vector3 ground = position;
            Vector3 normal = Vector3.up;
            groundWave.enabled = ringRadius > 0f;
            if (ringRadius > 0f)
            {
                if (!VfxLibrary.FindGround(position, out ground, out normal))
                    ground = position;
                VfxLibrary.PlaceOnGround(groundWave.transform, ground, normal, 0.05f);
            }

            flashLight.color = color;
            flashLight.range = 1.5f + radius * 5f;
            baseIntensity = darkCore ? 1.4f : 3.2f;
            flashLight.intensity = baseIntensity;

            gameObject.SetActive(true);
            Animate(0f);
            FaceCamera(host.ViewCamera);
            EmitParticles(host, position, ground, normal, palette, kind);
        }

        private void EmitParticles(VfxHost host, Vector3 position, Vector3 ground, Vector3 normal,
            VfxPalette palette, VfxElement kind)
        {
            float strength = Mathf.InverseLerp(0.3f, 1.6f, maxRadius);
            int count = Mathf.RoundToInt(Mathf.Lerp(7f, 34f, strength));
            float speed = 2.4f + maxRadius * 3.4f;
            float size = 0.09f + maxRadius * 0.09f;
            float life = 0.32f + maxRadius * 0.22f;
            Color spark = kind == VfxElement.Neutral ? palette.Spark : palette.Spark;
            host.Burst(kind, position, Vector3.up, spark, count, speed, size, life, 1f, maxRadius * 0.15f);

            switch (kind)
            {
                case VfxElement.Fire:
                    host.Smoke(position, maxRadius * 0.3f, new Color(0.1f, 0.07f, 0.06f, 0.55f),
                        Mathf.RoundToInt(3 + strength * 6), 0.5f + maxRadius * 0.6f, 1.1f, Vector3.up * 0.9f);
                    break;
                case VfxElement.Frost:
                    host.Smoke(position, maxRadius * 0.35f, new Color(0.8f, 0.94f, 1f, 0.35f),
                        Mathf.RoundToInt(3 + strength * 6), 0.5f + maxRadius * 0.7f, 0.9f, Vector3.up * 0.25f);
                    break;
                case VfxElement.Shadow:
                    host.Smoke(position, maxRadius * 0.35f, palette.Spark,
                        Mathf.RoundToInt(5 + strength * 10), 0.6f + maxRadius * 0.8f, 1f, Vector3.up * 0.35f);
                    break;
                case VfxElement.Light:
                    host.Glints(position, maxRadius * 0.6f, palette.Spark, Mathf.RoundToInt(4 + strength * 10),
                        0.12f + maxRadius * 0.08f, 0.6f, Vector3.up * 0.6f);
                    break;
                case VfxElement.Storm:
                    host.Glints(position, maxRadius * 0.4f, palette.Core, Mathf.RoundToInt(3 + strength * 6),
                        0.1f + maxRadius * 0.08f, 0.25f, Vector3.zero);
                    break;
            }

            if (ringRadius <= 0f)
                return;
            // El frente de la onda levanta material del elemento a lo largo del anillo.
            int ringCount = Mathf.Clamp(Mathf.RoundToInt(ringRadius * 9f), 10, 60);
            host.Ring(kind, ground, normal, Mathf.Max(0.3f, ringRadius * 0.35f), spark, ringCount,
                ringRadius * 2.4f, kind == VfxElement.Blood ? 2.5f : 1.2f, 0.12f + ringRadius * 0.025f, 0.55f);
            if (kind != VfxElement.Neutral && ringRadius >= 1.2f)
                GroundMark.Spawn(ground, normal, ringRadius * 0.8f, palette.Mark, palette.GlowingMark, 3.2f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Animate(elapsed);
            if (elapsed >= lifetime)
            {
                if (pool != null)
                    pool.Release(this);
                else
                    Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            VfxHost host = VfxHost.Get();
            FaceCamera(host != null ? host.ViewCamera : Camera.main);
        }

        private void FaceCamera(Camera camera)
        {
            VfxLibrary.FaceCamera(core.transform, camera);
            VfxLibrary.FaceCamera(glow.transform, camera);
            VfxLibrary.FaceCamera(airWave.transform, camera);
        }

        private void Animate(float time)
        {
            float t = Mathf.Clamp01(time / duration);
            float whole = Mathf.Clamp01(time / lifetime);
            float grow = 1f - (1f - t) * (1f - t);
            float fade = (1f - t) * (1f - t);

            // Destello: nace blanco y pequeño, crece rapido y vira al color del elemento.
            core.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, maxRadius * 2.3f, grow);
            Color c = darkCore ? coreColor : Color.Lerp(Color.white, color, t * 1.4f);
            c.a = (darkCore ? coreColor.a : 1f) * fade;
            VfxDisc.Tint(core, block, c);

            // Halo amplio que tarda mas en apagarse.
            glow.transform.localScale = Vector3.one * Mathf.Lerp(maxRadius * 1.6f, maxRadius * 3.8f, Mathf.Sqrt(whole));
            float glowFade = (1f - whole) * (1f - whole);
            VfxDisc.Tint(glow, block, VfxLibrary.WithAlpha(color, (darkCore ? 0.9f : 0.75f) * glowFade));

            // Onda en el aire: anillo que se abre de cara a la camara.
            airWave.transform.localScale = Vector3.one * (maxRadius * 3.4f);
            VfxDisc.Wave(color, Mathf.Lerp(0.12f, 1f, grow), 0.28f * (1f - t) + 0.04f, 0.85f * (1f - t))
                .Apply(airWave, block);

            if (groundWave.enabled)
            {
                float g = Mathf.Clamp01(time / (lifetime * 0.85f));
                float spread = 1f - (1f - g) * (1f - g) * (1f - g);
                groundWave.transform.localScale = new Vector3(ringRadius, 1f, ringRadius);
                VfxDisc wave = VfxDisc.Wave(color, Mathf.Lerp(0.06f, 1f, spread), 0.32f * (1f - g) + 0.05f, 1f - g);
                // Fogonazo del area entera en el primer instante.
                wave.Base = 0.38f * Mathf.Clamp01(1f - g * 5f);
                wave.Soft = 0.25f;
                wave.Apply(groundWave, block);
            }

            flashLight.intensity = baseIntensity * fade;
        }
    }
}
