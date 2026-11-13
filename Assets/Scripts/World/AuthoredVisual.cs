using UnityEngine;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Marca una jerarquia visual creada por un artista (Blender, Maya, etc.).
    /// La logica de combate, IA, vida y colliders vive en la raiz del enemigo;
    /// este componente solo declara que NO deben generarse cuerpos provisionales encima.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthoredVisual : MonoBehaviour
    {
        [SerializeField] private string source = "Vhaldren";

        public string Source => source;

        public static Transform FindRoot(Transform anchor)
        {
            if (anchor == null)
                return null;

            AuthoredVisual marker = anchor.GetComponentInChildren<AuthoredVisual>(true);
            if (marker != null)
                return marker.transform;

            // Compatibilidad con los prefabs KayKit ya existentes.
            Animator animator = anchor.GetComponentInChildren<Animator>(true);
            if (animator != null)
                return animator.transform;

            Transform legacy = anchor.Find("Modelo texturizado");
            return legacy;
        }
    }
}
