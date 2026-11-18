using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>Region del mapa: circulo en el plano XZ con su ambiente propio.</summary>
    [Serializable]
    public sealed class AtlasRegion
    {
        public string id;
        public string nombre;
        public Vector2 centro;
        public float radio;
        public int nivel;
        public Color colorNiebla = new Color(0.45f, 0.47f, 0.52f);
        public float densidadNiebla = 0.006f;
        public Color colorLuz = Color.white;
    }

    /// <summary>
    /// Lago circular. Con <see cref="nivelAgua"/> finito la profundidad es la altura del agua
    /// menos la de los pies; con NaN se usa un perfil de cuenco (maximo en el centro).
    /// </summary>
    [Serializable]
    public sealed class AtlasLake
    {
        public string nombre;
        public Vector2 centro;
        public float radio;
        public float nivelAgua = float.NaN;
        public float profundidad = 3f;
    }

    /// <summary>
    /// Rio como polilinea con anchura. Sin nivel de agua (NaN) la profundidad sigue un perfil
    /// parabolico: <see cref="profundidad"/> en el centro del cauce y cero en la orilla.
    /// </summary>
    [Serializable]
    public sealed class AtlasRiver
    {
        public string nombre;
        public List<Vector2> puntos = new List<Vector2>();
        public float anchura = 8f;
        public float profundidad = 2f;
        public float nivelAgua = float.NaN;

        [NonSerialized] internal Rect bounds;
        [NonSerialized] internal bool boundsReady;
    }

    /// <summary>
    /// Atlas del mundo (uno por escena): regiones, agua, limites y textura del mapa.
    /// Lo rellena el generador del mapa desde el editor con <see cref="AddRegion"/>,
    /// <see cref="AddLake"/>, <see cref="AddRiver"/>, <see cref="SetBounds"/> y
    /// <see cref="SetMapTexture"/>. En juego lo consultan la region actual, el mapa, la
    /// guardia del agua y el vuelo. La textura del mapa cubre exactamente los limites:
    /// arriba = +Z (norte), derecha = +X.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldAtlas : MonoBehaviour
    {
        public const string DefaultMapResource = "Mapa/mapa_mundo";

        [SerializeField] private string mapaResource = DefaultMapResource;
        [SerializeField] private Vector2 limiteMin;
        [SerializeField] private Vector2 limiteMax;
        [Tooltip("Si esta activo, todo lo que quede por debajo de este nivel cuenta como agua (mar).")]
        [SerializeField] private bool aguaGlobal;
        [SerializeField] private float nivelAguaGlobal;
        [Tooltip("Metros sobre el terreno a los que vuelan las monturas aladas.")]
        [SerializeField, Range(15f, 60f)] private float alturaVuelo = 30f;
        [SerializeField] private List<AtlasRegion> regiones = new List<AtlasRegion>();
        [SerializeField] private List<AtlasLake> lagos = new List<AtlasLake>();
        [SerializeField] private List<AtlasRiver> rios = new List<AtlasRiver>();

        private static WorldAtlas active;

        /// <summary>Atlas activo de la escena (null en salas de prueba sin mapa grande).</summary>
        public static WorldAtlas Active => active;

        public IReadOnlyList<AtlasRegion> Regions => regiones;
        public IReadOnlyList<AtlasLake> Lakes => lagos;
        public IReadOnlyList<AtlasRiver> Rivers => rios;
        public string MapResource => string.IsNullOrEmpty(mapaResource) ? DefaultMapResource : mapaResource;
        public float FlightHeight => alturaVuelo;
        public bool HasGlobalWater => aguaGlobal;
        public float GlobalWaterLevel => nivelAguaGlobal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active = null;

        private void OnEnable()
        {
            if (active == null || !active.isActiveAndEnabled)
                active = this;
        }

        private void OnDisable()
        {
            if (active == this)
                active = null;
        }

        // ── Construccion (editor o codigo) ─────────────────────────────

        /// <summary>Borra regiones, agua y limites (para regenerar el mapa).</summary>
        public void Clear()
        {
            regiones.Clear();
            lagos.Clear();
            rios.Clear();
            limiteMin = limiteMax = Vector2.zero;
            aguaGlobal = false;
        }

        public AtlasRegion AddRegion(string id, string nombre, Vector2 centro, float radio, int nivel,
            Color colorNiebla, float densidadNiebla)
            => AddRegion(id, nombre, centro, radio, nivel, colorNiebla, densidadNiebla, Color.white);

        /// <summary>Añade una region. Si ya habia una con el mismo id, la sustituye.</summary>
        public AtlasRegion AddRegion(string id, string nombre, Vector2 centro, float radio, int nivel,
            Color colorNiebla, float densidadNiebla, Color colorLuz)
        {
            AtlasRegion region = new AtlasRegion
            {
                id = string.IsNullOrEmpty(id) ? "region_" + regiones.Count : id,
                nombre = string.IsNullOrEmpty(nombre) ? id : nombre,
                centro = centro,
                radio = Mathf.Max(1f, radio),
                nivel = Mathf.Max(1, nivel),
                colorNiebla = colorNiebla,
                densidadNiebla = Mathf.Clamp(densidadNiebla, 0f, 0.2f),
                colorLuz = colorLuz
            };
            for (int i = 0; i < regiones.Count; i++)
            {
                if (regiones[i] != null && regiones[i].id == region.id)
                {
                    regiones[i] = region;
                    return region;
                }
            }
            regiones.Add(region);
            return region;
        }

        /// <summary>Lago circular. nivelAgua = altura Y de la superficie (NaN = perfil de cuenco).</summary>
        public AtlasLake AddLake(Vector2 centro, float radio, float nivelAgua, float profundidad, string nombre = "")
        {
            AtlasLake lake = new AtlasLake
            {
                nombre = nombre ?? string.Empty,
                centro = centro,
                radio = Mathf.Max(0.5f, radio),
                nivelAgua = nivelAgua,
                profundidad = Mathf.Max(0f, profundidad)
            };
            lagos.Add(lake);
            return lake;
        }

        /// <summary>Rio: polilinea en XZ con anchura total. nivelAgua NaN = perfil parabolico.</summary>
        public AtlasRiver AddRiver(IList<Vector2> puntos, float anchura, float profundidad = 2f,
            float nivelAgua = float.NaN, string nombre = "")
        {
            AtlasRiver river = new AtlasRiver
            {
                nombre = nombre ?? string.Empty,
                anchura = Mathf.Max(0.5f, anchura),
                profundidad = Mathf.Max(0f, profundidad),
                nivelAgua = nivelAgua
            };
            if (puntos != null)
                for (int i = 0; i < puntos.Count; i++)
                    river.puntos.Add(puntos[i]);
            rios.Add(river);
            return river;
        }

        /// <summary>Limites del mapa en XZ (los cubre exactamente la textura del mapa).</summary>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            limiteMin = Vector2.Min(min, max);
            limiteMax = Vector2.Max(min, max);
        }

        /// <summary>Ruta en Resources de la textura del mapa (sin extension).</summary>
        public void SetMapTexture(string resourcePath) =>
            mapaResource = string.IsNullOrEmpty(resourcePath) ? DefaultMapResource : resourcePath;

        /// <summary>Mar: por debajo de este nivel todo cuenta como agua.</summary>
        public void SetGlobalWater(bool enabled, float level)
        {
            aguaGlobal = enabled;
            nivelAguaGlobal = level;
        }

        /// <summary>Nivel del mar: por debajo todo cuenta como agua (equivale a SetGlobalWater(true, nivel)).</summary>
        public void SetSeaLevel(float level) => SetGlobalWater(true, level);

        public void SetFlightHeight(float metres) => alturaVuelo = Mathf.Clamp(metres, 15f, 60f);

        // ── Consultas ──────────────────────────────────────────────────

        public Vector2 BoundsMin { get { ResolveBounds(out Vector2 min, out _); return min; } }
        public Vector2 BoundsMax { get { ResolveBounds(out _, out Vector2 max); return max; } }

        /// <summary>Limites declarados o, si faltan, los que abarcan todas las regiones.</summary>
        public void ResolveBounds(out Vector2 min, out Vector2 max)
        {
            if (limiteMax.x - limiteMin.x > 1f && limiteMax.y - limiteMin.y > 1f)
            {
                min = limiteMin;
                max = limiteMax;
                return;
            }
            ExplorationMath.RegionBounds(regiones, out min, out max);
        }

        public bool Contains(Vector2 point)
        {
            ResolveBounds(out Vector2 min, out Vector2 max);
            return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;
        }

        public int RegionIndexAt(Vector3 position) =>
            ExplorationMath.FindRegionIndex(regiones, ExplorationMath.Planar(position));

        /// <summary>Region que contiene la posicion (si varias, la de centro mas cercano); null si ninguna.</summary>
        public AtlasRegion RegionAt(Vector3 position)
        {
            int index = RegionIndexAt(position);
            return index >= 0 ? regiones[index] : null;
        }

        public AtlasRegion FindRegion(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < regiones.Count; i++)
                if (regiones[i] != null && regiones[i].id == id)
                    return regiones[i];
            return null;
        }

        /// <summary>
        /// Profundidad del agua (m) bajo unos pies. <paramref name="groundY"/> opcional: si los
        /// pies quedan claramente por encima del terreno (un puente) los cauces sin nivel de
        /// agua no cuentan. 0 = seco.
        /// </summary>
        public float WaterDepthAt(Vector3 feet, float groundY = float.NaN)
        {
            if (!ExplorationMath.IsFinite(feet.x) || !ExplorationMath.IsFinite(feet.y) ||
                !ExplorationMath.IsFinite(feet.z))
                return 0f;
            Vector2 p = new Vector2(feet.x, feet.z);
            bool onBridge = ExplorationMath.IsFinite(groundY) && feet.y > groundY + 0.6f;
            float depth = 0f;

            for (int i = 0; i < lagos.Count; i++)
            {
                AtlasLake lake = lagos[i];
                if (lake == null)
                    continue;
                float distance = Vector2.Distance(p, lake.centro);
                if (distance > lake.radio)
                    continue;
                float d = ExplorationMath.IsFinite(lake.nivelAgua)
                    ? ExplorationMath.SurfaceDepth(lake.nivelAgua, feet.y)
                    : onBridge ? 0f : ExplorationMath.ChannelDepth(distance, lake.radio, lake.profundidad);
                depth = Mathf.Max(depth, d);
            }

            for (int i = 0; i < rios.Count; i++)
            {
                AtlasRiver river = rios[i];
                if (river == null || river.puntos == null || river.puntos.Count == 0)
                    continue;
                float half = river.anchura * 0.5f;
                if (!river.boundsReady)
                {
                    river.bounds = ExplorationMath.PolylineBounds(river.puntos, half);
                    river.boundsReady = true;
                }
                if (!river.bounds.Contains(p))
                    continue;
                float distance = ExplorationMath.DistanceToPolyline(river.puntos, p);
                if (distance > half)
                    continue;
                float d = ExplorationMath.IsFinite(river.nivelAgua)
                    ? ExplorationMath.SurfaceDepth(river.nivelAgua, feet.y)
                    : onBridge ? 0f : ExplorationMath.ChannelDepth(distance, half, river.profundidad);
                depth = Mathf.Max(depth, d);
            }

            if (aguaGlobal && Contains(p))
                depth = Mathf.Max(depth, ExplorationMath.SurfaceDepth(nivelAguaGlobal, feet.y));
            return depth;
        }

        private void OnValidate()
        {
            for (int i = 0; i < rios.Count; i++)
                if (rios[i] != null)
                    rios[i].boundsReady = false;
        }

        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < regiones.Count; i++)
            {
                AtlasRegion r = regiones[i];
                if (r == null)
                    continue;
                Gizmos.color = new Color(r.colorNiebla.r, r.colorNiebla.g, r.colorNiebla.b, 0.9f);
                Vector3 c = new Vector3(r.centro.x, transform.position.y, r.centro.y);
                const int steps = 48;
                for (int s = 0; s < steps; s++)
                {
                    float a0 = s * Mathf.PI * 2f / steps, a1 = (s + 1) * Mathf.PI * 2f / steps;
                    Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r.radio,
                        c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r.radio);
                }
            }
        }
    }
}
