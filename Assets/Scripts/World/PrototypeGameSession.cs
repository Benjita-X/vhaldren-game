using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Survival;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Loop corto para probar un mundo: enemigos, jefe, vida, curacion y reaparicion.
    /// Una sola instancia para la escena de prueba. Ninguna logica de red.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeGameSession : MonoBehaviour
    {
        /// <summary>Camara cenital de accion del mundo: inclinacion hacia abajo, en grados.</summary>
        public const float ActionCameraPitch = 54f;
        /// <summary>Campo de vision vertical estrecho: aspecto casi isometrico.</summary>
        public const float ActionCameraFieldOfView = 40f;

        [SerializeField] private Transform player;
        [SerializeField] private float healAmount = 35f;
        [SerializeField, Min(0.1f)] private float healCooldown = 12f;
        [SerializeField, Min(0.1f)] private float playerRespawnDelay = 3f;

        private readonly HashSet<PrototypeEnemy> defeated = new HashSet<PrototypeEnemy>();
        private PrototypeEnemy[] enemies;
        private CombatHealth playerHealth;
        private PlayerMotor playerMotor;
        private LocalPlayerShooter playerShooter;
        private PlayerDamageTracker playerDamage;
        private VillageQuestJournal villageJournal;
        private GameObject ownedNoticeboard;
        private bool playerShooterWasEnabled;
        private Vector3 spawnPosition;
        private Vector3 originalSpawnPosition;
        private bool hasCustomSpawn;
        private float healReadyAt;
        private float respawnAt = float.PositiveInfinity;
        private string statusText = "";
        private string healText = "";
        private string lastHealFeedback = "";
        private float lastHealFeedbackUntil;
        private string combatText = "";
        private float nextHudRefresh;
        private int minionCount;
        private bool bossDefeated;
        private SimpleFollowCamera worldCamera;

        /// <summary>Camara del mundo configurada por esta sesion (null en salas de prueba).</summary>
        public SimpleFollowCamera WorldCamera => worldCamera;

        private void OnEnable()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.Find("PlayerRoot");
                if (playerObject != null)
                    player = playerObject.transform;
            }
            if (player == null)
                return;

            spawnPosition = player.position;
            originalSpawnPosition = spawnPosition;
            hasCustomSpawn = false;
            playerHealth = player.GetComponent<CombatHealth>();
            playerMotor = player.GetComponent<PlayerMotor>();
            playerShooter = player.GetComponent<LocalPlayerShooter>();
            // Crear antes de activar controles de arma o habilidad: todas las
            // fuentes notifican este contador con el daño REAL aplicado.
            playerDamage = player.GetComponent<PlayerDamageTracker>();
            if (Application.isPlaying && playerDamage == null)
                playerDamage = player.gameObject.AddComponent<PlayerDamageTracker>();
            if (Application.isPlaying && playerHealth != null)
            {
                CombatDamageReceiver receiver = player.GetComponent<CombatDamageReceiver>();
                if (receiver == null)
                    receiver = player.gameObject.AddComponent<CombatDamageReceiver>();
                CombatDefense protection = player.GetComponent<CombatDefense>();
                if (protection == null)
                    protection = player.gameObject.AddComponent<CombatDefense>();
                if (player.GetComponent<PlayerDefensiveInput>() == null)
                    player.gameObject.AddComponent<PlayerDefensiveInput>();
                if (player.GetComponent<PlayerCombatAbilities>() == null)
                    player.gameObject.AddComponent<PlayerCombatAbilities>();
                if (player.GetComponent<PlayerControlsGuide>() == null)
                    player.gameObject.AddComponent<PlayerControlsGuide>();
                // Magia de escuelas, esencia y estado de combate (019).
                if (player.GetComponent<Vhaldren.Gameplay.Magic.PlayerSpellbook>() == null)
                    player.gameObject.AddComponent<Vhaldren.Gameplay.Magic.PlayerSpellbook>();
                if (player.GetComponent<Vhaldren.Gameplay.Magic.PlayerFeeding>() == null)
                    player.gameObject.AddComponent<Vhaldren.Gameplay.Magic.PlayerFeeding>();
                if (GetComponent<GamePauseMenu>() == null)
                    gameObject.AddComponent<GamePauseMenu>();
                // Game feel solo en el mundo jugable: parones, temblor, chispas y sonido.
                GameFeel.Enabled = true;
                if (player.GetComponent<PlayerMovementFeedback>() == null)
                    player.gameObject.AddComponent<PlayerMovementFeedback>();
                // Raza elegida en el menu principal.
                if (player.GetComponent<Vhaldren.Gameplay.Races.PlayerRace>() == null)
                    player.gameObject.AddComponent<Vhaldren.Gameplay.Races.PlayerRace>()
                        .Configure(Vhaldren.Gameplay.Races.RaceSelection.Current);
                if (GetComponent<DayNightCycle>() == null)
                    gameObject.AddComponent<DayNightCycle>();
                if (player.GetComponent<SurvivalInventory>() != null &&
                    player.GetComponent<Vhaldren.Gameplay.Progression.PlayerEquipment>() == null)
                    player.gameObject.AddComponent<Vhaldren.Gameplay.Progression.PlayerEquipment>();
                // HUD 022: barra de acción única, viñeta, avisos, misión, estados y armas visibles.
                if (player.GetComponent<Vhaldren.Gameplay.UI.ActionBarHud>() == null)
                    player.gameObject.AddComponent<Vhaldren.Gameplay.UI.ActionBarHud>().Bind(this);
                Vhaldren.Gameplay.UI.ScreenVignette vignette = GetComponent<Vhaldren.Gameplay.UI.ScreenVignette>();
                if (vignette == null)
                    vignette = gameObject.AddComponent<Vhaldren.Gameplay.UI.ScreenVignette>();
                vignette.Bind(playerHealth);
                Vhaldren.Gameplay.UI.WorldHud worldHud = GetComponent<Vhaldren.Gameplay.UI.WorldHud>();
                if (worldHud == null)
                    worldHud = gameObject.AddComponent<Vhaldren.Gameplay.UI.WorldHud>();
                worldHud.Bind(player);
                if (player.GetComponent<CharacterLoadout>() == null)
                    player.gameObject.AddComponent<CharacterLoadout>();
                if (GameAutoCapture.RequestedFolder() != null && GetComponent<GameAutoCapture>() == null)
                    gameObject.AddComponent<GameAutoCapture>();

                // Solo el mundo usa flechas fisicas; las pruebas y la sala
                // de entrenamiento conservan el disparo legacy configurable.
                if (playerShooter != null)
                    playerShooter.SetPhysicalArrows(true);
            }
            if (Application.isPlaying)
            {
                // Camara cenital de accion solo en el mundo; las salas de prueba conservan la suya.
                ConfigureWorldCamera(true);
                GameSettings.Changed += OnSettingsChanged;
            }
            if (playerHealth != null)
            {
                CombatDamageFeedback incoming = player.GetComponent<CombatDamageFeedback>();
                if (incoming == null)
                    incoming = player.gameObject.AddComponent<CombatDamageFeedback>();
                incoming.ConfigureForPlayer();
            }
            if (Application.isPlaying && player.GetComponent<SurvivalInventory>() != null)
            {
                villageJournal = player.GetComponent<VillageQuestJournal>();
                if (villageJournal == null)
                    villageJournal = player.gameObject.AddComponent<VillageQuestJournal>();

                // Un primer punto de interacción y misiones conectado al mundo
                // existente. No genera otro inventario ni altera la escena 015.
                bool alreadyHasBoard = false;
                foreach (VillageNoticeboard candidate in
                    FindObjectsByType<VillageNoticeboard>(FindObjectsSortMode.None))
                {
                    if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                    {
                        alreadyHasBoard = true;
                        break;
                    }
                }
                if (!alreadyHasBoard)
                {
                    ownedNoticeboard = new GameObject("Puesto del Cruce - tablon de misiones");
                    ownedNoticeboard.SetActive(false);
                    ownedNoticeboard.transform.position = new Vector3(-4f, 0f, -6f);
                    VillageNoticeboard board = ownedNoticeboard.AddComponent<VillageNoticeboard>();
                    board.BindPlayer(player);
                    ownedNoticeboard.SetActive(true);
                }
            }
            if (playerHealth != null)
                playerHealth.Defeated += OnPlayerDefeated;

            enemies = FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None);
            if (Application.isPlaying)
                foreach (PrototypeEnemy candidate in enemies)
                {
                    if (candidate == null)
                        continue;
                    if (candidate.GetComponent<Vhaldren.Gameplay.Magic.EssenceSource>() == null)
                        candidate.gameObject.AddComponent<Vhaldren.Gameplay.Magic.EssenceSource>();
                    if (candidate.GetComponent<Vhaldren.Gameplay.Progression.CombatLevel>() == null)
                        candidate.gameObject.AddComponent<Vhaldren.Gameplay.Progression.CombatLevel>()
                            .Configure(Vhaldren.Gameplay.Progression.CombatLevel.LevelFor(candidate));
                    if (candidate.GetComponent<Vhaldren.Gameplay.Progression.EnemyLoot>() == null)
                        candidate.gameObject.AddComponent<Vhaldren.Gameplay.Progression.EnemyLoot>();
                    if (candidate.GetComponent<Vhaldren.Gameplay.Races.RacePowerTaken>() == null)
                        candidate.gameObject.AddComponent<Vhaldren.Gameplay.Races.RacePowerTaken>();
                    // Arma propia en la mano según el modelo (hacha, espada y escudo, bastón...).
                    if (candidate.GetComponent<CharacterLoadout>() == null)
                        candidate.gameObject.AddComponent<CharacterLoadout>();
                }
            minionCount = 0;
            defeated.Clear();
            bossDefeated = false;
            foreach (PrototypeEnemy enemy in enemies)
            {
                // No conectar enemigos de otras escenas cargadas aditivamente.
                if (enemy == null || enemy.gameObject.scene != gameObject.scene)
                    continue;
                if (!enemy.IsBoss)
                    minionCount++;
                enemy.Defeated += OnEnemyDefeated;
            }
            healReadyAt = 0f;
            respawnAt = float.PositiveInfinity;
            nextHudRefresh = 0f;
        }

        private void Start()
        {
            // Al cargar la escena, la camara puede activarse despues que esta sesion.
            if (Application.isPlaying && worldCamera == null)
                ConfigureWorldCamera(true);
        }

        private void OnSettingsChanged() => ConfigureWorldCamera(false);

        /// <summary>
        /// Encuadre del mundo: perspectiva inclinada con FOV estrecho, distancia por defecto
        /// de las opciones, zoom entre los limites, giro y adelanto segun las opciones.
        /// Los objetos que tapan al personaje se ocultan; solo el terreno acerca la camara.
        /// Solo toca la camara principal si sigue a este jugador.
        /// </summary>
        private void ConfigureWorldCamera(bool immediate)
        {
            if (player == null)
                return;
            if (worldCamera == null)
            {
                Camera view = Camera.main;
                SimpleFollowCamera follow = view != null ? view.GetComponent<SimpleFollowCamera>() : null;
                if (follow == null || follow.Target != player)
                    return;
                worldCamera = follow;
                immediate = true;
            }

            worldCamera.ConfigureActionView(ActionCameraPitch, GameSettings.CameraDistance,
                GameSettings.CameraDistanceMin, GameSettings.CameraDistanceMax,
                ActionCameraFieldOfView, immediate);
            worldCamera.AllowRotation = GameSettings.CameraRotation;
            worldCamera.LookAheadEnabled = GameSettings.CameraLookAhead;
            worldCamera.ObstructionTerrainOnly = true;

            CameraOcclusionHider hider = worldCamera.GetComponent<CameraOcclusionHider>();
            if (hider == null)
                hider = worldCamera.gameObject.AddComponent<CameraOcclusionHider>();
            hider.SetTarget(player);
        }

        private void OnDisable()
        {
            GameSettings.Changed -= OnSettingsChanged;
            GameFeel.Enabled = false;
            if (playerHealth != null)
                playerHealth.Defeated -= OnPlayerDefeated;
            if (enemies != null)
            {
                foreach (PrototypeEnemy enemy in enemies)
                {
                    // Desuscribir incluso si el enemigo cambio de escena;
                    // no dejar handlers retenidos tras una transferencia aditiva.
                    if (enemy != null)
                        enemy.Defeated -= OnEnemyDefeated;
                }
            }
            defeated.Clear();
            enemies = null;
            if (ownedNoticeboard != null)
            {
                Destroy(ownedNoticeboard);
                ownedNoticeboard = null;
            }
        }

        private void OnEnemyDefeated(PrototypeEnemy enemy)
        {
            if (enemy == null)
                return;

            // El HUD/victoria cuentan cada enemigo unico una sola vez, pero una
            // mision activa debe recibir TODA derrota posterior al aceptarla,
            // incluso si ese mismo enemigo ya murio antes y luego reaparecio.
            bool firstUniqueDefeat = defeated.Add(enemy);
            if (enemy.IsBoss)
            {
                if (firstUniqueDefeat)
                    bossDefeated = true;
                villageJournal?.RecordAction(VillageObjective.DefeatBoss);
            }
            else
                villageJournal?.RecordAction(VillageObjective.DefeatGuardians);

            if (firstUniqueDefeat)
                nextHudRefresh = 0f;
        }

        private void OnPlayerDefeated()
        {
            respawnAt = Time.time + Mathf.Max(0.1f, playerRespawnDelay);
            if (playerMotor != null)
                playerMotor.enabled = false;
            if (playerShooter != null)
            {
                playerShooterWasEnabled = playerShooter.enabled;
                playerShooter.enabled = false;
            }
        }

        private void Update()
        {
            if (playerHealth == null || player == null)
                return;

            if (playerHealth.IsDefeated)
            {
                if (Time.time >= respawnAt)
                    RespawnPlayer();
            }
            else if (WasHealPressed())
                TryDrinkPotion();

            if (Time.time >= nextHudRefresh)
            {
                nextHudRefresh = Time.time + 0.2f;
                int fallen = 0;
                foreach (PrototypeEnemy enemy in defeated)
                    if (enemy != null && !enemy.IsBoss)
                        fallen++;

                string region = player.position.z > 16f ? "Altar de sangre" :
                    player.position.x < -12f ? "Bosque oscuro" :
                    player.position.x > 12f ? "Ruinas" : "Cruce central";
                statusText = "VHALDREN | " + region + " | Vida " +
                    playerHealth.CurrentHealth.ToString("0") + "/" +
                    playerHealth.MaximumHealth.ToString("0") + " | Guardianes " +
                    fallen + "/" + minionCount + " | Jefe " +
                    (bossDefeated ? "DERROTADO" : "PENDIENTE");
                float missing = Mathf.Max(0f,
                    playerHealth.MaximumHealth - playerHealth.CurrentHealth);
                float availableHealing = Mathf.Min(Mathf.Max(0f, healAmount), missing);
                healText = Time.time < healReadyAt
                    ? "Q: poción en " + Mathf.CeilToInt(healReadyAt - Time.time) + " s"
                    : availableHealing > 0f
                        ? "Q: poción (recupera +" + availableHealing.ToString("0.##") + " PV)"
                        : "Q: vida llena (poción no consumida)";
                combatText = playerDamage != null
                    ? "GOLPES: " + playerDamage.TotalHits +
                      "  |  DANO TOTAL: " + playerDamage.TotalDamage.ToString("0.#") +
                      " PV  |  FLECHA " + playerDamage.DamageFor(CombatDamageKind.Arrow).ToString("0") +
                      " / ESPADA " + playerDamage.DamageFor(CombatDamageKind.Melee).ToString("0") +
                      " / MAGIA " + playerDamage.DamageFor(CombatDamageKind.Magic).ToString("0") +
                      " / DOT " + playerDamage.DamageFor(CombatDamageKind.DamageOverTime).ToString("0")
                    : "Estadisticas no disponibles";
            }
        }

        public float LastPotionHealed { get; private set; }

        /// <summary>Segundos hasta poder beber otra poción (barra de acción).</summary>
        public float PotionCooldownRemaining => Mathf.Max(0f, healReadyAt - Time.time);
        public float PotionCooldown => Mathf.Max(0.1f, healCooldown);
        public bool IsPlayerDefeated => playerHealth != null && playerHealth.IsDefeated;
        public float RespawnSecondsLeft => float.IsInfinity(respawnAt) ? 0f : Mathf.Max(0f, respawnAt - Time.time);
        public bool TrialComplete => bossDefeated && defeated.Count >= minionCount + 1;
        public int GuardiansDefeated
        {
            get
            {
                int fallen = 0;
                foreach (PrototypeEnemy enemy in defeated)
                    if (enemy != null && !enemy.IsBoss)
                        fallen++;
                return fallen;
            }
        }
        public int GuardianCount => minionCount;
        public bool BossDefeated => bossDefeated;

        /// <summary>
        /// Una pocion restaura hasta healAmount PV, nunca mas que la vida
        /// faltante. Deja feedback con el valor real y solo inicia cooldown
        /// si recupero vida. Se puede invocar desde Tests sin simular Input.
        /// </summary>
        public float TryDrinkPotion()
        {
            LastPotionHealed = 0f;
            if (playerHealth == null || playerHealth.IsDefeated)
                return 0f;

            if (Time.time < healReadyAt)
                lastHealFeedback = "Poción recargando";
            else
            {
                LastPotionHealed = playerHealth.Heal(Mathf.Max(0f, healAmount));
                if (LastPotionHealed > 0f)
                {
                    healReadyAt = Time.time + Mathf.Max(0.1f, healCooldown);
                    lastHealFeedback = "Poción: +" +
                        LastPotionHealed.ToString("0.##") + " PV reales";
                }
                else
                    lastHealFeedback = "Vida completa: no se gastó la poción";
            }
            lastHealFeedbackUntil = Time.time + 2.0f;
            nextHudRefresh = 0f;
            Vhaldren.Gameplay.UI.HudNotices.Post(lastHealFeedback, LastPotionHealed > 0f
                ? new Color(1f, 0.62f, 0.62f) : Vhaldren.Gameplay.UI.HudNotices.Warning);
            return LastPotionHealed;
        }

        /// <summary>Una cama construida cambia el punto de reaparicion.</summary>
        public void SetRespawnPoint(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
                return;
            if (!hasCustomSpawn)
                originalSpawnPosition = spawnPosition;
            spawnPosition = position;
            hasCustomSpawn = true;
        }

        /// <summary>Al desmontar la cama activa se vuelve al punto inicial.</summary>
        public void ClearRespawnPoint(Vector3 position)
        {
            if (!hasCustomSpawn || (spawnPosition - position).sqrMagnitude > 0.01f)
                return;
            spawnPosition = originalSpawnPosition;
            hasCustomSpawn = false;
        }

        public Vector3 RespawnPoint => spawnPosition;

        private void RespawnPlayer()
        {
            CharacterController character = player.GetComponent<CharacterController>();
            if (character != null)
                character.enabled = false;
            player.position = spawnPosition;
            if (character != null)
                character.enabled = true;
            playerHealth.ResetToFull();
            player.GetComponent<Vhaldren.Gameplay.Magic.PlayerSpellbook>()?.ResetCooldowns();
            if (playerMotor != null)
                playerMotor.enabled = true;
            if (playerShooter != null)
                playerShooter.enabled = playerShooterWasEnabled;
            respawnAt = float.PositiveInfinity;
        }

        private void OnGUI()
        {
            if (playerHealth == null)
                return;
            if (!Vhaldren.Gameplay.UI.HudMode.Debug)
            {
                DrawOutcome();
                return;
            }
            const float height = 114f;
            Rect box = new Rect(12f, 12f, Mathf.Max(240f, Mathf.Min(Screen.width - 24f, 900f)), height);
            GUI.Box(box, "");
            GUI.Label(new Rect(22f, 16f, box.width - 20f, 22f), statusText);
            GUI.Label(new Rect(22f, 40f, box.width - 20f, 22f), combatText);
            GUI.Label(new Rect(22f, 65f, box.width - 20f, 22f),
                "WASD mover | Espacio esquivar | Clic ballesta | " + healText);
            if (Time.time < lastHealFeedbackUntil)
                GUI.Label(new Rect(22f, 88f, box.width - 20f, 22f),
                    lastHealFeedback);
            if (playerHealth.IsDefeated)
                GUI.Box(new Rect(Screen.width / 2f - 165f, Screen.height / 2f - 40f,
                    330f, 80f), "HAS CAIDO\nReapareceras en el cruce");
            else if (bossDefeated && defeated.Count >= minionCount + 1)
                GUI.Box(new Rect(Screen.width / 2f - 180f, 102f, 360f, 60f),
                    "PRUEBA COMPLETADA\nHas derrotado a los guardianes y al jefe");
        }

        /// <summary>Caída y victoria: texto grande con contorno, sin cajas opacas.</summary>
        private void DrawOutcome()
        {
            if (Event.current.type != EventType.Repaint || Vhaldren.Gameplay.UI.HudMode.Hidden)
                return;
            string title = null, subtitle = null;
            Color tint = Vhaldren.Gameplay.UI.HudSkin.GoldBright;
            if (playerHealth.IsDefeated)
            {
                title = "HAS CAÍDO";
                subtitle = "Reaparecerás en " + Mathf.CeilToInt(RespawnSecondsLeft) + " s";
                tint = new Color(0.95f, 0.3f, 0.3f);
            }
            else if (TrialComplete)
            {
                title = "PRUEBA COMPLETADA";
                subtitle = "Has derrotado a los guardianes y al jefe";
            }
            if (title == null)
                return;
            Vhaldren.Gameplay.UI.MenuStyle.Begin();
            float w = Vhaldren.Gameplay.UI.MenuStyle.Width;
            float y = playerHealth.IsDefeated ? Vhaldren.Gameplay.UI.MenuStyle.Height * 0.36f : 130f;
            Vhaldren.Gameplay.UI.HudSkin.SoftPanel(new Rect(w * 0.5f - 300f, y - 10f, 600f, 92f), 0.6f);
            Vhaldren.Gameplay.UI.HudSkin.Label(new Rect(0f, y, w, 46f), title, 40, tint, TextAnchor.MiddleCenter, true);
            Vhaldren.Gameplay.UI.HudSkin.FadeLine(new Rect(w * 0.5f - 200f, y + 50f, 400f, 1.5f), Vhaldren.Gameplay.UI.HudSkin.Gold);
            Vhaldren.Gameplay.UI.HudSkin.Label(new Rect(0f, y + 54f, w, 24f), subtitle, 17, Vhaldren.Gameplay.UI.HudSkin.Text);
            Vhaldren.Gameplay.UI.MenuStyle.End();
        }

        private static bool WasHealPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.Potion);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Q);
#else
            return false;
#endif
        }
    }
}
