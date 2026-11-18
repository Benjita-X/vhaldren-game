using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Mounts;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Mapa del mundo (M) y minimapa (arriba a la izquierda). El mapa usa la textura de
    /// Resources indicada por <see cref="WorldAtlas"/> ("Mapa/mapa_mundo"; si falta, dibuja
    /// las regiones como circulos), con niebla de guerra por celdas que se revela al
    /// caminar (guardada), lugares descubiertos, piedras de viaje (clic en una activada =
    /// viajar con fundido a negro), bestias por domar vistas, nombres de las regiones
    /// visitadas y la posicion y orientacion del jugador. Sin atlas no dibuja nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMapHud : MonoBehaviour
    {
        public const float RevealRadius = 42f;
        public const float FlightRevealRadius = 90f;
        public const float MinimapSize = 176f;
        public const float MinimapRange = 95f;
        public const float FlightMinimapRange = 170f;

        private static readonly Color32 HiddenFog = new Color32(9, 7, 13, 238);
        private static readonly Color32 ClearFog = new Color32(0, 0, 0, 0);
        private static readonly Color StoneColor = new Color(0.45f, 0.92f, 1f);

        private WorldAtlas atlas;
        private Vector2 boundsMin, boundsMax;
        private FogOfWarGrid fog;
        private Texture2D fogTexture;
        private Color32[] fogPixels;
        private Texture2D mapTexture;
        private readonly List<int> changed = new List<int>(512);
        private readonly HashSet<string> visitedRegions = new HashSet<string>();
        private bool fogDirty, textureDirty, regionsDirty = true;
        private float nextReveal, nextSave, nextApply;

        private bool open;
        private int lockToken;
        private PlayerActionModifiers modifiers;
        private CombatHealth health;
        private Transform player;
        private PlayerMotor motor;

        private bool travelling;
        private float fade;
        private Waystone hoverStone;
        private string hoverTitle, hoverDetail;
        private Vector2 hoverPoint;

        public bool IsOpen => open;
        public bool IsTravelling => travelling;
        public FogOfWarGrid Fog => fog;
        public float ExploredFraction => fog != null ? fog.Fraction : 0f;
        public bool HasMapTexture => mapTexture != null;

        private void Awake()
        {
            player = transform;
            ExplorationSave.Changed += OnSaveChanged;
        }

        private void OnDestroy()
        {
            ExplorationSave.Changed -= OnSaveChanged;
            if (fogTexture != null)
                Destroy(fogTexture);
        }

        private void OnSaveChanged() => regionsDirty = true;

        private void Setup()
        {
            atlas.ResolveBounds(out boundsMin, out boundsMax);
            fog = FogOfWarGrid.ForBounds(boundsMin, boundsMax, 16f);
            fog.Deserialize(ExplorationSave.GetText(ExplorationSave.Fog, "mapa"));
            fogPixels = new Color32[fog.Width * fog.Height];
            for (int y = 0; y < fog.Height; y++)
                for (int x = 0; x < fog.Width; x++)
                    fogPixels[y * fog.Width + x] = fog.IsRevealed(x, y) ? ClearFog : HiddenFog;
            fogTexture = new Texture2D(fog.Width, fog.Height, TextureFormat.RGBA32, false)
            {
                name = "Niebla del mapa",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            fogTexture.SetPixels32(fogPixels);
            fogTexture.Apply(false);
            mapTexture = Resources.Load<Texture2D>(atlas.MapResource);
            regionsDirty = true;
        }

        private void Update()
        {
            if (atlas == null)
            {
                atlas = WorldAtlas.Active;
                if (atlas == null)
                    return;
                Setup();
            }
            if (modifiers == null) modifiers = GetComponent<PlayerActionModifiers>();
            if (health == null) health = GetComponent<CombatHealth>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            float now = Time.unscaledTime;

            if (now >= nextReveal)
            {
                nextReveal = now + 0.25f;
                Reveal();
            }
            if (textureDirty && now >= nextApply)
            {
                nextApply = now + 0.25f;
                textureDirty = false;
                fogTexture.SetPixels32(fogPixels);
                fogTexture.Apply(false);
            }
            if (fogDirty && now >= nextSave)
                SaveFog(false);

            bool dead = health != null && health.IsDefeated;
            if (ExplorationUi.MapOpenRequested)
            {
                ExplorationUi.MapOpenRequested = false;
                if (!dead && !travelling)
                    SetOpen(true);
            }
            if (GameInput.Down(GameAction.Map) && Time.timeScale > 0f && !travelling && !ExplorationUi.ReadingLore && !dead)
                SetOpen(!open);
            if (open && (Time.timeScale <= 0f || dead))
                SetOpen(false);
        }

        private void Reveal()
        {
            if (fog == null || player == null)
                return;
            changed.Clear();
            float radius = ExplorationUi.PlayerFlying ? FlightRevealRadius : RevealRadius;
            if (fog.Reveal(ExplorationMath.Planar(player.position), radius, changed) <= 0)
                return;
            for (int i = 0; i < changed.Count; i++)
                fogPixels[changed[i]] = ClearFog;
            fogDirty = true;
            textureDirty = true;
            if (nextSave <= Time.unscaledTime)
                nextSave = Time.unscaledTime + 20f;
        }

        private void SaveFog(bool flush)
        {
            if (fog == null)
                return;
            ExplorationSave.SetText(ExplorationSave.Fog, "mapa", fog.Serialize(), flush);
            fogDirty = false;
            nextSave = Time.unscaledTime + 20f;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveFog(true);
        }

        private void OnApplicationQuit() => SaveFog(true);

        private void OnDisable()
        {
            SetOpen(false);
            if (fogDirty)
                SaveFog(true);
        }

        public void SetOpen(bool value)
        {
            if (value && atlas == null)
                return;
            open = value;
            ExplorationUi.MapOpen = value;
            if (value && modifiers != null && lockToken == 0)
                lockToken = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack |
                                                  PlayerActionModifiers.LockedAction.Movement |
                                                  PlayerActionModifiers.LockedAction.Dash);
            else if (!value && lockToken != 0 && !travelling)
                ReleaseLock();
            hoverStone = null;
        }

        private void ReleaseLock()
        {
            if (modifiers != null && lockToken != 0)
                modifiers.RemoveModifier(lockToken);
            lockToken = 0;
        }

        // ── Viaje rapido ───────────────────────────────────────────────

        /// <summary>Viaja a una piedra activada con fundido a negro.</summary>
        public bool TravelTo(Waystone stone)
        {
            if (stone == null || !stone.IsActivated || travelling)
                return false;
            StartCoroutine(Travel(stone));
            return true;
        }

        private IEnumerator Travel(Waystone stone)
        {
            travelling = true;
            if (modifiers != null && lockToken == 0)
                lockToken = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack |
                                                  PlayerActionModifiers.LockedAction.Movement |
                                                  PlayerActionModifiers.LockedAction.Dash);
            open = false;
            ExplorationUi.MapOpen = false;
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                fade = t / 0.45f;
                yield return null;
            }
            fade = 1f;
            if (stone != null)
                Teleport(transform, stone.TravelPoint, stone.transform.position);
            yield return new WaitForSecondsRealtime(0.2f);
            if (stone != null)
                HudNotices.Post("Has viajado a " + stone.DisplayName, StoneColor, 2.4f);
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                fade = 1f - t / 0.6f;
                yield return null;
            }
            fade = 0f;
            travelling = false;
            ReleaseLock();
        }

        /// <summary>
        /// Teletransporta al jugador (como la reaparicion: CharacterController apagado y
        /// encendido) sobre el suelo del punto, desmontando antes y recolocando la camara.
        /// </summary>
        public static void Teleport(Transform target, Vector3 point, Vector3 lookAt)
        {
            if (target == null)
                return;
            MountController mount = target.GetComponent<MountController>();
            if (mount != null && mount.IsMounted)
                mount.ForceDismount(true);
            CharacterController body = target.GetComponent<CharacterController>();
            float feetOffset = body != null ? (body.height * 0.5f - body.center.y) * Mathf.Abs(target.lossyScale.y) : 1f;
            float ground = point.y;
            if (GroundProbe.TryGroundBelow(point, target, 30f, out float found))
                ground = found;
            Vector3 destination = new Vector3(point.x, ground + feetOffset + 0.05f, point.z);
            bool wasEnabled = body != null && body.enabled;
            if (body != null)
                body.enabled = false;
            target.position = destination;
            if (body != null && wasEnabled)
                body.enabled = true;
            DeepWaterGuard guard = target.GetComponent<DeepWaterGuard>();
            if (guard != null)
                guard.ResetSafePosition(destination);
            PlayerMotor playerMotor = target.GetComponent<PlayerMotor>();
            Vector3 facing = lookAt - destination;
            facing.y = 0f;
            if (playerMotor != null && facing.sqrMagnitude > 0.01f)
                playerMotor.SetFacing(-facing);
            Camera view = Camera.main;
            SimpleFollowCamera follow = view != null ? view.GetComponent<SimpleFollowCamera>() : null;
            if (follow != null && follow.Target == target)
                follow.SetTarget(target);
        }

        // ── Dibujo ─────────────────────────────────────────────────────

        private void OnGUI()
        {
            GUI.depth = open ? -20 : -6;
            if (atlas == null && fade <= 0f)
                return;
            MenuStyle.Begin();
            float w = MenuStyle.Width, h = MenuStyle.Height;
            if (atlas != null && !HudMode.Hidden)
            {
                if (regionsDirty)
                    RefreshVisitedRegions();
                if (open)
                    DrawMap(w, h);
                else if (!HudMode.Debug && fade <= 0f)
                    DrawMinimap();
            }
            if (fade > 0f && Event.current.type == EventType.Repaint)
                HudSkin.Fill(new Rect(-10f, -10f, w + 20f, h + 20f), new Color(0f, 0f, 0f, Mathf.Clamp01(fade)));
            MenuStyle.End();
        }

        private void RefreshVisitedRegions()
        {
            regionsDirty = false;
            visitedRegions.Clear();
            foreach (AtlasRegion region in atlas.Regions)
                if (region != null && ExplorationSave.Has(ExplorationSave.Regions, region.id))
                    visitedRegions.Add(region.id);
        }

        /// <summary>Dibuja la parte de una textura del mundo entero que cae dentro de la ventana.</summary>
        private void DrawWorldWindow(Texture texture, Rect rect, Vector2 windowMin, Vector2 windowMax, Color tint)
        {
            if (texture == null)
                return;
            Vector2 a = Vector2.Max(windowMin, boundsMin), b = Vector2.Min(windowMax, boundsMax);
            if (b.x <= a.x || b.y <= a.y)
                return;
            Vector2 uvA = ExplorationMath.WorldToMap01(a, boundsMin, boundsMax);
            Vector2 uvB = ExplorationMath.WorldToMap01(b, boundsMin, boundsMax);
            Vector2 size = windowMax - windowMin;
            float x0 = rect.x + (a.x - windowMin.x) / size.x * rect.width;
            float x1 = rect.x + (b.x - windowMin.x) / size.x * rect.width;
            float yTop = rect.y + (windowMax.y - b.y) / size.y * rect.height;
            float yBottom = rect.y + (windowMax.y - a.y) / size.y * rect.height;
            HudSkin.DrawPart(Rect.MinMaxRect(x0, yTop, x1, yBottom), texture,
                new Rect(uvA.x, uvA.y, uvB.x - uvA.x, uvB.y - uvA.y), tint);
        }

        private static Vector2 ToRect(Vector2 world, Rect rect, Vector2 windowMin, Vector2 windowMax)
        {
            Vector2 uv = ExplorationMath.WorldToMap01(world, windowMin, windowMax);
            return new Vector2(rect.x + uv.x * rect.width, rect.yMax - uv.y * rect.height);
        }

        private void DrawMap(float w, float h)
        {
            Event e = Event.current;
            const float top = 70f, bottom = 64f, side = 40f;
            Vector2 size = boundsMax - boundsMin;
            float availableWidth = w - side * 2f, availableHeight = h - top - bottom;
            float aspect = size.y > 0.01f ? size.x / size.y : 1f;
            float mw = availableWidth, mh = mw / aspect;
            if (mh > availableHeight)
            {
                mh = availableHeight;
                mw = mh * aspect;
            }
            Rect map = new Rect((w - mw) * 0.5f, top + (availableHeight - mh) * 0.5f, mw, mh);

            FindHover(map, e.mousePosition);
            if (e.type == EventType.MouseDown && e.button == 0 && hoverStone != null)
            {
                if (hoverStone.IsActivated)
                    TravelTo(hoverStone);
                else
                    HudNotices.Post("Activa antes esa piedra de viaje (" + GameInput.Label(GameAction.Gather) + " junto a ella)", HudNotices.Warning);
                e.Use();
                return;
            }
            if (e.type != EventType.Repaint)
                return;

            HudSkin.Fill(new Rect(-10f, -10f, w + 20f, h + 20f), new Color(0f, 0f, 0f, 0.78f));
            HudSkin.OrnateFrame(new Rect(map.x - 12f, map.y - 12f, map.width + 24f, map.height + 24f), 1f, true);
            if (mapTexture != null)
                HudSkin.Draw(map, mapTexture, Color.white);
            else
                DrawFallbackMap(map, boundsMin, boundsMax);
            HudSkin.Draw(map, fogTexture, Color.white);

            // Nombres de las regiones visitadas.
            foreach (AtlasRegion region in atlas.Regions)
            {
                if (region == null || !visitedRegions.Contains(region.id))
                    continue;
                Vector2 c = ToRect(region.centro, map, boundsMin, boundsMax);
                HudSkin.Label(new Rect(c.x - 150f, c.y - 11f, 300f, 22f), region.nombre.ToUpperInvariant(), 15,
                    new Color(HudSkin.GoldBright.r, HudSkin.GoldBright.g, HudSkin.GoldBright.b, 0.95f), TextAnchor.MiddleCenter, true);
                HudSkin.Label(new Rect(c.x - 150f, c.y + 9f, 300f, 16f), "Nv " + region.nivel, 12, HudSkin.TextDim);
            }
            DrawIcons(map, boundsMin, boundsMax, 1f);
            DrawPlayer(ToRect(ExplorationMath.Planar(player.position), map, boundsMin, boundsMax), 22f);

            HudSkin.Label(new Rect(0f, 18f, w, 30f), "MAPA DE VHALDREN", 26, HudSkin.GoldBright, TextAnchor.MiddleCenter, true);
            HudSkin.FadeLine(new Rect(w * 0.5f - 180f, 50f, 360f, 1.5f), HudSkin.Gold);
            HudSkin.Label(new Rect(0f, h - 46f, w, 20f),
                GameInput.Label(GameAction.Map) + ": cerrar  ·  clic en una piedra de viaje activada: viajar  ·  explorado " +
                Mathf.RoundToInt(ExploredFraction * 100f) + " %", 15, HudSkin.Text);

            if (hoverTitle != null)
            {
                float tw = Mathf.Max(HudSkin.TextWidth(hoverTitle, 16, true), HudSkin.TextWidth(hoverDetail ?? "", 13)) + 30f;
                Rect tip = new Rect(Mathf.Min(hoverPoint.x + 16f, w - tw - 10f), hoverPoint.y - 48f, tw, string.IsNullOrEmpty(hoverDetail) ? 30f : 48f);
                HudSkin.SoftPanel(tip, 0.85f);
                HudSkin.Label(new Rect(tip.x, tip.y + 4f, tip.width, 22f), hoverTitle, 16, HudSkin.GoldBright, TextAnchor.MiddleCenter, true);
                if (!string.IsNullOrEmpty(hoverDetail))
                    HudSkin.Label(new Rect(tip.x, tip.y + 25f, tip.width, 18f), hoverDetail, 13, HudSkin.Text);
            }
        }

        private void DrawFallbackMap(Rect rect, Vector2 windowMin, Vector2 windowMax)
        {
            HudSkin.Fill(rect, new Color(0.17f, 0.15f, 0.12f, 1f));
            float scale = rect.width / Mathf.Max(1f, windowMax.x - windowMin.x);
            foreach (AtlasRegion region in atlas.Regions)
            {
                if (region == null)
                    continue;
                Vector2 c = ToRect(region.centro, rect, windowMin, windowMax);
                float d = region.radio * 2f * scale;
                Color tint = Color.Lerp(region.colorNiebla, new Color(0.5f, 0.45f, 0.35f), 0.4f);
                tint.a = 0.55f;
                HudSkin.Draw(new Rect(c.x - d * 0.5f, c.y - d * 0.5f, d, d), HudSkin.Disc, tint);
            }
            Color water = new Color(0.22f, 0.42f, 0.62f, 0.9f);
            foreach (AtlasLake lake in atlas.Lakes)
            {
                if (lake == null)
                    continue;
                Vector2 c = ToRect(lake.centro, rect, windowMin, windowMax);
                float d = lake.radio * 2f * scale;
                HudSkin.Draw(new Rect(c.x - d * 0.5f, c.y - d * 0.5f, d, d), HudSkin.Disc, water);
            }
            foreach (AtlasRiver river in atlas.Rivers)
            {
                if (river == null || river.puntos == null)
                    continue;
                float d = Mathf.Max(2f, river.anchura * scale);
                for (int i = 0; i < river.puntos.Count - 1; i++)
                {
                    Vector2 a = ToRect(river.puntos[i], rect, windowMin, windowMax);
                    Vector2 b = ToRect(river.puntos[i + 1], rect, windowMin, windowMax);
                    int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(1.5f, d * 0.5f)), 1, 200);
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                        HudSkin.Draw(new Rect(p.x - d * 0.5f, p.y - d * 0.5f, d, d), HudSkin.Disc, water);
                    }
                }
            }
        }

        /// <summary>Iconos de lugares, piedras y bestias dentro de la ventana del mundo.</summary>
        private void DrawIcons(Rect rect, Vector2 windowMin, Vector2 windowMax, float iconScale)
        {
            foreach (PointOfInterest poi in PointOfInterest.All)
            {
                if (poi == null || !poi.IsDiscovered)
                    continue;
                Vector2 p = ToRect(ExplorationMath.Planar(poi.transform.position), rect, windowMin, windowMax);
                if (!rect.Contains(p))
                    continue;
                Color c = PointOfInterest.KindColor(poi.Kind, poi.IsSecret);
                HudSkin.Diamond(p, 15f * iconScale, new Color(0f, 0f, 0f, 0.8f));
                HudSkin.Diamond(p, 11f * iconScale, c);
                if (poi.IsSecret)
                    HudSkin.Circle(p, 5f * iconScale, Color.white);
            }
            foreach (MountSpawn spawn in MountSpawn.All)
            {
                if (spawn == null || spawn.IsTamed || spawn.WildModel == null)
                    continue;
                Vector2 world = ExplorationMath.Planar(spawn.WildModel.transform.position);
                if (fog == null || !fog.IsRevealedWorld(world))
                    continue;
                Vector2 p = ToRect(world, rect, windowMin, windowMax);
                if (!rect.Contains(p))
                    continue;
                HudSkin.Circle(p, 13f * iconScale, new Color(0f, 0f, 0f, 0.8f));
                HudSkin.Circle(p, 9f * iconScale, new Color(0.7f, 0.95f, 0.6f));
            }
            foreach (Waystone stone in Waystone.All)
            {
                if (stone == null)
                    continue;
                Vector2 world = ExplorationMath.Planar(stone.transform.position);
                if (!stone.IsActivated && (fog == null || !fog.IsRevealedWorld(world)))
                    continue;
                Vector2 p = ToRect(world, rect, windowMin, windowMax);
                if (!rect.Contains(p))
                    continue;
                bool lit = stone.IsActivated;
                bool hover = stone == hoverStone && open;
                float d = (lit ? 16f : 12f) * iconScale * (hover ? 1.3f : 1f);
                HudSkin.Diamond(p, d + 5f, new Color(0f, 0f, 0f, 0.85f));
                HudSkin.Diamond(p, d, lit ? StoneColor : new Color(0.5f, 0.55f, 0.6f));
                if (lit)
                    HudSkin.Diamond(p, d * 0.45f, Color.white);
            }
        }

        private void DrawPlayer(Vector2 point, float size)
        {
            Vector3 facing = motor != null ? motor.CurrentFacing : player.forward;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            HudSkin.Circle(point, size * (1.3f + pulse * 0.4f), new Color(1f, 0.85f, 0.45f, 0.25f));
            HudSkin.Circle(point, size * 0.55f, new Color(0f, 0f, 0f, 0.8f));
            float angle = ExplorationMath.ScreenAngle(facing);
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = saved * Matrix4x4.TRS(point, Quaternion.Euler(0f, 0f, angle), Vector3.one) *
                         Matrix4x4.TRS(-point, Quaternion.identity, Vector3.one);
            Rect r = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
            HudSkin.Draw(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), HudSkin.Arrow, new Color(0f, 0f, 0f, 0.85f));
            HudSkin.Draw(r, HudSkin.Arrow, HudSkin.GoldBright);
            GUI.matrix = saved;
        }

        private void FindHover(Rect map, Vector2 mouse)
        {
            hoverStone = null;
            hoverTitle = null;
            hoverDetail = null;
            float best = 16f * 16f;
            foreach (Waystone stone in Waystone.All)
            {
                if (stone == null)
                    continue;
                Vector2 world = ExplorationMath.Planar(stone.transform.position);
                if (!stone.IsActivated && (fog == null || !fog.IsRevealedWorld(world)))
                    continue;
                Vector2 p = ToRect(world, map, boundsMin, boundsMax);
                float d = (p - mouse).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    hoverStone = stone;
                    hoverPoint = p;
                    hoverTitle = stone.DisplayName;
                    hoverDetail = stone.IsActivated ? "Clic: viajar aquí" : "Piedra de viaje sin activar";
                }
            }
            foreach (PointOfInterest poi in PointOfInterest.All)
            {
                if (poi == null || !poi.IsDiscovered)
                    continue;
                Vector2 p = ToRect(ExplorationMath.Planar(poi.transform.position), map, boundsMin, boundsMax);
                float d = (p - mouse).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    hoverStone = null;
                    hoverPoint = p;
                    hoverTitle = poi.DisplayName;
                    hoverDetail = poi.IsSecret ? "Lugar secreto" : PointOfInterest.KindLabel(poi.Kind);
                }
            }
            foreach (MountSpawn spawn in MountSpawn.All)
            {
                if (spawn == null || spawn.IsTamed || spawn.WildModel == null)
                    continue;
                Vector2 world = ExplorationMath.Planar(spawn.WildModel.transform.position);
                if (fog == null || !fog.IsRevealedWorld(world))
                    continue;
                Vector2 p = ToRect(world, map, boundsMin, boundsMax);
                float d = (p - mouse).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    hoverStone = null;
                    hoverPoint = p;
                    hoverTitle = spawn.DisplayName;
                    hoverDetail = spawn.Flies ? "Bestia voladora salvaje: dómala" : "Bestia salvaje: dómala";
                }
            }
        }

        private void DrawMinimap()
        {
            if (Event.current.type != EventType.Repaint || player == null)
                return;
            Rect mini = new Rect(20f, 20f, MinimapSize, MinimapSize);
            float range = ExplorationUi.PlayerFlying ? FlightMinimapRange : MinimapRange;
            Vector2 center = ExplorationMath.Planar(player.position);
            Vector2 windowMin = center - Vector2.one * range, windowMax = center + Vector2.one * range;

            HudSkin.SoftPanel(new Rect(mini.x - 12f, mini.y - 12f, mini.width + 24f, mini.height + 50f), 0.5f);
            HudSkin.Fill(mini, new Color(0.08f, 0.07f, 0.06f, 0.9f));
            if (mapTexture != null)
                DrawWorldWindow(mapTexture, mini, windowMin, windowMax, Color.white);
            else
            {
                AtlasRegion here = atlas.RegionAt(player.position);
                if (here != null)
                    HudSkin.Fill(mini, new Color(here.colorNiebla.r, here.colorNiebla.g, here.colorNiebla.b, 0.35f));
            }
            DrawWorldWindow(fogTexture, mini, windowMin, windowMax, Color.white);
            DrawIcons(mini, windowMin, windowMax, 0.8f);
            DrawPlayer(mini.center, 18f);
            HudSkin.OrnateFrame(mini, 0.9f, false);
            HudSkin.Label(new Rect(mini.center.x - 10f, mini.y + 2f, 20f, 16f), "N", 13, HudSkin.GoldBright, TextAnchor.MiddleCenter, true);

            AtlasRegion region = atlas.RegionAt(player.position);
            if (region != null)
            {
                HudSkin.Label(new Rect(mini.x - 10f, mini.yMax + 6f, mini.width + 20f, 18f), region.nombre, 14, HudSkin.GoldBright, TextAnchor.MiddleCenter, true);
                HudSkin.Label(new Rect(mini.x - 10f, mini.yMax + 23f, mini.width + 20f, 14f),
                    "Nivel " + region.nivel + "  ·  " + GameInput.Label(GameAction.Map) + ": mapa", 11, HudSkin.TextDim);
            }
            else
                HudSkin.Label(new Rect(mini.x - 10f, mini.yMax + 8f, mini.width + 20f, 14f),
                    GameInput.Label(GameAction.Map) + ": mapa", 11, HudSkin.TextDim);
        }
    }
}
