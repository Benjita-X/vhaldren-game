using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Utilidades de efectos con formas nativas de Unity para el prototipo:
    /// esferas, discos y anillos sin collider, con un solo material compartido
    /// teñido por MaterialPropertyBlock (no crea un material por efecto).
    /// </summary>
    public static class PrototypeVfx
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static Material sharedMaterial;
        private static MaterialPropertyBlock block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sharedMaterial = null;
            block = null;
        }

        /// <summary>
        /// Material sin iluminacion y con transparencia. Sprites/Default siempre se
        /// incluye en builds del pipeline integrado, que es el que usa el proyecto.
        /// </summary>
        public static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null)
                        shader = Shader.Find("Unlit/Color");
                    if (shader != null)
                        sharedMaterial = new Material(shader) { name = "VhaldrenPrototypeVfx" };
                }
                return sharedMaterial;
            }
        }

        /// <summary>Crea una forma primitiva visual: se le quita el collider.</summary>
        public static GameObject CreateShape(PrimitiveType type, string name, Transform parent,
            Vector3 position, Vector3 scale, Color color)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            Collider collider = shape.GetComponent<Collider>();
            if (collider != null)
            {
                // Destroy diferido dejaria un frame con collider: puede bloquear
                // raycasts del propio hechizo. DestroyImmediate es seguro aqui.
                collider.enabled = false;
                if (Application.isPlaying)
                    Object.Destroy(collider);
                else
                    Object.DestroyImmediate(collider);
            }
            if (parent != null)
                shape.transform.SetParent(parent, false);
            shape.transform.position = position;
            shape.transform.localScale = scale;
            Renderer renderer = shape.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = SharedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                SetColor(renderer, color);
            }
            return shape;
        }

        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;
            if (block == null)
                block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(ColorProperty, color);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Anillo horizontal dibujado con LineRenderer en espacio local.</summary>
        public static LineRenderer CreateRing(string name, Transform parent, float radius,
            float width, Color color, int segments = 64)
        {
            GameObject ringObject = new GameObject(name);
            if (parent != null)
                ringObject.transform.SetParent(parent, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.sharedMaterial = SharedMaterial;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.alignment = LineAlignment.TransformZ;
            ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.widthMultiplier = width;
            ring.startColor = color;
            ring.endColor = color;
            SetRingRadius(ring, radius, segments);
            return ring;
        }

        public static void SetRingRadius(LineRenderer ring, float radius, int segments = 64)
        {
            if (ring == null)
                return;
            segments = Mathf.Max(8, segments);
            if (ring.positionCount != segments)
                ring.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                // El anillo esta girado 90 grados en X: el plano local XY queda horizontal.
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius, 0f));
            }
        }

        public static Light CreatePointLight(Transform parent, Color color, float range,
            float intensity)
        {
            GameObject lightObject = new GameObject("Luz");
            lightObject.transform.SetParent(parent, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
