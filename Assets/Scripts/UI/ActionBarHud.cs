using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Races;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Barra de acción única abajo en el centro (≤ 45 % del ancho): orbe de vida con la
    /// resistencia en arco; a la izquierda ballesta, espada, pulso, escarcha y la poción
    /// junto al orbe; a la derecha la escuela activa y los hechizos 1-2-3 y el poder de
    /// raza. Encima, una línea mínima con la escuela y la esencia, y los avisos de
    /// contexto (absorber, recolectar, tablón). Solo lee datos públicos de los sistemas.
    /// F3 (reasignable) alterna los recuadros antiguos de depuración.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActionBarHud : MonoBehaviour
    {
        public enum SlotId { Crossbow, Sword, Pulse, Frost, Potion, School, Spell1, Spell2, Spell3, Race }

        public struct SlotState
        {
            public SlotId Id;
            public string Name;
            public string Key;
            public Color Tint;
            public HudSkin.Glyph Glyph;
            public float Remaining;
            public float Total;
            public float Cooldown01;
            public bool Ready;
            public bool Usable;
            public int Charges;
            public int MaxCharges;
        }

        // Medidas en el lienzo virtual de 900 px de alto.
        public const float IconSize = 50f;
        public const float IconGap = 7f;
        public const float SmallIcon = 40f;
        public const float OrbDiameter = 96f;
        public const float FramePad = 18f;
        public const float MaxWidthFraction = 0.45f;
        private const float OrbGap = 12f;
        private const float GroupGap = 10f;
        private const float BottomMargin = 12f;

        private static readonly Color CrossbowTint = new Color(0.72f, 0.5f, 0.27f);
        private static readonly Color SwordTint = new Color(0.62f, 0.66f, 0.74f);
        private static readonly Color PulseTint = new Color(0.52f, 0.46f, 0.95f);
        private static readonly Color FrostTint = new Color(0.42f, 0.78f, 0.98f);
        private static readonly Color PotionTint = new Color(0.78f, 0.12f, 0.18f);
        private static readonly Color HealthLiquid = new Color(0.66f, 0.05f, 0.09f);
        private static readonly Color StaminaColor = new Color(0.96f, 0.78f, 0.36f);

        private readonly SlotState[] slots = new SlotState[10];
        private readonly float[] readySince = new float[10];
        private readonly bool[] wasReady = new bool[10];

        private CombatHealth health;
        private CombatDefense defense;
        private LocalPlayerShooter shooter;
        private PlayerCombatAbilities abilities;
        private PlayerSpellbook spellbook;
        private PlayerRace race;
        private PrototypeGameSession session;
        private EssenceReservoir essence;
        private CombatState combat;
        private ArcaneBarrier barrier;
        private PlayerActionModifiers modifiers;
        private PlayerFeeding feeding;
        private PlayerGatherer gatherer;
        private PlayerBuilder builder;
        private VillageNoticeboard noticeboard;
        private float nextLookup;

        public SlotState Slot(SlotId id) => slots[(int)id];

        public void Bind(PrototypeGameSession owner) => session = owner;

        private void Awake()
        {
            for (int i = 0; i < readySince.Length; i++)
                readySince[i] = -10f;
            Lookup();
        }

        private void Lookup()
        {
            if (health == null) health = GetComponent<CombatHealth>();
            if (defense == null) defense = GetComponent<CombatDefense>();
            if (shooter == null) shooter = GetComponent<LocalPlayerShooter>();
            if (abilities == null) abilities = GetComponent<PlayerCombatAbilities>();
            if (spellbook == null) spellbook = GetComponent<PlayerSpellbook>();
            if (race == null) race = GetComponent<PlayerRace>();
            if (essence == null) essence = GetComponent<EssenceReservoir>();
            if (combat == null) combat = GetComponent<CombatState>();
            if (barrier == null) barrier = GetComponent<ArcaneBarrier>();
            if (modifiers == null) modifiers = GetComponent<PlayerActionModifiers>();
            if (feeding == null) feeding = GetComponent<PlayerFeeding>();
            if (gatherer == null) gatherer = GetComponent<PlayerGatherer>();
            if (builder == null) builder = GetComponent<PlayerBuilder>();
            if (session == null) session = FindAnyObjectByType<PrototypeGameSession>();
            if (noticeboard == null) noticeboard = FindAnyObjectByType<VillageNoticeboard>();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 0.5f;
                Lookup();
            }
            if (GameInput.Down(GameAction.DebugHud))
            {
                HudMode.Debug = !HudMode.Debug;
                HudNotices.Post(HudMode.Debug ? "Recuadros de depuración: sí" : "Recuadros de depuración: no");
            }
            Refresh();
        }

        /// <summary>Fracción de recarga que falta (1 recién usada, 0 lista). Robusta ante datos raros.</summary>
        public static float CooldownFraction(float remaining, float total)
        {
            if (float.IsNaN(remaining) || remaining <= 0f || float.IsNaN(total) || total <= 0f)
                return 0f;
            return Mathf.Clamp01(remaining / total);
        }

        /// <summary>Lee todos los sistemas y rellena el estado de los diez huecos.</summary>
        public void Refresh()
        {
            bool canAct = modifiers == null || modifiers.AllowsAttack;
            float stamina = defense != null ? defense.Stamina : 999f;

            float arrowLeft = shooter != null ? shooter.ArrowCooldownRemaining : 0f;
            Set(SlotId.Crossbow, "Ballesta", "Clic", CrossbowTint, HudSkin.Glyph.Crossbow,
                arrowLeft, shooter != null ? shooter.ArrowCooldown : 1f, canAct, 0, 0);

            if (abilities != null)
            {
                Set(SlotId.Sword, "Espada", GameInput.Label(GameAction.Sword), SwordTint, HudSkin.Glyph.Sword,
                    abilities.SwordCooldownRemaining, abilities.SwordCooldown, canAct && stamina >= abilities.SwordStaminaCost, 0, 0);
                Set(SlotId.Pulse, "Pulso", GameInput.Label(GameAction.RunePulse), PulseTint, HudSkin.Glyph.Pulse,
                    abilities.RuneCooldownRemaining, abilities.RuneCooldown, canAct && stamina >= abilities.RuneStaminaCost, 0, 0);
                Set(SlotId.Frost, "Escarcha", GameInput.Label(GameAction.Frost), FrostTint, HudSkin.Glyph.Frost,
                    abilities.FrostCooldownRemaining, abilities.FrostCooldown, canAct && stamina >= abilities.FrostStaminaCost, 0, 0);
            }
            else
            {
                Set(SlotId.Sword, "Espada", GameInput.Label(GameAction.Sword), SwordTint, HudSkin.Glyph.Sword, 0f, 1f, false, 0, 0);
                Set(SlotId.Pulse, "Pulso", GameInput.Label(GameAction.RunePulse), PulseTint, HudSkin.Glyph.Pulse, 0f, 1f, false, 0, 0);
                Set(SlotId.Frost, "Escarcha", GameInput.Label(GameAction.Frost), FrostTint, HudSkin.Glyph.Frost, 0f, 1f, false, 0, 0);
            }

            bool potionUseful = health != null && health.CurrentHealth < health.MaximumHealth - 0.01f;
            Set(SlotId.Potion, "Poción", GameInput.Label(GameAction.Potion), PotionTint, HudSkin.Glyph.Potion,
                session != null ? session.PotionCooldownRemaining : 0f, session != null ? session.PotionCooldown : 1f,
                potionUseful, 0, 0);

            SpellSchool school = spellbook != null ? spellbook.School : SpellSchool.Pyromancy;
            Color schoolTint = SpellCatalog.SchoolColor(school);
            Set(SlotId.School, SpellCatalog.SchoolName(school), GameInput.Label(GameAction.NextSchool), schoolTint,
                HudSkin.Glyph.School, 0f, 1f, spellbook != null && spellbook.UnlockedCount > 1, 0, 0);

            for (int i = 0; i < SpellCatalog.SpellsPerSchool; i++)
            {
                SpellDefinition spell = SpellCatalog.Get(school, i);
                SlotId id = SlotId.Spell1 + i;
                GameAction key = GameAction.Spell1 + i;
                HudSkin.Glyph glyph = i == 0 ? HudSkin.Glyph.Rune1 : i == 1 ? HudSkin.Glyph.Rune2 : HudSkin.Glyph.Rune3;
                if (spell == null || spellbook == null)
                {
                    Set(id, spell != null ? spell.DisplayName : "—", GameInput.Label(key), schoolTint, glyph, 0f, 1f, false, 0, 0);
                    continue;
                }
                int charges = spellbook.ChargesOf(spell);
                float left = spellbook.CooldownRemaining(spell);
                float total = spellbook.RechargeSeconds(spell);
                // Con cargas restantes el hechizo está listo aunque otra carga se recargue.
                Set(id, spell.DisplayName, GameInput.Label(key), schoolTint, glyph,
                    charges > 0 ? 0f : left, total, canAct && stamina >= spell.StaminaCost, charges, spell.Charges);
            }

            if (race != null)
                Set(SlotId.Race, race.Data.PowerName, GameInput.Label(GameAction.RacialPower), race.HudColor, HudSkin.Glyph.Fang,
                    race.PowerCooldownRemaining, Mathf.Max(1f, race.Data.PowerCooldown), canAct, 0, 0);
            else
                Set(SlotId.Race, "Raza", GameInput.Label(GameAction.RacialPower), new Color(0.8f, 0.8f, 0.85f), HudSkin.Glyph.Fang, 0f, 1f, false, 0, 0);
        }

        private void Set(SlotId id, string name, string key, Color tint, HudSkin.Glyph glyph,
            float remaining, float total, bool usable, int charges, int maxCharges)
        {
            int i = (int)id;
            float left = float.IsNaN(remaining) ? 0f : Mathf.Max(0f, remaining);
            bool ready = left <= 0f;
            if (ready && !wasReady[i])
                readySince[i] = Time.unscaledTime;
            wasReady[i] = ready;
            slots[i] = new SlotState
            {
                Id = id, Name = name, Key = key, Tint = tint, Glyph = glyph,
                Remaining = left, Total = Mathf.Max(0.01f, total),
                Cooldown01 = CooldownFraction(left, total),
                Ready = ready, Usable = usable, Charges = charges, MaxCharges = maxCharges
            };
        }

        // ── Medidas ────────────────────────────────────────────────────

        /// <summary>Ancho total de la barra con su marco, sin escalar (lienzo virtual).</summary>
        public static float NaturalWidth =>
            2f * (OrbDiameter * 0.5f + OrbGap + SmallIcon + GroupGap + 4f * IconSize + 3f * IconGap + FramePad);

        /// <summary>Escala de la barra para no pasar del 45 % del ancho (pantallas estrechas).</summary>
        public static float BarScale(float virtualWidth) =>
            Mathf.Min(1f, virtualWidth * MaxWidthFraction / NaturalWidth);

        /// <summary>Fracción del ancho de pantalla que ocupa la barra a una resolución dada.</summary>
        public static float WidthFraction(int screenWidth, int screenHeight)
        {
            float scale = Mathf.Max(0.5f, screenHeight / MenuStyle.VirtualHeight);
            float virtualWidth = screenWidth / scale;
            return NaturalWidth * BarScale(virtualWidth) / virtualWidth;
        }

        // ── Dibujo ─────────────────────────────────────────────────────

        private void OnGUI()
        {
            // La profundidad se fija en todos los eventos (no solo al pintar) para que Unity
            // ordene bien: la barra y los avisos quedan encima de las barras de los enemigos.
            GUI.depth = -10;
            if (Event.current.type != EventType.Repaint || HudMode.Hidden || health == null)
                return;
            MenuStyle.Begin();
            Matrix4x4 baseMatrix = GUI.matrix;
            float w = MenuStyle.Width, h = MenuStyle.Height;

            HudNotices.Draw(w, 74f);

            float barScale = BarScale(w);
            float cx = w * 0.5f;
            GUI.matrix = baseMatrix *
                         Matrix4x4.TRS(new Vector3(cx, h, 0f), Quaternion.identity, new Vector3(barScale, barScale, 1f)) *
                         Matrix4x4.TRS(new Vector3(-cx, -h, 0f), Quaternion.identity, Vector3.one);
            DrawBar(cx, h);
            GUI.matrix = baseMatrix;
            MenuStyle.End();
        }

        private void DrawBar(float cx, float h)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            float iconTop = h - BottomMargin - 14f - IconSize;
            float half = NaturalWidth * 0.5f;
            Rect frame = new Rect(cx - half, iconTop - 12f, half * 2f, IconSize + 14f + 12f + 8f);
            bool building = builder != null && builder.IsBuilding;
            float alpha = building ? 0.55f : 1f;
            HudSkin.OrnateFrame(frame, alpha);

            // Iconos: izquierda (de fuera hacia dentro) y derecha (de dentro hacia fuera).
            float innerLeft = cx - OrbDiameter * 0.5f - OrbGap - SmallIcon - GroupGap;
            for (int i = 0; i < 4; i++)
            {
                float x = innerLeft - IconSize - (3 - i) * (IconSize + IconGap);
                DrawSlot(slots[(int)SlotId.Crossbow + i], new Rect(x, iconTop, IconSize, IconSize), true);
            }
            float innerRight = cx + OrbDiameter * 0.5f + OrbGap + SmallIcon + GroupGap;
            for (int i = 0; i < 4; i++)
            {
                float x = innerRight + i * (IconSize + IconGap);
                DrawSlot(slots[(int)SlotId.Spell1 + (i < 3 ? i : 3)], new Rect(x, iconTop, IconSize, IconSize), true);
            }

            // Poción (izquierda del orbe) y escuela activa (derecha), pequeñas y pegadas al orbe.
            float smallTop = iconTop + IconSize - SmallIcon;
            DrawSlot(slots[(int)SlotId.Potion], new Rect(cx - OrbDiameter * 0.5f - OrbGap - SmallIcon, smallTop, SmallIcon, SmallIcon), false);
            DrawSlot(slots[(int)SlotId.School], new Rect(cx + OrbDiameter * 0.5f + OrbGap, smallTop, SmallIcon, SmallIcon), false);

            // Orbe y arco de resistencia.
            float orbCy = h - BottomMargin - 10f - OrbDiameter * 0.5f;
            Rect orb = new Rect(cx - OrbDiameter * 0.5f, orbCy - OrbDiameter * 0.5f, OrbDiameter, OrbDiameter);
            float h01 = health.MaximumHealth > 0f ? health.CurrentHealth / health.MaximumHealth : 0f;
            float lowPulse = h01 < ScreenVignette.LowHealthThreshold ? pulse : 0f;
            // Arco de resistencia separado del aro dorado por una franja oscura.
            Rect arc = new Rect(orb.x - 14f, orb.y - 14f, orb.width + 28f, orb.height + 28f);
            HudSkin.SoftPanel(new Rect(arc.x - 14f, arc.y - 8f, arc.width + 28f, arc.height + 10f), 0.55f);
            float st01 = defense != null ? defense.Stamina / defense.MaximumStamina : 1f;
            Color stColor = defense != null && defense.GuardBroken ? new Color(0.95f, 0.3f, 0.25f) :
                defense != null && defense.ParryWindowActive ? Color.white : StaminaColor;
            HudSkin.Arc(arc, st01, stColor);
            HudSkin.Orb(orb, h01, HealthLiquid, lowPulse);
            HudSkin.Label(new Rect(orb.x, orb.y + 26f, orb.width, 30f), Mathf.CeilToInt(health.CurrentHealth).ToString(), 24,
                HudSkin.Text, TextAnchor.MiddleCenter, true);
            HudSkin.Label(new Rect(orb.x, orb.y + 52f, orb.width, 16f), "/ " + Mathf.CeilToInt(health.MaximumHealth), 12,
                HudSkin.TextDim);
            HudSkin.Diamond(new Vector2(cx, arc.y - 3f), 9f, HudSkin.Gold);

            // Línea mínima: escuela activa y esencia.
            float lineY = arc.y - 30f;
            DrawSchoolLine(cx, lineY);
            DrawContextPrompt(cx, lineY - 34f);
        }

        private void DrawSlot(SlotState s, Rect r, bool showName)
        {
            float since = Time.unscaledTime - readySince[(int)s.Id];
            float glow = since < 0.6f ? 1f - since / 0.6f : 0.25f;
            HudSkin.Icon(r, s.Tint, s.Glyph, s.Cooldown01, s.Ready, s.Usable, glow);

            if (!s.Ready)
                HudSkin.Label(new Rect(r.x, r.y, r.width, r.height), s.Remaining >= 10f ? Mathf.CeilToInt(s.Remaining).ToString() :
                    s.Remaining.ToString("0.0"), r.width > 45f ? 17 : 14, Color.white, TextAnchor.MiddleCenter, true);

            // Puntos de cargas (hechizos con varias).
            if (s.MaxCharges > 1)
            {
                float d = 6f, gap = 3f;
                float total = s.MaxCharges * d + (s.MaxCharges - 1) * gap;
                for (int i = 0; i < s.MaxCharges; i++)
                {
                    Vector2 c = new Vector2(r.center.x - total * 0.5f + d * 0.5f + i * (d + gap), r.yMax - 6f);
                    HudSkin.Circle(c, d + 2f, new Color(0f, 0f, 0f, 0.8f));
                    HudSkin.Circle(c, d, i < s.Charges ? HudSkin.GoldBright : new Color(0.3f, 0.26f, 0.22f, 0.9f));
                }
            }

            // Tecla real (cambia al reasignar en Opciones).
            float kw = Mathf.Max(16f, HudSkin.TextWidth(s.Key, 11, true) + 8f);
            Rect chip = new Rect(r.x - 3f, r.y - 5f, kw, 15f);
            HudSkin.Fill(chip, new Color(0.03f, 0.02f, 0.03f, 0.9f));
            HudSkin.Border(chip, 1f, new Color(HudSkin.Gold.r, HudSkin.Gold.g, HudSkin.Gold.b, 0.8f));
            HudSkin.Label(chip, s.Key, 11, HudSkin.GoldBright, TextAnchor.MiddleCenter, true);

            if (showName)
            {
                string name = ShortName(s.Name, r.width + 10f);
                HudSkin.Label(new Rect(r.x - 8f, r.yMax + 1f, r.width + 16f, 13f), name, 11,
                    s.Usable ? HudSkin.TextDim : new Color(0.5f, 0.47f, 0.45f));
            }
        }

        private static string ShortName(string name, float width)
        {
            if (string.IsNullOrEmpty(name) || HudSkin.TextWidth(name, 11) <= width)
                return name;
            int space = name.IndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }

        private void DrawSchoolLine(float cx, float y)
        {
            SpellSchool school = spellbook != null ? spellbook.School : SpellSchool.Pyromancy;
            string schoolName = SpellCatalog.SchoolName(school).ToUpperInvariant();
            string essenceText = essence != null && essence.Kind != EssenceKind.None
                ? "Esencia " + EssenceCatalog.Name(essence.Kind) + " " + essence.Quality + " % · " + EssenceCatalog.Bonus(essence.Kind)
                : "Sin esencia";
            string extra = "";
            if (barrier != null && barrier.Active)
                extra = "  ·  Égida " + barrier.Remaining.ToString("0");
            if (combat != null && combat.InCombat)
                extra += "  ·  en combate";

            float wSchool = HudSkin.TextWidth(schoolName, 14, true);
            float wRest = HudSkin.TextWidth("   " + essenceText + extra, 13);
            float total = wSchool + wRest + 16f;
            float x = cx - total * 0.5f;
            HudSkin.SoftPanel(new Rect(x - 30f, y - 2f, total + 60f, 22f), 0.4f);
            Color tint = SpellCatalog.SchoolColor(school);
            HudSkin.Diamond(new Vector2(x + 5f, y + 9f), 9f, tint);
            HudSkin.Label(new Rect(x + 14f, y, wSchool + 4f, 18f), schoolName, 14, Color.Lerp(tint, Color.white, 0.25f), TextAnchor.MiddleLeft, true);
            HudSkin.Label(new Rect(x + 16f + wSchool, y, wRest + 10f, 18f), "   " + essenceText + extra, 13,
                combat != null && combat.InCombat ? new Color(0.95f, 0.72f, 0.68f) : HudSkin.TextDim, TextAnchor.MiddleLeft);
        }

        /// <summary>Un solo aviso de contexto encima de la barra, con barra de progreso si la hay.</summary>
        private void DrawContextPrompt(float cx, float y)
        {
            string text = null;
            float progress = -1f;
            Color color = HudSkin.Text;
            if (feeding != null && feeding.IsChanneling)
            {
                text = "Absorbiendo esencia · suelta " + GameInput.Label(GameAction.Absorb) + " para cancelar · clic: ejecutar";
                progress = feeding.Progress01;
                color = new Color(1f, 0.6f, 0.6f);
            }
            else if (feeding != null && feeding.Candidate != null)
            {
                EssenceSource source = feeding.Candidate.GetComponent<EssenceSource>();
                text = "Mantén " + GameInput.Label(GameAction.Absorb) + ": absorber esencia" +
                       (source != null ? " " + EssenceCatalog.Name(source.Kind) + " " + source.Quality + " %" : "");
                color = new Color(1f, 0.62f, 0.62f);
            }
            else if (gatherer != null && gatherer.Target != null && (builder == null || !builder.IsBuilding))
            {
                HarvestNode node = gatherer.Target;
                text = gatherer.CanGatherTarget
                    ? "Mantén " + GameInput.Label(GameAction.Gather) + ": recolectar " + HarvestNode.ResourceName(node.Resource) + " (" + node.Remaining + ")"
                    : "Necesitas " + SurvivalToolbelt.DisplayName(node.RequiredTool) + " para " + HarvestNode.ResourceName(node.Resource);
                if (gatherer.CanGatherTarget && gatherer.Progress01 > 0f)
                    progress = gatherer.Progress01;
            }
            else if (noticeboard != null && noticeboard.PlayerNearby && !noticeboard.MenuOpen)
            {
                text = "Puesto del Cruce · " + GameInput.Label(GameAction.Noticeboard) + ": tablón de misiones";
                color = HudSkin.GoldBright;
            }
            if (text == null)
                return;
            float tw = HudSkin.TextWidth(text, 15) + 50f;
            Rect band = new Rect(cx - tw * 0.5f, y, tw, 24f);
            HudSkin.SoftPanel(band, 0.55f);
            HudSkin.Label(band, text, 15, color);
            if (progress >= 0f)
            {
                Rect bar = new Rect(cx - 110f, band.yMax + 2f, 220f, 4f);
                HudSkin.Fill(bar, new Color(0f, 0f, 0f, 0.6f));
                HudSkin.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(progress), bar.height), color);
            }
        }
    }
}
