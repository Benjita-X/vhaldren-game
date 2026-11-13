using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Cuerpo visual de algo que vuela (proyectiles, meteoro): nucleo, halo que late,
    /// cuerpo estirado en la direccion del vuelo, estela, luz y particulas del elemento
    /// que se quedan atras (brasas que suben, esquirlas, chispas, humo, gotas, rayos).
    /// Quien lo posee lo mueve con <see cref="Step"/>. Solo visual: sin colliders.
    /// </summary>
    public sealed class VfxOrb : MonoBehaviour
    {
        public static readonly Func<Transform, VfxOrb> Factory = Create;

        private MeshRenderer halo;
        private MeshRenderer core;
        private LineRenderer body;
        private LineRenderer arcs;
        private TrailRenderer trail;
        private Light orbLight;
        private MaterialPropertyBlock block;
        private readonly Vector3[] arcPoints = new Vector3[7];

        private VfxElement element;
        private VfxPalette palette;
        private Color glow;
        private float size;
        private float emitDebt;
        private float nextArc;
        private float pulseSeed;
        private float lightIntensity;
        private Vector3 lastPosition;
        private Vector3 velocity;
        private bool lit;

        public VfxElement Element => element;
        public bool Lit => lit;
        public float LingerSeconds => trail != null ? trail.time + 0.05f : 0.05f;

        public static VfxOrb Create(Transform parent)
        {
            GameObject root = new GameObject("Cuerpo del hechizo");
            root.transform.SetParent(parent, false);
            VfxOrb orb = root.AddComponent<VfxOrb>();
            orb.block = new MaterialPropertyBlock();
            orb.halo = VfxLibrary.CreateQuad("Halo", root.transform, false, VfxLibrary.AdditiveGlow);
            orb.core = VfxLibrary.CreateQuad("Nucleo", root.transform, false, VfxLibrary.AdditiveGlow);
            orb.halo.sortingOrder = 0;
            orb.core.sortingOrder = 3;

            orb.body = NewLine("Forma", root.transform, 2);
            orb.body.positionCount = 2;
            orb.body.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.65f, 0.75f), new Keyframe(1f, 1f));

            orb.arcs = NewLine("Arcos", root.transform, 2);
            orb.arcs.positionCount = orb.arcPoints.Length;
            orb.arcs.widthMultiplier = 0.035f;
            orb.arcs.enabled = false;

            orb.trail = root.AddComponent<TrailRenderer>();
            orb.trail.time = 0.2f;
            orb.trail.minVertexDistance = 0.08f;
            orb.trail.alignment = LineAlignment.View;
            orb.trail.textureMode = LineTextureMode.Stretch;
            orb.trail.numCapVertices = 2;
            orb.trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            orb.trail.autodestruct = false;
            VfxLibrary.ConfigureRenderer(orb.trail);

            orb.orbLight = PrototypeVfx.CreatePointLight(root.transform, Color.white, 4f, 1.8f);
            return orb;
        }

        private static LineRenderer NewLine(string name, Transform parent, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 3;
            line.sortingOrder = order;
            line.sharedMaterial = VfxLibrary.AdditiveBeam;
            VfxLibrary.ConfigureRenderer(line);
            return line;
        }

        /// <summary>Prepara el aspecto para un elemento. size = diametro aproximado del nucleo.</summary>
        public void Configure(VfxElement kind, Color tint, float diameter, float trailSeconds)
        {
            element = kind;
            palette = VfxLibrary.Palette(kind, tint);
            glow = kind == VfxElement.Neutral ? VfxLibrary.WithAlpha(tint, 1f) : palette.Glow;
            size = Mathf.Max(0.1f, diameter);
            pulseSeed = UnityEngine.Random.Range(0f, 10f);
            emitDebt = 0f;
            lit = true;

            bool dark = palette.DarkCore;
            core.sharedMaterial = dark ? VfxLibrary.AlphaDrop : VfxLibrary.AdditiveGlow;
            body.sharedMaterial = kind == VfxElement.Frost ? VfxLibrary.AdditiveShard
                : dark ? VfxLibrary.AlphaBeam : VfxLibrary.AdditiveBeam;
            trail.sharedMaterial = dark ? VfxLibrary.AlphaBeam : VfxLibrary.AdditiveBeam;

            core.transform.localScale = Vector3.one * size * (dark ? 1.1f : 1.25f);
            VfxDisc.Tint(core, block, dark ? palette.Core : VfxLibrary.WithAlpha(palette.Core, 1f));
            body.widthMultiplier = size * (kind == VfxElement.Frost ? 1.1f : kind == VfxElement.Storm ? 0.55f : 0.9f);
            Color bodyHead = dark ? palette.Core : Color.Lerp(glow, Color.white, 0.45f);
            body.startColor = VfxLibrary.WithAlpha(glow, 0f);
            body.endColor = VfxLibrary.WithAlpha(bodyHead, dark ? 0.9f : 1f);

            Color trailColor = dark ? palette.Spark : glow;
            trail.time = Mathf.Max(0.02f, trailSeconds);
            trail.widthMultiplier = size * (kind == VfxElement.Light ? 0.75f : 0.6f);
            trail.startColor = VfxLibrary.WithAlpha(trailColor, dark ? 0.75f : 0.8f);
            trail.endColor = VfxLibrary.WithAlpha(trailColor, 0f);

            arcs.enabled = kind == VfxElement.Storm;
            arcs.startColor = arcs.endColor = VfxLibrary.WithAlpha(Color.Lerp(glow, Color.white, 0.6f), 0.95f);

            orbLight.color = glow;
            orbLight.range = 3f + size * 5f;
            lightIntensity = dark ? 0.9f : 1.9f;
            orbLight.intensity = lightIntensity;
            orbLight.enabled = true;

            halo.enabled = core.enabled = body.enabled = true;
            trail.emitting = true;
        }

        /// <summary>Coloca el cuerpo sin dejar estela desde la posicion anterior.</summary>
        public void Teleport(Vector3 position, Vector3 initialVelocity)
        {
            transform.position = position;
            lastPosition = position;
            velocity = initialVelocity;
            trail.Clear();
            UpdateBody(position);
        }

        /// <summary>Avanza a <paramref name="position"/> y suelta particulas por el camino.</summary>
        public void Step(Vector3 position, float deltaTime)
        {
            if (!lit)
                return;
            float dt = Mathf.Max(deltaTime, 0.0001f);
            Vector3 moved = position - lastPosition;
            if (moved.sqrMagnitude > 0.000001f)
                velocity = moved / dt;
            transform.position = position;
            UpdateBody(position);
            EmitTrail(position, dt);
            if (element == VfxElement.Storm && Time.time >= nextArc)
            {
                nextArc = Time.time + 0.04f;
                Scribble(position);
            }
            lastPosition = position;
        }

        /// <summary>Se apaga al impactar: la estela termina de desvanecerse sola.</summary>
        public void Extinguish()
        {
            lit = false;
            halo.enabled = core.enabled = body.enabled = arcs.enabled = false;
            orbLight.enabled = false;
            trail.emitting = false;
        }

        private void UpdateBody(Vector3 head)
        {
            float speed = velocity.magnitude;
            Vector3 direction = speed > 0.01f ? velocity / speed : Vector3.forward;
            float length = Mathf.Clamp(speed * 0.045f, size * 1.2f, size * 4.5f);
            if (element == VfxElement.Light)
                length *= 1.5f;
            body.SetPosition(0, head - direction * length);
            body.SetPosition(1, head + direction * size * 0.25f);
        }

        private void EmitTrail(Vector3 position, float dt)
        {
            VfxHost host = VfxHost.Get();
            if (host == null)
                return;
            float rate = 46f + size * 40f;
            emitDebt += rate * dt;
            int count = Mathf.FloorToInt(emitDebt);
            if (count <= 0)
                return;
            emitDebt -= count;
            count = Mathf.Min(count, 12);
            Vector3 back = -velocity * 0.06f;
            float jitter = size * 0.35f;
            switch (element)
            {
                case VfxElement.Fire:
                    host.Trail(element, position, back, palette.Spark, count, size * 0.45f, 0.65f, jitter, 1.1f);
                    break;
                case VfxElement.Frost:
                    host.Trail(element, position, back, palette.Spark, Mathf.Max(1, count / 2), size * 0.35f, 0.45f, jitter, 1.4f);
                    host.Glints(position, jitter, palette.Spark, 1, size * 0.4f, 0.4f, Vector3.zero);
                    break;
                case VfxElement.Storm:
                    host.Trail(element, position, back, palette.Spark, count, size * 0.3f, 0.22f, jitter, 4.5f);
                    break;
                case VfxElement.Shadow:
                    host.Trail(element, position, back * 0.5f, palette.Spark, Mathf.Max(1, count / 2), size * 1.3f, 0.75f, jitter, 0.35f);
                    break;
                case VfxElement.Blood:
                    host.Trail(element, position, back * 0.4f, palette.Spark, Mathf.Max(1, count / 2), size * 0.3f, 0.6f, jitter, 1.2f);
                    break;
                case VfxElement.Light:
                    host.Trail(element, position, back, palette.Spark, Mathf.Max(1, count / 2), size * 0.3f, 0.3f, jitter, 2.6f);
                    host.Glints(position, jitter, palette.Spark, 1, size * 0.5f, 0.45f, Vector3.up * 0.3f);
                    break;
                default:
                    host.Trail(element, position, back, palette.Spark, count, size * 0.3f, 0.3f, jitter, 2f);
                    break;
            }
        }

        private void Scribble(Vector3 center)
        {
            // Pequeños arcos electricos que saltan alrededor del nucleo.
            float reach = size * 1.6f;
            Vector3 previous = center;
            for (int i = 0; i < arcPoints.Length; i++)
            {
                Vector3 jump = UnityEngine.Random.insideUnitSphere * reach;
                previous = i == 0 ? center : Vector3.Lerp(previous, center + jump, 0.7f);
                arcPoints[i] = previous;
            }
            arcs.SetPositions(arcPoints);
        }

        private void LateUpdate()
        {
            if (!lit)
                return;
            VfxHost host = VfxHost.Get();
            Camera view = host != null ? host.ViewCamera : Camera.main;
            VfxLibrary.FaceCamera(halo.transform, view);
            VfxLibrary.FaceCamera(core.transform, view);
            float pulse = 1f + 0.14f * Mathf.Sin((Time.time + pulseSeed) * 21f) + 0.06f * Mathf.Sin((Time.time + pulseSeed) * 47f);
            halo.transform.localScale = Vector3.one * size * 3.3f * pulse;
            VfxDisc.Tint(halo, block, VfxLibrary.WithAlpha(glow, palette.DarkCore ? 0.85f : 0.7f));
            orbLight.intensity = lightIntensity * (0.85f + 0.15f * pulse);
        }
    }
}
