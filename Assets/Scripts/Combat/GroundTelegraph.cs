using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Aviso de area en el suelo: el area entera se tiñe, el borde marca el limite exacto
    /// y un relleno crece desde el centro hasta tocar el borde justo cuando estalla.
    /// Al terminar destella y se apaga. Reutilizable; solo visual.
    /// </summary>
    public sealed class GroundTelegraph : MonoBehaviour
    {
        public const float FinishSeconds = 0.26f;
        private static readonly Func<Transform, GroundTelegraph> Factory = Create;

        private MeshRenderer disc;
        private MaterialPropertyBlock block;
        private VfxPool<GroundTelegraph> pool;
        private Transform follow;
        private float followLift;
        private Color color;
        private float radius;
        private float started;
        private float duration;
        private float cancelledAt = -1f;
        private bool runes;

        /// <summary>Progreso del relleno: 0 al empezar, 1 cuando el area estalla.</summary>
        public float Fill { get; private set; }
        public float Radius => radius;
        public bool Showing => isActiveAndEnabled;

        /// <summary>
        /// Muestra un aviso. Con <paramref name="follow"/>, sigue a ese objeto (a
        /// <paramref name="lift"/> metros de su origen); sin el, se apoya en el suelo de
        /// <paramref name="position"/>.
        /// </summary>
        public static GroundTelegraph Show(Transform follow, float lift, Vector3 position, float radius,
            Color color, float seconds, bool runes = true)
        {
            VfxHost host = VfxHost.Get();
            if (host == null || radius <= 0.05f)
                return null;
            VfxPool<GroundTelegraph> reserve = host.Pool(Factory);
            GroundTelegraph telegraph = reserve.Take();
            telegraph.pool = reserve;
            telegraph.Begin(follow, lift, position, radius, color, seconds, runes);
            return telegraph;
        }

        private static GroundTelegraph Create(Transform parent)
        {
            GameObject root = new GameObject("Aviso de area — sin colision");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            GroundTelegraph telegraph = root.AddComponent<GroundTelegraph>();
            telegraph.block = new MaterialPropertyBlock();
            telegraph.disc = VfxLibrary.CreateQuad("Disco", root.transform, true, VfxLibrary.GroundAdditive);
            return telegraph;
        }

        private void Begin(Transform target, float lift, Vector3 position, float size, Color tint, float seconds,
            bool withRunes)
        {
            follow = target;
            followLift = lift;
            radius = size;
            color = VfxLibrary.WithAlpha(tint, 1f);
            started = Time.time;
            duration = Mathf.Max(0.05f, seconds);
            cancelledAt = -1f;
            runes = withRunes;
            Fill = 0f;
            disc.transform.localScale = new Vector3(radius, 1f, radius);
            if (follow != null)
            {
                transform.SetPositionAndRotation(follow.position + Vector3.up * followLift, Quaternion.identity);
            }
            else
            {
                if (!VfxLibrary.FindGround(position, out Vector3 ground, out Vector3 normal))
                {
                    ground = position;
                    normal = Vector3.up;
                }
                VfxLibrary.PlaceOnGround(transform, ground, normal, 0.05f);
            }
            gameObject.SetActive(true);
            Refresh();
        }

        /// <summary>El hechizo se interrumpio: el aviso se apaga rapido sin destellar.</summary>
        public void Cancel()
        {
            if (cancelledAt < 0f && isActiveAndEnabled)
                cancelledAt = Time.time;
        }

        private void Update()
        {
            if (follow != null)
            {
                transform.position = follow.position + Vector3.up * followLift;
            }
            else if (!ReferenceEquals(follow, null))
            {
                follow = null;   // el objeto seguido se destruyo: el aviso se apaga
                Cancel();
            }
            if (Refresh())
            {
                follow = null;
                if (pool != null)
                    pool.Release(this);
                else
                    Destroy(gameObject);
            }
        }

        /// <summary>Actualiza el disco. Devuelve true cuando ha terminado.</summary>
        private bool Refresh()
        {
            float now = Time.time;
            float rimWidth = Mathf.Clamp(0.09f / radius, 0.012f, 0.2f);
            float edgeWidth = Mathf.Clamp(0.35f / radius, 0.03f, 0.35f);
            VfxDisc style = new VfxDisc
            {
                Color = color,
                Soft = 0.02f,
                RimWidth = rimWidth,
                EdgeWidth = edgeWidth,
                Ticks = runes ? 0.55f : 0f,
                Spin = (now - started) * 0.35f
            };

            if (cancelledAt >= 0f)
            {
                float fade = 1f - Mathf.Clamp01((now - cancelledAt) / 0.15f);
                style.Color = VfxLibrary.WithAlpha(color, fade);
                style.Base = 0.1f;
                style.RimAlpha = 0.8f;
                style.Fill = Fill;
                style.FillAlpha = 0.2f;
                style.EdgeAlpha = 0.5f;
                style.Apply(disc, block);
                return fade <= 0f;
            }

            float age = now - started;
            if (age < duration)
            {
                Fill = Mathf.Clamp01(age / duration);
                // El borde late mas deprisa a medida que se acerca el estallido.
                float pulse = 0.75f + 0.25f * Mathf.Sin(age * Mathf.Lerp(8f, 22f, Fill));
                style.Base = 0.08f + 0.07f * Fill;
                style.RimAlpha = 0.95f * pulse;
                style.Fill = Mathf.Max(0.002f, Fill);
                style.FillAlpha = 0.16f + 0.16f * Fill;
                style.EdgeAlpha = 0.85f;
                style.Apply(disc, block);
                return false;
            }

            // Estallido: el disco entero destella y se apaga.
            Fill = 1f;
            float t = Mathf.Clamp01((age - duration) / FinishSeconds);
            float flash = (1f - t) * (1f - t);
            style.Color = VfxLibrary.WithAlpha(Color.Lerp(Color.white, color, 0.55f + 0.45f * t), flash);
            style.Base = 0.2f;
            style.RimAlpha = 1f;
            style.Fill = 1f;
            style.FillAlpha = 0.55f;
            style.EdgeAlpha = 0f;
            style.Apply(disc, block);
            return t >= 1f;
        }
    }
}
