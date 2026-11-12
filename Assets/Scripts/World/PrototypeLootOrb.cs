using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Botin sencillo: esencia que suelta un enemigo derrotado. Flota en el sitio y se
    /// recoge al acercarse. No usa triggers ni Rigidbody: comprueba la distancia al jugador.
    /// </summary>
    public sealed class PrototypeLootOrb : MonoBehaviour
    {
        private PrototypeGameSession session;
        private Transform player;
        private int essence;
        private float heal;
        private float pickupRadius;
        private float expiresAt;
        private Vector3 basePosition;
        private Transform visual;
        private Renderer visualRenderer;
        private Color color;

        public int Essence => essence;

        public static PrototypeLootOrb Spawn(PrototypeGameSession owner, Transform player,
            Vector3 position, int essence, float heal, bool large)
        {
            GameObject root = new GameObject(large ? "Botin - Esencia mayor" : "Botin - Esencia");
            root.transform.position = position;
            PrototypeLootOrb orb = root.AddComponent<PrototypeLootOrb>();
            orb.session = owner;
            orb.player = player;
            orb.essence = Mathf.Max(1, essence);
            orb.heal = Mathf.Max(0f, heal);
            orb.pickupRadius = large ? 1.8f : 1.4f;
            orb.expiresAt = Time.time + 45f;
            orb.basePosition = position;
            orb.color = large ? new Color(1f, 0.75f, 0.2f, 1f) : new Color(0.95f, 0.2f, 0.35f, 1f);
            float size = large ? 0.7f : 0.42f;
            orb.visual = PrototypeVfx.CreateShape(PrimitiveType.Sphere, "Esencia", root.transform,
                position, Vector3.one * size, orb.color).transform;
            orb.visualRenderer = orb.visual.GetComponent<Renderer>();
            PrototypeVfx.CreateRing("Marca", root.transform, size * 1.3f, 0.05f, orb.color)
                .transform.localPosition = Vector3.down * (position.y - 0.03f);
            PrototypeVfx.CreatePointLight(root.transform, orb.color, 3.5f, 1.6f);
            return orb;
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * 3f) * 0.15f;
            visual.position = basePosition + Vector3.up * bob;
            visual.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);

            float remaining = expiresAt - Time.time;
            if (remaining < 6f)
            {
                // Parpadea antes de desaparecer para que se lea que caduca.
                Color c = color;
                c.a = Mathf.PingPong(Time.time * 4f, 1f) * 0.7f + 0.3f;
                PrototypeVfx.SetColor(visualRenderer, c);
            }
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (player == null || !player.gameObject.activeInHierarchy)
                return;
            CombatHealth health = player.GetComponent<CombatHealth>();
            if (health != null && health.IsDefeated)
                return;
            Vector3 delta = player.position - basePosition;
            float vertical = Mathf.Abs(delta.y);
            delta.y = 0f;
            if (delta.sqrMagnitude > pickupRadius * pickupRadius || vertical > 2.2f)
                return;

            if (session != null)
                session.CollectLoot(essence, heal);
            SpellImpactFlash.Spawn(visual.position, color, 0.45f, 0.3f);
            Destroy(gameObject);
        }
    }
}
