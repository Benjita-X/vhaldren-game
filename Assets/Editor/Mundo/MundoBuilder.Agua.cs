using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vhaldren.EditorTools.Mundo
{
    public static partial class MundoBuilder
    {
        private const string CarpetaMallasAgua = MundoKit.Raiz + "/Mallas";

        private static void CrearAgua()
        {
            // Mar / nivel general del agua: una rejilla que cubre todo el mapa.
            int n = 72;
            float lado = T.Tam + 600f;
            List<Vector3> v = new List<Vector3>();
            List<Color> c = new List<Color>();
            List<int> tri = new List<int>();
            bool hayMar = false;
            for (int zi = 0; zi <= n; zi++)
                for (int xi = 0; xi <= n; xi++)
                {
                    float x = -lado / 2 + lado * xi / n, z = -lado / 2 + lado * zi / n;
                    float suelo = Mathf.Abs(x) < T.Mitad && Mathf.Abs(z) < T.Mitad ? AlturaRejilla(x, z) : T.Agua - 20f;
                    float prof = T.Agua - suelo;
                    if (prof > 0f) hayMar = true;
                    v.Add(new Vector3(x, T.Agua, z));
                    c.Add(new Color(1, 1, 1, Mathf.Clamp01(prof / 8f)));
                }
            for (int zi = 0; zi < n; zi++)
                for (int xi = 0; xi < n; xi++)
                {
                    int a = zi * (n + 1) + xi;
                    tri.AddRange(new[] { a, a + n + 1, a + 1, a + 1, a + n + 1, a + n + 2 });
                }
            if (hayMar)
                ObjetoAgua("Mar de Vhaldren", v, c, tri, K.Agua);

            // Lagos sobre el nivel del mar
            for (int i = 0; i < D.lagos.Count; i++)
            {
                LagoDiseno l = D.lagos[i];
                float nivel = T.NivelLago[i];
                if (nivel <= T.Agua + 0.05f) continue;
                v = new List<Vector3>(); c = new List<Color>(); tri = new List<int>();
                int anillos = 10, seg = 48;
                float rmax = l.radio + 22f;
                v.Add(new Vector3(l.centro.x, nivel, l.centro.z));
                c.Add(new Color(1, 1, 1, Mathf.Clamp01((nivel - AlturaRejilla(l.centro.x, l.centro.z)) / 8f)));
                for (int a = 1; a <= anillos; a++)
                    for (int s = 0; s < seg; s++)
                    {
                        float ang = s / (float)seg * Mathf.PI * 2f, rr = rmax * a / anillos;
                        float x = l.centro.x + Mathf.Cos(ang) * rr, z = l.centro.z + Mathf.Sin(ang) * rr;
                        v.Add(new Vector3(x, nivel, z));
                        c.Add(new Color(1, 1, 1, Mathf.Clamp01((nivel - AlturaRejilla(x, z)) / 8f)));
                    }
                for (int s = 0; s < seg; s++)
                    tri.AddRange(new[] { 0, 1 + (s + 1) % seg, 1 + s });
                for (int a = 1; a < anillos; a++)
                    for (int s = 0; s < seg; s++)
                    {
                        int i0 = 1 + (a - 1) * seg + s, i1 = 1 + (a - 1) * seg + (s + 1) % seg;
                        int j0 = 1 + a * seg + s, j1 = 1 + a * seg + (s + 1) % seg;
                        tri.AddRange(new[] { i0, i1, j0, i1, j1, j0 });
                    }
                ObjetoAgua("Lago " + (string.IsNullOrEmpty(l.nombre) ? i.ToString() : l.nombre), v, c, tri, K.Agua);
            }

            // Ríos: franja que sigue la superficie calculada
            int ri = 0;
            foreach (MundoTerreno.Linea r in T.Rios)
            {
                v = new List<Vector3>(); c = new List<Color>(); tri = new List<int>();
                float half = Mathf.Max(4f, r.Anchura) * 0.5f + 2.5f;
                for (int i = 0; i < r.P.Length; i++)
                {
                    Vector2 dir = (i < r.P.Length - 1 ? r.P[i + 1] - r.P[i] : r.P[i] - r.P[i - 1]).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float y = r.Superficie[i] + 0.05f;
                    for (int k = 0; k < 5; k++)
                    {
                        float t = k / 4f * 2f - 1f;
                        Vector2 p = r.P[i] + perp * half * t;
                        v.Add(new Vector3(p.x, y, p.y));
                        c.Add(new Color(1, 1, 1, (1f - Mathf.Abs(t)) * 0.8f));
                    }
                }
                for (int i = 0; i < r.P.Length - 1; i++)
                    for (int k = 0; k < 4; k++)
                    {
                        int a = i * 5 + k, b = a + 5;
                        tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    }
                ObjetoAgua("Río " + (ri++), v, c, tri, K.AguaRio);
            }
        }

        /// <summary>Un puente (o vado) donde cada camino cruza un río.</summary>
        private static void Puentes()
        {
            Transform padre = Hijo(props, "Puentes");
            int n = 0;
            foreach (MundoTerreno.Linea c in T.Caminos)
                foreach (MundoTerreno.Linea r in T.Rios)
                {
                    float ultimo = -1000f;
                    for (int i = 0; i < c.P.Length - 1; i++)
                        for (int j = 0; j < r.P.Length - 1; j++)
                        {
                            if (!Cruce(c.P[i], c.P[i + 1], r.P[j], r.P[j + 1], out float tc, out float tr)) continue;
                            Vector2 q = Vector2.Lerp(c.P[i], c.P[i + 1], tc);
                            float recorrido = i + tc;
                            if (recorrido - ultimo < 3f) continue; // un solo puente por cruce
                            ultimo = recorrido;
                            Vector2 d = (c.P[i + 1] - c.P[i]).normalized;
                            float sup = r.Superficie[j] + (r.Superficie[j + 1] - r.Superficie[j]) * tr;
                            float yaw = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
                            bool piedra = c.Anchura >= 6f;
                            GameObject g = PonerEn(padre, q.x, q.y, yaw, 1f, piedra ? "puente_piedra" : "puente_madera", "puente_madera", "puente_piedra");
                            if (g == null) continue;
                            float largo = Mathf.Max(4f, r.Anchura) + 8f;
                            float escala = Mathf.Max(1f, largo / (piedra ? 16f : 14f));
                            g.transform.localScale = new Vector3(escala, 1f, 1f);
                            g.transform.position = new Vector3(q.x, sup + 0.75f, q.y);
                            g.name = "Puente";
                            n++;
                        }
                }
            Debug.Log("[Vhaldren] Mapa: " + n + " puentes.");
        }

        private static bool Cruce(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float t, out float u)
        {
            Vector2 r = b - a, s = d - c;
            float den = r.x * s.y - r.y * s.x;
            t = u = 0f;
            if (Mathf.Abs(den) < 1e-6f) return false;
            Vector2 ac = c - a;
            t = (ac.x * s.y - ac.y * s.x) / den;
            u = (ac.x * r.y - ac.y * r.x) / den;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        private static void ObjetoAgua(string nombre, List<Vector3> v, List<Color> c, List<int> tri, Material m)
        {
            Mesh mesh = new Mesh { name = nombre, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string ruta = CarpetaMallasAgua + "/agua_" + Limpio(nombre) + ".asset";
            AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(mesh, ruta);
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(aguaRaiz, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.layer = 4; // Water
        }

        private static string Limpio(string s)
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            foreach (char ch in s)
                b.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
            return b.ToString();
        }
    }
}
