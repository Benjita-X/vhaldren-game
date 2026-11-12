using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>Parpadeo visual del fuego de la hoguera. Solo apariencia.</summary>
    public sealed class CampfireFlicker : MonoBehaviour
    {
        private Vector3 baseScale;
        private Light fireLight;

        private void Awake()
        {
            baseScale = transform.localScale;
            fireLight = transform.parent != null ? transform.parent.GetComponentInChildren<Light>() : null;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(Time.time * 6f, transform.position.x);
            transform.localScale = new Vector3(baseScale.x * (0.85f + n * 0.3f), baseScale.y * (0.8f + n * 0.45f), baseScale.z * (0.85f + n * 0.3f));
            if (fireLight != null)
                fireLight.intensity = 2f + n * 1.2f;
        }
    }
}
