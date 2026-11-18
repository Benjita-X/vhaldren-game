using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Datos del golpe que se está resolviendo en este instante: si es crítico, con qué
    /// fuerza empuja, de qué tipo es y desde dónde llega. El atacante abre un ámbito con
    /// <see cref="Push"/> justo antes de llamar a <see cref="CombatDamageReceiver.Receive"/>;
    /// la respuesta del golpe (números, parón, empujón, reacción del modelo) lo lee mientras
    /// el receptor aplica el daño. Todo es síncrono y no asigna memoria.
    /// </summary>
    public static class CombatHitContext
    {
        public const float MaxForce = 4f;

        /// <summary>El golpe actual es crítico (números grandes, parón más largo).</summary>
        public static bool Critical { get; private set; }

        /// <summary>Multiplicador de empuje del golpe actual (1 = normal).</summary>
        public static float Force { get; private set; } = 1f;

        /// <summary>True mientras un receptor está aplicando daño (dentro de Receive).</summary>
        public static bool Resolving { get; private set; }

        /// <summary>Tipo de daño del golpe que se aplica ahora mismo.</summary>
        public static CombatDamageKind Kind { get; private set; }

        /// <summary>Origen del golpe que se aplica ahora mismo.</summary>
        public static Vector3 Source { get; private set; }

        /// <summary>Ámbito del atacante: al cerrarse devuelve los valores anteriores.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly bool previousCritical;
            private readonly float previousForce;

            internal Scope(bool critical, float force)
            {
                previousCritical = critical;
                previousForce = force;
            }

            public void Dispose()
            {
                Critical = previousCritical;
                Force = previousForce;
            }
        }

        /// <summary>Ámbito del receptor (tipo y origen); lo usa CombatDamageReceiver.</summary>
        public readonly struct Resolution : IDisposable
        {
            private readonly bool previousResolving;
            private readonly CombatDamageKind previousKind;
            private readonly Vector3 previousSource;

            internal Resolution(bool resolving, CombatDamageKind kind, Vector3 source)
            {
                previousResolving = resolving;
                previousKind = kind;
                previousSource = source;
            }

            public void Dispose()
            {
                Resolving = previousResolving;
                Kind = previousKind;
                Source = previousSource;
            }
        }

        /// <summary>Marca el próximo golpe como crítico o con más empuje (usar con <c>using</c>).</summary>
        public static Scope Push(bool critical, float force = 1f)
        {
            Scope scope = new Scope(Critical, Force);
            Critical = critical;
            Force = float.IsNaN(force) || float.IsInfinity(force) ? 1f : Mathf.Clamp(force, 0f, MaxForce);
            return scope;
        }

        internal static Resolution Resolve(CombatDamageKind kind, Vector3 source)
        {
            Resolution scope = new Resolution(Resolving, Kind, Source);
            Resolving = true;
            Kind = kind;
            Source = source;
            return scope;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Critical = false;
            Force = 1f;
            Resolving = false;
            Kind = CombatDamageKind.Melee;
            Source = Vector3.zero;
        }
    }
}
