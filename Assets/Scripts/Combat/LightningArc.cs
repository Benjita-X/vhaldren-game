using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Rayo en zigzag entre varios puntos: un nucleo fino casi blanco y un resplandor
    /// ancho del color del elemento. El zigzag se rehace cada pocos milisegundos para
    /// que chisporrotee y se apaga parpadeando. Con jitter 0 sirve de haz recto
    /// (pilar de luz, virote). Reutilizable; solo visual.
    /// </summary>
    public sealed class LightningArc : MonoBehaviour
    {
        public const int MaxAnchors = 8;
        private const int Subdivisions = 9;
        private const float RebuildSeconds = 0.035f;
        private static readonly Func<Transform, LightningArc> Factory = Create;

        private readonly Vector3[] anchors = new Vector3[MaxAnchors];
        private readonly Vector3[] points = new Vector3[(MaxAnchors - 1) * Subdivisions + 1];
        private LineRenderer glowLine;
        private LineRenderer coreLine;
        private VfxPool<LightningArc> pool;
        private int anchorCount;
        private int pointCount;
        private Color color;
        private float width;
        private float jitter;
        private float duration;
        private float elapsed;
        private float nextRebuild;

        public int PointCount => pointCount;
        public bool Playing => isActiveAndEnabled && elapsed < duration;

        public static LightningArc Strike(Vector3 from, Vector3 to, Color color, float width, float duration, float jitter)
        {
            LightningArc arc = Take();
            if (arc == null)
                return null;
            arc.anchors[0] = from;
            arc.anchors[1] = to;
            arc.anchorCount = 2;
            arc.Begin(color, width, duration, jitter);
            return arc;
        }

        /// <summary>Rayo que recorre una cadena de puntos (hasta <see cref="MaxAnchors"/>).</summary>
        public static LightningArc Strike(List<Vector3> path, Color color, float width, float duration, float jitter)
        {
            if (path == null || path.Count < 2)
                return null;
            LightningArc arc = Take();
            if (arc == null)
                return null;
            arc.anchorCount = Mathf.Min(path.Count, MaxAnchors);
            for (int i = 0; i < arc.anchorCount; i++)
                arc.anchors[i] = path[i];
            arc.Begin(color, width, duration, jitter);
            return arc;
        }

        private static LightningArc Take()
        {
            VfxHost host = VfxHost.Get();
            if (host == null)
                return null;
            VfxPool<LightningArc> reserve = host.Pool(Factory);
            LightningArc arc = reserve.Take();
            arc.pool = reserve;
            return arc;
        }

        private static LightningArc Create(Transform parent)
        {
            GameObject root = new GameObject("Rayo — sin colision");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            LightningArc arc = root.AddComponent<LightningArc>();
            arc.glowLine = Line("Resplandor", root.transform, 0);
            arc.coreLine = Line("Nucleo", root.transform, 1);
            return arc;
        }

        private static LineRenderer Line(string name, Transform parent, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.numCornerVertices = 1;
            line.sharedMaterial = VfxLibrary.AdditiveBeam;
            line.sortingOrder = order;
            VfxLibrary.ConfigureRenderer(line);
            return line;
        }

        private void Begin(Color tint, float size, float seconds, float zigzag)
        {
            color = VfxLibrary.WithAlpha(tint, 1f);
            width = Mathf.Max(0.01f, size);
            jitter = Mathf.Max(0f, zigzag);
            duration = Mathf.Max(0.05f, seconds);
            elapsed = 0f;
            nextRebuild = RebuildSeconds;
            Rebuild();
            gameObject.SetActive(true);
            Refresh();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (jitter > 0f && elapsed >= nextRebuild)
            {
                nextRebuild = elapsed + RebuildSeconds;
                Rebuild();
            }
            Refresh();
            if (elapsed >= duration)
            {
                if (pool != null)
                    pool.Release(this);
                else
                    Destroy(gameObject);
            }
        }

        private void Rebuild()
        {
            pointCount = 0;
            for (int segment = 0; segment < anchorCount - 1; segment++)
            {
                Vector3 a = anchors[segment];
                Vector3 b = anchors[segment + 1];
                Vector3 along = b - a;
                float length = along.magnitude;
                Vector3 direction = length > 0.001f ? along / length : Vector3.up;
                float amplitude = jitter * Mathf.Clamp(length * 0.18f, 0.25f, 1.6f);
                int steps = jitter > 0f ? Subdivisions : 1;
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    Vector3 point = a + along * t;
                    if (s > 0 && jitter > 0f)
                    {
                        // Desviacion perpendicular, nula en los extremos de cada tramo.
                        Vector3 offset = Vector3.ProjectOnPlane(UnityEngine.Random.insideUnitSphere, direction);
                        point += offset * amplitude * Mathf.Sin(t * Mathf.PI);
                    }
                    points[pointCount++] = point;
                }
            }
            points[pointCount++] = anchors[Mathf.Max(0, anchorCount - 1)];
            glowLine.positionCount = pointCount;
            coreLine.positionCount = pointCount;
            glowLine.SetPositions(points);
            coreLine.SetPositions(points);
        }

        private void Refresh()
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float fade = Mathf.Pow(1f - t, 1.4f);
            // Parpadeo de descarga electrica (no en los haces rectos).
            float flicker = jitter > 0f ? (UnityEngine.Random.value < 0.18f ? 0.35f : 1f) : 1f;
            float alpha = fade * flicker;
            glowLine.widthMultiplier = width * 3.2f * Mathf.Lerp(1f, 0.6f, t);
            coreLine.widthMultiplier = width * Mathf.Lerp(1f, 0.4f, t);
            Color glow = VfxLibrary.WithAlpha(color, 0.6f * alpha);
            Color core = VfxLibrary.WithAlpha(Color.Lerp(color, Color.white, 0.75f), alpha);
            glowLine.startColor = glowLine.endColor = glow;
            coreLine.startColor = coreLine.endColor = core;
        }
    }
}
