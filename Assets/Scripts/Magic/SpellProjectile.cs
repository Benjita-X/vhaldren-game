using System;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Proyectil magico. Avanza con SphereCast cada frame para no atravesar objetivos a
    /// pocos FPS, golpea una sola vez y se detiene contra paredes. Se reutiliza: al
    /// impactar deja que su estela se apague y vuelve a la reserva en vez de destruirse.
    /// Su aspecto (nucleo, halo, cuerpo, estela y particulas de la escuela) es un <see cref="VfxOrb"/>.
    /// </summary>
    public sealed class SpellProjectile : MonoBehaviour
    {
        public const float Speed = 24f;
        /// <summary>El radio de golpe es menor que el brillo visible: aciertos justos, no regalados.</summary>
        public const float HitRadiusFactor = 0.6f;
        private static readonly Func<Transform, SpellProjectile> Factory = Create;

        private readonly RaycastHit[] hits = new RaycastHit[12];
        private PlayerSpellbook caster;
        private SpellDefinition spell;
        private float power;
        private Vector3 direction;
        private float travelled;
        private bool done;
        private float recycleAt = float.PositiveInfinity;
        private VfxOrb orb;
        private VfxPool<SpellProjectile> pool;

        public bool IsDone => done;
        public Vector3 LastImpactPoint { get; private set; }
        public SpellDefinition Spell => spell;
        public VfxOrb Visual => orb;

        public static SpellProjectile Launch(PlayerSpellbook caster, SpellDefinition spell, float power,
            Vector3 origin, Vector3 direction)
        {
            VfxHost host = VfxHost.Get();
            VfxPool<SpellProjectile> reserve = host != null ? host.Pool(Factory) : null;
            SpellProjectile projectile = reserve != null ? reserve.Take() : Create(null);
            projectile.pool = reserve;
            projectile.Begin(caster, spell, power, origin, direction);
            return projectile;
        }

        private static SpellProjectile Create(Transform parent)
        {
            GameObject root = new GameObject("Hechizo (proyectil)");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            SpellProjectile projectile = root.AddComponent<SpellProjectile>();
            projectile.orb = VfxOrb.Create(root.transform);
            return projectile;
        }

        private void Begin(PlayerSpellbook owner, SpellDefinition definition, float spellPower, Vector3 origin,
            Vector3 heading)
        {
            caster = owner;
            spell = definition;
            power = spellPower;
            heading.y = 0f;
            direction = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
            travelled = 0f;
            done = false;
            recycleAt = float.PositiveInfinity;
            transform.position = origin;
            LastImpactPoint = origin;
            gameObject.SetActive(true);
            if (spell != null)
            {
                orb.Configure(SpellVfx.ElementOf(spell.School), SpellCatalog.SchoolColor(spell.School),
                    spell.Radius * 1.4f, 0.2f);
                orb.Teleport(origin, direction * Speed);
            }
        }

        private void Update()
        {
            if (done)
            {
                if (Time.time >= recycleAt)
                    Recycle();
                return;
            }
            Step(Time.deltaTime);
        }

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
            int count = Physics.SphereCastNonAlloc(start, spell.Radius * HitRadiusFactor, direction, hits, step,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] completeHits = null;
            // Unity no garantiza que un NonAlloc saturado conserve los impactos
            // mas cercanos. En bosques/campamentos densos eso podia omitir una
            // pared y dejar que el proyectil la atravesara.
            if (count >= hits.Length)
            {
                completeHits = Physics.SphereCastAll(start, spell.Radius * HitRadiusFactor, direction, step,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = completeHits.Length;
            }

            float nearest = float.PositiveInfinity;
            Collider nearestCollider = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = completeHits != null ? completeHits[i] : hits[i];
                Collider hit = candidate.collider;
                if (hit == null || hit.transform.IsChildOf(transform) ||
                    hit.transform.IsChildOf(caster.transform))
                    continue;
                // distance 0 = ya solapado al empezar; cuenta como impacto inmediato.
                if (candidate.distance < nearest)
                {
                    nearest = candidate.distance;
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
            orb.Step(transform.position, deltaTime);
            if (travelled >= spell.Range - 0.001f)
                Finish(transform.position);
        }

        private void Finish(Vector3 point)
        {
            if (done)
                return;
            done = true;
            LastImpactPoint = point;
            transform.position = point;
            orb.Step(point, Time.deltaTime);
            orb.Extinguish();
            if (spell != null)
                SpellImpactFlash.Spawn(point, SpellCatalog.SchoolColor(spell.School), 0.55f, 0.22f, 0f,
                    SpellVfx.ElementOf(spell.School));
            // La estela termina de apagarse antes de volver a la reserva.
            recycleAt = Time.time + orb.LingerSeconds;
            caster = null;
        }

        private void Recycle()
        {
            recycleAt = float.PositiveInfinity;
            spell = null;
            if (pool != null)
                pool.Release(this);
            else
                Destroy(gameObject);
        }
    }
}
