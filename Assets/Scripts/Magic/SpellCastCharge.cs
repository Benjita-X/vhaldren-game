using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Preparacion de un hechizo: particulas del elemento que convergen en la mano
    /// (la que mas adelantada esta segun la animacion) y un brillo que crece en ella
    /// hasta que el hechizo sale. Uno por mago, reutilizado en cada lanzamiento.
    /// Solo visual: no cambia tiempos ni daño.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpellCastCharge : MonoBehaviour
    {
        private static readonly string[] HandNames = { "handslot.l", "handslot.r", "hand.l", "hand.r" };

        private Transform anchor;
        private ParticleSystem motes;
        private ParticleSystemRenderer motesRenderer;
        private MeshRenderer orb;
        private MaterialPropertyBlock block;
        private Light glowLight;
        private CharacterController body;
        private Transform leftHand;
        private Transform rightHand;
        private bool handsSearched;
        private bool anchorPlaced;

        private VfxElement element;
        private VfxPalette palette;
        private Color glow;
        private Vector3 aim = Vector3.forward;
        private float started;
        private float until;
        private float emitDebt;
        private float releasedAt = -10f;
        private bool charging;

        public bool IsCharging => charging;
        public float Progress { get; private set; }
        public int LiveParticles => motes != null ? motes.particleCount : 0;
        public Vector3 HandPosition => anchor != null ? anchor.position : transform.position;
        public int Releases { get; private set; }

        public static SpellCastCharge For(Component caster)
        {
            if (caster == null)
                return null;
            SpellCastCharge charge = caster.GetComponent<SpellCastCharge>();
            return charge != null ? charge : caster.gameObject.AddComponent<SpellCastCharge>();
        }

        /// <summary>Empieza a concentrar energia durante <paramref name="seconds"/>.</summary>
        public void Begin(VfxElement kind, Color tint, float seconds, Vector3 aimDirection)
        {
            if (!Application.isPlaying)
                return;
            EnsureBuilt();
            element = kind;
            palette = VfxLibrary.Palette(kind, tint);
            glow = kind == VfxElement.Neutral ? VfxLibrary.WithAlpha(tint, 1f) : palette.Glow;
            aimDirection.y = 0f;
            aim = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : transform.forward;
            started = Time.time;
            until = Time.time + Mathf.Max(0.12f, seconds);
            emitDebt = 0f;
            charging = true;
            Progress = 0f;
            ConfigureMotes();
            anchor.position = HandPoint();
            anchorPlaced = true;
            // Unas cuantas desde el primer instante: los hechizos rapidos tambien se notan.
            Emit(Mathf.Clamp(Mathf.RoundToInt(seconds * 50f), 10, 20), Mathf.Clamp(seconds, 0.12f, 0.4f));
            orb.enabled = true;
            glowLight.enabled = true;
        }

        /// <summary>El hechizo sale: destello en la mano en la direccion del lanzamiento.</summary>
        public void Release(Vector3 direction)
        {
            if (!charging || anchor == null)
                return;
            charging = false;
            releasedAt = Time.time;
            Releases++;
            VfxHost host = VfxHost.Get();
            if (host != null)
            {
                Vector3 hand = anchor.position;
                host.Burst(element, hand, direction.sqrMagnitude > 0.001f ? direction : aim, palette.Spark, 12,
                    4.5f, 0.11f, 0.28f, 0.45f, 0.05f);
                host.Glints(hand, 0.08f, VfxLibrary.WithAlpha(Color.Lerp(glow, Color.white, 0.5f), 1f), 2, 0.55f, 0.16f, Vector3.zero);
            }
        }

        /// <summary>Hechizo interrumpido: la energia se disipa sin destello.</summary>
        public void Cancel()
        {
            charging = false;
            releasedAt = Time.time - 0.1f;
        }

        private void EnsureBuilt()
        {
            if (anchor != null)
                return;
            body = GetComponent<CharacterController>();
            block = new MaterialPropertyBlock();
            anchor = new GameObject("Energia en la mano — sin colision").transform;
            anchor.SetParent(transform, false);

            GameObject system = new GameObject("Particulas que convergen");
            system.transform.SetParent(anchor, false);
            motes = system.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = motes.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;   // siguen a la mano
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule emission = motes.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = motes.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule color = motes.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ParticleSystem.SizeOverLifetimeModule size = motes.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.35f)));
            motesRenderer = system.GetComponent<ParticleSystemRenderer>();
            motesRenderer.sortMode = ParticleSystemSortMode.None;
            VfxLibrary.ConfigureRenderer(motesRenderer);

            // Por encima del personaje: desde la camara la mano suele quedar detras del cuerpo.
            orb = VfxLibrary.CreateQuad("Brillo en la mano", anchor, false, VfxLibrary.Overlay(VfxLibrary.AdditiveGlow));
            orb.enabled = false;
            glowLight = PrototypeVfx.CreatePointLight(anchor, Color.white, 3f, 0f);
            glowLight.enabled = false;
        }

        private void ConfigureMotes()
        {
            // Materiales "encima": la energia se ve aunque el cuerpo tape la mano.
            switch (element)
            {
                case VfxElement.Frost:
                    motesRenderer.sharedMaterial = VfxLibrary.Overlay(VfxLibrary.AdditiveShard);
                    motesRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                    motesRenderer.velocityScale = 0.06f;
                    motesRenderer.lengthScale = 1.4f;
                    break;
                case VfxElement.Storm:
                case VfxElement.Light:
                    motesRenderer.sharedMaterial = VfxLibrary.Overlay(VfxLibrary.AdditiveGlow);
                    motesRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                    motesRenderer.velocityScale = 0.07f;
                    motesRenderer.lengthScale = 2f;
                    break;
                case VfxElement.Shadow:
                    motesRenderer.sharedMaterial = VfxLibrary.Overlay(VfxLibrary.AlphaSmoke);
                    motesRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                    break;
                case VfxElement.Blood:
                    motesRenderer.sharedMaterial = VfxLibrary.Overlay(VfxLibrary.AlphaDrop);
                    motesRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                    motesRenderer.velocityScale = 0.05f;
                    motesRenderer.lengthScale = 1.3f;
                    break;
                default:
                    motesRenderer.sharedMaterial = VfxLibrary.Overlay(VfxLibrary.AdditiveGlow);
                    motesRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                    break;
            }
        }

        private void Emit(int count, float lifetime)
        {
            if (motes == null || count <= 0)
                return;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            bool dark = element == VfxElement.Shadow || element == VfxElement.Blood;
            // Tamaño pensado para la camara lejana (~20 m): a esa distancia una mota de
            // 10 cm desaparece; de 20 cm se lee como chispa.
            float baseSize = element == VfxElement.Shadow ? 0.45f : 0.2f;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                float distance = Random.Range(0.75f, 1.25f);
                float life = lifetime * Random.Range(0.75f, 1f);
                Vector3 start = direction * distance;
                // Espiral suave hacia la mano.
                Vector3 swirl = Vector3.Cross(Vector3.up, direction).normalized * distance * 0.5f;
                emit.position = start;
                emit.velocity = (-start / life) * 1.02f + swirl;
                emit.startLifetime = life;
                emit.startSize = baseSize * Random.Range(0.7f, 1.2f);
                emit.startColor = dark ? palette.Spark : (Random.value < 0.3f ? palette.Core : palette.Spark);
                emit.rotation = Random.Range(0f, 360f);
                motes.Emit(emit, 1);
            }
        }

        private void LateUpdate()
        {
            if (anchor == null)
                return;
            Vector3 target = HandPoint();
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            anchor.position = anchorPlaced ? Vector3.Lerp(anchor.position, target, 1f - Mathf.Exp(-30f * dt)) : target;
            anchorPlaced = true;

            float sinceRelease = Time.time - releasedAt;
            if (charging)
            {
                float total = Mathf.Max(0.01f, until - started);
                Progress = Mathf.Clamp01((Time.time - started) / total);
                float remaining = until - Time.time;
                emitDebt += 90f * dt;
                int count = Mathf.FloorToInt(emitDebt);
                if (count > 0)
                {
                    emitDebt -= count;
                    Emit(Mathf.Min(count, 6), Mathf.Clamp(remaining, 0.18f, 0.42f));
                }
                if (Time.time >= until + 0.25f)
                    charging = false;   // el lanzamiento se perdio: no dejar el brillo encendido
            }

            float glowAmount = charging ? Mathf.Lerp(0.35f, 1f, Progress) : Mathf.Clamp01(1f - sinceRelease / 0.16f);
            if (glowAmount <= 0.001f)
            {
                if (orb.enabled)
                {
                    orb.enabled = false;
                    glowLight.enabled = false;
                }
                return;
            }
            VfxHost host = VfxHost.Get();
            VfxLibrary.FaceCamera(orb.transform, host != null ? host.ViewCamera : Camera.main);
            float flare = charging ? 1f : 1f + (1f - glowAmount) * 1.2f;
            float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 31f);
            orb.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.15f, Progress) * flare * flicker;
            VfxDisc.Tint(orb, block, VfxLibrary.WithAlpha(Color.Lerp(glow, Color.white, 0.35f), glowAmount));
            glowLight.color = glow;
            glowLight.range = 4.5f;
            glowLight.intensity = 3f * glowAmount * Mathf.Lerp(0.4f, 1f, Progress);
        }

        /// <summary>Mano mas adelantada hacia donde se apunta; sin esqueleto, delante del pecho.</summary>
        private Vector3 HandPoint()
        {
            if (!handsSearched)
            {
                handsSearched = true;
                Transform[] all = GetComponentsInChildren<Transform>(true);
                for (int n = 0; n < HandNames.Length; n++)
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i].name != HandNames[n])
                            continue;
                        if (HandNames[n].EndsWith(".l"))
                            leftHand = leftHand != null ? leftHand : all[i];
                        else
                            rightHand = rightHand != null ? rightHand : all[i];
                    }
            }
            Vector3 chest = body != null ? body.bounds.center + Vector3.up * 0.25f : transform.position + Vector3.up * 1.1f;
            Vector3 fallback = chest + aim * 0.5f;
            bool left = leftHand != null && leftHand.gameObject.activeInHierarchy;
            bool right = rightHand != null && rightHand.gameObject.activeInHierarchy;
            if (!left && !right)
                return fallback;
            if (left && right)
            {
                float l = Vector3.Dot(leftHand.position - chest, aim);
                float r = Vector3.Dot(rightHand.position - chest, aim);
                return (l >= r ? leftHand.position : rightHand.position) + aim * 0.12f;
            }
            return (left ? leftHand.position : rightHand.position) + aim * 0.12f;
        }
    }
}
