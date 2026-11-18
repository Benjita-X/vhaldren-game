using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.EditorTools.Mundo
{
    public static partial class MundoBuilder
    {
        private const string Exploracion = "Vhaldren.Gameplay.Exploration.";
        private const string Monturas_ = "Vhaldren.Gameplay.Mounts.";
        private static bool tablonPuesto;

        // ================================================================ lugares
        private static void Lugares()
        {
            tablonPuesto = false;
            // el poblado más cercano al inicio recibe el tablón de misiones
            LugarDiseno cruce = null;
            float mejor = float.MaxValue;
            foreach (LugarDiseno l in D.lugares)
                if (l.tipo == "poblado")
                {
                    float d = Vector2.Distance(l.posicion.V, D.inicio.V);
                    if (d < mejor) { mejor = d; cruce = l; }
                }

            foreach (LugarDiseno l in D.lugares)
            {
                Vector3 c = Suelo(l.posicion.x, l.posicion.z);
                GameObject root = new GameObject(l.nombre);
                root.transform.SetParent(lugares, false);
                root.transform.position = c;
                System.Random r = Semilla(l.id);
                ocupados.Add(l.posicion.V);
                switch (l.tipo)
                {
                    case "poblado": Poblado(root.transform, c, r, l == cruce); break;
                    case "puerto": Puerto(root.transform, c, r); break;
                    case "campamento": Campamento(root.transform, c, r); break;
                    case "ruina": Ruina(root.transform, c, r, l); break;
                    case "altar": Altar(root.transform, c, r); break;
                    case "torre": Torre(root.transform, c, r); break;
                    case "santuario": Santuario(root.transform, c, r); break;
                    case "mirador": Mirador(root.transform, c, r, l); break;
                    case "cueva": Cueva(root.transform, c, r, l); break;
                    case "cripta": Cripta(root.transform, c, r); break;
                    case "mina": Mina(root.transform, c, r); break;
                    case "hito": Hito(root.transform, c, r, l); break;
                    case "guarida": Guarida(root.transform, c, r); break;
                    case "secreto": Secreto(root.transform, c, r, l); break;
                    default: Ruina(root.transform, c, r, l); break;
                }
                Component poi = MundoGancho.Añadir(root, Exploracion + "PointOfInterest");
                MundoGancho.Llamar(poi, "Configure", l.id, l.nombre, l.tipo, l.secreto, l.descripcion ?? "", l.recompensa ?? "");
                int rareza = l.secreto ? 2 : (l.tipo == "cueva" || l.tipo == "cripta" || l.tipo == "torre" || l.tipo == "guarida" || l.tipo == "mirador") ? 1 : -1;
                if (l.tipo == "ruina" || l.tipo == "mina") rareza = r.NextDouble() < 0.5 ? 1 : 0;
                if (rareza >= 0)
                    Cofre(root.transform, c + Desfase(r, 3f, 7f), rareza, l.recompensa ?? "");
                if (l.tipo == "ruina" || l.tipo == "santuario" || l.tipo == "hito" || l.tipo == "altar" || l.tipo == "cripta" || l.secreto)
                    Lapida(root.transform, c + Desfase(r, 5f, 9f), l.nombre, l.descripcion);
                Recursos(root.transform, c, r, l.tipo, l.region);
            }
        }

        private static Vector3 Desfase(System.Random r, float min, float max)
        {
            float a = (float)(r.NextDouble() * Math.PI * 2), d = min + (float)r.NextDouble() * (max - min);
            return new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
        }

        private static float Yaw(Vector3 desde, Vector3 hacia) => Mathf.Atan2(hacia.x - desde.x, hacia.z - desde.z) * Mathf.Rad2Deg;

        private static void Anillo(Transform p, Vector3 c, System.Random r, int n, float radio, float jitter, bool mirarCentro, float escala, params string[] modelos)
        {
            float a0 = (float)(r.NextDouble() * Math.PI * 2);
            for (int i = 0; i < n; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / n + (float)(r.NextDouble() - 0.5) * jitter;
                float rr = radio * (0.85f + (float)r.NextDouble() * 0.3f);
                Vector3 q = c + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                if (T.DistanciaCamino(q.x, q.z) < 2.5f) continue;
                float yaw = mirarCentro ? Yaw(q, c) : (float)(r.NextDouble() * 360);
                PonerEn(p, q.x, q.z, yaw, escala * (0.9f + (float)r.NextDouble() * 0.2f), modelos[i % modelos.Length]);
            }
        }

        private static void Poblado(Transform p, Vector3 c, System.Random r, bool conTablon)
        {
            PonerEn(p, c.x, c.z, 0f, 1f, "pozo");
            Anillo(p, c, r, 4, 7f, 0.2f, false, 1f, "farol");
            string[] casas = { "casa_a", "casa_b", "casa_a", "taberna", "casa_b", "casa_a", "casa_b", "casa_a", "casa_a" };
            int n = 7 + r.Next(3);
            float a0 = (float)(r.NextDouble() * Math.PI * 2);
            for (int i = 0; i < n; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / n + (float)(r.NextDouble() - 0.5) * 0.25f;
                float rr = 20f + (float)r.NextDouble() * 12f;
                Vector3 q = c + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                if (T.DistanciaCamino(q.x, q.z) < 6f) { q = c + new Vector3(Mathf.Cos(a + 0.35f) * rr, 0f, Mathf.Sin(a + 0.35f) * rr); }
                if (T.DistanciaCamino(q.x, q.z) < 5f) continue;
                PonerEn(p, q.x, q.z, Yaw(q, c), 1f, casas[i % casas.Length]);
                // detalles junto a cada casa
                Vector3 lado = q + (c - q).normalized * 5f + Desfase(r, 1f, 3f);
                PonerEn(p, lado.x, lado.z, (float)(r.NextDouble() * 360), 1f, r.NextDouble() < 0.5 ? "caja" : "carreta");
            }
            Anillo(p, c, r, 10, 40f, 0.3f, true, 1f, "valla");
            Vector3 torre = c + Desfase(r, 36f, 42f);
            PonerEn(p, torre.x, torre.z, Yaw(torre, c), 1f, "torre_vigia");
            Anillo(p, c, r, 3, 12f, 0.4f, true, 1f, "tienda");
            Fogata(p, c + Desfase(r, 9f, 12f));
            if (conTablon && !tablonPuesto)
            {
                tablonPuesto = true;
                GameObject tablon = new GameObject("Tablón de misiones del Cruce");
                tablon.transform.SetParent(p, false);
                Vector3 q = c + new Vector3(4f, 0f, -4f);
                tablon.transform.position = Suelo(q.x, q.z);
                tablon.AddComponent<VillageNoticeboard>();
                PonerEn(p, q.x + 1.2f, q.z, 0f, 1f, "poste_senal");
            }
        }

        private static void Puerto(Transform p, Vector3 c, System.Random r)
        {
            // buscar hacia dónde está el agua
            Vector3 mejor = Vector3.forward;
            float bajo = float.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float h = AlturaRejilla(c.x + d.x * 30f, c.z + d.z * 30f);
                if (h < bajo) { bajo = h; mejor = d; }
            }
            Vector3 muelle = c + mejor * 26f;
            GameObject m = PonerEn(p, muelle.x, muelle.z, Mathf.Atan2(mejor.x, mejor.z) * Mathf.Rad2Deg, 1f, "muelle");
            if (m != null) m.transform.position = new Vector3(muelle.x, T.Agua + 0.6f, muelle.z);
            for (int i = 0; i < 3; i++)
            {
                Vector3 q = c - mejor * (8f + i * 2f) + Vector3.Cross(mejor, Vector3.up) * (i - 1) * 11f;
                PonerEn(p, q.x, q.z, Yaw(q, muelle), 1f, i == 1 ? "taberna" : "casa_a");
            }
            Anillo(p, c, r, 5, 9f, 0.5f, false, 1f, "caja", "carreta", "farol");
            Fogata(p, c + Desfase(r, 4f, 6f));
        }

        private static void Campamento(Transform p, Vector3 c, System.Random r)
        {
            Fogata(p, c);
            Anillo(p, c, r, 3, 7f, 0.3f, true, 1f, "tienda");
            Anillo(p, c, r, 7, 13f, 0.25f, true, 1f, "empalizada");
            Anillo(p, c, r, 2, 5f, 0.6f, false, 1f, "estandarte");
            Anillo(p, c, r, 3, 9f, 0.6f, false, 1f, "caja", "jaula");
        }

        private static void Ruina(Transform p, Vector3 c, System.Random r, LugarDiseno l)
        {
            string[] piezas = { "muro_ruina_a", "muro_ruina_b", "arco_ruina", "columna", "columna_rota", "muro_ruina_a", "escalera", "columna_rota" };
            int n = 8 + r.Next(5);
            for (int i = 0; i < n; i++)
            {
                Vector3 q = c + Desfase(r, 3f, 22f);
                PonerEn(p, q.x, q.z, (float)(r.NextDouble() * 4) * 90f + (float)(r.NextDouble() * 10 - 5), 1f + (float)r.NextDouble() * 0.3f, piezas[i % piezas.Length], "muro_ruina_a");
            }
            if (Contiene(l, "aguja", "gótic", "gotic", "catedral", "capilla"))
                PonerEn(p, c.x, c.z, 0f, 1.4f, "aguja_gotica", "torre_ruina");
            Anillo(p, c, r, 5, 15f, 0.8f, false, 1f, "roca_b", "arbusto_a", "huesos");
        }

        private static void Altar(Transform p, Vector3 c, System.Random r)
        {
            PonerEn(p, c.x, c.z, 0f, 1.3f, "altar_sangre");
            Anillo(p, c, r, 4, 14f, 0f, true, 1.2f, "obelisco_runa");
            Anillo(p, c, r, 8, 21f, 0f, true, 1f, "columna", "columna_rota");
            Anillo(p, c, r, 4, 11f, 0.2f, true, 1f, "estandarte");
            Anillo(p, c, r, 7, 30f, 0.15f, true, 1f, "muro_ciudadela", "muro_ruina_b");
        }

        private static void Torre(Transform p, Vector3 c, System.Random r)
        {
            PonerEn(p, c.x, c.z, (float)(r.NextDouble() * 360), 1f, "torre_ruina");
            Anillo(p, c, r, 4, 11f, 0.5f, true, 1f, "muro_ruina_a", "columna_rota");
        }

        private static void Santuario(Transform p, Vector3 c, System.Random r)
        {
            PonerEn(p, c.x, c.z, 0f, 1f, "santuario");
            Anillo(p, c, r, 3, 7f, 0.5f, false, 1f, "cristales", "hongos_brillo");
        }

        private static void Mirador(Transform p, Vector3 c, System.Random r, LugarDiseno l)
        {
            if (Contiene(l, "lucero", "ángel", "angel", "caído", "caido"))
                PonerEn(p, c.x, c.z, (float)(r.NextDouble() * 360), 1f, "estatua_lucero");
            else
                PonerEn(p, c.x, c.z, 0f, 1f, "obelisco_runa");
            Anillo(p, c, r, 5, 6f, 0.5f, false, 1f, "roca_b", "roca_c");
            PonerEn(p, c.x + 3f, c.z + 2f, 30f, 1f, "poste_senal");
        }

        private static void Cueva(Transform p, Vector3 c, System.Random r, LugarDiseno l)
        {
            // boca de cueva: dos peñas inclinadas y un fondo oscuro
            Vector3 frente = new Vector3((float)(r.NextDouble() - 0.5), 0f, (float)(r.NextDouble() - 0.5)).normalized;
            if (frente == Vector3.zero) frente = Vector3.forward;
            Pendiente(c.x, c.z, out Vector2 bajada);
            if (bajada.sqrMagnitude > 0.0001f) frente = new Vector3(bajada.x, 0f, bajada.y).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, frente);
            float yaw = Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg;
            PonerEn(p, c.x + lado.x * 6f - frente.x * 4f, c.z + lado.z * 6f - frente.z * 4f, yaw + 80f, 1.1f, "acantilado_a", "roca_d");
            PonerEn(p, c.x - lado.x * 6f - frente.x * 4f, c.z - lado.z * 6f - frente.z * 4f, yaw - 80f, 1.1f, "acantilado_b", "roca_d");
            PonerEn(p, c.x - frente.x * 9f, c.z - frente.z * 9f, yaw, 1.3f, "acantilado_b", "roca_d");
            Vector3 s = Suelo(c.x, c.z);
            GameObject techo = PonerEn(p, c.x - frente.x * 3f, c.z - frente.z * 3f, yaw + 90f, 1.2f, "roca_d");
            if (techo != null) techo.transform.position = s + Vector3.up * 6.5f - frente * 3f;
            Material oscuro = K.Mat("Roca_oscura");
            GameObject fondo = Primitiva("Fondo de la cueva", PrimitiveType.Cube, p, s - frente * 6f + Vector3.up * 2f, new Vector3(7f, 4.5f, 1f), oscuro, true);
            fondo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Anillo(p, c - frente * 3f, r, 3, 2.5f, 0.6f, false, 1f, "hongos_brillo", "cristales");
            if (Contiene(l, "cascada"))
                Cascada(p, s + frente * 1.5f + Vector3.up * 0.2f, yaw, 6f, 9f);
        }

        private static void Cascada(Transform p, Vector3 base_, float yaw, float ancho, float alto)
        {
            GameObject g = new GameObject("Cascada");
            g.transform.SetParent(p, false);
            Mesh m = new Mesh { name = "cascada" };
            m.vertices = new[] { new Vector3(-ancho / 2, 0, 0), new Vector3(ancho / 2, 0, 0), new Vector3(-ancho / 2, alto, 0.6f), new Vector3(ancho / 2, alto, 0.6f) };
            m.colors = new[] { new Color(1, 1, 1, 0.3f), new Color(1, 1, 1, 0.3f), new Color(1, 1, 1, 0.6f), new Color(1, 1, 1, 0.6f) };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 };
            m.RecalculateNormals();
            string ruta = CarpetaMallasAgua + "/cascada_" + (base_.x * 10f + base_.z).GetHashCode().ToString("x") + ".asset";
            AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(m, ruta);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            g.AddComponent<MeshRenderer>().sharedMaterial = K.AguaRio;
            g.transform.position = base_;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static void Cripta(Transform p, Vector3 c, System.Random r)
        {
            PonerEn(p, c.x, c.z, (float)(r.Next(4) * 90), 1f, "cripta_entrada");
            for (int i = 0; i < 12; i++)
            {
                Vector3 q = c + new Vector3(-9f + (i % 4) * 6f, 0f, 9f + (i / 4) * 4f) + Desfase(r, 0f, 0.8f);
                PonerEn(p, q.x, q.z, (float)(r.NextDouble() * 20 - 10), 1f, "lapida");
            }
            Anillo(p, c, r, 4, 8f, 0.6f, false, 1f, "huesos", "arbol_muerto_a");
        }

        private static void Mina(Transform p, Vector3 c, System.Random r)
        {
            GameObject e = PonerEn(p, c.x, c.z, (float)(r.NextDouble() * 360), 1f, "entrada_mina", "mina_entrada", "cripta_entrada");
            PonerEn(p, c.x + 5f, c.z + 2f, 30f, 1f, "vagoneta", "carreta");
            for (int i = 0; i < 5; i++)
            {
                Vector3 q = c + Desfase(r, 6f, 14f);
                Nodo(p, q, ResourceKind.Ore, 12, 2, "cristales", "roca_oscura");
            }
            Anillo(p, c, r, 3, 9f, 0.5f, false, 1f, "caja", "farol");
        }

        private static void Hito(Transform p, Vector3 c, System.Random r, LugarDiseno l)
        {
            if (Contiene(l, "árbol", "arbol", "madre"))
                PonerEn(p, c.x, c.z, (float)(r.NextDouble() * 360), 1f, "arbol_madre");
            else if (Contiene(l, "estatua", "lucero", "ángel", "angel", "caído", "caido"))
                PonerEn(p, c.x, c.z, (float)(r.NextDouble() * 360), 1.6f, "estatua_lucero");
            else if (Contiene(l, "portal", "grieta", "infiern", "abismo"))
                PonerEn(p, c.x, c.z, (float)(r.NextDouble() * 360), 1.4f, "portal_infernal");
            else
                PonerEn(p, c.x, c.z, 0f, 2.4f, "obelisco_runa");
            Anillo(p, c, r, 6, 14f, 0.4f, false, 1f, "roca_b", "roca_musgo", "arbusto_b");
        }

        private static void Guarida(Transform p, Vector3 c, System.Random r)
        {
            Anillo(p, c, r, 5, 8f, 0.5f, false, 1f, "huesos");
            Anillo(p, c, r, 4, 13f, 0.5f, false, 1f, "roca_c", "roca_d", "arbol_muerto_b");
        }

        private static void Secreto(Transform p, Vector3 c, System.Random r, LugarDiseno l)
        {
            Anillo(p, c, r, 4, 3.5f, 0.6f, false, 1f, "hongos_brillo", "cristales");
            Anillo(p, c, r, 6, 9f, 0.4f, false, 1f, "roca_musgo", "arbusto_a", "roca_b");
            if (Contiene(l, "cascada")) Cascada(p, Suelo(c.x, c.z) + Vector3.forward * 4f, 180f, 6f, 8f);
        }

        private static bool Contiene(LugarDiseno l, params string[] claves)
        {
            string t = ((l.nombre ?? "") + " " + (l.descripcion ?? "")).ToLowerInvariant();
            foreach (string k in claves) if (t.Contains(k)) return true;
            return false;
        }

        private static void Fogata(Transform p, Vector3 q)
        {
            GameObject f = PonerEn(p, q.x, q.z, 0f, 1f, "hoguera");
            if (f == null) return;
            GameObject luz = new GameObject("Luz de la hoguera");
            luz.transform.SetParent(f.transform, false);
            luz.transform.localPosition = Vector3.up * 1.2f;
            Light l = luz.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.55f, 0.2f);
            l.range = 9f;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;
        }

        // ================================================================ cofres, lápidas y recursos
        private static void Cofre(Transform p, Vector3 q, int rareza, string contenido)
        {
            GameObject root = new GameObject(rareza == 2 ? "Cofre épico" : rareza == 1 ? "Cofre raro" : "Cofre");
            root.transform.SetParent(p, false);
            root.transform.position = Suelo(q.x, q.z);
            root.transform.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            Material atlas = AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/KayKitDungeon/KayKitAtlas.mat");
            GameObject cuerpo = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitDungeon + "chest.obj");
            GameObject tapa = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitDungeon + "chest_lid.obj");
            if (cuerpo != null)
            {
                GameObject b = (GameObject)PrefabUtility.InstantiatePrefab(cuerpo, root.transform);
                b.name = "cuerpo";
                foreach (Renderer rr in b.GetComponentsInChildren<Renderer>()) if (atlas != null) rr.sharedMaterial = atlas;
                foreach (Collider cc in b.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(cc);
            }
            if (tapa != null)
            {
                GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(tapa, root.transform);
                t.name = "tapa";
                foreach (Renderer rr in t.GetComponentsInChildren<Renderer>()) if (atlas != null) rr.sharedMaterial = atlas;
                foreach (Collider cc in t.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(cc);
            }
            if (cuerpo == null)
                Primitiva("cuerpo", PrimitiveType.Cube, root.transform, root.transform.position + Vector3.up * 0.4f, new Vector3(1.2f, 0.8f, 0.8f), K.Mat("Madera"), false);
            root.transform.localScale = Vector3.one * (rareza == 2 ? 1.5f : 1.25f);
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.2f, 1f, 0.9f);
            box.center = new Vector3(0f, 0.5f, 0f);
            if (rareza >= 1)
            {
                GameObject brillo = new GameObject("Brillo");
                brillo.transform.SetParent(root.transform, false);
                brillo.transform.localPosition = Vector3.up * 1.2f;
                Light l = brillo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = rareza == 2 ? new Color(1f, 0.75f, 0.25f) : new Color(0.4f, 0.65f, 1f);
                l.range = 5f;
                l.intensity = 1.2f;
                l.shadows = LightShadows.None;
            }
            Component chest = MundoGancho.Añadir(root, Exploracion + "TreasureChest");
            MundoGancho.Llamar(chest, "Configure", rareza, contenido ?? "");
        }

        private static void Lapida(Transform p, Vector3 q, string titulo, string texto)
        {
            GameObject g = PonerEn(p, q.x, q.z, Rand(0f, 360f), 1f, "lapida", "obelisco_runa");
            if (g == null) return;
            Component lore = MundoGancho.Añadir(g, Exploracion + "LoreStone");
            MundoGancho.Llamar(lore, "Configure", titulo ?? "", string.IsNullOrEmpty(texto) ? "Las letras están gastadas por el tiempo." : texto);
        }

        private static void Nodo(Transform p, Vector3 q, ResourceKind tipo, int total, int porGolpe, params string[] modelos)
        {
            GameObject g = PonerEn(p, q.x, q.z, Rand(0f, 360f), 1f, modelos);
            if (g == null)
            {
                g = new GameObject("Recurso");
                g.transform.SetParent(p, false);
                g.transform.position = Suelo(q.x, q.z);
                Primitiva("Visual", tipo == ResourceKind.Wood ? PrimitiveType.Cylinder : PrimitiveType.Sphere, g.transform,
                    g.transform.position + Vector3.up * 0.6f, Vector3.one * 1.2f, K.Mat("Roca"), true);
            }
            g.isStatic = false;
            g.name = (tipo == ResourceKind.Wood ? "Árbol talable" : tipo == ResourceKind.Ore ? "Veta de mineral" :
                tipo == ResourceKind.Fiber ? "Arbusto de fibra" : tipo == ResourceKind.Crystal ? "Cristales" : "Roca") + " (" + g.name + ")";
            g.AddComponent<HarvestNode>().Configure(tipo, total, porGolpe);
        }

        /// <summary>Recursos recolectables junto a cada lugar (madera, piedra, fibra; mineral y cristal en zonas duras).</summary>
        private static void Recursos(Transform p, Vector3 c, System.Random r, string tipo, string region)
        {
            RegionDiseno reg = D.Region(region);
            string bioma = reg != null ? reg.bioma : "pradera";
            bool pueblo = tipo == "poblado" || tipo == "puerto";
            int arboles = pueblo ? 6 : 2, rocas = pueblo ? 4 : 2, fibras = pueblo ? 3 : 1;
            string[] madera = reg != null && reg.arboles.Count > 0 ? reg.arboles.ToArray() : new[] { "roble_a", "pino_a" };
            for (int i = 0; i < arboles; i++) Nodo(p, c + Desfase(r, 45f, 62f), ResourceKind.Wood, 12, 3, madera[i % madera.Length], "roble_a", "pino_a");
            for (int i = 0; i < rocas; i++) Nodo(p, c + Desfase(r, 30f, 55f), ResourceKind.Stone, 15, 3, "roca_b", "roca_a");
            for (int i = 0; i < fibras; i++) Nodo(p, c + Desfase(r, 30f, 55f), ResourceKind.Fiber, 9, 3, "arbusto_a", "arbusto_b");
            if (bioma == "ceniza" || bioma == "cumbres" || bioma == "nieve" || bioma == "carmesi")
                Nodo(p, c + Desfase(r, 20f, 40f), ResourceKind.Crystal, 8, 2, "cristales", "roca_oscura");
            if (bioma == "ruinas" || bioma == "meseta" || bioma == "cumbres")
                Nodo(p, c + Desfase(r, 20f, 40f), ResourceKind.Ore, 10, 2, "roca_oscura", "roca_c");
        }

        // ================================================================ enemigos
        private static void Campamentos()
        {
            foreach (CampamentoDiseno camp in D.campamentos)
            {
                Vector3 c = Suelo(camp.posicion.x, camp.posicion.z);
                Transform grupo = Hijo(enemigos, "Campamento " + NombreRegion(camp.region) + " (" + camp.posicion.x.ToString("0") + "," + camp.posicion.z.ToString("0") + ")");
                // utilería si no hay un lugar del diseño encima
                bool hayLugar = false;
                foreach (Vector2 o in ocupados) if (Vector2.Distance(o, camp.posicion.V) < 20f) { hayLugar = true; break; }
                List<string> tipos = camp.tipos != null && camp.tipos.Count > 0 ? camp.tipos : new List<string> { "saqueador" };
                bool bestias = tipos.TrueForAll(t => t == "lobo");
                if (!hayLugar && !bestias)
                {
                    Transform u = Hijo(lugares, "Puesto enemigo");
                    u.position = c;
                    Campamento(u, c, Semilla(camp.region + camp.posicion.x));
                }
                int n = Mathf.Clamp(camp.cantidad, 1, 8);
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2f + Rand(-0.3f, 0.3f);
                    float rr = n == 1 ? 0f : Rand(3.5f, 7f);
                    Vector3 q = c + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                    string tipo = tipos[i % tipos.Count];
                    Bestiario(grupo, tipo, Suelo(q.x, q.z), camp.nivel, i);
                }
            }
        }

        private static void Jefes()
        {
            foreach (JefeDiseno j in D.jefes)
            {
                Vector3 c = Suelo(j.posicion.x, j.posicion.z);
                string nombre = "JEFE - " + j.nombre;
                GameObject e = Enemigo(nombre, enemigos, c, Mathf.Max(300f, j.vida), Mathf.Max(14f, j.dano), 2.6f, true);
                string modelo = string.IsNullOrEmpty(j.modelo) ? "Knight" : j.modelo;
                Modelo(e.transform, modelo, modelo == "Mage" ? KayKitEnemySetup.AttackStyle.Spell : KayKitEnemySetup.AttackStyle.Heavy);
                RegionDiseno reg = D.Region(j.region);
                Nivel(e, reg != null ? Mathf.Clamp(reg.nivel + 4, 1, 40) : 20);
            }
        }

        private static void Bestiario(Transform padre, string tipo, Vector3 pos, int nivel, int i)
        {
            EnemyArchetype data = EnemyArchetypes.Find(tipo);
            if (data == null) { Debug.LogWarning("[Vhaldren] Tipo de enemigo desconocido: " + tipo); return; }
            string nombre = data.DisplayName + " " + (char)('A' + i);
            GameObject e = Enemigo(nombre, padre, pos, data.Health, data.Damage, data.Speed, false);
            PrototypeEnemy pe = e.GetComponent<PrototypeEnemy>();
            SerializedObject so = new SerializedObject(pe);
            so.FindProperty("archetypeId").stringValue = tipo;
            so.FindProperty("attackRange").floatValue = data.AttackRange;
            so.FindProperty("aggroRange").floatValue = data.Aggro;
            so.ApplyModifiedPropertiesWithoutUndo();
            switch (tipo)
            {
                case "saqueador": Modelo(e.transform, "Barbarian", KayKitEnemySetup.AttackStyle.Melee); break;
                case "ballestero": Modelo(e.transform, "Rogue", KayKitEnemySetup.AttackStyle.Ranged); break;
                case "acolito":
                case "sanadora": Modelo(e.transform, "Mage", KayKitEnemySetup.AttackStyle.Spell); break;
                case "bruto": Modelo(e.transform, "Barbarian", KayKitEnemySetup.AttackStyle.Heavy); break;
                case "centinela_runico": Modelo(e.transform, "Knight", KayKitEnemySetup.AttackStyle.Heavy); break;
                case "esqueleto": Modelo(e.transform, i % 2 == 1 ? "Skeleton_Minion" : "Skeleton_Warrior", KayKitEnemySetup.AttackStyle.Melee); break;
                case "lobo": Bestia(e.transform, "montura_lobo_bruma", 0.75f); break;
                default: Modelo(e.transform, "Rogue", KayKitEnemySetup.AttackStyle.Melee); break;
            }
            Nivel(e, nivel);
        }

        private static void Nivel(GameObject e, int nivel)
        {
            Vhaldren.Gameplay.Progression.CombatLevel cl = e.GetComponent<Vhaldren.Gameplay.Progression.CombatLevel>() ??
                                                       e.AddComponent<Vhaldren.Gameplay.Progression.CombatLevel>();
            cl.Configure(Mathf.Clamp(nivel, 1, 40));
        }

        private static GameObject Enemigo(string nombre, Transform padre, Vector3 suelo, float hp, float dano, float velocidad, bool jefe)
        {
            GameObject root = new GameObject(nombre);
            root.transform.SetParent(padre, false);
            float alto = jefe ? 2.8f : 2f;
            root.transform.position = suelo + Vector3.up * (alto * 0.5f + 0.08f);
            root.transform.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            CharacterController body = root.AddComponent<CharacterController>();
            body.height = alto;
            body.radius = jefe ? 0.55f : 0.34f;
            body.center = Vector3.zero;
            body.minMoveDistance = 0f;
            body.slopeLimit = 50f;
            body.stepOffset = 0.5f;
            CombatHealth health = root.AddComponent<CombatHealth>();
            SerializedObject sh = new SerializedObject(health);
            sh.FindProperty("maximumHealth").floatValue = hp;
            sh.ApplyModifiedPropertiesWithoutUndo();
            health.ResetToFull();
            root.AddComponent<CombatDamageFeedback>().ConfigureForEnemy(jefe);

            GameObject visual = Primitiva("Visual enemigo", PrimitiveType.Capsule, root.transform, root.transform.position,
                jefe ? new Vector3(1.4f, 1.5f, 1.4f) : new Vector3(0.84f, 1f, 0.84f), K.Mat("Tela_roja"), false);
            visual.transform.localPosition = Vector3.zero;

            PrototypeEnemy enemy = root.AddComponent<PrototypeEnemy>();
            SerializedObject s = new SerializedObject(enemy);
            s.FindProperty("target").objectReferenceValue = jugador;
            s.FindProperty("modelRoot").objectReferenceValue = visual;
            s.FindProperty("isBoss").boolValue = jefe;
            s.FindProperty("moveSpeed").floatValue = velocidad;
            s.FindProperty("attackDamage").floatValue = dano;
            s.FindProperty("aggroRange").floatValue = jefe ? 20f : 15f;
            s.FindProperty("attackRange").floatValue = jefe ? 2.4f : 1.75f;
            s.FindProperty("respawnSeconds").floatValue = jefe ? 120f : 60f;
            s.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>Modelo KayKit animado bajo "Visual enemigo" (igual que el mundo anterior).</summary>
        private static void Modelo(Transform root, string modelId, KayKitEnemySetup.AttackStyle style)
        {
            Transform anchor = root.Find("Visual enemigo");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitEnemySetup.PrefabPath(modelId));
            RuntimeAnimatorController overrides = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(KayKitEnemySetup.OverridePath(modelId, style));
            if (anchor == null || prefab == null) return;
            CharacterController body = root.GetComponent<CharacterController>();
            float half = body != null ? body.height * 0.5f : 1f;
            bool boss = body != null && body.height > 2.4f;
            float esc = boss ? 1.12f : 0.82f;
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            model.name = "Modelo texturizado";
            Vector3 a = anchor.localScale;
            model.transform.localPosition = new Vector3(0f, -half / Mathf.Max(0.01f, a.y), 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = new Vector3(esc / a.x, esc / a.y, esc / a.z);
            Animator anim = model.GetComponent<Animator>();
            if (anim != null && overrides != null) anim.runtimeAnimatorController = overrides;
            MeshRenderer ph = anchor.GetComponent<MeshRenderer>();
            if (ph != null) ph.enabled = false;
        }

        /// <summary>Bestia de cuatro patas: modelo de montura con animación procedural.</summary>
        private static void Bestia(Transform root, string modelo, float escala)
        {
            Transform anchor = root.Find("Visual enemigo");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Monturas/" + modelo + ".prefab");
            if (anchor == null || prefab == null) return;
            CharacterController body = root.GetComponent<CharacterController>();
            float half = body != null ? body.height * 0.5f : 1f;
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            model.name = "Modelo de bestia";
            Vector3 a = anchor.localScale;
            model.transform.localPosition = new Vector3(0f, -half / Mathf.Max(0.01f, a.y), 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = new Vector3(escala / a.x, escala / a.y, escala / a.z);
            MundoGancho.Añadir(model, Monturas_ + "QuadrupedGait");
            MeshRenderer ph = anchor.GetComponent<MeshRenderer>();
            if (ph != null) ph.enabled = false;
        }

        // ================================================================ monturas, piedras y habitantes
        private static void Monturas()
        {
            Transform padre = Hijo(lugares, "Monturas salvajes");
            foreach (MonturaDiseno m in D.monturas)
            {
                GameObject g = new GameObject("Montura salvaje - " + m.nombre);
                g.transform.SetParent(padre, false);
                g.transform.position = Suelo(m.posicion.x, m.posicion.z);
                g.transform.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                Component spawn = MundoGancho.Añadir(g, Monturas_ + "MountSpawn");
                MundoGancho.Llamar(spawn, "Configure", m.id, m.nombre, m.vuela, m.velocidad > 0f ? m.velocidad : (m.vuela ? 24f : 14f));
                ocupados.Add(m.posicion.V);
            }
        }

        private static void PiedrasViaje()
        {
            Transform padre = Hijo(lugares, "Piedras de viaje");
            foreach (PiedraDiseno p in D.piedrasViaje)
            {
                GameObject g = PonerEn(padre, p.posicion.x, p.posicion.z, Rand(0f, 360f), 1f, "piedra_camino");
                if (g == null)
                {
                    g = new GameObject();
                    g.transform.SetParent(padre, false);
                    g.transform.position = Suelo(p.posicion.x, p.posicion.z);
                    Primitiva("Monolito", PrimitiveType.Cube, g.transform, g.transform.position + Vector3.up * 1.6f, new Vector3(0.9f, 3.2f, 0.6f), K.Mat("Runa_azul"), true);
                }
                g.isStatic = false;
                g.name = "Piedra de viaje - " + p.nombre;
                Component w = MundoGancho.Añadir(g, Exploracion + "Waystone");
                MundoGancho.Llamar(w, "Configure", p.id, p.nombre);
                GameObject luz = new GameObject("Luz rúnica");
                luz.transform.SetParent(g.transform, false);
                luz.transform.localPosition = Vector3.up * 2.5f;
                Light l = luz.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(0.35f, 0.7f, 1f); l.range = 7f; l.intensity = 1.4f; l.shadows = LightShadows.None;
            }
        }

        private static void Habitantes()
        {
            foreach (NpcDiseno n in D.npcs)
            {
                Vector3 pos = Suelo(n.posicion.x, n.posicion.z);
                GameObject g = new GameObject(n.nombre + " (" + n.oficio + ")");
                g.transform.SetParent(npcs, false);
                g.transform.position = pos;
                string modelo = ModeloOficio(n.oficio);
                Component npc = MundoGancho.Añadir(g, "Vhaldren.Gameplay.NPC.NpcVillager");
                if (npc == null) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitEnemySetup.PrefabPath(modelo));
                MundoGancho.Campo(npc, "modelPrefab", prefab);
                string[] frases = { n.frase ?? "" };
                MundoGancho.Llamar(npc, "Configure", n.id, n.nombre, n.oficio, frases, pos, modelo);
            }
        }

        private static string ModeloOficio(string oficio)
        {
            string o = (oficio ?? "").ToLowerInvariant();
            if (o.Contains("herr") || o.Contains("cazad") || o.Contains("leñ") || o.Contains("min")) return "Barbarian";
            if (o.Contains("mag") || o.Contains("sab") || o.Contains("alqu") || o.Contains("sanad") || o.Contains("sacer")) return "Mage";
            if (o.Contains("guard") || o.Contains("capit") || o.Contains("cabal") || o.Contains("solda")) return "Knight";
            return "Rogue";
        }
    }
}
