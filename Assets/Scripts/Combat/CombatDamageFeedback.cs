using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Feedback visual sin modificar las reglas de combate:
    /// vida sobre enemigos, numeros flotantes por dano REAL aplicado y aviso de derrota.
    /// El HUD usa IMGUI solo para la primera prueba jugable; no crea materiales
    /// ni texturas por enemigo. Los datos vienen de CombatHealth, nunca del clic.
    /// </summary>
    [RequireComponent(typeof(CombatHealth))]
    [DisallowMultipleComponent]
    public sealed class CombatDamageFeedback : MonoBehaviour
    {
        private const int PopupCapacity = 6;
        private const float PopupLifetime = 1.05f;

        private struct DamagePopup
        {
            public string Text;
            public float Time;
            public int Lane;
            public bool Healing;
            public bool Defended;
            public bool Critical;
            public bool OverTime;
            public bool Magic;
            public float Scale;
        }

        /// <summary>Tamaño relativo de un número: los golpes que quitan más vida se leen más grandes.</summary>
        public static float PopupScale(float amount, float maxHealth, bool critical, bool overTime)
        {
            if (overTime)
                return 0.72f;
            float share = maxHealth > 0f ? Mathf.Clamp01(amount / maxHealth) : 0f;
            float scale = Mathf.Lerp(0.95f, 1.35f, Mathf.Clamp01(share * 2.5f));
            return critical ? scale * 1.45f : scale;
        }

        /// <summary>Texto del número: entero (sin decimales que ensucien); crítico con exclamación.</summary>
        public static string PopupText(float amount, bool critical, bool healing)
        {
            string value = amount >= 1f ? Mathf.RoundToInt(amount).ToString() : amount.ToString("0.#");
            if (healing)
                return "+" + value;
            return critical ? "¡" + value + "!" : "-" + value;
        }

        [SerializeField, Min(2f)] private float maximumDisplayDistance = 42f;
        [SerializeField] private bool showBarAtFullHealth = true;
        [SerializeField] private bool playerTarget;
        [SerializeField] private bool bossTarget;
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 1.65f, 0f);

        private readonly DamagePopup[] popups = new DamagePopup[PopupCapacity];
        private CombatHealth health;
        private Camera view;
        private GUIStyle nameStyle;
        private GUIStyle numberStyle;
        private int nextPopup;
        private float defeatAt = float.NegativeInfinity;
        private float lastHitAt = float.NegativeInfinity;
        private float cachedHealth = float.NaN;
        private float cachedMaxHealth = float.NaN;
        private string healthText;
        private string targetName;

        public int DamageEventsReceived { get; private set; }
        public float LastDamageReceived { get; private set; }
        public float TotalDamageReceived { get; private set; }
        public int HealingEventsReceived { get; private set; }
        public float LastHealingReceived { get; private set; }
        public float HealthFraction
        {
            get
            {
                CombatHealth value = health != null ? health : GetComponent<CombatHealth>();
                if (value == null || value.MaximumHealth <= 0f)
                    return 0f;
                return Mathf.Clamp01(value.CurrentHealth / value.MaximumHealth);
            }
        }

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            targetName = gameObject.name;
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Healed += OnHealed;
                health.Defeated += OnDefeated;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Healed -= OnHealed;
                health.Defeated -= OnDefeated;
            }
        }

        /// <summary>Altura de la barra de vida sobre el personaje (el HUD coloca encima los estados).</summary>
        public Vector3 LabelOffset => labelOffset;
        public bool IsBossBar => bossTarget;

        public void ConfigureForEnemy(bool isBoss)
        {
            playerTarget = false;
            bossTarget = isBoss;
            labelOffset = isBoss
                ? new Vector3(0f, 2.15f, 0f)
                : new Vector3(0f, 1.65f, 0f);
            targetName = gameObject.name;
        }

        public void ConfigureForPlayer()
        {
            playerTarget = true;
            bossTarget = false;
            labelOffset = new Vector3(0f, 1.55f, 0f);
            targetName = "JUGADOR";
        }

        private void OnDamaged(float actualAmount)
        {
            if (float.IsNaN(actualAmount) || float.IsInfinity(actualAmount) || actualAmount <= 0f)
                return;

            LastDamageReceived = actualAmount;
            TotalDamageReceived += actualAmount;
            DamageEventsReceived++;
            lastHitAt = Time.time;

            // Buffer fijo: al saturar solo reemplaza el aviso mas antiguo.
            // Formatear texto una sola vez por golpe, no en cada OnGUI.
            // El receptor publica tipo y critico del golpe que se aplica ahora mismo.
            bool resolving = CombatHitContext.Resolving;
            bool critical = resolving && CombatHitContext.Critical && !playerTarget;
            bool overTime = resolving && CombatHitContext.Kind == CombatDamageKind.DamageOverTime;
            bool magic = resolving && (CombatHitContext.Kind == CombatDamageKind.Magic ||
                                       CombatHitContext.Kind == CombatDamageKind.Explosion);
            int index = nextPopup % PopupCapacity;
            nextPopup++;
            popups[index] = new DamagePopup
            {
                Text = PopupText(actualAmount, critical, false),
                Time = Time.time,
                Lane = index,
                Healing = false,
                Defended = false,
                Critical = critical,
                OverTime = overTime,
                Magic = magic,
                Scale = PopupScale(actualAmount, health != null ? health.MaximumHealth : 100f, critical, overTime)
            };
            cachedHealth = float.NaN;
        }

        private void OnHealed(float actualAmount)
        {
            if (float.IsNaN(actualAmount) || float.IsInfinity(actualAmount) || actualAmount <= 0f)
                return;
            LastHealingReceived = actualAmount;
            HealingEventsReceived++;

            int index = nextPopup % PopupCapacity;
            nextPopup++;
            popups[index] = new DamagePopup
            {
                Text = PopupText(actualAmount, false, true),
                Time = Time.time,
                Lane = index,
                Healing = true,
                Defended = false,
                Scale = 1f
            };
            cachedHealth = float.NaN;
        }

        /// <summary>Mostrar una defensa efectiva sin inventar daño recibido.</summary>
        public void ShowDefenseResult(float prevented, bool parry, bool evaded)
        {
            if (float.IsNaN(prevented) || float.IsInfinity(prevented) || prevented <= 0f)
                return;
            int index = nextPopup % PopupCapacity;
            nextPopup++;
            popups[index] = new DamagePopup
            {
                Text = evaded ? "ESQUIVA" : parry ? "PARADA" :
                    "RESISTE " + prevented.ToString("0.#"),
                Time = Time.time,
                Lane = index,
                Healing = false,
                Defended = true,
                Scale = parry ? 1.1f : 0.85f
            };
        }

        private void OnDefeated()
        {
            defeatAt = Time.time;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || health == null)
                return;

            if (view == null)
                view = Camera.main;
            if (view == null)
                return;

            if (maximumDisplayDistance > 0f &&
                (view.transform.position - transform.position).sqrMagnitude >
                maximumDisplayDistance * maximumDisplayDistance)
                return;

            Vector3 screen = view.WorldToScreenPoint(transform.position + labelOffset);
            if (screen.z <= 0f || screen.x < -150f || screen.x > Screen.width + 150f ||
                screen.y < -100f || screen.y > Screen.height + 100f)
                return;

            EnsureStyles();

            float x = screen.x;
            float y = Screen.height - screen.y;
            float current = health.CurrentHealth;
            float maximum = health.MaximumHealth;
            bool dead = health.IsDefeated;
            bool showDeath = dead && Time.time - defeatAt < 1.5f;

            if (!playerTarget && ((!dead && (showBarAtFullHealth ||
                current < maximum || Time.time - lastHitAt < 4f)) || showDeath))
                DrawHealthBar(x, y, current, maximum, dead);

            DrawDamagePopups(x, y);
        }

        private void DrawHealthBar(float x, float y, float current, float maximum, bool dead)
        {
            float width = bossTarget ? 214f : 146f;
            float barHeight = bossTarget ? 13f : 10f;
            Rect border = new Rect(x - width * 0.5f - 2f, y - 6f, width + 4f, barHeight + 4f);
            Rect background = new Rect(x - width * 0.5f, y - 4f, width, barHeight);

            if (cachedHealth != current || cachedMaxHealth != maximum || healthText == null)
            {
                cachedHealth = current;
                cachedMaxHealth = maximum;
                healthText = current.ToString("0.##") + " / " + maximum.ToString("0.##") + " PV";
            }

            Color previous = GUI.color;
            DrawRect(border, new Color(0f, 0f, 0f, 0.9f));
            DrawRect(background, new Color(0.14f, 0.10f, 0.15f, 0.95f));
            float portion = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            if (portion > 0f)
                DrawRect(new Rect(background.x, background.y,
                    background.width * portion, background.height),
                    Time.time - lastHitAt < 0.12f
                        ? new Color(1f, 0.72f, 0.28f, 1f)
                        : bossTarget
                            ? new Color(0.69f, 0.22f, 0.76f, 1f)
                            : new Color(0.88f, 0.22f, 0.26f, 1f));

            string label = dead ? "DERROTADO" : (targetName ?? gameObject.name);
            GUI.color = Color.white;
            GUI.Label(new Rect(x - width * 0.5f - 30f, y - 40f, width + 60f, 24f),
                label, nameStyle);
            if (!dead)
                GUI.Label(new Rect(x - width * 0.5f - 15f, y + 6f, width + 30f, 20f),
                    healthText, nameStyle);
            GUI.color = previous;
        }

        private void DrawDamagePopups(float x, float y)
        {
            Color previous = GUI.color;
            // Opcion del menu: ocultar los numeros flotantes de daño y curacion.
            int baseSize = bossTarget ? 27 : 23;
            for (int i = 0; Vhaldren.Gameplay.Settings.GameSettings.DamageNumbers && i < PopupCapacity; i++)
            {
                DamagePopup popup = popups[i];
                if (popup.Text == null)
                    continue;
                float lifetime = popup.Critical ? PopupLifetime * 1.3f : PopupLifetime;
                float age = Time.time - popup.Time;
                if (age < 0f || age >= lifetime)
                    continue;

                float t = Mathf.Clamp01(age / lifetime);
                // Opaco la primera mitad y se desvanece despues: se lee mejor.
                float fade = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
                float spread = (popup.Lane - 2.5f) * 16f;
                // Sube rapido y frena (ease-out); el critico sube menos y late al aparecer.
                float rise = (1f - (1f - t) * (1f - t)) * (popup.Critical ? 48f : 62f);
                float pop = popup.Critical
                    ? 1f + 0.55f * Mathf.Clamp01(1f - age / 0.14f)
                    : 1f + 0.2f * Mathf.Clamp01(1f - age / 0.1f);
                float scale = Mathf.Max(0.5f, popup.Scale > 0f ? popup.Scale : 1f) * pop;
                Color color = popup.Healing
                    ? new Color(0.35f, 1f, 0.54f, fade)
                    : popup.Defended
                        ? new Color(0.38f, 0.86f, 1f, fade)
                        : playerTarget
                            ? new Color(1f, 0.3f, 0.35f, fade)
                            : popup.Critical
                                ? new Color(1f, 0.58f, 0.12f, fade)
                                : popup.OverTime
                                    ? new Color(1f, 0.55f, 0.2f, fade * 0.9f)
                                    : popup.Magic
                                        ? new Color(0.86f, 0.84f, 1f, fade)
                                        : new Color(1f, 0.9f, 0.45f, fade);
                numberStyle.fontSize = Mathf.RoundToInt(baseSize * scale);
                float w = 150f * scale, hgt = 44f * scale;
                float shake = popup.Critical && age < 0.18f ? Mathf.Sin(age * 90f) * 2.5f : 0f;
                Rect box = new Rect(x - w * 0.5f + spread + shake, y - 56f - rise - (hgt - 36f) * 0.5f, w, hgt);
                // Contorno oscuro: legible sobre hierba, nieve o fuego.
                GUI.color = new Color(0f, 0f, 0f, 0.8f * fade);
                GUI.Label(new Rect(box.x + 2f, box.y + 2f, box.width, box.height), popup.Text, numberStyle);
                GUI.Label(new Rect(box.x - 1.5f, box.y + 1.5f, box.width, box.height), popup.Text, numberStyle);
                GUI.Label(new Rect(box.x + 1.5f, box.y - 1f, box.width, box.height), popup.Text, numberStyle);
                GUI.color = color;
                GUI.Label(box, popup.Text, numberStyle);
            }
            numberStyle.fontSize = baseSize;
            GUI.color = previous;
        }

        private static void DrawRect(Rect rectangle, Color tint)
        {
            GUI.color = tint;
            GUI.DrawTexture(rectangle, Texture2D.whiteTexture);
        }

        private void EnsureStyles()
        {
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = bossTarget ? 13 : 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
                nameStyle.normal.textColor = Color.white;
            }
            if (numberStyle == null)
            {
                numberStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = bossTarget ? 27 : 23,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Overflow,
                    wordWrap = false
                };
                numberStyle.normal.textColor = Color.white;
            }
        }
    }
}
