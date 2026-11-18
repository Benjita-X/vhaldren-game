using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Progression;
using Vhaldren.Gameplay.Races;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.UI;
using Vhaldren.Gameplay.World;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.NPC
{
    /// <summary>
    /// En el jugador: nombres y oficios flotando sobre los aldeanos cercanos, bocadillos,
    /// el aviso «E · Hablar» y el panel de diálogo (retrato de color, texto letra a letra y
    /// opciones con 1-4, W/S + E o clic). E solo abre el diálogo si no hay un recurso más
    /// cerca (recolectar sigue teniendo prioridad). Mientras habla, el jugador no se mueve
    /// ni ataca. Herrero → forja, mercader → comercio de caravanas, sanadora → cura entera,
    /// guía → marca un lugar con una columna de luz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcInteractor : MonoBehaviour
    {
        public const float LabelDistance = 16f;
        public const float CloseDistance = 5f;

        private enum Page { Main, Trade }

        private readonly List<NpcVillager> visible = new List<NpcVillager>(16);
        private readonly List<string> options = new List<string>(4);
        private NpcVillager focus;
        private NpcVillager partner;
        private Page page;
        private string text = string.Empty;
        private float textStartedAt;
        private int highlighted;
        private int pendingChoice = -1;
        private PlayerActionModifiers modifiers;
        private int lockToken;
        private PlayerGatherer gatherer;
        private PlayerBuilder builder;
        private CombatHealth health;
        private PlayerMotor motor;
        private float nextScan;
        private GUIStyle textStyle;
        private GUIStyle optionStyle;
        private GUIStyle optionActiveStyle;
        private Vector2 lastMouse;

        public NpcVillager Focus => focus;
        public NpcVillager Partner => partner;
        public bool DialogueOpen => partner != null;
        public string CurrentText => text;
        public IReadOnlyList<string> Options => options;
        public bool TextComplete => NpcDialogue.VisibleCharacters(Time.unscaledTime - textStartedAt, text.Length) >= text.Length;

        private bool IsLucifer
        {
            get
            {
                PlayerRace race = GetComponent<PlayerRace>();
                return race != null && race.Race == RaceId.Lucifer;
            }
        }

        private void Lookup()
        {
            if (modifiers == null) modifiers = GetComponent<PlayerActionModifiers>();
            if (gatherer == null) gatherer = GetComponent<PlayerGatherer>();
            if (builder == null) builder = GetComponent<PlayerBuilder>();
            if (health == null) health = GetComponent<CombatHealth>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
        }

        private void OnDisable()
        {
            Close(false);
            NpcDialogue.ClaimsInteractKey = false;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
                return;
            Lookup();
            if (partner != null)
            {
                HandleDialogue();
                return;
            }
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.15f;
                Scan();
            }
            bool canTalk = focus != null && CanTalk() && !ResourceCloser(focus);
            NpcDialogue.ClaimsInteractKey = canTalk;
            if (canTalk && GameInput.Down(GameAction.Gather))
                Open(focus);
        }

        private void Scan()
        {
            visible.Clear();
            focus = null;
            float best = NpcVillager.TalkRange;
            IReadOnlyList<NpcVillager> all = NpcVillager.All;
            for (int i = 0; i < all.Count; i++)
            {
                NpcVillager npc = all[i];
                if (npc == null || npc.IsAsleep)
                    continue;
                float d = Planar(npc.transform.position);
                if (d <= LabelDistance)
                    visible.Add(npc);
                if (d <= best)
                {
                    best = d;
                    focus = npc;
                }
            }
        }

        private float Planar(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private bool CanTalk() =>
            (health == null || !health.IsDefeated) &&
            (builder == null || !builder.IsBuilding) &&
            (modifiers == null || modifiers.AllowsAttack);

        private bool ResourceCloser(NpcVillager npc)
        {
            if (gatherer == null || gatherer.Target == null)
                return false;
            return Planar(gatherer.Target.transform.position) < Planar(npc.transform.position);
        }

        // ── Diálogo ────────────────────────────────────────────────────

        /// <summary>Abre el diálogo con un aldeano (E, o desde pruebas y capturas).</summary>
        public bool Open(NpcVillager npc)
        {
            if (npc == null || partner != null)
                return false;
            Lookup();
            partner = npc;
            npc.BeginTalk();
            Vector3 toward = npc.transform.position - transform.position;
            toward.y = 0f;
            if (motor != null && toward.sqrMagnitude > 0.01f)
                motor.SetFacing(toward);
            if (modifiers != null && lockToken == 0)
                lockToken = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Movement |
                                                  PlayerActionModifiers.LockedAction.Attack |
                                                  PlayerActionModifiers.LockedAction.Dash |
                                                  PlayerActionModifiers.LockedAction.Facing);
            NpcDialogue.IsOpen = true;
            NpcDialogue.ClaimsInteractKey = true;
            ShowMain(NpcDialogue.Greeting(npc.Role, IsLucifer, Random.Range(0, 1000) + npc.Conversations));
            return true;
        }

        /// <summary>Cierra el diálogo; con despedida, el aldeano la dice en su bocadillo.</summary>
        public void Close(bool farewell)
        {
            if (partner == null)
                return;
            NpcVillager npc = partner;
            partner = null;
            npc.EndTalk();
            if (farewell)
                npc.Say(NpcDialogue.Farewell(npc.Role, IsLucifer), 3f);
            if (lockToken != 0 && modifiers != null)
                modifiers.RemoveModifier(lockToken);
            lockToken = 0;
            pendingChoice = -1;
            NpcDialogue.IsOpen = false;
        }

        private void HandleDialogue()
        {
            if (partner == null || !partner.isActiveAndEnabled || (health != null && health.IsDefeated) ||
                (builder != null && builder.IsBuilding) || Planar(partner.transform.position) > CloseDistance)
            {
                Close(false);
                return;
            }
            int count = options.Count;
            int chosen = pendingChoice;
            pendingChoice = -1;
            bool confirm = GameInput.Down(GameAction.Gather);
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) chosen = 0;
                else if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) chosen = 1;
                else if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) chosen = 2;
                else if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) chosen = 3;
                if (count > 0 && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame))
                    highlighted = (highlighted - 1 + count) % count;
                if (count > 0 && (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame))
                    highlighted = (highlighted + 1) % count;
                if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
                    confirm = true;
            }
