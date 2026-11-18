using System.Text;
using UnityEngine;

namespace Vhaldren.Gameplay.NPC
{
    /// <summary>Oficio de un aldeano: decide su función en el diálogo.</summary>
    public enum NpcRole
    {
        Villager = 0,   // Aldeano: habla del lugar y de los encargos del Cruce.
        Smith = 1,      // Herrero: abre la forja.
        Merchant = 2,   // Mercader: comercio de las caravanas.
        Healer = 3,     // Sanadora: cura entera.
        Guide = 4       // Guía: marca un lugar con una luz.
    }

    /// <summary>
    /// Textos y reglas puras de los diálogos de Vhaldren: saludo, historia del lugar,
    /// encargos y despedida con la personalidad de cada oficio, y reacciones especiales
    /// cuando quien habla es Lucifer. Sin MonoBehaviour: se prueba en EditMode.
    /// </summary>
    public static class NpcDialogue
    {
        public const float CharactersPerSecond = 52f;
        public const string AskPlace = "Háblame de este lugar";
        public const string AskWork = "¿Tienes trabajo?";
        public const string Goodbye = "Adiós";

        /// <summary>True mientras hay un diálogo abierto (las teclas de combate se ignoran).</summary>
        public static bool IsOpen { get; internal set; }

        /// <summary>
        /// True si E (Recolectar/Interactuar) es ahora para un aldeano: hay uno a mano o un
        /// diálogo abierto. Otros sistemas que usen E pueden consultarlo para no pisarse.
        /// </summary>
        public static bool ClaimsInteractKey { get; internal set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOpen = false;
            ClaimsInteractKey = false;
        }

        // ── Oficio ─────────────────────────────────────────────────────

        /// <summary>Oficio a partir del texto libre del generador ("herrero", "Mercader", "sanadora"...).</summary>
        public static NpcRole RoleFor(string trade)
        {
            string t = Normalize(trade);
            if (t.Length == 0)
                return NpcRole.Villager;
            if (t.Contains("herre") || t.Contains("forj") || t.Contains("armer"))
                return NpcRole.Smith;
            if (t.Contains("merca") || t.Contains("comerc") || t.Contains("tender") || t.Contains("buhon") || t.Contains("vended"))
                return NpcRole.Merchant;
            if (t.Contains("sana") || t.Contains("curan") || t.Contains("medic") || t.Contains("hierb") || t.Contains("sacerdot"))
                return NpcRole.Healer;
            if (t.Contains("guia") || t.Contains("explor") || t.Contains("rastre") || t.Contains("batidor"))
                return NpcRole.Guide;
            return NpcRole.Villager;
        }

        public static string RoleTitle(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Smith: return "Herrería";
                case NpcRole.Merchant: return "Comercio";
                case NpcRole.Healer: return "Sanación";
                case NpcRole.Guide: return "Guía";
                default: return "Aldeano";
            }
        }

