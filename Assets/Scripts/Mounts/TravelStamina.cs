using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Exploration;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Mounts
{
    /// <summary>
    /// Aguante de viaje del jugador (correr a pie con Mayús y galopar en montura), aparte de
    /// la resistencia de combate. Se recupera solo tras una breve pausa. Muestra una barra
    /// pequeña bajo el personaje mientras no esta lleno.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TravelStamina : MonoBehaviour
    {
        private static readonly Color BarColor = new Color(0.96f, 0.82f, 0.42f);
        private static readonly Color ExhaustedColor = new Color(0.95f, 0.35f, 0.28f);

        private readonly StaminaPool pool = new StaminaPool();
        private bool usedThisFrame;
        private float visibleUntil;
        private Camera view;
        private CharacterController body;
        private CombatHealth health;

        public StaminaPool Pool => pool;
        public float Fraction => pool.Fraction;
        public bool Exhausted => pool.Exhausted;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            health = GetComponent<CombatHealth>();
        }

        /// <summary>Gasta aguante para este paso. Devuelve false si esta agotado.</summary>
        public bool Use(float perSecond, float deltaTime)
        {
            if (float.IsNaN(perSecond) || float.IsNaN(deltaTime) || deltaTime <= 0f)
                return false;
            bool ok = pool.TryConsume(Mathf.Max(0f, perSecond) * deltaTime);
            if (ok)
                usedThisFrame = true;
            visibleUntil = Time.unscaledTime + 1.2f;
            return ok;
        }

        private void LateUpdate()
        {
            if (!usedThisFrame)
                pool.Regenerate(Time.deltaTime);
            usedThisFrame = false;
            if (pool.Fraction < 0.999f)
                visibleUntil = Mathf.Max(visibleUntil, Time.unscaledTime + 0.5f);
        }

        private void OnGUI()
        {
            GUI.depth = -5;
            if (Event.current.type != EventType.Repaint || HudMode.Hidden)
                return;
            float now = Time.unscaledTime;
            float alpha = Mathf.Clamp01((visibleUntil + 0.4f - now) / 0.4f);
            if (alpha <= 0.01f || (health != null && health.IsDefeated))
                return;
            if (view == null || !view.isActiveAndEnabled)
                view = Camera.main;
            if (view == null)
                return;
            Vector3 feet = body != null
                ? new Vector3(transform.position.x, body.bounds.min.y - 0.15f, transform.position.z)
                : transform.position;
            Vector3 screen = view.WorldToScreenPoint(feet);
            if (screen.z <= 0f)
                return;
            MenuStyle.Begin();
            float scale = MenuStyle.Scale;
            float x = screen.x / scale, y = (Screen.height - screen.y) / scale + 10f;
            const float width = 64f, height = 5f;
            Rect back = new Rect(x - width * 0.5f - 1f, y - 1f, width + 2f, height + 2f);
            HudSkin.Fill(back, new Color(0f, 0f, 0f, 0.6f * alpha));
            Color color = pool.Exhausted ? ExhaustedColor : BarColor;
            color.a = alpha;
            HudSkin.Fill(new Rect(x - width * 0.5f, y, width * pool.Fraction, height), color);
            MenuStyle.End();
        }
    }
}
