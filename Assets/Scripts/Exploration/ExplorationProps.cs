using System;
using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Formas de respaldo para cofres, piedras y estelas cuando el generador no les pone
    /// modelo: primitivas sin colisionador con materiales cacheados. Tambien busqueda de
    /// hijos por nombre (exacto o que contenga un texto).
    /// </summary>
    public static class ExplorationProps
    {
        private static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => materials.Clear();

        public static bool HasRenderers(GameObject target) =>
            target != null && target.GetComponentInChildren<Renderer>(true) != null;

        public static Material Lit(Color color)
        {
            if (materials.TryGetValue(color, out Material material) && material != null)
                return material;
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Exploracion", color = color };
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", 0.2f);
            materials[color] = material;
            return material;
        }

        /// <summary>Primitiva visual (sin colisionador). glow = sin iluminacion (brilla en la oscuridad).</summary>
        public static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 scale, Color color, Vector3 euler = default, bool glow = false)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            Collider collider = shape.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
                else UnityEngine.Object.DestroyImmediate(collider);
            }
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = localPosition;
            shape.transform.localRotation = Quaternion.Euler(euler);
            shape.transform.localScale = scale;
            Renderer renderer = shape.GetComponent<Renderer>();
            if (glow)
            {
                renderer.sharedMaterial = PrototypeVfx.SharedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrototypeVfx.SetColor(renderer, color);
            }
            else
                renderer.sharedMaterial = Lit(color);
            return shape.transform;
        }

        public static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPosition;
            return pivot.transform;
        }

        /// <summary>Primer hijo (en anchura) cuyo nombre contiene el texto, sin distinguir mayusculas.</summary>
        public static Transform FindContaining(Transform root, string fragment)
        {
            if (root == null || string.IsNullOrEmpty(fragment))
                return null;
            Queue<Transform> pending = new Queue<Transform>();
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                Transform current = pending.Dequeue();
                if (current != root && current.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return current;
                for (int i = 0; i < current.childCount; i++)
                    pending.Enqueue(current.GetChild(i));
            }
            return null;
        }

        /// <summary>Caja que abarca los renderers del objeto (o un punto si no tiene).</summary>
        public static Bounds RendererBounds(GameObject target)
        {
            Bounds bounds = new Bounds(target.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer)
                    continue;
                if (!any) bounds = r.bounds; else bounds.Encapsulate(r.bounds);
                any = true;
            }
            return bounds;
        }
    }
}
