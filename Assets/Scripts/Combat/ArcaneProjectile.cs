using System;
using UnityEngine;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Proyectil magico que viaja por el mundo frame a frame. La colision usa un
    /// SphereCast con el MISMO radio que la esfera visible, y el cuerpo se coloca en el
    /// punto de contacto antes de aplicar el dano: lo que se ve tocar es lo que golpea.
    /// Local y no autoritativo, como el resto del combate de prototipo.
    /// </summary>
    public sealed class ArcaneProjectile : MonoBehaviour
    {
        private const int HitCapacity = 16;
        private static readonly RaycastHit[] Hits = new RaycastHit[HitCapacity];
        private static readonly Collider[] Overlaps = new Collider[HitCapacity];

        public struct Settings
        {
            public float Speed;
            public float Radius;
            public float Range;
            public float Damage;
            public float ImpactNoiseRadius;
            public float OccludedNoiseScale;
            public Color CoreColor;
            public Color GlowColor;
            public LayerMask Layers;
        }

        private Settings settings;
        private Transform owner;
        private Vector3 launchPosition;
        private Vector3 direction;
        private float travelled;
        private bool finished;
        private float destroyAt;
        private Transform body;
        private Renderer bodyRenderer;
        private Renderer haloRenderer;
        private TrailRenderer trail;
        private Light glow;

        /// <summary>Se dispara cuando el proyectil aplica dano: (salud, dano aplicado).</summary>
        public event Action<CombatHealth, float> HitTarget;
        public bool HasFinished => finished;
        public float Radius => settings.Radius;
        public Vector3 Direction => direction;

        public static ArcaneProjectile Launch(Vector3 origin, Vector3 direction, Transform owner,
            Settings settings)
        {
            GameObject root = new GameObject("Proyectil arcano");
            root.transform.position = origin;
            ArcaneProjectile projectile = root.AddComponent<ArcaneProjectile>();
            projectile.Initialize(origin, direction, owner, settings);
            return projectile;
        }

        private void Initialize(Vector3 origin, Vector3 dir, Transform ownerRoot, Settings config)
        {
            settings = config;
            settings.Speed = Mathf.Max(1f, Safe(config.Speed, 20f));
            settings.Radius = Mathf.Clamp(Safe(config.Radius, 0.2f), 0.05f, 1f);
            settings.Range = Mathf.Max(1f, Safe(config.Range, 30f));
            settings.Damage = Mathf.Max(0f, Safe(config.Damage, 0f));
            owner = ownerRoot;
            launchPosition = origin;
            direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // Cuerpo: esfera brillante con el diametro exacto de la colision.
            body = PrototypeVfx.CreateShape(PrimitiveType.Sphere, "Cuerpo", transform,
                origin, Vector3.one * settings.Radius * 2f, settings.CoreColor).transform;
            bodyRenderer = body.GetComponent<Renderer>();
            // Halo translucido algo mayor: solo decorativo, no altera la colision.
            Color halo = settings.GlowColor;
            halo.a = 0.35f;
            haloRenderer = PrototypeVfx.CreateShape(PrimitiveType.Sphere, "Halo", body,
                origin, Vector3.one * 1.6f, halo).GetComponent<Renderer>();

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = PrototypeVfx.SharedMaterial;
            trail.time = 0.28f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, settings.Radius * 1.8f),
                new Keyframe(1f, 0f));
            Color tail = settings.GlowColor;
            tail.a = 0f;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(settings.CoreColor, 0f),
                    new GradientColorKey(settings.GlowColor, 1f)
                },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            glow = PrototypeVfx.CreatePointLight(transform, settings.GlowColor, 4f, 2.2f);

            // Si nace dentro de algo (pegado a una pared), impacta en el acto.
            int overlaps = Physics.OverlapSphereNonAlloc(origin, settings.Radius, Overlaps,
                settings.Layers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps; i++)
            {
                if (IsOwnerCollider(Overlaps[i]))
                    continue;
                Impact(origin, origin, Overlaps[i]);
                return;
            }
        }

        private void Update()
        {
            if (finished)
            {
                if (Time.time >= destroyAt)
                    Destroy(gameObject);
                return;
            }

            float step = settings.Speed * Time.deltaTime;
            float remaining = settings.Range - travelled;
            bool reachesEnd = step >= remaining;
            if (reachesEnd)
                step = Mathf.Max(0f, remaining);

            Vector3 position = transform.position;
            if (step > 0f && TryFindHit(position, step, out RaycastHit hit))
            {
                // Centro de la esfera en el instante del contacto.
                Vector3 contactCenter = position + direction * hit.distance;
                Vector3 contactPoint = hit.distance <= 0f || hit.point == Vector3.zero
                    ? contactCenter : hit.point;
                Impact(contactCenter, contactPoint, hit.collider);
                return;
            }

            transform.position = position + direction * step;
            travelled += step;
            // Pulso sutil del halo y la luz: el cuerpo (colision) no cambia de tamano.
            float pulse = 1.5f + Mathf.Sin(Time.time * 30f) * 0.15f;
            haloRenderer.transform.localScale = Vector3.one * pulse;
            glow.intensity = 2.2f + Mathf.Sin(Time.time * 30f) * 0.4f;

            if (reachesEnd)
                Fizzle();
        }

        private bool TryFindHit(Vector3 position, float distance, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.SphereCastNonAlloc(position, settings.Radius, direction, Hits,
                distance, settings.Layers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = Hits;
            if (count == hits.Length)
            {
                hits = Physics.SphereCastAll(position, settings.Radius, direction, distance,
                    settings.Layers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            float best = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (IsOwnerCollider(hits[i].collider) || hits[i].distance >= best)
                    continue;
                best = hits[i].distance;
                nearest = hits[i];
                found = true;
            }
            return found;
        }

        private bool IsOwnerCollider(Collider candidate)
        {
            if (candidate == null)
                return true;
            Transform t = candidate.transform;
            return t == transform || t.IsChildOf(transform) ||
                owner != null && (t == owner || t.IsChildOf(owner));
        }

        private void Impact(Vector3 contactCenter, Vector3 contactPoint, Collider hitCollider)
        {
            transform.position = contactCenter;
            CombatHealth health = hitCollider != null
                ? hitCollider.GetComponentInParent<CombatHealth>() : null;
            bool hitLiving = false;
            if (health != null && !health.IsDefeated &&
                (owner == null || !health.transform.IsChildOf(owner)))
            {
                // El enemigo investigara el punto de lanzamiento: la trayectoria
                // demuestra linea despejada hasta ahi, no ve la posicion actual.
                PrototypeEnemy enemy = health.GetComponent<PrototypeEnemy>();
                if (enemy != null)
                    enemy.ReceiveAttackOrigin(launchPosition);
                float applied = health.ApplyDamage(settings.Damage);
                if (applied > 0f)
                {
                    hitLiving = true;
                    HitTarget?.Invoke(health, applied);
                }
            }

            // Respuesta visible: mas grande y blanca al herir; menor contra paredes.
            SpellImpactFlash.Spawn(contactPoint, hitLiving ? settings.CoreColor : settings.GlowColor,
                hitLiving ? 0.75f : 0.5f, hitLiving ? 0.35f : 0.28f, hitLiving ? 1.4f : 0f);
            PrototypeEnemy.EmitNoise(contactPoint, settings.ImpactNoiseRadius,
                settings.OccludedNoiseScale);
            Finish();
        }

        private void Fizzle()
        {
            SpellImpactFlash.Spawn(transform.position, settings.GlowColor, 0.3f, 0.22f);
            Finish();
        }

        private void Finish()
        {
            finished = true;
            if (bodyRenderer != null)
                bodyRenderer.gameObject.SetActive(false);
            if (glow != null)
                glow.enabled = false;
            if (trail != null)
                trail.emitting = false;
            // Deja que la estela se desvanezca en lugar de cortarla.
            destroyAt = Time.time + (trail != null ? trail.time : 0f);
        }

        private static float Safe(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
