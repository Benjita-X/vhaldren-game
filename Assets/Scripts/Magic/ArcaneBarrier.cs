using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>Escudo magico temporal que absorbe daño antes de restar PV.</summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneBarrier : MonoBehaviour, IDamageAbsorber
    {
        private float remaining;
        private float until;
        private LineRenderer ring;

        public float Remaining => Time.time < until ? remaining : 0f;
        public bool Active => Remaining > 0f;

        public void Raise(float amount, float seconds, Color tint)
        {
            if (float.IsNaN(amount) || amount <= 0f || seconds <= 0f)
                return;
            remaining = Mathf.Max(Remaining, amount);
            until = Time.time + seconds;
            if (ring == null)
                ring = PrototypeVfx.CreateRing("Egida — sin colision", transform, 0.85f, 0.07f, tint);
            if (ring != null)
            {
                ring.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                ring.gameObject.SetActive(true);
            }
        }

        public float Absorb(float amount)
        {
            if (!Active || amount <= 0f)
                return amount;
            float absorbed = Mathf.Min(remaining, amount);
            remaining -= absorbed;
            if (remaining <= 0f)
                Drop();
            return amount - absorbed;
        }

        public void Drop()
        {
            remaining = 0f;
            until = 0f;
            if (ring != null)
                ring.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (ring != null && ring.gameObject.activeSelf && !Active)
                Drop();
        }
    }
}
