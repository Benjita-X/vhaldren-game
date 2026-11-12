using UnityEngine;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Aviso visual de ataques cuerpo a cuerpo. No aplica dano ni decide impactos.
    /// LineRenderer persistente con radio exacto: la IA resuelve el golpe tras la carga.
    /// No necesita regenerar WorldPrototype001 ni anadir assets externos.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttackTelegraph : MonoBehaviour
    {
        private const int Segments = 60;
        private const float FlashDuration = 0.13f;

        [SerializeField, Range(0.03f, 0.18f)] private float ringWidth = 0.10f;

        private CharacterController controller;
        private LineRenderer ring;
        private Material ringMaterial;
        private bool warningActive;
        private bool bossWarning;
        private bool magicWarning;
        private float warningStarted;
        private float warningEnds;
        private float flashEnds;

        public bool WarningActive => warningActive;
        public bool IsVisible => ring != null && ring.enabled;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            GameObject visual = new GameObject("Aviso de ataque - sin colision");
            visual.transform.SetParent(transform, false);
            ring = visual.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = Segments;
            ring.widthMultiplier = ringWidth;
            ring.numCornerVertices = 2;
            ring.numCapVertices = 2;
            ring.receiveShadows = false;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                ringMaterial = new Material(shader);
                ringMaterial.name = "Aviso de ataque runtime";
                ring.sharedMaterial = ringMaterial;
            }

            ring.enabled = false;
        }

        /// <summary>
        /// Inicia un anillo telegrafiado. Su centro sigue al enemigo, pero
        /// este debe permanecer quieto mientras carga para que el jugador pueda reaccionar.
        /// </summary>
        public void Begin(float reach, float seconds, bool boss, bool magic = false)
        {
            if (ring == null)
                return;
            float radius = float.IsNaN(reach) || float.IsInfinity(reach)
                ? 0.5f : Mathf.Max(0.5f, reach);
            bossWarning = boss;
            magicWarning = magic;
            warningActive = true;
            warningStarted = Time.time;
            warningEnds = Time.time + Mathf.Max(0.1f, seconds);
            flashEnds = 0f;

            float foot = controller != null
                ? controller.center.y - controller.height * 0.5f + 0.10f
                : -0.95f;
            ring.transform.localPosition = new Vector3(0f, foot, 0f);
            ring.widthMultiplier = boss ? ringWidth * 1.7f : ringWidth;
            for (int i = 0; i < Segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / Segments;
                ring.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            ring.startColor = ring.endColor = magic
                ? new Color(0.23f, 0.73f, 1f, 0.93f)
                : boss
                    ? new Color(0.85f, 0.30f, 0.85f, 0.85f)
                    : new Color(1f, 0.48f, 0.14f, 0.85f);
            ring.enabled = true;
        }

        public void Strike()
        {
            if (ring == null)
                return;
            warningActive = false;
            flashEnds = Time.time + FlashDuration;
            ring.startColor = ring.endColor = new Color(1f, 0.20f, 0.15f, 1f);
            ring.enabled = true;
        }

        public void Cancel()
        {
            warningActive = false;
            flashEnds = 0f;
            if (ring != null)
                ring.enabled = false;
        }

        private void Update()
        {
            if (ring == null || !ring.enabled)
                return;

            if (!warningActive)
            {
                if (Time.time >= flashEnds)
                    ring.enabled = false;
                return;
            }

            float duration = Mathf.Max(0.1f, warningEnds - warningStarted);
            float progress = Mathf.Clamp01((Time.time - warningStarted) / duration);
            float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 13f));
            Color start = magicWarning
                ? new Color(0.14f, 0.58f, 0.95f, 1f)
                : bossWarning
                    ? new Color(0.6f, 0.22f, 0.8f, 1f)
                    : new Color(0.98f, 0.52f, 0.15f, 1f);
            Color end = magicWarning
                ? new Color(0.40f, 0.95f, 1f, 1f)
                : bossWarning
                    ? new Color(1f, 0.3f, 0.7f, 1f)
                    : new Color(1f, 0.15f, 0.12f, 1f);
            Color current = Color.Lerp(start, end, progress);
            current.a = Mathf.Lerp(0.4f, 1f, progress) * pulse;
            ring.startColor = current;
            ring.endColor = current;
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            if (ringMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(ringMaterial);
                else
                    DestroyImmediate(ringMaterial);
            }
        }
    }
}
