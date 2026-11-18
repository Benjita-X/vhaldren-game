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
        private readonly bool[] exclusiveGranted = new bool[SpellCatalog.SchoolCount];
        private readonly int[] charges = new int[SpellCatalog.SchoolCount * SpellCatalog.SpellsPerSchool];
        private readonly float[] rechargeAt = new float[SpellCatalog.SchoolCount * SpellCatalog.SpellsPerSchool];
        private readonly HashSet<CombatDamageReceiver> struck = new HashSet<CombatDamageReceiver>();
        private readonly RaycastHit[] rayHits = new RaycastHit[16];
        private readonly List<LabelEntry> labels = new List<LabelEntry>();
        private readonly HashSet<GameObject> labelled = new HashSet<GameObject>();
        private readonly StringBuilder text = new StringBuilder(256);
        private readonly List<Vector3> chainPoints = new List<Vector3>(LightningArc.MaxAnchors);
        private Collider[] candidates = new Collider[48];
        private SpellCastCharge castCharge;

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
            if (castCharge != null)
                castCharge.Cancel();
            // Un Salto de alas cortado a medias nunca deja al jugador sin colisiones ni bloqueado.
            LuciferFlight flight = GetComponent<LuciferFlight>();
            if (flight != null)
                flight.Land();
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
            // La Luz caída no se aprende: solo la trae el linaje de Lucifer.
            if (SpellCatalog.IsExclusive(value) && !exclusiveGranted[(int)value])
                return false;
            unlocked[(int)value] = true;
            SchoolUnlocked?.Invoke(value);
            Announce("Nueva escuela aprendida: " + SpellCatalog.SchoolName(value) + " (Tab)");
            return true;
        }

        /// <summary>Escuelas que este personaje podría llegar a tener (comunes + exclusivas concedidas).</summary>
        public int AvailableSchoolCount
        {
            get
            {
                int count = SpellCatalog.CommonSchoolCount;
                for (int i = 0; i < exclusiveGranted.Length; i++)
                    if (exclusiveGranted[i])
                        count++;
                return count;
            }
        }

        public bool HasExclusive(SpellSchool value) => exclusiveGranted[(int)value];

        /// <summary>
        /// Concede una escuela exclusiva del linaje (Luz caída para Lucifer), la aprende sin
        /// aviso y, si se pide, la deja activa aunque haya combate (inicio de partida).
        /// </summary>
        public void GrantExclusive(SpellSchool value, bool select)
        {
            exclusiveGranted[(int)value] = true;
            if (!unlocked[(int)value])
            {
                unlocked[(int)value] = true;
                SchoolUnlocked?.Invoke(value);
            }
            if (select)
                school = value;
        }

        /// <summary>Retira una escuela exclusiva (cambio de linaje en pruebas o capturas).</summary>
        public void RevokeExclusive(SpellSchool value)
        {
            exclusiveGranted[(int)value] = false;
            unlocked[(int)value] = false;
            if (school == value)
                school = SpellSchool.Pyromancy;
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

        /// <summary>Segundos que tarda en volver una carga (con la bonificación de esencia).</summary>
        public float RechargeSeconds(SpellDefinition spell) => EffectiveCooldown(spell);

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
            if (Vhaldren.Gameplay.NPC.NpcDialogue.IsOpen)
                return;   // En un dialogo 1-4 eligen respuesta.

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
            // En pleno Salto de alas no se conjura (sin avisos: es un instante).
            LuciferFlight flight = GetComponent<LuciferFlight>();
            if (flight != null && flight.Busy)
                return false;
            if (modifiers != null && !modifiers.AllowsAttack)
                return Reject(label + ": cierra construcción (B) o diálogo (N)");

            // Relanzar Paso sombrío: vuelves al punto de partida sin gastar carga.
            if (spell.Delivery == SpellDelivery.Blink && Time.time < blinkRecastUntil)
            {
                blinkRecastUntil = 0f;
                ShadowPuff(spell.School, transform.position);
                MoveTo(blinkReturnPoint);
                SpellImpactFlash.Spawn(transform.position + Vector3.up * 0.5f,
                    SpellCatalog.SchoolColor(spell.School), 0.8f, 0.3f, 1.2f, SpellVfx.ElementOf(spell.School));
                ShadowPuff(spell.School, transform.position);
                Announce(label + ": regreso", false);
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
            Announce(label, false);
            if (GameFeel.Enabled)
                GameAudio.Play(spell.School == SpellSchool.Cryomancy ? GameSound.Frost : GameSound.SpellCast,
                    transform.position, 0.75f);
            // La Luz caída (Lucifer) tiene su propio kit; el resto, el grimorio común.
            StartCoroutine(spell.School == SpellSchool.FallenLight
                ? FallenLightKit.Execute(this, spell, SpellPower, aimPoint)
                : Execute(spell, SpellPower, aimPoint));
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
            VfxElement element = SpellVfx.ElementOf(spell.School);
            Vector3 origin = transform.position;
            Vector3 toAim = aim - origin;
            toAim.y = 0f;
            Vector3 direction = toAim.sqrMagnitude > 0.01f ? toAim.normalized : Facing();

            // Preparacion visible: la energia de la escuela converge en la mano.
            SpellCastCharge hand = spell.Delivery == SpellDelivery.Blink || spell.Delivery == SpellDelivery.Barrier
                ? null : CastCharge();
            if (hand != null)
                hand.Begin(element, tint, spell.Windup, direction);

            switch (spell.Delivery)
            {
                case SpellDelivery.Bolt:
                    if (spell.Windup > 0f)
                        yield return new WaitForSeconds(spell.Windup);
                    if (!Alive())
                    {
                        CancelCharge(hand);
                        yield break;
                    }
                    if (hand != null)
                        hand.Release(direction);
                    SpellProjectile.Launch(this, spell, power, CastOrigin() + direction * 0.6f, direction);
                    break;

                case SpellDelivery.Nova:
                {
                    GroundTelegraph warning = Telegraph(transform, Vector3.zero, spell.Radius, tint, spell.Windup);
                    if (spell.Windup > 0f)
                        yield return new WaitForSeconds(spell.Windup);
                    if (!Alive())
                    {
                        if (warning != null)
                            warning.Cancel();
                        CancelCharge(hand);
                        yield break;
                    }
                    if (hand != null)
                        hand.Release(Vector3.up);
                    SpellVfx.AreaBurst(spell, Ground(transform.position), transform);
                    AreaHit(transform.position, spell, power);
                    break;
                }

                case SpellDelivery.Zone:
                {
                    Vector3 point = origin + Vector3.ClampMagnitude(toAim, spell.Range);
                    point.y = origin.y;
                    Vector3 ground = ZoneGround(point);
                    GroundTelegraph warning = Telegraph(null, ground, spell.Radius, tint, spell.Windup);
                    // El remate (meteoro, rayos, luz...) termina justo cuando estalla el area.
                    float travel = Mathf.Min(SpellVfx.DescentSeconds, spell.Windup);
                    Coroutine descent = SpellVfx.Descent(spell, ground, spell.Windup - travel, travel);
                    if (spell.Windup > 0f)
                        yield return new WaitForSeconds(spell.Windup);
                    if (!Alive())
                    {
                        if (warning != null)
                            warning.Cancel();
                        SpellVfx.StopDescent(descent);
                        CancelCharge(hand);
                        yield break;
                    }
                    if (hand != null)
                        hand.Release(direction);
                    SpellVfx.AreaBurst(spell, ground, transform);
                    AreaHit(point, spell, power);
                    break;
                }

                case SpellDelivery.Chain:
                    if (spell.Windup > 0f)
                        yield return new WaitForSeconds(spell.Windup);
                    if (!Alive())
                    {
                        CancelCharge(hand);
                        yield break;
                    }
                    if (hand != null)
                        hand.Release(direction);
                    ChainHit(direction, spell, power);
                    break;

                case SpellDelivery.Blink:
                {
                    blinkReturnPoint = transform.position;
                    float distance = Mathf.Min(spell.Range, toAim.magnitude > 0.5f ? toAim.magnitude : spell.Range);
                    SpellImpactFlash.Spawn(transform.position + Vector3.up * 0.5f, tint, 0.8f, 0.3f, 1.2f, element);
                    ShadowPuff(spell.School, transform.position);
                    MoveTo(transform.position + direction * distance);
                    blinkRecastUntil = Time.time + BlinkRecastSeconds;
                    SpellImpactFlash.Spawn(Ground(transform.position), tint, 1.0f, 0.4f, spell.Radius, element);
                    ShadowPuff(spell.School, transform.position);
                    AreaHit(transform.position, spell, power);
                    break;
                }

                case SpellDelivery.Barrier:
                {
                    ArcaneBarrier barrier = GetComponent<ArcaneBarrier>();
                    if (barrier == null)
                        barrier = gameObject.AddComponent<ArcaneBarrier>();
                    barrier.Raise(spell.Damage * power, BarrierSeconds, tint);
                    SpellImpactFlash.Spawn(transform.position + Vector3.up, tint, 0.9f, 0.35f, 1f, element);
                    break;
                }
            }

            if (spell.SelfHeal > 0f && Alive())
            {
                health.Heal(spell.SelfHeal * power);
                // Curacion visible: motas doradas que suben por el cuerpo.
                VfxHost host = VfxHost.Get();
                if (host != null)
                    host.Glints(transform.position + Vector3.up * 0.7f, 0.45f, new Color(1f, 0.93f, 0.6f, 1f), 16,
                        0.2f, 0.9f, Vector3.up * 1.5f);
            }
        }

        internal SpellCastCharge CastCharge()
        {
            if (castCharge == null)
                castCharge = SpellCastCharge.For(this);
            return castCharge;
        }

        private static void CancelCharge(SpellCastCharge hand)
        {
            if (hand != null)
                hand.Cancel();
        }

        /// <summary>Bocanada de humo de sombra al desaparecer o aparecer.</summary>
        private static void ShadowPuff(SpellSchool school, Vector3 feet)
        {
            VfxHost host = VfxHost.Get();
            if (host == null)
                return;
            host.Smoke(feet + Vector3.up * 0.8f, 0.45f, SpellVfx.PaletteOf(school).Spark, 14, 0.95f, 0.85f,
                Vector3.up * 0.45f);
        }

        /// <summary>Suelo real bajo el centro de una zona (terreno irregular incluido).</summary>
        internal Vector3 ZoneGround(Vector3 point)
        {
            Vector3 flat = Ground(point);
            return VfxLibrary.FindGround(flat, out Vector3 ground, out _) ? ground : flat;
        }

        /// <summary>Aplica el golpe de un hechizo: daño, critico, estatica, estado y robo de vida.</summary>
        public float ApplySpellHit(CombatDamageReceiver receiver, SpellDefinition spell, float power, Vector3 origin) =>
            ApplySpellHit(receiver, spell, power, origin, 1f, 1f);

        /// <summary>
        /// Igual, con una escala de daño (lanza que pierde fuerza al atravesar, borde de un
        /// area) y una fuerza de empuje para la respuesta del golpe.
        /// </summary>
        public float ApplySpellHit(CombatDamageReceiver receiver, SpellDefinition spell, float power, Vector3 origin,
            float damageScale, float force)
        {
            if (receiver == null || spell == null || receiver.transform.IsChildOf(transform))
                return 0f;
            // Afinidad de la raza con la escuela (p. ej. vampiro +30 % Hemomancia).
            float damage = spell.Damage * Mathf.Max(0f, power) * Mathf.Max(0f, damageScale) *
                           Vhaldren.Gameplay.Races.PlayerRace.AffinityFor(spell.School);
            bool critical = essence != null && random.NextDouble() < essence.CriticalChance;
            if (critical)
                damage *= CriticalMultiplier;

            CombatDamageResult result;
            // El receptor y la respuesta del golpe saben si es critico y cuanto empuja.
            using (CombatHitContext.Push(critical, force))
                result = receiver.Receive(damage, CombatDamageKind.Magic, origin);
            if (result.Applied <= 0f)
                return 0f;

            StatusEffects status = StatusEffects.For(receiver);
            float total = result.Applied;
            bool hadBleed = false;
            if (status != null)
            {
                hadBleed = status.Has(StatusKind.Bleed);
                total += status.ConsumeStatic(transform.position);
                for (int i = 0; i < spell.StatusStacks; i++)
                    status.ApplyFrom(spell.Status, this);
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
            SpellVfx.Hit(spell.School, receiver.transform.position + Vector3.up * 0.9f, critical);
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
            List<Vector3> points = chainPoints;
            points.Clear();
            points.Add(from);
            for (int jump = 0; jump < Mathf.Max(1, spell.ChainTargets); jump++)
            {
                int count = Physics.OverlapSphereNonAlloc(searchCenter, searchRadius, candidates,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Collider[] chainCandidates = candidates;
                if (count >= candidates.Length)
                {
                    chainCandidates = Physics.OverlapSphere(searchCenter, searchRadius,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    count = chainCandidates.Length;
                }
                CombatDamageReceiver best = null;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    CombatDamageReceiver receiver = chainCandidates[i] != null
                        ? chainCandidates[i].GetComponentInParent<CombatDamageReceiver>() : null;
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
            DrawChain(points, spell.School);
        }

        /// <summary>
        /// Enemigos vivos dentro del radio y con linea despejada desde el centro (las paredes
        /// tapan, los demas combatientes no). Para los hechizos de la Luz caida.
        /// </summary>
        internal int CollectTargets(Vector3 center, float radius, List<CombatDamageReceiver> into)
        {
            into.Clear();
            int count = Query(center, radius);
            for (int i = 0; i < count; i++)
            {
                CombatDamageReceiver receiver = candidates[i] != null
                    ? candidates[i].GetComponentInParent<CombatDamageReceiver>() : null;
                if (receiver == null || receiver.transform.IsChildOf(transform) || !struck.Add(receiver))
                    continue;
                CombatHealth targetHealth = receiver.GetComponent<CombatHealth>();
                if (targetHealth == null || targetHealth.IsDefeated)
                    continue;
                if (!Unobstructed(receiver, center + Vector3.up * 0.55f))
                    continue;
                into.Add(receiver);
            }
            return into.Count;
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

        internal bool Unobstructed(CombatDamageReceiver target, Vector3 start)
        {
            Collider targetCollider = target.GetComponent<Collider>();
            Vector3 end = targetCollider != null ? targetCollider.bounds.center : target.transform.position + Vector3.up * 0.6f;
            Vector3 ray = end - start;
            float range = ray.magnitude;
            if (range < 0.01f)
                return true;
            int count = Physics.RaycastNonAlloc(start, ray / range, rayHits, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] completeHits = null;
            if (count >= rayHits.Length)
            {
                completeHits = Physics.RaycastAll(start, ray / range, range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = completeHits.Length;
            }
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = completeHits != null ? completeHits[i] : rayHits[i];
                if (candidate.collider == null)
                    continue;
                Transform obstacle = candidate.collider.transform;
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

        internal bool Alive() => this != null && health != null && !health.IsDefeated;

        internal Vector3 Facing()
        {
            Vector3 facing = motor != null ? motor.CurrentFacing : transform.forward;
            facing.y = 0f;
            return facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward;
        }

        internal Vector3 CastOrigin() =>
            body != null ? body.bounds.center + Vector3.up * 0.2f : transform.position + Vector3.up;

        internal Vector3 Ground(Vector3 point)
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
            RaycastHit[] completeHits = null;
            if (count >= rayHits.Length)
            {
                completeHits = Physics.RaycastAll(ray, 200f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                count = completeHits.Length;
            }
            float best = float.PositiveInfinity;
            Vector3 point = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = completeHits != null ? completeHits[i] : rayHits[i];
                if (candidate.collider == null ||
                    candidate.collider.transform.IsChildOf(transform) ||
                    candidate.distance >= best)
                    continue;
                best = candidate.distance;
                point = candidate.point;
            }
            if (best < float.PositiveInfinity)
                return point;
            Plane plane = new Plane(Vector3.up, transform.position);
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : transform.position + Facing() * 8f;
        }

        /// <summary>
        /// Aviso de area en el suelo que se llena durante la preparacion: con
        /// <paramref name="parent"/> sigue al mago (Nova); sin el, queda en el punto (Zona).
        /// </summary>
        internal GroundTelegraph Telegraph(Transform parent, Vector3 position, float radius, Color tint, float seconds)
        {
            if (seconds <= 0f)
                return null;
            if (parent == null)
                return GroundTelegraph.Show(null, 0f, position, radius, tint, seconds);
            float feet = body != null ? body.bounds.min.y : parent.position.y - 1f;
            return GroundTelegraph.Show(parent, feet - parent.position.y + 0.06f, parent.position, radius, tint, seconds);
        }

        /// <summary>Cadena de rayos en zigzag que chisporrotea, con chispas en cada objetivo.</summary>
        private static void DrawChain(List<Vector3> points, SpellSchool school)
        {
            VfxPalette palette = SpellVfx.PaletteOf(school);
            // Dos rayos entrelazados (resplandor del color de la escuela y nucleo casi blanco).
            LightningArc.Strike(points, palette.Glow, 0.17f, 0.36f, 0.45f);
            LightningArc.Strike(points, palette.Core, 0.07f, 0.28f, 0.7f);
            VfxHost host = VfxHost.Get();
            if (host == null)
                return;
            for (int i = 1; i < points.Count; i++)
                host.Burst(VfxElement.Storm, points[i], Vector3.up, palette.Spark, 12, 5.5f, 0.12f, 0.3f, 1f, 0.1f);
        }

        /// <summary>Lanzar un hechizo no avisa (la barra ya lo muestra); los rechazos y escuelas sí.</summary>
        private void Announce(string value, bool notify = true)
        {
            message = value;
            messageUntil = Time.time + 1.8f;
            if (notify)
                Vhaldren.Gameplay.UI.HudNotices.Post(value, SpellCatalog.SchoolColor(school) * 0.35f + Vhaldren.Gameplay.UI.HudNotices.Neutral * 0.65f);
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
            // HUD normal: ActionBarHud y WorldHud. Esto queda como recuadros de depuración (F3).
            if (Event.current.type != EventType.Repaint || health == null || !Vhaldren.Gameplay.UI.HudMode.Debug)
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
                .Append("</color></b>   Tab: escuela (").Append(UnlockedCount).Append('/').Append(AvailableSchoolCount).Append(')');
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
