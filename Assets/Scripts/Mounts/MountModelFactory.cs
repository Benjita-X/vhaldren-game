using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Mounts
{
    /// <summary>
    /// Crea el modelo visual de una montura: el prefab Resources "Monturas/montura_&lt;id&gt;"
    /// si existe o, si no, un sustituto de primitivas con las mismas partes con nombre
    /// (cuerpo, cabeza, patas, cola, alas y "silla"), de modo que
    /// <see cref="QuadrupedGait"/> lo anima igual. Nunca falla. Quita colisionadores y
    /// cuerpos rigidos: la montura es solo visual (el CharacterController del jugador manda).
    /// </summary>
    public static class MountModelFactory
    {
        public const string ResourcePrefix = "Monturas/montura_";

        private static readonly Dictionary<Color, Material> litMaterials = new Dictionary<Color, Material>();
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            litMaterials.Clear();
            prefabs.Clear();
        }

        public static GameObject LoadPrefab(string id)
        {
            string key = MountUnlocks.Sanitize(id);
            if (prefabs.TryGetValue(key, out GameObject cached))
                return cached;
            GameObject prefab = key.Length > 0 ? Resources.Load<GameObject>(ResourcePrefix + key) : null;
            prefabs[key] = prefab;
            return prefab;
        }

        /// <summary>Instancia el modelo bajo <paramref name="parent"/> (posicion y giro locales a cero).</summary>
        public static GameObject Create(string id, Transform parent, out bool fromPrefab)
        {
            MountDefinition definition = MountCatalog.Get(id);
            GameObject prefab = LoadPrefab(id);
            GameObject model;
            fromPrefab = prefab != null;
            if (fromPrefab)
            {
                model = Object.Instantiate(prefab, parent, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
            }
            else
                model = BuildSubstitute(definition, parent);
            model.name = "Montura_" + definition.Id;
            StripPhysics(model);
            EnsureSaddle(model);
            return model;
        }

        /// <summary>Quita colisionadores, cuerpos rigidos y animadores propios del modelo.</summary>
        public static void StripPhysics(GameObject model)
        {
            if (model == null)
                return;
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
                Destroy(c);
            }
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
                Destroy(body);
            // Un Animator con clips pisaria la animacion procedural.
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
        }

        private static void Destroy(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        /// <summary>El Transform "silla"; si falta, se crea sobre el lomo (lo mas alto del modelo, en el centro).</summary>
        public static Transform EnsureSaddle(GameObject model)
        {
            Transform saddle = QuadrupedGait.FindDeep(model.transform, "silla");
            if (saddle != null)
                return saddle;
            Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) bounds = r.bounds; else bounds.Encapsulate(r.bounds);
                any = true;
            }
            GameObject seat = new GameObject("silla");
            seat.transform.SetParent(model.transform, false);
            Transform body = QuadrupedGait.FindDeep(model.transform, "cuerpo");
            Vector3 world = any
                ? new Vector3(bounds.center.x, Mathf.Lerp(bounds.center.y, bounds.max.y, 0.75f), bounds.center.z)
                : model.transform.position + Vector3.up * 1.5f;
            if (body != null)
                seat.transform.SetParent(body, true);
            seat.transform.position = world;
            return seat.transform;
        }

        // ── Sustituto de primitivas ────────────────────────────────────

        private static Material Lit(Color color)
        {
            if (litMaterials.TryGetValue(color, out Material material) && material != null)
                return material;
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Montura sustituta", color = color };
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", 0.18f);
            litMaterials[color] = material;
            return material;
        }

        private static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPosition;
            return pivot.transform;
        }

        private static Transform Shape(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale,
            Color color, Vector3 euler = default, bool glow = false)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = type.ToString().ToLowerInvariant();
            Collider collider = shape.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
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

        /// <summary>Modelo de primitivas con las partes con nombre. Raiz en el suelo, mirando a +Z.</summary>
        public static GameObject BuildSubstitute(MountDefinition d, Transform parent)
        {
            GameObject root = new GameObject("Montura_" + d.Id);
            root.transform.SetParent(parent, false);
            Transform r = root.transform;
            float s = Mathf.Clamp(d.Size, 0.5f, 2f);
            Color body = d.BodyColor, accent = d.AccentColor;
            Color dark = Color.Lerp(body, Color.black, 0.45f);

            if (d.Shape == MountShape.Phoenix)
            {
                BuildBird(r, d, s);
                return root;
            }

            // Proporciones por silueta.
            float legHeight = 1.05f, bodyLength = 1.7f, bodyWidth = 0.75f, legWidth = 0.17f;
            switch (d.Shape)
            {
                case MountShape.Wolf: legHeight = 0.8f; bodyLength = 1.5f; bodyWidth = 0.6f; legWidth = 0.13f; break;
                case MountShape.Lion: legHeight = 0.85f; bodyLength = 1.6f; bodyWidth = 0.75f; legWidth = 0.17f; break;
                case MountShape.Bear: legHeight = 0.75f; bodyLength = 1.7f; bodyWidth = 1.05f; legWidth = 0.26f; break;
                case MountShape.Griffin: legHeight = 0.9f; bodyLength = 1.6f; bodyWidth = 0.75f; legWidth = 0.16f; break;
            }
            legHeight *= s; bodyLength *= s; bodyWidth *= s; legWidth *= s;
            float bodyY = legHeight + bodyWidth * 0.4f;
            float halfLength = bodyLength * 0.5f;

            Transform torso = Pivot("cuerpo", r, new Vector3(0f, bodyY, 0f));
            Shape(PrimitiveType.Capsule, torso, Vector3.zero, new Vector3(bodyWidth, halfLength, bodyWidth * 1.05f), body, new Vector3(90f, 0f, 0f));

            // Cuello y cabeza (la cabeza gira desde la base del cuello).
            Transform headPivot = Pivot("cabeza", torso, new Vector3(0f, bodyWidth * 0.25f, halfLength * 0.8f));
            float neck = d.Shape == MountShape.Horse ? 0.75f * s : d.Shape == MountShape.Bear ? 0.3f * s : 0.45f * s;
            Shape(PrimitiveType.Cube, headPivot, new Vector3(0f, neck * 0.45f, neck * 0.25f), new Vector3(0.3f * s, neck, 0.32f * s), body, new Vector3(35f, 0f, 0f));
            Vector3 headPos = new Vector3(0f, neck * 0.85f, neck * 0.55f + 0.18f * s);
            Vector3 headSize = d.Shape == MountShape.Horse ? new Vector3(0.3f, 0.32f, 0.68f) * s
                : d.Shape == MountShape.Bear ? new Vector3(0.48f, 0.42f, 0.5f) * s
                : new Vector3(0.36f, 0.34f, 0.52f) * s;
            Transform skull = Shape(PrimitiveType.Cube, headPivot, headPos, headSize, body, new Vector3(12f, 0f, 0f));
            // Ojos (los del corcel infernal brillan).
            bool glowingEyes = d.Id == "corcel_infernal" || d.Id == "lobo_bruma";
            Color eye = glowingEyes ? accent : new Color(0.05f, 0.04f, 0.04f);
            Shape(PrimitiveType.Sphere, skull, new Vector3(-0.42f, 0.18f, 0.25f), Vector3.one * 0.18f, eye, default, glowingEyes);
            Shape(PrimitiveType.Sphere, skull, new Vector3(0.42f, 0.18f, 0.25f), Vector3.one * 0.18f, eye, default, glowingEyes);

            switch (d.Shape)
            {
                case MountShape.Wolf:
                    Shape(PrimitiveType.Cube, skull, new Vector3(-0.3f, 0.65f, -0.15f), new Vector3(0.22f, 0.5f, 0.12f), dark, new Vector3(-10f, 0f, 12f));
                    Shape(PrimitiveType.Cube, skull, new Vector3(0.3f, 0.65f, -0.15f), new Vector3(0.22f, 0.5f, 0.12f), dark, new Vector3(-10f, 0f, -12f));
                    Shape(PrimitiveType.Cube, skull, new Vector3(0f, -0.15f, 0.65f), new Vector3(0.55f, 0.5f, 0.6f), accent);
                    break;
                case MountShape.Horse:
                    Shape(PrimitiveType.Cube, headPivot, new Vector3(0f, neck * 0.6f, 0f), new Vector3(0.08f, neck * 0.9f, 0.22f) * 1f, accent, new Vector3(35f, 0f, 0f), d.Id == "corcel_infernal");
                    break;
                case MountShape.Bear:
                    Shape(PrimitiveType.Sphere, skull, new Vector3(-0.38f, 0.55f, -0.2f), Vector3.one * 0.32f, dark);
                    Shape(PrimitiveType.Sphere, skull, new Vector3(0.38f, 0.55f, -0.2f), Vector3.one * 0.32f, dark);
                    for (int i = 0; i < 3; i++)
                        Shape(PrimitiveType.Cube, torso, new Vector3(0f, bodyWidth * 0.48f, (i - 1) * bodyLength * 0.26f),
                            new Vector3(bodyWidth * 0.85f, 0.1f * s, bodyLength * 0.2f), accent, new Vector3(0f, 0f, 0f));
                    break;
                case MountShape.Griffin:
                    Shape(PrimitiveType.Cube, skull, new Vector3(0f, -0.15f, 0.7f), new Vector3(0.35f, 0.4f, 0.55f), new Color(0.95f, 0.75f, 0.25f), new Vector3(25f, 0f, 0f));
                    Shape(PrimitiveType.Sphere, headPivot, new Vector3(0f, neck * 0.55f, neck * 0.2f), new Vector3(0.5f, 0.6f, 0.5f) * s, Color.Lerp(body, Color.white, 0.55f));
                    break;
            }
            if (d.Shape == MountShape.Lion)
                Shape(PrimitiveType.Sphere, headPivot, new Vector3(0f, neck * 0.7f, neck * 0.35f), Vector3.one * 0.75f * s, accent);

            // Patas: pivote en la cadera, la pata cuelga hasta el suelo.
            float legX = bodyWidth * 0.32f, frontZ = halfLength * 0.62f, backZ = -halfLength * 0.62f;
            Leg("pata_del_izq", r, new Vector3(-legX, legHeight, frontZ), legHeight, legWidth, body, dark);
            Leg("pata_del_der", r, new Vector3(legX, legHeight, frontZ), legHeight, legWidth, body, dark);
            Leg("pata_tras_izq", r, new Vector3(-legX, legHeight, backZ), legHeight, legWidth * 1.1f, body, dark);
            Leg("pata_tras_der", r, new Vector3(legX, legHeight, backZ), legHeight, legWidth * 1.1f, body, dark);

            // Cola.
            Transform tail = Pivot("cola", torso, new Vector3(0f, bodyWidth * 0.2f, -halfLength * 0.95f));
            float tailLength = d.Shape == MountShape.Bear ? 0.2f * s : d.Shape == MountShape.Wolf ? 0.75f * s : 0.85f * s;
            Shape(PrimitiveType.Cube, tail, new Vector3(0f, -tailLength * 0.3f, -tailLength * 0.45f),
                new Vector3(0.12f * s, 0.12f * s, tailLength), d.Shape == MountShape.Wolf ? accent : dark, new Vector3(-35f, 0f, 0f));
            if (d.Shape == MountShape.Lion || d.Shape == MountShape.Griffin)
                Shape(PrimitiveType.Sphere, tail, new Vector3(0f, -tailLength * 0.62f, -tailLength * 0.88f), Vector3.one * 0.2f * s, accent);

            if (d.Shape == MountShape.Griffin)
            {
                Wing("ala_izq", torso, -1f, bodyWidth, s * 1.7f, body, accent);
                Wing("ala_der", torso, 1f, bodyWidth, s * 1.7f, body, accent);
            }

            // Silla de montar con manta del color de acento.
            Transform saddle = Pivot("silla", torso, new Vector3(0f, bodyWidth * 0.55f, 0.05f * s));
            Shape(PrimitiveType.Cube, saddle, new Vector3(0f, -0.04f * s, 0f), new Vector3(bodyWidth * 0.95f, 0.08f * s, 0.6f * s),
                d.Shape == MountShape.Bear ? new Color(0.35f, 0.12f, 0.1f) : new Color(0.3f, 0.1f, 0.12f));
            Shape(PrimitiveType.Cube, saddle, new Vector3(0f, 0.02f * s, -0.22f * s), new Vector3(0.36f * s, 0.14f * s, 0.1f * s), new Color(0.25f, 0.16f, 0.1f));
            return root;
        }

        private static void Leg(string name, Transform parent, Vector3 hip, float length, float width, Color upper, Color lower)
        {
            Transform pivot = Pivot(name, parent, hip);
            Shape(PrimitiveType.Cube, pivot, new Vector3(0f, -length * 0.3f, 0f), new Vector3(width * 1.3f, length * 0.62f, width * 1.4f), upper);
            Shape(PrimitiveType.Cube, pivot, new Vector3(0f, -length * 0.76f, 0.02f), new Vector3(width, length * 0.48f, width), lower);
        }

        private static void Wing(string name, Transform parent, float side, float bodyWidth, float span, Color inner, Color outer)
        {
            Transform pivot = Pivot(name, parent, new Vector3(side * bodyWidth * 0.4f, bodyWidth * 0.35f, 0.25f));
            Shape(PrimitiveType.Cube, pivot, new Vector3(side * span * 0.28f, 0f, -0.1f), new Vector3(span * 0.56f, 0.06f, 0.7f * span * 0.5f), inner);
            Shape(PrimitiveType.Cube, pivot, new Vector3(side * span * 0.75f, 0f, -0.22f), new Vector3(span * 0.5f, 0.05f, 0.55f * span * 0.5f), outer, new Vector3(0f, side * -12f, 0f));
        }

        /// <summary>Ave mitologica: cuerpo, cuello y cabeza, dos patas traseras, grandes alas y cola de plumas.</summary>
        private static void BuildBird(Transform r, MountDefinition d, float s)
        {
            Color body = d.BodyColor, accent = d.AccentColor;
            float legHeight = 0.75f * s;
            Transform torso = Pivot("cuerpo", r, new Vector3(0f, legHeight + 0.35f * s, 0f));
            Shape(PrimitiveType.Sphere, torso, Vector3.zero, new Vector3(0.8f, 0.7f, 1.4f) * s, body);
            Shape(PrimitiveType.Sphere, torso, new Vector3(0f, -0.08f * s, 0.2f * s), new Vector3(0.6f, 0.5f, 0.9f) * s, accent, default, true);
            Transform headPivot = Pivot("cabeza", torso, new Vector3(0f, 0.25f * s, 0.6f * s));
            Shape(PrimitiveType.Cube, headPivot, new Vector3(0f, 0.25f * s, 0.12f * s), new Vector3(0.22f, 0.55f, 0.22f) * s, body, new Vector3(25f, 0f, 0f));
            Transform skull = Shape(PrimitiveType.Sphere, headPivot, new Vector3(0f, 0.55f * s, 0.28f * s), new Vector3(0.36f, 0.34f, 0.42f) * s, body);
            Shape(PrimitiveType.Cube, skull, new Vector3(0f, -0.1f, 0.65f), new Vector3(0.3f, 0.25f, 0.6f), new Color(0.95f, 0.8f, 0.3f), new Vector3(20f, 0f, 0f));
            Shape(PrimitiveType.Sphere, skull, new Vector3(-0.38f, 0.15f, 0.25f), Vector3.one * 0.2f, accent, default, true);
            Shape(PrimitiveType.Sphere, skull, new Vector3(0.38f, 0.15f, 0.25f), Vector3.one * 0.2f, accent, default, true);
            Shape(PrimitiveType.Cube, skull, new Vector3(0f, 0.6f, -0.3f), new Vector3(0.12f, 0.6f, 0.5f), accent, new Vector3(-30f, 0f, 0f), true);
            Leg("pata_tras_izq", r, new Vector3(-0.2f * s, legHeight, -0.05f * s), legHeight, 0.08f * s, body, new Color(0.25f, 0.2f, 0.15f));
            Leg("pata_tras_der", r, new Vector3(0.2f * s, legHeight, -0.05f * s), legHeight, 0.08f * s, body, new Color(0.25f, 0.2f, 0.15f));
            Wing("ala_izq", torso, -1f, 0.8f * s, 2.2f * s, body, accent);
            Wing("ala_der", torso, 1f, 0.8f * s, 2.2f * s, body, accent);
            Transform tail = Pivot("cola", torso, new Vector3(0f, 0.05f * s, -0.65f * s));
            for (int i = -1; i <= 1; i++)
                Shape(PrimitiveType.Cube, tail, new Vector3(i * 0.14f * s, -0.05f * s, -0.55f * s), new Vector3(0.12f * s, 0.04f * s, 1.1f * s),
                    i == 0 ? accent : body, new Vector3(-12f, i * 14f, 0f), i == 0);
            Transform saddle = Pivot("silla", torso, new Vector3(0f, 0.36f * s, 0.05f * s));
            Shape(PrimitiveType.Cube, saddle, new Vector3(0f, -0.04f * s, 0f), new Vector3(0.62f, 0.08f, 0.55f) * s, new Color(0.3f, 0.1f, 0.12f));
        }
    }
}
