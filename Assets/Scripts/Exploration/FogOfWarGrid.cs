using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Niebla de guerra del mapa: rejilla de celdas sobre los limites del mundo que se
    /// revelan al caminar. Un bit por celda; se guarda como texto base64. Logica pura
    /// (sin texturas ni PlayerPrefs) para poder probarla.
    /// </summary>
    public sealed class FogOfWarGrid
    {
        private readonly byte[] bits;
        private readonly Vector2 min;
        private readonly Vector2 cellSize;

        public int Width { get; }
        public int Height { get; }
        public int RevealedCount { get; private set; }
        public Vector2 Min => min;
        public Vector2 Max => min + new Vector2(cellSize.x * Width, cellSize.y * Height);
        public float Fraction => Width * Height > 0 ? (float)RevealedCount / (Width * Height) : 0f;

        public FogOfWarGrid(int width, int height, Vector2 worldMin, Vector2 worldMax)
        {
            Width = Mathf.Clamp(width, 1, 1024);
            Height = Mathf.Clamp(height, 1, 1024);
            Vector2 lo = Vector2.Min(worldMin, worldMax);
            Vector2 hi = Vector2.Max(worldMin, worldMax);
            Vector2 size = hi - lo;
            if (!(size.x > 0.01f)) size.x = 1f;
            if (!(size.y > 0.01f)) size.y = 1f;
            min = lo;
            cellSize = new Vector2(size.x / Width, size.y / Height);
            bits = new byte[(Width * Height + 7) / 8];
        }

        /// <summary>Rejilla con celdas de unos <paramref name="cellMetres"/> m (entre 32 y 256 por lado).</summary>
        public static FogOfWarGrid ForBounds(Vector2 worldMin, Vector2 worldMax, float cellMetres = 16f)
        {
            Vector2 size = worldMax - worldMin;
            float cell = Mathf.Max(1f, cellMetres);
            int w = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(size.x) / cell), 32, 256);
            int h = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(size.y) / cell), 32, 256);
            return new FogOfWarGrid(w, h, worldMin, worldMax);
        }

        public bool IsRevealed(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return false;
            int index = y * Width + x;
            return (bits[index >> 3] & (1 << (index & 7))) != 0;
        }

        public bool IsRevealedWorld(Vector2 world)
        {
            Vector2Int cell = CellOf(world);
            return IsRevealed(cell.x, cell.y);
        }

        /// <summary>Celda que contiene el punto (puede quedar fuera de la rejilla).</summary>
        public Vector2Int CellOf(Vector2 world) => new Vector2Int(
            Mathf.FloorToInt((world.x - min.x) / cellSize.x),
            Mathf.FloorToInt((world.y - min.y) / cellSize.y));

        public Vector2 CellCenter(int x, int y) =>
            min + new Vector2((x + 0.5f) * cellSize.x, (y + 0.5f) * cellSize.y);

        private bool Set(int x, int y)
        {
            int index = y * Width + x;
            int mask = 1 << (index & 7);
            if ((bits[index >> 3] & mask) != 0)
                return false;
            bits[index >> 3] = (byte)(bits[index >> 3] | mask);
            RevealedCount++;
            return true;
        }

        /// <summary>
        /// Revela las celdas cuyo centro queda dentro del circulo. Devuelve cuantas eran
        /// nuevas y, si se pasa una lista, añade sus indices (y * Width + x).
        /// </summary>
        public int Reveal(Vector2 center, float radius, List<int> changed = null)
        {
            if (!ExplorationMath.IsFinite(center) || !ExplorationMath.IsFinite(radius) || radius <= 0f)
                return 0;
            Vector2Int lo = CellOf(center - Vector2.one * radius);
            Vector2Int hi = CellOf(center + Vector2.one * radius);
            lo.x = Mathf.Max(0, lo.x); lo.y = Mathf.Max(0, lo.y);
            hi.x = Mathf.Min(Width - 1, hi.x); hi.y = Mathf.Min(Height - 1, hi.y);
            float radiusSquared = radius * radius;
            int added = 0;
            for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                {
                    if ((CellCenter(x, y) - center).sqrMagnitude > radiusSquared)
                        continue;
                    if (Set(x, y))
                    {
                        added++;
                        changed?.Add(y * Width + x);
                    }
                }
            return added;
        }

        public string Serialize() => Width + "x" + Height + ":" + Convert.ToBase64String(bits);

        /// <summary>Carga un texto de <see cref="Serialize"/>. Si las medidas no coinciden, no cambia nada.</summary>
        public bool Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data))
                return false;
            int colon = data.IndexOf(':');
            if (colon <= 0 || data.Substring(0, colon) != Width + "x" + Height)
                return false;
            byte[] loaded;
            try
            {
                loaded = Convert.FromBase64String(data.Substring(colon + 1));
            }
            catch (FormatException)
            {
                return false;
            }
            if (loaded.Length != bits.Length)
                return false;
            Array.Copy(loaded, bits, bits.Length);
            RevealedCount = 0;
            int total = Width * Height;
            for (int i = 0; i < total; i++)
                if ((bits[i >> 3] & (1 << (i & 7))) != 0)
                    RevealedCount++;
            return true;
        }
    }
}
