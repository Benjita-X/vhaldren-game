using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Reaccion de impacto breve SOLO en el objeto visual: se aplasta en proporcion al
    /// golpe, se inclina alejandose del atacante y destella un instante (mas en criticos).
    /// Nunca mueve CharacterController, nunca aplica dano y no toca la posicion del
    /// jugador o de los enemigos. Al terminar deja escala, giro y color como estaban.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class EnemyHitReaction : MonoBehaviour
    {
        public const float FlashSeconds = 0.09f;
        public const float MaxTiltDegrees = 14f;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Range(0f, 0.25f)] private float squash = 0.12f;
        [SerializeField, Min(0.05f)] private float recoverSeconds = 0.18f;

        private CombatHealth health;
        private CombatDamageReceiver receiver;
        private Transform visual;
        private Vector3 normalScale;
        private Quaternion normalRotation;
        private float struckAt;
        private bool recovering;
        private float strength = 1f;
        private Vector3 tiltAxis;
        private float tiltDegrees;

        private Renderer[] renderers;
        private Color[] baseColors;
        private bool[] hadBlockColor;
        private MaterialPropertyBlock block;
        private bool flashing;
        private float flashUntil;
        private float flashBoost;

        public bool Recovering => recovering;
        public bool Flashing => flashing;

        /// <summary>Fuerza relativa de la reaccion (0,55 golpe flojo, 1,6 golpe enorme).</summary>
        public static float ReactionStrength(float amount, float maxHealth, bool critical)
        {
            if (amount <= 0f || float.IsNaN(amount))
                return 0f;
            float share = Mathf.Clamp01(amount / Mathf.Max(1f, maxHealth));
            float value = Mathf.Lerp(0.55f, 1.6f, Mathf.Clamp01(share * 3f));
            return critical ? Mathf.Min(value * 1.3f, 1.8f) : value;
        }

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            receiver = GetComponent<CombatDamageReceiver>();
            BindVisual(transform.Find("Visual enemigo"));
        }

        public void BindVisual(Transform nextVisual)
        {
            Restore();
            visual = nextVisual;
            renderers = null;
            if (visual != null)
            {
                normalScale = visual.localScale;
                normalRotation = visual.localRotation;
            }
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

            bool critical = CombatHitContext.Resolving && CombatHitContext.Critical;
            bool overTime = CombatHitContext.Resolving && CombatHitContext.Kind == CombatDamageKind.DamageOverTime;
            float max = health != null ? health.MaximumHealth : 100f;
            strength = overTime ? 0.35f : ReactionStrength(amount, max, critical);

            if (!recovering)
            {
                normalScale = visual.localScale;
                normalRotation = visual.localRotation;
            }
            struckAt = Time.time;
            recovering = true;
            visual.localScale = normalScale * (1f - CurrentSquash());

            // Inclinacion alejandose de quien golpea (solo si se sabe de donde vino).
            tiltDegrees = 0f;
            if (!overTime)
            {
                if (receiver == null)
                    receiver = GetComponent<CombatDamageReceiver>();
                Vector3 source = CombatHitContext.Resolving ? CombatHitContext.Source :
                    receiver != null ? receiver.LastAttackSource : transform.position;
                Vector3 away = transform.position - source;
                away.y = 0f;
                if (away.sqrMagnitude > 0.01f && visual.parent != null)
                {
                    Vector3 local = visual.parent.InverseTransformDirection(away.normalized);
                    tiltAxis = Vector3.Cross(Vector3.up, local);
                    if (tiltAxis.sqrMagnitude > 0.0001f)
                    {
                        tiltAxis.Normalize();
                        tiltDegrees = MaxTiltDegrees * Mathf.Clamp(strength, 0f, 1.4f) / 1.4f;
                    }
                }
            }

            Flash(critical ? 2.4f : Mathf.Lerp(1.5f, 2.1f, Mathf.Clamp01(strength - 0.5f)),
                overTime ? FlashSeconds * 0.5f : FlashSeconds * (critical ? 1.5f : 1f));
        }

        private float CurrentSquash() => Mathf.Clamp(squash * Mathf.Max(0.3f, strength), 0f, 0.25f);

        private void OnDefeated() => Restore();

        private void LateUpdate()
        {
            if (flashing && Time.time >= flashUntil)
                EndFlash();
            if (!recovering || visual == null)
                return;

            float duration = Mathf.Max(0.05f, recoverSeconds);
            float progress = Mathf.Clamp01((Time.time - struckAt) / duration);
            float compression = Mathf.Clamp01(1f - progress);
            visual.localScale = normalScale * (1f - CurrentSquash() * compression * compression);
            if (tiltDegrees > 0.01f)
                visual.localRotation = Quaternion.AngleAxis(tiltDegrees * compression * compression, tiltAxis) * normalRotation;
            if (progress >= 1f)
                Restore();
        }

        // ── Destello ───────────────────────────────────────────────────

        private void Flash(float boost, float seconds)
        {
            if (visual == null)
                return;
            if (renderers == null)
                CacheRenderers();
            if (renderers == null || renderers.Length == 0)
                return;
            flashBoost = boost;
            flashUntil = Time.time + seconds;
            if (block == null)
                block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled)
                    continue;
                r.GetPropertyBlock(block);
                if (!flashing)
                {
                    // Recordar el color real (tinte del arquetipo o el del material).
                    hadBlockColor[i] = block.HasColor(ColorId);
                    baseColors[i] = hadBlockColor[i] ? block.GetColor(ColorId) :
                        r.sharedMaterial != null && r.sharedMaterial.HasProperty(ColorId)
                            ? r.sharedMaterial.GetColor(ColorId) : Color.white;
                }
                Color c = baseColors[i];
                block.SetColor(ColorId, new Color(c.r * flashBoost + 0.25f, c.g * flashBoost + 0.22f,
                    c.b * flashBoost + 0.2f, c.a));
                r.SetPropertyBlock(block);
            }
            flashing = true;
        }

        private void EndFlash()
        {
            if (!flashing)
                return;
            flashing = false;
            if (renderers == null || block == null)
                return;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, baseColors[i]);
                r.SetPropertyBlock(block);
            }
        }

        private void CacheRenderers()
        {
            Renderer[] found = visual.GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int i = 0; i < found.Length; i++)
                if (found[i] is MeshRenderer || found[i] is SkinnedMeshRenderer)
                    count++;
            renderers = new Renderer[count];
            baseColors = new Color[count];
            hadBlockColor = new bool[count];
            int k = 0;
            for (int i = 0; i < found.Length; i++)
                if (found[i] is MeshRenderer || found[i] is SkinnedMeshRenderer)
                    renderers[k++] = found[i];
        }

        private void Restore()
        {
            recovering = false;
            EndFlash();
            if (visual != null)
            {
                visual.localScale = normalScale;
                if (tiltDegrees > 0.01f)
                    visual.localRotation = normalRotation;
            }
            tiltDegrees = 0f;
        }
    }
}
