using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>Elemento visual de un efecto: decide forma, color y comportamiento de sus particulas.</summary>
    public enum VfxElement
    {
        Neutral = 0,   // chispas teñidas con el color que se pida
        Fire = 1,      // brasas que suben
        Frost = 2,     // esquirlas de hielo
        Storm = 3,     // chispas rapidas y arcos en zigzag
        Shadow = 4,    // humo oscuro
        Blood = 5,     // gotas que caen
        Light = 6      // rayos de luz
    }

    /// <summary>Colores de cada elemento. Nucleo, halo, particulas y marca en el suelo.</summary>
    public readonly struct VfxPalette
    {
        public readonly Color Core;
        public readonly Color Glow;
        public readonly Color Spark;
        public readonly Color Mark;
        /// <summary>Sombra y sangre: el nucleo tapa (alfa) en vez de brillar.</summary>
        public readonly bool DarkCore;
        /// <summary>La marca de luz brilla (aditiva); el resto oscurece o escarcha el suelo.</summary>
        public readonly bool GlowingMark;

        public VfxPalette(Color core, Color glow, Color spark, Color mark, bool darkCore, bool glowingMark)
        {
            Core = core;
            Glow = glow;
            Spark = spark;
            Mark = mark;
            DarkCore = darkCore;
            GlowingMark = glowingMark;
        }
    }

    /// <summary>
    /// Recursos compartidos de los efectos: formas generadas por codigo (sin arte externo),
    /// materiales unicos por forma y mezcla, mallas de quad y utilidades de colocacion.
    /// Todo se crea una sola vez y se reutiliza: ningun efecto crea materiales propios.
    /// </summary>
    public static class VfxLibrary
    {
        public const string ParticleShaderName = "Vhaldren/VfxParticula";
        public const string GroundShaderName = "Vhaldren/VfxSuelo";
        public const int ElementCount = 7;

        public static readonly int ColorId = Shader.PropertyToID("_Color");
        public static readonly int FillId = Shader.PropertyToID("_Fill");
        public static readonly int BaseId = Shader.PropertyToID("_Base");
        public static readonly int SoftId = Shader.PropertyToID("_Soft");
        public static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        public static readonly int RimAlphaId = Shader.PropertyToID("_RimAlpha");
        public static readonly int FillAlphaId = Shader.PropertyToID("_FillAlpha");
        public static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        public static readonly int EdgeAlphaId = Shader.PropertyToID("_EdgeAlpha");
        public static readonly int TicksId = Shader.PropertyToID("_Ticks");
        public static readonly int SpinId = Shader.PropertyToID("_Spin");
        public static readonly int NoiseId = Shader.PropertyToID("_Noise");
        public static readonly int SeedId = Shader.PropertyToID("_Seed");

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly RaycastHit[] GroundHits = new RaycastHit[12];
        private static readonly System.Collections.Generic.Dictionary<Material, Material> overlays =
            new System.Collections.Generic.Dictionary<Material, Material>();

        private static Texture2D glowTexture;
        private static Texture2D smokeTexture;
        private static Texture2D shardTexture;
        private static Texture2D beamTexture;
        private static Texture2D dropTexture;
        private static Material additiveGlow;
        private static Material additiveShard;
        private static Material additiveBeam;
        private static Material alphaSmoke;
        private static Material alphaDrop;
        private static Material alphaBeam;
        private static Material groundAdditive;
        private static Material groundAlpha;
        private static Mesh groundQuad;
        private static Mesh billboardQuad;
        private static bool shadersMissing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            glowTexture = smokeTexture = shardTexture = beamTexture = dropTexture = null;
            additiveGlow = additiveShard = additiveBeam = alphaSmoke = alphaDrop = alphaBeam = null;
            groundAdditive = groundAlpha = null;
            groundQuad = billboardQuad = null;
            overlays.Clear();
            shadersMissing = false;
        }

        /// <summary>
        /// Copia de un material de particulas que se dibuja por encima de todo (sin prueba
        /// de profundidad) y algo mas intensa: la energia en la mano se ve aunque la mano
        /// quede detras del cuerpo desde la camara. Una copia por material, compartida.
        /// </summary>
        public static Material Overlay(Material source)
        {
            if (source == null)
                return null;
            if (overlays.TryGetValue(source, out Material overlay) && overlay != null)
                return overlay;
            overlay = new Material(source) { name = source.name + " (encima)" };
            overlay.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.Always);
            overlay.SetFloat(IntensityId, 1.5f);
            overlay.renderQueue = 3100;
            overlays[source] = overlay;
            return overlay;
        }

        /// <summary>True si el ejecutable no incluye los shaders propios (se usa Sprites/Default).</summary>
        public static bool UsingFallback
        {
            get
            {
                Material probe = AdditiveGlow;
                return shadersMissing || probe == null;
            }
        }

        // ── Paleta ─────────────────────────────────────────────────────

        public static VfxPalette Palette(VfxElement element, Color tint)
        {
            switch (element)
            {
                case VfxElement.Fire:
                    return new VfxPalette(new Color(1f, 0.93f, 0.7f), new Color(1f, 0.42f, 0.08f),
                        new Color(1f, 0.82f, 0.45f), new Color(0.06f, 0.035f, 0.02f, 0.8f), false, false);
                case VfxElement.Frost:
                    return new VfxPalette(new Color(0.93f, 1f, 1f), new Color(0.38f, 0.82f, 1f),
                        new Color(0.78f, 0.96f, 1f), new Color(0.78f, 0.93f, 1f, 0.55f), false, false);
                case VfxElement.Storm:
                    return new VfxPalette(new Color(0.96f, 0.94f, 1f), new Color(0.55f, 0.48f, 1f),
                        new Color(0.85f, 0.8f, 1f), new Color(0.05f, 0.05f, 0.09f, 0.6f), false, false);
                case VfxElement.Shadow:
                    return new VfxPalette(new Color(0.07f, 0.02f, 0.11f, 0.95f), new Color(0.55f, 0.2f, 0.9f),
                        new Color(0.2f, 0.06f, 0.3f, 0.85f), new Color(0.09f, 0.02f, 0.13f, 0.72f), true, false);
                case VfxElement.Blood:
                    return new VfxPalette(new Color(0.32f, 0f, 0.03f, 0.95f), new Color(0.92f, 0.06f, 0.14f),
                        new Color(0.6f, 0.01f, 0.05f, 1f), new Color(0.28f, 0.01f, 0.03f, 0.75f), true, false);
                case VfxElement.Light:
                    return new VfxPalette(new Color(1f, 1f, 0.92f), new Color(1f, 0.86f, 0.45f),
                        new Color(1f, 0.95f, 0.72f), new Color(1f, 0.82f, 0.4f, 0.55f), false, true);
                default:
                    Color glow = tint;
                    glow.a = 1f;
                    return new VfxPalette(Color.Lerp(tint, Color.white, 0.75f), glow, Color.Lerp(tint, Color.white, 0.3f),
                        new Color(0f, 0f, 0f, 0f), false, true);
            }
        }

        // ── Materiales (uno por forma y mezcla, compartidos) ──────────

        /// <summary>Luz suave: brasas, chispas, rayos, destellos y halos.</summary>
        public static Material AdditiveGlow => additiveGlow != null ? additiveGlow :
            additiveGlow = ParticleMaterial("VFX brillo aditivo", GlowTexture, true);

        /// <summary>Esquirla en rombo para el hielo.</summary>
        public static Material AdditiveShard => additiveShard != null ? additiveShard :
            additiveShard = ParticleMaterial("VFX esquirla aditiva", ShardTexture, true);

        /// <summary>Franja luminosa para estelas y rayos (LineRenderer / TrailRenderer).</summary>
        public static Material AdditiveBeam => additiveBeam != null ? additiveBeam :
            additiveBeam = ParticleMaterial("VFX franja aditiva", BeamTexture, true);

        /// <summary>Humo que tapa: sombra, polvo, nieblas.</summary>
        public static Material AlphaSmoke => alphaSmoke != null ? alphaSmoke :
            alphaSmoke = ParticleMaterial("VFX humo alfa", SmokeTexture, false);

        /// <summary>Gota de borde nitido: sangre y nucleos oscuros.</summary>
        public static Material AlphaDrop => alphaDrop != null ? alphaDrop :
            alphaDrop = ParticleMaterial("VFX gota alfa", DropTexture, false);

        /// <summary>Franja que tapa: estelas de sombra y sangre.</summary>
        public static Material AlphaBeam => alphaBeam != null ? alphaBeam :
            alphaBeam = ParticleMaterial("VFX franja alfa", BeamTexture, false);

        /// <summary>Discos que brillan en el suelo: avisos y ondas expansivas.</summary>
        public static Material GroundAdditive => groundAdditive != null ? groundAdditive :
            groundAdditive = GroundMaterial("VFX disco aditivo", true);

        /// <summary>Manchas que oscurecen o tiñen el suelo: quemado, escarcha, sangre.</summary>
        public static Material GroundAlpha => groundAlpha != null ? groundAlpha :
            groundAlpha = GroundMaterial("VFX disco alfa", false);

        private static Material ParticleMaterial(string name, Texture2D texture, bool additive)
        {
            Shader shader = Shader.Find(ParticleShaderName);
            Material material;
            if (shader != null)
            {
                material = new Material(shader) { name = name };
                SetBlend(material, additive);
            }
            else
            {
                shadersMissing = true;
                Shader fallback = Shader.Find("Sprites/Default");
                if (fallback == null)
                    return null;
                material = new Material(fallback) { name = name + " (respaldo)" };
            }
            material.SetTexture(MainTexId, texture);
            material.renderQueue = 3000;
            return material;
        }

        private static Material GroundMaterial(string name, bool additive)
        {
            Shader shader = Shader.Find(GroundShaderName);
            if (shader == null)
            {
                shadersMissing = true;
                Material fallback = ParticleMaterial(name, GlowTexture, false);
                return fallback;
            }
            Material material = new Material(shader) { name = name };
            SetBlend(material, additive);
            material.renderQueue = 2980;
            return material;
        }

        private static void SetBlend(Material material, bool additive)
        {
            material.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(DstBlendId, additive
                ? (float)UnityEngine.Rendering.BlendMode.One
                : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        // ── Formas generadas por codigo ───────────────────────────────

        public static Texture2D GlowTexture => glowTexture != null ? glowTexture : glowTexture = BuildGlow(64);
        public static Texture2D SmokeTexture => smokeTexture != null ? smokeTexture : smokeTexture = BuildSmoke(64);
        public static Texture2D ShardTexture => shardTexture != null ? shardTexture : shardTexture = BuildShard(32, 64);
        public static Texture2D BeamTexture => beamTexture != null ? beamTexture : beamTexture = BuildBeam(8, 64);
        public static Texture2D DropTexture => dropTexture != null ? dropTexture : dropTexture = BuildDrop(32);

        private static Texture2D NewTexture(string name, int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave
            };
        }

        /// <summary>Punto de luz: centro caliente y caida suave.</summary>
        private static Texture2D BuildGlow(int size)
        {
            Texture2D texture = NewTexture("VFX forma brillo", size, size);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float falloff = Mathf.Clamp01(1f - r);
                    float a = falloff * falloff * (0.55f + 0.45f * falloff) + Mathf.Pow(falloff, 8f) * 0.6f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>Bocanada de humo: disco suave con grumos.</summary>
        private static Texture2D BuildSmoke(int size)
        {
            Texture2D texture = NewTexture("VFX forma humo", size, size);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float lumps = Mathf.PerlinNoise(x * 0.11f + 3.1f, y * 0.11f + 7.7f) * 0.65f +
                                  Mathf.PerlinNoise(x * 0.27f + 11.3f, y * 0.27f + 1.9f) * 0.35f;
                    float edge = Mathf.Clamp01(1f - r * (0.85f + 0.3f * lumps));
                    float a = Mathf.SmoothStep(0f, 1f, edge) * (0.55f + 0.45f * lumps);
                    byte shade = (byte)(Mathf.Lerp(0.78f, 1f, lumps) * 255f);
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(Mathf.Clamp01(a) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>Esquirla: rombo alargado con arista central brillante.</summary>
        private static Texture2D BuildShard(int width, int height)
        {
            Texture2D texture = NewTexture("VFX forma esquirla", width, height);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width * 2f - 1f;
                    float v = (y + 0.5f) / height * 2f - 1f;
                    float diamond = Mathf.Abs(u) + Mathf.Abs(v) * 0.55f;
                    float body = Mathf.Clamp01((1f - diamond) * 4f);
                    float ridge = Mathf.Clamp01(1f - Mathf.Abs(u) * 6f) * (1f - Mathf.Abs(v));
                    float a = Mathf.Clamp01(body * 0.75f + ridge * 0.5f);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>Franja: brillo maximo en el eje y caida suave hacia los lados (eje V).</summary>
        private static Texture2D BuildBeam(int width, int height)
        {
            Texture2D texture = NewTexture("VFX forma franja", width, height);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / height * 2f - 1f);
                float falloff = Mathf.Clamp01(1f - v);
                float a = falloff * falloff * 0.7f + Mathf.Pow(falloff, 10f) * 0.6f;
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>Gota: disco de borde nitido con un brillo pequeño.</summary>
        private static Texture2D BuildDrop(int size)
        {
            Texture2D texture = NewTexture("VFX forma gota", size, size);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((1f - r) * 5f);
                    float shine = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.3f, 0.3f)) * 3f);
                    byte shade = (byte)(Mathf.Lerp(0.82f, 1f, shine) * 255f);
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        // ── Mallas ─────────────────────────────────────────────────────

        /// <summary>Quad horizontal de -1 a 1 en X y Z (escala = radio).</summary>
        public static Mesh GroundQuad => groundQuad != null ? groundQuad : groundQuad = BuildQuad("VFX quad suelo", true);

        /// <summary>Quad vertical de -0,5 a 0,5 que se orienta a la camara.</summary>
        public static Mesh BillboardQuad => billboardQuad != null ? billboardQuad : billboardQuad = BuildQuad("VFX quad frontal", false);

        private static Mesh BuildQuad(string name, bool ground)
        {
            Mesh mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            float s = ground ? 1f : 0.5f;
            mesh.vertices = ground
                ? new[] { new Vector3(-s, 0f, -s), new Vector3(s, 0f, -s), new Vector3(s, 0f, s), new Vector3(-s, 0f, s) }
                : new[] { new Vector3(-s, -s, 0f), new Vector3(s, -s, 0f), new Vector3(s, s, 0f), new Vector3(-s, s, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = ground ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f * s, ground ? 0.1f : 2f * s, ground ? 2f * s : 0.1f));
            return mesh;
        }

        /// <summary>Crea un renderer de quad sin sombras ni sondas (efecto puro).</summary>
        public static MeshRenderer CreateQuad(string name, Transform parent, bool ground, Material material)
        {
            GameObject quad = new GameObject(name);
            quad.transform.SetParent(parent, false);
            quad.AddComponent<MeshFilter>().sharedMesh = ground ? GroundQuad : BillboardQuad;
            MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureRenderer(renderer);
            return renderer;
        }

        public static void ConfigureRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
        }

        /// <summary>Gira un quad frontal hacia la camara principal.</summary>
        public static void FaceCamera(Transform quad, Camera camera)
        {
            if (quad == null || camera == null)
                return;
            quad.rotation = camera.transform.rotation;
        }

        // ── Suelo ──────────────────────────────────────────────────────

        /// <summary>
        /// Busca el suelo bajo un punto ignorando personajes (jugador y enemigos), para que
        /// un aviso centrado sobre un enemigo no quede flotando a la altura de su cabeza.
        /// </summary>
        public static bool FindGround(Vector3 point, out Vector3 position, out Vector3 normal,
            float above = 1.5f, float below = 6f)
        {
            Vector3 start = point + Vector3.up * above;
            int count = Physics.RaycastNonAlloc(start, Vector3.down, GroundHits, above + below,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            position = point;
            normal = Vector3.up;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = GroundHits[i];
                if (hit.collider == null || hit.distance >= best || IsCharacter(hit.collider))
                    continue;
                best = hit.distance;
                position = hit.point;
                normal = hit.normal.y > 0.2f ? hit.normal : Vector3.up;
            }
            return best < float.PositiveInfinity;
        }

        private static bool IsCharacter(Collider collider)
        {
            if (collider is CharacterController)
                return true;
            return collider.attachedRigidbody != null ||
                   collider.GetComponentInParent<CombatDamageReceiver>() != null;
        }

        /// <summary>Orienta un quad de suelo segun la normal, elevandolo unos milimetros.</summary>
        public static void PlaceOnGround(Transform quad, Vector3 position, Vector3 normal, float lift = 0.04f)
        {
            if (quad == null)
                return;
            Vector3 up = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            quad.SetPositionAndRotation(position + up * lift, Quaternion.FromToRotation(Vector3.up, up));
        }

        /// <summary>Alfa en un color sin crear otro.</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
