using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vhaldren.EditorTools.Mundo
{
    public static partial class MundoBuilder
    {
        public const string RutaMapa = "Assets/Resources/Mapa/mapa_mundo.png";

        private static Color ColorBioma(string b)
        {
            switch (b)
            {
                case "pradera": return new Color(0.42f, 0.50f, 0.28f);
                case "bosque": return new Color(0.20f, 0.32f, 0.18f);
                case "pantano": return new Color(0.27f, 0.31f, 0.21f);
                case "ruinas": return new Color(0.46f, 0.45f, 0.38f);
                case "ceniza": return new Color(0.24f, 0.22f, 0.21f);
                case "nieve": return new Color(0.86f, 0.88f, 0.92f);
                case "meseta": return new Color(0.60f, 0.42f, 0.28f);
                case "costa": return new Color(0.62f, 0.58f, 0.44f);
                case "carmesi": return new Color(0.46f, 0.16f, 0.13f);
                case "cumbres": return new Color(0.50f, 0.50f, 0.52f);
                default: return new Color(0.4f, 0.45f, 0.3f);
            }
        }

        /// <summary>Atlas del mundo para la exploración: regiones, agua y límites.</summary>
        private static void Atlas(GameObject mundo)
        {
            Component atlas = MundoGancho.Añadir(mundo, Exploracion + "WorldAtlas");
            if (atlas == null) return;
            MundoGancho.Llamar(atlas, "SetBounds", new Vector2(-T.Mitad, -T.Mitad), new Vector2(T.Mitad, T.Mitad));
            MundoGancho.Llamar(atlas, "SetSeaLevel", T.Agua);
            foreach (RegionDiseno r in D.regiones)
            {
                Color niebla = r.colorNiebla != null ? r.colorNiebla.C : new Color(0.3f, 0.32f, 0.36f);
                Color luz = Color.Lerp(Color.white, niebla, 0.35f);
                MundoGancho.Llamar(atlas, "AddRegion", r.id, r.nombre, r.centro.V, r.radio, r.nivel, niebla,
                    r.densidadNiebla > 0f ? r.densidadNiebla : 0.004f, luz);
            }
            for (int i = 0; i < D.lagos.Count; i++)
                MundoGancho.Llamar(atlas, "AddLake", D.lagos[i].centro.V, D.lagos[i].radio, T.NivelLago[i], Mathf.Max(2f, D.lagos[i].profundidad));
            foreach (MundoTerreno.Linea r in T.Rios)
                MundoGancho.Llamar(atlas, "AddRiver", r.P, Mathf.Max(4f, r.Anchura));
            MundoGancho.Llamar(atlas, "SetMapTexture", "Mapa/mapa_mundo");
        }

        /// <summary>Mapa ilustrado del mundo (relieve sombreado, biomas, agua y caminos).</summary>
        private static void MapaTextura()
        {
            int N = 1024;
            Color[] px = new Color[N * N];
            RegionDiseno[] regs = T.Regiones;
            Color[] colReg = new Color[regs.Length];
            for (int i = 0; i < regs.Length; i++) colReg[i] = ColorBioma(regs[i].bioma);
            Color pergamino = new Color(0.78f, 0.70f, 0.54f);
            Vector3 sol = new Vector3(-0.6f, 0.75f, 0.55f).normalized;
            Parallel.For(0, N, yi =>
            {
                float[] w = new float[regs.Length];
                float z = -T.Mitad + T.Tam * (yi + 0.5f) / N;
                for (int xi = 0; xi < N; xi++)
                {
                    float x = -T.Mitad + T.Tam * (xi + 0.5f) / N;
                    float h = AlturaRejilla(x, z);
                    T.Pesos(x, z, w);
                    Color c = Color.black;
                    for (int k = 0; k < regs.Length; k++) c += colReg[k] * w[k];
                    float e = 3f;
                    float dx = (AlturaRejilla(x + e, z) - AlturaRejilla(x - e, z)) / (2f * e);
                    float dz = (AlturaRejilla(x, z + e) - AlturaRejilla(x, z - e)) / (2f * e);
                    Vector3 n = new Vector3(-dx, 1f, -dz).normalized;
                    float luz = Mathf.Clamp01(Vector3.Dot(n, sol)) * 0.75f + 0.35f;
                    if (h > T.HMax * 0.52f) c = Color.Lerp(c, new Color(0.9f, 0.92f, 0.95f), MundoGeo.Smooth((h - T.HMax * 0.52f) / 20f));
                    float pend = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
                    if (pend > 30f) c = Color.Lerp(c, new Color(0.38f, 0.37f, 0.36f), MundoGeo.Smooth((pend - 30f) / 12f));
                    c *= luz;
                    float agua = T.NivelAguaEn(x, z, h);
                    if (agua > float.MinValue)
                    {
                        float prof = Mathf.Clamp01((agua - h) / 10f);
                        c = Color.Lerp(new Color(0.22f, 0.38f, 0.42f), new Color(0.08f, 0.17f, 0.24f), prof);
                    }
                    else if (T.DistanciaCamino(x, z) < 1.5f)
                        c = Color.Lerp(c, new Color(0.62f, 0.50f, 0.34f), 0.8f);
                    // curvas de nivel suaves
                    float curva = Mathf.Abs(Mathf.Repeat(h, 20f) - 10f);
                    if (curva > 9.4f && agua == float.MinValue) c *= 0.88f;
                    c = Color.Lerp(c, pergamino, 0.18f);
                    c.a = 1f;
                    px[yi * N + xi] = c;
                }
            });
            Texture2D t = new Texture2D(N, N, TextureFormat.RGB24, false);
            t.SetPixels(px);
            File.WriteAllBytes(RutaMapa, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(RutaMapa);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(RutaMapa);
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.SaveAndReimport();
        }

        private static void Ambiente(GameObject camara)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.24f, 0.26f, 0.31f);
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.46f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.30f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.13f, 0.12f);
            Camera cam = camara.GetComponent<Camera>();
            if (cam != null)
            {
                cam.farClipPlane = 800f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = RenderSettings.fogColor;
            }
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional)
                {
                    l.shadows = LightShadows.Soft;
                    l.shadowStrength = 0.8f;
                    l.intensity = Mathf.Max(l.intensity, 1.1f);
                }
            QualitySettings.shadowDistance = 90f;
        }
    }
}
