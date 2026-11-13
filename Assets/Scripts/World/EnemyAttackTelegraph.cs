using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Aviso visual de los ataques enemigos. No aplica dano ni decide impactos.
    /// Un disco en el suelo con el radio exacto del golpe: el borde marca el alcance y el
    /// relleno crece desde los pies del enemigo hasta tocar el borde justo cuando el golpe
    /// se resuelve. Al golpear destella en rojo. No necesita regenerar WorldPrototype001.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttackTelegraph : MonoBehaviour
    {
        private const float FlashDuration = 0.16f;

        [SerializeField, Range(0.03f, 0.18f)] private float ringWidth = 0.10f;

        private CharacterController controller;
        private MeshRenderer disc;
        private MaterialPropertyBlock block;
        private bool warningActive;
        private bool bossWarning;
        private bool magicWarning;
        private float warningStarted;
        private float warningEnds;
        private float flashEnds;
        private float radius = 1f;

        public bool WarningActive => warningActive;
        public bool IsVisible => disc != null && disc.enabled;
        /// <summary>Progreso del aviso: 0 al empezar la carga, 1 en el instante del golpe.</summary>
        public float Fill { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            block = new MaterialPropertyBlock();
            disc = VfxLibrary.CreateQuad("Aviso de ataque - sin colision", transform, true, VfxLibrary.GroundAdditive);
            disc.enabled = false;
        }

        /// <summary>
        /// Inicia el aviso. Su centro sigue al enemigo, pero este debe permanecer
        /// quieto mientras carga para que el jugador pueda reaccionar.
        /// </summary>
        public void Begin(float reach, float seconds, bool boss, bool magic = false)
        {
            if (disc == null)
                return;
            radius = float.IsNaN(reach) || float.IsInfinity(reach)
                ? 0.5f : Mathf.Max(0.5f, reach);
            bossWarning = boss;
            magicWarning = magic;
            warningActive = true;
            warningStarted = Time.time;
            warningEnds = Time.time + Mathf.Max(0.1f, seconds);
            flashEnds = 0f;
            Fill = 0f;

            float foot = controller != null
                ? controller.center.y - controller.height * 0.5f + 0.06f
                : -0.95f;
            disc.transform.localPosition = new Vector3(0f, foot, 0f);
            disc.transform.localRotation = Quaternion.identity;
            disc.transform.localScale = new Vector3(radius, 1f, radius);
            disc.enabled = true;
            Refresh();
        }

        public void Strike()
        {
            if (disc == null)
                return;
            warningActive = false;
            Fill = 1f;
            flashEnds = Time.time + FlashDuration;
            disc.enabled = true;
            Refresh();
        }

        public void Cancel()
        {
            warningActive = false;
            flashEnds = 0f;
            Fill = 0f;
            if (disc != null)
                disc.enabled = false;
        }

        private void Update()
        {
            if (disc == null || !disc.enabled)
                return;
            if (!warningActive && Time.time >= flashEnds)
            {
                disc.enabled = false;
                return;
            }
            Refresh();
        }

        private Color StartColor => magicWarning
            ? new Color(0.14f, 0.58f, 0.95f, 1f)
            : bossWarning
                ? new Color(0.6f, 0.22f, 0.8f, 1f)
                : new Color(0.98f, 0.52f, 0.15f, 1f);

        private Color EndColor => magicWarning
            ? new Color(0.40f, 0.95f, 1f, 1f)
            : bossWarning
                ? new Color(1f, 0.3f, 0.7f, 1f)
                : new Color(1f, 0.15f, 0.12f, 1f);

        private void Refresh()
        {
            // El grosor del borde se fija en metros (ringWidth) y se pasa al disco relativo al radio.
            float width = (bossWarning ? ringWidth * 1.7f : ringWidth) / radius;
            VfxDisc style = new VfxDisc
            {
                Soft = 0.02f,
                RimWidth = Mathf.Clamp(width, 0.02f, 0.25f),
                EdgeWidth = Mathf.Clamp(0.3f / radius, 0.05f, 0.4f)
            };

            if (!warningActive)
            {
                // Golpe: el disco entero destella en rojo y se apaga enseguida.
                float t = 1f - Mathf.Clamp01((flashEnds - Time.time) / FlashDuration);
                style.Color = VfxLibrary.WithAlpha(new Color(1f, 0.2f, 0.15f), 1f - t * t);
                style.Base = 0.3f;
                style.RimAlpha = 1f;
                style.Fill = 1f;
                style.FillAlpha = 0.55f;
                style.EdgeAlpha = 0f;
                style.Apply(disc, block);
                return;
            }

            float duration = Mathf.Max(0.1f, warningEnds - warningStarted);
            Fill = Mathf.Clamp01((Time.time - warningStarted) / duration);
            // El borde late cada vez mas rapido a medida que se acerca el golpe.
            float pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin((Time.time - warningStarted) * Mathf.Lerp(7f, 20f, Fill)));
            style.Color = Color.Lerp(StartColor, EndColor, Fill);
            style.Base = 0.1f + 0.1f * Fill;
            style.RimAlpha = Mathf.Lerp(0.6f, 1f, Fill) * pulse;
            style.Fill = Mathf.Max(0.002f, Fill);
            style.FillAlpha = 0.3f + 0.25f * Fill;
            style.EdgeAlpha = 1f;
            style.Apply(disc, block);
        }

        private void OnDisable() => Cancel();
    }
}
