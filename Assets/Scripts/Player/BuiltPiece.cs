using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Pieza construida por el jugador. Guarda su receta para devolver materiales al
    /// desmontarla. Las hogueras se registran para que SurvivalNeeds las encuentre sin
    /// buscar en toda la escena.
    /// </summary>
    public sealed class BuiltPiece : MonoBehaviour
    {
        private static readonly List<BuiltPiece> Campfires = new List<BuiltPiece>();

        public int PieceIndex { get; private set; }
        public int WoodCost { get; private set; }
        public int StoneCost { get; private set; }
        public bool IsCampfire { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Campfires.Clear();

        public void Initialize(int pieceIndex, int wood, int stone, bool campfire)
        {
            PieceIndex = pieceIndex;
            WoodCost = wood;
            StoneCost = stone;
            IsCampfire = campfire;
            if (campfire && isActiveAndEnabled && !Campfires.Contains(this))
                Campfires.Add(this);
        }

        private void OnEnable()
        {
            if (IsCampfire && !Campfires.Contains(this))
                Campfires.Add(this);
        }

        private void OnDisable() => Campfires.Remove(this);

        public static bool IsNearCampfire(Vector3 position, float radius)
        {
            float sq = radius * radius;
            foreach (BuiltPiece fire in Campfires)
            {
                if (fire == null)
                    continue;
                Vector3 d = fire.transform.position - position;
                d.y *= 0.5f;
                if (d.sqrMagnitude <= sq)
                    return true;
            }
            return false;
        }
    }
}
