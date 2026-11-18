using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// No hay natacion: si el jugador entra en agua mas profunda que
    /// <see cref="MaxDepth"/> (segun lagos, rios y mar de <see cref="WorldAtlas"/>) vuelve
    /// con suavidad al ultimo punto seco con el aviso "El agua es demasiado profunda".
    /// Volando no se aplica. Sin atlas no hace nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeepWaterGuard : MonoBehaviour
    {
        public const float MaxDepth = 1.3f;
        public const float SafeDepth = 0.45f;
        public const float ReturnSeconds = 0.6f;

        private CharacterController body;
        private CombatHealth health;
        private Vector3 safePosition;
        private bool hasSafe;
        private float nextSafe;
        private float nextCheck;
        private bool returning;
        private Vector3 returnFrom, returnTo;
        private float returnStart;

        public bool IsReturning => returning;
        public bool HasSafePosition => hasSafe;
        public Vector3 LastSafePosition => safePosition;

        /// <summary>Tras un teletransporte: el nuevo sitio pasa a ser el punto seguro.</summary>
        public void ResetSafePosition(Vector3 position)
        {
            safePosition = position;
            hasSafe = true;
            nextSafe = Time.time + 0.3f;
        }

        /// <summary>Decide si hay que sacar al jugador del agua (logica pura).</summary>
        public static bool ShouldReturn(float depth, bool flying, bool hasSafePosition) =>
            !flying && hasSafePosition && depth > MaxDepth;

        private void Update()
        {
            if (returning)
            {
                StepReturn();
                return;
            }
            WorldAtlas atlas = WorldAtlas.Active;
            if (atlas == null)
                return;
            if (body == null) body = GetComponent<CharacterController>();
            if (health == null) health = GetComponent<CombatHealth>();
            if (body == null || !body.enabled || (health != null && health.IsDefeated) || Time.time < nextCheck)
                return;
            nextCheck = Time.time + 0.1f;

            Vector3 feet = new Vector3(transform.position.x, body.bounds.min.y, transform.position.z);
            float ground = GroundProbe.TryTerrainHeight(feet, out float terrain) ? terrain : float.NaN;
            float depth = atlas.WaterDepthAt(feet, ground);
            if (ShouldReturn(depth, ExplorationUi.PlayerFlying, hasSafe))
            {
                StartReturn();
                return;
            }
            if (!ExplorationUi.PlayerFlying && body.isGrounded && depth < SafeDepth && Time.time >= nextSafe)
            {
                safePosition = transform.position;
                hasSafe = true;
                nextSafe = Time.time + 0.3f;
            }
        }

        private void StartReturn()
        {
            returning = true;
            returnStart = Time.time;
            returnFrom = transform.position;
            returnTo = safePosition + Vector3.up * 0.05f;
            body.enabled = false;
            HudNotices.Post("El agua es demasiado profunda", HudNotices.Warning, 2.2f);
            VfxHost host = VfxHost.Get();
            if (host != null)
                host.Burst(VfxElement.Neutral, returnFrom, Vector3.up, new Color(0.6f, 0.8f, 1f), 16, 3f, 0.1f, 0.6f, 0.6f);
        }

        private void StepReturn()
        {
            float t = Mathf.Clamp01((Time.time - returnStart) / ReturnSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(returnFrom, returnTo, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.2f);
            if (t < 1f)
                return;
            transform.position = returnTo;
            if (body != null)
                body.enabled = true;
            returning = false;
            nextCheck = Time.time + 0.3f;
        }

        private void OnDisable()
        {
            if (!returning)
                return;
            transform.position = returnTo;
            if (body != null)
                body.enabled = true;
            returning = false;
        }
    }
}
