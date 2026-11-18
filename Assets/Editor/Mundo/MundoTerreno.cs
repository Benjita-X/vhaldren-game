using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Vhaldren.EditorTools.Mundo
{
    /// <summary>
    /// Relieve del mapa grande a partir del diseño: regiones con altura y rugosidad
    /// propias, cordilleras con pasos, bordes naturales (montaña o mar), ríos que bajan,
    /// lagos, caminos suavizados y explanadas para los lugares. Determinista.
    /// </summary>
    public sealed class MundoTerreno
    {
        public sealed class Linea
        {
            public Vector2[] P;
            public float[] Superficie; // ríos: altura del agua en cada punto
            public float Anchura, Altura, Profundidad;
            public float MinX, MinZ, MaxX, MaxZ;

            public float Distancia(float x, float z, out int seg, out float t)
            {
                float best = float.MaxValue; seg = 0; t = 0f;
                for (int i = 0; i < P.Length - 1; i++)
                {
                    float d = MundoGeo.DistSeg(x, z, P[i].x, P[i].y, P[i + 1].x, P[i + 1].y, out float tt);
                    if (d < best) { best = d; seg = i; t = tt; }
                }
                return best;
            }

            public bool Cerca(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }

        public sealed class Explanada
        {
            public Vector2 C; public float Radio; public float Altura; public bool Orilla;
        }

        public readonly DisenoMapa D;
        public readonly float Tam, Mitad, HMax, Agua;
        private readonly MundoRuido ruido = new MundoRuido(1813);
        private readonly MundoRuido ruido2 = new MundoRuido(4242);
        public readonly List<Linea> Cordilleras = new List<Linea>();
        public readonly List<Linea> Rios = new List<Linea>();
        public readonly List<Linea> Caminos = new List<Linea>();
        public readonly List<Explanada> Explanadas = new List<Explanada>();
        public readonly float[] NivelLago;
        private readonly RegionDiseno[] reg;

        public MundoTerreno(DisenoMapa d)
        {
            D = d;
            Tam = d.tamano > 0 ? d.tamano : 2048f;
            Mitad = Tam * 0.5f;
            // margen sobre la altura de diseño para que las cimas no queden recortadas en plano
            HMax = Math.Max(d.alturaMaxima > 0 ? d.alturaMaxima : 260f, 320f);
            Agua = d.nivelAgua;
            reg = d.regiones.ToArray();
            foreach (LineaDiseno c in d.cordilleras)
                Cordilleras.Add(c.puntos.Count >= 2 && Distancia(c) > 120f
                    ? Hacer(c, Math.Max(40f, c.anchura) * 0.6f + 70f, 22f, 60f, 0.0045f)
                    : Hacer(c, Math.Max(40f, c.anchura) * 0.6f, 0f));
            foreach (LineaDiseno c in d.caminos) Caminos.Add(Hacer(c, Math.Max(4f, c.anchura) + 30f, 10f));
            foreach (LineaDiseno c in d.rios) Rios.Add(Hacer(c, Math.Max(6f, c.anchura) + 40f, 8f));

            // Superficie de cada río: sigue el terreno base pero nunca sube aguas abajo.
            foreach (Linea r in Rios)
            {
                r.Superficie = new float[r.P.Length];
                float prev = float.MaxValue;
                for (int i = 0; i < r.P.Length; i++)
                {
                    float h = AlturaBase(r.P[i].x, r.P[i].y) - 1.2f;
                    if (EsMeseta(r.P[i].x, r.P[i].y)) h = Math.Min(h, Agua + 3f); // cañón
                    h = Math.Min(h, prev - 0.02f);
                    h = Math.Max(h, Agua - 0.5f);
                    r.Superficie[i] = h;
                    prev = h;
                }
                // suavizar escalones
                for (int pass = 0; pass < 3; pass++)
                    for (int i = 1; i < r.P.Length - 1; i++)
                        r.Superficie[i] = Math.Min(r.Superficie[i - 1], (r.Superficie[i - 1] + r.Superficie[i] * 2f + r.Superficie[i + 1]) * 0.25f);
            }

            NivelLago = new float[d.lagos.Count];
            for (int i = 0; i < d.lagos.Count; i++)
            {
                LagoDiseno l = d.lagos[i];
                float sum = 0f;
                for (int k = 0; k < 16; k++)
                {
                    float a = k / 16f * Mathf.PI * 2f;
                    sum += AlturaBase(l.centro.x + Mathf.Cos(a) * l.radio, l.centro.z + Mathf.Sin(a) * l.radio);
                }
                // el diseño pone los lagos en zonas bajas: su superficie es el nivel del agua
                NivelLago[i] = Agua;
            }

            // Explanadas para los lugares: el pueblo, el campamento o la ruina se asientan.
            foreach (LugarDiseno l in d.lugares)
            {
                float radio = RadioLugar(l.tipo);
                if (radio <= 0f) continue;
                float h = AlturaAsiento(l.posicion.V, radio);
                bool orilla = l.tipo == "puerto";
                if (!orilla) h = Math.Max(h, Agua + 1.6f);
                Explanadas.Add(new Explanada { C = l.posicion.V, Radio = radio, Altura = h, Orilla = orilla });
            }
            foreach (CampamentoDiseno c in d.campamentos)
                Explanadas.Add(new Explanada { C = c.posicion.V, Radio = 16f, Altura = Math.Max(Agua + 1.6f, AlturaAsiento(c.posicion.V, 16f)) });
            foreach (JefeDiseno j in d.jefes)
                Explanadas.Add(new Explanada { C = j.posicion.V, Radio = 26f, Altura = Math.Max(Agua + 1.6f, AlturaAsiento(j.posicion.V, 26f)) });
            foreach (PiedraDiseno p in d.piedrasViaje)
                Explanadas.Add(new Explanada { C = p.posicion.V, Radio = 8f, Altura = Math.Max(Agua + 1.2f, AlturaBase(p.posicion.x, p.posicion.z)) });
            Explanadas.Add(new Explanada { C = d.inicio.V, Radio = 14f, Altura = Math.Max(Agua + 1.6f, AlturaBase(d.inicio.x, d.inicio.z)) });
        }

        private bool EsMeseta(float x, float z)
        {
            float[] w = new float[reg.Length];
            Pesos(x, z, w);
            float m = 0f;
            for (int i = 0; i < reg.Length; i++) if (reg[i].bioma == "meseta") m += w[i];
            return m > 0.45f;
        }

        public static float RadioLugar(string tipo)
        {
            switch (tipo)
            {
                case "poblado": return 48f;
                case "puerto": return 30f;
                case "campamento": return 18f;
                case "ruina": return 28f;
                case "altar": return 32f;
                case "torre": return 16f;
                case "santuario": return 12f;
                case "cripta": return 16f;
                case "mina": return 16f;
                case "mirador": return 9f;
                case "hito": return 22f;
                case "guarida": return 18f;
                case "cueva": return 0f;   // la cueva se apoya en la pendiente tal cual
                case "secreto": return 0f;
                default: return 12f;
            }
        }

        private static float Distancia(LineaDiseno c)
        {
            float t = 0f;
            for (int i = 0; i < c.puntos.Count - 1; i++) t += Vector2.Distance(c.puntos[i].V, c.puntos[i + 1].V);
            return t;
        }

        private float AlturaAsiento(Vector2 c, float radio)
        {
            float sum = AlturaBase(c.x, c.y) * 2f;
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                sum += AlturaBase(c.x + Mathf.Cos(a) * radio, c.y + Mathf.Sin(a) * radio);
            }
            return sum / 10f;
        }

        private Linea Hacer(LineaDiseno c, float margen, float paso, float meandro = -1f, float frecuencia = 0.008f)
        {
            List<Vector2> pts = new List<Vector2>();
            foreach (P2 p in c.puntos) pts.Add(p.V);
            if (pts.Count == 1) pts.Add(pts[0] + Vector2.one * 0.1f);
            if (paso > 0f)
            {
                List<Vector2> fino = new List<Vector2>();
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    float len = Vector2.Distance(pts[i], pts[i + 1]);
                    int n = Math.Max(1, (int)(len / paso));
                    for (int k = 0; k < n; k++)
                    {
                        Vector2 q = Vector2.Lerp(pts[i], pts[i + 1], k / (float)n);
                        // meandros suaves: desplazar perpendicularmente con ruido
                        Vector2 dir = (pts[i + 1] - pts[i]).normalized;
                        Vector2 perp = new Vector2(-dir.y, dir.x);
                        float amp = meandro >= 0f ? meandro : Math.Min(18f, len * 0.08f);
                        float w = ruido2.Noise(q.x * frecuencia, q.y * frecuencia) * amp;
                        bool extremo = (i == 0 && k == 0);
                        fino.Add(extremo ? q : q + perp * w);
                    }
                }
                fino.Add(pts[pts.Count - 1]);
                pts = fino;
            }
            Linea l = new Linea { P = pts.ToArray(), Anchura = c.anchura, Altura = c.altura, Profundidad = c.profundidad };
            l.MinX = l.MinZ = float.MaxValue; l.MaxX = l.MaxZ = float.MinValue;
            foreach (Vector2 p in l.P)
            {
                l.MinX = Math.Min(l.MinX, p.x - margen); l.MaxX = Math.Max(l.MaxX, p.x + margen);
                l.MinZ = Math.Min(l.MinZ, p.y - margen); l.MaxZ = Math.Max(l.MaxZ, p.y + margen);
            }
            return l;
        }

        // ---------------- regiones ----------------
        /// <summary>Pesos suaves de cada región (suman 1). w debe tener tantos huecos como regiones.</summary>
        public void Pesos(float x, float z, float[] w)
        {
            float sum = 0f;
            for (int i = 0; i < reg.Length; i++)
            {
                float dx = x - reg[i].centro.x, dz = z - reg[i].centro.z;
                float r = Math.Max(60f, reg[i].radio);
                float d = (float)Math.Sqrt(dx * dx + dz * dz) / r;
                // bordes irregulares entre regiones
                d += ruido2.Noise(x * 0.004f + i * 3.1f, z * 0.004f - i * 1.7f) * 0.18f;
                float v = (float)Math.Exp(-d * d * 2.2f) + 1e-6f;
                w[i] = v;
                sum += v;
            }
            for (int i = 0; i < reg.Length; i++) w[i] /= sum;
        }

        public int RegionDominante(float x, float z)
        {
            float[] w = new float[reg.Length];
            Pesos(x, z, w);
            int best = 0;
            for (int i = 1; i < w.Length; i++) if (w[i] > w[best]) best = i;
            return best;
        }

        public RegionDiseno[] Regiones => reg;

        // ---------------- altura ----------------
        /// <summary>Terreno natural antes de ríos, lagos, caminos y explanadas.</summary>
        public float AlturaBase(float x, float z)
        {
            float[] w = new float[reg.Length];
            return AlturaBase(x, z, w);
        }

        public float AlturaBase(float x, float z, float[] w)
        {
            Pesos(x, z, w);
            float baseH = 0f, relieve = 0f, rug = 0f, meseta = 0f, carmesi = 0f;
            bool costaCerca = false;
            for (int i = 0; i < reg.Length; i++)
            {
                baseH += w[i] * reg[i].alturaBase;
                relieve += w[i] * Math.Max(0f, reg[i].relieve);
                rug += w[i] * MundoGeo.Clamp01(reg[i].rugosidad);
                if (reg[i].bioma == "meseta") meseta += w[i];
                if (reg[i].bioma == "carmesi" || reg[i].bioma == "ceniza") carmesi += w[i];
                if (reg[i].bioma == "costa" && w[i] > 0.35f) costaCerca = true;
            }
            // colinas: mezcla de ruido suave y crestas según la rugosidad
            float f = 0.0045f;
            float suave = ruido.Fbm(x * f, z * f, 5);
            float crestas = ruido.Ridged(x * f * 0.9f + 50f, z * f * 0.9f - 30f, 5) * 2f - 0.6f;
            float h = baseH + relieve * ((1f - rug) * suave + rug * crestas);
            // colinas medianas (lomas de 60-120 m) y detalle fino: nada se ve plano
            h += ruido2.Fbm(x * 0.011f + 13f, z * 0.011f - 7f, 4) * (3.5f + relieve * 0.3f);
            h += ruido.Fbm(x * 0.03f, z * 0.03f, 3) * (1.4f + relieve * 0.05f);
            // mesetas: terrazas escalonadas con farallones
            if (meseta > 0.05f)
            {
                float paso = 9f;
                float t = h / paso;
                float terr = (float)Math.Floor(t) + MundoGeo.Smooth((t - (float)Math.Floor(t) - 0.75f) * 4f);
                h = h + (terr * paso - h) * meseta * 0.85f;
            }
            // tierras carmesí/ceniza: agujas y grietas
            if (carmesi > 0.05f)
                h += ruido2.Ridged(x * 0.02f, z * 0.02f, 3) * 6f * carmesi;

            // cordilleras
            float pasoFactor = 1f;
            foreach (PasoDiseno p in D.pasos)
            {
                float dx = x - p.posicion.x, dz = z - p.posicion.z;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                float a = Math.Max(30f, p.anchura);
                pasoFactor = Math.Min(pasoFactor, 0.12f + 0.88f * MundoGeo.Smooth((d - a * 0.35f) / (a * 1.1f)));
            }
            // cordilleras: "altura" es la altura absoluta de la cresta; se aplica max(terreno, perfil)
            // y en los pasos la cresta baja hasta casi el terreno del entorno.
            float montes = float.MinValue;
            foreach (Linea c in Cordilleras)
            {
                if (!c.Cerca(x, z)) continue;
                float d = c.Distancia(x, z, out int seg, out float tt);
                float half = Math.Max(40f, c.Anchura) * 0.5f * (0.75f + 0.5f * (0.5f + 0.5f * ruido2.Noise(x * 0.004f + 3f, z * 0.004f + 9f)));
                if (d >= half) continue;
                float perfil = MundoGeo.Smooth(1f - d / half);
                perfil = (float)Math.Pow(perfil, 1.25f);
                float picos = 0.78f + 0.3f * ruido.Ridged(x * 0.006f + 7f, z * 0.006f + 3f, 4);
                float dientes = (ruido.Ridged(x * 0.02f, z * 0.02f, 3) - 0.4f) * 0.12f;
                float cresta = c.Altura * (picos + dientes);
                float m = h + (cresta - h) * perfil;
                montes = Math.Max(montes, m);
            }
            if (montes > h)
                h = h + (montes - h) * pasoFactor;

            // borde del mapa: montañas infranqueables o fondo marino en la costa
            float borde = Math.Min(Mitad - Math.Abs(x), Mitad - Math.Abs(z));
            // borde irregular: la costa y las montañas del límite no siguen una línea recta
            borde += ruido2.Fbm(x * 0.0035f + 77f, z * 0.0035f - 41f, 3) * 40f;
            float anchoBorde = 95f;
            if (borde < anchoBorde)
            {
                float t = MundoGeo.Smooth(1f - Math.Max(0f, borde) / anchoBorde);
                if (costaCerca)
                    h = h + ((Agua - 14f) - h) * t;
                else
                {
                    // en las cumbres ya altas no se suma más montaña de borde (no recortar la Corona)
                    float extra = t * t * (HMax * 0.6f) * (0.7f + 0.3f * ruido.Ridged(x * 0.008f, z * 0.008f, 4));
                    float techo = HMax * 0.82f;
                    h = h + Math.Max(0f, Math.Min(extra, techo - h));
                }
            }
            return h;
        }

        /// <summary>Altura final (en metros del mundo).</summary>
        public float Altura(float x, float z, float[] w)
        {
            float h = AlturaBase(x, z, w);

            // explanadas de los lugares
            foreach (Explanada e in Explanadas)
            {
                float dx = x - e.C.x, dz = z - e.C.y;
                float borde = 14f + e.Radio * 0.5f;
                float r = e.Radio + borde;
                if (dx > r || dx < -r || dz > r || dz < -r) continue;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d >= r) continue;
                float t = MundoGeo.Smooth((r - d) / borde);
                float objetivo = e.Altura + ruido.Noise(x * 0.08f, z * 0.08f) * 0.25f;
                h = h + (objetivo - h) * t;
            }

            // caminos: suavizar el relieve a lo largo del trazado
            foreach (Linea c in Caminos)
            {
                if (!c.Cerca(x, z)) continue;
                float d = c.Distancia(x, z, out int seg, out float t);
                float half = Math.Max(3f, c.Anchura) * 0.5f;
                float band = half + 9f;
                if (d >= band) continue;
                Vector2 q = Vector2.Lerp(c.P[seg], c.P[seg + 1], t);
                float centro = AlturaBase(q.x, q.y, w);
                // no hundir el camino bajo el agua
                centro = Math.Max(centro, Agua + 0.6f);
                float k = MundoGeo.Smooth((band - d) / 9f) * 0.85f;
                h = h + (centro - h) * k;
            }

            // ríos: cauce excavado con orillas suaves
            foreach (Linea r in Rios)
            {
                if (!r.Cerca(x, z)) continue;
                float d = r.Distancia(x, z, out int seg, out float t);
                float half = Math.Max(4f, r.Anchura) * 0.5f;
                float orilla = 10f + half * 0.6f;
                if (d >= half + orilla) continue;
                float sup = r.Superficie[seg] + (r.Superficie[seg + 1] - r.Superficie[seg]) * t;
                float prof = Math.Max(0.8f, r.Profundidad);
                float lecho = sup - prof * MundoGeo.Smooth(1f - d / Math.Max(1f, half));
                if (d < half)
                    h = Math.Min(h, Math.Min(lecho, sup - 0.35f));
                else
                {
                    float k = MundoGeo.Smooth(1f - (d - half) / orilla);
                    float objetivo = sup + 0.4f + (d - half) * 0.08f;
                    if (h > objetivo) h = h + (objetivo - h) * k;
                }
            }

            // lagos
            for (int i = 0; i < D.lagos.Count; i++)
            {
                LagoDiseno l = D.lagos[i];
                float dx = x - l.centro.x, dz = z - l.centro.z;
                float rr = l.radio + 30f;
                if (dx > rr || dx < -rr || dz > rr || dz < -rr) continue;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                d += ruido2.Noise(x * 0.02f, z * 0.02f) * l.radio * 0.12f;
                float nivel = NivelLago[i];
                if (d < l.radio)
                {
                    float fondo = nivel - Math.Max(2f, l.profundidad) * MundoGeo.Smooth(1f - d / l.radio);
                    h = Math.Min(h, Math.Min(fondo, nivel - 0.5f));
                }
                else if (d < rr)
                {
                    float k = MundoGeo.Smooth(1f - (d - l.radio) / 30f);
                    float objetivo = nivel + 0.3f + (d - l.radio) * 0.12f;
                    if (h > objetivo) h = h + (objetivo - h) * k;
                }
            }
            return Math.Max(0.5f, Math.Min(HMax - 0.5f, h));
        }

        /// <summary>Calcula la rejilla de alturas normalizadas para TerrainData (en paralelo).</summary>
        public float[,] Rejilla(int res)
        {
            float[,] heights = new float[res, res];
            Parallel.For(0, res, zi =>
            {
                float[] w = new float[reg.Length];
                float z = -Mitad + Tam * zi / (res - 1);
                for (int xi = 0; xi < res; xi++)
                {
                    float x = -Mitad + Tam * xi / (res - 1);
                    heights[zi, xi] = Altura(x, z, w) / HMax;
                }
            });
            return heights;
        }

        // ---------------- agua ----------------
        /// <summary>Altura del agua en (x,z) o float.MinValue si no hay agua encima.</summary>
        public float NivelAguaEn(float x, float z, float suelo)
        {
            float best = suelo < Agua ? Agua : float.MinValue;
            for (int i = 0; i < D.lagos.Count; i++)
            {
                LagoDiseno l = D.lagos[i];
                float dx = x - l.centro.x, dz = z - l.centro.z;
                if (dx * dx + dz * dz < (l.radio + 8f) * (l.radio + 8f) && suelo < NivelLago[i])
                    best = Math.Max(best, NivelLago[i]);
            }
            foreach (Linea r in Rios)
            {
                if (!r.Cerca(x, z)) continue;
                float d = r.Distancia(x, z, out int seg, out float t);
                if (d > Math.Max(4f, r.Anchura) * 0.5f + 3f) continue;
                float sup = r.Superficie[seg] + (r.Superficie[seg + 1] - r.Superficie[seg]) * t;
                if (suelo < sup) best = Math.Max(best, sup);
            }
            return best;
        }

        public float DistanciaCamino(float x, float z)
        {
            float best = float.MaxValue;
            foreach (Linea c in Caminos)
            {
                if (!c.Cerca(x, z)) continue;
                float d = c.Distancia(x, z, out _, out _) - Math.Max(3f, c.Anchura) * 0.5f;
                if (d < best) best = d;
            }
            return best;
        }

        public float DistanciaRio(float x, float z)
        {
            float best = float.MaxValue;
            foreach (Linea r in Rios)
            {
                if (!r.Cerca(x, z)) continue;
                float d = r.Distancia(x, z, out _, out _) - Math.Max(4f, r.Anchura) * 0.5f;
                if (d < best) best = d;
            }
            return best;
        }

        public float DistanciaExplanada(float x, float z)
        {
            float best = float.MaxValue;
            foreach (Explanada e in Explanadas)
            {
                float d = Vector2.Distance(new Vector2(x, z), e.C) - e.Radio;
                if (d < best) best = d;
            }
            return best;
        }

        public float Ruido(float x, float z, float f) => ruido2.Fbm(x * f, z * f, 3);
    }
}
