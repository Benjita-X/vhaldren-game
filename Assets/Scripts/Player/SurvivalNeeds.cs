using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Hambre sencilla con solucion jugable: baja con el tiempo, las bayas la recuperan
    /// y una hoguera construida cerca cura y la hace bajar mas despacio. Con hambre 0
    /// se pierde vida poco a poco (nunca mata de golpe).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalNeeds : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHunger = 100f;
        [Tooltip("Segundos para pasar de lleno a vacio sin hoguera.")]
        [SerializeField, Min(10f)] private float secondsToStarve = 420f;
        [SerializeField, Min(0f)] private float starvingDamagePerSecond = 0.7f;
        [SerializeField, Min(0f)] private float campfireHealPerSecond = 1.5f;
        [SerializeField, Min(0.5f)] private float campfireRadius = 4.5f;

        private CombatHealth health;
        private bool wasDefeated;
        private float pendingDamage;

        public float Hunger { get; private set; }
        public float MaxHunger => Mathf.Max(1f, maxHunger);
        public bool NearCampfire { get; private set; }
        public bool IsStarving => Hunger <= 0f;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            Hunger = MaxHunger;
        }

        public bool Eat(float amount)
        {
            if (amount <= 0f || Hunger >= MaxHunger - 0.5f)
                return false;
            Hunger = Mathf.Min(MaxHunger, Hunger + amount);
            return true;
        }

        private void Update()
        {
            if (health != null)
            {
                if (health.IsDefeated)
                {
                    wasDefeated = true;
                    return;
                }
                if (wasDefeated)
                {
                    // Tras reaparecer: algo de hambre, no la barra vacia.
                    wasDefeated = false;
                    Hunger = Mathf.Max(Hunger, MaxHunger * 0.6f);
                }
            }

            NearCampfire = BuiltPiece.IsNearCampfire(transform.position, campfireRadius);
            float drain = MaxHunger / Mathf.Max(10f, secondsToStarve) * (NearCampfire ? 0.5f : 1f);
            Hunger = Mathf.Max(0f, Hunger - drain * Time.deltaTime);

            if (health == null)
                return;
            if (NearCampfire && !IsStarving)
                health.Heal(campfireHealPerSecond * Time.deltaTime);
            if (IsStarving)
            {
                pendingDamage += starvingDamagePerSecond * Time.deltaTime;
                if (pendingDamage >= 1f)
                {
                    health.ApplyDamage(Mathf.Floor(pendingDamage));
                    pendingDamage -= Mathf.Floor(pendingDamage);
                }
            }
        }

        private void OnGUI()
        {
            if (health != null && health.IsDefeated)
                return;
            float width = Mathf.Min(Screen.width - 16f, 720f);
            float left = (Screen.width - width) / 2f;
            Rect bar = new Rect(left, Screen.height - 98f, 200f, 10f);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = IsStarving ? Color.red : new Color(0.95f, 0.65f, 0.2f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Hunger / MaxHunger, bar.height), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(bar.xMax + 8f, bar.y - 6f, 360f, 22f),
                "Hambre " + Mathf.CeilToInt(Hunger) + (IsStarving ? "  ¡COME ALGO!" : "") +
                (NearCampfire ? "  ·  Hoguera: curando" : ""));
        }
    }
}
