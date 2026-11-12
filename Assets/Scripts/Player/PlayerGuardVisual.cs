using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Avisos de defensa que funcionan incluso con un Animator antiguo.
    /// Escudo: arco frontal ambar. Barrera: anillo magico completo.
    /// Parry: destello blanco. VFX sin colision, daño ni asignaciones por frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatDefense))]
    public sealed class PlayerGuardVisual : MonoBehaviour
    {
        private const int Samples = 40;
        private CombatDefense defense;
        private GameObject visualObject;
        private LineRenderer line;
        private Material material;
        private CombatDefense.GuardMode lastMode = CombatDefense.GuardMode.None;
        private float flashUntil;

        public bool IsVisible => line != null && line.enabled;

        private void Awake()
        {
            defense = GetComponent<CombatDefense>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return;

            visualObject = new GameObject("Feedback guardia — sin colision");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            line = visualObject.AddComponent<LineRenderer>();
            material = new Material(shader) { name = "Guardia temporal Vhaldren" };
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = Samples;
            line.loop = false;
            line.widthMultiplier = 0.10f;
            line.numCornerVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
        }

        public void FlashParry()
        {
            flashUntil = Time.time + 0.24f;
            lastMode = (CombatDefense.GuardMode)(-1);
        }

        private void LateUpdate()
        {
            if (line == null || defense == null)
                return;

            bool flashing = Time.time < flashUntil;
            CombatDefense.GuardMode mode = defense.Guard;
            if (mode == CombatDefense.GuardMode.None && !flashing)
            {
                line.enabled = false;
                lastMode = mode;
                return;
            }

            CombatDefense.GuardMode display = mode == CombatDefense.GuardMode.None
                ? CombatDefense.GuardMode.PhysicalShield : mode;
            if (lastMode != display)
            {
                lastMode = display;
                bool ward = display == CombatDefense.GuardMode.ArcaneWard;
                float radius = ward ? 1.40f : 1.02f;
                float angleSpan = ward ? 360f : 145f;
                line.loop = ward;
                line.widthMultiplier = ward ? 0.13f : 0.16f;
                visualObject.transform.localPosition = ward
                    ? new Vector3(0f, -0.35f, 0f)
                    : new Vector3(0f, 0.12f, 0f);
                for (int i = 0; i < Samples; i++)
                {
                    float t = ward ? (float)i / Samples :
                        (float)i / (Samples - 1);
                    float degrees = ward ? t * angleSpan :
                        (t - 0.5f) * angleSpan;
                    float radians = degrees * Mathf.Deg2Rad;
                    line.SetPosition(i, new Vector3(
                        Mathf.Sin(radians) * radius, 0f,
                        Mathf.Cos(radians) * radius));
                }
            }

            Color tint = flashing
                ? new Color(1f, 1f, 0.78f, 1f)
                : mode == CombatDefense.GuardMode.ArcaneWard
                    ? new Color(0.32f, 0.72f, 1f, 0.90f)
                    : new Color(1f, 0.62f, 0.20f, 0.92f);
            line.startColor = tint;
            line.endColor = tint;
            line.enabled = true;
        }

        private void OnDisable()
        {
            if (line != null)
                line.enabled = false;
            lastMode = CombatDefense.GuardMode.None;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }
        }
    }
}
