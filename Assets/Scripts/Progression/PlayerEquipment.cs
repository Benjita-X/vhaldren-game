using System;
using System.Text;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Survival;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Progression
{
    /// <summary>
    /// Equipo del jugador: arma, armadura y talisman, de viajero a runico. U abre el
    /// panel de forja. Las calidades altas exigen un banco de trabajo cerca. Fabricar
    /// es atomico: o se gasta todo y se mejora, o no se gasta nada.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SurvivalInventory), typeof(CombatHealth))]
    public sealed class PlayerEquipment : MonoBehaviour
    {
        public enum UpgradeResult { Ok, MaxTier, NeedsWorkbench, MissingResources }

        private static PlayerEquipment local;

        private readonly int[] tiers = new int[3];
        private readonly StringBuilder text = new StringBuilder(256);
        private SurvivalInventory inventory;
        private CombatHealth health;
        private PlayerActionModifiers modifiers;
        private float baseMaxHealth;
        private bool panelOpen;
        private int panelLock;
        private string message = string.Empty;
        private float messageUntil;
        private GUIStyle style;

        public static PlayerEquipment Local => local;
        public event Action Changed;
        public int GearLevel => GearCatalog.GearLevel(tiers[0], tiers[1], tiers[2]);
        public float SpellPowerMultiplier => GearCatalog.TalismanSpellPower(tiers[(int)GearSlot.Talisman]);
        public bool PanelOpen => panelOpen;
        public string LastMessage => message;

        public int TierOf(GearSlot slot) => tiers[(int)slot];

        public string ItemName(GearSlot slot) =>
            GearCatalog.SlotName(slot) + " " + GearCatalog.Get(tiers[(int)slot]).Suffix;

        private void Awake()
        {
            inventory = GetComponent<SurvivalInventory>();
            health = GetComponent<CombatHealth>();
            modifiers = GetComponent<PlayerActionModifiers>();
            baseMaxHealth = health.MaximumHealth;
        }

        private void OnEnable() => local = this;

        private void OnDisable()
        {
            if (local == this)
                local = null;
            SetPanel(false);
        }

        /// <summary>Fabrica la siguiente calidad del hueco indicado.</summary>
        public UpgradeResult TryUpgrade(GearSlot slot)
        {
            int current = tiers[(int)slot];
            if (current >= GearCatalog.MaxTier)
                return Fail(UpgradeResult.MaxTier, GearCatalog.SlotName(slot) + ": ya es rúnica, la mejor calidad");
            GearTier next = GearCatalog.Get(current + 1);
            if (next.NeedsWorkbench &&
                !BuiltPiece.AnyWithin(BuildFunction.Workbench, transform.position, BuildCatalog.WorkbenchRadius))
                return Fail(UpgradeResult.NeedsWorkbench, "Necesitas un banco de trabajo cerca (constrúyelo con B)");
            if (!inventory.TrySpend(next.Cost))
                return Fail(UpgradeResult.MissingResources, "Faltan materiales: " + CostText(next));

            tiers[(int)slot] = current + 1;
            if (slot == GearSlot.Armor)
                health.SetMaximumHealth(baseMaxHealth + GearCatalog.ArmorHealthBonus(current + 1), false);
            Notify("Forjado: " + ItemName(slot) + " — nivel de equipo " + GearLevel);
            Changed?.Invoke();
            return UpgradeResult.Ok;
        }

        private UpgradeResult Fail(UpgradeResult result, string reason)
        {
            Notify(reason);
            return result;
        }

        public void Notify(string value)
        {
            message = value;
            messageUntil = Time.unscaledTime + 3f;
        }

        public static string CostText(GearTier tier)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < tier.Cost.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(tier.Cost[i].Amount).Append(' ').Append(HarvestNode.ResourceName(tier.Cost[i].Kind));
            }
            if (tier.NeedsWorkbench)
                builder.Append(" · banco");
            return builder.ToString();
        }

        public void SetPanel(bool open)
        {
            if (open == panelOpen)
                return;
            panelOpen = open;
            if (modifiers == null)
                modifiers = GetComponent<PlayerActionModifiers>();
            if (open && modifiers != null)
                panelLock = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack);
            else if (!open && modifiers != null && panelLock != 0)
            {
                modifiers.RemoveModifier(panelLock);
                panelLock = 0;
            }
        }

        private void Update()
        {
            if (Time.timeScale > 0f && TogglePressed())
                SetPanel(!panelOpen);
        }

        private void OnGUI()
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true, wordWrap = true };

            if (Time.unscaledTime < messageUntil && Event.current.type == EventType.Repaint)
                GUI.Box(new Rect(Screen.width * 0.5f - 230f, 250f, 460f, 26f), message, style);

            if (!panelOpen)
                return;
            float width = 470f;
            float x = (Screen.width - width) * 0.5f;
            float y = Mathf.Max(60f, Screen.height * 0.5f - 190f);
            GUI.Box(new Rect(x, y, width, 300f), "<b>FORJA DE EQUIPO</b>   (U cerrar)   Nivel de equipo: <b>" + GearLevel + "</b>", style);
            for (int i = 0; i < 3; i++)
            {
                GearSlot slot = (GearSlot)i;
                int tier = tiers[i];
                text.Clear();
                text.Append("<b>").Append(ItemName(slot)).Append("</b>  (nivel ").Append(GearCatalog.Get(tier).Level).Append(")\n");
                if (tier >= GearCatalog.MaxTier)
                {
                    text.Append("Calidad máxima");
                }
                else
                {
                    GearTier next = GearCatalog.Get(tier + 1);
                    bool affordable = inventory.CanAfford(next.Cost);
                    text.Append("Siguiente: ").Append(next.Suffix).Append(" (nivel ").Append(next.Level).Append(") — ")
                        .Append(affordable ? "<color=#8f8>" : "<color=#f99>").Append(CostText(next)).Append("</color>");
                }
                GUI.Box(new Rect(x + 10f, y + 32f + i * 78f, width - 130f, 70f), text.ToString(), style);
                GUI.enabled = tier < GearCatalog.MaxTier;
                if (GUI.Button(new Rect(x + width - 112f, y + 46f + i * 78f, 100f, 40f), "Forjar"))
                    TryUpgrade(slot);
                GUI.enabled = true;
            }
            GUI.Label(new Rect(x + 10f, y + 268f, width - 20f, 26f),
                "Armadura: +vida máxima · Talismán: +poder de hechizo · Botín: caza enemigos");
        }

        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.U);
#else
            return false;
#endif
        }
    }
}
