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

            float weight = Mathf.Clamp01(applied / 40f);
            HitStop(Mathf.Lerp(0.03f, 0.085f, weight));
            Shake(Mathf.Lerp(0.05f, 0.16f, weight), 0.16f);
            Host().Burst(center, away, SparkColor(kind), Mathf.RoundToInt(Mathf.Lerp(8f, 20f, weight)), Mathf.Lerp(4f, 7f, weight));
            GameAudio.Play(kind == CombatDamageKind.Magic ? GameSound.SpellImpact : GameSound.HitFlesh, center,
                Mathf.Lerp(0.6f, 1f, weight));
            Vhaldren.Gameplay.World.PrototypeEnemy enemy = victim.GetComponent<Vhaldren.Gameplay.World.PrototypeEnemy>();
            if (enemy != null)
            {
                enemy.ApplyKnockback(away, Mathf.Lerp(0.25f, 0.8f, weight) * (enemy.IsBoss ? 0.3f : 1f));
                CombatHealth health = victim.GetComponent<CombatHealth>();
                if (health != null && health.IsDefeated)
                {
                    HitStop(0.12f);
                    Shake(0.2f, 0.25f);
                    GameAudio.Play(GameSound.EnemyDown, center, 1f, 0.05f);
                }
            }
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

        /// <summary>Golpe critico: un empujon extra de impacto.</summary>
        public static void Critical(Vector3 position)
        {
            if (!Enabled)
                return;
            HitStop(0.09f);
            Shake(0.12f, 0.15f);
            Host().Burst(position, Vector3.up, new Color(1f, 0.95f, 0.6f), 16, 6f);
        }

        /// <summary>Polvo al moverse o esquivar.</summary>
        public static void Dust(Vector3 feet, Vector3 direction, int count, float speed)
        {
            if (!Enabled)
                return;
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
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
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
            renderer.sharedMaterial = PrototypeVfx.SharedMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            // Viñeta roja: bordes de la pantalla al recibir daño.
            Color previous = GUI.color;
            GUI.color = new Color(0.7f, 0f, 0.03f, hurtAlpha);
            float edge = Mathf.Min(Screen.width, Screen.height) * 0.08f;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, edge), pixel);
            GUI.DrawTexture(new Rect(0f, Screen.height - edge, Screen.width, edge), pixel);
            GUI.DrawTexture(new Rect(0f, edge, edge, Screen.height - 2f * edge), pixel);
            GUI.DrawTexture(new Rect(Screen.width - edge, edge, edge, Screen.height - 2f * edge), pixel);
            GUI.color = previous;
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
