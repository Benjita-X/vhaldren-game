using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Grimorio del jugador. 1/2/3 lanzan los hechizos de la escuela activa, Tab cambia
    /// de escuela (no en combate). Los hechizos basicos tienen dos cargas; el resto,
    /// recarga normal. Todo el daño pasa por <see cref="CombatDamageReceiver"/> y solo
    /// se registra lo realmente aplicado.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth), typeof(CombatDefense))]
    public sealed class PlayerSpellbook : MonoBehaviour
    {
        public const float CriticalMultiplier = 1.6f;
        public const float BlinkRecastSeconds = 3f;
        public const float BarrierSeconds = 4f;

        [SerializeField] private Camera viewCamera;

        private readonly bool[] unlocked = new bool[SpellCatalog.SchoolCount];
        private readonly int[] charges = new int[SpellCatalog.SchoolCount * SpellCatalog.SpellsPerSchool];
        private readonly float[] rechargeAt = new float[SpellCatalog.SchoolCount * SpellCatalog.SpellsPerSchool];
        private readonly HashSet<CombatDamageReceiver> struck = new HashSet<CombatDamageReceiver>();
        private readonly RaycastHit[] rayHits = new RaycastHit[16];
        private readonly List<LabelEntry> labels = new List<LabelEntry>();
        private readonly HashSet<GameObject> labelled = new HashSet<GameObject>();
        private readonly StringBuilder text = new StringBuilder(256);
        private Collider[] candidates = new Collider[48];

        private CombatHealth health;
        private CombatDefense defense;
        private CharacterController body;
        private PlayerMotor motor;
        private PlayerDamageTracker tracker;
        private PlayerActionModifiers modifiers;
        private PlayerAnimationDriver animations;
        private EssenceReservoir essence;
        private CombatState combat;
        private PlayerFeeding feeding;
        private Vhaldren.Gameplay.Progression.PlayerEquipment equipment;
        private System.Random random = new System.Random(20261112);

        private SpellSchool school = SpellSchool.Pyromancy;
        private float castingUntil;
        private Vector3 blinkReturnPoint;
        private float blinkRecastUntil;
        private string message = string.Empty;
        private float messageUntil;
        private float nextLabelRefresh;
        private GUIStyle style;
        private GUIStyle centered;

        private struct LabelEntry
        {
            public Transform Target;
            public string Text;
        }

        public event Action<SpellSchool> SchoolUnlocked;
        public SpellSchool School => school;
        public int SpellsCast { get; private set; }
        public int TargetsHit { get; private set; }
        public int CriticalHits { get; private set; }
        public string LastMessage => message;
        public bool IsCasting => Time.time < castingUntil;

        /// <summary>Poder de hechizo actual: base 1 por la bonificacion de esencia.</summary>
        public float SpellPower =>
            (essence != null ? essence.SpellPowerMultiplier : 1f) *
            (equipment != null ? equipment.SpellPowerMultiplier : 1f);

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            defense = GetComponent<CombatDefense>();
            body = GetComponent<CharacterController>();
            motor = GetComponent<PlayerMotor>();
            tracker = GetComponent<PlayerDamageTracker>();
            modifiers = GetComponent<PlayerActionModifiers>();
            animations = GetComponent<PlayerAnimationDriver>();
            essence = GetComponent<EssenceReservoir>();
            if (essence == null)
                essence = gameObject.AddComponent<EssenceReservoir>();
            equipment = GetComponent<Vhaldren.Gameplay.Progression.PlayerEquipment>();
            combat = GetComponent<CombatState>();
            if (combat == null)
                combat = gameObject.AddComponent<CombatState>();

            // Tres escuelas desde el principio; las otras se aprenden absorbiendo esencias.
            unlocked[(int)SpellSchool.Pyromancy] = true;
            unlocked[(int)SpellSchool.Cryomancy] = true;
            unlocked[(int)SpellSchool.Fulmancy] = true;
            for (int i = 0; i < charges.Length; i++)
            {
                charges[i] = SpellCatalog.All[i].Charges;
                rechargeAt[i] = float.PositiveInfinity;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            castingUntil = 0f;
        }

        // ── Escuelas ─────────────────────────────────────────────────────

        public bool IsUnlocked(SpellSchool value) => unlocked[(int)value];

        public int UnlockedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < unlocked.Length; i++)
                    if (unlocked[i])
                        count++;
                return count;
            }
        }

        /// <summary>Aprende una escuela. Devuelve true solo la primera vez.</summary>
        public bool Unlock(SpellSchool value)
        {
            if (unlocked[(int)value])
                return false;
            unlocked[(int)value] = true;
            SchoolUnlocked?.Invoke(value);
            Announce("Nueva escuela aprendida: " + SpellCatalog.SchoolName(value) + " (Tab)");
            return true;
        }

        public bool SelectSchool(SpellSchool value)
        {
            if (!unlocked[(int)value])
            {
                Announce(SpellCatalog.SchoolName(value) + ": aún no la has aprendido");
                return false;
            }
            if (value == school)
                return true;
            if (combat != null && combat.InCombat)
            {
                Announce("No puedes cambiar de escuela en combate (" + combat.SecondsLeft.ToString("0") + " s)");
                return false;
            }
            school = value;
            Announce("Escuela: " + SpellCatalog.SchoolName(value));
            return true;
        }

        public bool NextSchool()
        {
            for (int step = 1; step <= SpellCatalog.SchoolCount; step++)
            {
                SpellSchool candidate = (SpellSchool)(((int)school + step) % SpellCatalog.SchoolCount);
                if (unlocked[(int)candidate])
                    return SelectSchool(candidate);
            }
            return false;
        }

        // ── Cargas y recarga ─────────────────────────────────────────────

        private static int IndexOf(SpellDefinition spell) =>
            (int)spell.School * SpellCatalog.SpellsPerSchool + SlotOf(spell);

        private static int SlotOf(SpellDefinition spell)
        {
            for (int slot = 0; slot < SpellCatalog.SpellsPerSchool; slot++)
                if (SpellCatalog.Get(spell.School, slot) == spell)
                    return slot;
            return 0;
        }

        public int ChargesOf(SpellDefinition spell)
        {
            RefreshCharges();
            return charges[IndexOf(spell)];
        }

        public float CooldownRemaining(SpellDefinition spell)
        {
            RefreshCharges();
            int index = IndexOf(spell);
            return charges[index] > 0 ? 0f : Mathf.Max(0f, rechargeAt[index] - Time.time);
        }

        private float EffectiveCooldown(SpellDefinition spell) =>
            Mathf.Max(0.2f, spell.Cooldown * (essence != null ? essence.CooldownMultiplier : 1f));

        private void RefreshCharges()
        {
            float now = Time.time;
            for (int i = 0; i < charges.Length; i++)
            {
                int max = SpellCatalog.All[i].Charges;
                while (charges[i] < max && now >= rechargeAt[i])
                {
                    charges[i]++;
                    rechargeAt[i] = charges[i] < max
                        ? rechargeAt[i] + EffectiveCooldown(SpellCatalog.All[i])
                        : float.PositiveInfinity;
                }
            }
        }

        /// <summary>Vuelve a llenar todas las cargas (al reaparecer o en pruebas).</summary>
        public void ResetCooldowns()
        {
            for (int i = 0; i < charges.Length; i++)
            {
                charges[i] = SpellCatalog.All[i].Charges;
                rechargeAt[i] = float.PositiveInfinity;
            }
            castingUntil = 0f;
            blinkRecastUntil = 0f;
        }

        // ── Lanzamiento ──────────────────────────────────────────────────

        private void Update()
        {
            RefreshCharges();
            if (health == null || health.IsDefeated || Time.timeScale <= 0f)
                return;
            PlayerBuilder builder = GetComponent<PlayerBuilder>();
            if (builder != null && builder.IsBuilding)
                return;   // En construccion 1-0 eligen piezas.

            if (MagicKeys.NextSchool)
                NextSchool();
            int slot = MagicKeys.SpellSlotPressed();
            if (slot >= 0)
                TryCast(slot, AimPoint());
        }

        /// <summary>Lanza el hechizo del hueco indicado hacia un punto del mundo.</summary>
        public bool TryCast(int slot, Vector3 aimPoint)
        {
            SpellDefinition spell = SpellCatalog.Get(school, slot);
            if (spell == null)
                return false;
            string label = spell.DisplayName;

            if (health == null || health.IsDefeated)
                return Reject(label + ": estás derrotado");
            if (modifiers == null)
                modifiers = GetComponent<PlayerActionModifiers>();
            if (feeding == null)
                feeding = GetComponent<PlayerFeeding>();
            if (feeding != null && feeding.IsChanneling)
                return Reject(label + ": estás absorbiendo esencia");
            if (modifiers != null && !modifiers.AllowsAttack)
                return Reject(label + ": cierra construcción (B) o diálogo (N)");

            // Relanzar Paso sombrío: vuelves al punto de partida sin gastar carga.
            if (spell.Delivery == SpellDelivery.Blink && Time.time < blinkRecastUntil)
            {
                blinkRecastUntil = 0f;
                MoveTo(blinkReturnPoint);
                SpellImpactFlash.Spawn(transform.position + Vector3.up * 0.5f,
                    SpellCatalog.SchoolColor(spell.School), 0.8f, 0.3f, 1.2f);
                Announce(label + ": regreso");
                return true;
            }

            if (Time.time < castingUntil)
                return Reject(label + ": ya estás conjurando");
            RefreshCharges();
            int index = IndexOf(spell);
            if (charges[index] <= 0)
                return Reject(label + ": recarga " + Mathf.Max(0f, rechargeAt[index] - Time.time).ToString("0.0") + " s");
            if (defense == null)
                defense = GetComponent<CombatDefense>();
            if (defense == null || !defense.TrySpend(spell.StaminaCost))
                return Reject(label + ": resistencia " + (defense != null ? defense.Stamina : 0f).ToString("0") +
                              "/" + spell.StaminaCost.ToString("0"));

            if (charges[index] == spell.Charges)
                rechargeAt[index] = Time.time + EffectiveCooldown(spell);
            charges[index]--;
            SpellsCast++;
            castingUntil = Time.time + spell.Windup;
            GetComponent<VillageQuestJournal>()?.RecordAction(VillageObjective.CastMagic);
            animations?.PlayCombatAction(spell.School == SpellSchool.Cryomancy
                ? PlayerAnimationDriver.CombatAction.Frost
                : PlayerAnimationDriver.CombatAction.Spell);
            Announce(label);
            if (GameFeel.Enabled)
                GameAudio.Play(spell.School == SpellSchool.Cryomancy ? GameSound.Frost : GameSound.SpellCast,
                    transform.position, 0.75f);
            StartCoroutine(Execute(spell, SpellPower, aimPoint));
            return true;
        }

        private bool Reject(string reason)
        {
            Announce(reason);
            return false;
        }

        private IEnumerator Execute(SpellDefinition spell, float power, Vector3 aim)
        {
            Color tint = SpellCatalog.SchoolColor(spell.School);
            Vector3 origin = transform.position;
            Vector3 toAim = aim - origin;
            toAim.y = 0f;
            Vector3 direction = toAim.sqrMagnitude > 0.01f ? toAim.normalized : Facing();

            switch (spell.Delivery)
            {
                case SpellDelivery.Bolt:
                    if (spell.Windup > 0f)
                        yield return new WaitForSeconds(spell.Windup);
                    if (!Alive())
                        yield break;
                    SpellProjectile.Launch(this, spell, power, CastOrigin() + direction * 0.6f, direction);
                    break;

                case SpellDelivery.Nova:
                    Telegraph(transform, Vector3.zero, spell.Radius, tint, spell.Windup + 0.2f);
                    if (spell.Windup > 0f)
                        yield return new WaitForSeconds(spell.Windup);
                    if (!Alive())
                        yield break;
                    SpellImpactFlash.Spawn(Ground(transform.position), tint, 1.2f, 0.45f, spell.Radius);
                    AreaHit(transform.position, spell, power);
                    break;

                case SpellDelivery.Zone:
                {
                    Vector3 point = origin + Vector3.ClampMagnitude(toAim, spell.Range);
                    point.y = origin.y;
                    Telegraph(null, Ground(point), spell.Radius, tint, spell.Windup + 0.2f);
                    if (spell.Windup > 0f)
                        yield return new WaitForSeconds(spell.Windup);
                    if (!Alive())
                        yield break;
                    SpellImpactFlash.Spawn(Ground(point), tint, 1.4f, 0.5f, spell.Radius);
                    AreaHit(point, spell, power);
                    break;
                }

                case SpellDelivery.Chain:
                    if (spell.Windup > 0f)
                        yield return new WaitForSeconds(spell.Windup);
                    if (!Alive())
                        yield break;
                    ChainHit(direction, spell, power);
                    break;

                case SpellDelivery.Blink:
                {
                    blinkReturnPoint = transform.position;
                    float distance = Mathf.Min(spell.Range, toAim.magnitude > 0.5f ? toAim.magnitude : spell.Range);
                    SpellImpactFlash.Spawn(transform.position + Vector3.up * 0.5f, tint, 0.8f, 0.3f, 1.2f);
                    MoveTo(transform.position + direction * distance);
                    blinkRecastUntil = Time.time + BlinkRecastSeconds;
                    SpellImpactFlash.Spawn(Ground(transform.position), tint, 1.0f, 0.4f, spell.Radius);
                    AreaHit(transform.position, spell, power);
                    break;
                }

                case SpellDelivery.Barrier:
                {
                    ArcaneBarrier barrier = GetComponent<ArcaneBarrier>();
                    if (barrier == null)
                        barrier = gameObject.AddComponent<ArcaneBarrier>();
                    barrier.Raise(spell.Damage * power, BarrierSeconds, tint);
                    SpellImpactFlash.Spawn(transform.position + Vector3.up, tint, 0.9f, 0.35f, 1f);
                    break;
                }
            }

            if (spell.SelfHeal > 0f && Alive())
                health.Heal(spell.SelfHeal * power);
        }

        /// <summary>Aplica el golpe de un hechizo: daño, critico, estatica, estado y robo de vida.</summary>
        public float ApplySpellHit(CombatDamageReceiver receiver, SpellDefinition spell, float power, Vector3 origin)
        {
            if (receiver == null || spell == null || receiver.transform.IsChildOf(transform))
                return 0f;
            // Afinidad de la raza con la escuela (p. ej. vampiro +30 % Hemomancia).
            float damage = spell.Damage * Mathf.Max(0f, power) * Vhaldren.Gameplay.Races.PlayerRace.AffinityFor(spell.School);
            bool critical = essence != null && random.NextDouble() < essence.CriticalChance;
            if (critical)
                damage *= CriticalMultiplier;

            CombatDamageResult result = receiver.Receive(damage, CombatDamageKind.Magic, origin);
            if (result.Applied <= 0f)
                return 0f;

            StatusEffects status = StatusEffects.For(receiver);
            float total = result.Applied;
            bool hadBleed = false;
            if (status != null)
            {
                hadBleed = status.Has(StatusKind.Bleed);
                total += status.ConsumeStatic();
                for (int i = 0; i < spell.StatusStacks; i++)
                    status.Apply(spell.Status);
            }

            float steal = spell.LifeSteal + (essence != null ? essence.LifeSteal : 0f) +
                          (hadBleed ? StatusEffects.BleedLifeSteal : 0f);
            if (steal > 0f)
                health.Heal(total * steal);

            if (tracker == null)
                tracker = GetComponent<PlayerDamageTracker>();
            tracker?.Record(total, CombatDamageKind.Magic);
            combat?.Mark();
            TargetsHit++;
            if (critical)
            {
                CriticalHits++;
                GameFeel.Critical(receiver.transform.position + Vector3.up);
            }
            SpellImpactFlash.Spawn(receiver.transform.position + Vector3.up * 0.9f,
                SpellCatalog.SchoolColor(spell.School), critical ? 0.8f : 0.5f, 0.22f);
            return total;
        }

        private void AreaHit(Vector3 center, SpellDefinition spell, float power)
        {
            int count = Query(center, spell.Radius);
            for (int i = 0; i < count; i++)
            {
                CombatDamageReceiver receiver = candidates[i] != null
                    ? candidates[i].GetComponentInParent<CombatDamageReceiver>() : null;
                if (receiver == null || receiver.transform.IsChildOf(transform) || !struck.Add(receiver))
                    continue;
                if (!Unobstructed(receiver, center + Vector3.up * 0.55f))
                    continue;
                ApplySpellHit(receiver, spell, power, center);
            }
        }

        private void ChainHit(Vector3 direction, SpellDefinition spell, float power)
        {
            struck.Clear();
            Vector3 from = CastOrigin();
            Vector3 searchCenter = transform.position;
            float searchRadius = spell.Range;
            List<Vector3> points = new List<Vector3> { from };
            for (int jump = 0; jump < Mathf.Max(1, spell.ChainTargets); jump++)
            {
                int count = Physics.OverlapSphereNonAlloc(searchCenter, searchRadius, candidates,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                CombatDamageReceiver best = null;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    CombatDamageReceiver receiver = candidates[i] != null
                        ? candidates[i].GetComponentInParent<CombatDamageReceiver>() : null;
                    if (receiver == null || receiver.transform.IsChildOf(transform) || struck.Contains(receiver))
                        continue;
                    CombatHealth targetHealth = receiver.GetComponent<CombatHealth>();
                    if (targetHealth == null || targetHealth.IsDefeated)
                        continue;
                    Vector3 delta = receiver.transform.position - searchCenter;
                    delta.y = 0f;
                    // El primer salto busca enemigos hacia donde apuntas.
                    if (jump == 0 && delta.sqrMagnitude > 0.01f && Vector3.Dot(direction, delta.normalized) < 0.35f)
                        continue;
                    if (!Unobstructed(receiver, searchCenter + Vector3.up * 0.55f))
                        continue;
                    if (delta.sqrMagnitude < bestScore)
                    {
                        bestScore = delta.sqrMagnitude;
                        best = receiver;
                    }
                }
                if (best == null)
                    break;
                struck.Add(best);
                ApplySpellHit(best, spell, power, searchCenter);
                searchCenter = best.transform.position;
                searchRadius = spell.Radius;
                points.Add(best.transform.position + Vector3.up * 0.9f);
            }
            if (points.Count == 1)
                points.Add(from + direction * spell.Range);
            DrawBolt(points, SpellCatalog.SchoolColor(spell.School));
        }

        private int Query(Vector3 center, float radius)
        {
            struck.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius, candidates,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= candidates.Length)
            {
                Collider[] all = Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                if (all.Length >= candidates.Length)
                    Array.Resize(ref candidates, Mathf.NextPowerOfTwo(all.Length + 1));
                Array.Copy(all, candidates, all.Length);
                count = all.Length;
            }
            return count;
        }

        private bool Unobstructed(CombatDamageReceiver target, Vector3 start)
        {
            Collider targetCollider = target.GetComponent<Collider>();
            Vector3 end = targetCollider != null ? targetCollider.bounds.center : target.transform.position + Vector3.up * 0.6f;
            Vector3 ray = end - start;
            float range = ray.magnitude;
            if (range < 0.01f)
                return true;
            int count = Physics.RaycastNonAlloc(start, ray / range, rayHits, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform obstacle = rayHits[i].collider.transform;
                if (obstacle.IsChildOf(transform) || obstacle.IsChildOf(target.transform))
                    continue;
                // Otros enemigos no tapan el area: solo paredes y escenario.
                if (obstacle.GetComponentInParent<CombatDamageReceiver>() != null)
                    continue;
                return false;
            }
            return true;
        }

        private void MoveTo(Vector3 destination)
        {
            Vector3 delta = destination - transform.position;
            delta.y = 0f;
            if (body != null && body.enabled)
            {
                body.Move(delta);   // respeta paredes: no se atraviesa geometria
            }
            else
            {
                float length = delta.magnitude;
                if (length > 0.01f && Physics.Raycast(transform.position, delta / length, out RaycastHit wall,
                        length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    !wall.collider.transform.IsChildOf(transform))
                    delta = delta / length * Mathf.Max(0f, wall.distance - 0.5f);
                transform.position += delta;
            }
            Physics.SyncTransforms();
        }

        // ── Utilidades ───────────────────────────────────────────────────

        private bool Alive() => this != null && health != null && !health.IsDefeated;

        private Vector3 Facing()
        {
            Vector3 facing = motor != null ? motor.CurrentFacing : transform.forward;
            facing.y = 0f;
            return facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward;
        }

        private Vector3 CastOrigin() =>
            body != null ? body.bounds.center + Vector3.up * 0.2f : transform.position + Vector3.up;

        private Vector3 Ground(Vector3 point)
        {
            float feet = body != null ? body.bounds.min.y : transform.position.y - 1f;
            return new Vector3(point.x, feet + 0.08f, point.z);
        }

        /// <summary>Punto del suelo bajo el cursor; sin camara, delante del jugador.</summary>
        public Vector3 AimPoint()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera == null)
                return transform.position + Facing() * 8f;
            Ray ray = viewCamera.ScreenPointToRay(MagicKeys.Pointer);
            int count = Physics.RaycastNonAlloc(ray, rayHits, 200f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            Vector3 point = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                if (rayHits[i].collider.transform.IsChildOf(transform) || rayHits[i].distance >= best)
                    continue;
                best = rayHits[i].distance;
                point = rayHits[i].point;
            }
            if (best < float.PositiveInfinity)
                return point;
            Plane plane = new Plane(Vector3.up, transform.position);
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : transform.position + Facing() * 8f;
        }

        private static void Telegraph(Transform parent, Vector3 position, float radius, Color tint, float lifetime)
        {
            LineRenderer ring = PrototypeVfx.CreateRing("Aviso hechizo — sin colision", parent, radius, 0.09f, tint);
            if (ring == null)
                return;
            if (parent != null)
                ring.transform.localPosition = new Vector3(0f, 0.12f - (parent.GetComponent<CharacterController>() != null
                    ? parent.GetComponent<CharacterController>().bounds.extents.y : 1f), 0f);
            else
                ring.transform.position = position;
            Destroy(ring.gameObject, Mathf.Max(0.1f, lifetime));
        }

        private static void DrawBolt(List<Vector3> points, Color tint)
        {
            GameObject visual = new GameObject("Cadena de rayos — sin colision");
            LineRenderer line = visual.AddComponent<LineRenderer>();
            line.sharedMaterial = PrototypeVfx.SharedMaterial;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.12f;
            line.startColor = line.endColor = tint;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            Destroy(visual, 0.25f);
        }

        private void Announce(string value)
        {
            message = value;
            messageUntil = Time.time + 1.8f;
        }

        // ── Interfaz ─────────────────────────────────────────────────────

        private void RefreshLabels()
        {
            labels.Clear();
            labelled.Clear();
            for (int i = 0; i < EssenceSource.All.Count; i++)
                AddLabel(EssenceSource.All[i] != null ? EssenceSource.All[i].gameObject : null);
            for (int i = 0; i < StatusEffects.All.Count; i++)
                AddLabel(StatusEffects.All[i] != null ? StatusEffects.All[i].gameObject : null);
        }

        private void AddLabel(GameObject target)
        {
            if (target == null || target.transform.IsChildOf(transform) || !labelled.Add(target))
                return;
            CombatHealth targetHealth = target.GetComponent<CombatHealth>();
            if (targetHealth == null || targetHealth.IsDefeated)
                return;
            if ((target.transform.position - transform.position).sqrMagnitude > 22f * 22f)
                return;
            text.Clear();
            Vhaldren.Gameplay.Progression.CombatLevel level = target.GetComponent<Vhaldren.Gameplay.Progression.CombatLevel>();
            if (level != null)
            {
                int gear = equipment != null ? equipment.GearLevel : level.Level;
                int gap = level.Level - gear;
                // Rojo: muy por encima de tu equipo; amarillo: reto; blanco: a tu altura.
                text.Append(gap >= 6 ? "<color=#ff5050>" : gap >= 2 ? "<color=#ffd24a>" : "<color=#dddddd>")
                    .Append("Nv ").Append(level.Level).Append("</color>\n");
            }
            StatusEffects status = target.GetComponent<StatusEffects>();
            if (status != null)
            {
                if (status.IsFrozen)
                    text.Append("<color=#9ef>CONGELADO</color> ");
                for (int k = 0; k <= (int)StatusKind.Dazzle; k++)
                {
                    StatusKind kind = (StatusKind)k;
                    if (!status.Has(kind))
                        continue;
                    text.Append(StatusEffects.DisplayName(kind));
                    if (kind == StatusKind.Chill)
                        text.Append(" x").Append(status.ChillStacks);
                    text.Append(' ');
                }
            }
            EssenceSource source = target.GetComponent<EssenceSource>();
            if (source != null && targetHealth.CurrentHealth <= targetHealth.MaximumHealth * PlayerFeeding.Threshold)
                text.Append("\n<color=#f66>Esencia ").Append(EssenceCatalog.Name(source.Kind)).Append(' ')
                    .Append(source.Quality).Append("% — H</color>");
            if (text.Length > 0)
                labels.Add(new LabelEntry { Target = target.transform, Text = text.ToString() });
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || health == null)
                return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true, wordWrap = true };
                centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, richText = true };
            }

            if (Time.time >= nextLabelRefresh)
            {
                nextLabelRefresh = Time.time + 0.2f;
                RefreshLabels();
            }
            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam != null)
            {
                for (int i = 0; i < labels.Count; i++)
                {
                    if (labels[i].Target == null)
                        continue;
                    Vector3 screen = cam.WorldToScreenPoint(labels[i].Target.position + Vector3.up * 2.5f);
                    if (screen.z <= 0f)
                        continue;
                    GUI.Label(new Rect(screen.x - 110f, Screen.height - screen.y - 20f, 220f, 40f), labels[i].Text, centered);
                }
            }

            PlayerBuilder builder = GetComponent<PlayerBuilder>();
            if (builder != null && builder.IsBuilding)
                return;

            // Centrada, pero sin invadir la columna derecha (tablón del pueblo, F1).
            float rightLimit = Screen.width - 395f;
            float width = Mathf.Clamp(rightLimit - 12f, 300f, 600f);
            float x = Mathf.Max(12f, Mathf.Min((Screen.width - width) * 0.5f, rightLimit - width));
            float y = Screen.height - 196f;
            Color tint = SpellCatalog.SchoolColor(school);
            string hex = ColorUtility.ToHtmlStringRGB(tint);

            text.Clear();
            text.Append("<b><color=#").Append(hex).Append('>').Append(SpellCatalog.SchoolName(school).ToUpperInvariant())
                .Append("</color></b>   Tab: escuela (").Append(UnlockedCount).Append("/6)");
            if (equipment == null)
                equipment = GetComponent<Vhaldren.Gameplay.Progression.PlayerEquipment>();
            if (equipment != null)
                text.Append("   Equipo <b>Nv ").Append(equipment.GearLevel).Append("</b> (U)");
            if (combat != null && combat.InCombat)
                text.Append("   <color=#f88>EN COMBATE ").Append(combat.SecondsLeft.ToString("0")).Append("s</color>");
            if (DayNightCycle.Active != null)
                text.Append("   ").Append(DayNightCycle.Active.IsCrimsonMoon ? "<color=#f55>" : "<color=#ccc>")
                    .Append(DayNightCycle.Active.Clock).Append("</color>");
            ArcaneBarrier barrier = GetComponent<ArcaneBarrier>();
            if (barrier != null && barrier.Active)
                text.Append("   Égida ").Append(barrier.Remaining.ToString("0"));
            GUI.Box(new Rect(x, y, width, 24f), text.ToString(), style);
            if (Vhaldren.Gameplay.Races.PlayerRace.Local != null)
                GUI.Box(new Rect(x, y - 26f, width, 24f), Vhaldren.Gameplay.Races.PlayerRace.Local.HudText(), style);

            float slotWidth = (width - 8f) / 3f;
            for (int slot = 0; slot < SpellCatalog.SpellsPerSchool; slot++)
            {
                SpellDefinition spell = SpellCatalog.Get(school, slot);
                int left = ChargesOf(spell);
                float cd = CooldownRemaining(spell);
                text.Clear();
                text.Append("<b>").Append(slot + 1).Append("  ").Append(spell.DisplayName).Append("</b>\n");
                if (left > 0)
                {
                    text.Append("<color=#8f8>LISTO</color>");
                    if (spell.Charges > 1)
                        text.Append(" ").Append(left).Append('/').Append(spell.Charges);
                }
                else
                {
                    text.Append("<color=#f99>").Append(cd.ToString("0.0")).Append(" s</color>");
                }
                text.Append("  <size=11>").Append(StatusEffects.DisplayName(spell.Status)).Append("</size>");
                GUI.Box(new Rect(x + slot * (slotWidth + 4f), y + 27f, slotWidth, 40f), text.ToString(), style);
            }

            text.Clear();
            if (essence != null && essence.Kind != EssenceKind.None)
                text.Append("Esencia <b>").Append(EssenceCatalog.Name(essence.Kind)).Append(' ').Append(essence.Quality)
                    .Append("%</b> (").Append(EssenceCatalog.Bonus(essence.Kind)).Append(")  reserva ")
                    .Append(essence.Pool.ToString("0"));
            else
                text.Append("Sin esencia: debilita a un enemigo y mantén H para absorberla");
            if (Time.time < messageUntil)
                text.Append("   ·   ").Append(message);
            GUI.Box(new Rect(x, y + 70f, width, 24f), text.ToString(), style);
        }
    }
}
