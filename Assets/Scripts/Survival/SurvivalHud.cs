using System.Text;
using UnityEngine;

namespace Vhaldren.Gameplay.Survival
{
    /// <summary>
    /// Interfaz minima de supervivencia: materiales, catalogo de construccion con costes
    /// y motivo de rechazo, y aviso de recoleccion. Los textos se reconstruyen solo al
    /// cambiar algo, no en cada repintado de OnGUI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalHud : MonoBehaviour
    {
        private static readonly ResourceKind[] Shown =
            { ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Fiber, ResourceKind.Ore };

        private SurvivalInventory inventory;
        private PlayerBuilder builder;
        private PlayerGatherer gatherer;
        private SurvivalToolbelt tools;
        private readonly StringBuilder text = new StringBuilder(256);
        private string resourcesLine = string.Empty;
        private string catalog = string.Empty;
        private string gatherLine = string.Empty;
        private int catalogKey = -1;
        private int gatherKey = -1;
        private GUIStyle style;

        private void Awake()
        {
            inventory = GetComponent<SurvivalInventory>();
            builder = GetComponent<PlayerBuilder>();
            gatherer = GetComponent<PlayerGatherer>();
            tools = GetComponent<SurvivalToolbelt>();
        }

        private void OnEnable()
        {
            if (inventory != null)
                inventory.Changed += RefreshResources;
            RefreshResources();
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.Changed -= RefreshResources;
        }

        private void RefreshResources()
        {
            if (inventory == null)
                return;
            text.Clear();
            for (int i = 0; i < Shown.Length; i++)
            {
                if (i > 0)
                    text.Append("   ");
                text.Append(HarvestNode.ResourceName(Shown[i])).Append(' ').Append(inventory.Count(Shown[i]));
            }
            text.Append("   (").Append(inventory.TotalItems).Append('/').Append(inventory.Capacity).Append(')');
            resourcesLine = text.ToString();
            catalogKey = -1;
        }

        private void OnGUI()
        {
            if (inventory == null)
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true, wordWrap = true };

            GUI.Box(new Rect(10f, Screen.height - 36f, 620f, 26f),
                resourcesLine + (builder != null && !builder.IsBuilding ? "    B: construir   E: recolectar" : string.Empty), style);
            if (tools == null)
                tools = GetComponent<SurvivalToolbelt>();
            if (tools != null)
            {
                string crafting = !tools.OwnsImprovisedPick
                    ? "J: fabricar pico improvisado (3 madera + 2 fibra)"
                    : !tools.OwnsStonePick
                        ? "K: fabricar pico de piedra (3 madera + 5 piedra + 2 fibra)"
                        : "Equipo: pico de piedra - piedra y mineral disponibles";
                GUI.Box(new Rect(10f, Screen.height - 65f, 565f, 26f), crafting, style);
            }

            if (builder != null && builder.IsBuilding)
            {
                int key = builder.SelectedIndex * 1000 + (int)builder.LastResult * 10 + builder.RotationSteps +
                          (builder.FreePlacement ? 500000 : 0);
                if (key != catalogKey)
                {
                    catalogKey = key;
                    catalog = BuildCatalogText();
                }
                GUI.Box(new Rect(Screen.width - 330f, 60f, 320f, 330f), catalog, style);
            }

            if (gatherer != null && gatherer.Target != null)
            {
                HarvestNode node = gatherer.Target;
                int key = (int)node.Resource * 1000 + node.Remaining +
                    (gatherer.CanGatherTarget ? 100000 : 0);
                if (key != gatherKey)
                {
                    gatherKey = key;
                    gatherLine = gatherer.CanGatherTarget
                        ? "Mantén E: recolectar " + HarvestNode.ResourceName(node.Resource) +
                          " (" + node.Remaining + ")"
                        : "Necesitas " + SurvivalToolbelt.DisplayName(node.RequiredTool) +
                          " para extraer " + HarvestNode.ResourceName(node.Resource);
                }
                Rect rect = new Rect(Screen.width * 0.5f - 170f, Screen.height - 100f, 340f, 26f);
                GUI.Box(rect, gatherLine, style);
                if (gatherer.CanGatherTarget && gatherer.Progress01 > 0f)
                    GUI.Box(new Rect(rect.x, rect.yMax + 2f, rect.width * gatherer.Progress01, 6f), GUIContent.none);
            }
        }

        private string BuildCatalogText()
        {
            text.Clear();
            text.Append("<b>CONSTRUCCIÓN</b>  (B salir)\n");
            for (int i = 0; i < BuildCatalog.Pieces.Count; i++)
            {
                BuildPieceDefinition piece = BuildCatalog.Pieces[i];
                bool selected = i == builder.SelectedIndex;
                bool affordable = inventory.CanAfford(piece.Cost);
                text.Append(selected ? "<b>> " : "   ");
                text.Append(i < 9 ? (i + 1).ToString() : (i == 9 ? "0" : "-")).Append(' ');
                text.Append(affordable ? piece.DisplayName : "<color=#d88>" + piece.DisplayName + "</color>");
                text.Append("  <size=11>");
                for (int c = 0; c < piece.Cost.Count; c++)
                {
                    if (c > 0)
                        text.Append(", ");
                    text.Append(piece.Cost[c].Amount).Append(' ').Append(HarvestNode.ResourceName(piece.Cost[c].Kind));
                }
                if (piece.RequiresWorkbench)
                    text.Append(" · banco");
                text.Append("</size>");
                if (selected)
                    text.Append("</b>");
                text.Append('\n');
            }
            text.Append("\n<b>").Append(BuildPlacement.Describe(builder.LastResult)).Append("</b>\n");
            text.Append("R girar · Shift libre").Append(builder.FreePlacement ? " (activo)" : string.Empty);
            text.Append(" · G desmontar · Ctrl+Z deshacer");
            return text.ToString();
        }
    }
}
