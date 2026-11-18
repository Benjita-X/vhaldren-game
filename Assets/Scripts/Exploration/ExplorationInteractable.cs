using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Algo del mundo que se usa con la tecla de recolectar (E) al acercarse: cofres,
    /// piedras de viaje, piedras de saber, bestias para domar. Se registran en una lista
    /// estatica (sin busquedas globales). <see cref="PlayerInteractor"/> elige el mas
    /// cercano; si un recurso para recolectar queda mas cerca, gana la recoleccion.
    /// </summary>
    public abstract class ExplorationInteractable : MonoBehaviour
    {
        private static readonly List<ExplorationInteractable> all = new List<ExplorationInteractable>(64);

        [SerializeField, Min(0.5f)] private float interactRadius = 2.6f;

        public static IReadOnlyList<ExplorationInteractable> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        public float InteractRadius
        {
            get => interactRadius;
            set => interactRadius = Mathf.Max(0.5f, float.IsNaN(value) ? 2.6f : value);
        }

        /// <summary>Se puede usar ahora (un cofre abierto ya no).</summary>
        public virtual bool CanInteract => isActiveAndEnabled;

        /// <summary>Lo que hace la tecla: «abrir el cofre», «domar Grifo».</summary>
        public abstract string PromptVerb { get; }

        /// <summary>Linea extra opcional del aviso (rareza, estado).</summary>
        public virtual string PromptDetail => null;

        /// <summary>0 = basta con pulsar; mas = hay que mantener la tecla esos segundos.</summary>
        public virtual float HoldSeconds => 0f;

        public virtual Color PromptColor => HudSkin.GoldBright;

        public virtual Vector3 InteractPoint => transform.position;

        public abstract void Interact(GameObject player);

        /// <summary>Progreso mientras se mantiene la tecla (0..1); 0 al soltar.</summary>
        public virtual void OnHoldProgress(float progress01) { }

        protected virtual void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        protected virtual void OnDisable() => all.Remove(this);

        /// <summary>El mas cercano en el plano dentro de su radio que se pueda usar; null si ninguno.</summary>
        public static ExplorationInteractable Nearest(Vector3 position, out float distance)
        {
            ExplorationInteractable best = null;
            float bestSquared = float.PositiveInfinity;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                ExplorationInteractable item = all[i];
                if (item == null)
                {
                    all.RemoveAt(i);
                    continue;
                }
                if (!item.CanInteract)
                    continue;
                Vector3 offset = item.InteractPoint - position;
                if (Mathf.Abs(offset.y) > 4f)
                    continue;
                offset.y = 0f;
                float squared = offset.sqrMagnitude;
                float radius = item.InteractRadius;
                if (squared <= radius * radius && squared < bestSquared)
                {
                    bestSquared = squared;
                    best = item;
                }
            }
            distance = best != null ? Mathf.Sqrt(bestSquared) : float.PositiveInfinity;
            return best;
        }
    }
}
