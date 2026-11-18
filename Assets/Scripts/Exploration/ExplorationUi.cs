using UnityEngine;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Estado compartido entre exploracion y monturas sin acoplarlos: mapa abierto, panel
    /// de lectura, jugador montado o volando. Lo escriben sus duenos y lo leen los demas.
    /// </summary>
    public static class ExplorationUi
    {
        /// <summary>Mapa del mundo a pantalla completa abierto (M).</summary>
        public static bool MapOpen { get; set; }
        /// <summary>Panel de una piedra de saber abierto.</summary>
        public static bool ReadingLore { get; set; }
        /// <summary>El jugador va en montura (cualquier estado distinto de a pie).</summary>
        public static bool PlayerMounted { get; set; }
        /// <summary>El jugador vuela (o aterriza) en una montura alada.</summary>
        public static bool PlayerFlying { get; set; }
        /// <summary>Una piedra de viaje activada pide abrir el mapa para viajar (lo atiende WorldMapHud).</summary>
        public static bool MapOpenRequested { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MapOpen = false;
            ReadingLore = false;
            PlayerMounted = false;
            PlayerFlying = false;
            MapOpenRequested = false;
        }
    }
}
