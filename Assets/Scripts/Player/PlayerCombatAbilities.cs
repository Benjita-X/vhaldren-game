using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Dos habilidades originales de prueba sin bloquear la locomocion:
    /// V tajo de espada frontal (fisico), T pulso runico (magico de area).
    /// El daño siempre pasa por CombatDamageReceiver y solo se registra al impactar.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth), typeof(CombatDefense))]
    public sealed class PlayerCombatAbilities : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float swordDamage = 35f;
        [SerializeField, Min(0.5f)] private float swordRange = 2.8f;
        [SerializeField, Min(0.1f)] private float swordCooldown = 2.4f;
        [SerializeField, Min(0f)] private float swordStaminaCost = 18f;

        [SerializeField, Min(0f)] private float runeDamage = 22f;
        [SerializeField, Min(0.5f)] private float runeRadius = 3.5f;
        [SerializeField, Min(0.1f)] private float runeCooldown = 7f;
        [SerializeField, Min(0f)] private float runeStaminaCost = 26f;
        [SerializeField, Min(0.1f)] private float runeWindup = 0.55f;

        private readonly Collider[] candidates = new Collider[48];
        private readonly HashSet<CombatDamageReceiver> applied = new HashSet<CombatDamageReceiver>();
        private CombatHealth health;
        private CombatDefense defense;
        private PlayerMotor motor;
        private float swordReadyAt;
        private float runeReadyAt;
        private float nextLabel;
        private string labelText = "";
        private static Material effectMaterial;

        public int SwordTargetsHit { get; private set; }
        public int RuneTargetsHit { get; private set; }
        public float SwordCooldownRemaining => Mathf.Max(0f, swordReadyAt - Time.time);
        public float RuneCooldownRemaining => Mathf.Max(0f, runeReadyAt - Time.time);

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            defense = GetComponent<CombatDefense>();
            motor = GetComponent<PlayerMotor>();
        }

        private void Update()
        {
            if (health != null && !health.IsDefeated)
            {
                if (SwordPressed())
                    TrySwordSweep();
                if (RunePressed())
                    TryRunePulse();
            }

            if (Time.time >= nextLabel)
            {
                nextLabel = Time.time + 0.18f;
                labelText = "V TAJO: " +
                    (SwordCooldownRemaining <= 0f ? "LISTO" :
                        SwordCooldownRemaining.ToString("0.0") + "s") +
                    "   |   T PULSO RÚNICO: " +
                    (RuneCooldownRemaining <= 0f ? "LISTO" :
                        RuneCooldownRemaining.ToString("0.0") + "s");
            }
        }

        public bool TrySwordSweep()
        {
            if (health == null || health.IsDefeated || Time.time < swordReadyAt ||
                defense == null || !defense.TrySpend(swordStaminaCost))
                return false;

            swordReadyAt = Time.time + Mathf.Max(0.1f, swordCooldown);
            StartCoroutine(SwordSequence());
            return true;
        }

        public bool TryRunePulse()
        {
            if (health == null || health.IsDefeated || Time.time < runeReadyAt ||
                defense == null || !defense.TrySpend(runeStaminaCost))
                return false;

            runeReadyAt = Time.time + Mathf.Max(0.1f, runeCooldown);
            Vector3 center = transform.position;
            StartCoroutine(RuneSequence(center));
            return true;
        }

        private IEnumerator SwordSequence()
        {
            Vector3 forward = motor != null ? motor.CurrentFacing : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            MakeArc(transform.position, forward, swordRange,
                new Color(1f, 0.68f, 0.20f), 0.34f);
            yield return new WaitForSeconds(0.12f);

            if (health == null || health.IsDefeated)
                yield break;
            Vector3 origin = transform.position;
            int count = QueryEnemies(origin, swordRange);
            for (int i = 0; i < count; i++)
            {
                Collider body = candidates[i];
                if (body == null)
                    continue;
                CombatDamageReceiver receiver =
                    body.GetComponentInParent<CombatDamageReceiver>();
                if (receiver == null || !applied.Add(receiver) ||
                    receiver.transform.IsChildOf(transform))
                    continue;
                Vector3 delta = receiver.transform.position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude <= 0.0001f ||
                    Vector3.Dot(forward, delta.normalized) < 0.48f ||
                    !Unobstructed(receiver, origin + Vector3.up * 0.55f))
                    continue;

                CombatDamageResult result =
                    receiver.Receive(swordDamage, CombatDamageKind.Melee, origin);
                if (result.Applied > 0f)
                {
                    SwordTargetsHit++;
                    PrototypeEnemy enemy = receiver.GetComponent<PrototypeEnemy>();
                    if (enemy != null)
                        enemy.InterruptAttack(0.24f);
                }
            }
        }

        private IEnumerator RuneSequence(Vector3 center)
        {
            MakeArc(center, Vector3.forward, runeRadius,
                new Color(0.15f, 0.70f, 0.97f), runeWindup + 0.3f, true);
            yield return new WaitForSeconds(Mathf.Max(0.1f, runeWindup));

            if (health == null || health.IsDefeated)
                yield break;
            int count = QueryEnemies(center, runeRadius);
            for (int i = 0; i < count; i++)
            {
                Collider body = candidates[i];
                if (body == null)
                    continue;
                CombatDamageReceiver receiver =
                    body.GetComponentInParent<CombatDamageReceiver>();
                if (receiver == null || !applied.Add(receiver) ||
                    receiver.transform.IsChildOf(transform))
                    continue;
                if (!Unobstructed(receiver, center + Vector3.up * 0.55f))
                    continue;
                CombatDamageResult result = receiver.Receive(
                    runeDamage, CombatDamageKind.Magic, center);
                if (result.Applied > 0f)
                {
                    RuneTargetsHit++;
                    PrototypeEnemy enemy = receiver.GetComponent<PrototypeEnemy>();
                    if (enemy != null)
                        enemy.InterruptAttack(0.45f);
                }
            }
        }

        private int QueryEnemies(Vector3 center, float range)
        {
            applied.Clear();
            int count = Physics.OverlapSphereNonAlloc(
                center, range, candidates, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            // No ignorar objetivos si hay muchos colliders de escenario.
            if (count >= candidates.Length)
            {
                Collider[] all = Physics.OverlapSphere(
                    center, range, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                // El mundo actual solo tiene seis enemigos, pero se evita
                // iterar fuera del array o inventar impactos.
                count = Mathf.Min(candidates.Length, all.Length);
                System.Array.Copy(all, candidates, count);
            }
            return count;
        }

        private bool Unobstructed(CombatDamageReceiver target, Vector3 start)
        {
            Collider targetCollider = target.GetComponent<Collider>();
            Vector3 end = targetCollider != null
                ? targetCollider.bounds.center
                : target.transform.position + Vector3.up * 0.6f;
            Vector3 ray = end - start;
            float range = ray.magnitude;
            if (range < 0.01f)
                return true;
            RaycastHit[] hits = Physics.RaycastAll(start, ray / range, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform obstacle = hits[i].collider.transform;
                if (obstacle.IsChildOf(transform) || obstacle.IsChildOf(target.transform))
                    continue;
                return false;
            }
            return true;
        }

        private static void MakeArc(Vector3 position, Vector3 facing, float radius,
            Color tint, float lifetime, bool wholeCircle = false)
        {
            if (effectMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    return;
                effectMaterial = new Material(shader);
            }
            GameObject visual = new GameObject(wholeCircle
                ? "Aviso pulso runico — sin colision"
                : "Arco espada — sin colision");
            visual.transform.position = new Vector3(position.x, 0.10f, position.z);
            LineRenderer line = visual.AddComponent<LineRenderer>();
            line.sharedMaterial = effectMaterial;
            line.useWorldSpace = false;
            line.loop = wholeCircle;
            const int segments = 44;
            line.positionCount = segments;
            line.widthMultiplier = wholeCircle ? 0.10f : 0.17f;
            line.startColor = line.endColor = tint;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float forwardAngle = Mathf.Atan2(facing.x, facing.z);
            for (int i = 0; i < segments; i++)
            {
                float ratio = wholeCircle ? (float)i / segments :
                    (float)i / (segments - 1);
                float angle = wholeCircle
                    ? ratio * Mathf.PI * 2f
                    : forwardAngle + (ratio - 0.5f) * Mathf.PI * 2f / 3f;
                line.SetPosition(i, new Vector3(
                    Mathf.Sin(angle) * radius, 0f,
                    Mathf.Cos(angle) * radius));
            }
            Destroy(visual, Mathf.Max(0.1f, lifetime));
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Rect box = new Rect(12f, 166f, Mathf.Min(510f, Screen.width - 24f), 29f);
            GUI.Box(box, "");
            GUI.Label(new Rect(box.x + 8f, box.y + 4f, box.width - 16f, 21f), labelText);
        }

        private void OnDisable() => StopAllCoroutines();

        private static bool SwordPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.V);
#else
            return false;
#endif
        }

        private static bool RunePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.T);
#else
            return false;
#endif
        }
    }
}
