using System;

namespace Vhaldren.EditorTools.Mundo
{
    /// <summary>
    /// Ruido de Perlin 2D propio, determinista y seguro entre hilos (el terreno se
    /// calcula en paralelo y Mathf.PerlinNoise es nativo).
    /// </summary>
    public sealed class MundoRuido
    {
        private readonly int[] perm = new int[512];

        public MundoRuido(int seed)
        {
            int[] p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            Random rng = new Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = p[i]; p[i] = p[j]; p[j] = t;
            }
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        /// <summary>Ruido en [-1, 1] aproximadamente.</summary>
        public float Noise(float x, float y)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float xf = x - xi, yf = y - yi;
            xi &= 255; yi &= 255;
            float u = Fade(xf), v = Fade(yf);
            int aa = perm[perm[xi] + yi], ab = perm[perm[xi] + yi + 1];
            int ba = perm[perm[xi + 1] + yi], bb = perm[perm[xi + 1] + yi + 1];
            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            return Lerp(x1, x2, v) * 0.85f;
        }

        /// <summary>Suma fractal en [-1, 1].</summary>
        public float Fbm(float x, float y, int octaves, float lacunarity = 2.03f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x * f + i * 17.13f, y * f - i * 9.71f) * amp;
                norm += amp;
                amp *= gain;
                f *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>Ruido de crestas en [0, 1] (montañas afiladas).</summary>
        public float Ridged(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, f = 1f, prev = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Math.Abs(Noise(x * f + i * 31.7f, y * f + i * 11.3f));
                n *= n;
                sum += n * amp * prev;
                prev = n;
                amp *= 0.5f;
                f *= 2.1f;
            }
            return Math.Min(1f, sum * 1.25f);
        }
    }

    public static class MundoGeo
    {
        public static float Smooth(float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }

        public static float Clamp01(float t) => t < 0f ? 0f : (t > 1f ? 1f : t);

        /// <summary>Distancia de (px,pz) al segmento AB y parametro t del punto mas cercano.</summary>
        public static float DistSeg(float px, float pz, float ax, float az, float bx, float bz, out float t)
        {
            float dx = bx - ax, dz = bz - az;
            float len2 = dx * dx + dz * dz;
            t = len2 > 1e-6f ? ((px - ax) * dx + (pz - az) * dz) / len2 : 0f;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            float cx = ax + dx * t - px, cz = az + dz * t - pz;
            return (float)Math.Sqrt(cx * cx + cz * cz);
        }
    }
}
