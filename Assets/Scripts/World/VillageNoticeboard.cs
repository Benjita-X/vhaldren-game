using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Survival;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Primer punto de encuentro de pueblo: tablero fisico 3D y acciones de
    /// misiones. Se instala junto al Cruce en runtime para que la escena 015
    /// existente no requiera regenerarse ni modificar prefabs ajenos.
    /// Los NPC modelados y la arquitectura definitiva vendran despues.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillageNoticeboard : MonoBehaviour
    {
        [SerializeField, Min(2f)] private float interactionDistance = 11f;
        [SerializeField] private Transform player;

        private VillageQuestJournal journal;
        private PlayerActionModifiers actionModifiers;
        private PlayerBuilder builder;
        private int attackLock;
        private int tradeIndex;
        private bool open;
        private string feedback = "";
        private float feedbackUntil;
        private GUIStyle labelStyle;
        private readonly List<Material> createdMaterials = new List<Material>();

        public bool MenuOpen => open;

        public void BindPlayer(Transform target)
        {
            player = target;
            journal = target != null ? target.GetComponent<VillageQuestJournal>() : null;
            actionModifiers = target != null
                ? target.GetComponent<PlayerActionModifiers>() : null;
            builder = target != null ? target.GetComponent<PlayerBuilder>() : null;
        }

        private void Awake()
        {
            if (player == null)
            {
                GameObject found = GameObject.Find("PlayerRoot");
                if (found != null)
                    player = found.transform;
            }
            if (player != null)
                BindPlayer(player);

            if (transform.childCount == 0)
                CreateVisuals();
        }

        private void Update()
        {
            if (player == null || journal == null)
                return;

            if (!PlayerNearby || (builder != null && builder.IsBuilding))
            {
                SetOpen(false);
                return;
            }

            if (OpenPressed())
                SetOpen(!open);
            if (!open)
                return;
            if (ActionPressed())
                TryInteract();
            if (CycleTradePressed())
                tradeIndex = (tradeIndex + 1) % VillageTradeCatalog.Offers.Count;
            if (BuyPressed())
                TryBuyCurrentOffer();
        }

        private void SetOpen(bool requested)
        {
            if (requested == open)
                return;
            open = requested;
            if (actionModifiers == null && player != null)
                actionModifiers = player.GetComponent<PlayerActionModifiers>();
            if (open && actionModifiers != null)
                attackLock = actionModifiers.AddModifier(
                    PlayerActionModifiers.LockedAction.Attack);
            else if (!open && actionModifiers != null && attackLock != 0)
            {
                actionModifiers.RemoveModifier(attackLock);
                attackLock = 0;
            }
        }

        private void OnDisable() => SetOpen(false);

        private void OnDestroy()
        {
            foreach (Material generated in createdMaterials)
            {
                if (generated == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(generated);
                else
                    DestroyImmediate(generated);
            }
            createdMaterials.Clear();
        }

        public VillageTradeResult TryBuyCurrentOffer()
        {
            if (journal == null || !PlayerNearby)
                return VillageTradeResult.NoOffer;
            VillageTradeResult result = journal.TryTrade(tradeIndex);
            switch (result)
            {
                case VillageTradeResult.Purchased:
                    feedback = "Trato cerrado. Materiales intercambiados.";
                    break;
                case VillageTradeResult.ReputationTooLow:
                    feedback = "Aún no confían en ti: ayuda al pueblo.";
                    break;
                case VillageTradeResult.NoStock:
                    feedback = "Sin existencias. Espera a la próxima caravana.";
                    break;
                case VillageTradeResult.InventoryFull:
                    feedback = "La mochila no tiene espacio.";
                    break;
                default:
                    feedback = "No tienes suficientes materiales para el trato.";
                    break;
            }
            feedbackUntil = Time.time + 2.7f;
            return result;
        }

        public bool PlayerNearby => player != null &&
            (player.position - transform.position).sqrMagnitude <=
            interactionDistance * interactionDistance;

        public bool TryInteract()
        {
            if (journal == null || !PlayerNearby)
                return false;

            bool accepted = journal.State == VillageQuestState.Available
                ? journal.TryAccept() : journal.TryClaim();
            feedback = accepted
                ? "Misión actualizada. El pueblo recuerda tus acciones."
                : journal.State == VillageQuestState.Active
                    ? "Objetivo pendiente: revisa la cantidad necesaria."
                    : "No hay recompensa disponible o falta espacio en mochila.";
            feedbackUntil = Time.time + 2.5f;
            return accepted;
        }

        private void OnGUI()
        {
            if (!PlayerNearby || journal == null)
                return;

            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 13,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    richText = true
                };

            Rect label = new Rect(Mathf.Max(10f, Screen.width - 385f),
                Mathf.Max(210f, Screen.height - 180f), 370f, 42f);
            GUI.Box(label, "PUESTO DEL CRUCE  |  N: tablón de misiones", labelStyle);
            if (!open)
                return;

            VillageQuest quest = journal.Current;
            string mission = quest == null ? "Cadena inicial completada" :
                "<b>" + quest.Name + "</b>\n" + quest.Description +
                "\nProgreso: " + Mathf.Min(quest.Required, journal.Progress) +
                " / " + quest.Required;
            string action = quest == null ? "Vuelve a explorar Vhaldren" :
                journal.State == VillageQuestState.Available
                    ? "Y: aceptar misión"
                    : journal.State == VillageQuestState.ReadyToClaim
                        ? "Y: entregar / reclamar recompensa"
                        : "Investiga y completa el encargo";
            string eventText = journal.EventTitle;
            string text = "<b>CONSEJO DEL CRUCE</b>\n" +
                "Reputación local: " +
                journal.Reputation(SettlementCulture.Mixed) +
                " | Arcanistas: " +
                journal.Reputation(SettlementCulture.Arcane) +
                "\nEvento actual: " + eventText + "\n\n" +
                mission + "\n\n" + action;

            VillageTradeOffer offer = VillageTradeCatalog.Offers[tradeIndex];
            int price = VillageTradeCatalog.Price(offer,
                journal.CurrentEvent, journal.Reputation(offer.Faction));
            text += "\n\n<b>MERCADO DEL CRUCE</b> (O: siguiente, P: comprar)" +
                "\n" + offer.DisplayName + " | Precio: " + price + " " +
                HarvestNode.ResourceName(offer.PriceResource) +
                " → +" + offer.RewardAmount + " " +
                HarvestNode.ResourceName(offer.Reward) +
                " | Stock: " + journal.TradeStock(tradeIndex);
            if (journal.Reputation(offer.Faction) < offer.ReputationNeeded)
                text += " | Requiere reputación " + offer.ReputationNeeded;
            text += "\nN: cerrar";
            if (Time.time < feedbackUntil)
                text += "\n\n" + feedback;

            GUI.Box(new Rect(Mathf.Max(10f, Screen.width - 385f),
                Mathf.Max(10f, Screen.height - 530f), 370f, 370f),
                text, labelStyle);
        }

        private void CreateVisuals()
        {
            // Solo prototipo de señal física. Pueblos completos y NPC con
            // malla/rig se añadirán con sus propios assets licenciados.
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Soporte de tablón";
            post.transform.SetParent(transform, false);
            post.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            post.transform.localScale = new Vector3(0.22f, 2.4f, 0.25f);
            Tint(post, new Color(0.30f, 0.19f, 0.11f));

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Tablón de misiones";
            board.transform.SetParent(transform, false);
            board.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            board.transform.localScale = new Vector3(2.7f, 1.15f, 0.20f);
            Tint(board, new Color(0.45f, 0.30f, 0.16f));

            GameObject placard = new GameObject("Letrero");
            placard.transform.SetParent(transform, false);
            placard.transform.localPosition = new Vector3(0f, 2.15f, 0.13f);
            TextMesh text = placard.AddComponent<TextMesh>();
            text.text = "PUESTO DEL CRUCE\nTABLON DE ENCARGOS";
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            text.characterSize = 0.07f;
            text.fontSize = 64;
            text.color = new Color(0.91f, 0.84f, 0.68f);
        }

        private void Tint(GameObject part, Color color)
        {
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer == null)
                return;
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                return;
            Material material = new Material(shader) { color = color };
            createdMaterials.Add(material);
            renderer.sharedMaterial = material;
        }

        private static bool CycleTradePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.Offer);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.O);
#else
            return false;
#endif
        }

        private static bool BuyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.Buy);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.P);
#else
            return false;
#endif
        }

        private static bool OpenPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.Noticeboard);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.N);
#else
            return false;
#endif
        }

        private static bool ActionPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.Accept);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Y);
#else
            return false;
#endif
        }
    }
}
