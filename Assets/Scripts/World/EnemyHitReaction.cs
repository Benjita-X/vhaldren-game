using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Reaccion de impacto breve SOLO en el objeto visual.
    /// Nunca mueve CharacterController, nunca aplica dano y no toca
    /// la posicion/rotacion del jugador o de los enemigos.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class EnemyHitReaction : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.25f)] private float squash = 0.12f;
        [SerializeField, Min(0.05f)] private float recoverSeconds = 0.18f;

        private CombatHealth health;
        private Transform visual;
        private Vector3 normalScale;
        private float struckAt;
        private bool recovering;

        public bool Recovering => recovering;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            BindVisual(transform.Find("Visual enemigo"));
        }

        public void BindVisual(Transform nextVisual)
        {
            Restore();
            visual = nextVisual;
            if (visual != null)
                normalScale = visual.localScale;
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Defeated += OnDefeated;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Defeated -= OnDefeated;
            }
            Restore();
        }

        private void OnDamaged(float amount)
        {
            if (amount <= 0f || visual == null)
                return;

            struckAt = Time.time;
            recovering = true;
            visual.localScale = normalScale * (1f - Mathf.Clamp(squash, 0f, 0.25f));
        }

        private void OnDefeated() => Restore();

        private void Update()
        {
            if (!recovering || visual == null)
                return;

            float duration = Mathf.Max(0.05f, recoverSeconds);
            float progress = Mathf.Clamp01((Time.time - struckAt) / duration);
            float compression = Mathf.Clamp01(1f - progress);
            visual.localScale = normalScale *
                (1f - Mathf.Clamp(squash, 0f, 0.25f) * compression * compression);
            if (progress >= 1f)
                Restore();
        }

        private void Restore()
        {
            recovering = false;
            if (visual != null)
                visual.localScale = normalScale;
        }
    }
}
