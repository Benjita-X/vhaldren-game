using UnityEngine;

namespace Vhaldren.Gameplay.Survival
{
    public enum PlacementResult { Ok, NoSurface, TooFar, Blocked, MissingResources, NeedsWorkbench }

    /// <summary>Reglas de colocacion. Siempre explica por que no se puede construir.</summary>
    public static class BuildPlacement
    {
        public const float MaxDistance = 10f;
        private const float Margin = 0.06f;
        private static readonly Collider[] overlaps = new Collider[16];

        public static string Describe(PlacementResult result)
        {
            switch (result)
            {
                case PlacementResult.Ok: return "Clic izquierdo: construir";
                case PlacementResult.NoSurface: return "Apunta al suelo o a otra pieza";
                case PlacementResult.TooFar: return "Demasiado lejos";
                case PlacementResult.Blocked: return "Algo ocupa ese espacio";
                case PlacementResult.MissingResources: return "Faltan materiales";
                case PlacementResult.NeedsWorkbench: return "Necesitas un banco de trabajo cerca";
                default: return string.Empty;
            }
        }

        /// <summary>Caja que ocupa la pieza, algo reducida para que las piezas vecinas se toquen.</summary>
        public static void Footprint(BuildPieceDefinition piece, Vector3 basePosition, int rotationSteps,
            out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        {
            rotation = BuildGrid.Rotation(rotationSteps);
            Vector3 size = piece.Size;
            center = basePosition + Vector3.up * (size.y * 0.5f);
            // Las paredes que se cruzan en una esquina comparten unos centimetros: se
            // recortan sus extremos para que no se bloqueen entre si.
            float endMargin = piece.SnapsToEdge ? size.z * 0.5f + Margin : Margin;
            halfExtents = new Vector3(
                Mathf.Max(0.01f, size.x * 0.5f - endMargin),
                Mathf.Max(0.01f, size.y * 0.5f - Margin),
                Mathf.Max(0.01f, size.z * 0.5f - Margin));
        }

        public static bool IsBlocked(BuildPieceDefinition piece, Vector3 basePosition, int rotationSteps)
        {
            Footprint(piece, basePosition, rotationSteps, out Vector3 center, out Vector3 half, out Quaternion rotation);
            int count = Physics.OverlapBoxNonAlloc(center, half, overlaps, rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return count > 0;
        }

        public static PlacementResult Evaluate(BuildPieceDefinition piece, bool hasSurface, Vector3 basePosition,
            int rotationSteps, Vector3 builderPosition, SurvivalInventory inventory,
            bool creativeMode = false)
        {
            if (piece == null || !hasSurface)
                return PlacementResult.NoSurface;
            Vector3 planar = basePosition - builderPosition;
            planar.y = 0f;
            if (planar.sqrMagnitude > MaxDistance * MaxDistance)
                return PlacementResult.TooFar;
            if (!creativeMode)
            {
                if (piece.RequiresWorkbench &&
                    !BuiltPiece.AnyWithin(BuildFunction.Workbench, basePosition, BuildCatalog.WorkbenchRadius))
                    return PlacementResult.NeedsWorkbench;
                if (inventory == null || !inventory.CanAfford(piece.Cost))
                    return PlacementResult.MissingResources;
            }
            if (IsBlocked(piece, basePosition, rotationSteps))
                return PlacementResult.Blocked;
            return PlacementResult.Ok;
        }
    }
}
