using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Respuesta visual de la esquiva: estela mientras dura y un indicador que se apaga
    /// durante la espera. Solo lee el estado del motor; no cambia el movimiento.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DashFeedback : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private TrailRenderer trail;
        [Tooltip("Opcional: se ve encendido cuando la esquiva esta lista y apagado durante la espera.")]
        [SerializeField] private Renderer readyIndicator;
        [SerializeField] private Color readyColor = new Color(1f, 0.89f, 0.6f);
        [SerializeField] private Color waitingColor = new Color(0.28f, 0.22f, 0.3f);

        private MaterialPropertyBlock block;
        private bool indicatorShowsReady;
        private bool indicatorInitialized;

        private void Awake()
        {
            if (motor == null)
                motor = GetComponentInParent<PlayerMotor>();
            if (trail != null)
                trail.emitting = false;
        }

        private void LateUpdate()
        {
            if (motor == null)
                return;

            if (trail != null)
                trail.emitting = motor.IsDashing || motor.DashMovedThisStep;

            if (readyIndicator == null)
                return;

            bool ready = motor.IsDashReady;
            if (indicatorInitialized && ready == indicatorShowsReady)
                return;

            indicatorInitialized = true;
            indicatorShowsReady = ready;
            if (block == null)
                block = new MaterialPropertyBlock();

            Color color = ready ? readyColor : waitingColor;
            readyIndicator.GetPropertyBlock(block);
            block.SetColor(ColorId, color * 0.2f);
            block.SetColor(EmissionId, color);
            readyIndicator.SetPropertyBlock(block);
        }
    }
}
