using UnityEngine;

namespace Vhaldren.Gameplay.Survival
{
    /// <summary>Encaje en rejilla, puro y sin estado para poder probarlo aislado.</summary>
    public static class BuildGrid
    {
        public static int NormalizeRotation(int steps) => ((steps % 4) + 4) % 4;

        public static Quaternion Rotation(int steps) => Quaternion.Euler(0f, 90f * NormalizeRotation(steps), 0f);

        /// <summary>
        /// Posicion de la base de la pieza. Con encaje: las piezas normales van al centro
        /// de la celda y las de borde (paredes) sobre la linea de la celda a lo largo de su
        /// largo. En modo libre se redondea a 25 cm. La altura siempre es la del punto apuntado.
        /// </summary>
        public static Vector3 Snap(Vector3 point, BuildPieceDefinition piece, int rotationSteps, bool free)
        {
            if (!IsFinite(point))
                return Vector3.zero;
            if (free || piece == null)
            {
                float f = BuildCatalog.FreeGridSize;
                return new Vector3(Round(point.x, f), point.y, Round(point.z, f));
            }

            float g = BuildCatalog.GridSize;
            float centerX = Mathf.Floor(point.x / g) * g + g * 0.5f;
            float centerZ = Mathf.Floor(point.z / g) * g + g * 0.5f;
            if (!piece.SnapsToEdge)
                return new Vector3(centerX, point.y, centerZ);

            // Rotacion 0/2: el largo va en X, la pared vive en una linea Z de la rejilla.
            bool alongX = NormalizeRotation(rotationSteps) % 2 == 0;
            return alongX
                ? new Vector3(centerX, point.y, Round(point.z, g))
                : new Vector3(Round(point.x, g), point.y, centerZ);
        }

        private static float Round(float value, float step) => Mathf.Round(value / step) * step;

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
}
