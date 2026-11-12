using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Respuesta visual corta de un impacto: nucleo que se expande y se apaga,
    /// esquirlas que salen despedidas, anillo en el suelo opcional y luz que decae.
    /// Solo visual; el dano lo resuelve quien crea el efecto.
    /// </summary>
    public sealed class SpellImpactFlash : MonoBehaviour
    {
        private const int ShardCount = 7;

        private Renderer core;
        private LineRenderer ring;
        private Light flashLight;
        private Transform[] shards;
        private Vector3[] shardVelocities;
        private Renderer[] shardRenderers;
        private Color color;
        private float maxRadius;
        private float ringRadius;
        private float duration;
        private float elapsed;
        private float baseIntensity;

        /// <summary>Crea el efecto en el mundo. ringRadius &lt;= 0 desactiva el anillo.</summary>
        public static SpellImpactFlash Spawn(Vector3 position, Color color, float maxRadius,
            float duration, float ringRadius = 0f)
        {
            GameObject root = new GameObject("FX impacto");
            root.transform.position = position;
            SpellImpactFlash flash = root.AddComponent<SpellImpactFlash>();
            flash.Build(color, Mathf.Max(0.1f, maxRadius), Mathf.Max(0.05f, duration),
                ringRadius);
            return flash;
        }

        private void Build(Color tint, float radius, float life, float groundRing)
        {
            color = tint;
            maxRadius = radius;
            duration = life;
            ringRadius = groundRing;

            core = PrototypeVfx.CreateShape(PrimitiveType.Sphere, "Nucleo", transform,
                transform.position, Vector3.one * 0.1f, Color.white).GetComponent<Renderer>();

            shards = new Transform[ShardCount];
            shardVelocities = new Vector3[ShardCount];
            shardRenderers = new Renderer[ShardCount];
            for (int i = 0; i < ShardCount; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.2f;
                GameObject shard = PrototypeVfx.CreateShape(PrimitiveType.Cube, "Esquirla",
                    transform, transform.position, Vector3.one * radius * 0.16f, tint);
                shard.transform.rotation = Random.rotation;
                shards[i] = shard.transform;
                shardRenderers[i] = shard.GetComponent<Renderer>();
                shardVelocities[i] = direction.normalized * radius * Random.Range(4f, 7f);
            }

            if (ringRadius > 0f)
            {
                ring = PrototypeVfx.CreateRing("Onda", transform, 0.1f, 0.18f, tint);
                ring.transform.localPosition = Vector3.up * 0.05f;
            }

            flashLight = PrototypeVfx.CreatePointLight(transform, tint, radius * 5f, 4f);
            baseIntensity = flashLight.intensity;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float fade = 1f - t;

            // Nucleo: crece rapido (ease-out) y pasa de blanco al color del hechizo.
            float grow = 1f - (1f - t) * (1f - t);
            core.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, maxRadius * 2f, grow);
            Color coreColor = Color.Lerp(Color.white, color, t * 1.6f);
            coreColor.a = fade * fade;
            PrototypeVfx.SetColor(core, coreColor);

            for (int i = 0; i < shards.Length; i++)
            {
                shardVelocities[i] += Physics.gravity * Time.deltaTime;
                shards[i].position += shardVelocities[i] * Time.deltaTime;
                shards[i].localScale = Vector3.one * maxRadius * 0.16f * fade;
                Color shardColor = color;
                shardColor.a = fade;
                PrototypeVfx.SetColor(shardRenderers[i], shardColor);
            }

            if (ring != null)
            {
                PrototypeVfx.SetRingRadius(ring, Mathf.Lerp(0.2f, ringRadius, grow));
                Color ringColor = color;
                ringColor.a = fade;
                ring.startColor = ringColor;
                ring.endColor = ringColor;
            }

            flashLight.intensity = baseIntensity * fade * fade;
            if (elapsed >= duration)
                Destroy(gameObject);
        }
    }
}
