using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Proyectil magico. Avanza con SphereCast cada frame para no atravesar objetivos a
    /// pocos FPS, golpea una sola vez y se detiene contra paredes.
    /// </summary>
    public sealed class SpellProjectile : MonoBehaviour
    {
        public const float Speed = 24f;

        private readonly RaycastHit[] hits = new RaycastHit[12];
        private PlayerSpellbook caster;
        private SpellDefinition spell;
        private float power;
        private Vector3 direction;
        private float travelled;
        private bool done;

        public static SpellProjectile Launch(PlayerSpellbook caster, SpellDefinition spell, float power,
            Vector3 origin, Vector3 direction)
        {
            Color tint = SpellCatalog.SchoolColor(spell.School);
            GameObject root = new GameObject("Hechizo " + spell.DisplayName);
            root.transform.position = origin;
            PrototypeVfx.CreateShape(PrimitiveType.Sphere, "Nucleo", root.transform, Vector3.zero,
                Vector3.one * (spell.Radius * 1.4f), tint);
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.widthMultiplier = spell.Radius * 1.1f;
            trail.sharedMaterial = PrototypeVfx.SharedMaterial;
            trail.startColor = tint;
            trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PrototypeVfx.CreatePointLight(root.transform, tint, 3.5f, 1.6f);

            SpellProjectile projectile = root.AddComponent<SpellProjectile>();
            projectile.caster = caster;
            projectile.spell = spell;
            projectile.power = power;
            direction.y = 0f;
            projectile.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            return projectile;
        }

        private void Update() => Step(Time.deltaTime);

        /// <summary>Avanza el proyectil. Separado de Update para poder probarlo.</summary>
        public void Step(float deltaTime)
        {
            if (done || spell == null)
                return;
            if (caster == null)
            {
                Finish(transform.position);
                return;
            }
            float step = Mathf.Min(Speed * Mathf.Max(0f, deltaTime), spell.Range - travelled);
            if (step <= 0f)
            {
                Finish(transform.position);
                return;
            }

            Vector3 start = transform.position;
            int count = Physics.SphereCastNonAlloc(start, spell.Radius, direction, hits, step,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Collider nearestCollider = null;
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) ||
                    hit.transform.IsChildOf(caster.transform))
                    continue;
                // distance 0 = ya solapado al empezar; cuenta como impacto inmediato.
                if (hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                    nearestCollider = hit;
                }
            }

            if (nearestCollider != null)
            {
                Vector3 impact = start + direction * nearest;
                CombatDamageReceiver receiver = nearestCollider.GetComponentInParent<CombatDamageReceiver>();
                if (receiver != null && !receiver.transform.IsChildOf(caster.transform))
                    caster.ApplySpellHit(receiver, spell, power, start);
                Finish(impact);
                return;
            }

            transform.position = start + direction * step;
            travelled += step;
            if (travelled >= spell.Range - 0.001f)
                Finish(transform.position);
        }

        public bool IsDone => done;

        private void Finish(Vector3 point)
        {
            if (done)
                return;
            done = true;
            if (spell != null)
                SpellImpactFlash.Spawn(point, SpellCatalog.SchoolColor(spell.School), 0.55f, 0.22f);
            Destroy(gameObject);
        }
    }
}
