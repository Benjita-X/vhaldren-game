using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Lugar de interes. Al entrar en su radio se descubre (PlayerPrefs) con aviso
    /// "Descubierto: nombre" y aparece en el mapa. Si es secreto: titulo "¡Lugar secreto!"
    /// y recompensa (esencia, materiales o equipo segun el texto de recompensa). Un lugar
    /// normal solo da recompensa si su texto de recompensa no esta vacio.
    /// </summary>
    public sealed class PointOfInterest : MonoBehaviour
    {
        private static readonly List<PointOfInterest> all = new List<PointOfInterest>(64);

        [SerializeField] private string idLugar = "";
        [SerializeField] private string nombre = "Lugar";
        [Tooltip("ruina, santuario, cueva, torre, poblado, campamento, jefe, altar, mazmorra...")]
        [SerializeField] private string tipo = "";
        [SerializeField] private bool secreto;
        [SerializeField, TextArea(2, 6)] private string descripcion = "";
        [SerializeField] private string recompensa = "";
        [SerializeField, Min(1f)] private float radio = 12f;

        private bool discovered;
        private bool loaded;

        public static IReadOnlyList<PointOfInterest> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        public string PlaceId => !string.IsNullOrEmpty(idLugar) ? idLugar : ExplorationSave.PositionId("lugar", transform.position);
        public string DisplayName => string.IsNullOrEmpty(nombre) ? "Lugar" : nombre;
        public string Kind => tipo ?? string.Empty;
        public bool IsSecret => secreto;
        public string Description => descripcion ?? string.Empty;
        public string Reward => recompensa ?? string.Empty;
        public float Radius => radio;
        public bool IsDiscovered { get { Load(); return discovered; } }

        /// <summary>Lo llama el generador del mapa.</summary>
        public void Configure(string id, string nombre, string tipo, bool secreto, string descripcion, string recompensa)
        {
            idLugar = id ?? string.Empty;
            this.nombre = string.IsNullOrEmpty(nombre) ? "Lugar" : nombre;
            this.tipo = tipo ?? string.Empty;
            this.secreto = secreto;
            this.descripcion = descripcion ?? string.Empty;
            this.recompensa = recompensa ?? string.Empty;
            if (secreto && radio > 8f)
                radio = 6f;   // los secretos hay que encontrarlos de cerca
        }

        public void SetRadius(float metres) => radio = Mathf.Max(1f, float.IsNaN(metres) ? 12f : metres);

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable() => all.Remove(this);

        private void Load()
        {
            if (loaded)
                return;
            loaded = true;
            discovered = ExplorationSave.Has(ExplorationSave.Places, PlaceId);
        }

        /// <summary>Revisa todos los lugares cercanos al jugador (lo llama RegionTracker unas 3 veces por segundo).</summary>
        public static void CheckDiscoveries(GameObject player)
        {
            if (player == null)
                return;
            Vector3 p = player.transform.position;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                PointOfInterest poi = all[i];
                if (poi == null)
                {
                    all.RemoveAt(i);
                    continue;
                }
                if (poi.IsDiscovered)
                    continue;
                Vector3 offset = poi.transform.position - p;
                // Los lugares normales tambien se descubren sobrevolandolos; los secretos, solo a pie.
                if (Mathf.Abs(offset.y) > poi.radio + (poi.secreto ? 6f : 45f))
                    continue;
                offset.y = 0f;
                if (offset.sqrMagnitude <= poi.radio * poi.radio)
                    poi.Discover(player);
            }
        }

        /// <summary>Marca el lugar como descubierto (con avisos y, si toca, recompensa).</summary>
        public void Discover(GameObject player)
        {
            Load();
            if (discovered)
                return;
            discovered = true;
            ExplorationSave.Mark(ExplorationSave.Places, PlaceId);
            if (secreto)
            {
                ExplorationBanner.Show("¡Lugar secreto!", DisplayName, new Color(0.85f, 0.6f, 1f), true, 3.2f);
                ExplorationRewards.GrantPlace(player, recompensa, 2);
                VfxHost host = VfxHost.Get();
                if (host != null)
                    host.Glints(player.transform.position + Vector3.up * 1.2f, 1.2f, new Color(0.85f, 0.6f, 1f), 30, 0.16f, 2f, Vector3.up);
            }
            else
            {
                HudNotices.Post("Descubierto: " + DisplayName, HudNotices.Reward, 3f);
                ExplorationBanner.Show(DisplayName, string.IsNullOrEmpty(descripcion) ? KindLabel(tipo) : descripcion,
                    HudSkin.GoldBright, false, 2.6f);
                if (!string.IsNullOrEmpty(recompensa))
                    ExplorationRewards.GrantPlace(player, recompensa, 0);
            }
        }

        /// <summary>Color del icono en el mapa segun el tipo.</summary>
        public static Color KindColor(string kind, bool secret)
        {
            if (secret)
                return new Color(0.85f, 0.6f, 1f);
            string k = (kind ?? string.Empty).ToLowerInvariant();
            if (k.Contains("jefe") || k.Contains("mazmorra") || k.Contains("guarida")) return new Color(1f, 0.38f, 0.32f);
            if (k.Contains("poblado") || k.Contains("pueblo") || k.Contains("aldea") || k.Contains("campamento")) return new Color(0.98f, 0.82f, 0.45f);
            if (k.Contains("santuario") || k.Contains("altar") || k.Contains("templo")) return new Color(0.6f, 0.9f, 1f);
            if (k.Contains("ruina") || k.Contains("torre") || k.Contains("fuerte") || k.Contains("castillo")) return new Color(0.82f, 0.78f, 0.7f);
            if (k.Contains("cueva") || k.Contains("mina")) return new Color(0.7f, 0.6f, 0.5f);
            return HudSkin.GoldBright;
        }

        public static string KindLabel(string kind)
        {
            if (string.IsNullOrEmpty(kind))
                return "Lugar de interés";
            return char.ToUpperInvariant(kind[0]) + kind.Substring(1);
        }
    }
}
