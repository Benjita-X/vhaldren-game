using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vhaldren.EditorTools.Mundo
{
    /// <summary>
    /// Convierte el kit de Blender (Assets/Art/Mundo) en prefabs del juego: materiales del
    /// proyecto según el nombre del material de Blender, una sola malla por modelo (los
    /// árboles del terreno lo exigen), colisores según el manifiesto, capas de terreno,
    /// pasto y agua. Idempotente: reutiliza los assets ya creados (mismo GUID).
    /// </summary>
    public sealed class MundoKit
    {
        public const string Raiz = "Assets/Art/Mundo";
        public const string Modelos = Raiz + "/Modelos";
        public const string Texturas = Raiz + "/Texturas";
        public const string Materiales = Raiz + "/Materiales";
        public const string Prefabs = Raiz + "/Prefabs";
        public const string Mallas = Raiz + "/Mallas";
        public const string Terreno = Raiz + "/Terreno";
        private const string PolyHaven = "Assets/ThirdParty/PolyHaven";

        public readonly Dictionary<string, GameObject> Prefab = new Dictionary<string, GameObject>();
        public readonly Dictionary<string, ModeloKit> Info = new Dictionary<string, ModeloKit>();
        private readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        public Material Agua, AguaRio, Pasto;

        public static void Carpetas()
        {
            // Crear cada tramo de la ruta (Assets/Art puede no existir todavía).
            foreach (string f in new[] { "Assets/Art", Raiz, Materiales, Prefabs, Mallas, Terreno, "Assets/Resources", "Assets/Resources/Monturas",
                         "Assets/Resources/Lucifer", "Assets/Resources/Mapa" })
                if (!AssetDatabase.IsValidFolder(f))
                {
                    string parent = Path.GetDirectoryName(f).Replace('\\', '/');
                    if (Directory.Exists(f)) AssetDatabase.ImportAsset(f);
                    else AssetDatabase.CreateFolder(parent, Path.GetFileName(f));
                }
        }

        // ---------------- materiales ----------------
        private struct Receta
        {
            public string Tex; public Color Tinte; public float Tile; public Color Emision; public float Brillo; public float Metal;
            public Receta(string tex, Color tinte, float tile, float brillo = 0.1f) { Tex = tex; Tinte = tinte; Tile = tile; Emision = Color.black; Brillo = brillo; Metal = 0f; }
        }

        private static readonly Dictionary<string, Receta> recetas = new Dictionary<string, Receta>
        {
            { "Corteza", new Receta("bark_brown_02", new Color(0.78f, 0.66f, 0.58f), 1.6f) },
            { "Corteza_muerta", new Receta("bark_brown_02", new Color(0.62f, 0.6f, 0.58f), 1.6f) },
            { "Hojas_pino", new Receta("forest_leaves_02", new Color(0.32f, 0.5f, 0.38f), 2.2f) },
            { "Hojas_roble", new Receta("forest_leaves_02", new Color(0.55f, 0.72f, 0.4f), 2.4f) },
            { "Hojas_otono", new Receta("forest_leaves_02", new Color(1.1f, 0.62f, 0.28f), 2.4f) },
            { "Hojas_madre", new Receta("forest_leaves_02", new Color(0.36f, 0.7f, 0.55f), 3.5f) },
            { "Arbusto", new Receta("forest_leaves_02", new Color(0.45f, 0.62f, 0.36f), 1.6f) },
            { "Musgo", new Receta("forest_leaves_02", new Color(0.42f, 0.62f, 0.3f), 1.5f) },
            { "Junco", new Receta("forest_leaves_02", new Color(0.8f, 0.82f, 0.45f), 1f) },
            { "Roca", new Receta("aerial_rocks_02", new Color(0.8f, 0.8f, 0.82f), 4f) },
            { "Roca_oscura", new Receta("dark_rock", new Color(0.62f, 0.62f, 0.66f), 3f) },
            { "Roca_roja", new Receta("dark_rock", new Color(0.95f, 0.55f, 0.5f), 3f) },
            { "Piedra_tallada", new Receta("castle_brick_07", new Color(0.86f, 0.84f, 0.8f), 2.4f) },
            { "Marmol", new Receta("castle_wall_varriation", new Color(1.1f, 1.08f, 1.05f), 3f, 0.25f) },
            { "Madera", new Receta("dark_wooden_planks", new Color(1.0f, 0.85f, 0.7f), 1.8f) },
            { "Madera_oscura", new Receta("dark_wooden_planks", new Color(0.6f, 0.5f, 0.45f), 1.8f) },
            { "Tejado", new Receta("castle_wall_slates", new Color(0.7f, 0.78f, 0.85f), 2.2f) },
            { "Tejado_rojo", new Receta("castle_wall_slates", new Color(1.0f, 0.55f, 0.45f), 2.2f) },
            { "Nieve", new Receta("castle_wall_varriation", new Color(1.35f, 1.4f, 1.5f), 5f, 0.35f) },
        };

        private static readonly Dictionary<string, Color> planos = new Dictionary<string, Color>
        {
            { "Yeso", new Color(0.72f, 0.66f, 0.55f) }, { "Metal", new Color(0.16f, 0.16f, 0.18f) },
            { "Tela_roja", new Color(0.45f, 0.05f, 0.06f) }, { "Tela_ocre", new Color(0.55f, 0.35f, 0.13f) },
            { "Tela_azul", new Color(0.12f, 0.2f, 0.42f) }, { "Hueso", new Color(0.78f, 0.73f, 0.62f) },
            { "Oro", new Color(0.85f, 0.65f, 0.2f) },
        };

        private static readonly Dictionary<string, Color> emisivos = new Dictionary<string, Color>
        {
            { "Cristal", new Color(0.3f, 0.75f, 1.2f) }, { "Runa", new Color(1.6f, 0.1f, 0.06f) },
            { "Runa_azul", new Color(0.3f, 0.8f, 1.6f) }, { "Luz_calida", new Color(1.5f, 0.85f, 0.35f) },
            { "Hongo_brillo", new Color(0.25f, 1.3f, 0.95f) }, { "Fuego_infernal", new Color(2.0f, 0.55f, 0.08f) },
            { "Lava", new Color(2.2f, 0.6f, 0.05f) }, { "Hielo", new Color(0.35f, 0.65f, 0.9f) },
        };

        public Material Mat(string nombre, Color? colorBlender = null)
        {
            string clave = nombre;
            int punto = clave.IndexOf('.');
            if (punto > 0) clave = clave.Substring(0, punto); // "Corteza.001" -> "Corteza"
            if (mats.TryGetValue(clave, out Material m) && m != null) return m;
            string ruta = Materiales + "/" + clave + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard")) { name = clave };
                AssetDatabase.CreateAsset(m, ruta);
            }
            if (recetas.TryGetValue(clave, out Receta r) && Tex(r.Tex, out Texture2D col, out Texture2D nor))
            {
                m.shader = Shader.Find("Vhaldren/Triplanar");
                m.SetTexture("_MainTex", col);
                if (nor != null) m.SetTexture("_BumpMap", nor);
                m.SetColor("_Color", r.Tinte);
                m.SetFloat("_Tile", r.Tile);
                m.SetFloat("_Glossiness", r.Brillo);
            }
            else if (emisivos.TryGetValue(clave, out Color e))
            {
                m.shader = Shader.Find("Standard");
                m.SetColor("_Color", e * 0.45f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", e);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetFloat("_Glossiness", 0.5f);
            }
            else
            {
                Color c = planos.TryGetValue(clave, out Color pc) ? pc : (colorBlender ?? new Color(0.5f, 0.5f, 0.5f));
                m.shader = Shader.Find("Standard");
                m.SetColor("_Color", c);
                m.SetFloat("_Glossiness", clave == "Metal" ? 0.45f : 0.12f);
                m.SetFloat("_Metallic", clave == "Metal" || clave == "Oro" ? 0.7f : 0f);
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            mats[clave] = m;
            return m;
        }

        public static bool Tex(string id, out Texture2D color, out Texture2D normal)
        {
            // primero las texturas propias del kit, después Poly Haven (CC0)
            color = AssetDatabase.LoadAssetAtPath<Texture2D>(Texturas + "/" + id + "_color.png");
            string normalPath = Texturas + "/" + id + "_normal.png";
            if (color == null)
            {
                color = AssetDatabase.LoadAssetAtPath<Texture2D>(PolyHaven + "/" + id + "/color.jpg");
                normalPath = PolyHaven + "/" + id + "/normal.jpg";
            }
            TextureImporter ni = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (ni != null && ni.textureType != TextureImporterType.NormalMap)
            {
                ni.textureType = TextureImporterType.NormalMap;
                ni.SaveAndReimport();
            }
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            return color != null;
        }

        // ---------------- prefabs ----------------
        public void CargarManifiesto()
        {
            string ruta = Raiz + "/manifest.json";
            if (!File.Exists(ruta))
            {
                Debug.LogWarning("[Vhaldren] Falta el manifiesto del kit: se usarán sustitutos.");
                return;
            }
            string json = File.ReadAllText(ruta).Trim();
            if (json.StartsWith("[")) json = "{\"modelos\":" + json + "}";
            ManifiestoKit mk = JsonUtility.FromJson<ManifiestoKit>(json);
            foreach (ModeloKit m in mk.modelos)
                if (!string.IsNullOrEmpty(m.nombre)) Info[m.nombre] = m;
        }

        /// <summary>Crea o actualiza los prefabs de todos los modelos del manifiesto (y de los FBX sueltos).</summary>
        public void CrearPrefabs()
        {
            Carpetas();
            CargarManifiesto();
            HashSet<string> nombres = new HashSet<string>(Info.Keys);
            if (AssetDatabase.IsValidFolder(Modelos))
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Modelos }))
                    nombres.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
            foreach (string n in nombres)
            {
                try { CrearPrefab(n); }
                catch (Exception ex) { Debug.LogWarning("[Vhaldren] Modelo " + n + " omitido: " + ex.Message); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Vhaldren] Kit del mundo: " + Prefab.Count + " prefabs listos.");
        }

        private string RutaFbx(string nombre)
        {
            if (Info.TryGetValue(nombre, out ModeloKit m) && !string.IsNullOrEmpty(m.archivo))
            {
                string a = m.archivo.Replace('\\', '/');
                string p = a.StartsWith("Assets/") ? a : Raiz + "/" + a;
                if (File.Exists(p)) return p;
            }
            string q = Modelos + "/" + nombre + ".fbx";
            return File.Exists(q) ? q : null;
        }

        private void CrearPrefab(string nombre)
        {
            string fbx = RutaFbx(nombre);
            if (fbx == null) return;
            ModelImporter imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (imp != null)
            {
                bool cambio = imp.importAnimation || imp.importCameras || imp.importLights || imp.addCollider || imp.isReadable;
                imp.importAnimation = false; imp.importCameras = false; imp.importLights = false;
                imp.addCollider = false; imp.isReadable = false; imp.globalScale = 1f;
                if (cambio) imp.SaveAndReimport();
            }
            Info.TryGetValue(nombre, out ModeloKit info);
            string cat = info != null ? info.categoria : Categoria(nombre);
            bool jerarquia = cat == "montura" || cat == "alas" || nombre.StartsWith("montura_") || nombre.StartsWith("alas_");
            if (jerarquia && imp != null && !imp.bakeAxisConversion)
            {
                // ejes limpios en cada parte: X derecha, Y arriba, Z adelante (la animación procedural lo supone)
                imp.bakeAxisConversion = true;
                imp.SaveAndReimport();
            }
            GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (modelo == null) return;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = nombre;
            inst.transform.position = Vector3.zero;
            foreach (Renderer rr in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] arr = rr.sharedMaterials;
                for (int i = 0; i < arr.Length; i++)
                {
                    Color? c = arr[i] != null && arr[i].HasProperty("_Color") ? arr[i].color : (Color?)null;
                    arr[i] = Mat(arr[i] != null ? arr[i].name : "Roca", c);
                }
                rr.sharedMaterials = arr;
                rr.shadowCastingMode = cat == "vegetacion_suelo" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }

            GameObject final = inst;
            if (jerarquia)
            {
                // raíz limpia (identidad) para que el código pueda girarla; el FBX conserva su corrección de ejes
                final = new GameObject(nombre);
                inst.name = "modelo";
                inst.transform.SetParent(final.transform, false);
                foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            }
            else
            {
                final = Aplanar(inst, nombre);
                UnityEngine.Object.DestroyImmediate(inst);
                AñadirColisor(final, info, cat);
                final.isStatic = true;
            }

            string destino = jerarquia
                ? (nombre.StartsWith("alas_") ? "Assets/Resources/Lucifer/" + nombre + ".prefab" : "Assets/Resources/Monturas/" + nombre + ".prefab")
                : Prefabs + "/" + nombre + ".prefab";
            GameObject guardado = PrefabUtility.SaveAsPrefabAsset(final, destino);
            UnityEngine.Object.DestroyImmediate(final);
            if (guardado != null)
                Prefab[nombre] = guardado;
        }

        /// <summary>Una sola malla en la raíz, con una submalla por material (exigido por los árboles del terreno).</summary>
        private GameObject Aplanar(GameObject src, string nombre)
        {
            Dictionary<Material, List<CombineInstance>> porMat = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 raizInv = src.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in src.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null) continue;
                Material[] ms = mr.sharedMaterials;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                {
                    Material m = ms[Math.Min(s, ms.Length - 1)];
                    if (!porMat.TryGetValue(m, out List<CombineInstance> lista)) porMat[m] = lista = new List<CombineInstance>();
                    lista.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = raizInv * mf.transform.localToWorldMatrix });
                }
            }
            List<Material> orden = new List<Material>(porMat.Keys);
            CombineInstance[] partes = new CombineInstance[orden.Count];
            for (int i = 0; i < orden.Count; i++)
            {
                Mesh sub = new Mesh { indexFormat = IndexFormat.UInt32 };
                sub.CombineMeshes(porMat[orden[i]].ToArray(), true, true);
                partes[i] = new CombineInstance { mesh = sub, transform = Matrix4x4.identity };
            }
            string rutaMalla = Mallas + "/" + nombre + ".asset";
            Mesh malla = AssetDatabase.LoadAssetAtPath<Mesh>(rutaMalla);
            bool nueva = malla == null;
            if (nueva) malla = new Mesh();
            malla.Clear();
            malla.name = nombre;
            malla.indexFormat = IndexFormat.UInt32;
            malla.CombineMeshes(partes, false, false);
            malla.RecalculateBounds();
            if (nueva) AssetDatabase.CreateAsset(malla, rutaMalla);
            else EditorUtility.SetDirty(malla);
            foreach (CombineInstance c in partes) UnityEngine.Object.DestroyImmediate(c.mesh);

            GameObject go = new GameObject(nombre);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = orden.ToArray();
            return go;
        }

        private static void AñadirColisor(GameObject go, ModeloKit info, string cat)
        {
            Bounds b = go.GetComponent<MeshFilter>().sharedMesh.bounds;
            string tipo = info != null && !string.IsNullOrEmpty(info.colisor) ? info.colisor : ColisorPorCategoria(cat);
            switch (tipo)
            {
                case "capsula":
                    CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
                    cc.radius = info != null && info.radio > 0.05f ? info.radio : Mathf.Max(0.2f, Mathf.Min(b.extents.x, b.extents.z) * 0.18f);
                    cc.height = Mathf.Max(cc.radius * 2f, b.size.y * 0.7f);
                    cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
                    break;
                case "caja":
                    BoxCollider bc = go.AddComponent<BoxCollider>();
                    if (info != null && info.tamano != null && info.tamano.Count == 3 && info.tamano[0] > 0f)
                    {
                        bc.size = new Vector3(info.tamano[0], info.tamano[1], info.tamano[2]);
                        bc.center = new Vector3(0f, info.tamano[1] * 0.5f, 0f);
                    }
                    else { bc.size = b.size; bc.center = b.center; }
                    break;
                case "malla":
                    go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                    break;
            }
        }

        private static string ColisorPorCategoria(string cat)
        {
            switch (cat)
            {
                case "arbol": return "capsula";
                case "roca": case "acantilado": case "ruina": case "poblado": case "hito": return "malla";
                default: return "ninguno";
            }
        }

        public static string Categoria(string nombre)
        {
            if (nombre.StartsWith("pino") || nombre.StartsWith("roble") || nombre.StartsWith("arbol")) return "arbol";
            if (nombre.StartsWith("arbusto")) return "arbusto";
            if (nombre.StartsWith("roca")) return "roca";
            if (nombre.StartsWith("acantilado") || nombre.StartsWith("pilar_roca")) return "acantilado";
            if (nombre.StartsWith("montura")) return "montura";
            if (nombre.StartsWith("alas")) return "alas";
            return "decoracion";
        }

        public GameObject Get(string nombre) => nombre != null && Prefab.TryGetValue(nombre, out GameObject g) ? g : null;

        /// <summary>Primer prefab disponible de la lista.</summary>
        public GameObject Alguno(params string[] nombres)
        {
            foreach (string n in nombres)
            {
                GameObject g = Get(n);
                if (g != null) return g;
            }
            return null;
        }

        /// <summary>Prefabs cuyo nombre empieza por el prefijo (por ejemplo "roca_").</summary>
        public List<GameObject> Familia(string prefijo)
        {
            List<GameObject> l = new List<GameObject>();
            foreach (KeyValuePair<string, GameObject> kv in Prefab)
                if (kv.Key.StartsWith(prefijo)) l.Add(kv.Value);
            l.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return l;
        }

        // ---------------- terreno: capas, pasto y agua ----------------
        public TerrainLayer Capa(string id, float tile, Color tinte)
        {
            string ruta = Terreno + "/" + id + ".terrainlayer";
            TerrainLayer l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(ruta);
            if (l == null)
            {
                l = new TerrainLayer();
                AssetDatabase.CreateAsset(l, ruta);
            }
            if (!Tex(id, out Texture2D c, out Texture2D n))
                return null;
            l.diffuseTexture = c;
            l.normalMapTexture = n;
            l.tileSize = new Vector2(tile, tile);
            l.diffuseRemapMax = new Vector4(tinte.r, tinte.g, tinte.b, 1f);
            l.smoothness = 0.05f;
            l.normalScale = 1f;
            EditorUtility.SetDirty(l);
            return l;
        }

        /// <summary>Textura de mata de pasto con transparencia, dibujada por código.</summary>
        public Texture2D TexturaPasto(string nombre, Color baseCol, Color punta, int seed)
        {
            string ruta = Terreno + "/" + nombre + ".png";
            int W = 128, H = 128;
            Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[W * H];
            System.Random rng = new System.Random(seed);
            for (int b = 0; b < 26; b++)
            {
                float x0 = 10 + (float)rng.NextDouble() * (W - 20);
                float lean = ((float)rng.NextDouble() - 0.5f) * 40f;
                float h = H * (0.45f + (float)rng.NextDouble() * 0.5f);
                float w0 = 2.5f + (float)rng.NextDouble() * 2.5f;
                for (int y = 0; y < (int)h; y++)
                {
                    float v = y / h;
                    float cx = x0 + lean * v * v;
                    float half = w0 * (1f - v);
                    Color c = Color.Lerp(baseCol, punta, v);
                    c *= 0.85f + 0.3f * (float)rng.NextDouble();
                    for (int x = (int)(cx - half - 1); x <= (int)(cx + half + 1); x++)
                    {
                        if (x < 0 || x >= W) continue;
                        float a = Mathf.Clamp01(half + 0.5f - Mathf.Abs(x - cx));
                        if (a <= 0f) continue;
                        int i = y * W + x;
                        Color32 prev = px[i];
                        float na = Mathf.Max(prev.a / 255f, a);
                        px[i] = new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), (byte)(na * 255));
                    }
                }
            }
            t.SetPixels32(px);
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(ruta);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }

        /// <summary>Mapa normal de olas repetible, dibujado por código.</summary>
        public Texture2D TexturaOlas()
        {
            string ruta = Terreno + "/agua_normal.png";
            int N = 256;
            Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            Color[] px = new Color[N * N];
            float H(float x, float y)
            {
                float v = 0f;
                for (int k = 1; k <= 5; k++)
                {
                    float a = k * 1.3f;
                    v += Mathf.Sin((x * Mathf.Cos(a) + y * Mathf.Sin(a)) * Mathf.PI * 2f * (k + 1)) / (k + 1);
                }
                return v;
            }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N, e = 1f / N;
                    float dx = H(u + e, v) - H(u - e, v);
                    float dy = H(u, v + e) - H(u, v - e);
                    Vector3 n = new Vector3(-dx * 2f, -dy * 2f, 1f).normalized;
                    px[y * N + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            t.SetPixels(px);
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(ruta);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
            ti.textureType = TextureImporterType.NormalMap;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }

        public void MaterialesAgua()
        {
            Texture2D olas = TexturaOlas();
            Agua = MatAgua("Agua_lago", olas, new Color(0.10f, 0.26f, 0.30f, 0.6f), new Color(0.02f, 0.07f, 0.11f, 0.9f), 16f, Vector2.zero);
            AguaRio = MatAgua("Agua_rio", olas, new Color(0.14f, 0.30f, 0.30f, 0.62f), new Color(0.04f, 0.12f, 0.14f, 0.85f), 9f, new Vector2(0f, 0.06f));
        }

        private Material MatAgua(string nombre, Texture2D olas, Color somero, Color hondo, float tile, Vector2 flujo)
        {
            string ruta = Materiales + "/" + nombre + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            Shader s = Shader.Find("Vhaldren/Agua");
            if (m == null)
            {
                m = new Material(s);
                AssetDatabase.CreateAsset(m, ruta);
            }
            m.shader = s;
            m.SetTexture("_BumpMap", olas);
            m.SetColor("_ColorSomero", somero);
            m.SetColor("_ColorProfundo", hondo);
            m.SetFloat("_Tile", tile);
            m.SetVector("_Flujo", new Vector4(flujo.x, flujo.y, 0f, 0f));
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
