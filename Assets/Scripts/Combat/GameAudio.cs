using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>Sonidos del juego. Todos se sintetizan por codigo (originales, sin archivos).</summary>
    public enum GameSound
    {
        HitFlesh,      // golpe que hace daño
        Slash,         // corte de espada / zarpazo
        Metal,         // parada o bloqueo con escudo
        SpellCast,     // conjurar
        SpellImpact,   // explosion magica
        Frost,         // hielo
        BowString,     // ballesta
        Footstep,      // paso
        Whoosh,        // esquiva
        PlayerHurt,    // jugador herido
        EnemyDown      // enemigo derrotado
    }

    /// <summary>
    /// Sintetiza los efectos una sola vez y los reproduce con una reserva de fuentes,
    /// con un poco de variacion de tono para que nunca suenen identicos.
    /// </summary>
    public static class GameAudio
    {
        private const int Rate = 44100;
        private const int Voices = 12;

        private static readonly Dictionary<GameSound, AudioClip> clips = new Dictionary<GameSound, AudioClip>();
        private static AudioSource[] sources;
        private static GameObject host;
        private static int next;
        private static System.Random random = new System.Random(77);
        private static float masterVolume = 0.8f;

        public static bool Muted { get; set; }
        public static int PlayedCount { get; private set; }

        public static void Play(GameSound sound, Vector3 position, float volume = 1f, float pitchJitter = 0.08f)
        {
            if (Muted || !Application.isPlaying)
                return;
            EnsureSources();
            AudioClip clip = Clip(sound);
            AudioSource source = sources[next];
            next = (next + 1) % sources.Length;
            source.transform.position = position;
            source.pitch = 1f + ((float)random.NextDouble() * 2f - 1f) * pitchJitter;
            source.PlayOneShot(clip, Mathf.Clamp01(volume) * masterVolume * Vhaldren.Gameplay.Settings.GameSettings.EffectsVolume);
            PlayedCount++;
        }

        public static AudioClip Clip(GameSound sound)
        {
            if (!clips.TryGetValue(sound, out AudioClip clip) || clip == null)
            {
                clip = Synthesize(sound);
                clips[sound] = clip;
            }
            return clip;
        }

        private static void EnsureSources()
        {
            if (host != null)
                return;
            host = new GameObject("Audio del juego");
            Object.DontDestroyOnLoad(host);
            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                GameObject voice = new GameObject("Voz " + i);
                voice.transform.SetParent(host.transform, false);
                AudioSource source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.35f;   // vista cenital: algo de direccion sin perder volumen
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 6f;
                source.maxDistance = 45f;
                sources[i] = source;
            }
        }

        // ── Sintesis ───────────────────────────────────────────────────

        private static AudioClip Synthesize(GameSound sound)
        {
            System.Random noise = new System.Random((int)sound * 9973 + 11);
            float seconds = Length(sound);
            int count = Mathf.CeilToInt(seconds * Rate);
            float[] data = new float[count];
            float low = 0f, low2 = 0f;
            // Karplus-Strong para la cuerda de la ballesta.
            float[] pluck = null;
            if (sound == GameSound.BowString)
            {
                pluck = new float[Rate / 196];
                for (int i = 0; i < pluck.Length; i++)
                    pluck[i] = (float)noise.NextDouble() * 2f - 1f;
            }
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float white = (float)noise.NextDouble() * 2f - 1f;
                float s = 0f;
                switch (sound)
                {
                    case GameSound.HitFlesh:
                    {
                        float f = 55f + 120f * Mathf.Exp(-t * 30f);
                        phase += f / Rate;
                        low += (white - low) * 0.25f;
                        s = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 22f) * 0.9f +
                            low * Mathf.Exp(-t * 55f) * 0.8f;
                        break;
                    }
                    case GameSound.Slash:
                    case GameSound.Whoosh:
                    {
                        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds));
                        float cutoff = sound == GameSound.Slash ? 0.08f + 0.35f * (t / seconds) : 0.05f + 0.12f * env;
                        low += (white - low) * cutoff;
                        low2 += (low - low2) * cutoff;
                        s = (low - low2 * 0.6f) * env * (sound == GameSound.Slash ? 1.6f : 1.2f);
                        break;
                    }
                    case GameSound.Metal:
                    {
                        float env = Mathf.Exp(-t * 7f);
                        s = (Mathf.Sin(2f * Mathf.PI * 523f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1187f * t) * 0.35f +
                             Mathf.Sin(2f * Mathf.PI * 1731f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 2643f * t) * 0.18f) * env +
                            white * Mathf.Exp(-t * 120f) * 0.6f;
                        break;
                    }
                    case GameSound.SpellCast:
                    {
                        float f = 330f + 900f * (t / seconds);
                        phase += f * (1f + 0.02f * Mathf.Sin(t * 60f)) / Rate;
                        float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 6f);
                        s = (Mathf.Sin(phase * Mathf.PI * 2f) * 0.6f + Mathf.Sin(phase * Mathf.PI * 6f) * 0.2f) * env;
                        break;
                    }
                    case GameSound.SpellImpact:
                    {
                        low += (white - low) * 0.12f;
                        s = low * Mathf.Exp(-t * 9f) * 1.2f +
                            Mathf.Sin(2f * Mathf.PI * (70f + 40f * Mathf.Exp(-t * 15f)) * t) * Mathf.Exp(-t * 8f) * 0.8f +
                            Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 25f) * 0.12f;
                        break;
                    }
                    case GameSound.Frost:
                    {
                        float crackle = noise.NextDouble() < 0.02 * Mathf.Exp(-t * 6f) ? white * 2.5f : 0f;
                        low += (white - low) * 0.6f;
                        s = crackle + (white - low) * Mathf.Exp(-t * 14f) * 0.35f +
                            Mathf.Sin(2f * Mathf.PI * 2960f * t) * Mathf.Exp(-t * 10f) * 0.15f;
                        break;
                    }
                    case GameSound.BowString:
                    {
                        int k = i % pluck.Length;
                        int k2 = (k + 1) % pluck.Length;
                        float value = pluck[k];
                        pluck[k] = 0.5f * (pluck[k] + pluck[k2]) * 0.994f;
                        s = value * 0.8f + white * Mathf.Exp(-t * 200f) * 0.4f;
                        break;
                    }
                    case GameSound.Footstep:
                    {
                        low += (white - low) * 0.18f;
                        s = low * Mathf.Exp(-t * 45f) * 1.4f;
                        break;
                    }
                    case GameSound.PlayerHurt:
                    {
                        float f = 150f - 50f * (t / seconds);
                        phase += f / Rate;
                        float saw = (phase % 1f) * 2f - 1f;
                        low += (white - low) * 0.3f;
                        s = (saw * 0.45f + low * 0.6f) * Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 12f);
                        break;
                    }
                    case GameSound.EnemyDown:
                    {
                        low += (white - low) * 0.06f;
                        s = Mathf.Sin(2f * Mathf.PI * (48f + 30f * Mathf.Exp(-t * 6f)) * t) * Mathf.Exp(-t * 5f) * 0.9f +
                            low * Mathf.Exp(-t * 4f) * 1.2f;
                        break;
                    }
                }
                data[i] = Mathf.Clamp(s, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("Sonido " + sound, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Length(GameSound sound)
        {
            switch (sound)
            {
                case GameSound.Footstep: return 0.09f;
                case GameSound.HitFlesh: return 0.2f;
                case GameSound.Slash: return 0.2f;
                case GameSound.Whoosh: return 0.28f;
                case GameSound.Metal: return 0.55f;
                case GameSound.SpellCast: return 0.3f;
                case GameSound.SpellImpact: return 0.5f;
                case GameSound.Frost: return 0.45f;
                case GameSound.BowString: return 0.3f;
                case GameSound.PlayerHurt: return 0.25f;
                default: return 0.6f;
            }
        }
    }
}
