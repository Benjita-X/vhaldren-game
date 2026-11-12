using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Settings;
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

        [Header("F — Onda de escarcha")]
        [SerializeField, Min(0f)] private float frostDamage = 14f;
        [SerializeField, Min(0.5f)] private float frostRange = 6f;
        [SerializeField, Range(10f, 170f)] private float frostConeDegrees = 75f;
        [SerializeField, Min(0.1f)] private float frostCooldown = 6f;
        [SerializeField, Min(0f)] private float frostStaminaCost = 22f;
        [SerializeField, Min(0.1f)] private float frostSlowSeconds = 1.75f;
        [SerializeField, Range(0.25f, 1f)] private float frostSpeedMultiplier = 0.55f;

        private Collider[] candidates = new Collider[48];
        private readonly HashSet<CombatDamageReceiver> applied = new HashSet<CombatDamageReceiver>();
        private CombatHealth health;
        private CombatDefense defense;
        private PlayerMotor motor;
        private PlayerDamageTracker damageTracker;
        private PlayerActionModifiers actionModifiers;
        private PlayerAnimationDriver animations;
        private LocalPlayerInput playerInput;
        private string lastActionMessage = "";
        private float lastActionMessageUntil;
        public string LastActionMessage => lastActionMessage;
        private float swordReadyAt;
        private float runeReadyAt;
        private float frostReadyAt;
        private float nextLabel;
        private string labelText = "";
        private static Material effectMaterial;

        public int SwordTargetsHit { get; private set; }
        public int CounterStrikes { get; private set; }
        public int RuneTargetsHit { get; private set; }
        public int FrostTargetsHit { get; private set; }
        public float FrostCooldownRemaining => Mathf.Max(0f, frostReadyAt - Time.time);
        public float SwordCooldownRemaining => Mathf.Max(0f, swordReadyAt - Time.time);
        public float RuneCooldownRemaining => Mathf.Max(0f, runeReadyAt - Time.time);

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            defense = GetComponent<CombatDefense>();
            motor = GetComponent<PlayerMotor>();
            damageTracker = GetComponent<PlayerDamageTracker>();
            actionModifiers = GetComponent<PlayerActionModifiers>();
            animations = GetComponent<PlayerAnimationDriver>();
        }

        private void Update()
        {
            if (health != null && !health.IsDefeated)
            {
                if (SwordPressed())
                    TrySwordSweep();
                if (RunePressed())
                    TryRunePulse();
                if (FrostPressed())
                    TryFrostWave();
            }

            if (Time.time >= nextLabel)
            {
                nextLabel = Time.time + 0.18f;
                labelText = "V TAJO: " +
                    (SwordCooldownRemaining <= 0f ? "LISTO" :
                        SwordCooldownRemaining.ToString("0.0") + "s") +
                    "   |   T PULSO RÚNICO: " +
                    (RuneCooldownRemaining <= 0f ? "LISTO" :
                        RuneCooldownRemaining.ToString("0.0") + "s") +
                    "   |   F ESCARCHA: " +
                    (FrostCooldownRemaining <= 0f ? "LISTA" :
                        FrostCooldownRemaining.ToString("0.0") + "s");
            }
        }

        public bool TrySwordSweep()
        {
            if (!CanStartAbility("Tajo de espada", swordReadyAt, swordStaminaCost))
                return false;

            FaceCursorForAction();
            animations?.PlayCombatAction(PlayerAnimationDriver.CombatAction.Sword);
            if (GameFeel.Enabled)
                GameAudio.Play(GameSound.Slash, transform.position, 0.8f);
            Announce("Tajo de espada");
            swordReadyAt = Time.time + Mathf.Max(0.1f, swordCooldown);
            bool counter = defense.TryConsumeCounter();
            if (counter)
                CounterStrikes++;
            StartCoroutine(SwordSequence(counter));
            return true;
        }

        public bool TryRunePulse()
        {
            if (!CanStartAbility("Pulso rúnico", runeReadyAt, runeStaminaCost))
                return false;

            FaceCursorForAction();
            animations?.PlayCombatAction(PlayerAnimationDriver.CombatAction.Spell);
            if (GameFeel.Enabled)
                GameAudio.Play(GameSound.SpellCast, transform.position, 0.8f);
            Announce("Pulso rúnico: canalizando");
            GetComponent<VillageQuestJournal>()?.RecordAction(VillageObjective.CastMagic);
            runeReadyAt = Time.time + Mathf.Max(0.1f, runeCooldown);
            Vector3 center = transform.position;
            StartCoroutine(RuneSequence(center));
            return true;
        }

        public bool TryFrostWave()
        {
            if (!CanStartAbility("Escarcha", frostReadyAt, frostStaminaCost))
                return false;

            FaceCursorForAction();
            animations?.PlayCombatAction(PlayerAnimationDriver.CombatAction.Frost);
            if (GameFeel.Enabled)
                GameAudio.Play(GameSound.Frost, transform.position, 0.8f);
            Announce("Onda de escarcha: canalizando");
            GetComponent<VillageQuestJournal>()?.RecordAction(VillageObjective.CastMagic);
            frostReadyAt = Time.time + Mathf.Max(0.1f, frostCooldown);
            Vector3 facing = motor != null ? motor.CurrentFacing : transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f)
                facing = Vector3.forward;
            StartCoroutine(FrostSequence(facing.normalized));
            return true;
        }

        private bool CanStartAbility(string name, float readyAt, float staminaCost)
        {
            if (health == null || health.IsDefeated)
            {
                Announce(name + ": personaje derrotado");
                return false;
            }

            if (actionModifiers == null)
                actionModifiers = GetComponent<PlayerActionModifiers>();
            if (actionModifiers != null && !actionModifiers.AllowsAttack)
            {
                Announce(name + ": cierra construcción (B) o diálogo (N)");
                return false;
            }

            if (Time.time < readyAt)
            {
                Announce(name + ": recarga " + (readyAt - Time.time).ToString("0.0") + " s");
                return false;
            }

            if (defense == null)
                defense = GetComponent<CombatDefense>();
            if (defense == null || !defense.TrySpend(staminaCost))
            {
                float have = defense != null ? defense.Stamina : 0f;
                Announce(name + ": resistencia " + have.ToString("0") + "/" +
                    staminaCost.ToString("0"));
                return false;
            }
            return true;
        }

        private void Announce(string message)
        {
            lastActionMessage = message;
            lastActionMessageUntil = Time.time + 1.6f;
        }

        /// <summary>
        /// Explorando, el personaje mira hacia donde camina: antes de leer la orientacion,
        /// el ataque la dirige al cursor y activa el modo combate (si hay jugador local).
        /// </summary>
        private void FaceCursorForAction()
        {
            if (playerInput == null)
                playerInput = GetComponent<LocalPlayerInput>();
            if (playerInput != null && playerInput.isActiveAndEnabled)
                playerInput.NotifyCombatAction();
        }

        private IEnumerator FrostSequence(Vector3 direction)
        {
            Vector3 point = transform.position;
            MakeArc(point, direction, frostRange,
                new Color(0.35f, 0.92f, 1f), 0.50f, false, frostConeDegrees);
            yield return new WaitForSeconds(0.25f);

            if (health == null || health.IsDefeated)
                yield break;
            Vector3 origin = transform.position;
            SpellImpactFlash.Spawn(
                new Vector3(origin.x, 0.36f, origin.z) + direction * (frostRange * 0.52f),
                new Color(0.31f, 0.80f, 1f), 0.9f, 0.37f);
            int count = QueryEnemies(origin, frostRange);
            float halfConeCos = Mathf.Cos(Mathf.Clamp(frostConeDegrees, 10f, 170f) *
                Mathf.Deg2Rad * 0.5f);
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
                if (delta.sqrMagnitude < 0.001f ||
                    delta.sqrMagnitude > frostRange * frostRange ||
                    Vector3.Dot(direction, delta.normalized) < halfConeCos ||
                    !Unobstructed(receiver, origin + Vector3.up * 0.55f))
                    continue;

                CombatDamageResult result = receiver.Receive(
                    frostDamage, CombatDamageKind.Magic, origin);
                if (result.Applied <= 0f)
                    continue;

                FrostTargetsHit++;
                SpellImpactFlash.Spawn(receiver.transform.position + Vector3.up * 0.85f,
                    new Color(0.44f, 0.90f, 1f), 0.55f, 0.22f);
                if (damageTracker == null)
                    damageTracker = GetComponent<PlayerDamageTracker>();
                if (damageTracker != null)
                    damageTracker.Record(result.Applied, CombatDamageKind.Magic);
                PrototypeEnemy enemy = receiver.GetComponent<PrototypeEnemy>();
                if (enemy != null)
                    enemy.ApplySlow(frostSpeedMultiplier, frostSlowSeconds);
            }
        }

        private IEnumerator SwordSequence(bool counter)
        {
            Vector3 forward = motor != null ? motor.CurrentFacing : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            MakeArc(transform.position, forward, swordRange,
                counter ? new Color(1f, 0.25f, 0.27f) :
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
                    receiver.Receive(swordDamage * (counter ? 1.5f : 1f),
                        CombatDamageKind.Melee, origin);
                if (result.Applied > 0f)
                {
                    if (damageTracker == null)
                        damageTracker = GetComponent<PlayerDamageTracker>();
                    if (damageTracker != null)
                        damageTracker.Record(result.Applied, CombatDamageKind.Melee);
                    SwordTargetsHit++;
                    SpellImpactFlash.Spawn(receiver.transform.position + Vector3.up * 0.85f,
                        new Color(1f, 0.65f, 0.22f), 0.46f, 0.20f);
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
            SpellImpactFlash.Spawn(new Vector3(center.x, 0.20f, center.z),
                new Color(0.20f, 0.77f, 1f), 1.1f, 0.42f, runeRadius);
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
                    if (damageTracker == null)
                        damageTracker = GetComponent<PlayerDamageTracker>();
                    if (damageTracker != null)
                        damageTracker.Record(result.Applied, CombatDamageKind.Magic);
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
                // No perder enemigos al saturarse con arboles o pilares;
                // ampliar el buffer reutilizable para siguientes ataques.
                if (all.Length >= candidates.Length)
                    System.Array.Resize(ref candidates,
                        Mathf.NextPowerOfTwo(all.Length + 1));
                System.Array.Copy(all, candidates, all.Length);
                count = all.Length;
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
            Color tint, float lifetime, bool wholeCircle = false,
            float arcDegrees = 120f)
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
                    : forwardAngle + (ratio - 0.5f) *
                        Mathf.Clamp(arcDegrees, 10f, 360f) * Mathf.Deg2Rad;
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
            Rect box = new Rect(12f, 184f, Mathf.Min(620f, Screen.width - 24f), 53f);
            GUI.Box(box, "");
            GUI.Label(new Rect(box.x + 8f, box.y + 4f, box.width - 16f, 21f), labelText);
            if (Time.time < lastActionMessageUntil)
                GUI.Label(new Rect(box.x + 8f, box.y + 26f, box.width - 16f, 22f),
                    lastActionMessage);
        }

        private void OnDisable() => StopAllCoroutines();

        private static bool SwordPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.Sword);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.V);
#else
            return false;
#endif
        }

        private static bool FrostPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.Frost);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F);
#else
            return false;
#endif
        }

        private static bool RunePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return GameInput.Down(GameAction.RunePulse);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.T);
#else
            return false;
#endif
        }
    }
}
