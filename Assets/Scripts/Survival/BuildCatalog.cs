using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Survival
{
    public enum BuildFunction { None, Campfire, Bed, Chest, Workbench }

    /// <summary>Forma visual de una pieza; la logica de colocacion solo usa Size.</summary>
    public enum BuildShape { Box, DoorFrame, Ramp, Campfire, Spikes }

    /// <summary>
    /// Datos inmutables de una pieza construible. Separados de la colocacion, el
    /// inventario y el guardado para poder anadir piezas sin tocar el sistema.
    /// </summary>
    public sealed class BuildPieceDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        /// <summary>Tamano en metros (ancho X, alto Y, fondo Z) sin rotar.</summary>
        public Vector3 Size { get; }
        public BuildShape Shape { get; }
        public BuildFunction Function { get; }
        /// <summary>Las paredes encajan en los bordes de la celda, no en su centro.</summary>
        public bool SnapsToEdge { get; }
        public bool RequiresWorkbench { get; }
        public Color Tint { get; }
        public IReadOnlyList<ResourceIngredient> Cost { get; }

        public BuildPieceDefinition(string id, string displayName, Vector3 size, BuildShape shape,
            BuildFunction function, bool snapsToEdge, bool requiresWorkbench, Color tint,
            params ResourceIngredient[] cost)
        {
            Id = id;
            DisplayName = displayName;
            Size = size;
            Shape = shape;
            Function = function;
            SnapsToEdge = snapsToEdge;
            RequiresWorkbench = requiresWorkbench;
            Tint = tint;
            Cost = Array.AsReadOnly(cost ?? Array.Empty<ResourceIngredient>());
        }
    }

    /// <summary>Catalogo inicial: 10 piezas (teclas 1-0) que deben funcionar bien antes de anadir cien.</summary>
    public static class BuildCatalog
    {
        public const float GridSize = 2f;
        public const float FreeGridSize = 0.25f;
        public const float WorkbenchRadius = 12f;

        private static readonly Color Wood = new Color(0.42f, 0.29f, 0.18f);
        private static readonly Color DarkWood = new Color(0.30f, 0.20f, 0.13f);
        private static readonly Color Stone = new Color(0.40f, 0.39f, 0.42f);

        private static ResourceIngredient R(ResourceKind kind, int amount) => new ResourceIngredient(kind, amount);

        public static readonly IReadOnlyList<BuildPieceDefinition> Pieces = Array.AsReadOnly(new[]
        {
            new BuildPieceDefinition("cimiento", "Cimiento de piedra", new Vector3(2f, 0.3f, 2f), BuildShape.Box,
                BuildFunction.None, false, false, Stone, R(ResourceKind.Stone, 4), R(ResourceKind.Wood, 1)),
            new BuildPieceDefinition("suelo", "Suelo o techo de madera", new Vector3(2f, 0.15f, 2f), BuildShape.Box,
                BuildFunction.None, false, false, Wood, R(ResourceKind.Wood, 3)),
            new BuildPieceDefinition("pared", "Pared de madera", new Vector3(2f, 2.5f, 0.25f), BuildShape.Box,
                BuildFunction.None, true, false, Wood, R(ResourceKind.Wood, 4)),
            new BuildPieceDefinition("puerta", "Marco de puerta", new Vector3(2f, 2.5f, 0.25f), BuildShape.DoorFrame,
                BuildFunction.None, true, true, DarkWood, R(ResourceKind.Wood, 5), R(ResourceKind.Fiber, 2)),
            new BuildPieceDefinition("escalera", "Escalera", new Vector3(2f, 2.5f, 4f), BuildShape.Ramp,
                BuildFunction.None, false, true, Wood, R(ResourceKind.Wood, 6)),
            new BuildPieceDefinition("fogata", "Fogata", new Vector3(1.2f, 0.5f, 1.2f), BuildShape.Campfire,
                BuildFunction.Campfire, false, false, Stone, R(ResourceKind.Stone, 5), R(ResourceKind.Wood, 3)),
            new BuildPieceDefinition("banco", "Banco de trabajo", new Vector3(1.8f, 1f, 0.9f), BuildShape.Box,
                BuildFunction.Workbench, false, false, DarkWood, R(ResourceKind.Wood, 10), R(ResourceKind.Stone, 4)),
            new BuildPieceDefinition("cofre", "Cofre", new Vector3(1.2f, 0.8f, 0.8f), BuildShape.Box,
                BuildFunction.Chest, false, false, Wood, R(ResourceKind.Wood, 8), R(ResourceKind.Fiber, 2)),
            new BuildPieceDefinition("cama", "Cama de pieles", new Vector3(1.2f, 0.5f, 2.2f), BuildShape.Box,
                BuildFunction.Bed, false, true, new Color(0.45f, 0.20f, 0.18f), R(ResourceKind.Wood, 6), R(ResourceKind.Fiber, 6)),
            new BuildPieceDefinition("estacas", "Barricada de estacas", new Vector3(2f, 1.4f, 0.6f), BuildShape.Spikes,
                BuildFunction.None, true, false, DarkWood, R(ResourceKind.Wood, 6)),
        });

        public static BuildPieceDefinition Find(string id)
        {
            for (int i = 0; i < Pieces.Count; i++)
                if (Pieces[i].Id == id)
                    return Pieces[i];
            return null;
        }
    }
}
