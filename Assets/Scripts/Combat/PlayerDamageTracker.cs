using System;
using UnityEngine;

namespace Vhaldren.Gameplay.Combat
{
    /// <summary>
    /// Fuente unica de estadisticas de dano ofensivo local: flechas,
    /// espada, magia y futuras habilidades notifican solo PV reales.
    /// No aplica dano, no crea VFX ni decide autoridad de red.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDamageTracker : MonoBehaviour
    {
        private readonly float[] totalByKind =
            new float[(int)CombatDamageKind.DamageOverTime + 1];
        private readonly int[] hitsByKind =
            new int[(int)CombatDamageKind.DamageOverTime + 1];

        public float TotalDamage { get; private set; }
        public float LastDamage { get; private set; }
        public int TotalHits { get; private set; }
        public CombatDamageKind LastKind { get; private set; }
        public event Action<float, CombatDamageKind> DamageRecorded;

        public float DamageFor(CombatDamageKind kind)
        {
            int index = (int)kind;
            return index >= 0 && index < totalByKind.Length ? totalByKind[index] : 0f;
        }

        public int HitsFor(CombatDamageKind kind)
        {
            int index = (int)kind;
            return index >= 0 && index < hitsByKind.Length ? hitsByKind[index] : 0;
        }

        public bool Record(float actualDamage, CombatDamageKind kind)
        {
            if (float.IsNaN(actualDamage) || float.IsInfinity(actualDamage) ||
                actualDamage <= 0f)
                return false;

            int index = (int)kind;
            if (index < 0 || index >= totalByKind.Length)
                return false;

            TotalDamage += actualDamage;
            LastDamage = actualDamage;
            LastKind = kind;
            TotalHits++;
            totalByKind[index] += actualDamage;
            hitsByKind[index]++;
            DamageRecorded?.Invoke(actualDamage, kind);
            return true;
        }
    }
}
