using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Vhaldren.EditorTools.Mundo
{
    public static partial class MundoBuilder
    {
        private struct CapaDef
        {
            public string Id, Alt; public float Tile; public Color Tinte, AltTinte; public Color Mapa;
            public CapaDef(string id, float tile, string alt, Color altTinte, Color mapa)
            { Id = id; Tile = tile; Alt = alt; Tinte = Color.white; AltTinte = altTinte; Mapa = mapa; }
        }

        // Capas del terreno: textura propia del kit o, si falta, una de Poly Haven teñida.
        private static readonly CapaDef[] capasDef =
        {
            new CapaDef("pasto", 9f, "forest_leaves_02", new Color(0.55f, 0.72f, 0.42f), new Color(0.30f, 0.38f, 0.20f)),
            new CapaDef("pasto_seco", 9f, "brown_mud_rocks_01", new Color(1.05f, 0.95f, 0.68f), new Color(0.48f, 0.42f, 0.26f)),
            new CapaDef("musgo_bosque", 7f, "forest_leaves_02", new Color(0.62f, 0.74f, 0.55f), new Color(0.20f, 0.28f, 0.17f)),
            new CapaDef("tierra_camino", 6f, "brown_mud_rocks_01", new Color(0.92f, 0.84f, 0.72f), new Color(0.44f, 0.36f, 0.27f)),
            new CapaDef("roca_acantilado", 11f, "aerial_rocks_02", new Color(0.78f, 0.78f, 0.8f), new Color(0.40f, 0.40f, 0.41f)),
            new CapaDef("nieve", 12f, "castle_wall_varriation", new Color(1.45f, 1.5f, 1.6f), new Color(0.86f, 0.88f, 0.92f)),
            new CapaDef("ceniza_volcanica", 8f, "dark_rock", new Color(0.5f, 0.48f, 0.48f), new Color(0.20f, 0.19f, 0.19f)),
            new CapaDef("barro_pantano", 7f, "brown_mud_rocks_01", new Color(0.55f, 0.56f, 0.42f), new Color(0.24f, 0.26f, 0.18f)),
            new CapaDef("arena", 8f, "brown_mud_rocks_01", new Color(1.25f, 1.15f, 0.92f), new Color(0.66f, 0.60f, 0.45f)),
            new CapaDef("tierra_roja", 8f, "dark_rock", new Color(1.0f, 0.55f, 0.46f), new Color(0.45f, 0.20f, 0.16f)),
            new CapaDef("grava", 6f, "cobblestone_floor_08", new Color(0.9f, 0.88f, 0.84f), new Color(0.42f, 0.40f, 0.37f)),
            new CapaDef("hojarasca", 5f, "forest_leaves_02", new Color(0.9f, 0.78f, 0.62f), new Color(0.32f, 0.25f, 0.16f)),
        };

        private static readonly Dictionary<string, string> sinonimos = new Dictionary<string, string>
        {
            { "musgo", "musgo_bosque" }, { "roca", "roca_acantilado" }, { "barro", "barro_pantano" }, { "ceniza", "ceniza_volcanica" },
            { "camino", "tierra_camino" }, { "tierra", "tierra_camino" }, { "piedra", "grava" }, { "hojas", "hojarasca" },
            { "forest_leaves_02", "hojarasca" }, { "nieve_polvo", "nieve" }, { "pasto_verde", "pasto" },
        };

        private static List<CapaDef> capas;          // capas efectivas (con textura)
        private static Dictionary<string, int> idx;  // id -> índice de capa
        private static float[,] alturas;             // normalizadas, ResAltura

        private static int Capa(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            if (sinonimos.TryGetValue(id, out string s)) id = s;
            return idx.TryGetValue(id, out int i) ? i : -1;
        }

        private static void CrearTerreno(Transform padre)
        {
            string ruta = MundoKit.Terreno + "/TerrenoMundo.asset";
            AssetDatabase.DeleteAsset(ruta);
            TerrainData td = new TerrainData { heightmapResolution = ResAltura };
            td.alphamapResolution = ResSplat;
            td.baseMapResolution = 1024;
            td.SetDetailResolution(ResDetalle, 32);
            td.size = new Vector3(T.Tam, T.HMax, T.Tam);
            AssetDatabase.CreateAsset(td, ruta);

            alturas = T.Rejilla(ResAltura);
            td.SetHeights(0, 0, alturas);

            // capas
            capas = new List<CapaDef>();
            idx = new Dictionary<string, int>();
            List<TerrainLayer> layers = new List<TerrainLayer>();
            foreach (CapaDef c in capasDef)
            {
                TerrainLayer l = null;
                if (MundoKit.Tex(c.Id, out _, out _))
                    l = K.Capa(c.Id, c.Tile, c.Tinte);
                if (l == null && MundoKit.Tex(c.Alt, out _, out _))
                    l = CapaAlternativa(c);
                if (l == null) continue;
                idx[c.Id] = capas.Count;
                capas.Add(c);
                layers.Add(l);
            }
            td.terrainLayers = layers.ToArray();
            td.SetAlphamaps(0, 0, Splat());

            GameObject go = Terrain.CreateTerrainGameObject(td);
            go.name = "Suelo fisico";
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(-T.Mitad, 0f, -T.Mitad);
            go.isStatic = true;
            terreno = go.GetComponent<Terrain>();
            terreno.heightmapPixelError = 6f;
            terreno.basemapDistance = 220f;
            terreno.drawInstanced = true;
            terreno.treeDistance = 210f;
            terreno.treeBillboardDistance = 210f;
            terreno.treeCrossFadeLength = 10f;
            terreno.treeMaximumFullLODCount = 400;
            terreno.detailObjectDistance = 70f;
            terreno.detailObjectDensity = 0.75f;
            terreno.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            // Los árboles con CapsuleCollider en su prefab ya generan colisión en el TerrainCollider.

            Arboles(td);
            Detalles(td);
            terreno.Flush();
            EditorUtility.SetDirty(td);
        }

        private static TerrainLayer CapaAlternativa(CapaDef c)
        {
            // TerrainLayer con nombre de la capa lógica pero textura de Poly Haven
            string ruta = MundoKit.Terreno + "/" + c.Id + ".terrainlayer";
            TerrainLayer l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(ruta);
            if (l == null)
            {
                l = new TerrainLayer();
                AssetDatabase.CreateAsset(l, ruta);
            }
            MundoKit.Tex(c.Alt, out Texture2D col, out Texture2D nor);
            l.diffuseTexture = col;
            l.normalMapTexture = nor;
            l.tileSize = new Vector2(c.Tile, c.Tile);
            l.diffuseRemapMax = new Vector4(c.AltTinte.r, c.AltTinte.g, c.AltTinte.b, 1f);
            l.smoothness = 0.05f;
            EditorUtility.SetDirty(l);
            return l;
        }

        // ---------------------------------------------------------------- muestreo
        /// <summary>Altura (m) bilineal de la rejilla calculada.</summary>
        private static float AlturaRejilla(float x, float z)
        {
            int n = alturas.GetLength(0);
            float fx = (x + T.Mitad) / T.Tam * (n - 1), fz = (z + T.Mitad) / T.Tam * (n - 1);
            int x0 = Mathf.Clamp((int)fx, 0, n - 2), z0 = Mathf.Clamp((int)fz, 0, n - 2);
            float tx = Mathf.Clamp01(fx - x0), tz = Mathf.Clamp01(fz - z0);
            float a = alturas[z0, x0] + (alturas[z0, x0 + 1] - alturas[z0, x0]) * tx;
            float b = alturas[z0 + 1, x0] + (alturas[z0 + 1, x0 + 1] - alturas[z0 + 1, x0]) * tx;
            return (a + (b - a) * tz) * T.HMax;
        }

        /// <summary>Pendiente en grados.</summary>
        private static float Pendiente(float x, float z, out Vector2 bajada)
        {
            const float e = 2f;
            float dx = (AlturaRejilla(x + e, z) - AlturaRejilla(x - e, z)) / (2f * e);
            float dz = (AlturaRejilla(x, z + e) - AlturaRejilla(x, z - e)) / (2f * e);
            bajada = new Vector2(-dx, -dz);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
        }

        private static float Pendiente(float x, float z) => Pendiente(x, z, out _);

        // ---------------------------------------------------------------- texturas del suelo
        private static float[,,] Splat()
        {
            int ar = ResSplat, L = capas.Count;
            float[,,] alpha = new float[ar, ar, L];
            RegionDiseno[] regs = T.Regiones;
            int iPasto = Capa("pasto"), iRoca = Capa("roca_acantilado"), iNieve = Capa("nieve"), iCamino = Capa("tierra_camino"),
                iArena = Capa("arena"), iBarro = Capa("barro_pantano"), iGrava = Capa("grava"), iHoja = Capa("hojarasca");
            int[][] texReg = new int[regs.Length][];
            for (int r = 0; r < regs.Length; r++)
            {
                List<int> l = new List<int>();
                foreach (string t in regs[r].texturas) { int c = Capa(t); if (c >= 0) l.Add(c); }
                if (l.Count == 0 && iPasto >= 0) l.Add(iPasto);
                texReg[r] = l.ToArray();
            }
            float nieveLinea = T.HMax * 0.52f;

            Parallel.For(0, ar, zi =>
            {
                float[] w = new float[regs.Length];
                float[] acc = new float[L];
                float z = -T.Mitad + T.Tam * (zi + 0.5f) / ar;
                for (int xi = 0; xi < ar; xi++)
                {
                    float x = -T.Mitad + T.Tam * (xi + 0.5f) / ar;
                    Array.Clear(acc, 0, L);
                    T.Pesos(x, z, w);
                    float n1 = T.Ruido(x, z, 0.018f), n2 = T.Ruido(x + 300f, z - 200f, 0.04f), n3 = T.Ruido(x - 90f, z + 400f, 0.009f);
                    for (int r = 0; r < regs.Length; r++)
                    {
                        if (w[r] < 0.01f) continue;
                        int[] tr = texReg[r];
                        acc[tr[0]] += w[r];
                        if (tr.Length > 1) acc[tr[1]] += w[r] * MundoGeo.Smooth((n1 + 0.05f) * 3f) * 1.1f;
                        if (tr.Length > 2) acc[tr[2]] += w[r] * MundoGeo.Smooth((n2 - 0.1f) * 3f) * 0.9f;
                        if (tr.Length > 3) acc[tr[3]] += w[r] * MundoGeo.Smooth((n3 - 0.2f) * 4f) * 0.8f;
                    }
                    float h = AlturaRejilla(x, z);
                    float pend = Pendiente(x, z);
                    // nieve en las cumbres
                    if (iNieve >= 0)
                    {
                        float s = MundoGeo.Smooth((h - nieveLinea - n1 * 14f) / 18f);
                        if (s > 0f) { for (int l = 0; l < L; l++) acc[l] *= 1f - s; acc[iNieve] += s * 2f; }
                    }
                    // orillas: arena o barro
                    float agua = T.NivelAguaEn(x, z, h - 1.6f);
                    if (agua > float.MinValue)
                    {
                        float orilla = MundoGeo.Smooth(1f - (h - agua) / 1.8f);
                        int c = iArena >= 0 ? iArena : iBarro;
                        if (iBarro >= 0 && n2 > 0.15f) c = iBarro;
                        if (c >= 0 && orilla > 0f) { for (int l = 0; l < L; l++) acc[l] *= 1f - orilla; acc[c] += orilla * 2f; }
                    }
                    // caminos
                    if (iCamino >= 0)
                    {
                        float d = T.DistanciaCamino(x, z);
                        float cam = MundoGeo.Smooth((2.2f - d + n2 * 1.5f) / 2.5f);
                        if (cam > 0f) { for (int l = 0; l < L; l++) acc[l] *= 1f - cam; acc[iCamino] += cam * 2f; }
                    }
                    // explanadas de pueblos y ruinas: grava pisada
                    if (iGrava >= 0)
                    {
                        float d = T.DistanciaExplanada(x, z);
                        float g = MundoGeo.Smooth((-d - 4f + n1 * 6f) / 6f) * 0.8f;
                        if (g > 0f) { for (int l = 0; l < L; l++) acc[l] *= 1f - g; acc[iGrava] += g * 1.6f; }
                    }
                    // roca en las pendientes (encima de todo)
                    if (iRoca >= 0)
                    {
                        float rr = MundoGeo.Smooth((pend - 27f + n2 * 6f) / 11f);
                        if (rr > 0f) { for (int l = 0; l < L; l++) acc[l] *= 1f - rr; acc[iRoca] += rr * 3f; }
                    }
                    float sum = 0f;
                    for (int l = 0; l < L; l++) sum += acc[l];
                    if (sum <= 1e-5f) { acc[0] = 1f; sum = 1f; }
                    for (int l = 0; l < L; l++) alpha[zi, xi, l] = acc[l] / sum;
                }
            });
            return alpha;
        }

        // ---------------------------------------------------------------- árboles y vegetación
        private static void Arboles(TerrainData td)
        {
            List<TreePrototype> protos = new List<TreePrototype>();
            Dictionary<string, int> pi = new Dictionary<string, int>();
            int Proto(string nombre)
            {
                if (pi.TryGetValue(nombre, out int i)) return i;
                GameObject p = K.Get(nombre) ?? K.Get(Alias(nombre));
                if (p == null)
                {
                    // "arbol_muerto" -> arbol_muerto_a / _b ; "pino_nevado" -> pino_nevado_a ...
                    List<GameObject> fam = K.Familia(nombre + "_");
                    if (fam.Count > 0) p = fam[Math.Abs(nombre.GetHashCode()) % fam.Count];
                }
                if (p == null || p.GetComponent<MeshRenderer>() == null) { pi[nombre] = -1; return -1; }
                pi[nombre] = protos.Count;
                protos.Add(new TreePrototype { prefab = p, bendFactor = 0f });
                return protos.Count - 1;
            }

            RegionDiseno[] regs = T.Regiones;
            List<TreeInstance> lista = new List<TreeInstance>();
            List<GameObject> arbustos = K.Familia("arbusto");
            System.Random r = new System.Random(77);
            float paso = 5.5f;
            int n = (int)(T.Tam / paso);
            float[] w = new float[regs.Length];
            float nieveLinea = T.HMax * 0.55f;
            List<string> pinos = new List<string>();
            foreach (string s in new[] { "pino_a", "pino_b", "pino_c", "pino_nevado" }) if (K.Get(s) != null) pinos.Add(s);

            for (int zi = 0; zi < n; zi++)
                for (int xi = 0; xi < n; xi++)
                {
                    float x = -T.Mitad + (xi + (float)r.NextDouble()) * paso;
                    float z = -T.Mitad + (zi + (float)r.NextDouble()) * paso;
                    if (Mathf.Abs(x) > T.Mitad - 20f || Mathf.Abs(z) > T.Mitad - 20f) continue;
                    double roll = r.NextDouble();
                    double rollTipo = r.NextDouble();
                    float h = AlturaRejilla(x, z);
                    if (h < T.Agua + 0.6f) continue;
                    float pend = Pendiente(x, z);
                    if (pend > 33f) continue;
                    if (T.NivelAguaEn(x, z, h - 0.9f) > float.MinValue) continue;
                    float dCamino = T.DistanciaCamino(x, z);
                    if (dCamino < 3.5f) continue;
                    float dLugar = T.DistanciaExplanada(x, z);
                    if (dLugar < 4f) continue;
                    T.Pesos(x, z, w);
                    int dom = 0;
                    float dens = 0f;
                    for (int k = 0; k < regs.Length; k++)
                    {
                        // densidadArboles = árboles por hectárea -> probabilidad por celda
                        dens += w[k] * Mathf.Clamp01(regs[k].densidadArboles * paso * paso / 10000f);
                        if (w[k] > w[dom]) dom = k;
                    }
                    // manchas de bosque y claros
                    float mancha = MundoGeo.Smooth((T.Ruido(x, z, 0.007f) + 0.12f) * 2.6f);
                    float p = dens * (0.35f + 1.1f * mancha);
                    if (dLugar < 14f) p *= 0.3f;
                    if (dCamino < 8f) p *= 0.5f;
                    string especie = null;
                    if (roll < p)
                    {
                        List<string> sp = regs[dom].arboles;
                        if (h > nieveLinea && pinos.Count > 0)
                            especie = pinos[(int)(rollTipo * pinos.Count) % pinos.Count];
                        else if (sp != null && sp.Count > 0)
                            especie = sp[(int)(rollTipo * sp.Count) % sp.Count];
                    }
                    else if (roll < p + 0.03f + dens * 0.3f && arbustos.Count > 0)
                        especie = arbustos[(int)(rollTipo * arbustos.Count) % arbustos.Count].name;
                    else if (roll > 1f - Decoracion(regs[dom].bioma).Length * 0.006f)
                    {
                        string[] deco = Decoracion(regs[dom].bioma);
                        especie = deco[(int)(rollTipo * deco.Length) % deco.Length];
                    }
                    if (especie == null) continue;
                    int proto = Proto(especie);
                    if (proto < 0) continue;
                    float esc = 0.78f + (float)r.NextDouble() * 0.55f;
                    lista.Add(new TreeInstance
                    {
                        prototypeIndex = proto,
                        position = new Vector3((x + T.Mitad) / T.Tam, (h - 0.08f) / T.HMax, (z + T.Mitad) / T.Tam),
                        widthScale = esc,
                        heightScale = esc * (0.9f + (float)r.NextDouble() * 0.2f),
                        rotation = (float)(r.NextDouble() * Math.PI * 2.0),
                        color = Color.Lerp(Color.white, new Color(0.85f, 0.9f, 0.8f), (float)r.NextDouble()),
                        lightmapColor = Color.white
                    });
                }
            // juncos junto al agua
            int pj = Proto("juncos");
            if (pj >= 0)
                for (int i = 0; i < 6000; i++)
                {
                    float x = Rand(-T.Mitad + 40f, T.Mitad - 40f), z = Rand(-T.Mitad + 40f, T.Mitad - 40f);
                    float h = AlturaRejilla(x, z);
                    float agua = T.NivelAguaEn(x, z, h - 0.8f);
                    if (agua == float.MinValue || h < agua - 0.3f) continue;
                    lista.Add(new TreeInstance { prototypeIndex = pj, position = new Vector3((x + T.Mitad) / T.Tam, h / T.HMax, (z + T.Mitad) / T.Tam), widthScale = 1f, heightScale = 1f, rotation = Rand(0f, 6.28f), color = Color.white, lightmapColor = Color.white });
                }
            td.treePrototypes = protos.ToArray();
            td.SetTreeInstances(lista.ToArray(), false);
            Debug.Log("[Vhaldren] Mapa: " + lista.Count + " árboles y plantas (" + protos.Count + " tipos).");
        }

        /// <summary>Plantas y piedras pequeñas propias de cada bioma (sin colisión).</summary>
        private static string[] Decoracion(string bioma)
        {
            switch (bioma)
            {
                case "bosque": return new[] { "helecho", "hongos_brillo", "tocon", "tronco_caido", "roca_musgo", "helecho" };
                case "pradera": return new[] { "flores_a", "hierba_alta", "roca_a", "flores_a", "arbusto_a" };
                case "pantano": return new[] { "juncos", "juncos", "tocon", "roca_musgo", "helecho" };
                case "meseta": return new[] { "cactus_a", "cactus_b", "arbusto_seco", "roca_meseta", "roca_plana" };
                case "ceniza": return new[] { "fumarola", "roca_volcanica_a", "roca_volcanica_b", "grieta_lava", "huesos" };
                case "nieve": return new[] { "roca_nieve_a", "roca_nieve_b", "hielo_cristales" };
                case "cumbres": return new[] { "roca_nieve_a", "roca_nieve_b", "hielo_cristales", "roca_c" };
                case "carmesi": return new[] { "huesos", "roca_roja", "arbusto_seco", "huesos" };
                case "costa": return new[] { "rocas_grupo", "arbusto_seco", "hierba_alta", "roca_plana" };
                case "ruinas": return new[] { "roca_b", "arbusto_a", "hierba_alta", "huesos" };
                default: return new[] { "roca_a", "arbusto_a" };
            }
        }

        private static readonly Dictionary<string, string> aliasArboles = new Dictionary<string, string>
        {
            { "abedul", "roble_otono" }, { "roble_b", "roble_otono" }, { "sauce", "sauce_pantano" }, { "cipres", "pino_c" },
            { "arbol_carmesi", "arbol_muerto_b" }, { "arbol_muerto", "arbol_muerto_a" }, { "pino_nevado", "pino_nevado_a" },
            { "pino_nieve", "pino_nevado_b" }, { "manglar_a", "manglar" }, { "calcinado", "arbol_calcinado" },
        };

        private static string Alias(string n) => aliasArboles.TryGetValue(n, out string a) ? a : n;

        private static void Detalles(TerrainData td)
        {
            Texture2D verde = K.TexturaPasto("pasto_mata_verde", new Color(0.2f, 0.3f, 0.12f), new Color(0.45f, 0.55f, 0.25f), 3);
            Texture2D seco = K.TexturaPasto("pasto_mata_seca", new Color(0.36f, 0.32f, 0.16f), new Color(0.7f, 0.62f, 0.36f), 9);
            td.detailPrototypes = new[]
            {
                new DetailPrototype { prototypeTexture = verde, renderMode = DetailRenderMode.GrassBillboard, minWidth = 0.7f, maxWidth = 1.3f,
                    minHeight = 0.45f, maxHeight = 0.95f, healthyColor = new Color(0.85f, 0.95f, 0.8f), dryColor = new Color(0.8f, 0.78f, 0.55f), noiseSpread = 0.3f },
                new DetailPrototype { prototypeTexture = seco, renderMode = DetailRenderMode.GrassBillboard, minWidth = 0.7f, maxWidth = 1.2f,
                    minHeight = 0.4f, maxHeight = 0.85f, healthyColor = Color.white, dryColor = new Color(0.85f, 0.75f, 0.6f), noiseSpread = 0.3f },
            };
            td.wavingGrassStrength = 0.25f;
            td.wavingGrassAmount = 0.3f;
            td.wavingGrassSpeed = 0.4f;
            td.wavingGrassTint = new Color(0.8f, 0.85f, 0.7f);

            int res = ResDetalle;
            int[,] capaVerde = new int[res, res], capaSeca = new int[res, res];
            RegionDiseno[] regs = T.Regiones;
            Parallel.For(0, res, zi =>
            {
                float[] w = new float[regs.Length];
                float z = -T.Mitad + T.Tam * (zi + 0.5f) / res;
                System.Random rr = new System.Random(zi * 7919);
                for (int xi = 0; xi < res; xi++)
                {
                    float x = -T.Mitad + T.Tam * (xi + 0.5f) / res;
                    float h = AlturaRejilla(x, z);
                    if (h < T.Agua + 0.4f || h > T.HMax * 0.5f) continue;
                    if (Pendiente(x, z) > 26f) continue;
                    if (T.NivelAguaEn(x, z, h - 0.5f) > float.MinValue) continue;
                    if (T.DistanciaCamino(x, z) < 1.6f) continue;
                    T.Pesos(x, z, w);
                    float g = 0f, s = 0f;
                    for (int k = 0; k < regs.Length; k++)
                    {
                        switch (regs[k].bioma)
                        {
                            case "pradera": g += w[k] * 1f; break;
                            case "bosque": g += w[k] * 0.55f; break;
                            case "pantano": g += w[k] * 0.45f; break;
                            case "costa": s += w[k] * 0.5f; break;
                            case "meseta": s += w[k] * 0.6f; break;
                            case "ruinas": g += w[k] * 0.4f; s += w[k] * 0.25f; break;
                            case "carmesi": s += w[k] * 0.2f; break;
                        }
                    }
                    float m = MundoGeo.Smooth((T.Ruido(x, z, 0.03f) + 0.25f) * 2.2f);
                    capaVerde[zi, xi] = (int)(g * m * 7f + rr.NextDouble());
                    capaSeca[zi, xi] = (int)(s * m * 6f + rr.NextDouble());
                }
            });
            td.SetDetailLayer(0, 0, 0, capaVerde);
            td.SetDetailLayer(0, 0, 1, capaSeca);
        }

        // ---------------------------------------------------------------- acantilados y peñascos
        private static GameObject RiscoBioma(string b, System.Random r)
        {
            string[] op;
            switch (b)
            {
                case "nieve": case "cumbres": op = new[] { "acantilado_nieve", "acantilado_a", "pilar_roca" }; break;
                case "ceniza": op = new[] { "acantilado_volcanico", "roca_volcanica_b", "pilar_roca" }; break;
                case "carmesi": op = new[] { "acantilado_rojo", "roca_roja", "pilar_roca" }; break;
                case "meseta": op = new[] { "roca_meseta", "acantilado_rojo", "acantilado_esquina" }; break;
                default: op = new[] { "acantilado_a", "acantilado_b", "acantilado_esquina", "pilar_roca" }; break;
            }
            return K.Get(op[r.Next(op.Length)]);
        }

        private static GameObject PenascoBioma(string b, System.Random r)
        {
            string[] op;
            switch (b)
            {
                case "nieve": case "cumbres": op = new[] { "roca_nieve_a", "roca_nieve_b", "roca_d" }; break;
                case "ceniza": op = new[] { "roca_volcanica_a", "roca_volcanica_b" }; break;
                case "carmesi": op = new[] { "roca_roja", "roca_oscura" }; break;
                case "bosque": case "pantano": op = new[] { "roca_musgo", "roca_c", "rocas_grupo" }; break;
                default: op = new[] { "roca_c", "roca_d", "rocas_grupo", "arco_roca" }; break;
            }
            return K.Get(op[r.Next(op.Length)]);
        }

        private static void Acantilados()
        {
            Transform padre = Hijo(props, "Acantilados y peñascos");
            List<GameObject> riscos = new List<GameObject>();
            foreach (string s in new[] { "acantilado_a", "acantilado_b", "pilar_roca", "roca_d", "roca_c" })
                if (K.Get(s) != null) riscos.Add(K.Get(s));
            if (riscos.Count == 0) return;
            System.Random r = new System.Random(99);
            int puestos = 0;
            float paso = 16f;
            int n = (int)(T.Tam / paso);
            for (int zi = 0; zi < n; zi++)
                for (int xi = 0; xi < n; xi++)
                {
                    float x = -T.Mitad + (xi + (float)r.NextDouble()) * paso;
                    float z = -T.Mitad + (zi + (float)r.NextDouble()) * paso;
                    double roll = r.NextDouble();
                    if (Mathf.Abs(x) > T.Mitad - 60f || Mathf.Abs(z) > T.Mitad - 60f) continue;
                    float pend = Pendiente(x, z, out Vector2 bajada);
                    float h = AlturaRejilla(x, z);
                    if (h < T.Agua + 1f) continue;
                    if (T.DistanciaCamino(x, z) < 6f || T.DistanciaExplanada(x, z) < 5f) continue;
                    bool risco = pend > 30f && roll < 0.42;
                    bool penasco = !risco && pend > 12f && roll < 0.025;
                    if (!risco && !penasco) continue;
                    string bioma = T.Regiones[T.RegionDominante(x, z)].bioma;
                    GameObject prefab = risco ? RiscoBioma(bioma, r) : PenascoBioma(bioma, r);
                    if (prefab == null)
                        prefab = risco ? riscos[r.Next(Math.Min(3, riscos.Count))] : riscos[Math.Min(riscos.Count - 1, 3 + r.Next(2))];
                    GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
                    float yaw = Mathf.Atan2(bajada.x, bajada.y) * Mathf.Rad2Deg + (float)(r.NextDouble() * 40 - 20);
                    float esc = risco ? 0.9f + (float)r.NextDouble() * 0.9f : 0.7f + (float)r.NextDouble() * 0.6f;
                    g.transform.position = new Vector3(x, h - (risco ? 2.2f : 0.6f) * esc, z);
                    g.transform.rotation = Quaternion.Euler((float)(r.NextDouble() * 8 - 4), yaw, (float)(r.NextDouble() * 8 - 4));
                    g.transform.localScale = Vector3.one * esc;
                    if (++puestos > 1400) return;
                }
            Debug.Log("[Vhaldren] Mapa: " + puestos + " acantilados y peñascos.");
        }
    }
}
