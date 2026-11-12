using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Parametros del shader de discos (Vhaldren/VfxSuelo) en un solo valor.
    /// Se aplican con MaterialPropertyBlock: muchos discos comparten un material.
    /// </summary>
    public struct VfxDisc
    {
        public Color Color;
        public float Fill;
        public float FillAlpha;
        public float EdgeWidth;
        public float EdgeAlpha;
        public float Base;
        public float Soft;
        public float RimWidth;
        public float RimAlpha;
        public float Ticks;
        public float Spin;
        public float Noise;
        public float Seed;

        /// <summary>Onda expansiva: solo el frente, sin area ni borde.</summary>
        public static VfxDisc Wave(Color color, float front, float width, float alpha)
        {
            return new VfxDisc
            {
                Color = color,
                Fill = Mathf.Clamp(front, 0.001f, 1f),
                EdgeWidth = Mathf.Clamp(width, 0.002f, 0.6f),
                EdgeAlpha = Mathf.Clamp01(alpha),
                Soft = 0.02f
            };
        }

        /// <summary>Mancha suave e irregular en el suelo.</summary>
        public static VfxDisc Stain(Color color, float opacity, float softness, float noise, float seed)
        {
            return new VfxDisc
            {
                Color = color,
                Base = Mathf.Clamp01(opacity),
                Soft = Mathf.Clamp(softness, 0.01f, 1f),
                Noise = Mathf.Clamp01(noise),
                Seed = seed,
                EdgeWidth = 0.05f
            };
        }

        public void Apply(Renderer renderer, MaterialPropertyBlock block)
        {
            if (renderer == null || block == null)
                return;
            block.Clear();
            block.SetColor(VfxLibrary.ColorId, Color);
            block.SetFloat(VfxLibrary.FillId, Fill);
            block.SetFloat(VfxLibrary.FillAlphaId, FillAlpha);
            block.SetFloat(VfxLibrary.EdgeWidthId, Mathf.Max(0.002f, EdgeWidth));
            block.SetFloat(VfxLibrary.EdgeAlphaId, EdgeAlpha);
            block.SetFloat(VfxLibrary.BaseId, Base);
            block.SetFloat(VfxLibrary.SoftId, Mathf.Max(0.001f, Soft));
            block.SetFloat(VfxLibrary.RimWidthId, RimWidth);
            block.SetFloat(VfxLibrary.RimAlphaId, RimAlpha);
            block.SetFloat(VfxLibrary.TicksId, Ticks);
            block.SetFloat(VfxLibrary.SpinId, Spin);
            block.SetFloat(VfxLibrary.NoiseId, Noise);
            block.SetFloat(VfxLibrary.SeedId, Seed);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Solo el tinte (quads de brillo con el shader de particulas).</summary>
        public static void Tint(Renderer renderer, MaterialPropertyBlock block, Color color)
        {
            if (renderer == null || block == null)
                return;
            block.Clear();
            block.SetColor(VfxLibrary.ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
