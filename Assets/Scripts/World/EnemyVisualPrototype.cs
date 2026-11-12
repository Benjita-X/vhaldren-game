using UnityEngine;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Modelos provisionales 3D con materiales de textura procedural.
    /// Bosque = guerrero encapuchado; ruinas = centinela blindado;
    /// jefe = guardian de altar con cuernos y baston.
    /// No crea colliders ni sustituye la raiz de movimiento o vida.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyVisualPrototype : MonoBehaviour
    {
        private static Texture2D clothPattern;
        private static Material bone;
        private static Material forestCloth;
        private static Material ruinsCloth;
        private static Material iron;
        private static Material glow;
        private static Material bossCloth;
        private static Material wood;
        private Transform anchor;
        private bool built;

        private void Awake() => BuildIfNeeded();

        public void BuildIfNeeded()
        {
            if (built)
                return;
            anchor = transform.Find("Visual enemigo");
            if (anchor == null)
                return;
            if (anchor.Find("Modelo texturizado") != null)
            {
                built = true;
                return;
            }

            EnsureMaterials();
            if (bone == null || forestCloth == null)
                return;

            MeshRenderer placeholder = anchor.GetComponent<MeshRenderer>();
            if (placeholder != null)
                placeholder.enabled = false;

            bool boss = name.Contains("JEFE") || name.Contains("Jefe");
            bool forest = name.Contains("bosque") || name.Contains("Bosque");
            Material outfit = boss ? bossCloth : forest ? forestCloth : ruinsCloth;

            Transform root = new GameObject("Modelo texturizado").transform;
            root.SetParent(anchor, false);
            root.localPosition = Vector3.zero;

            Piece("Torso con textura", PrimitiveType.Cube, root,
                new Vector3(0f, 0.02f, 0f), new Vector3(0.80f, 0.89f, 0.48f), outfit);
            Piece("Pecho metalico", PrimitiveType.Cube, root,
                new Vector3(0f, 0.14f, 0.28f), new Vector3(0.62f, 0.50f, 0.08f), iron);
            Piece("Cuello", PrimitiveType.Cylinder, root,
                new Vector3(0f, 0.49f, 0f), new Vector3(0.18f, 0.11f, 0.18f), bone);
            Piece("Cabeza", PrimitiveType.Sphere, root,
                new Vector3(0f, 0.72f, 0f), new Vector3(0.57f, 0.55f, 0.53f), bone);
            Piece("Capucha", PrimitiveType.Sphere, root,
                new Vector3(0f, 0.88f, -0.11f), new Vector3(0.72f, 0.52f, 0.68f), outfit);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.53f;
                Piece("Hombro", PrimitiveType.Sphere, root,
                    new Vector3(x, 0.31f, 0f), new Vector3(0.32f, 0.28f, 0.32f), iron);
                Piece("Brazo", PrimitiveType.Capsule, root,
                    new Vector3(x * 1.14f, -0.10f, 0f),
                    new Vector3(0.20f, 0.68f, 0.22f), bone);
                Piece("Guante", PrimitiveType.Cube, root,
                    new Vector3(x * 1.14f, -0.50f, 0.06f),
                    new Vector3(0.20f, 0.25f, 0.22f), iron);
                Piece("Pierna", PrimitiveType.Capsule, root,
                    new Vector3(side * 0.23f, -0.68f, 0f),
                    new Vector3(0.28f, 0.58f, 0.28f), outfit);
                Piece("Bota", PrimitiveType.Cube, root,
                    new Vector3(side * 0.23f, -0.91f, 0.13f),
                    new Vector3(0.30f, 0.18f, 0.48f), iron);
                Piece("Ojo luminoso", PrimitiveType.Cube, root,
                    new Vector3(side * 0.12f, 0.73f, 0.267f),
                    new Vector3(0.11f, 0.07f, 0.045f), glow);
            }

            if (boss)
            {
                Piece("Cuerno izquierdo", PrimitiveType.Cylinder, root,
                    new Vector3(-0.35f, 1.18f, -0.08f),
                    new Vector3(0.11f, 0.38f, 0.11f), bone);
                Piece("Cuerno derecho", PrimitiveType.Cylinder, root,
                    new Vector3(0.35f, 1.18f, -0.08f),
                    new Vector3(0.11f, 0.38f, 0.11f), bone);
                Piece("Runas del pecho", PrimitiveType.Cube, root,
                    new Vector3(0f, 0.18f, 0.327f),
                    new Vector3(0.28f, 0.28f, 0.03f), glow);
                Piece("Baston de jefe", PrimitiveType.Cylinder, root,
                    new Vector3(0.81f, -0.04f, 0.17f),
                    new Vector3(0.075f, 1.18f, 0.075f), wood);
                Piece("Cristal del baston", PrimitiveType.Sphere, root,
                    new Vector3(0.81f, 1.10f, 0.17f),
                    new Vector3(0.30f, 0.34f, 0.30f), glow);
            }
            else
            {
                Piece("Empunadura", PrimitiveType.Cylinder, root,
                    new Vector3(0.83f, -0.43f, 0.18f),
                    new Vector3(0.06f, 0.18f, 0.06f), wood);
                Piece("Hoja de acero", PrimitiveType.Cube, root,
                    new Vector3(0.83f, 0.05f, 0.18f),
                    new Vector3(0.14f, 0.92f, 0.055f), iron);
                Piece("Guarda de espada", PrimitiveType.Cube, root,
                    new Vector3(0.83f, -0.27f, 0.18f),
                    new Vector3(0.38f, 0.09f, 0.14f), iron);
            }

            built = true;
        }

        private static void Piece(string title, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(type);
            piece.name = title;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            Collider collider = piece.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }
            Renderer renderer = piece.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static void EnsureMaterials()
        {
            if (bone != null && forestCloth != null && ruinsCloth != null &&
                iron != null && glow != null && bossCloth != null && wood != null)
                return;

            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            if (clothPattern == null)
            {
                clothPattern = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                clothPattern.name = "Trama textil procedural para enemigos";
                clothPattern.wrapMode = TextureWrapMode.Repeat;
                clothPattern.filterMode = FilterMode.Point;
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        int n = ((x * 37 + y * 61 + x * y * 13) ^ (x << 4)) & 31;
                        float shade = (0.68f + n * 0.010f);
                        if ((x + y) % 7 == 0)
                            shade *= 0.82f;
                        clothPattern.SetPixel(x, y, new Color(shade, shade, shade));
                    }
                clothPattern.Apply(false, true);
            }

            bone = Make(shader, new Color(0.79f, 0.76f, 0.64f), false);
            forestCloth = Make(shader, new Color(0.20f, 0.31f, 0.25f), true);
            ruinsCloth = Make(shader, new Color(0.31f, 0.32f, 0.40f), true);
            iron = Make(shader, new Color(0.42f, 0.48f, 0.52f), true);
            glow = Make(shader, new Color(0.87f, 0.18f, 0.32f), false);
            bossCloth = Make(shader, new Color(0.33f, 0.14f, 0.36f), true);
            wood = Make(shader, new Color(0.27f, 0.16f, 0.11f), true);
        }

        private static Material Make(Shader shader, Color tint, bool textured)
        {
            Material material = new Material(shader);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", tint);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", tint);
            if (textured && clothPattern != null)
            {
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", clothPattern);
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", clothPattern);
            }
            if (tint.r > 0.7f && tint.g < 0.3f &&
                material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", tint * 0.35f);
            }
            return material;
        }
    }
}
