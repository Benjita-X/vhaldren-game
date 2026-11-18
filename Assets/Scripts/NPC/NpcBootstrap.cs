using UnityEngine;

namespace Vhaldren.Gameplay.NPC
{
    /// <summary>
    /// Punto de entrada de los aldeanos: deja en el jugador el <see cref="NpcInteractor"/>
    /// (hablar con E, nombres flotantes, bocadillos y el panel de diálogo). Idempotente.
    /// Si nadie lo llama, el primer aldeano que encuentra al jugador lo instala igualmente.
    /// </summary>
    public static class NpcBootstrap
    {
        public static NpcInteractor Install(Transform player)
        {
            if (player == null)
                return null;
            NpcInteractor interactor = player.GetComponent<NpcInteractor>();
            if (interactor == null)
                interactor = player.gameObject.AddComponent<NpcInteractor>();
            return interactor;
        }
    }
}
