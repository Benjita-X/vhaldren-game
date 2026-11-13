using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Absorcion de esencia. Debilita a un enemigo por debajo del 30 % de vida, mantén H
    /// cerca y canaliza 2,4 s. Mientras absorbes quedas Expuesto (recibes +30 % de daño)
    /// y un golpe fuerte te interrumpe. Al terminar adoptas su tipo y calidad de esencia,
    /// recuperas vida y el enemigo cae. Clic izquierdo durante la absorcion lo ejecuta
    /// al instante, pero sin esencia.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class PlayerFeeding : MonoBehaviour
    {
        public const float Range = 2.6f;
        public const float Threshold = 0.30f;
        public const float ChannelSeconds = 2.4f;
        public const float HealFraction = 0.15f;
        public const float InterruptFraction = 0.12f;

        private readonly Collider[] nearby = new Collider[32];
        private CombatHealth health;
        private EssenceReservoir reservoir;
        private PlayerSpellbook spellbook;
        private PlayerActionModifiers modifiers;
        private CombatHealth target;
        private EssenceSource source;
        private int attackLock;
        private float progress;
        private float damageDuringChannel;
        private bool waitForRelease;
        private int deferredLock;
        private int deferredUntilFrame;
        private string message = string.Empty;
        private float messageUntil;
        private GUIStyle style;

        public bool IsChanneling => target != null;
        public float Progress01 => target != null ? Mathf.Clamp01(progress / ChannelSeconds) : 0f;
        public CombatHealth Target => target;
        public CombatHealth Candidate { get; private set; }
        public int Absorbed { get; private set; }
        public int Executed { get; private set; }
        public string LastMessage => message;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            reservoir = GetComponent<EssenceReservoir>();
            if (reservoir == null)
                reservoir = gameObject.AddComponent<EssenceReservoir>();
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<CombatHealth>();
            health.Damaged += OnHurt;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Damaged -= OnHurt;
            End();
            if (deferredLock != 0)
                modifiers?.RemoveModifier(deferredLock);
            deferredLock = 0;
        }

        private void Update()
        {
            if (Time.timeScale > 0f)
                Tick(MagicKeys.AbsorbHeld, MagicKeys.PrimaryPressed, Time.deltaTime);
        }

        /// <summary>Avanza la absorcion. Separado de Update para poder probarlo.</summary>
        public void Tick(bool held, bool executePressed, float deltaTime)
        {
            // El clic que ejecuta no debe disparar tambien la ballesta en el mismo frame.
            if (deferredLock != 0 && Time.frameCount > deferredUntilFrame)
            {
                modifiers?.RemoveModifier(deferredLock);
                deferredLock = 0;
            }
            if (!held)
                waitForRelease = false;

            if (target == null)
            {
                Candidate = FindCandidate();
                if (held && !waitForRelease && Candidate != null)
                    Begin(Candidate);
                return;
            }

            if (health == null || health.IsDefeated)
            {
                Cancel("Absorción interrumpida: has caído");
                return;
            }
            if (target.IsDefeated)
            {
                Cancel("El objetivo cayó antes de tiempo");
                return;
            }
            if (HorizontalDistance(target.transform.position) > Range + 0.8f)
            {
                Cancel("Absorción cancelada: demasiado lejos");
                return;
            }
            if (executePressed)
            {
                Execute();
                return;
            }
            if (!held)
            {
                Cancel("Absorción cancelada: soltaste H");
                return;
            }

            // La victima queda inmovil y el jugador vulnerable mientras dura.
            StatusEffects.For(target)?.Freeze(0.4f);
            StatusEffects.For(health)?.Apply(StatusKind.Exposed, 0.4f);
            progress += Mathf.Max(0f, float.IsNaN(deltaTime) ? 0f : deltaTime);
            if (progress >= ChannelSeconds)
                Complete();
        }

        /// <summary>Empieza a absorber a un objetivo concreto si cumple las condiciones.</summary>
        public bool Begin(CombatHealth victim)
        {
            if (victim == null || target != null)
                return false;
            if (modifiers == null)
                modifiers = GetComponent<PlayerActionModifiers>();
            if (modifiers != null && !modifiers.AllowsAttack)
            {
                Announce("No puedes absorber mientras construyes o hablas");
                waitForRelease = true;
                return false;
            }
            if (!IsEligible(victim))
            {
                Announce("Debilítalo por debajo del 30 % de vida para absorber su esencia");
                waitForRelease = true;
                return false;
            }
            target = victim;
            source = EssenceSource.For(victim);
            progress = 0f;
            damageDuringChannel = 0f;
            if (modifiers != null)
                attackLock = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack);
            StatusEffects.For(victim)?.Freeze(0.4f);
            StatusEffects.For(health)?.Apply(StatusKind.Exposed, 0.4f);
            Announce("Absorbiendo esencia " + EssenceCatalog.Name(source.Kind) + " " + source.Quality + "%");
            return true;
        }

        public bool IsEligible(CombatHealth victim)
        {
            if (victim == null || victim.IsDefeated || victim.transform.IsChildOf(transform))
                return false;
            if (victim.GetComponent<PrototypeEnemy>() == null && victim.GetComponent<EssenceSource>() == null)
                return false;
            return victim.CurrentHealth <= victim.MaximumHealth * Threshold &&
                   HorizontalDistance(victim.transform.position) <= Range;
        }

        private CombatHealth FindCandidate()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, Range, nearby,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider[] scan = nearby;
            if (count >= nearby.Length)
            {
                scan = Physics.OverlapSphere(transform.position, Range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = scan.Length;
            }
            CombatHealth best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                CombatHealth candidate = scan[i] != null ? scan[i].GetComponentInParent<CombatHealth>() : null;
                if (candidate == null || !IsEligible(candidate))
                    continue;
                float distance = HorizontalDistance(candidate.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        private void Complete()
        {
            EssenceKind kind = source != null ? source.Kind : EssenceKind.None;
            // De noche (y mas con la Luna carmesi) la esencia sale mas pura.
            int quality = source != null
                ? Mathf.Min(100, source.Quality + DayNightCycle.EssenceQualityBonus) : 0;
            CombatHealth victim = target;
            End();
            waitForRelease = true;

            reservoir.Absorb(kind, quality);
            Absorbed++;
            health.Heal(health.MaximumHealth * HealFraction);
            string learned = string.Empty;
            if (spellbook == null)
                spellbook = GetComponent<PlayerSpellbook>();
            if (EssenceCatalog.TeachesSchool(kind, out SpellSchool school) && spellbook != null && spellbook.Unlock(school))
                learned = " — aprendes " + SpellCatalog.SchoolName(school);
            if (kind == EssenceKind.Arcane && spellbook != null)
            {
                for (int i = 0; i < SpellCatalog.SchoolCount; i++)
                    spellbook.Unlock((SpellSchool)i);
                learned = " — dominas todas las escuelas";
            }
            // Leer la esencia antes de rematar: al caer, la fuente cambia para su reaparicion.
            victim.ApplyDamage(victim.CurrentHealth);
            SpellImpactFlash.Spawn(transform.position + Vector3.up, new Color(0.85f, 0.1f, 0.2f), 1f, 0.45f, 1.4f);
            Announce("Esencia " + EssenceCatalog.Name(kind) + " " + quality + "% absorbida" + learned);
        }

        private void Execute()
        {
            CombatHealth victim = target;
            int keepLock = attackLock;
            attackLock = 0;
            End();
            deferredLock = keepLock;
            deferredUntilFrame = Time.frameCount;
            waitForRelease = true;
            Executed++;
            victim.ApplyDamage(victim.CurrentHealth);
            Announce("Ejecutado: no obtienes esencia");
        }

        public void Cancel(string reason)
        {
            if (target == null)
                return;
            End();
            waitForRelease = true;
            Announce(reason);
        }

        private void End()
        {
            if (modifiers != null && attackLock != 0)
                modifiers.RemoveModifier(attackLock);
            attackLock = 0;
            StatusEffects self = GetComponent<StatusEffects>();
            if (self != null)
                self.Clear(StatusKind.Exposed);
            target = null;
            source = null;
            progress = 0f;
        }

        private void OnHurt(float amount)
        {
            if (target == null)
                return;
            damageDuringChannel += amount;
            if (damageDuringChannel >= health.MaximumHealth * InterruptFraction)
                Cancel("¡Te interrumpieron mientras absorbías!");
        }

        private float HorizontalDistance(Vector3 point)
        {
            Vector3 delta = point - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void Announce(string value)
        {
            message = value;
            messageUntil = Time.time + 2.2f;
            Vhaldren.Gameplay.UI.HudNotices.Post(value, new Color(1f, 0.55f, 0.55f), 2.2f);
        }

        private void OnGUI()
        {
            // El aviso de absorber y su progreso los dibuja ActionBarHud; esto es solo F3.
            if (Event.current.type != EventType.Repaint || !Vhaldren.Gameplay.UI.HudMode.Debug)
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 13, richText = true };
            Rect rect = new Rect(Screen.width * 0.5f - 190f, Screen.height - 236f, 380f, 30f);
            if (target != null)
            {
                GUI.Box(rect, "<b>Absorbiendo esencia</b> — suelta H para cancelar · Clic: ejecutar", style);
                GUI.Box(new Rect(rect.x, rect.yMax + 2f, rect.width * Progress01, 7f), GUIContent.none);
            }
            else if (Candidate != null)
            {
                EssenceSource candidateSource = Candidate.GetComponent<EssenceSource>();
                string essence = candidateSource != null
                    ? EssenceCatalog.Name(candidateSource.Kind) + " " + candidateSource.Quality + "%"
                    : "desconocida";
                GUI.Box(rect, "Mantén <b>H</b>: absorber esencia " + essence, style);
            }
            else if (Time.time < messageUntil)
            {
                GUI.Box(rect, message, style);
            }
        }
    }
}
