using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Aspecto provisional de cada tipo de enemigo para reconocerlo de un vistazo:
    /// color propio y accesorios (ballesta, baston, orbe, runas). El lobo cambia el
    /// cuerpo humanoide por uno cuadrupedo. Solo presentacion: sin colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyArchetypeVisual : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Transform props;
        private string builtFor;

        public string BuiltFor => builtFor;

        public void Build(EnemyArchetype data)
        {
            if (data == null || builtFor == data.Id)
                return;
            Transform anchor = transform.Find("Visual enemigo");
            if (anchor == null)
                anchor = transform;
            if (props != null)
                Destroy(props.gameObject);
            props = new GameObject("Accesorios " + data.DisplayName).transform;
            props.SetParent(anchor, false);
            builtFor = data.Id;

            // Con un personaje KayKit texturizado no se tiñe ni se añaden armas falsas:
            // solo los brillos que delatan magia (mago, sanadora, elite).
            Transform realModel = AuthoredVisual.FindRoot(anchor);
            bool authored = realModel != null;
            if (authored)
            {
                if (data.Role == EnemyRole.Caster)
                    Orb(new Vector3(0.45f, 0.95f, 0.25f), 0.2f, new Color(1f, 0.45f, 0.1f));
                else if (data.Role == EnemyRole.Support)
                    Orb(new Vector3(0f, 1.35f, 0f), 0.24f, new Color(0.35f, 1f, 0.45f));
                else if (data.Role == EnemyRole.Elite)
                {
                    LineRenderer runes = PrototypeVfx.CreateRing("Runas del centinela", props, 0.75f, 0.05f, new Color(0.4f, 0.7f, 1f));
                    if (runes != null)
                        runes.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                }
                else if (data.Role == EnemyRole.Brute)
                    realModel.localScale *= 1.2f;   // no tocar el ancla: EnemyHitReaction la restaura
                return;
            }

            Tint(anchor, data.Tint);
            Color glow = data.Tint * 1.6f;
            switch (data.Role)
            {
                case EnemyRole.Ranged:
                    Shape(PrimitiveType.Cube, new Vector3(0.42f, 0.05f, 0.35f), new Vector3(0.06f, 0.06f, 0.75f), new Color(0.35f, 0.22f, 0.12f));
                    Shape(PrimitiveType.Cube, new Vector3(0.42f, 0.05f, 0.62f), new Vector3(0.7f, 0.05f, 0.06f), new Color(0.3f, 0.2f, 0.1f));
                    break;
                case EnemyRole.Caster:
                    Shape(PrimitiveType.Cylinder, new Vector3(0.45f, 0.1f, 0.2f), new Vector3(0.06f, 0.9f, 0.06f), new Color(0.25f, 0.15f, 0.1f));
                    Orb(new Vector3(0.45f, 1.05f, 0.2f), 0.22f, new Color(1f, 0.45f, 0.1f));
                    break;
                case EnemyRole.Support:
                    Orb(new Vector3(0f, 1.35f, 0f), 0.26f, new Color(0.35f, 1f, 0.45f));
                    break;
                case EnemyRole.Brute:
                    Transform bulk = anchor.Find("Modelo texturizado");
                    if (bulk != null)
                        bulk.localScale = new Vector3(bulk.localScale.x * 1.3f, bulk.localScale.y * 1.12f, bulk.localScale.z * 1.3f);
                    Shape(PrimitiveType.Cube, new Vector3(-0.48f, 0.42f, 0f), new Vector3(0.36f, 0.22f, 0.42f), data.Tint * 0.8f);
                    Shape(PrimitiveType.Cube, new Vector3(0.48f, 0.42f, 0f), new Vector3(0.36f, 0.22f, 0.42f), data.Tint * 0.8f);
                    Shape(PrimitiveType.Cylinder, new Vector3(0.55f, -0.1f, 0.3f), new Vector3(0.18f, 0.55f, 0.18f), new Color(0.3f, 0.3f, 0.32f));
                    break;
                case EnemyRole.Elite:
                    LineRenderer ring = PrototypeVfx.CreateRing("Runas del centinela", props, 0.75f, 0.05f, new Color(0.4f, 0.7f, 1f));
                    if (ring != null)
                        ring.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    Orb(new Vector3(0f, 0.95f, 0f), 0.12f, new Color(0.4f, 0.7f, 1f));
                    break;
                case EnemyRole.Beast:
                    BuildBeast(anchor, data.Tint);
                    break;
            }
            if (data.Role == EnemyRole.Melee && data.Id == "esqueleto")
                Shape(PrimitiveType.Cube, new Vector3(0f, 0.1f, 0.26f), new Vector3(0.5f, 0.5f, 0.05f), new Color(0.9f, 0.88f, 0.8f));
            else if (data.Role == EnemyRole.Melee)
                Shape(PrimitiveType.Cube, new Vector3(0.42f, -0.05f, 0.3f), new Vector3(0.05f, 0.05f, 0.5f), new Color(0.75f, 0.75f, 0.78f));
        }

        private void BuildBeast(Transform anchor, Color fur)
        {
            // Ocultar el cuerpo humanoide: un lobo no camina erguido.
            foreach (Renderer renderer in anchor.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.IsChildOf(props))
                    renderer.enabled = false;
            Shape(PrimitiveType.Capsule, new Vector3(0f, -0.35f, 0f), new Vector3(0.55f, 0.55f, 0.55f), fur, new Vector3(90f, 0f, 0f));
            Shape(PrimitiveType.Sphere, new Vector3(0f, -0.15f, 0.62f), new Vector3(0.42f, 0.38f, 0.48f), fur * 1.1f);
            Shape(PrimitiveType.Cube, new Vector3(0f, -0.22f, 0.9f), new Vector3(0.18f, 0.14f, 0.26f), fur * 0.8f);
            Shape(PrimitiveType.Cube, new Vector3(-0.12f, 0.08f, 0.6f), new Vector3(0.07f, 0.16f, 0.07f), fur * 0.7f);
            Shape(PrimitiveType.Cube, new Vector3(0.12f, 0.08f, 0.6f), new Vector3(0.07f, 0.16f, 0.07f), fur * 0.7f);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.2f : 0.2f;
                float z = i < 2 ? 0.35f : -0.35f;
                Shape(PrimitiveType.Cube, new Vector3(x, -0.75f, z), new Vector3(0.1f, 0.42f, 0.1f), fur * 0.75f);
            }
            Shape(PrimitiveType.Cube, new Vector3(0f, -0.2f, -0.62f), new Vector3(0.08f, 0.08f, 0.4f), fur * 0.9f, new Vector3(-25f, 0f, 0f));
            Shape(PrimitiveType.Cube, new Vector3(0f, -0.08f, 0.83f), new Vector3(0.26f, 0.04f, 0.04f), new Color(0.6f, 0.9f, 1f));
        }

        private void Shape(PrimitiveType type, Vector3 localPosition, Vector3 scale, Color color, Vector3 euler = default)
        {
            GameObject shape = PrototypeVfx.CreateShape(type, type.ToString(), props, localPosition, scale, color);
            if (shape == null)
                return;
            shape.transform.localPosition = localPosition;
            shape.transform.localRotation = Quaternion.Euler(euler);
        }

        private void Orb(Vector3 localPosition, float size, Color color)
        {
            Shape(PrimitiveType.Sphere, localPosition, Vector3.one * size, color);
            Light light = PrototypeVfx.CreatePointLight(props, color, 3f, 1.4f);
            if (light != null)
                light.transform.localPosition = localPosition;
        }

        private static void Tint(Transform anchor, Color color)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer renderer in anchor.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is LineRenderer)
                    continue;
                renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, Color.Lerp(Color.white, color, 0.65f));
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
