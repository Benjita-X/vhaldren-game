using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Progression;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.UI
{
    /// <summary>
    /// Información del mundo sin cajas: misión actual (una o dos líneas arriba a la
    /// derecha, con la hora), estados de los enemigos como puntitos de color con su
    /// número de cargas (más nivel y esencia en pequeño) y flechas en el borde cuando un
    /// enemigo fuera de la pantalla prepara un golpe contra el jugador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldHud : MonoBehaviour
    {
        public const float MarkerRange = 22f;
        public const float EdgeMargin = 42f;

        public struct OffscreenWarning
        {
            public PrototypeEnemy Enemy;
            public Vector2 EdgePoint;   // píxeles de pantalla, origen arriba a la izquierda
            public float Angle;         // grados; 0 = derecha, 90 = abajo
            public float Fill;
        }

        [SerializeField] private Transform player;
        private readonly List<PrototypeEnemy> enemies = new List<PrototypeEnemy>(32);
        private readonly List<OffscreenWarning> warnings = new List<OffscreenWarning>(8);
        private VillageQuestJournal journal;
        private VillageNoticeboard noticeboard;
        private PlayerEquipment equipment;
        private Camera view;
        private float nextScan;

        public IReadOnlyList<OffscreenWarning> Warnings => warnings;

        public void Bind(Transform target)
        {
            player = target;
            journal = null;
            equipment = null;
        }

        private void Scan()
        {
            enemies.Clear();
            foreach (PrototypeEnemy enemy in FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None))
                if (enemy != null)
                    enemies.Add(enemy);
            if (player != null && journal == null)
                journal = player.GetComponent<VillageQuestJournal>();
            if (player != null && equipment == null)
                equipment = player.GetComponent<PlayerEquipment>();
            if (noticeboard == null)
                noticeboard = FindAnyObjectByType<VillageNoticeboard>();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                Scan();
            }
            if (view == null || !view.isActiveAndEnabled)
                view = Camera.main;
            UpdateWarnings();
        }

        /// <summary>Calcula las flechas: enemigos que preparan un golpe contra el jugador y no se ven.</summary>
        private void UpdateWarnings()
        {
            warnings.Clear();
            if (view == null || player == null)
                return;
            Vector2 size = new Vector2(Screen.width, Screen.height);
            Rect bar = BarScreenRect();
            for (int i = 0; i < enemies.Count; i++)
            {
                PrototypeEnemy enemy = enemies[i];
                if (enemy == null || enemy.IsDefeated || !enemy.IsAttackTelegraphed || enemy.Target != player)
                    continue;
                // El centro del cuerpo: si se ve en pantalla no hace falta flecha.
                Vector3 viewport = view.WorldToViewportPoint(enemy.transform.position);
                if (!OffscreenEdge(viewport, size, EdgeMargin, out Vector2 edge, out float angle))
                    continue;
                edge = AvoidRect(edge, bar);
                EnemyAttackTelegraph telegraph = enemy.GetComponent<EnemyAttackTelegraph>();
                warnings.Add(new OffscreenWarning
                {
                    Enemy = enemy, EdgePoint = edge, Angle = angle,
                    Fill = telegraph != null ? telegraph.Fill : 0.5f
                });
            }
        }

        /// <summary>
        /// Si el punto (coordenadas de viewport) queda fuera de la pantalla, devuelve dónde
        /// poner la flecha en el borde (píxeles, y hacia abajo) y su ángulo. Detrás de la
        /// cámara se invierte la dirección.
        /// </summary>
        public static bool OffscreenEdge(Vector3 viewport, Vector2 screen, float margin, out Vector2 edge, out float angle)
        {
            edge = Vector2.zero;
            angle = 0f;
            bool behind = viewport.z < 0f;
            bool inside = !behind && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
            if (inside)
                return false;
            Vector2 dir = new Vector2(viewport.x - 0.5f, 0.5f - viewport.y);   // y hacia abajo
            if (behind)
                dir = -dir;
            if (dir.sqrMagnitude < 1e-6f)
                dir = Vector2.down;
            Vector2 half = new Vector2(screen.x * 0.5f - margin, screen.y * 0.5f - margin);
            Vector2 pixels = new Vector2(dir.x * screen.x, dir.y * screen.y);
            float scale = Mathf.Min(
                Mathf.Abs(pixels.x) > 1e-4f ? half.x / Mathf.Abs(pixels.x) : float.MaxValue,
                Mathf.Abs(pixels.y) > 1e-4f ? half.y / Mathf.Abs(pixels.y) : float.MaxValue);
            edge = screen * 0.5f + pixels * scale;
            angle = Mathf.Atan2(pixels.y, pixels.x) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>Zona de la barra de acción en píxeles (para no poner flechas encima).</summary>
        public static Rect BarScreenRect()
        {
            float scale = MenuStyle.Scale;
            float virtualWidth = Screen.width / scale;
            float half = ActionBarHud.NaturalWidth * ActionBarHud.BarScale(virtualWidth) * 0.5f * scale;
            float height = 190f * scale;   // barra, orbe y la línea de la escuela
            return new Rect(Screen.width * 0.5f - half - 20f, Screen.height - height, half * 2f + 40f, height);
        }

        /// <summary>Si el punto cae dentro de la zona, lo sube justo por encima de ella.</summary>
        public static Vector2 AvoidRect(Vector2 point, Rect avoid)
        {
            if (!avoid.Contains(point))
                return point;
            return new Vector2(point.x, avoid.yMin - 18f);
        }

        /// <summary>Color de cada estado (el de su escuela de magia).</summary>
        public static Color StatusColor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Ignition: return new Color(1f, 0.48f, 0.12f);
                case StatusKind.Chill: return new Color(0.55f, 0.85f, 1f);
                case StatusKind.Static: return new Color(1f, 0.92f, 0.35f);
                case StatusKind.Curse: return new Color(0.68f, 0.38f, 0.95f);
                case StatusKind.Bleed: return new Color(0.88f, 0.08f, 0.16f);
                case StatusKind.Dazzle: return new Color(1f, 0.97f, 0.78f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        private void OnGUI()
        {
            GUI.depth = -4;   // encima de las barras de vida de los enemigos (0), debajo de la barra (-10)
            if (Event.current.type != EventType.Repaint || HudMode.Hidden)
                return;
            DrawEnemyMarkers();
            DrawWarnings();
            MenuStyle.Begin();
            DrawQuestTracker(MenuStyle.Width);
            MenuStyle.End();
        }

        // ── Enemigos: nivel, puntos de estado y esencia ────────────────

        private void DrawEnemyMarkers()
        {
            if (view == null || player == null)
                return;
            Vector3 origin = player.position;
            for (int i = 0; i < enemies.Count; i++)
            {
                PrototypeEnemy enemy = enemies[i];
                if (enemy == null || enemy.IsDefeated)
                    continue;
                if ((enemy.transform.position - origin).sqrMagnitude > MarkerRange * MarkerRange)
                    continue;
                CombatHealth targetHealth = enemy.GetComponent<CombatHealth>();
                if (targetHealth == null || targetHealth.IsDefeated)
                    continue;
                CombatDamageFeedback bar = enemy.GetComponent<CombatDamageFeedback>();
                Vector3 offset = bar != null ? bar.LabelOffset : new Vector3(0f, 1.65f, 0f);
                Vector3 screen = view.WorldToScreenPoint(enemy.transform.position + offset);
                if (screen.z <= 0f || screen.x < -60f || screen.x > Screen.width + 60f || screen.y < -60f || screen.y > Screen.height + 60f)
                    continue;
                float x = screen.x, y = Screen.height - screen.y;
                float barHalf = bar != null && bar.IsBossBar ? 107f : 73f;
                DrawStatusRow(enemy, x, y - 50f, barHalf);

                EssenceSource source = enemy.GetComponent<EssenceSource>();
                if (source != null && targetHealth.CurrentHealth <= targetHealth.MaximumHealth * PlayerFeeding.Threshold)
                    HudSkin.Label(new Rect(x - 110f, y + 24f, 220f, 16f),
                        "Esencia " + EssenceCatalog.Name(source.Kind) + " " + source.Quality + " % · " + GameInput.Label(GameAction.Absorb),
                        12, new Color(1f, 0.5f, 0.5f));
            }
        }

        /// <summary>Fila sobre el nombre: «Nv 5» pequeño a la izquierda y un punto por estado.</summary>
        private void DrawStatusRow(PrototypeEnemy enemy, float x, float y, float barHalf)
        {
            CombatLevel level = enemy.GetComponent<CombatLevel>();
            if (level != null)
            {
                int gear = equipment != null ? equipment.GearLevel : level.Level;
                int gap = level.Level - gear;
                Color c = gap >= 6 ? new Color(1f, 0.32f, 0.32f) : gap >= 2 ? new Color(1f, 0.82f, 0.3f) : new Color(0.86f, 0.86f, 0.86f);
                HudSkin.Label(new Rect(x - barHalf - 2f, y - 1f, 60f, 14f), "Nv " + level.Level, 11, c, TextAnchor.MiddleLeft, true);
            }

            StatusEffects status = enemy.GetComponent<StatusEffects>();
            if (status == null)
                return;
            const float d = 9f, gapX = 5f;
            int count = 0;
            for (int k = 0; k <= (int)StatusKind.Dazzle; k++)
                if (status.Has((StatusKind)k))
                    count++;
            bool frozen = status.IsFrozen;
            if (frozen)
                count++;
            if (count == 0)
                return;
            float width = count * d + (count - 1) * gapX + 10f;
            float cx = x - width * 0.5f + d * 0.5f;
            if (frozen)
            {
                Vector2 c = new Vector2(cx, y + 6f);
                HudSkin.Circle(c, d + 6f, new Color(0.6f, 0.95f, 1f, 0.45f));
                HudSkin.Circle(c, d + 2f, new Color(0f, 0f, 0f, 0.85f));
                HudSkin.Circle(c, d, new Color(0.75f, 0.97f, 1f));
                cx += d + gapX;
            }
            for (int k = 0; k <= (int)StatusKind.Dazzle; k++)
            {
                StatusKind kind = (StatusKind)k;
                if (!status.Has(kind))
                    continue;
                Vector2 c = new Vector2(cx, y + 6f);
                // Parpadea en el último segundo antes de expirar.
                float left = status.Remaining(kind);
                float alpha = left < 1f ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 9f)) : 1f;
                Color color = StatusColor(kind);
                color.a = alpha;
                HudSkin.Circle(c, d + 2f, new Color(0f, 0f, 0f, 0.85f * alpha));
                HudSkin.Circle(c, d, color);
                if (kind == StatusKind.Chill && status.ChillStacks > 1)
                {
                    HudSkin.Label(new Rect(c.x + 3f, c.y - 2f, 14f, 12f), status.ChillStacks.ToString(), 10, Color.white, TextAnchor.MiddleLeft, true);
                    cx += 8f;
                }
                cx += d + gapX;
            }
        }

        // ── Flechas de golpe fuera de pantalla ─────────────────────────

        private void DrawWarnings()
        {
            if (warnings.Count == 0)
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            float size = Mathf.Clamp(Screen.height / 900f, 0.75f, 1.6f) * 38f;
            Matrix4x4 previous = GUI.matrix;
            for (int i = 0; i < warnings.Count; i++)
            {
                OffscreenWarning w = warnings[i];
                Color color = Color.Lerp(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.12f, 0.12f), Mathf.Clamp01(w.Fill));
                HudSkin.Circle(w.EdgePoint, size * 1.5f, new Color(0f, 0f, 0f, 0.45f));
                GUIUtility.RotateAroundPivot(w.Angle, w.EdgePoint);
                Rect r = new Rect(w.EdgePoint.x - size * 0.5f, w.EdgePoint.y - size * 0.5f, size, size);
                HudSkin.Draw(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), HudSkin.Arrow, new Color(0f, 0f, 0f, 0.7f));
                color.a = 0.75f + 0.25f * pulse;
                HudSkin.Draw(r, HudSkin.Arrow, color);
                GUI.matrix = previous;
            }
        }

        // ── Misión actual ──────────────────────────────────────────────

        private void DrawQuestTracker(float width)
        {
            if (noticeboard != null && noticeboard.MenuOpen)
                return;
            float right = width - 22f;
            float y = 16f;
            if (DayNightCycle.Active != null)
            {
                bool crimson = DayNightCycle.Active.IsCrimsonMoon;
                string clock = DayNightCycle.Active.Clock;   // «Día 1 · 08:21 día» (o «LUNA CARMESÍ»)
                HudSkin.Label(new Rect(right - 360f, y, 360f, 16f), clock, 13,
                    crimson ? new Color(1f, 0.38f, 0.38f) : HudSkin.TextDim, TextAnchor.MiddleRight);
                y += 20f;
            }
            if (journal == null)
                return;
            VillageQuest quest = journal.Current;
            string title, detail;
            if (quest == null)
            {
                title = "Cadena del Cruce completada";
                detail = "Explora Vhaldren";
            }
            else
            {
                title = quest.Name;
                switch (journal.State)
                {
                    case VillageQuestState.Available:
                        detail = "Acéptala en el tablón del Cruce (" + GameInput.Label(GameAction.Noticeboard) + ")";
                        break;
                    case VillageQuestState.ReadyToClaim:
                        detail = "Lista: entrégala en el tablón (" + GameInput.Label(GameAction.Noticeboard) + ")";
                        break;
                    case VillageQuestState.Active:
                        detail = "Progreso " + Mathf.Min(quest.Required, journal.Progress) + " / " + quest.Required;
                        break;
                    default:
                        detail = string.Empty;
                        break;
                }
            }
            float tw = Mathf.Max(HudSkin.TextWidth(title, 15, true), HudSkin.TextWidth(detail, 13)) + 22f;
            HudSkin.Diamond(new Vector2(right - tw + 4f, y + 9f), 8f, HudSkin.Gold);
            HudSkin.Label(new Rect(right - 400f, y, 400f, 18f), title, 15, HudSkin.GoldBright, TextAnchor.MiddleRight, true);
            if (!string.IsNullOrEmpty(detail))
                HudSkin.Label(new Rect(right - 400f, y + 19f, 400f, 16f), detail, 13,
                    journal.State == VillageQuestState.ReadyToClaim ? new Color(0.75f, 0.95f, 0.7f) : HudSkin.Text, TextAnchor.MiddleRight);
            HudSkin.FadeLine(new Rect(right - tw, y + 38f, tw, 1f), new Color(HudSkin.Gold.r, HudSkin.Gold.g, HudSkin.Gold.b, 0.5f));
        }
    }
}
