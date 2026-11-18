using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Vhaldren.Gameplay.Mounts
{
    /// <summary>
    /// Monturas domadas, guardadas en PlayerPrefs (comunes a todos los mundos) como una
    /// lista separada por comas, y la ultima usada. Las funciones de lista son puras.
    /// </summary>
    public static class MountUnlocks
    {
        public const string ListKey = "vh.monturas.desbloqueadas";
        public const string LastKey = "vh.monturas.ultima";

        private static List<string> cache;
        private static string last;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache = null;
            last = null;
            Changed = null;
        }

        /// <summary>Minusculas, letras, numeros y guion bajo ("Grifo " -> "grifo").</summary>
        public static string Sanitize(string id)
        {
            if (string.IsNullOrEmpty(id))
                return string.Empty;
            StringBuilder builder = new StringBuilder(id.Length);
            foreach (char c in id.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    builder.Append(c);
                else if (c == ' ' || c == '-')
                    builder.Append('_');
            }
            return builder.ToString();
        }

        /// <summary>Lista sin vacios ni repetidos, en el orden guardado.</summary>
        public static List<string> ParseList(string raw)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(raw))
                return result;
            foreach (string piece in raw.Split(','))
            {
                string id = Sanitize(piece);
                if (id.Length > 0 && !result.Contains(id))
                    result.Add(id);
            }
            return result;
        }

        public static bool ListContains(string raw, string id)
        {
            string key = Sanitize(id);
            return key.Length > 0 && ParseList(raw).Contains(key);
        }

        /// <summary>Añade el id al final si no estaba. Devuelve la lista como texto.</summary>
        public static string AddToList(string raw, string id)
        {
            List<string> list = ParseList(raw);
            string key = Sanitize(id);
            if (key.Length > 0 && !list.Contains(key))
                list.Add(key);
            return string.Join(",", list);
        }

        private static List<string> Load()
        {
            if (cache == null)
                cache = ParseList(PlayerPrefs.GetString(ListKey, string.Empty));
            return cache;
        }

        public static IReadOnlyList<string> Unlocked => Load();

        public static int Count => Load().Count;

        public static bool IsUnlocked(string id) => Load().Contains(Sanitize(id));

        /// <summary>Doma una montura. Devuelve true si es nueva. Pasa a ser la ultima usada.</summary>
        public static bool Unlock(string id)
        {
            string key = Sanitize(id);
            if (key.Length == 0)
                return false;
            List<string> list = Load();
            bool added = !list.Contains(key);
            if (added)
            {
                list.Add(key);
                PlayerPrefs.SetString(ListKey, string.Join(",", list));
            }
            Last = key;
            PlayerPrefs.Save();
            Changed?.Invoke();
            return added;
        }

        /// <summary>Ultima montura usada si sigue domada; si no, la primera domada; null si ninguna.</summary>
        public static string Last
        {
            get
            {
                if (last == null)
                    last = Sanitize(PlayerPrefs.GetString(LastKey, string.Empty));
                List<string> list = Load();
                if (last.Length > 0 && list.Contains(last))
                    return last;
                return list.Count > 0 ? list[list.Count - 1] : null;
            }
            set
            {
                string key = Sanitize(value);
                if (key.Length == 0 || key == last)
                    return;
                last = key;
                PlayerPrefs.SetString(LastKey, key);
            }
        }

        /// <summary>Olvida todas las monturas (pruebas o nueva partida).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(ListKey);
            PlayerPrefs.DeleteKey(LastKey);
            cache = null;
            last = null;
            Changed?.Invoke();
        }
    }
}
