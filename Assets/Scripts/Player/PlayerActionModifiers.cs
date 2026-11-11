using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Punto de extension para habilidades y estados: sprint, ralentizacion,
    /// aturdimiento y bloqueo de ataques. Cada efecto usa su propia ficha;
    /// retirar uno no borra las restricciones de los demas.
    /// No decide autoridad de red ni lee dispositivos.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerActionModifiers : MonoBehaviour
    {
        [System.Flags]
        public enum LockedAction
        {
            None = 0,
            Movement = 1,
            Dash = 2,
            Attack = 4,
            Facing = 8
        }

        private struct Entry
        {
            public LockedAction Locks;
            public float SpeedMultiplier;
        }

        private readonly Dictionary<int, Entry> effects = new Dictionary<int, Entry>();
        private int nextId = 1;

        public int ActiveCount => effects.Count;
        public bool AllowsMovement => !HasLock(LockedAction.Movement);
        public bool AllowsDash => !HasLock(LockedAction.Dash);
        public bool AllowsAttack => !HasLock(LockedAction.Attack);
        public bool AllowsFacing => !HasLock(LockedAction.Facing);

        public float SpeedMultiplier
        {
            get
            {
                float combined = 1f;
                foreach (Entry effect in effects.Values)
                    combined = Mathf.Min(4f, combined * effect.SpeedMultiplier);
                return combined;
            }
        }

        /// <summary>
        /// Registra un efecto y devuelve su identificador. El usuario del token
        /// debe retirarlo al acabar la habilidad, expirar el estado o desactivarse.
        /// Un valor no finito de velocidad se interpreta como neutro.
        /// </summary>
        public int AddModifier(LockedAction locks = LockedAction.None, float speedMultiplier = 1f)
        {
            float safeSpeed = float.IsNaN(speedMultiplier) || float.IsInfinity(speedMultiplier)
                ? 1f : Mathf.Clamp(speedMultiplier, 0f, 4f);
            int id = nextId++;
            if (nextId <= 0)
                nextId = 1;
            // En caso de wrap-around, nunca reemplazar silenciosamente otro efecto.
            while (effects.ContainsKey(id))
                id = nextId++;
            effects.Add(id, new Entry { Locks = locks, SpeedMultiplier = safeSpeed });
            return id;
        }

        public bool RemoveModifier(int id) => effects.Remove(id);

        public void ClearModifiers()
        {
            effects.Clear();
            nextId = 1;
        }

        private void OnDisable() => ClearModifiers();

        private bool HasLock(LockedAction action)
        {
            foreach (Entry effect in effects.Values)
            {
                if ((effect.Locks & action) != 0)
                    return true;
            }
            return false;
        }
    }
}
