using UnityEngine;
using Vhaldren.Gameplay.Mounts;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Punto de entrada de exploracion y monturas. PrototypeGameSession lo llama una vez:
    /// <c>Vhaldren.Gameplay.Exploration.ExplorationBootstrap.Install(player, this);</c>
    /// Añade al jugador: aguante de viaje y carrera (Mayús), monturas (G / Ctrl),
    /// interaccion (E), region actual con titulos y ambiente, guardia del agua profunda y
    /// mapa (M) con minimapa. Sin <see cref="WorldAtlas"/> en la escena (salas de prueba)
    /// las partes del mapa quedan inactivas y nada falla. Llamarlo dos veces no duplica nada.
    /// </summary>
    public static class ExplorationBootstrap
    {
        public static void Install(Transform player, PrototypeGameSession session)
        {
            if (player == null || !Application.isPlaying)
                return;
            ExplorationContext.Player = player;
            if (session != null)
                ExplorationContext.Session = session;
            ExplorationSave.WorldKey = player.gameObject.scene.IsValid() ? player.gameObject.scene.name : "mundo";

            GameObject target = player.gameObject;
            Ensure<TravelStamina>(target);
            Ensure<PlayerSprint>(target);
            Ensure<MountController>(target);
            Ensure<PlayerInteractor>(target);
            Ensure<RegionTracker>(target);
            Ensure<DeepWaterGuard>(target);
            Ensure<WorldMapHud>(target);
        }

        private static T Ensure<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }
    }
}
