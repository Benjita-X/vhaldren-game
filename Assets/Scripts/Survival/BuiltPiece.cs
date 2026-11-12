using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Survival
{
    /// <summary>Pieza ya colocada: sabe que es y cuanto costo, para desmontarla o deshacerla.</summary>
    [DisallowMultipleComponent]
    public sealed class BuiltPiece : MonoBehaviour
    {
        private static readonly List<BuiltPiece> all = new List<BuiltPiece>();

        public static IReadOnlyList<BuiltPiece> All => all;

        [SerializeField] private string pieceId;
        [SerializeField] private int rotationSteps;
        [SerializeField] private bool materialsPaid;
        private bool removalClaimed;

        public string PieceId => pieceId;
        public int RotationSteps => rotationSteps;
        public BuildPieceDefinition Definition => BuildCatalog.Find(pieceId);
        public bool MaterialsPaid => materialsPaid;
        public bool IsBeingRemoved => removalClaimed;

        // Solo una pieza que realmente costo recursos puede devolverlos.
        // La vista previa y los objetos de escenario no generan materiales.
        public void MarkMaterialsPaid() => materialsPaid = true;

        public bool TryClaimRemoval()
        {
            if (removalClaimed || !gameObject.activeInHierarchy)
                return false;
            removalClaimed = true;
            return true;
        }

        public void CancelRemoval() => removalClaimed = false;

        public void Configure(BuildPieceDefinition definition, int rotation)
        {
            pieceId = definition != null ? definition.Id : string.Empty;
            rotationSteps = BuildGrid.NormalizeRotation(rotation);
        }

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable() => all.Remove(this);

        /// <summary>Hay una pieza con esa funcion a menos de radius metros.</summary>
        public static bool AnyWithin(BuildFunction function, Vector3 position, float radius)
        {
            float squared = radius * radius;
            for (int i = 0; i < all.Count; i++)
            {
                BuiltPiece piece = all[i];
                BuildPieceDefinition definition = piece != null ? piece.Definition : null;
                if (definition != null && definition.Function == function &&
                    (piece.transform.position - position).sqrMagnitude <= squared)
                    return true;
            }
            return false;
        }

        public static int CountWithFunction(BuildFunction function)
        {
            int count = 0;
            for (int i = 0; i < all.Count; i++)
            {
                BuildPieceDefinition definition = all[i] != null ? all[i].Definition : null;
                if (definition != null && definition.Function == function)
                    count++;
            }
            return count;
        }
    }
}
