using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Gameplay.Survival
{
    /// <summary>
    /// Modo construccion del jugador. B entra y sale; 1-0 eligen pieza; R gira; Shift
    /// coloca libre (25 cm); clic construye; G desmonta lo apuntado; Ctrl+Z deshace.
    /// Desmontar y deshacer devuelven todo el material: experimentar no debe castigar.
    /// Mientras se construye no se ataca, pero si se puede mover y esquivar.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SurvivalInventory))]
    public sealed class PlayerBuilder : MonoBehaviour
    {
        private const int UndoLimit = 30;
        private static readonly List<PlayerBuilder> all = new List<PlayerBuilder>();

        [SerializeField] private Camera viewCamera;

        private readonly List<BuiltPiece> undoStack = new List<BuiltPiece>();
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private SurvivalInventory inventory;
        private CombatHealth health;
        private PlayerActionModifiers modifiers;
        private Behaviour[] combatBehaviours;
        private bool[] combatWasEnabled;
        private int attackLock;
        private GameObject preview;
        private int previewIndex = -1;
        private int previewRotation = -1;

        public static IReadOnlyList<PlayerBuilder> All => all;
        public bool IsBuilding { get; private set; }
        public int SelectedIndex { get; private set; }
        public int RotationSteps { get; private set; }
        public bool FreePlacement { get; private set; }
        public PlacementResult LastResult { get; private set; } = PlacementResult.NoSurface;
        public BuildPieceDefinition Selected => BuildCatalog.Pieces[SelectedIndex];
        public SurvivalInventory Inventory => inventory;
        public CombatHealth Health => health;
        public int UndoCount => undoStack.Count;

        private void Awake()
        {
            inventory = GetComponent<SurvivalInventory>();
            health = GetComponent<CombatHealth>();
            modifiers = GetComponent<PlayerActionModifiers>();
            // Ataques que se pausan al construir. Se buscan por tipo para no acoplarse.
            combatBehaviours = new Behaviour[]
            {
                GetComponent<PlayerCombatAbilities>(),
                GetComponent<PlayerDefensiveInput>()
            };
            combatWasEnabled = new bool[combatBehaviours.Length];
        }

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            SetBuildMode(false);
        }

        private void Update()
        {
            if (SurvivalKeys.ToggleBuild)
                SetBuildMode(!IsBuilding);
            if (!IsBuilding)
                return;
            if (health != null && health.IsDefeated)
            {
                SetBuildMode(false);
                return;
            }

            int slot = SurvivalKeys.PieceSlotPressed();
            if (slot >= 0 && slot < BuildCatalog.Pieces.Count)
                SelectedIndex = slot;
            if (SurvivalKeys.Rotate)
                RotationSteps = BuildGrid.NormalizeRotation(RotationSteps + 1);
            FreePlacement = SurvivalKeys.FreePlacementHeld;
            if (SurvivalKeys.Undo)
                Undo();

            bool hasSurface = TryPointerHit(out RaycastHit hit);
            Vector3 basePosition = hasSurface
                ? BuildGrid.Snap(hit.point, Selected, RotationSteps, FreePlacement)
                : Vector3.zero;
            if (hasSurface && hit.collider.GetComponentInParent<BuiltPiece>() is BuiltPiece target &&
                SurvivalKeys.Dismantle)
            {
                Dismantle(target);
                return;
            }

            LastResult = BuildPlacement.Evaluate(Selected, hasSurface, basePosition, RotationSteps,
                transform.position, inventory);
            UpdatePreview(hasSurface, basePosition);

            if (SurvivalKeys.PlacePressed && LastResult == PlacementResult.Ok)
                TryPlace(SelectedIndex, basePosition, RotationSteps, out _);
        }

        public void SetBuildMode(bool building)
        {
            if (building == IsBuilding)
                return;
            IsBuilding = building;
            if (building)
            {
                // Algunos sistemas de combate se instalan en runtime desde
                // PrototypeGameSession.OnEnable, despues de PlayerBuilder.Awake.
                // Resolver los componentes al entrar, no antes de crearlos.
                combatBehaviours[0] = GetComponent<PlayerCombatAbilities>();
                combatBehaviours[1] = GetComponent<PlayerDefensiveInput>();
                if (modifiers == null)
                    modifiers = GetComponent<PlayerActionModifiers>();
                if (modifiers != null)
                    attackLock = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack);
                for (int i = 0; i < combatBehaviours.Length; i++)
                {
                    combatWasEnabled[i] = combatBehaviours[i] != null && combatBehaviours[i].enabled;
                    if (combatBehaviours[i] != null)
                        combatBehaviours[i].enabled = false;
                }
            }
            else
            {
                if (modifiers != null && attackLock != 0)
                    modifiers.RemoveModifier(attackLock);
                attackLock = 0;
                for (int i = 0; i < combatBehaviours.Length; i++)
                    if (combatBehaviours[i] != null)
                        combatBehaviours[i].enabled = combatWasEnabled[i];
                DestroyPreview();
            }
        }

        /// <summary>Coloca una pieza si las reglas lo permiten. Solo cobra si se coloca.</summary>
        public BuiltPiece TryPlace(int pieceIndex, Vector3 basePosition, int rotationSteps, out PlacementResult result)
        {
            if (pieceIndex < 0 || pieceIndex >= BuildCatalog.Pieces.Count)
            {
                result = PlacementResult.NoSurface;
                return null;
            }
            BuildPieceDefinition piece = BuildCatalog.Pieces[pieceIndex];
            result = BuildPlacement.Evaluate(piece, true, basePosition, rotationSteps, transform.position, inventory);
            if (result != PlacementResult.Ok)
                return null;
            if (!inventory.TrySpend(piece.Cost))
            {
                result = PlacementResult.MissingResources;
                return null;
            }

            GameObject built = null;
            try
            {
                built = BuildPieceFactory.Create(piece, basePosition, rotationSteps, false);
                BuiltPiece builtPiece = built != null ? built.GetComponent<BuiltPiece>() : null;
                if (builtPiece == null)
                    throw new System.InvalidOperationException("La pieza no tiene BuiltPiece.");
                builtPiece.MarkMaterialsPaid();
                undoStack.Add(builtPiece);
                if (undoStack.Count > UndoLimit)
                    undoStack.RemoveAt(0);
                Physics.SyncTransforms();
                return builtPiece;
            }
            catch (System.Exception exception)
            {
                if (built != null)
                {
                    built.SetActive(false);
                    Destroy(built);
                }
                // Si falla la fabrica visual, devolver el cobro completo.
                if (!inventory.TryRefund(piece.Cost))
                    Debug.LogError("[Vhaldren] Error critico reembolsando una pieza fallida.");
                result = PlacementResult.Blocked;
                Debug.LogException(exception);
                return null;
            }
        }

        /// <summary>Desmonta una pieza y devuelve todo su coste.</summary>
        public bool Dismantle(BuiltPiece piece)
        {
            if (piece == null || inventory == null || !piece.TryClaimRemoval())
                return false;

            BuildPieceDefinition definition = piece.Definition;
            if (piece.MaterialsPaid)
            {
                if (definition == null)
                {
                    piece.CancelRemoval();
                    return false;
                }

                int lostStorage = definition.Function == BuildFunction.Chest
                    ? SurvivalInventory.ChestCapacity : 0;
                // La pieza continua en pie si no hay espacio para devolver
                // recursos. Nunca perder material ni reembolsar dos veces.
                if (!inventory.TryRefund(definition.Cost, 1f, lostStorage))
                {
                    piece.CancelRemoval();
                    return false;
                }
            }

            undoStack.Remove(piece);
            piece.gameObject.SetActive(false);
            Destroy(piece.gameObject);
            return true;
        }

        /// <summary>Deshace la ultima pieza colocada que siga en pie.</summary>
        public bool Undo()
        {
            while (undoStack.Count > 0)
            {
                BuiltPiece last = undoStack[undoStack.Count - 1];
                undoStack.RemoveAt(undoStack.Count - 1);
                if (last != null)
                    return Dismantle(last);
            }
            return false;
        }

        private bool TryPointerHit(out RaycastHit best)
        {
            best = default;
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera == null)
                return false;
            Ray ray = viewCamera.ScreenPointToRay(SurvivalKeys.Pointer);
            int count = Physics.RaycastNonAlloc(ray, hits, 200f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= bestDistance)
                    continue;
                bestDistance = hits[i].distance;
                best = hits[i];
            }
            return bestDistance < float.PositiveInfinity;
        }

        private void UpdatePreview(bool visible, Vector3 basePosition)
        {
            if (preview != null && (previewIndex != SelectedIndex || previewRotation != RotationSteps))
                DestroyPreview();
            if (preview == null)
            {
                preview = BuildPieceFactory.Create(Selected, basePosition, RotationSteps, true);
                previewIndex = SelectedIndex;
                previewRotation = RotationSteps;
            }
            preview.SetActive(visible);
            preview.transform.SetPositionAndRotation(basePosition, BuildGrid.Rotation(RotationSteps));
            BuildPieceFactory.SetPreviewValid(preview, LastResult == PlacementResult.Ok);
        }

        private void DestroyPreview()
        {
            if (preview != null)
                Destroy(preview);
            preview = null;
            previewIndex = -1;
            previewRotation = -1;
        }
    }

    /// <summary>Recoleccion: mantener E cerca de un arbol, roca, mineral o planta.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SurvivalInventory))]
    public sealed class PlayerGatherer : MonoBehaviour
    {
        public const float Range = 2.8f;

        private SurvivalInventory inventory;
        private SurvivalToolbelt tools;
        private PlayerAnimationDriver animations;
        private CombatHealth health;
        private PlayerBuilder builder;
        private float progress;
        private HarvestNode current;
        private bool gatheringStarted;

        public HarvestNode Target => current;
        public SurvivalToolbelt Tools => tools;
        public float Progress01 => current != null ? Mathf.Clamp01(progress / current.GatherSeconds) : 0f;
        public bool CanGatherTarget => current != null &&
            (current.RequiredTool == GatheringTool.Hands ||
             (tools != null && tools.CanHarvest(current)));
        public CraftingResult LastCraftingResult { get; private set; } = CraftingResult.InvalidTool;

        private void Awake()
        {
            inventory = GetComponent<SurvivalInventory>();
            tools = GetComponent<SurvivalToolbelt>();
            if (tools == null)
                tools = gameObject.AddComponent<SurvivalToolbelt>();
            animations = GetComponent<PlayerAnimationDriver>();
            health = GetComponent<CombatHealth>();
            builder = GetComponent<PlayerBuilder>();
        }

        private void Update()
        {
            if (health != null && health.IsDefeated)
            {
                Tick(false, Time.deltaTime);
                return;
            }

            if (SurvivalKeys.CraftImprovisedPick)
                TryCraftTool(GatheringTool.ImprovisedPick);
            if (SurvivalKeys.CraftStonePick)
                TryCraftTool(GatheringTool.StonePick);

            Tick(SurvivalKeys.GatherHeld && (builder == null || !builder.IsBuilding),
                Time.deltaTime);
        }

        public CraftingResult TryCraftTool(GatheringTool tool)
        {
            if (health != null && health.IsDefeated)
                return CraftingResult.InvalidTool;
            LastCraftingResult = tools != null ? tools.TryCraft(tool) :
                CraftingResult.MissingMaterials;
            return LastCraftingResult;
        }

        /// <summary>
        /// Cada ciclo de E ejecuta una accion visible y un solo pago de recursos.
        /// El punto de extraccion permanece ligado al objetivo inicial. No se
        /// permite extraer piedra/mineral sin fabricar primero la herramienta.
        /// </summary>
        public int Tick(bool held, float deltaTime)
        {
            HarvestNode nearest = HarvestNode.Nearest(transform.position, Range);
            if (nearest != current)
            {
                current = nearest;
                progress = 0f;
                gatheringStarted = false;
            }

            if (current == null || !held || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime) || deltaTime <= 0f ||
                !CanGatherTarget)
            {
                progress = 0f;
                gatheringStarted = false;
                return 0;
            }

            if (!gatheringStarted)
            {
                gatheringStarted = true;
                if (animations == null)
                    animations = GetComponent<PlayerAnimationDriver>();
                if (animations != null)
                    animations.TriggerHarvest(current.Resource);
            }

            progress += deltaTime;
            if (progress < current.GatherSeconds)
                return 0;

            progress = 0f;
            gatheringStarted = false;
            return current.Harvest(inventory);
        }
    }
}
