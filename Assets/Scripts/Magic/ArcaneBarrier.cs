using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Escudo magico temporal que absorbe daño antes de restar PV. Se ve como un circulo
    /// runico que gira a los pies y motas de luz que suben mientras dura; destella al
    /// recibir golpes y se apaga cuando se agota.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneBarrier : MonoBehaviour, IDamageAbsorber
    {
        private float remaining;
        private float until;
        private float capacity = 1f;
        private float struckAt = -10f;
        private float nextMote;
        private Color tint = Color.white;
        private MeshRenderer ring;
        private MaterialPropertyBlock block;

        public float Remaining => Time.time < until ? remaining : 0f;
        public bool Active => Remaining > 0f;
        public bool IsVisible => ring != null && ring.enabled;

        public void Raise(float amount, float seconds, Color color)
        {
            if (float.IsNaN(amount) || amount <= 0f || seconds <= 0f)
                return;
            remaining = Mathf.Max(Remaining, amount);
            capacity = Mathf.Max(1f, remaining);
            until = Time.time + seconds;
            tint = VfxLibrary.WithAlpha(color, 1f);
            if (ring == null)
            {
                block = new MaterialPropertyBlock();
                ring = VfxLibrary.CreateQuad("Egida — sin colision", transform, true, VfxLibrary.GroundAdditive);
                CharacterController body = GetComponent<CharacterController>();
                float feet = body != null ? body.center.y - body.height * 0.5f + 0.06f : -0.94f;
                ring.transform.localPosition = new Vector3(0f, feet, 0f);
                ring.transform.localScale = new Vector3(0.95f, 1f, 0.95f);
            }
            ring.enabled = true;
            Refresh();
        }

        public float Absorb(float amount)
        {
            if (!Active || amount <= 0f)
                return amount;
            float absorbed = Mathf.Min(remaining, amount);
            remaining -= absorbed;
            struckAt = Time.time;
            if (remaining <= 0f)
                Drop();
            return amount - absorbed;
        }

        public void Drop()
        {
            remaining = 0f;
            until = 0f;
            if (ring != null)
                ring.enabled = false;
        }

        private void Update()
        {
            if (ring == null || !ring.enabled)
                return;
            if (!Active)
            {
                Drop();
                return;
            }
            Refresh();
            if (Time.time >= nextMote)
            {
                nextMote = Time.time + 0.09f;
                VfxHost host = VfxHost.Get();
                if (host != null)
                {
                    Vector2 around = Random.insideUnitCircle.normalized * 0.85f;
                    host.Glints(ring.transform.position + new Vector3(around.x, 0.1f, around.y), 0.05f,
                        Color.Lerp(tint, Color.white, 0.4f), 1, 0.13f, 0.8f, Vector3.up * 1.6f);
                }
            }
        }

        private void Refresh()
        {
            float charge = Mathf.Clamp01(Remaining / capacity);
            float hit = Mathf.Clamp01(1f - (Time.time - struckAt) / 0.2f);
            float ending = Mathf.Clamp01((until - Time.time) / 0.6f);
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 6f);
            VfxDisc style = new VfxDisc
            {
                Color = VfxLibrary.WithAlpha(Color.Lerp(tint, Color.white, hit * 0.6f), ending),
                Base = 0.1f + 0.25f * hit,
                Soft = 0.3f,
                RimWidth = 0.07f,
                RimAlpha = (0.55f + 0.45f * charge) * pulse,
                Ticks = 0.8f,
                Spin = Time.time * 0.6f,
                EdgeWidth = 0.05f
            };
            style.Apply(ring, block);
        }
    }
}
