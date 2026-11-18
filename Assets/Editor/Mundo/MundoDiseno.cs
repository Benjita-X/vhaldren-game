using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Vhaldren.EditorTools.Mundo
{
    // Esquema de Assets/Art/Mundo/diseno_mapa.json (lo lee JsonUtility: solo clases y listas).
    [Serializable] public class P2 { public float x; public float z; public Vector2 V => new Vector2(x, z); }
    [Serializable] public class Col { public float r; public float g; public float b; public Color C => new Color(r, g, b); }

    [Serializable] public class CamaraDiseno { public float inclinacion; public float fov; public float distancia; public float distanciaMin; public float distanciaMax; }

    [Serializable]
    public class RegionDiseno
    {
        public string id; public string nombre; public string bioma; public P2 centro; public float radio;
        public float alturaBase; public float relieve; public float rugosidad; public int nivel;
        public Col colorNiebla; public float densidadNiebla; public List<string> texturas = new List<string>();
        public float densidadArboles; public List<string> arboles = new List<string>(); public string descripcion;
    }

    [Serializable] public class LineaDiseno { public List<P2> puntos = new List<P2>(); public float altura; public float anchura; public float profundidad; }
    [Serializable] public class PasoDiseno { public P2 posicion; public float anchura; public string nombre; }
    [Serializable] public class LagoDiseno { public P2 centro; public float radio; public float profundidad; public string nombre; }

    [Serializable]
    public class LugarDiseno
    {
        public string id; public string nombre; public string tipo; public string region; public P2 posicion;
        public bool secreto; public string descripcion; public string recompensa;
    }

    [Serializable] public class CampamentoDiseno { public string region; public P2 posicion; public List<string> tipos = new List<string>(); public int cantidad = 3; public int nivel = 1; }

    [Serializable]
    public class JefeDiseno
    {
        public string id; public string nombre; public string region; public P2 posicion; public string modelo;
        public float vida; public float dano; public string descripcion;
    }

    [Serializable] public class MonturaDiseno { public string id; public string nombre; public string region; public P2 posicion; public bool vuela; public float velocidad; public string requisito; }
    [Serializable] public class PiedraDiseno { public string id; public string nombre; public P2 posicion; }
    [Serializable] public class NpcDiseno { public string id; public string nombre; public string oficio; public string poblado; public P2 posicion; public string frase; }

    [Serializable]
    public class DisenoMapa
    {
        public float tamano = 2048f; public float alturaMaxima = 260f; public float nivelAgua = 18f;
        public CamaraDiseno camara = new CamaraDiseno();
        public List<RegionDiseno> regiones = new List<RegionDiseno>();
        public List<LineaDiseno> cordilleras = new List<LineaDiseno>();
        public List<PasoDiseno> pasos = new List<PasoDiseno>();
        public List<LineaDiseno> rios = new List<LineaDiseno>();
        public List<LagoDiseno> lagos = new List<LagoDiseno>();
        public List<LineaDiseno> caminos = new List<LineaDiseno>();
        public List<LugarDiseno> lugares = new List<LugarDiseno>();
        public List<CampamentoDiseno> campamentos = new List<CampamentoDiseno>();
        public List<JefeDiseno> jefes = new List<JefeDiseno>();
        public List<MonturaDiseno> monturas = new List<MonturaDiseno>();
        public List<PiedraDiseno> piedrasViaje = new List<PiedraDiseno>();
        public List<NpcDiseno> npcs = new List<NpcDiseno>();
        public P2 inicio = new P2();

        public const string Ruta = "Assets/Art/Mundo/diseno_mapa.json";

        public static DisenoMapa Cargar(string ruta = Ruta)
        {
            if (!File.Exists(ruta))
                throw new FileNotFoundException("Falta el diseño del mapa", ruta);
            DisenoMapa d = JsonUtility.FromJson<DisenoMapa>(File.ReadAllText(ruta));
            if (d == null || d.regiones == null || d.regiones.Count == 0)
                throw new InvalidDataException("El diseño del mapa no tiene regiones.");
            return d;
        }

        public RegionDiseno Region(string id)
        {
            foreach (RegionDiseno r in regiones)
                if (r.id == id)
                    return r;
            return null;
        }
    }

    // Manifiesto del kit de modelos (Assets/Art/Mundo/manifest.json).
    [Serializable]
    public class ModeloKit
    {
        public string nombre; public string archivo; public string categoria; public List<float> dims = new List<float>();
        public int tris; public List<string> materiales = new List<string>(); public string colisor; public float radio;
        public List<float> tamano = new List<float>();
    }

    [Serializable] public class ManifiestoKit { public List<ModeloKit> modelos = new List<ModeloKit>(); }
}
