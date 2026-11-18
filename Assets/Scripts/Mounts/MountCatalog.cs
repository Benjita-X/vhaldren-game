using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Mounts
{
    /// <summary>Silueta del sustituto de primitivas cuando falta el prefab de la montura.</summary>
    public enum MountShape { Wolf, Lion, Horse, Bear, Griffin, Phoenix }

    /// <summary>Datos de una montura: nombre, si vuela y sus velocidades.</summary>
    public sealed class MountDefinition
    {
        public string Id;
        public string Name;
        public bool Flies;
        /// <summary>Velocidad de marcha en tierra (m/s) sin galopar.</summary>
        public float GroundSpeed = 13f;
        /// <summary>Velocidad de crucero en vuelo (m/s).</summary>
        public float FlySpeed = 25f;
        /// <summary>Multiplicador con Mayús (galope / picado), gasta aguante.</summary>
        public float GallopMultiplier = 1.32f;
        public Color BodyColor = new Color(0.5f, 0.45f, 0.4f);
        public Color AccentColor = new Color(0.8f, 0.7f, 0.5f);
        public float Size = 1f;
        public MountShape Shape = MountShape.Horse;
        public string Description = "";

        public MountDefinition Clone() => (MountDefinition)MemberwiseClone();
    }

    /// <summary>
    /// Catalogo de monturas. Trae seis de serie (lobo_bruma, leon_cumbres, corcel_infernal,
    /// oso_acorazado, grifo, fenix_ceniza); <see cref="MountSpawn"/> registra nombre,
    /// vuelo y velocidad que fija el generador del mapa. Un id desconocido recibe una
    /// definicion generica (nunca falla).
    /// </summary>
    public static class MountCatalog
    {
        public const float MinGroundSpeed = 8f;
        public const float MaxGroundSpeed = 18f;
        public const float MinFlySpeed = 18f;
        public const float MaxFlySpeed = 30f;

        public static readonly string[] BuiltInIds =
        {
            "lobo_bruma", "leon_cumbres", "corcel_infernal", "oso_acorazado", "grifo", "fenix_ceniza"
        };

        private static readonly Dictionary<string, MountDefinition> registered = new Dictionary<string, MountDefinition>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => registered.Clear();

        /// <summary>Olvida lo registrado por MountSpawn (pruebas); vuelven los valores de serie.</summary>
        public static void ResetRegistered() => registered.Clear();

        /// <summary>Definicion de la montura (registrada, de serie o generica). Nunca null.</summary>
        public static MountDefinition Get(string id)
        {
            string key = MountUnlocks.Sanitize(id);
            if (registered.TryGetValue(key, out MountDefinition found))
                return found;
            return BuiltIn(key);
        }

        /// <summary>
        /// Sustituye nombre, vuelo y velocidad de una montura (lo llama MountSpawn). Para las
        /// voladoras, una velocidad de 18 o mas es la de vuelo; menor, la de tierra.
        /// </summary>
        public static MountDefinition Register(string id, string displayName, bool flies, float speed)
        {
            string key = MountUnlocks.Sanitize(id);
            if (string.IsNullOrEmpty(key))
                return BuiltIn("montura");
            MountDefinition definition = (registered.TryGetValue(key, out MountDefinition existing) ? existing : BuiltIn(key)).Clone();
            if (!string.IsNullOrEmpty(displayName))
                definition.Name = displayName;
            ApplyConfiguredSpeed(definition, flies, speed);
            registered[key] = definition;
            return definition;
        }

        /// <summary>Interpreta la velocidad configurada (logica pura, probada en EditMode).</summary>
        public static void ApplyConfiguredSpeed(MountDefinition definition, bool flies, float speed)
        {
            if (definition == null)
                return;
            definition.Flies = flies;
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
                speed = 0f;
            if (!flies)
            {
                if (speed > 0f)
                    definition.GroundSpeed = Mathf.Clamp(speed, MinGroundSpeed, MaxGroundSpeed);
                return;
            }
            if (speed >= MinFlySpeed)
                definition.FlySpeed = Mathf.Clamp(speed, MinFlySpeed, MaxFlySpeed);
            else if (speed > 0f)
                definition.GroundSpeed = Mathf.Clamp(speed, MinGroundSpeed, 16f);
            definition.FlySpeed = Mathf.Clamp(definition.FlySpeed, MinFlySpeed, MaxFlySpeed);
        }

        private static MountDefinition BuiltIn(string id)
        {
            switch (id)
            {
                case "lobo_bruma":
                    return new MountDefinition
                    {
                        Id = id, Name = "Lobo de bruma", GroundSpeed = 13.5f, Shape = MountShape.Wolf, Size = 0.9f,
                        BodyColor = new Color(0.5f, 0.55f, 0.63f), AccentColor = new Color(0.82f, 0.88f, 0.98f),
                        Description = "Ágil y silencioso, nacido de la niebla de los pantanos."
                    };
                case "leon_cumbres":
                    return new MountDefinition
                    {
                        Id = id, Name = "León de las cumbres", GroundSpeed = 14.5f, Shape = MountShape.Lion, Size = 1f,
                        BodyColor = new Color(0.78f, 0.6f, 0.35f), AccentColor = new Color(0.42f, 0.25f, 0.12f),
                        Description = "Señor de los riscos: salta y corre sin cansarse."
                    };
                case "corcel_infernal":
                    return new MountDefinition
                    {
                        Id = id, Name = "Corcel infernal", GroundSpeed = 16f, GallopMultiplier = 1.3f, Shape = MountShape.Horse,
                        Size = 1.1f, BodyColor = new Color(0.09f, 0.07f, 0.08f), AccentColor = new Color(1f, 0.38f, 0.1f),
                        Description = "El más rápido en tierra. Sus cascos dejan brasas."
                    };
                case "oso_acorazado":
                    return new MountDefinition
                    {
                        Id = id, Name = "Oso acorazado", GroundSpeed = 12f, GallopMultiplier = 1.28f, Shape = MountShape.Bear,
                        Size = 1.15f, BodyColor = new Color(0.32f, 0.22f, 0.15f), AccentColor = new Color(0.56f, 0.58f, 0.63f),
                        Description = "Lento pero imparable, cubierto de placas de hierro."
                    };
                case "grifo":
                    return new MountDefinition
                    {
                        Id = id, Name = "Grifo", Flies = true, GroundSpeed = 11.5f, FlySpeed = 25f, Shape = MountShape.Griffin,
                        Size = 1.1f, BodyColor = new Color(0.8f, 0.68f, 0.48f), AccentColor = new Color(0.42f, 0.3f, 0.18f),
                        Description = "Mitad águila, mitad león. Domina los cielos de Vhaldren."
                    };
                case "fenix_ceniza":
                    return new MountDefinition
                    {
                        Id = id, Name = "Fénix de ceniza", Flies = true, GroundSpeed = 10.5f, FlySpeed = 27f, Shape = MountShape.Phoenix,
                        Size = 1f, BodyColor = new Color(0.88f, 0.32f, 0.1f), AccentColor = new Color(1f, 0.78f, 0.28f),
                        Description = "Renace de sus cenizas. El vuelo más veloz."
                    };
            }
            return new MountDefinition
            {
                Id = string.IsNullOrEmpty(id) ? "montura" : id,
                Name = string.IsNullOrEmpty(id) ? "Montura" : PrettyName(id),
                Shape = MountShape.Horse
            };
        }

        private static string PrettyName(string id)
        {
            string text = id.Replace('_', ' ');
            return text.Length > 0 ? char.ToUpperInvariant(text[0]) + text.Substring(1) : "Montura";
        }
    }
}
