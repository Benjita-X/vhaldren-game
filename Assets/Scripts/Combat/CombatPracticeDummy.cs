using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Solo para la sala de practica: muestra vida sobre el objetivo y lo restaura.
    /// No controla ataques ni movimiento. No usar como autoridad de red.
    /// </summary>
    [RequireComponent(typeof(CombatHealth))]
    [DisallowMultipleComponent]
    public sealed class CombatPracticeDummy : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float respawnDelay = 3f;
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 1.8f, 0f);

        private CombatHealth health;
        private float respawnAt = float.PositiveInfinity;

        private void OnEnable()
        {
            health = GetComponent<CombatHealth>();
            health.Defeated += HandleDefeated;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Defeated -= HandleDefeated;
        }

        private void HandleDefeated()
        {
            float safeDelay = float.IsNaN(respawnDelay) || float.IsInfinity(respawnDelay)
                ? 3f : Mathf.Max(0.1f, respawnDelay);
            respawnAt = Time.time + safeDelay;
        }

        private void Update()
        {
            if (health == null || !health.IsDefeated || Time.time < respawnAt)
                return;

            health.ResetToFull();
            respawnAt = float.PositiveInfinity;
        }

        private void OnGUI()
        {
            if (health == null)
                return;
            Camera camera = Camera.main;
            if (camera == null)
                return;

            Vector3 screen = camera.WorldToScreenPoint(transform.position + labelOffset);
            if (screen.z <= 0f)
                return;

            const float width = 156f;
            const float height = 25f;
            Rect rect = new Rect(screen.x - width / 2f,
                Screen.height - screen.y - height, width, height);

            Color previousColor = GUI.color;
            GUI.color = health.IsDefeated
                ? new Color(0.9f, 0.55f, 0.55f)
                : new Color(0.85f, 1f, 0.8f);
            string label = health.IsDefeated
                ? "Objetivo derrotado"
                : $"Diana: {health.CurrentHealth:0}/{health.MaximumHealth:0} PV";
            GUI.Box(rect, label);
            GUI.color = previousColor;
        }
    }
}
