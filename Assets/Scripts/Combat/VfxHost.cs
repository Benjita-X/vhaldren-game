using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Reserva de objetos de efecto: se reutilizan en vez de crear y destruir uno por
    /// hechizo. Los objetos sueltos se guardan desactivados bajo el anfitrion.
    /// </summary>
    public sealed class VfxPool<T> where T : Component
    {
        private readonly Stack<T> free = new Stack<T>();
        private readonly Func<Transform, T> factory;
        private readonly Transform parent;

        public VfxPool(Transform parent, Func<Transform, T> factory)
        {
            this.parent = parent;
            this.factory = factory;
        }

        /// <summary>Objetos creados en total (no crece si se reutilizan bien).</summary>
        public int Created { get; private set; }
        public int Available => free.Count;

        public T Take()
        {
            while (free.Count > 0)
            {
                T item = free.Pop();
                if (item != null)   // una prueba o un cambio de escena pudo destruirlo
                    return item;
            }
            Created++;
            return factory(parent);
        }

        public void Release(T item)
        {
            if (item == null)
                return;
            item.gameObject.SetActive(false);
            if (item.transform.parent != parent)
                item.transform.SetParent(parent, false);
            free.Push(item);
        }
    }

    /// <summary>
    /// Anfitrion de los efectos de la escena: reservas de objetos y un sistema de particulas
    /// por elemento que emite donde se le pida (EmitParams), sin crear objetos nuevos.
    /// Se crea solo la primera vez que algo lo necesita y muere con la escena.
    /// </summary>
    public sealed class VfxHost : MonoBehaviour
    {
        private static VfxHost instance;

        private readonly Dictionary<Type, object> pools = new Dictionary<Type, object>();
        private readonly ParticleSystem[] motes = new ParticleSystem[VfxLibrary.ElementCount];
        private ParticleSystem glints;
        private ParticleSystem smoke;
        private Camera cachedCamera;
        private int cameraFrame = -1;

        /// <summary>Anfitrion de la escena actual; null fuera del modo juego.</summary>
        public static VfxHost Get()
        {
            if (!Application.isPlaying)
                return null;
            if (instance == null)
            {
                GameObject go = new GameObject("Efectos visuales (reservas)");
                instance = go.AddComponent<VfxHost>();
            }
            return instance;
        }

        public static bool Exists => instance != null;

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public VfxPool<T> Pool<T>(Func<Transform, T> factory) where T : Component
        {
            if (pools.TryGetValue(typeof(T), out object existing))
                return (VfxPool<T>)existing;
            VfxPool<T> pool = new VfxPool<T>(transform, factory);
            pools[typeof(T)] = pool;
            return pool;
        }

        /// <summary>Camara principal, consultada como mucho una vez por fotograma.</summary>
        public Camera ViewCamera
        {
            get
            {
                if (cameraFrame != Time.frameCount || cachedCamera == null)
                {
                    cameraFrame = Time.frameCount;
                    cachedCamera = Camera.main;
                }
                return cachedCamera;
            }
        }

        // ── Emision ────────────────────────────────────────────────────

        /// <summary>
        /// Estallido de particulas del elemento: salen en un abanico alrededor de
        /// <paramref name="normal"/> (spread 0 = cono estrecho, 1 = esfera completa).
        /// </summary>
        public void Burst(VfxElement element, Vector3 position, Vector3 normal, Color tint, int count,
            float speed, float size, float lifetime, float spread = 0.7f, float jitter = 0.05f)
        {
            ParticleSystem system = Motes(element);
            if (system == null || count <= 0)
                return;
            Vector3 axis = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            count = Mathf.Min(count, 80);
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Vector3.Slerp(axis, UnityEngine.Random.onUnitSphere, Mathf.Clamp01(spread) * UnityEngine.Random.value);
                if (element == VfxElement.Fire)
                    direction.y = Mathf.Abs(direction.y) * 0.8f + 0.25f;   // las brasas suben
                emit.position = position + UnityEngine.Random.insideUnitSphere * jitter;
                emit.velocity = direction.normalized * speed * UnityEngine.Random.Range(0.45f, 1f);
                emit.startSize = size * UnityEngine.Random.Range(0.6f, 1.15f);
                emit.startLifetime = lifetime * UnityEngine.Random.Range(0.6f, 1f);
                emit.startColor = Vary(tint, element);
                emit.rotation = UnityEngine.Random.Range(0f, 360f);
                emit.angularVelocity = UnityEngine.Random.Range(-90f, 90f);
                system.Emit(emit, 1);
            }
        }

        /// <summary>Particulas que se quedan atras de algo en movimiento (estelas).</summary>
        public void Trail(VfxElement element, Vector3 position, Vector3 velocity, Color tint, int count,
            float size, float lifetime, float jitter, float drift)
        {
            ParticleSystem system = Motes(element);
            if (system == null || count <= 0)
                return;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 random = UnityEngine.Random.insideUnitSphere;
                emit.position = position + random * jitter;
                Vector3 v = velocity + random * drift;
                if (element == VfxElement.Fire)
                    v.y += drift * 0.8f;
                else if (element == VfxElement.Blood)
                    v.y -= drift * 0.5f;
                emit.velocity = v;
                emit.startSize = size * UnityEngine.Random.Range(0.55f, 1.1f);
                emit.startLifetime = lifetime * UnityEngine.Random.Range(0.6f, 1f);
                emit.startColor = Vary(tint, element);
                emit.rotation = UnityEngine.Random.Range(0f, 360f);
                emit.angularVelocity = UnityEngine.Random.Range(-120f, 120f);
                system.Emit(emit, 1);
            }
        }

        /// <summary>Particulas repartidas en un anillo que salen hacia fuera y hacia arriba.</summary>
        public void Ring(VfxElement element, Vector3 center, Vector3 up, float radius, Color tint, int count,
            float outward, float upward, float size, float lifetime)
        {
            ParticleSystem system = Motes(element);
            if (system == null || count <= 0)
                return;
            Vector3 axis = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 other = Vector3.Cross(axis, side);
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            count = Mathf.Min(count, 120);
            float offset = UnityEngine.Random.value * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = offset + i * Mathf.PI * 2f / count + UnityEngine.Random.Range(-0.08f, 0.08f);
                Vector3 radial = side * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                emit.position = center + radial * radius * UnityEngine.Random.Range(0.9f, 1.05f) + axis * 0.05f;
                emit.velocity = radial * outward * UnityEngine.Random.Range(0.6f, 1.1f) +
                                axis * upward * UnityEngine.Random.Range(0.5f, 1.2f);
                emit.startSize = size * UnityEngine.Random.Range(0.6f, 1.1f);
                emit.startLifetime = lifetime * UnityEngine.Random.Range(0.65f, 1f);
                emit.startColor = Vary(tint, element);
                emit.rotation = UnityEngine.Random.Range(0f, 360f);
                emit.angularVelocity = UnityEngine.Random.Range(-90f, 90f);
                system.Emit(emit, 1);
            }
        }

        /// <summary>Destellos suaves que flotan (luz, chispas lentas, motas magicas).</summary>
        public void Glints(Vector3 position, float radius, Color tint, int count, float size, float lifetime, Vector3 drift)
        {
            if (glints == null)
                glints = CreateSoftSystem("Destellos", VfxLibrary.AdditiveGlow, 0f, false);
            Scatter(glints, position, radius, tint, count, size, lifetime, drift);
        }

        /// <summary>Bocanadas de humo o polvo que tapan (alfa).</summary>
        public void Smoke(Vector3 position, float radius, Color tint, int count, float size, float lifetime, Vector3 drift)
        {
            if (smoke == null)
                smoke = CreateSoftSystem("Humo y polvo", VfxLibrary.AlphaSmoke, -0.04f, true);
            Scatter(smoke, position, radius, tint, count, size, lifetime, drift);
        }

        private static void Scatter(ParticleSystem system, Vector3 position, float radius, Color tint, int count,
            float size, float lifetime, Vector3 drift)
        {
            if (system == null || count <= 0)
                return;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            count = Mathf.Min(count, 80);
            for (int i = 0; i < count; i++)
            {
                Vector3 random = UnityEngine.Random.insideUnitSphere;
                emit.position = position + random * radius;
                emit.velocity = drift + random * drift.magnitude * 0.35f;
                emit.startSize = size * UnityEngine.Random.Range(0.6f, 1.2f);
                emit.startLifetime = lifetime * UnityEngine.Random.Range(0.6f, 1f);
                emit.startColor = tint;
                emit.rotation = UnityEngine.Random.Range(0f, 360f);
                emit.angularVelocity = UnityEngine.Random.Range(-40f, 40f);
                system.Emit(emit, 1);
            }
        }

        /// <summary>Particulas vivas de todos los sistemas (para pruebas y diagnostico).</summary>
        public int LiveParticles
        {
            get
            {
                int total = 0;
                for (int i = 0; i < motes.Length; i++)
                    if (motes[i] != null)
                        total += motes[i].particleCount;
                if (glints != null)
                    total += glints.particleCount;
                if (smoke != null)
                    total += smoke.particleCount;
                return total;
            }
        }

        private static Color32 Vary(Color tint, VfxElement element)
        {
            float shade = UnityEngine.Random.Range(0.85f, 1.1f);
            Color c = new Color(tint.r * shade, tint.g * shade, tint.b * shade, tint.a);
            if (element == VfxElement.Storm && UnityEngine.Random.value < 0.35f)
                c = Color.Lerp(c, Color.white, 0.6f);
            return c;
        }

        // ── Sistemas por elemento ──────────────────────────────────────

        private ParticleSystem Motes(VfxElement element)
        {
            int index = Mathf.Clamp((int)element, 0, motes.Length - 1);
            if (motes[index] == null)
                motes[index] = CreateElementSystem((VfxElement)index);
            return motes[index];
        }

        private ParticleSystem CreateElementSystem(VfxElement element)
        {
            switch (element)
            {
                case VfxElement.Fire:
                    return Build("Brasas", VfxLibrary.AdditiveGlow, ParticleSystemRenderMode.Billboard, -0.35f, 0.6f, 1.2f,
                        Gradient3(new Color(1f, 1f, 0.9f), new Color(1f, 0.62f, 0.25f), new Color(0.7f, 0.16f, 0.05f), 0.9f, 0f),
                        Curve(1f, 0.85f, 0.25f), 0f, 0f, 1.6f);
                case VfxElement.Frost:
                    return Build("Esquirlas de hielo", VfxLibrary.AdditiveShard, ParticleSystemRenderMode.Stretch, 0.55f, 0f, 1f,
                        Gradient3(Color.white, new Color(0.75f, 0.95f, 1f), new Color(0.45f, 0.8f, 1f), 1f, 0f),
                        Curve(1f, 0.9f, 0.45f), 0.05f, 1.6f, 2.4f);
                case VfxElement.Storm:
                    return Build("Chispas de tormenta", VfxLibrary.AdditiveGlow, ParticleSystemRenderMode.Stretch, 0.25f, 0.35f, 2.5f,
                        Gradient3(Color.white, new Color(0.85f, 0.8f, 1f), new Color(0.5f, 0.45f, 1f), 1f, 0f),
                        Curve(1f, 0.7f, 0.15f), 0.045f, 2.2f, 4f);
                case VfxElement.Shadow:
                    return Build("Humo de sombra", VfxLibrary.AlphaSmoke, ParticleSystemRenderMode.Billboard, -0.08f, 0.45f, 0.8f,
                        GradientFade(0.15f, 0.85f), Curve(0.55f, 1.15f, 1.6f), 0f, 0f, 1.4f);
                case VfxElement.Blood:
                    return Build("Gotas de sangre", VfxLibrary.AlphaDrop, ParticleSystemRenderMode.Stretch, 1.35f, 0f, 1f,
                        GradientFade(0f, 0.85f), Curve(1f, 0.95f, 0.7f), 0.04f, 1.3f, 0.8f);
                case VfxElement.Light:
                    return Build("Rayos de luz", VfxLibrary.AdditiveGlow, ParticleSystemRenderMode.Stretch, 0f, 0.15f, 1f,
                        Gradient3(Color.white, new Color(1f, 0.95f, 0.75f), new Color(1f, 0.8f, 0.4f), 1f, 0f),
                        Curve(1f, 0.8f, 0.3f), 0.11f, 3.2f, 3f);
                default:
                    return Build("Chispas", VfxLibrary.AdditiveGlow, ParticleSystemRenderMode.Stretch, 0.7f, 0f, 1f,
                        Gradient3(Color.white, Color.white, Color.white, 1f, 0f),
                        Curve(1f, 0.75f, 0.2f), 0.045f, 1.6f, 2.6f);
            }
        }

        private ParticleSystem CreateSoftSystem(string name, Material material, float gravity, bool grow)
        {
            return Build(name, material, ParticleSystemRenderMode.Billboard, gravity, grow ? 0.25f : 0f, 0.6f,
                GradientFade(grow ? 0.12f : 0.05f, grow ? 0.75f : 1f),
                grow ? Curve(0.6f, 1.1f, 1.5f) : Curve(0.4f, 1f, 0f), 0f, 0f, 0.6f);
        }

        private ParticleSystem Build(string name, Material material, ParticleSystemRenderMode mode, float gravity,
            float noise, float noiseFrequency, Gradient color, AnimationCurve size, float velocityScale,
            float lengthScale, float drag)
        {
            GameObject go = new GameObject("Particulas " + name);
            go.transform.SetParent(transform, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.startSize = 0.1f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = color;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, size);

            if (drag > 0f)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 60f;
                limit.dampen = 0f;
                limit.drag = drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = true;
            }

            if (noise > 0f)
            {
                ParticleSystem.NoiseModule turbulence = system.noise;
                turbulence.enabled = true;
                turbulence.strength = noise;
                turbulence.frequency = noiseFrequency;
                turbulence.scrollSpeed = 0.6f;
                turbulence.quality = ParticleSystemNoiseQuality.Medium;
            }

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = velocityScale;
                renderer.lengthScale = lengthScale;
            }
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 1f;
            VfxLibrary.ConfigureRenderer(renderer);
            return system;
        }

        private static Gradient Gradient3(Color start, Color middle, Color end, float startAlpha, float endAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, 0.35f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, 0.55f), new GradientAlphaKey(endAlpha, 1f) });
            return gradient;
        }

        /// <summary>Aparece rapido, se mantiene y se desvanece (humo, gotas).</summary>
        private static Gradient GradientFade(float fadeIn, float peak)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(fadeIn > 0f ? 0f : peak, 0f),
                    new GradientAlphaKey(peak, Mathf.Max(0.01f, fadeIn)),
                    new GradientAlphaKey(peak, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static AnimationCurve Curve(float start, float middle, float end)
        {
            return new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.3f, middle), new Keyframe(1f, end));
        }
    }
}
