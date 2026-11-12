using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Flecha de ballesta viajera: cuerpo visible, caida ligera y barrido de
    /// colision por frame para no atravesar enemigos a alta velocidad.
    /// Sin Rigidbody ni simulacion de red en esta fase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhysicalArrowProjectile : MonoBehaviour
    {
        private const int HitCapacity = 24;
        private readonly RaycastHit[] hits = new RaycastHit[HitCapacity];

        private Transform owner;
        private LocalPlayerShooter shooter;
        private Vector3 velocity;
        private float damage;
        private float remainingRange;
        private float gravityScale;
        private float collisionRadius = 0.09f;
        private bool initialized;
        private bool finished;

        private static Material wood;
        private static Material metal;
        private static Material feathers;
        private static Material spark;

        public bool IsInFlight => initialized && !finished;
        public float RemainingDistance => remainingRange;

        public static PhysicalArrowProjectile Spawn(Transform attacker, Vector3 origin,
            Vector3 direction, float damage, float speed, float range,
            LocalPlayerShooter callback, float gravity = 0.18f)
        {
            if (attacker == null || direction.sqrMagnitude <= 0.0001f ||
                !Finite(speed) || speed <= 0f || !Finite(range) || range <= 0f)
                return null;

            GameObject arrowObject = new GameObject("Flecha fisica de ballesta");
            arrowObject.transform.position = origin;
            arrowObject.transform.rotation = Quaternion.LookRotation(direction.normalized);
            PhysicalArrowProjectile arrow = arrowObject.AddComponent<PhysicalArrowProjectile>();
            arrow.Initialize(attacker, direction.normalized, damage, speed, range, callback, gravity);
            return arrow;
        }

        private void Initialize(Transform attacker, Vector3 direction, float requestedDamage,
            float speed, float range, LocalPlayerShooter callback, float gravity)
        {
            owner = attacker;
            shooter = callback;
            velocity = direction * speed;
            damage = Finite(requestedDamage) ? Mathf.Max(0f, requestedDamage) : 0f;
            remainingRange = range;
            gravityScale = Finite(gravity) ? Mathf.Clamp(gravity, 0f, 2f) : 0.18f;
            BuildVisibleArrow();
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || finished)
                return;

            float dt = Mathf.Min(Time.deltaTime, 0.06f);
            if (dt <= 0f)
                return;

            Vector3 previous = transform.position;
            velocity += Physics.gravity * gravityScale * dt;
            Vector3 step = velocity * dt;
            float length = step.magnitude;
            if (length <= 0.00001f)
                return;

            Vector3 direction = step / length;
            float scanLength = Mathf.Min(length, remainingRange);
            if (TryFirstHit(previous, direction, scanLength, out RaycastHit hit))
            {
                transform.position = hit.point - direction * 0.06f;
                transform.rotation = Quaternion.LookRotation(direction);
                OnImpact(hit);
                return;
            }

            transform.position += direction * scanLength;
            transform.rotation = Quaternion.LookRotation(direction);
            remainingRange -= scanLength;
            if (remainingRange <= 0f)
                Destroy(gameObject);
        }

        private bool TryFirstHit(Vector3 origin, Vector3 direction, float distance,
            out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction,
                hits, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            RaycastHit[] all = hits;
            if (count == HitCapacity)
            {
                all = Physics.SphereCastAll(origin, collisionRadius, direction,
                    distance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                count = all.Length;
            }

            float shortest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = all[i].collider;
                if (candidate == null)
                    continue;
                if (owner != null && (candidate.transform == owner ||
                    candidate.transform.IsChildOf(owner)))
                    continue;
                if (all[i].distance < shortest)
                {
                    shortest = all[i].distance;
                    nearest = all[i];
                    found = true;
                }
            }
            return found;
        }

        private void OnImpact(RaycastHit hit)
        {
            if (finished)
                return;
            finished = true;

            float applied = 0f;
            Collider hitCollider = hit.collider;
            if (hitCollider != null)
            {
                CombatDamageReceiver receiver =
                    hitCollider.GetComponentInParent<CombatDamageReceiver>();
                if (receiver != null && (owner == null ||
                    !receiver.transform.IsChildOf(owner)))
                {
                    applied = receiver.Receive(damage, CombatDamageKind.Arrow,
                        owner != null ? owner.position : transform.position).Applied;
                }
                else
                {
                    CombatHealth health = hitCollider.GetComponentInParent<CombatHealth>();
                    if (health != null && (owner == null ||
                        !health.transform.IsChildOf(owner)))
                        applied = health.ApplyDamage(damage);
                }
            }

            if (shooter != null)
                shooter.RegisterProjectileImpact(hit.point, applied);

            ShowImpact(hit.point, applied > 0f);
            Destroy(gameObject);
        }

        private void BuildVisibleArrow()
        {
            EnsureMaterials();

            // Eje local Z = direccion real de vuelo. No hay collider extra
            // sobre el modelo, solo un barrido esferico para la trayectoria.
            Part("Vástago de madera", PrimitiveType.Cylinder,
                new Vector3(0f, 0f, 0f), new Vector3(0.035f, 0.42f, 0.035f),
                Quaternion.Euler(90f, 0f, 0f), wood);
            Part("Punta metalica", PrimitiveType.Sphere,
                new Vector3(0f, 0f, 0.47f), new Vector3(0.095f, 0.095f, 0.22f),
                Quaternion.identity, metal);
            Part("Pluma izquierda", PrimitiveType.Cube,
                new Vector3(-0.085f, 0.025f, -0.31f),
                new Vector3(0.14f, 0.035f, 0.20f), Quaternion.Euler(0f, 12f, 0f), feathers);
            Part("Pluma derecha", PrimitiveType.Cube,
                new Vector3(0.085f, 0.025f, -0.31f),
                new Vector3(0.14f, 0.035f, 0.20f), Quaternion.Euler(0f, -12f, 0f), feathers);
        }

        private void Part(string name, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject child = GameObject.CreatePrimitive(primitive);
            child.name = name;
            child.transform.SetParent(transform, false);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            child.transform.localScale = scale;
            Collider body = child.GetComponent<Collider>();
            if (body != null)
                Destroy(body);
            child.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void EnsureMaterials()
        {
            if (wood != null && metal != null && feathers != null && spark != null)
                return;
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            if (wood == null)
                wood = CreateMaterial(shader, new Color(0.40f, 0.23f, 0.13f));
            if (metal == null)
                metal = CreateMaterial(shader, new Color(0.73f, 0.79f, 0.80f));
            if (feathers == null)
                feathers = CreateMaterial(shader, new Color(0.16f, 0.60f, 0.58f));
            if (spark == null)
                spark = CreateMaterial(shader, new Color(0.96f, 0.62f, 0.23f));
        }

        private static Material CreateMaterial(Shader shader, Color color)
        {
            Material material = new Material(shader);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            return material;
        }

        private static void ShowImpact(Vector3 where, bool onTarget)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = onTarget ? "Impacto acertado" : "Flecha choca";
            flash.transform.position = where;
            flash.transform.localScale = Vector3.one * (onTarget ? 0.28f : 0.18f);
            Collider collision = flash.GetComponent<Collider>();
            if (collision != null)
                Destroy(collision);
            Renderer renderer = flash.GetComponent<Renderer>();
            if (renderer != null && spark != null)
                renderer.sharedMaterial = spark;
            Destroy(flash, 0.16f);
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
