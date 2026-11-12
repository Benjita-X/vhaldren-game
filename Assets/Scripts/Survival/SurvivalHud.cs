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

        private static Color ResourceColor(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Wood: return new Color(0.66f, 0.45f, 0.25f);
                case ResourceKind.Stone: return new Color(0.62f, 0.62f, 0.64f);
                case ResourceKind.Fiber: return new Color(0.48f, 0.7f, 0.34f);
                case ResourceKind.Ore: return new Color(0.45f, 0.62f, 0.85f);
                default: return new Color(0.8f, 0.8f, 0.8f);
            }
        }

        private void OnGUI()
        {
            if (inventory == null)
                return;
            if (!Vhaldren.Gameplay.UI.HudMode.Debug)
            {
                if (Event.current.type == EventType.Repaint && !Vhaldren.Gameplay.UI.HudMode.Hidden)
                    DrawMinimal();
                return;
            }
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

        /// <summary>
        /// Fila mínima abajo a la izquierda (sin cajas). En modo construcción se amplía: nombres
        /// de los materiales, capacidad, fabricación de picos y el catálogo con el marco del HUD.
        /// </summary>
        private void DrawMinimal()
        {
            Vhaldren.Gameplay.UI.MenuStyle.Begin();
            float w = Vhaldren.Gameplay.UI.MenuStyle.Width, h = Vhaldren.Gameplay.UI.MenuStyle.Height;
            bool building = builder != null && builder.IsBuilding;
            float barLeft = w * 0.5f - Vhaldren.Gameplay.UI.ActionBarHud.NaturalWidth *
                Vhaldren.Gameplay.UI.ActionBarHud.BarScale(w) * 0.5f;
            float maxRow = Mathf.Max(160f, barLeft - 36f);
            float x = 20f, y = h - 30f;
            float rowStart = x;
            for (int i = 0; i < Shown.Length; i++)
            {
                ResourceKind kind = Shown[i];
                string label = building ? HarvestNode.ResourceName(kind) + " " + inventory.Count(kind) : inventory.Count(kind).ToString();
                float lw = Vhaldren.Gameplay.UI.HudSkin.TextWidth(label, 16) + 30f;
                if (x + lw - rowStart > maxRow && x > rowStart)
                {
                    x = rowStart;
                    y -= 24f;
                }
                Vhaldren.Gameplay.UI.HudSkin.Fill(new Rect(x, y + 3f, 13f, 13f), new Color(0f, 0f, 0f, 0.85f));
                Vhaldren.Gameplay.UI.HudSkin.Fill(new Rect(x + 1.5f, y + 4.5f, 10f, 10f), ResourceColor(kind));
                Vhaldren.Gameplay.UI.HudSkin.Label(new Rect(x + 18f, y, lw, 19f), label, 16, Vhaldren.Gameplay.UI.HudSkin.Text, TextAnchor.MiddleLeft);
                x += lw;
            }
            string capacity = inventory.TotalItems + "/" + inventory.Capacity;
            Vhaldren.Gameplay.UI.HudSkin.Label(new Rect(x + 2f, y, 120f, 19f), capacity, 14, Vhaldren.Gameplay.UI.HudSkin.TextDim, TextAnchor.MiddleLeft);

            if (building)
            {
                if (tools == null)
                    tools = GetComponent<SurvivalToolbelt>();
                if (tools != null)
                {
                    string crafting = !tools.OwnsImprovisedPick
                        ? Vhaldren.Gameplay.Settings.GameInput.Label(Vhaldren.Gameplay.Settings.GameAction.CraftPick1) + ": pico improvisado (3 madera + 2 fibra)"
                        : !tools.OwnsStonePick
                            ? Vhaldren.Gameplay.Settings.GameInput.Label(Vhaldren.Gameplay.Settings.GameAction.CraftPick2) + ": pico de piedra (3 madera + 5 piedra + 2 fibra)"
                            : "Pico de piedra: piedra y mineral disponibles";
                    Vhaldren.Gameplay.UI.HudSkin.Label(new Rect(rowStart, y - 24f, 560f, 19f), crafting, 15, Vhaldren.Gameplay.UI.HudSkin.TextDim, TextAnchor.MiddleLeft);
                }
                int key = builder.SelectedIndex * 1000 + (int)builder.LastResult * 10 + builder.RotationSteps +
                          (builder.FreePlacement ? 500000 : 0);
                if (key != catalogKey)
                {
                    catalogKey = key;
                    catalog = BuildCatalogText();
                }
                Rect panel = new Rect(w - 392f, 96f, 372f, 262f);
                Vhaldren.Gameplay.UI.MenuStyle.FramedPanel(panel);
                GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 32f, panel.height - 20f), catalog, Vhaldren.Gameplay.UI.MenuStyle.Small);
            }
            Vhaldren.Gameplay.UI.MenuStyle.End();
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
