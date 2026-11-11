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

        private readonly HashSet<PrototypeEnemy> defeated = new HashSet<PrototypeEnemy>();
        private PrototypeEnemy[] enemies;
        private CombatHealth playerHealth;
        private PlayerMotor playerMotor;
        private LocalPlayerShooter playerShooter;
        private bool playerShooterWasEnabled;
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
                    if (enemy != null && enemy.gameObject.scene == gameObject.scene)
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
                    (bossDefeated ? "DERROTADO" : "PENDIENTE");
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
            respawnAt = float.PositiveInfinity;
        }

        private void OnGUI()
        {
            if (playerHealth == null)
                return;
            const float height = 75f;
            Rect box = new Rect(12f, 12f, Mathf.Min(Screen.width - 24f, 850f), height);
            GUI.Box(box, "");
            GUI.Label(new Rect(22f, 16f, box.width - 20f, 22f), statusText);
            GUI.Label(new Rect(22f, 37f, box.width - 20f, 22f),
                "WASD mover | Espacio esquivar | Mouse apuntar/disparar | " + healText);
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
