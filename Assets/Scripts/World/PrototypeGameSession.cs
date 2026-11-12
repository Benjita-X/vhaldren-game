using System.Collections.Generic;
using UnityEngine;
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
                if (GetComponent<DayNightCycle>() == null)
                    gameObject.AddComponent<DayNightCycle>();
                if (GameAutoCapture.RequestedFolder() != null && GetComponent<GameAutoCapture>() == null)
                    gameObject.AddComponent<GameAutoCapture>();

                // Solo el mundo usa flechas fisicas; las pruebas y la sala
                // de entrenamiento conservan el disparo legacy configurable.
                if (playerShooter != null)
                    playerShooter.SetPhysicalArrows(true);
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
                    if (candidate != null && candidate.GetComponent<Vhaldren.Gameplay.Magic.EssenceSource>() == null)
                        candidate.gameObject.AddComponent<Vhaldren.Gameplay.Magic.EssenceSource>();
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

        private void OnDisable()
        {
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
            if (enemy == null || !defeated.Add(enemy))
                return;
            if (enemy.IsBoss)
            {
                bossDefeated = true;
                villageJournal?.RecordAction(VillageObjective.DefeatBoss);
            }
            else
                villageJournal?.RecordAction(VillageObjective.DefeatGuardians);
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
                      " / MAGIA " + playerDamage.DamageFor(CombatDamageKind.Magic).ToString("0")
                    : "Estadisticas no disponibles";
            }
        }

        public float LastPotionHealed { get; private set; }

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

        private static bool WasHealPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Q);
#else
            return false;
#endif
        }
    }
}
