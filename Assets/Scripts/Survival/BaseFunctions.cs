using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Survival
{
    /// <summary>Fogata construida: cura poco a poco a quien descansa cerca y da luz.</summary>
    [DisallowMultipleComponent]
    public sealed class CampfireComfort : MonoBehaviour
    {
        public const float Radius = 4f;
        public const float HealPerSecond = 4f;

        private Light glow;
        private float seed;

        private void Awake()
        {
            GameObject lightObject = new GameObject("Luz de fogata");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.58f, 0.25f);
            glow.range = 9f;
            glow.intensity = 2.2f;
            seed = Random.value * 10f;
        }

        private void Update()
        {
            if (glow != null)
                glow.intensity = 2f + Mathf.PerlinNoise(seed, Time.time * 3f) * 0.8f;

            float squared = Radius * Radius;
            for (int i = 0; i < PlayerBuilder.All.Count; i++)
            {
                PlayerBuilder player = PlayerBuilder.All[i];
                if (player == null || (player.transform.position - transform.position).sqrMagnitude > squared)
                    continue;
                CombatHealth health = player.Health;
                if (health != null && !health.IsDefeated && health.CurrentHealth < health.MaximumHealth)
                    health.Heal(HealPerSecond * Time.deltaTime);
            }
        }
    }

    /// <summary>Cama construida: el jugador reaparece junto a ella.</summary>
    [DisallowMultipleComponent]
    public sealed class BedRespawnPoint : MonoBehaviour
    {
        private PrototypeGameSession session;

        private void OnEnable()
        {
            session = FindAnyObjectByType<PrototypeGameSession>();
            if (session != null)
                session.SetRespawnPoint(RespawnPosition);
        }

        private void OnDisable()
        {
            if (session != null)
                session.ClearRespawnPoint(RespawnPosition);
        }

        private Vector3 RespawnPosition => transform.position + transform.right * 1.3f + Vector3.up * 1.1f;
    }
}
