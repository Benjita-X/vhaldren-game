using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Datos de una intencion de movimiento para un paso de simulacion.
    /// No lee teclado, camara, entrada de red ni representa autoridad.
    /// Direcciones expresadas en espacio del mundo (plano XZ).
    /// </summary>
    public readonly struct PlayerMotionCommand
    {
        public readonly Vector3 MoveWorld;
        public readonly Vector3 FaceWorld;

        public PlayerMotionCommand(Vector3 moveWorld, Vector3 faceWorld)
        {
            MoveWorld = moveWorld;
            FaceWorld = faceWorld;
        }
    }
}
