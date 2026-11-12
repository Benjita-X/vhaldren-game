using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Survival
{
    /// <summary>
    /// Crea la representacion de una pieza con formas basicas. La raiz esta en la base
    /// (centro inferior). Una vista previa no tiene colliders ni funciones.
    /// </summary>
    public static class BuildPieceFactory
    {
        private static readonly Dictionary<Color, Material> solids = new Dictionary<Color, Material>();
        private static Material previewOk;
        private static Material previewBad;

        public static GameObject Create(BuildPieceDefinition piece, Vector3 basePosition, int rotationSteps, bool preview)
        {
            GameObject root = new GameObject(preview ? "Vista previa: " + piece.DisplayName : piece.DisplayName);
            root.transform.SetPositionAndRotation(basePosition, BuildGrid.Rotation(rotationSteps));
            Material material = preview ? PreviewMaterial(true) : Solid(piece.Tint);
            Vector3 s = piece.Size;

            switch (piece.Shape)
            {
                case BuildShape.DoorFrame:
                    float post = 0.35f;
                    Part(root, new Vector3(-(s.x - post) * 0.5f, s.y * 0.5f, 0f), new Vector3(post, s.y, s.z), Quaternion.identity, material, preview);
                    Part(root, new Vector3((s.x - post) * 0.5f, s.y * 0.5f, 0f), new Vector3(post, s.y, s.z), Quaternion.identity, material, preview);
                    Part(root, new Vector3(0f, s.y - 0.2f, 0f), new Vector3(s.x - 2f * post, 0.4f, s.z), Quaternion.identity, material, preview);
                    break;
                case BuildShape.Ramp:
                    float length = Mathf.Sqrt(s.y * s.y + s.z * s.z);
                    float angle = Mathf.Atan2(s.y, s.z) * Mathf.Rad2Deg;
                    Part(root, new Vector3(0f, s.y * 0.5f, 0f), new Vector3(s.x, 0.2f, length), Quaternion.Euler(-angle, 0f, 0f), material, preview);
                    break;
                case BuildShape.Campfire:
                    Part(root, new Vector3(0f, 0.1f, 0f), new Vector3(s.x, 0.2f, s.z), Quaternion.identity, material, preview, PrimitiveType.Cylinder);
                    Material log = preview ? material : Solid(new Color(0.30f, 0.18f, 0.10f));
                    Part(root, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.15f, 0.15f), Quaternion.Euler(0f, 35f, 0f), log, preview);
                    Part(root, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.15f, 0.15f), Quaternion.Euler(0f, -35f, 0f), log, preview);
                    break;
                case BuildShape.Spikes:
                    Part(root, new Vector3(0f, 0.35f, 0f), new Vector3(s.x, 0.2f, 0.2f), Quaternion.identity, material, preview);
                    for (int i = 0; i < 5; i++)
                    {
                        float x = -s.x * 0.4f + i * (s.x * 0.2f);
                        Part(root, new Vector3(x, s.y * 0.5f, 0.1f), new Vector3(0.14f, s.y * 1.05f, 0.14f), Quaternion.Euler(-30f, 0f, 0f), material, preview);
                    }
                    break;
                default:
                    Part(root, new Vector3(0f, s.y * 0.5f, 0f), s, Quaternion.identity, material, preview);
                    break;
            }

            if (!preview)
            {
                root.AddComponent<BuiltPiece>().Configure(piece, rotationSteps);
                if (piece.Function == BuildFunction.Campfire)
                    root.AddComponent<CampfireComfort>();
                else if (piece.Function == BuildFunction.Bed)
                    root.AddComponent<BedRespawnPoint>();
            }
            return root;
        }

        public static void SetPreviewValid(GameObject preview, bool valid)
        {
            if (preview == null)
                return;
            Material material = PreviewMaterial(valid);
            foreach (Renderer renderer in preview.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
        }

        private static void Part(GameObject root, Vector3 localPosition, Vector3 size, Quaternion localRotation,
            Material material, bool preview, PrimitiveType type = PrimitiveType.Cube)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = "Parte";
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            // El cilindro primitivo mide 2 de alto: se compensa para que size sea real.
            part.transform.localScale = type == PrimitiveType.Cylinder
                ? new Vector3(size.x, size.y * 0.5f, size.z)
                : size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (preview)
            {
                // DestroyImmediate: la vista previa no debe bloquear ni un solo frame.
                Object.DestroyImmediate(part.GetComponent<Collider>());
            }
        }

        private static Material Solid(Color color)
        {
            if (solids.TryGetValue(color, out Material cached) && cached != null)
                return cached;
            Material material = new Material(Shader.Find("Standard")) { color = color, name = "Pieza" };
            material.SetFloat("_Glossiness", 0.08f);
            solids[color] = material;
            return material;
        }

        private static Material PreviewMaterial(bool valid)
        {
            Material cached = valid ? previewOk : previewBad;
            if (cached != null)
                return cached;
            Material material = new Material(Shader.Find("Standard"))
            {
                name = valid ? "Vista previa valida" : "Vista previa invalida",
                color = valid ? new Color(0.35f, 0.95f, 0.55f, 0.38f) : new Color(1f, 0.3f, 0.25f, 0.38f)
            };
            // Modo transparente del shader Standard.
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            if (valid)
                previewOk = material;
            else
                previewBad = material;
            return material;
        }
    }
}
