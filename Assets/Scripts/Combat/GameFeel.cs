using System.Collections;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Game feel de cada golpe (entrada → accion → respuesta): parón breve (hit stop),
    /// temblor de camara, chispas hacia fuera del atacante, empujon, sonido y destello
    /// rojo al recibir daño. Lo dispara CombatDamageReceiver solo con daño REAL aplicado.
    /// Desactivado por defecto: lo activa la sesion del mundo, no las salas de prueba.
    /// </summary>
    public static class GameFeel
    {
        public const float HitStopScale = 0.04f;
        public const float MinHitStopGap = 0.1f;

        private static GameFeelHost host;
        private static float lastHitStopAt = -10f;

        public static bool Enabled { get; set; }
        public static Vector3 CameraOffset => host != null ? host.Offset : Vector3.zero;
        public static int HitStops { get; private set; }

        /// <summary>Respuesta a un golpe que quito vida.</summary>
        public static void OnHit(CombatDamageReceiver victim, float applied, CombatDamageKind kind, Vector3 source)
        {
            if (!Enabled || victim == null || applied <= 0f || !Application.isPlaying)
                return;
            bool playerHurt = victim.GetComponent<Vhaldren.Gameplay.Player.PlayerMotor>() != null;
            Vector3 center = Center(victim);

            // Daño periódico (fuego/venenos futuros) conserva números y vida real,
            // pero no debe congelar el juego ni empujar al objetivo cada segundo.
            if (kind == CombatDamageKind.DamageOverTime)
            {
                if (playerHurt)
                    Host().FlashHurt(Mathf.Clamp01(applied / 50f) * 0.45f);
                Host().Burst(center, Vector3.up, new Color(1f, 0.34f, 0.08f, 0.75f),
                    4, 1.8f, 0.22f, 0.07f);
                return;
            }

            Vector3 away = center - source;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -victim.transform.forward;

            if (playerHurt)
            {
                HitStop(0.06f);
                Shake(0.22f, 0.22f);
                Host().FlashHurt(Mathf.Clamp01(applied / 30f));
                GameAudio.Play(GameSound.PlayerHurt, center, 0.9f);
                Host().Burst(center, away, new Color(0.75f, 0.05f, 0.08f), 10, 3.5f);
                return;
            }

            // Respuesta proporcional: golpes flojos apenas paran el tiempo; fuertes, criticos
            // y el golpe que remata congelan un instante mas, empujan mas y sueltan mas chispas.
            bool critical = CombatHitContext.Critical;
            float force = CombatHitContext.Force;
            CombatHealth health = victim.GetComponent<CombatHealth>();
            bool killed = health != null && health.IsDefeated;
            float maxHealth = health != null ? health.MaximumHealth : 100f;
            float weight = HitWeight(applied, critical);
            HitStop(HitStopSeconds(applied, critical, killed));
            Shake(Mathf.Lerp(0.05f, 0.17f, weight) * (critical ? 1.35f : 1f) * (killed ? 1.25f : 1f),
                critical || killed ? 0.22f : 0.16f);
            Host().Burst(center, away, SparkColor(kind), Mathf.RoundToInt(Mathf.Lerp(8f, 22f, weight)), Mathf.Lerp(4f, 7.5f, weight));
            if (critical)
                Host().Burst(center, Vector3.up, new Color(1f, 0.86f, 0.45f), 18, 7f, 0.34f, 0.13f);
            GameAudio.Play(kind == CombatDamageKind.Magic ? GameSound.SpellImpact : GameSound.HitFlesh, center,
                Mathf.Lerp(0.6f, 1f, weight));
            Vhaldren.Gameplay.World.PrototypeEnemy enemy = victim.GetComponent<Vhaldren.Gameplay.World.PrototypeEnemy>();
            if (enemy != null)
            {
                if (killed)
                {
                    // Muerte con impulso: el cuerpo sale despedido en la direccion del golpe.
                    enemy.LaunchCorpse(away, DeathImpulse(applied, maxHealth, critical, force, enemy.IsBoss));
                    Shake(0.2f, 0.25f);
                    GameAudio.Play(GameSound.EnemyDown, center, 1f, 0.05f);
                }
                else
                {
                    enemy.ApplyKnockback(away, KnockbackDistance(applied, maxHealth, kind, enemy.IsBoss, critical, force));
                }
            }
        }

        /// <summary>Peso de un golpe entre 0 y 1 (40 de daño = golpe pesado; el crítico pesa más).</summary>
        public static float HitWeight(float applied, bool critical) =>
            Mathf.Clamp01(Mathf.Max(0f, applied) / 40f * (critical ? 1.4f : 1f));

        /// <summary>Duración del parón: breve en golpes flojos, más en fuertes, críticos y remates.</summary>
        public static float HitStopSeconds(float applied, bool critical, bool killing)
        {
            float seconds = Mathf.Lerp(0.03f, 0.09f, HitWeight(applied, false));
            if (critical)
                seconds = Mathf.Max(seconds, 0.11f);
            if (killing)
                seconds = Mathf.Max(seconds, 0.12f);
            return Mathf.Min(seconds, 0.15f);
        }

        /// <summary>
        /// Metros que retrocede un enemigo: crece con el daño, baja con su vida máxima (un bruto
        /// apenas se mueve, un lobo sale volando), depende del tipo de golpe y de su fuerza.
        /// </summary>
        public static float KnockbackDistance(float applied, float maxHealth, CombatDamageKind kind, bool boss,
            bool critical, float force)
        {
            if (applied <= 0f || float.IsNaN(applied))
                return 0f;
            float distance = Mathf.Lerp(0.2f, 1.1f, Mathf.Clamp01(applied / 40f));
            distance *= Mathf.Clamp(80f / Mathf.Max(1f, maxHealth), 0.45f, 1.25f);
            switch (kind)
            {
                case CombatDamageKind.Arrow: distance *= 0.7f; break;
                case CombatDamageKind.Magic: distance *= 0.9f; break;
                case CombatDamageKind.Explosion: distance *= 1.25f; break;
                case CombatDamageKind.DamageOverTime: return 0f;
            }
            if (critical)
                distance *= 1.35f;
            distance *= float.IsNaN(force) ? 1f : Mathf.Clamp(force, 0f, CombatHitContext.MaxForce);
            if (boss)
                distance *= 0.3f;
            return Mathf.Clamp(distance, 0f, 3f);
        }

        /// <summary>Velocidad (m/s) con la que sale despedido el cuerpo del enemigo que cae.</summary>
        public static float DeathImpulse(float applied, float maxHealth, bool critical, float force, bool boss)
        {
            float overkill = Mathf.Clamp01(Mathf.Max(0f, applied) / Mathf.Max(1f, maxHealth * 0.5f));
            float speed = Mathf.Lerp(2.2f, 6.5f, overkill) * (critical ? 1.3f : 1f) *
                          Mathf.Clamp(float.IsNaN(force) ? 1f : force, 0.5f, 2.5f);
            return boss ? speed * 0.35f : Mathf.Min(speed, 11f);
        }

        /// <summary>Parada perfecta o bloqueo: choque metalico sin daño.</summary>
        public static void OnBlocked(CombatDamageReceiver victim, bool parried)
        {
            if (!Enabled || victim == null)
                return;
            Vector3 center = Center(victim);
            GameAudio.Play(GameSound.Metal, center, parried ? 1f : 0.6f);
            Host().Burst(center + victim.transform.forward * 0.5f, victim.transform.forward,
                new Color(1f, 0.85f, 0.4f), parried ? 18 : 8, 5f);
            if (parried)
            {
                HitStop(0.1f);
                Shake(0.14f, 0.18f);
            }
        }

        /// <summary>
        /// Golpe critico: destello dorado extra. El parón largo ya lo dio OnHit al leer
        /// <see cref="CombatHitContext.Critical"/>; aqui solo se intenta por si el golpe no
        /// paso por el receptor (no se encadena gracias a MinHitStopGap).
        /// </summary>
        public static void Critical(Vector3 position)
        {
            if (!Enabled)
                return;
            HitStop(0.11f);
            Shake(0.12f, 0.15f);
            Host().Burst(position, Vector3.up, new Color(1f, 0.95f, 0.6f), 16, 6f);
        }

        /// <summary>Polvo al moverse o esquivar: bocanadas suaves que tapan (no brillan).</summary>
        public static void Dust(Vector3 feet, Vector3 direction, int count, float speed)
        {
            if (!Enabled)
                return;
            VfxHost vfx = VfxHost.Get();
            if (vfx != null)
                vfx.Smoke(feet + Vector3.up * 0.12f, 0.18f, new Color(0.58f, 0.53f, 0.46f, 0.55f),
                    Mathf.Clamp(count, 1, 24), 0.32f, 0.6f, direction * (speed * 0.45f));
            else
                Host().Burst(feet + Vector3.up * 0.05f, direction, new Color(0.55f, 0.5f, 0.44f, 0.7f), count, speed, 0.5f, 0.22f);
        }

        public static void HitStop(float seconds)
        {
            if (!Enabled || seconds <= 0f || !Vhaldren.Gameplay.Settings.GameSettings.HitStop)
                return;
            // No encadenar parones en areas que golpean a muchos ni pisar la pausa.
            if (Time.unscaledTime - lastHitStopAt < MinHitStopGap || Time.timeScale < 0.99f)
                return;
            lastHitStopAt = Time.unscaledTime;
            HitStops++;
            Host().StartHitStop(Mathf.Clamp(seconds, 0f, 0.15f));
        }

        public static void Shake(float amplitude, float seconds)
        {
            if (!Enabled)
                return;
            float intensity = Vhaldren.Gameplay.Settings.GameSettings.ShakeIntensity;
            if (intensity > 0.01f)
                Host().AddShake(amplitude * intensity, seconds);
        }

        private static Vector3 Center(Component target)
        {
            Collider body = target.GetComponent<Collider>();
            return body != null ? body.bounds.center : target.transform.position + Vector3.up;
        }

        private static Color SparkColor(CombatDamageKind kind)
        {
            switch (kind)
            {
                case CombatDamageKind.Magic: return new Color(0.5f, 0.75f, 1f);
                case CombatDamageKind.Arrow: return new Color(1f, 0.9f, 0.6f);
                default: return new Color(1f, 0.55f, 0.2f);
            }
        }

        private static GameFeelHost Host()
        {
            if (host == null)
            {
                GameObject go = new GameObject("Game feel");
                host = go.AddComponent<GameFeelHost>();
            }
            return host;
        }
    }

    /// <summary>Ejecuta parones, temblor, particulas reutilizables y destello de daño.</summary>
    public sealed class GameFeelHost : MonoBehaviour
    {
        private const int PoolSize = 16;
        private readonly ParticleSystem[] pool = new ParticleSystem[PoolSize];
        private int nextParticle;
        private float shakeAmplitude;
        private float shakeUntil;
        private float shakeDuration = 0.1f;
        private float hurtAlpha;
        private bool stopping;
        private Texture2D pixel;

        public Vector3 Offset { get; private set; }

        public void StartHitStop(float seconds) => StartCoroutine(HitStopRoutine(seconds));

        private IEnumerator HitStopRoutine(float seconds)
        {
            stopping = true;
            Time.timeScale = GameFeel.HitStopScale;
            yield return new WaitForSecondsRealtime(seconds);
            // Si mientras tanto se abrio la pausa (timeScale 0) no la deshacemos.
            if (Mathf.Approximately(Time.timeScale, GameFeel.HitStopScale))
                Time.timeScale = 1f;
            stopping = false;
        }

        public void AddShake(float amplitude, float seconds)
        {
            shakeAmplitude = Mathf.Max(shakeAmplitude * Mathf.Clamp01((shakeUntil - Time.unscaledTime) / shakeDuration), amplitude);
            shakeDuration = Mathf.Max(0.05f, seconds);
            shakeUntil = Time.unscaledTime + shakeDuration;
        }

        public void FlashHurt(float strength) => hurtAlpha = Mathf.Max(hurtAlpha, Mathf.Lerp(0.18f, 0.42f, strength));

        public void Burst(Vector3 position, Vector3 direction, Color color, int count, float speed,
            float lifetime = 0.28f, float size = 0.11f)
        {
            ParticleSystem system = pool[nextParticle];
            if (system == null)
                system = pool[nextParticle] = CreateSystem(nextParticle);
            nextParticle = (nextParticle + 1) % PoolSize;
            system.transform.position = position;
            system.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.up);
            ParticleSystem.MainModule main = system.main;
            main.startColor = color;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            // La forma de luz tiene bordes suaves: algo mas grande para ocupar lo mismo.
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.9f, size * 1.6f);
            system.Emit(Mathf.Clamp(count, 1, 40));
        }

        private ParticleSystem CreateSystem(int index)
        {
            GameObject go = new GameObject("Particulas " + index);
            go.transform.SetParent(transform, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.9f;
            main.useUnscaledTime = true;   // las chispas siguen durante el hit stop
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.08f;
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.SizeOverLifetimeModule shrink = system.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            // Chispa con forma de luz (centro caliente y bordes suaves) en vez de un
            // rectangulo plano: los golpes se leen como destellos, no como cuadrados.
            Material spark = VfxLibrary.AdditiveGlow;
            renderer.sharedMaterial = spark != null ? spark : PrototypeVfx.SharedMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private void LateUpdate()
        {
            float remaining = shakeUntil - Time.unscaledTime;
            if (remaining > 0f)
            {
                float strength = shakeAmplitude * (remaining / shakeDuration);
                float t = Time.unscaledTime * 55f;
                Offset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, 0f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * (2f * strength);
            }
            else
            {
                Offset = Vector3.zero;
            }
            hurtAlpha = Mathf.MoveTowards(hurtAlpha, 0f, Time.unscaledDeltaTime * 1.4f);
        }

        private void OnGUI()
        {
            if (hurtAlpha <= 0.001f || Event.current.type != EventType.Repaint)
                return;
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1);
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }
            // Viñeta roja al recibir daño: el mismo degradado suave del HUD (sin bordes duros).
            Vhaldren.Gameplay.UI.HudSkin.DrawVignette(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.75f, 0f, 0.04f, Mathf.Clamp01(hurtAlpha * 1.6f)));
        }

        private void OnDisable()
        {
            // Nunca dejar el juego congelado si este objeto desaparece en mitad de un parón.
            if (stopping && Mathf.Approximately(Time.timeScale, GameFeel.HitStopScale))
                Time.timeScale = 1f;
            stopping = false;
            Offset = Vector3.zero;
        }
    }
}
