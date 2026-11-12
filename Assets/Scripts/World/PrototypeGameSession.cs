using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
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

        [Header("Botin")]
        [SerializeField] private bool dropLoot = true;
        [SerializeField, Min(0f)] private float lootHealPerEssence = 8f;
        [Tooltip("Segundos que cada esencia recogida resta a la espera del estallido (E).")]
        [SerializeField, Min(0f)] private float areaCooldownRefundPerEssence = 1.5f;

        private readonly HashSet<PrototypeEnemy> defeated = new HashSet<PrototypeEnemy>();
        private PrototypeEnemy[] enemies;
        private CombatHealth playerHealth;
        private PlayerMotor playerMotor;
        private LocalPlayerShooter playerShooter;
        private bool playerShooterWasEnabled;
        private PlayerSpellCaster playerCaster;
        private bool playerCasterWasEnabled;
        private int essenceCollected;
        private string spellText = "";
        private string lootMessage = "";
        private float lootMessageUntil;
        private Vector3 spawnPosition;
        private float healReadyAt;
        private float respawnAt = float.PositiveInfinity;
        private string statusText = "";
        private string healText = "";
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
            playerHealth = player.GetComponent<CombatHealth>();
            playerMotor = player.GetComponent<PlayerMotor>();
            playerShooter = player.GetComponent<LocalPlayerShooter>();
            playerCaster = player.GetComponent<PlayerSpellCaster>();
            // Escenas generadas antes de las habilidades: se anaden en tiempo de juego.
            if (playerCaster == null && playerShooter != null && Application.isPlaying)
                playerCaster = player.gameObject.AddComponent<PlayerSpellCaster>();
            if (Application.isPlaying && player.GetComponent<SurvivalInventory>() == null)
                player.gameObject.AddComponent<SurvivalInventory>();
            // Recursos de supervivencia sobre la escena ya generada (sin regenerarla).
            if (Application.isPlaying)
                SurvivalWorldSeeder.Seed(gameObject.scene, player.position);
            essenceCollected = 0;
            if (playerHealth != null)
                playerHealth.Defeated += OnPlayerDefeated;

            enemies = FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None);
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
        }

        private void OnEnemyDefeated(PrototypeEnemy enemy)
        {
            if (enemy == null || !defeated.Add(enemy))
                return;
            if (enemy.IsBoss)
                bossDefeated = true;
            nextHudRefresh = 0f;
            if (dropLoot && Application.isPlaying)
            {
                Vector3 at = enemy.transform.position;
                PrototypeLootOrb.Spawn(this, player, new Vector3(at.x, 0.75f, at.z),
                    enemy.IsBoss ? 5 : 1, 0f, enemy.IsBoss);
            }
        }

        /// <summary>Esencia recogida: cura un poco y acorta la espera del estallido.</summary>
        public void CollectLoot(int essence, float extraHeal)
        {
            if (essence <= 0 || playerHealth == null || playerHealth.IsDefeated)
                return;
            essenceCollected += essence;
            float healed = playerHealth.Heal(essence * Mathf.Max(0f, lootHealPerEssence) +
                Mathf.Max(0f, extraHeal));
            float refund = essence * Mathf.Max(0f, areaCooldownRefundPerEssence);
            if (playerCaster != null)
                playerCaster.ReduceAreaCooldown(refund);
            lootMessage = "+" + essence + " esencia" + (healed > 0f
                ? "  (+" + healed.ToString("0") + " PV)" : "") +
                "  |  Estallido -" + refund.ToString("0.#") + " s";
            lootMessageUntil = Time.time + 2.5f;
            nextHudRefresh = 0f;
        }

        public int EssenceCollected => essenceCollected;

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
            if (playerCaster != null)
            {
                playerCasterWasEnabled = playerCaster.enabled;
                playerCaster.enabled = false;
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
            else if (WasHealPressed() && Time.time >= healReadyAt)
            {
                if (playerHealth.Heal(Mathf.Max(0f, healAmount)) > 0f)
                    healReadyAt = Time.time + Mathf.Max(0.1f, healCooldown);
            }

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
                    (bossDefeated ? "DERROTADO" : "PENDIENTE") +
                    " | Esencia " + essenceCollected;
                spellText = BuildSpellText();
                healText = Time.time >= healReadyAt ? "Q: curar +35 PV" :
                    "Q: pocion en " + Mathf.CeilToInt(healReadyAt - Time.time) + " s";
            }
        }

        private void RespawnPlayer()
        {
            CharacterController character = player.GetComponent<CharacterController>();
            if (character != null)
                character.enabled = false;
            player.position = spawnPosition;
            if (character != null)
                character.enabled = true;
            playerHealth.ResetToFull();
            if (playerMotor != null)
                playerMotor.enabled = true;
            if (playerShooter != null)
                playerShooter.enabled = playerShooterWasEnabled;
            if (playerCaster != null)
                playerCaster.enabled = playerCasterWasEnabled;
            respawnAt = float.PositiveInfinity;
        }

        private string BuildSpellText()
        {
            if (playerCaster == null)
                return "";
            float orb = playerCaster.ProjectileCooldownRemaining;
            float blast = playerCaster.AreaCooldownRemaining;
            string orbText = orb > 0f ? "Clic der: Orbe arcano " + orb.ToString("0.0") + " s"
                : "Clic der: Orbe arcano LISTO";
            string blastText = playerCaster.IsAreaCharging ? "E: Estallido CARGANDO..."
                : blast > 0f ? "E: Estallido en " + Mathf.CeilToInt(blast) + " s"
                : "E: Estallido de sangre LISTO";
            return orbText + "  |  " + blastText;
        }

        private void OnGUI()
        {
            if (playerHealth == null)
                return;
            const float height = 100f;
            Rect box = new Rect(12f, 12f, Mathf.Min(Screen.width - 24f, 850f), height);
            GUI.Box(box, "");
            GUI.Label(new Rect(22f, 16f, box.width - 20f, 22f), statusText);
            GUI.Label(new Rect(22f, 37f, box.width - 20f, 22f),
                "WASD mover | Espacio esquivar | 1-9 objeto + clic izq usar | I mochila | B construir | " + healText);
            if (playerCaster != null)
            {
                GUI.Label(new Rect(22f, 58f, box.width - 20f, 22f), spellText);
                DrawCooldownBar(new Rect(22f, 82f, 180f, 8f), playerCaster.ProjectileCooldown,
                    playerCaster.ProjectileCooldownRemaining, new Color(0.55f, 0.45f, 1f));
                DrawCooldownBar(new Rect(212f, 82f, 180f, 8f), playerCaster.AreaCooldown,
                    playerCaster.AreaCooldownRemaining, new Color(0.95f, 0.2f, 0.3f));
            }
            if (Time.time < lootMessageUntil)
                GUI.Box(new Rect(Screen.width / 2f - 170f, Screen.height - 170f, 340f, 28f),
                    lootMessage);
            if (playerHealth.IsDefeated)
                GUI.Box(new Rect(Screen.width / 2f - 165f, Screen.height / 2f - 40f,
                    330f, 80f), "HAS CAIDO\nReapareceras en el cruce");
            else if (bossDefeated && defeated.Count >= minionCount + 1)
                GUI.Box(new Rect(Screen.width / 2f - 180f, 102f, 360f, 60f),
                    "PRUEBA COMPLETADA\nHas derrotado a los guardianes y al jefe");
        }

        /// <summary>Barra llena = habilidad lista; se rellena durante la espera.</summary>
        private static void DrawCooldownBar(Rect rect, float total, float remaining, Color color)
        {
            float ready = total > 0f ? 1f - Mathf.Clamp01(remaining / total) : 1f;
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = remaining > 0f ? color * 0.7f : color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * ready, rect.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
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