        /// <summary>Color del retrato y del nombre flotante según el oficio.</summary>
        public static Color PortraitColor(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Smith: return new Color(0.78f, 0.36f, 0.12f);
                case NpcRole.Merchant: return new Color(0.78f, 0.62f, 0.2f);
                case NpcRole.Healer: return new Color(0.3f, 0.68f, 0.45f);
                case NpcRole.Guide: return new Color(0.3f, 0.48f, 0.78f);
                default: return new Color(0.55f, 0.5f, 0.46f);
            }
        }

        /// <summary>Texto del botón de la función del oficio (la segunda opción del diálogo).</summary>
        public static string RoleOption(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Smith: return "¿Puedes forjarme algo? (forja)";
                case NpcRole.Merchant: return "Enséñame tu mercancía";
                case NpcRole.Healer: return "Cura mis heridas";
                case NpcRole.Guide: return "¿Adónde debo ir?";
                default: return AskWork;
            }
        }

        // ── Líneas ─────────────────────────────────────────────────────

        private static readonly string[] SmithHello =
        {
            "¿Vienes a por acero o a mirar cómo sudo? En el Cruce no hay tiempo para las dos cosas.",
            "Cuidado con el yunque, aún está caliente. Como mi paciencia.",
            "Cada noche de Luna carmesí me traen más hojas melladas. Y menos dueños que las recojan."
        };
        private static readonly string[] MerchantHello =
        {
            "¡Mercancía honrada, precios casi honrados! Pasa, pasa.",
            "Las caravanas ya no cruzan de noche. Lo que ves es lo que hay... y vale el doble.",
            "Si traes pieles o cristal, hablamos. Si traes deudas, sigue andando."
        };
        private static readonly string[] HealerHello =
        {
            "Siéntate. Respira. Sangras más de lo que crees.",
            "La luz cura, pero la paciencia también. De las dos me queda poca.",
            "Desde que el altar se tiñó de rojo no paro de coser heridas."
        };
        private static readonly string[] GuideHello =
        {
            "Conozco cada sendero entre el Cruce y el altar. Los que pisan los vivos y los que no.",
            "Si te pierdes, busca el humo: donde hay humo hay campamento, y donde hay campamento hay problemas.",
            "Los caminos cambian cuando sale la Luna carmesí. Yo no."
        };
        private static readonly string[] VillagerHello =
        {
            "Buenos días... si es que alguno lo es desde que llegó el Caballero de sangre.",
            "No te había visto antes. ¿Vienes por el camino del norte?",
            "Cierra bien la puerta cuando salga la luna roja. Hazme caso."
        };

        private static readonly string[] LuciferHello =
        {
            "¡Por las piedras del Cruce! Esas alas... ¿eres el Lucero? Mi abuela contaba que un día caería del cielo.",
            "Dicen que una estrella cayó sobre el Cruce y que de ella salió alguien con las alas rotas. Ya veo que no mentían.",
            "No te acerques demasiado al fuego, caído. Aunque sospecho que el fuego te obedece a ti."
        };

        private static readonly string[] Lore =
        {
            "El Cruce era el lugar donde se encontraban humanos, nacidos de magia y las estirpes de la noche. Ahora es lo único que queda en pie entre el bosque y el altar.",
            "Una noche de Luna carmesí, el Caballero de sangre tomó el altar del norte. Su esencia corrompe la tierra y despierta a los muertos de la cripta.",
            "Cada pocas noches la luna se tiñe de rojo. Los saqueadores se vuelven valientes y las esencias, más puras. Mal momento para pasear; buen momento para cazar.",
            "Al este quedan las ruinas de la vieja academia: centinelas rúnicos que aún obedecen órdenes de magos muertos.",
            "Los lobos de bruma cazan en manada en el bosque oscuro. Si oyes uno, ya te han oído a ti.",
            "Hace poco cayó una estrella cerca del Cruce. Desde entonces las hogueras arden más doradas, y nadie sabe por qué."
        };

        private const string LuciferLore =
            "Hay quien dice que el Lucero vino a castigar al Caballero de sangre. Otros, que vino a quitarle el trono. Tú sabrás cuál de las dos cosas eres.";

        public static string Greeting(NpcRole role, bool lucifer, int seed)
        {
            if (lucifer && Mathf.Abs(seed) % 2 == 0)
                return Pick(LuciferHello, seed / 2);
            switch (role)
            {
                case NpcRole.Smith: return Pick(SmithHello, seed);
                case NpcRole.Merchant: return Pick(MerchantHello, seed);
                case NpcRole.Healer: return Pick(HealerHello, seed);
                case NpcRole.Guide: return Pick(GuideHello, seed);
                default: return Pick(VillagerHello, seed);
            }
        }

        /// <summary>
        /// "Háblame de este lugar": alterna la historia común, lo propio del oficio, las frases
        /// del propio aldeano (del generador) y, para Lucifer, lo que se cuenta de él.
        /// </summary>
        public static string AboutPlace(NpcRole role, string[] ownLines, bool lucifer, int turn)
        {
            int own = ownLines != null ? ownLines.Length : 0;
            int cycle = Lore.Length + 1 + own + (lucifer ? 1 : 0);
            int k = ((turn % cycle) + cycle) % cycle;
            if (k < own && !string.IsNullOrEmpty(ownLines[k]))
                return ownLines[k];
            k -= own;
            if (k == 0)
                return RoleLore(role);
            k -= 1;
            if (lucifer && k == 0)
                return LuciferLore;
            if (lucifer)
                k -= 1;
            return Lore[Mathf.Clamp(k, 0, Lore.Length - 1)];
        }

        private static string RoleLore(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Smith:
                    return "El mejor mineral sale de las vetas de las canteras, pero los brutos de allí no regalan nada. Tráeme mineral y haremos algo que corte.";
                case NpcRole.Merchant:
                    return "Las caravanas pagan en fibra, en pieles o en cristal. El oro no sirve de nada cuando el Caballero de sangre te pisa los talones.";
                case NpcRole.Healer:
                    return "Las sanadoras del culto usan la misma luz que yo, pero la retuercen. Si ves a una curando bandidos, ve primero a por ella.";
                case NpcRole.Guide:
                    return "El altar está al norte. Antes de ir, hazte fuerte: el Caballero de sangre se enfurece cuando le quitas la mitad de la vida.";
                default:
                    return "Aquí todos ayudamos en lo que podemos. El tablón del Cruce siempre tiene algún encargo para quien no tenga miedo.";
            }
        }

        public static string Farewell(NpcRole role, bool lucifer)
        {
            if (lucifer)
                return "Vuela alto, Lucero. O al menos intenta no volver a caerte.";
            switch (role)
            {
                case NpcRole.Smith: return "Que tu acero no se rompa antes que tú.";
                case NpcRole.Merchant: return "¡Vuelve pronto! Y con los bolsillos llenos.";
                case NpcRole.Healer: return "Que la luz te guarde... y que no tengas que volver.";
                case NpcRole.Guide: return "No te alejes del camino cuando caiga la noche.";
                default: return "Ten cuidado ahí fuera.";
            }
        }

        /// <summary>Frase corta que el aldeano suelta al verte pasar (bocadillo).</summary>
        public static string Bark(NpcRole role, string[] ownLines, bool lucifer, int seed)
        {
            if (ownLines != null && ownLines.Length > 0 && Mathf.Abs(seed) % 3 == 0)
                return Pick(ownLines, seed / 3);
            if (lucifer && Mathf.Abs(seed) % 3 == 1)
                return "¿Esas alas son... de verdad?";
            switch (role)
            {
                case NpcRole.Smith: return "¡Acero recién templado!";
                case NpcRole.Merchant: return "¡Raciones, fibras, cristal!";
                case NpcRole.Healer: return "¿Heridas? Acércate.";
                case NpcRole.Guide: return "El camino del norte está peligroso.";
                default: return "Buen día, forastero.";
            }
        }

        public const string SmithReply = "Enséñame lo que traes. Si el material vale, el acero obedece.";
        public const string HealerDone = "Quieto... La luz vuelve a tus venas.";
        public const string HealerHealthy = "No tienes ni un rasguño. Vuelve cuando te hayan pegado de verdad.";
        public const string HealerTired = "Aún me tiemblan las manos del último conjuro. Dame un momento.";
        public const string NoCaravan = "Hoy no ha llegado ninguna caravana. Vuelve más tarde.";
        public const string WorkFallback = "Trabajo siempre hay: mata lo que muerde y recoge lo que no. El tablón del Cruce tiene encargos.";

        /// <summary>Respuesta del guía al marcar un lugar.</summary>
        public static string GuideReply(string place, Vector3 delta)
        {
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            int meters = Mathf.RoundToInt(flat.magnitude);
            if (meters < 8)
                return "Ya estás encima de " + place + ". Abre bien los ojos.";
            return "Mira hacia " + DirectionName(flat) + ": a unos " + meters + " m está " + place +
                   ". Te he marcado el camino con una luz en el cielo.";
        }

        // ── Utilidades puras ───────────────────────────────────────────

        /// <summary>Letras visibles del texto que aparece letra a letra.</summary>
        public static int VisibleCharacters(float elapsed, int length, float charactersPerSecond = CharactersPerSecond)
        {
            if (length <= 0 || float.IsNaN(elapsed) || elapsed <= 0f)
                return 0;
            if (float.IsInfinity(elapsed))
                return length;
            return Mathf.Clamp(Mathf.FloorToInt(elapsed * Mathf.Max(1f, charactersPerSecond)), 0, length);
        }

        /// <summary>Punto cardinal en español (+Z = norte, +X = este).</summary>
        public static string DirectionName(Vector3 flat)
        {
            if (flat.sqrMagnitude < 0.0001f)
                return "aquí mismo";
            float angle = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            if (angle < 0f)
                angle += 360f;
            string[] names = { "el norte", "el noreste", "el este", "el sureste", "el sur", "el suroeste", "el oeste", "el noroeste" };
            return names[Mathf.RoundToInt(angle / 45f) % 8];
        }

        public static string Pick(string[] lines, int seed)
        {
            if (lines == null || lines.Length == 0)
                return string.Empty;
            int index = ((seed % lines.Length) + lines.Length) % lines.Length;
            return lines[index] ?? string.Empty;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value.ToLowerInvariant())
            {
                switch (c)
                {
                    case 'á': case 'à': case 'ä': builder.Append('a'); break;
                    case 'é': case 'è': case 'ë': builder.Append('e'); break;
                    case 'í': case 'ì': case 'ï': builder.Append('i'); break;
                    case 'ó': case 'ò': case 'ö': builder.Append('o'); break;
                    case 'ú': case 'ù': case 'ü': builder.Append('u'); break;
                    default: builder.Append(c); break;
                }
            }
            return builder.ToString().Trim();
        }
    }
}
