using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.NPC
{
    /// <summary>
    /// Columna de luz que el guía deja sobre un lugar: se ve desde lejos con la cámara
    /// cenital, late suavemente y se apaga sola. Solo hay una a la vez. Solo visual.
    /// </summary>
    public sealed class NpcBeacon : MonoBehaviour
    {
        public const float Height = 40f;
        private static NpcBeacon current;

        private LineRenderer beam;
        private Color color;
        private float bornAt;
        private float lifetime;

        public static NpcBeacon Current => current;

        public static NpcBeacon Mark(Vector3 point, Color tint, float seconds)
        {
            if (current != null)
                Destroy(current.gameObject);
            Material material = VfxLibrary.AdditiveBeam;
            GameObject go = new GameObject("Luz del guía — sin colision");
            go.transform.position = VfxLibrary.FindGround(point, out Vector3 ground, out _) ? ground : point;
            NpcBeacon beacon = go.AddComponent<NpcBeacon>();
            beacon.color = tint;
            beacon.bornAt = Time.time;
            beacon.lifetime = Mathf.Max(3f, seconds);
            if (material != null)
            {
                beacon.beam = go.AddComponent<LineRenderer>();
                beacon.beam.sharedMaterial = material;
                beacon.beam.useWorldSpace = false;
                beacon.beam.positionCount = 2;
                beacon.beam.SetPosition(0, Vector3.zero);
                beacon.beam.SetPosition(1, Vector3.up * Height);
                beacon.beam.alignment = LineAlignment.View;
                beacon.beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beacon.beam.receiveShadows = false;
            }
            SpellImpactFlash.Spawn(go.transform.position + Vector3.up * 0.3f, tint, 1.4f, 0.6f, 3f);
            current = beacon;
            return beacon;
        }

        private void Update()
        {
            float age = Time.time - bornAt;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            if (beam == null)
                return;
            float fade = Mathf.Clamp01((lifetime - age) / 3f) * Mathf.Clamp01(age / 0.4f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            beam.widthMultiplier = 0.7f * pulse;
            beam.startColor = new Color(color.r, color.g, color.b, 0.9f * fade);
            beam.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        private void OnDestroy()
        {
            if (current == this)
                current = null;
        }
    }
}
