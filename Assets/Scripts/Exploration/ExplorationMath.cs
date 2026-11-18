using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Calculos puros de exploracion, sin escena: region por posicion, distancia a rios,
    /// profundidad del agua y paso de coordenadas del mundo al mapa. Probados en EditMode.
    /// </summary>
    public static class ExplorationMath
    {
        public static Vector2 Planar(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>
        /// Region cuyo radio contiene el punto; si varias lo contienen, la de centro mas
        /// cercano. -1 si ninguna.
        /// </summary>
        public static int FindRegionIndex(IReadOnlyList<AtlasRegion> regions, Vector2 point)
        {
            if (regions == null || !IsFinite(point))
                return -1;
            int best = -1;
            float bestSquared = float.PositiveInfinity;
            for (int i = 0; i < regions.Count; i++)
            {
                AtlasRegion region = regions[i];
                if (region == null || !(region.radio > 0f))
                    continue;
                float squared = (point - region.centro).sqrMagnitude;
                if (squared <= region.radio * region.radio && squared < bestSquared)
                {
                    bestSquared = squared;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Region de centro mas cercano aunque el punto quede fuera de todas. -1 si no hay.</summary>
        public static int FindNearestRegionIndex(IReadOnlyList<AtlasRegion> regions, Vector2 point)
        {
            if (regions == null || !IsFinite(point))
                return -1;
            int best = -1;
            float bestSquared = float.PositiveInfinity;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i] == null)
                    continue;
                float squared = (point - regions[i].centro).sqrMagnitude;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Rectangulo que abarca todos los circulos de region (o ±1024 si no hay).</summary>
        public static void RegionBounds(IReadOnlyList<AtlasRegion> regions, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            if (regions != null)
                for (int i = 0; i < regions.Count; i++)
                {
                    AtlasRegion r = regions[i];
                    if (r == null)
                        continue;
                    min = Vector2.Min(min, r.centro - Vector2.one * r.radio);
                    max = Vector2.Max(max, r.centro + Vector2.one * r.radio);
                }
            if (!IsFinite(min) || !IsFinite(max) || max.x - min.x < 1f || max.y - min.y < 1f)
            {
                min = new Vector2(-1024f, -1024f);
                max = new Vector2(1024f, 1024f);
            }
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared < 1e-8f)
                return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Distancia minima del punto a la polilinea (infinito si no tiene puntos).</summary>
        public static float DistanceToPolyline(IReadOnlyList<Vector2> points, Vector2 p)
        {
            if (points == null || points.Count == 0)
                return float.PositiveInfinity;
            if (points.Count == 1)
                return Vector2.Distance(p, points[0]);
            float best = float.PositiveInfinity;
            for (int i = 0; i < points.Count - 1; i++)
                best = Mathf.Min(best, DistanceToSegment(p, points[i], points[i + 1]));
            return best;
        }

        public static Rect PolylineBounds(IReadOnlyList<Vector2> points, float margin)
        {
            if (points == null || points.Count == 0)
                return new Rect(0f, 0f, 0f, 0f);
            Vector2 min = points[0], max = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }
            min -= Vector2.one * margin;
            max += Vector2.one * margin;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// Perfil de cauce parabolico: <paramref name="maxDepth"/> en el centro y cero en la
        /// orilla (distancia = <paramref name="halfWidth"/>).
        /// </summary>
        public static float ChannelDepth(float distance, float halfWidth, float maxDepth)
        {
            if (!IsFinite(distance) || !IsFinite(halfWidth) || !IsFinite(maxDepth) ||
                halfWidth <= 0f || maxDepth <= 0f || distance >= halfWidth)
                return 0f;
            float x = Mathf.Max(0f, distance) / halfWidth;
            return maxDepth * (1f - x * x);
        }

        /// <summary>Altura del agua por encima de los pies; nunca negativa.</summary>
        public static float SurfaceDepth(float waterLevel, float feetY)
        {
            if (!IsFinite(waterLevel) || !IsFinite(feetY))
                return 0f;
            return Mathf.Max(0f, waterLevel - feetY);
        }

        /// <summary>Del plano XZ a 0..1 dentro de los limites del mapa (x = este, y = norte).</summary>
        public static Vector2 WorldToMap01(Vector2 world, Vector2 min, Vector2 max)
        {
            Vector2 size = max - min;
            return new Vector2(
                size.x > 1e-4f ? (world.x - min.x) / size.x : 0.5f,
                size.y > 1e-4f ? (world.y - min.y) / size.y : 0.5f);
        }

        public static Vector2 Map01ToWorld(Vector2 uv, Vector2 min, Vector2 max) =>
            new Vector2(Mathf.LerpUnclamped(min.x, max.x, uv.x), Mathf.LerpUnclamped(min.y, max.y, uv.y));

        /// <summary>Angulo en grados de pantalla (0 = derecha, 90 = abajo) de una direccion del mundo.</summary>
        public static float ScreenAngle(Vector3 worldDirection) =>
            Mathf.Atan2(-worldDirection.z, worldDirection.x) * Mathf.Rad2Deg;

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
    }
}
