using System.Text;
using UnityEngine;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Progression;
using Vhaldren.Gameplay.Races;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Códice del juego: toda la informacion de la pantalla y de los sistemas, generada
    /// a partir de los catalogos (nunca se desactualiza): controles, interfaz, hechizos,
    /// estados, esencias, razas, bestiario y equipo.
    /// </summary>
    public sealed class CodexPanel
    {
        private static readonly string[] Tabs = { "Controles", "Interfaz", "Hechizos", "Estados", "Esencias", "Razas", "Bestiario", "Equipo" };
        private int tab;
        private Vector2 scroll;
        private string cachedText;
        private int cachedTab = -1;

        public bool Draw(Rect area)
        {
            MenuStyle.FramedPanel(area);
            GUI.Label(new Rect(area.x + 24f, area.y + 14f, 500f, 40f), "INFORMACIÓN", MenuStyle.Header);
            float tabWidth = Mathf.Min(140f, (area.width - 48f) / Tabs.Length - 6f);
            for (int i = 0; i < Tabs.Length; i++)
                if (GUI.Button(new Rect(area.x + 24f + i * (tabWidth + 6f), area.y + 62f, tabWidth, 36f), Tabs[i],
                        i == tab ? MenuStyle.TabActive : MenuStyle.Tab))
                {
                    tab = i;
                    scroll = Vector2.zero;
                }
            if (cachedTab != tab || tab == 0)
            {
                cachedText = Build(tab);
                cachedTab = tab;
            }
            Rect view = new Rect(area.x + 24f, area.y + 112f, area.width - 48f, area.height - 186f);
            float height = MenuStyle.Body.CalcHeight(new GUIContent(cachedText), view.width - 30f) + 20f;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, view.width - 24f, height));
            GUI.Label(new Rect(0f, 0f, view.width - 30f, height), cachedText, MenuStyle.Body);
            GUI.EndScrollView();
            return GUI.Button(new Rect(area.xMax - 244f, area.yMax - 60f, 220f, 44f), "Volver", MenuStyle.Button);
        }

        public static string Build(int tab)
        {
            StringBuilder t = new StringBuilder(4096);
            switch (tab)
            {
                case 0:
                    string group = null;
                    foreach (GameInput.Binding b in GameInput.All)
                    {
                        if (b.Group != group)
                        {
                            group = b.Group;
                            t.Append("\n<b><color=#eec872>").Append(group.ToUpperInvariant()).Append("</color></b>\n");
                        }
                        t.Append("  <b>").Append(GameInput.Label(b.Action)).Append("</b> — ").Append(b.Label).Append('\n');
                    }
                    t.Append("  <b>Clic izquierdo</b> — ballesta (y ejecutar mientras absorbes)\n  <b>Rueda</b> — zoom de cámara\n");
                    t.Append("  <b>1–0</b> en construcción — elegir pieza · <b>Mayús</b> colocar libre · <b>Ctrl+Z</b> deshacer\n");
                    t.Append("\nLas teclas se cambian en Opciones → Controles.");
                    break;
                case 1:
                    t.Append("<b>Arriba a la izquierda</b>: zona actual, vida, guardianes y jefe; daño total por arma; estado de la poción.\n");
                    t.Append("<b>Bajo ese panel</b>: defensa (escudo, barrera, parada), resistencia y habilidades V/T/F con su recarga.\n");
                    t.Append("<b>Derecha</b>: ayuda F1 con los controles actuales.\n");
                    t.Append("<b>Barra central inferior</b>: tu raza y su poder (R); escuela activa, nivel de equipo, estado de combate y hora; los tres hechizos con cargas y recarga; esencia actual y su reserva.\n");
                    t.Append("<b>Abajo a la izquierda</b>: materiales e inventario (madera, piedra, fibra, mineral, capacidad).\n");
                    t.Append("<b>Sobre los enemigos</b>: nombre y vida; <color=#dddddd>Nv blanco</color> a tu altura, <color=#ffd24a>amarillo</color> reto, <color=#ff5050>rojo</color> demasiado fuerte; estados activos; su esencia cuando está bajo el 30 %.\n");
                    t.Append("<b>Avisos en el suelo</b>: anillo naranja = golpe físico (escudo Z), azul = mágico (barrera C), línea roja = disparo.\n");
                    t.Append("<b>Números</b>: rojo = daño recibido, verde = curación, dorado = bloqueado.\n");
                    t.Append("<b>Bordes rojos</b>: te acaban de herir. <b>EN COMBATE</b>: sin regeneración ni cambio de escuela durante 6 s.");
                    break;
                case 2:
                    for (int s = 0; s < SpellCatalog.SchoolCount; s++)
                    {
                        SpellSchool school = (SpellSchool)s;
                        t.Append("\n<b><color=#").Append(ColorUtility.ToHtmlStringRGB(SpellCatalog.SchoolColor(school))).Append('>')
                            .Append(SpellCatalog.SchoolName(school).ToUpperInvariant()).Append("</color></b>\n");
                        for (int slot = 0; slot < SpellCatalog.SpellsPerSchool; slot++)
                        {
                            SpellDefinition spell = SpellCatalog.Get(school, slot);
                            t.Append("  <b>").Append(slot + 1).Append(". ").Append(spell.DisplayName).Append("</b> — ")
                                .Append(spell.Damage.ToString("0")).Append(" daño · recarga ").Append(spell.Cooldown.ToString("0.#"))
                                .Append(" s · ").Append(spell.StaminaCost.ToString("0")).Append(" resistencia");
                            if (spell.Charges > 1) t.Append(" · ").Append(spell.Charges).Append(" cargas");
                            t.Append("\n     ").Append(spell.Description).Append('\n');
                        }
                    }
                    t.Append("\nSe empieza con Piromancia, Criomancia y Fulmancia (más las de tu raza). Las demás se aprenden absorbiendo esencias.");
                    break;
                case 3:
                    foreach (StatusKind kind in System.Enum.GetValues(typeof(StatusKind)))
                        t.Append("<b>").Append(StatusEffects.DisplayName(kind)).Append("</b> — ").Append(StatusText(kind)).Append('\n');
                    break;
                case 4:
                    t.Append("Debilita a un enemigo por debajo del 30 % y mantén <b>").Append(GameInput.Label(GameAction.Absorb))
                        .Append("</b> 2,4 s. Quedas expuesto (+30 % daño recibido) y un golpe fuerte te interrumpe. Clic = ejecutar sin esencia.\n");
                    t.Append("La calidad (1–100 %) da la fuerza de la bonificación; la reserva dura unos 10 min y por debajo del 25 % rinde a la mitad. De noche +15 calidad; Luna carmesí +30.\n\n");
                    foreach (EssenceKind kind in System.Enum.GetValues(typeof(EssenceKind)))
                    {
                        if (kind == EssenceKind.None) continue;
                        t.Append("<b>").Append(EssenceCatalog.Name(kind)).Append("</b> — ").Append(EssenceCatalog.Bonus(kind));
                        if (EssenceCatalog.TeachesSchool(kind, out SpellSchool school))
                            t.Append(" · enseña ").Append(SpellCatalog.SchoolName(school));
                        if (kind == EssenceKind.Arcane) t.Append(" · solo jefes, enseña todas las escuelas");
                        t.Append('\n');
                    }
                    break;
                case 5:
                    foreach (RaceDefinition race in RaceCatalog.All)
                        AppendRace(t, race);
                    break;
                case 6:
                    foreach (EnemyArchetype enemy in EnemyArchetypes.All)
                        t.Append("<b>").Append(enemy.DisplayName).Append("</b> (Nv ").Append(enemy.Level).Append(", ").Append(RoleName(enemy.Role))
                            .Append(") — ").Append(enemy.Health.ToString("0")).Append(" PV, ").Append(enemy.Damage.ToString("0"))
                            .Append(" daño. ").Append(enemy.Description).Append('\n');
                    t.Append("\n<b>Acechador del bosque</b> (Nv 6) · <b>Centinela de ruinas</b> (Nv 9) · <b>Guardián del altar</b> (Nv 13)\n");
                    t.Append("<b>JEFE — Caballero de sangre</b> (Nv 26): se enfurece al 50 %; esencia Arcana.\n");
                    t.Append("\nAtacar a uno alerta a su campamento (10 m). Por cada nivel de diferencia: ±4 % de daño.");
                    break;
                default:
                    t.Append("Tu nivel de equipo es la media de arma, armadura y talismán. Forja con <b>").Append(GameInput.Label(GameAction.Equipment)).Append("</b>.\n\n");
                    foreach (GearTier tier in GearCatalog.Tiers)
                    {
                        t.Append("<b>").Append(tier.Suffix).Append("</b> — nivel ").Append(tier.Level);
                        if (tier.Cost.Length > 0) t.Append(" — ").Append(PlayerEquipment.CostText(tier));
                        t.Append('\n');
                    }
                    t.Append("\nArmadura: +20 / +45 / +80 vida · Talismán: +6 / +12 / +20 % poder de hechizo.\n");
                    t.Append("Piel: lobos, tiradores · Mineral: bruto, ruinas, vetas (con pico) · Cristal y esencia: acólitos, centinela, jefe, mercado.");
                    break;
            }
            return t.ToString();
        }

        public static void AppendRace(StringBuilder t, RaceDefinition race)
        {
            t.Append("\n<b><color=#eec872>").Append(race.Name.ToUpperInvariant()).Append("</color></b> — ").Append(race.Summary).Append('\n');
            t.Append("  Vida ×").Append(race.Health.ToString("0.##")).Append(" · Resistencia ").Append(race.Stamina.ToString("0"))
                .Append(" · Velocidad ×").Append(race.Speed.ToString("0.##")).Append(" · Físico ×").Append(race.Physical.ToString("0.##"))
                .Append(" · Magia ×").Append(race.Spell.ToString("0.##")).Append('\n');
            t.Append("  Afinidad: ");
            for (int s = 0; s < SpellCatalog.SchoolCount; s++)
            {
                float a = race.Affinity[s];
                if (Mathf.Approximately(a, 1f)) continue;
                t.Append(SpellCatalog.SchoolName((SpellSchool)s)).Append(a > 1f ? " +" : " ").Append(Mathf.RoundToInt((a - 1f) * 100f)).Append("%  ");
            }
            t.Append('\n');
            t.Append("  <color=#8f8>Ventajas:</color> ").Append(string.Join(", ", race.Strengths)).Append('\n');
            t.Append("  <color=#f99>Debilidades:</color> ").Append(string.Join(", ", race.Weaknesses)).Append('\n');
            t.Append("  <b>Poder (").Append(GameInput.Label(GameAction.RacialPower)).Append("): ").Append(race.PowerName).Append("</b> — ")
                .Append(race.PowerText).Append(" Recarga ").Append(race.PowerCooldown.ToString("0")).Append(" s.\n");
        }

        private static string StatusText(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Ignition: return "quema 5 PV por segundo durante 4 s (Piromancia).";
                case StatusKind.Chill: return "ralentiza; con 3 cargas congela 1,4 s (Criomancia).";
                case StatusKind.Static: return "el siguiente hechizo que le alcance hace +18 de daño (Fulmancia).";
                case StatusKind.Curse: return "recibe un 25 % más de daño durante 5 s (Umbramancia).";
                case StatusKind.Bleed: return "tus golpes al marcado te curan un 35 % del daño (Hemomancia).";
                case StatusKind.Dazzle: return "hace un 35 % menos de daño durante 4 s (Lumancia).";
                default: return "mientras absorbes esencia recibes un 30 % más de daño.";
            }
        }

        private static string RoleName(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Ranged: return "tirador";
                case EnemyRole.Caster: return "mago";
                case EnemyRole.Beast: return "bestia";
                case EnemyRole.Brute: return "bruto";
                case EnemyRole.Support: return "apoyo";
                case EnemyRole.Elite: return "élite";
                default: return "cuerpo a cuerpo";
            }
        }
    }
}