#endif
            if (confirm && chosen < 0)
            {
                if (!TextComplete)
                    textStartedAt = -1000f;   // primera pulsación: mostrar todo el texto
                else
                    chosen = highlighted;
            }
            if (chosen >= 0 && chosen < count)
                Choose(chosen);
        }

        /// <summary>Elige una opción del diálogo (0 = primera).</summary>
        public void Choose(int index)
        {
            if (partner == null || index < 0 || index >= options.Count)
                return;
            if (page == Page.Trade)
            {
                if (index < VillageTradeCatalog.Offers.Count)
                    Trade(index);
                else
                    ShowMain("¿Algo más?");
                return;
            }
            switch (index)
            {
                case 0:
                    ShowMain(NpcDialogue.AboutPlace(partner.Role, partner.Lines, IsLucifer, partner.LoreTurn++));
                    break;
                case 1:
                    RoleAction();
                    break;
                default:
                    Close(true);
                    break;
            }
        }

        private void ShowMain(string line)
        {
            page = Page.Main;
            options.Clear();
            options.Add(NpcDialogue.AskPlace);
            options.Add(NpcDialogue.RoleOption(partner != null ? partner.Role : NpcRole.Villager));
            options.Add(NpcDialogue.Goodbye);
            Show(line);
        }

        private void Show(string line)
        {
            text = line ?? string.Empty;
            textStartedAt = Time.unscaledTime;
            highlighted = Mathf.Clamp(highlighted, 0, Mathf.Max(0, options.Count - 1));
        }

        private void RoleAction()
        {
            switch (partner.Role)
            {
                case NpcRole.Smith: OpenForge(); break;
                case NpcRole.Merchant: ShowTrade("Esto es lo que traen las caravanas. Elige con cabeza:"); break;
                case NpcRole.Healer: Heal(); break;
                case NpcRole.Guide: MarkPlace(); break;
                default: ShowMain(WorkText()); break;
            }
        }

        private void OpenForge()
        {
            PlayerEquipment equipment = GetComponent<PlayerEquipment>();
            if (equipment == null)
                equipment = PlayerEquipment.Local;
            if (equipment == null)
            {
                ShowMain("La forja aún está fría. Vuelve cuando tengas material que trabajar.");
                return;
            }
            NpcVillager smith = partner;
            Close(false);
            smith.Say(NpcDialogue.SmithReply, 3.5f);
            equipment.SetPanel(true);
        }

        private void ShowTrade(string intro)
        {
            VillageQuestJournal journal = GetComponent<VillageQuestJournal>();
            if (journal == null)
            {
                ShowMain(NpcDialogue.NoCaravan);
                return;
            }
            page = Page.Trade;
            RefreshOffers(journal);
            highlighted = 0;
            Show(intro);
        }

        private void RefreshOffers(VillageQuestJournal journal)
        {
            options.Clear();
            for (int i = 0; i < VillageTradeCatalog.Offers.Count; i++)
            {
                VillageTradeOffer offer = VillageTradeCatalog.Offers[i];
                int price = VillageTradeCatalog.Price(offer, journal.CurrentEvent, journal.Reputation(offer.Faction));
                string label = offer.DisplayName + ": " + price + " " + HarvestNode.ResourceName(offer.PriceResource) +
                               " → " + offer.RewardAmount + " " + HarvestNode.ResourceName(offer.Reward) +
                               " (quedan " + journal.TradeStock(i) + ")";
                if (journal.Reputation(offer.Faction) < offer.ReputationNeeded)
                    label += " · reputación " + offer.ReputationNeeded;
                options.Add(label);
            }
            options.Add("Volver");
        }

        private void Trade(int index)
        {
            VillageQuestJournal journal = GetComponent<VillageQuestJournal>();
            if (journal == null)
            {
                ShowMain(NpcDialogue.NoCaravan);
                return;
            }
            VillageTradeResult result = journal.TryTrade(index);
            string reply;
            switch (result)
            {
                case VillageTradeResult.Purchased: reply = "¡Trato hecho! Un placer robarte... digo, servirte."; break;
                case VillageTradeResult.ReputationTooLow: reply = "Aún no te conozco lo bastante. Ayuda al Cruce y hablamos."; break;
                case VillageTradeResult.NoStock: reply = "Se me ha acabado. Espera a la próxima caravana."; break;
                case VillageTradeResult.InventoryFull: reply = "Tu mochila no aguanta ni una pluma más."; break;
                default: reply = "Te falta material para ese trato. Vuelve con más."; break;
            }
            if (result == VillageTradeResult.Purchased)
                HudNotices.Post("Comercio: " + VillageTradeCatalog.Offers[index].DisplayName, HudNotices.Reward);
            RefreshOffers(journal);
            Show(reply);
        }

        private void Heal()
        {
            if (health == null)
            {
                ShowMain(NpcDialogue.HealerHealthy);
                return;
            }
            StatusEffects status = GetComponent<StatusEffects>();
            bool afflicted = false;
            if (status != null)
                for (int k = 0; k <= (int)StatusKind.Exposed && !afflicted; k++)
                    afflicted = status.Has((StatusKind)k);
            bool hurt = health.CurrentHealth < health.MaximumHealth - 0.5f;
            if (!hurt && !afflicted)
            {
                ShowMain(NpcDialogue.HealerHealthy);
                return;
            }
            if (Time.time < partner.HealReadyAt)
            {
                ShowMain(NpcDialogue.HealerTired + " (" + Mathf.CeilToInt(partner.HealReadyAt - Time.time) + " s)");
                return;
            }
            float healed = health.Heal(health.MaximumHealth);
            if (status != null)
                status.ClearAll();
            partner.HealReadyAt = Time.time + NpcVillager.HealCooldown;
            Color light = new Color(0.75f, 1f, 0.7f);
            SpellImpactFlash.Spawn(transform.position + Vector3.up, light, 1f, 0.45f, 1.6f);
            VfxHost host = VfxHost.Get();
            if (host != null)
                host.Glints(transform.position + Vector3.up * 0.8f, 0.5f, new Color(0.8f, 1f, 0.75f, 1f), 26, 0.18f, 1f,
                    Vector3.up * 1.3f);
            ShowMain(NpcDialogue.HealerDone + (healed >= 1f ? " Recuperas " + Mathf.RoundToInt(healed) + " PV." : ""));
            HudNotices.Post(partner.DisplayName + " te cura" + (healed >= 1f ? ": +" + Mathf.RoundToInt(healed) + " PV" : ""),
                new Color(0.6f, 1f, 0.65f));
        }

        private void MarkPlace()
        {
            Vector3 target;
            string place;
            Color tint = new Color(1f, 0.82f, 0.45f);
            if (partner.HasGuideTarget)
            {
                target = partner.GuideTarget;
                place = partner.GuidePlace;
            }
            else
            {
                PrototypeEnemy boss = FindBoss();
                VillageNoticeboard board = boss == null ? FindAnyObjectByType<VillageNoticeboard>() : null;
                if (boss != null)
                {
                    target = boss.transform.position;
                    place = "el altar del Caballero de sangre";
                    tint = new Color(0.95f, 0.15f, 0.12f);
                }
                else if (board != null)
                {
                    target = board.transform.position;
                    place = "el tablón del Cruce";
                }
                else
                {
                    ShowMain("Por aquí ya no queda nada que marcar... de momento. Explora y vuelve.");
                    return;
                }
            }
            NpcBeacon.Mark(target, tint, 45f);
            Vector3 delta = target - transform.position;
            ShowMain(NpcDialogue.GuideReply(place, delta));
            delta.y = 0f;
            HudNotices.Post("Marcado: " + place + " · " + Mathf.RoundToInt(delta.magnitude) + " m hacia " +
                            NpcDialogue.DirectionName(delta), tint);
        }

        private static PrototypeEnemy FindBoss()
        {
            foreach (PrototypeEnemy enemy in FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None))
                if (enemy != null && enemy.IsBoss && !enemy.IsDefeated)
                    return enemy;
            return null;
        }

        private string WorkText()
        {
            VillageQuestJournal journal = GetComponent<VillageQuestJournal>();
            VillageQuest quest = journal != null ? journal.Current : null;
            if (quest == null)
                return NpcDialogue.WorkFallback;
            switch (journal.State)
            {
                case VillageQuestState.Available:
                    return "El consejo del Cruce busca a alguien para «" + quest.Name + "». " + quest.Description +
                           " Acepta el encargo en el tablón (" + GameInput.Label(GameAction.Noticeboard) + ").";
                case VillageQuestState.Active:
                    return "Ya estás con «" + quest.Name + "»: llevas " + Mathf.Min(quest.Required, journal.Progress) +
                           " de " + quest.Required + ". ¡Ánimo!";
                case VillageQuestState.ReadyToClaim:
                    return "¡Has cumplido «" + quest.Name + "»! Pasa por el tablón a cobrar tu recompensa.";
                default:
                    return NpcDialogue.WorkFallback;
            }
        }

        // ── Interfaz ───────────────────────────────────────────────────

        private void OnGUI()
        {
            if (HudMode.Hidden || (partner == null && visible.Count == 0))
                return;
            GUI.depth = -15;
            bool repaint = Event.current.type == EventType.Repaint;
            MenuStyle.Begin();
            if (repaint)
                DrawLabels();
            if (partner != null)
                DrawPanel(repaint);
            MenuStyle.End();
        }

        private void DrawLabels()
        {
            Camera view = Camera.main;
            if (view == null)
                return;
            float scale = MenuStyle.Scale;
            for (int i = 0; i < visible.Count; i++)
            {
                NpcVillager npc = visible[i];
                if (npc == null || npc == partner)
                    continue;
                Vector3 screen = view.WorldToScreenPoint(npc.transform.position + Vector3.up * 2.25f);
                if (screen.z <= 0f)
                    continue;
                float x = screen.x / scale;
                float y = (Screen.height - screen.y) / scale;
                float d = Planar(npc.transform.position);
                float alpha = Mathf.Clamp01((LabelDistance - d) / 4f);
                Color gold = HudSkin.GoldBright;
                HudSkin.Label(new Rect(x - 130f, y - 38f, 260f, 22f), npc.DisplayName, 15,
                    new Color(gold.r, gold.g, gold.b, alpha), TextAnchor.MiddleCenter, true);
                HudSkin.Label(new Rect(x - 130f, y - 19f, 260f, 18f), Capitalize(npc.Trade), 12,
                    new Color(HudSkin.TextDim.r, HudSkin.TextDim.g, HudSkin.TextDim.b, alpha));
                if (npc == focus)
                {
                    string prompt = GameInput.Label(GameAction.Gather) + " · Hablar";
                    float pw = HudSkin.TextWidth(prompt, 13, true) + 26f;
                    Rect chip = new Rect(x - pw * 0.5f, y + 2f, pw, 20f);
                    HudSkin.SoftPanel(chip, 0.75f);
                    HudSkin.Label(chip, prompt, 13, HudSkin.GoldBright, TextAnchor.MiddleCenter, true);
                }
                string bark = npc.CurrentBark;
                if (!string.IsNullOrEmpty(bark))
                {
                    float bw = Mathf.Min(420f, HudSkin.TextWidth(bark, 14) + 30f);
                    Rect bubble = new Rect(x - bw * 0.5f, y - 68f, bw, 26f);
                    HudSkin.SoftPanel(bubble, 0.8f);
                    HudSkin.Label(bubble, bark, 14, HudSkin.Text);
                }
            }
        }

        private void DrawPanel(bool repaint)
        {
            EnsureStyles();
            float w = MenuStyle.Width, h = MenuStyle.Height;
            float pw = Mathf.Min(860f, w - 80f), ph = 300f;
            Rect panel = new Rect((w - pw) * 0.5f, h - ph - 168f, pw, ph);
            MenuStyle.FramedPanel(panel);
            Rect portrait = new Rect(panel.x + 22f, panel.y + 22f, 112f, 136f);
            float tx = portrait.xMax + 20f;
            float tw = panel.xMax - tx - 22f;
            if (repaint)
            {
                DrawPortrait(portrait, partner);
                HudSkin.Label(new Rect(tx, panel.y + 14f, tw, 28f), partner.DisplayName, 22, HudSkin.GoldBright,
                    TextAnchor.MiddleLeft, true);
                HudSkin.Label(new Rect(tx, panel.y + 40f, tw, 18f),
                    Capitalize(partner.Trade) + " · " + NpcDialogue.RoleTitle(partner.Role), 13, HudSkin.TextDim, TextAnchor.MiddleLeft);
                HudSkin.FadeLine(new Rect(tx, panel.y + 62f, tw, 1.5f), HudSkin.Gold);
                int shown = NpcDialogue.VisibleCharacters(Time.unscaledTime - textStartedAt, text.Length);
                GUI.Label(new Rect(tx, panel.y + 70f, tw, 96f), text.Substring(0, shown), textStyle);
                string hint = (options.Count > 0 ? "1-" + options.Count + ", W/S o clic · " : "") +
                              GameInput.Label(GameAction.Gather) + ": " + (shown < text.Length ? "ver todo" : "elegir");
                HudSkin.Label(new Rect(panel.x + 22f, panel.yMax - 30f, portrait.width + 10f, 20f), hint, 11,
                    HudSkin.TextDim, TextAnchor.MiddleLeft);
            }
            float oy = panel.y + 170f;
            // El ratón resalta la opción que pisa (solo cuando se mueve: el teclado manda si no).
            Vector2 mouse = Event.current.mousePosition;
            bool mouseMoved = repaint && (mouse - lastMouse).sqrMagnitude > 1f;
            if (mouseMoved)
                lastMouse = mouse;
            for (int i = 0; i < options.Count; i++)
            {
                Rect r = new Rect(tx, oy + i * 31f, tw, 28f);
                if (mouseMoved && r.Contains(mouse))
                    highlighted = i;
                if (GUI.Button(r, (i + 1) + ".  " + options[i], i == highlighted ? optionActiveStyle : optionStyle))
                {
                    highlighted = i;
                    pendingChoice = i;
                }
                if (repaint && i == highlighted)
                    HudSkin.Diamond(new Vector2(r.x - 9f, r.center.y), 9f, HudSkin.GoldBright);
            }
        }

        private static void DrawPortrait(Rect r, NpcVillager npc)
        {
            Color c = npc.Tint;
            HudSkin.Fill(r, new Color(0.03f, 0.02f, 0.03f, 0.95f));
            Rect inner = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f);
            HudSkin.Fill(inner, new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 1f));
            HudSkin.Fill(new Rect(inner.x, inner.y, inner.width, inner.height * 0.55f), new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 0.9f));
            string initial = string.IsNullOrEmpty(npc.DisplayName) ? "?" : npc.DisplayName.Substring(0, 1).ToUpperInvariant();
            HudSkin.Label(new Rect(inner.x, inner.y + 6f, inner.width, inner.height - 46f), initial, 58,
                new Color(1f, 0.96f, 0.9f, 0.95f), TextAnchor.MiddleCenter, true);
            HudSkin.Icon(new Rect(r.center.x - 18f, r.yMax - 46f, 36f, 36f), c, GlyphFor(npc.Role), 0f, true, true, 0.3f);
            HudSkin.Border(r, 2f, HudSkin.Gold);
        }

        private static HudSkin.Glyph GlyphFor(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Smith: return HudSkin.Glyph.Sword;
                case NpcRole.Merchant: return HudSkin.Glyph.Rune1;
                case NpcRole.Healer: return HudSkin.Glyph.Potion;
                case NpcRole.Guide: return HudSkin.Glyph.School;
                default: return HudSkin.Glyph.Rune2;
            }
        }

        private void EnsureStyles()
        {
            if (textStyle == null)
            {
                textStyle = new GUIStyle(MenuStyle.Body) { richText = false, wordWrap = true, fontSize = 17 };
                textStyle.normal.textColor = HudSkin.Text;
            }
            if (optionStyle == null)
            {
                optionStyle = new GUIStyle(MenuStyle.Tab) { alignment = TextAnchor.MiddleLeft, fontSize = 15, richText = false, clipping = TextClipping.Clip };
                optionStyle.padding = new RectOffset(14, 10, 2, 2);
                optionActiveStyle = new GUIStyle(MenuStyle.TabActive) { alignment = TextAnchor.MiddleLeft, fontSize = 15, richText = false, clipping = TextClipping.Clip };
                optionActiveStyle.padding = new RectOffset(14, 10, 2, 2);
            }
        }

        private static string Capitalize(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
}
