using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Marca en el suelo tras un impacto de area (quemado, escarcha, mancha de sombra o
    /// de sangre, sello de luz). Aparece de golpe, se mantiene y se desvanece. Reutilizable.
    /// </summary>
    public sealed class GroundMark : MonoBehaviour
    {
        private static readonly Func<Transform, GroundMark> Factory = Create;

        private MeshRenderer disc;
        private MaterialPropertyBlock block;
        private VfxPool<GroundMark> pool;
        private Color color;
        private float lifetime;
        private float elapsed;
        private float seed;
        private bool glowing;

        public float Opacity { get; private set; }

        public static GroundMark Spawn(Vector3 position, Vector3 normal, float radius, Color color,
            bool glowing, float lifetime = 3f)
        {
            VfxHost host = VfxHost.Get();
            if (host == null || radius <= 0.05f || color.a <= 0.01f)
                return null;
            VfxPool<GroundMark> reserve = host.Pool(Factory);
            GroundMark mark = reserve.Take();
            mark.pool = reserve;
            mark.Begin(position, normal, radius, color, glowing, lifetime);
            return mark;
        }

        private static GroundMark Create(Transform parent)
        {
            GameObject root = new GameObject("Marca en el suelo");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            GroundMark mark = root.AddComponent<GroundMark>();
            mark.block = new MaterialPropertyBlock();
            mark.disc = VfxLibrary.CreateQuad("Mancha", root.transform, true, VfxLibrary.GroundAlpha);
            return mark;
        }

        private void Begin(Vector3 position, Vector3 normal, float radius, Color tint, bool glow, float seconds)
        {
            color = tint;
            glowing = glow;
            lifetime = Mathf.Max(0.5f, seconds);
            elapsed = 0f;
            seed = UnityEngine.Random.Range(0f, 100f);
            disc.sharedMaterial = glow ? VfxLibrary.GroundAdditive : VfxLibrary.GroundAlpha;
            VfxLibrary.PlaceOnGround(transform, position, normal, 0.03f);
            // Giro aleatorio: dos marcas seguidas no se ven identicas.
            transform.Rotate(Vector3.up, UnityEngine.Random.Range(0f, 360f), Space.Self);
            disc.transform.localScale = new Vector3(radius, 1f, radius);
            gameObject.SetActive(true);
            Refresh();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Refresh();
            if (elapsed >= lifetime)
            {
                if (pool != null)
                    pool.Release(this);
                else
                    Destroy(gameObject);
            }
        }

        private void Refresh()
        {
            float fadeIn = Mathf.Clamp01(elapsed / 0.08f);
            float fadeStart = lifetime * 0.5f;
            float fadeOut = 1f - Mathf.Clamp01((elapsed - fadeStart) / Mathf.Max(0.01f, lifetime - fadeStart));
            Opacity = fadeIn * fadeOut;
            if (glowing)
            {
                // Sello de luz: brillo suave con marcas runicas que giran.
                VfxDisc sigil = VfxDisc.Stain(VfxLibrary.WithAlpha(color, color.a * Opacity), 0.5f, 0.45f, 0.35f, seed);
                sigil.RimWidth = 0.025f;
                sigil.RimAlpha = 0.8f;
                sigil.Ticks = 0.7f;
                sigil.Spin = elapsed * 0.2f;
                sigil.Apply(disc, block);
            }
            else
            {
                VfxDisc.Stain(VfxLibrary.WithAlpha(color, color.a * Opacity), 1f, 0.6f, 0.85f, seed).Apply(disc, block);
            }
        }
    }
}
