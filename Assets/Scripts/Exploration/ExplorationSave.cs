using System;
using System.Text;
using UnityEngine;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Progreso de exploracion guardado en PlayerPrefs: lugares descubiertos, cofres
    /// abiertos, piedras activadas, regiones visitadas, textos leidos y niebla del mapa.
    /// Cada mundo (escena) tiene sus propias claves. Los componentes leen su estado una vez
    /// y lo cachean: nunca se consulta por fotograma.
    /// </summary>
    public static class ExplorationSave
    {
        public const string Places = "lugar";
        public const string Chests = "cofre";
        public const string Waystones = "piedra";
        public const string Regions = "region";
        public const string Lore = "saber";
        public const string Tamed = "domada";
        public const string Fog = "niebla";

        private const string Root = "vh.explora.";
        private static string worldKey = "mundo";

        /// <summary>Algo nuevo se descubrio, abrio o activo (el mapa se refresca).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            worldKey = "mundo";
            Changed = null;
        }

        /// <summary>Clave del mundo (nombre de la escena). La fija ExplorationBootstrap.</summary>
        public static string WorldKey
        {
            get => worldKey;
            set => worldKey = Sanitize(value, "mundo");
        }

        public static string Key(string category, string id) =>
            Root + worldKey + "." + Sanitize(category, "x") + "." + Sanitize(id, "x");

        public static bool Has(string category, string id) =>
            !string.IsNullOrEmpty(id) && PlayerPrefs.GetInt(Key(category, id), 0) == 1;

        /// <summary>Marca como hecho. Devuelve true solo la primera vez.</summary>
        public static bool Mark(string category, string id)
        {
            if (string.IsNullOrEmpty(id) || Has(category, id))
                return false;
            PlayerPrefs.SetInt(Key(category, id), 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static string GetText(string category, string id) => PlayerPrefs.GetString(Key(category, id), string.Empty);

        public static void SetText(string category, string id, string value, bool flush)
        {
            PlayerPrefs.SetString(Key(category, id), value ?? string.Empty);
            if (flush)
                PlayerPrefs.Save();
        }

        /// <summary>Id estable para objetos sin id propio (cofres): nombre del tipo y posicion redondeada.</summary>
        public static string PositionId(string prefix, Vector3 position) =>
            prefix + "_" + Mathf.RoundToInt(position.x) + "_" + Mathf.RoundToInt(position.y) + "_" + Mathf.RoundToInt(position.z);

        /// <summary>Solo letras, numeros, guion y guion bajo (claves seguras de PlayerPrefs).</summary>
        public static string Sanitize(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return builder.Length > 0 ? builder.ToString() : fallback;
        }
    }
}
