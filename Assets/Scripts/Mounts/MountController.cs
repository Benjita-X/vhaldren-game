using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Exploration;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Mounts
{
    /// <summary>
    /// Monturas del jugador. G (reasignable) invoca y monta la ultima domada; G otra vez
    /// desmonta (en vuelo, aterriza y desmonta). Mantener G 0,6 s abre una rueda para
    /// elegir montura. Montado: velocidad propia con aceleracion suave y giro con inercia,
    /// Mayús = galope con aguante, sin ataques ni esquiva (atacar desmonta; un golpe fuerte
    /// derriba). Las aladas despegan y aterrizan con Ctrl y vuelan a unos 30 m sobre el
    /// relieve, sin atravesar montañas. Sustituye la locomocion de <see cref="PlayerMotor"/>
    /// mediante <see cref="IPlayerLocomotionOverride"/>; el jinete se sienta en la "silla".
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class MountController : MonoBehaviour, IPlayerLocomotionOverride
    {
        public enum MountState { OnFoot, Summoning, Riding, Flying, Landing, Dismounting }

        public const float WheelHoldSeconds = 0.6f;
        public const float SummonSeconds = 0.45f;
        public const float DismountSeconds = 0.3f;
        public const float Gravity = -25f;
        public const float GallopDrainPerSecond = 13f;
        public const float HeavyHitFraction = 0.12f;
        public const float GroundCameraDistance = 1.12f;
        public const float FlightCameraDistance = 1.75f;

        private PlayerMotor motor;
        private CharacterController body;
        private PlayerActionModifiers modifiers;
        private PlayerAnimationDriver animationDriver;
        private CombatHealth health;
        private PlayerBuilder builder;
        private TravelStamina stamina;
        private Transform visualRoot;
        private Renderer facingIndicator;
        private SimpleFollowCamera followCamera;
        private Camera followSource;
        private bool subscribed;

        private readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        private readonly Dictionary<GameObject, Vector3> modelScales = new Dictionary<GameObject, Vector3>();
        private MountDefinition current;
        private GameObject model;
        private QuadrupedGait gait;
        private Transform saddle;
        private MountState state;
        private float stateStart;
        private int lockToken;
        private bool dismountAfterLanding;

        private Vector3 heading = Vector3.forward;
        private float speed;
        private float verticalSpeed;
        private float yawRate;
        private float modelPitch, modelRoll;
        private float floorAhead = float.NegativeInfinity;
        private float lastFloor;
        private float nextFloorSample;

        private Vector3 visualRestLocal;
        private Quaternion visualRestLocalRotation = Quaternion.identity;
        private Vector3 hipOffset = new Vector3(0f, -0.1f, 0f);
        private bool riderBonesSearched;
        private Transform hips, upperLeft, lowerLeft, footLeft, upperRight, lowerRight, footRight;

        private float mountKeyDownAt = -1f;
        private bool wheelOpen;
        private int wheelHover = -1;
        private int wheelLock;
        private readonly List<string> wheelIds = new List<string>(8);
        private bool flightTipShown;

        public MountState State => state;
        public bool IsMounted => state != MountState.OnFoot;
        public bool IsFlying => state == MountState.Flying || state == MountState.Landing;
        public bool IsGalloping { get; private set; }
        public bool WheelOpen => wheelOpen;
        public MountDefinition Current => IsMounted ? current : null;
        public string CurrentMountId => IsMounted && current != null ? current.Id : null;
        public float Speed => IsMounted ? speed : 0f;

        public bool OverridesLocomotion => state != MountState.OnFoot && model != null;

        private void Awake() => Lookup();

        private void Lookup()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (body == null) body = GetComponent<CharacterController>();
            if (modifiers == null) modifiers = GetComponent<PlayerActionModifiers>();
            if (animationDriver == null) animationDriver = GetComponent<PlayerAnimationDriver>();
            if (builder == null) builder = GetComponent<PlayerBuilder>();
            if (stamina == null) stamina = GetComponent<TravelStamina>();
            if (health == null) health = GetComponent<CombatHealth>();
            if (health != null && !subscribed && isActiveAndEnabled)
            {
                health.Damaged += OnDamaged;
                health.Defeated += OnDefeated;
                subscribed = true;
            }
            if (visualRoot == null)
            {
                visualRoot = transform.Find("VisualRoot");
                if (visualRoot == null)
                {
                    Animator animator = GetComponentInChildren<Animator>();
                    if (animator != null && animator.transform != transform && animator.transform.parent != null)
                        visualRoot = animator.transform.parent == transform ? animator.transform : animator.transform.parent;
                }
                if (visualRoot == transform)
                    visualRoot = null;
                if (visualRoot != null)
                {
                    Transform indicator = visualRoot.Find("FacingIndicator");
                    facingIndicator = indicator != null ? indicator.GetComponent<Renderer>() : null;
                }
            }
        }

        private void OnEnable() => Lookup();

        private void OnDisable()
        {
            ForceDismount(true);
            CloseWheel();
            if (health != null && subscribed)
            {
                health.Damaged -= OnDamaged;
                health.Defeated -= OnDefeated;
            }
            subscribed = false;
        }

        private void OnDestroy()
        {
            foreach (GameObject cached in models.Values)
                if (cached != null)
                    Destroy(cached);
            models.Clear();
        }

        // ── Entrada ────────────────────────────────────────────────────

        private void Update()
        {
            Lookup();
            if (health != null && health.IsDefeated)
            {
                if (IsMounted)
                    ForceDismount(true);
                CloseWheel();
                mountKeyDownAt = -1f;
                return;
            }
            bool building = builder != null && builder.IsBuilding;
            if (building && IsMounted)
                ForceDismount(false);
            bool blocked = Time.timeScale <= 0f || ExplorationUi.MapOpen || building;
            HandleMountKey(blocked);
            if (IsMounted && !blocked)
            {
                HandleFlightKey();
                HandleAttackKeys();
            }
            AdvanceState();
            UpdateCamera();
            ExplorationUi.PlayerMounted = IsMounted;
            ExplorationUi.PlayerFlying = IsFlying;
        }

        private void HandleMountKey(bool blocked)
        {
            if (blocked)
            {
                CloseWheel();
                mountKeyDownAt = -1f;
                return;
            }
            if (GameInput.Down(GameAction.Mount))
                mountKeyDownAt = Time.unscaledTime;
            if (mountKeyDownAt < 0f)
                return;
            bool held = GameInput.Held(GameAction.Mount);
            if (held && !wheelOpen && Time.unscaledTime - mountKeyDownAt >= WheelHoldSeconds)
                OpenWheel();
            if (held)
                return;
            mountKeyDownAt = -1f;
            if (wheelOpen)
            {
                string pick = wheelHover >= 0 && wheelHover < wheelIds.Count ? wheelIds[wheelHover] : null;
                CloseWheel();
                if (pick != null)
                    Choose(pick);
            }
            else
                ToggleMount();
        }

        /// <summary>Lo que hace una pulsacion corta de G.</summary>
        public void ToggleMount()
        {
            if (IsMounted)
            {
                if (state == MountState.Flying || state == MountState.Landing)
                {
                    dismountAfterLanding = true;
                    BeginLanding();
                }
                else if (state == MountState.Riding)
                    Dismount();
                return;
            }
            string id = MountUnlocks.Last;
            if (string.IsNullOrEmpty(id))
            {
                HudNotices.Post("Aún no tienes monturas: busca bestias salvajes y dómalas (" +
                                GameInput.Label(GameAction.Gather) + ")", HudNotices.Warning, 3f);
                return;
            }
            TrySummon(id);
        }

        private void Choose(string id)
        {
            if (IsMounted && current != null && current.Id == id)
                return;
            if (IsFlying)
            {
                HudNotices.Post("Aterriza antes de cambiar de montura", HudNotices.Warning);
                return;
            }
            if (IsMounted)
                ForceDismount(true);
            TrySummon(id);
        }

        private void HandleFlightKey()
        {
            if (!GameInput.Down(GameAction.Fly) || current == null)
                return;
            if (!current.Flies)
            {
                HudNotices.Post(current.Name + " no puede volar", HudNotices.Warning);
                return;
            }
            if (state == MountState.Riding)
                TakeOff();
            else if (state == MountState.Flying)
                BeginLanding();
        }

        private void HandleAttackKeys()
        {
            if (wheelOpen)
                return;
            bool attack = GameInput.Down(GameAction.Sword) || GameInput.Down(GameAction.RunePulse) ||
                          GameInput.Down(GameAction.Frost) || GameInput.Down(GameAction.Spell1) ||
                          GameInput.Down(GameAction.Spell2) || GameInput.Down(GameAction.Spell3) ||
                          GameInput.Down(GameAction.RacialPower) || GameInput.Down(GameAction.Parry) ||
                          GameInput.Down(GameAction.Shield) || GameInput.Down(GameAction.Ward);
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                attack = true;
#endif
            if (!attack)
                return;
            if (state == MountState.Riding)
            {
                ForceDismount(false);
                HudNotices.Post("Desmontas para combatir", HudNotices.Neutral);
            }
            else if (IsFlying)
                HudNotices.Post("No puedes atacar en vuelo", HudNotices.Warning);
        }

        private void OnDamaged(float amount)
        {
            if (health == null || !(state == MountState.Riding || state == MountState.Summoning))
                return;
            if (amount >= Mathf.Max(12f, health.MaximumHealth * HeavyHitFraction))
            {
                ForceDismount(false);
                HudNotices.Post("¡Te han derribado de la montura!", HudNotices.Warning, 2.5f);
            }
        }

        private void OnDefeated() => ForceDismount(true);

        // ── Montar y desmontar ─────────────────────────────────────────

        /// <summary>Invoca y monta una montura domada. Devuelve false si no se puede ahora.</summary>
        public bool TrySummon(string id)
        {
            Lookup();
            if (IsMounted || motor == null || body == null || !motor.isActiveAndEnabled || !body.enabled)
                return false;
            if (!MountUnlocks.IsUnlocked(id))
            {
                HudNotices.Post("Todavía no has domado esa montura", HudNotices.Warning);
                return false;
            }
            if (!body.isGrounded && !motor.IsGrounded)
            {
                HudNotices.Post("Necesitas suelo firme para montar", HudNotices.Warning);
                return false;
            }
            WorldAtlas atlas = WorldAtlas.Active;
            if (atlas != null && atlas.WaterDepthAt(FeetPosition()) > 0.9f)
            {
                HudNotices.Post("No puedes montar dentro del agua", HudNotices.Warning);
                return false;
            }

            current = MountCatalog.Get(id);
            MountUnlocks.Last = current.Id;
            model = GetModel(current.Id);
            model.SetActive(true);
            gait = model.GetComponent<QuadrupedGait>();
            if (gait == null)
                gait = model.AddComponent<QuadrupedGait>();
            gait.Flying = false;
            gait.ForceFlap = false;
            saddle = MountModelFactory.EnsureSaddle(model);

            Vector3 facing = motor.CurrentFacing;
            facing.y = 0f;
            heading = facing.sqrMagnitude > 0.001f ? facing.normalized : transform.forward;
            speed = 0f;
            verticalSpeed = -2f;
            yawRate = 0f;
            modelPitch = modelRoll = 0f;
            dismountAfterLanding = false;

            if (modifiers != null && lockToken == 0)
                lockToken = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack |
                                                  PlayerActionModifiers.LockedAction.Dash);
            motor.LocomotionOverride = this;
            BeginSeat();
            SetState(MountState.Summoning);
            PlaceModel(0f);

            Vector3 feet = FeetPosition();
            SpellImpactFlash.Spawn(feet + Vector3.up * 0.1f, current.AccentColor, 2.6f, 0.5f, 2.2f);
            VfxHost host = VfxHost.Get();
            if (host != null)
            {
                host.Ring(VfxElement.Neutral, feet + Vector3.up * 0.15f, Vector3.up, 1.3f, current.AccentColor, 30, 3f, 1.6f, 0.13f, 0.7f);
                host.Smoke(feet + Vector3.up * 0.4f, 1.2f, new Color(0.6f, 0.58f, 0.62f, 0.5f), 10, 1.1f, 0.9f, Vector3.up * 0.6f);
            }
            HudNotices.Post("Montas: " + current.Name + " · " + GameInput.Label(GameAction.Mount) + " desmontar" +
                            (current.Flies ? " · " + GameInput.Label(GameAction.Fly) + " volar" : ""), HudNotices.Reward, 2.6f);
            return true;
        }

        /// <summary>Desmonta con una breve animacion (solo en tierra).</summary>
        public void Dismount()
        {
            if (state != MountState.Riding)
                return;
            SetState(MountState.Dismounting);
        }

        /// <summary>Desmonta al instante (muerte, teletransporte, golpe fuerte). En el aire se cae.</summary>
        public void ForceDismount(bool silent)
        {
            if (state == MountState.OnFoot)
                return;
            if (!silent && model != null)
            {
                VfxHost host = VfxHost.Get();
                if (host != null)
                    host.Smoke(FeetPosition() + Vector3.up * 0.5f, 1.2f, new Color(0.55f, 0.52f, 0.56f, 0.5f), 10, 1.1f, 0.8f, Vector3.up * 0.5f);
            }
            FinishDismount();
        }

        private void FinishDismount()
        {
            if (motor != null && ReferenceEquals(motor.LocomotionOverride, this))
                motor.LocomotionOverride = null;
            if (modifiers != null && lockToken != 0)
                modifiers.RemoveModifier(lockToken);
            lockToken = 0;
            EndSeat();
            if (model != null)
            {
                if (gait != null)
                {
                    gait.Flying = false;
                    gait.ForceFlap = false;
                }
                if (modelScales.TryGetValue(model, out Vector3 baseScale))
                    model.transform.localScale = baseScale;
                model.SetActive(false);
            }
            model = null;
            gait = null;
            saddle = null;
            dismountAfterLanding = false;
            IsGalloping = false;
            SetState(MountState.OnFoot);
            ExplorationUi.PlayerMounted = false;
            ExplorationUi.PlayerFlying = false;
        }

        private GameObject GetModel(string id)
        {
            if (models.TryGetValue(id, out GameObject cached) && cached != null)
                return cached;
            GameObject created = MountModelFactory.Create(id, transform, out _);
            created.SetActive(false);
            modelScales[created] = created.transform.localScale;
            if (created.GetComponent<QuadrupedGait>() == null)
                created.AddComponent<QuadrupedGait>();
            models[id] = created;
            return created;
        }

        private void TakeOff()
        {
            SetState(MountState.Flying);
            if (gait != null)
                gait.Flying = true;
            verticalSpeed = 8f;
            nextFloorSample = 0f;
            Vector3 feet = FeetPosition();
            lastFloor = feet.y;
            GameFeel.Dust(feet, Vector3.up, 20, 4f);
            if (!flightTipShown)
            {
                flightTipShown = true;
                HudNotices.Post("Vuelas · " + GameInput.Label(GameAction.Fly) + ": aterrizar · " +
                                GameInput.Label(GameAction.Sprint) + ": picado", HudNotices.Reward, 3f);
            }
        }

        private void BeginLanding()
        {
            if (state != MountState.Flying && state != MountState.Landing)
                return;
            Vector3 position = transform.position;
            WorldAtlas atlas = WorldAtlas.Active;
            if (atlas != null && GroundProbe.TryFlightFloor(position, transform, out float floor) &&
                atlas.WaterDepthAt(new Vector3(position.x, floor, position.z), floor) > 1.3f)
            {
                dismountAfterLanding = false;
                HudNotices.Post("Debajo hay aguas profundas: no puedes aterrizar aquí", HudNotices.Warning);
                return;
            }
            SetState(MountState.Landing);
        }

        private void Touchdown()
        {
            SetState(MountState.Riding);
            if (gait != null)
            {
                gait.Flying = false;
                gait.ForceFlap = false;
            }
            verticalSpeed = -2f;
            GameFeel.Dust(FeetPosition(), Vector3.up, 18, 3f);
            if (dismountAfterLanding)
            {
                dismountAfterLanding = false;
                Dismount();
            }
        }

        private void SetState(MountState next)
        {
            state = next;
            stateStart = Time.time;
        }

        private void AdvanceState()
        {
            float elapsed = Time.time - stateStart;
            if (state == MountState.Summoning && elapsed >= SummonSeconds)
                SetState(MountState.Riding);
            else if (state == MountState.Dismounting && elapsed >= DismountSeconds)
                FinishDismount();
            if (IsMounted && (model == null || !model.activeInHierarchy))
                FinishDismount();
        }

        private void UpdateCamera()
        {
            Camera view = Camera.main;
            if (view != followSource)
            {
                followSource = view;
                followCamera = view != null ? view.GetComponent<SimpleFollowCamera>() : null;
            }
            if (followCamera == null || followCamera.Target != transform)
                return;
            float wanted = IsFlying ? FlightCameraDistance : IsMounted ? GroundCameraDistance : 1f;
            if (!Mathf.Approximately(followCamera.TargetDistanceMultiplier, wanted))
                followCamera.SetDistanceMultiplier(wanted);
        }

        // ── Locomocion (la llama PlayerMotor) ──────────────────────────

        public Vector3 StepVelocity(PlayerMotionCommand command, float deltaTime, out Vector3 facing)
        {
            Vector3 input = command.MoveWorld;
            input.y = 0f;
            if (float.IsNaN(input.x) || float.IsNaN(input.z) || float.IsInfinity(input.x) || float.IsInfinity(input.z))
                input = Vector3.zero;
            input = Vector3.ClampMagnitude(input, 1f);
            IsGalloping = false;
            switch (state)
            {
                case MountState.Riding: GroundStep(input, deltaTime); break;
                case MountState.Flying: FlightStep(input, deltaTime); break;
                case MountState.Landing: LandingStep(input, deltaTime); break;
                default:
                    speed = Mathf.MoveTowards(speed, 0f, 20f * deltaTime);
                    GravityStep(deltaTime);
                    break;
            }
            facing = heading;
            return heading * speed + Vector3.up * verticalSpeed;
        }

        private float SlowFactor() => modifiers != null ? Mathf.Clamp(modifiers.SpeedMultiplier, 0.2f, 1f) : 1f;

        private void GravityStep(float deltaTime)
        {
            if (body != null && body.isGrounded)
                verticalSpeed = -(2f + speed * 0.5f);   // pegado al suelo incluso cuesta abajo
            else
                verticalSpeed = Mathf.Max(verticalSpeed + Gravity * deltaTime, -45f);
        }

        private void GroundStep(Vector3 input, float deltaTime)
        {
            float max = current.GroundSpeed * SlowFactor();
            bool moving = input.sqrMagnitude > 0.04f;
            IsGalloping = moving && GameInput.Held(GameAction.Sprint) && stamina != null &&
                          stamina.Use(GallopDrainPerSecond, deltaTime);
            if (IsGalloping)
                max *= current.GallopMultiplier;
            Turn(input, deltaTime, max, 420f, 130f);
            float align = moving ? Mathf.Clamp01(Vector3.Dot(heading, input.normalized)) : 0f;
            float target = max * input.magnitude * Mathf.Lerp(0.3f, 1f, align);
            speed = Mathf.MoveTowards(speed, target, (target > speed ? 8.5f : 15f) * deltaTime);
            // Contra una pared no se acumula velocidad.
            Vector3 achieved = motor != null ? motor.ActualVelocity : Vector3.zero;
            float achievedPlanar = new Vector2(achieved.x, achieved.z).magnitude;
            if (speed > 4f && achievedPlanar < speed * 0.3f && Time.time - stateStart > 0.3f)
                speed = Mathf.MoveTowards(speed, achievedPlanar, 30f * deltaTime);
            GravityStep(deltaTime);
        }

        private void FlightStep(Vector3 input, float deltaTime)
        {
            float max = current.FlySpeed * SlowFactor();
            bool moving = input.sqrMagnitude > 0.04f;
            IsGalloping = moving && GameInput.Held(GameAction.Sprint) && stamina != null &&
                          stamina.Use(GallopDrainPerSecond, deltaTime);
            if (IsGalloping)
                max *= current.GallopMultiplier;
            Turn(input, deltaTime, max, 260f, 95f);
            float align = moving ? Mathf.Clamp01(Vector3.Dot(heading, input.normalized)) : 0f;
            float target = max * input.magnitude * Mathf.Lerp(0.35f, 1f, align);
            speed = Mathf.MoveTowards(speed, target, (target > speed ? 7f : 5f) * deltaTime);
            KeepInsideWorld();

            Vector3 position = transform.position;
            float feet = position.y - FeetOffset();
            float floor = FlightFloor(position);
            WorldAtlas atlas = WorldAtlas.Active;
            float cruise = atlas != null ? atlas.FlightHeight : 30f;
            float desiredVy = Mathf.Clamp((floor + cruise - feet) * 1.1f, -9f, 14f);
            if (feet < floor + 5f)
                desiredVy = Mathf.Max(desiredVy, 12f);
            verticalSpeed = Mathf.MoveTowards(verticalSpeed, desiredVy, 22f * deltaTime);
            if (gait != null)
                gait.ForceFlap = verticalSpeed > 2f || IsGalloping || speed < target - 4f;
        }

        private void LandingStep(Vector3 input, float deltaTime)
        {
            float max = current.FlySpeed * 0.35f * SlowFactor();
            Turn(input, deltaTime, max, 260f, 140f);
            speed = Mathf.MoveTowards(speed, max * input.magnitude, 9f * deltaTime);
            KeepInsideWorld();
            Vector3 position = transform.position;
            float feet = position.y - FeetOffset();
            float ground = FlightFloor(position);
            float height = feet - ground;
            float desiredVy = -Mathf.Clamp(height * 1.2f, 2.5f, 13f);
            verticalSpeed = Mathf.MoveTowards(verticalSpeed, desiredVy, 20f * deltaTime);
            if (gait != null)
                gait.ForceFlap = height < 4f;
            if ((body != null && body.isGrounded && Time.time - stateStart > 0.2f) || height < 0.3f)
                Touchdown();
        }

        /// <summary>Giro con inercia: rapido al ir despacio, amplio a toda velocidad.</summary>
        private void Turn(Vector3 input, float deltaTime, float maxSpeed, float slowRate, float fastRate)
        {
            if (input.sqrMagnitude < 0.01f)
            {
                yawRate = Mathf.Lerp(yawRate, 0f, 1f - Mathf.Exp(-8f * deltaTime));
                return;
            }
            Vector3 desired = input.normalized;
            float angle = Vector3.SignedAngle(heading, desired, Vector3.up);
            float rate = Mathf.Lerp(slowRate, fastRate, Mathf.Clamp01(speed / Mathf.Max(1f, maxSpeed)));
            if (Mathf.Abs(angle) > 150f && speed < 3f)
                rate = 600f;
            float step = Mathf.Clamp(angle, -rate * deltaTime, rate * deltaTime);
            heading = Quaternion.AngleAxis(step, Vector3.up) * heading;
            heading.y = 0f;
            heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
            yawRate = Mathf.Lerp(yawRate, step / deltaTime, 1f - Mathf.Exp(-8f * deltaTime));
        }

        /// <summary>Suelo para el vuelo: aqui (cada paso) y por delante (10 veces por segundo).</summary>
        private float FlightFloor(Vector3 position)
        {
            float here;
            if (!GroundProbe.TryTerrainHeight(position, out here))
                here = lastFloor;
            if (Time.time >= nextFloorSample)
            {
                nextFloorSample = Time.time + 0.1f;
                Vector3 velocity = heading * speed;
                float best = float.NegativeInfinity;
                bool any = false;
                for (int i = 0; i <= 2; i++)
                {
                    if (GroundProbe.TryFlightFloor(position + velocity * (0.8f * i), transform, out float h))
                    {
                        best = Mathf.Max(best, h);
                        any = true;
                    }
                }
                floorAhead = any ? best : float.NegativeInfinity;
            }
            float floor = Mathf.Max(here, floorAhead);
            if (float.IsNegativeInfinity(floor) || float.IsNaN(floor))
                floor = lastFloor;
            lastFloor = floor;
            return floor;
        }

        private void KeepInsideWorld()
        {
            WorldAtlas atlas = WorldAtlas.Active;
            if (atlas == null)
                return;
            atlas.ResolveBounds(out Vector2 min, out Vector2 max);
            const float margin = 12f;
            Vector3 p = transform.position;
            Vector3 velocity = heading * speed;
            bool clamped = false;
            if (p.x < min.x + margin && velocity.x < 0f) { velocity.x = 0f; clamped = true; }
            if (p.x > max.x - margin && velocity.x > 0f) { velocity.x = 0f; clamped = true; }
            if (p.z < min.y + margin && velocity.z < 0f) { velocity.z = 0f; clamped = true; }
            if (p.z > max.y - margin && velocity.z > 0f) { velocity.z = 0f; clamped = true; }
            if (!clamped)
                return;
            speed = velocity.magnitude;
            if (speed > 0.01f)
                heading = velocity / speed;
            HudNotices.Post("Has llegado al límite del mundo", HudNotices.Warning);
        }

        private float FeetOffset() =>
            body != null ? (body.height * 0.5f - body.center.y) * Mathf.Abs(transform.lossyScale.y) : 1f;

        private Vector3 FeetPosition() => transform.position - Vector3.up * FeetOffset();

        // ── Modelo y jinete ────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!IsMounted || model == null)
                return;
            float dt = Time.deltaTime;
            float elapsed = Time.time - stateStart;
            float grow = state == MountState.Summoning ? Mathf.Clamp01(elapsed / SummonSeconds)
                : state == MountState.Dismounting ? 1f - Mathf.Clamp01(elapsed / DismountSeconds) : 1f;
            float targetPitch = 0f, targetRoll;
            if (IsFlying)
            {
                float climb = motor != null ? motor.ActualVelocity.y : verticalSpeed;
                targetPitch = Mathf.Clamp(-climb * 1.4f, -16f, 16f);
                targetRoll = Mathf.Clamp(-yawRate * 0.12f, -28f, 28f);
            }
            else
                targetRoll = Mathf.Clamp(-yawRate * 0.03f, -7f, 7f);
            float blend = 1f - Mathf.Exp(-5f * dt);
            modelPitch = Mathf.Lerp(modelPitch, targetPitch, blend);
            modelRoll = Mathf.Lerp(modelRoll, targetRoll, blend);
            PlaceModel(grow);
            SeatRider(state == MountState.Summoning ? Mathf.SmoothStep(0f, 1f, grow)
                : state == MountState.Dismounting ? Mathf.SmoothStep(0f, 1f, grow) : 1f);
        }

        private void PlaceModel(float grow01)
        {
            if (model == null)
                return;
            Quaternion rotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(modelPitch, 0f, modelRoll);
            model.transform.SetPositionAndRotation(FeetPosition(), rotation);
            Vector3 baseScale = modelScales.TryGetValue(model, out Vector3 s) ? s : Vector3.one;
            // Aparece creciendo con un pequeño rebote.
            float t = Mathf.Clamp01(grow01);
            float eased = state == MountState.Summoning
                ? 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f)
                : t;
            model.transform.localScale = baseScale * Mathf.Lerp(0.15f, 1f, Mathf.Clamp(eased, 0f, 1.08f));
        }

        private void BeginSeat()
        {
            if (animationDriver != null)
                animationDriver.Seated = true;
            if (facingIndicator != null)
                facingIndicator.enabled = false;
            if (visualRoot == null)
                return;
            visualRestLocal = visualRoot.localPosition;
            visualRestLocalRotation = visualRoot.localRotation;
            FindRiderBones();
            Vector3 hipWorld;
            if (hips != null)
                hipWorld = hips.position;
            else if (upperLeft != null && upperRight != null)
                hipWorld = (upperLeft.position + upperRight.position) * 0.5f;
            else
                hipWorld = visualRoot.position + Vector3.up * -0.1f;
            hipOffset = Quaternion.Inverse(visualRoot.rotation) * (hipWorld - visualRoot.position);
        }

        private void EndSeat()
        {
            if (animationDriver != null)
                animationDriver.Seated = false;
            if (facingIndicator != null)
                facingIndicator.enabled = true;
            if (visualRoot == null)
                return;
            visualRoot.localPosition = visualRestLocal;
            Vector3 facing = heading;
            facing.y = 0f;
            visualRoot.rotation = facing.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(facing.normalized, Vector3.up)
                : transform.rotation * visualRestLocalRotation;
        }

        private void FindRiderBones()
        {
            if (riderBonesSearched || visualRoot == null)
                return;
            riderBonesSearched = true;
            hips = QuadrupedGait.FindDeep(visualRoot, "hips");
            upperLeft = QuadrupedGait.FindDeep(visualRoot, "upperleg.l");
            lowerLeft = QuadrupedGait.FindDeep(visualRoot, "lowerleg.l");
            footLeft = QuadrupedGait.FindDeep(visualRoot, "foot.l");
            upperRight = QuadrupedGait.FindDeep(visualRoot, "upperleg.r");
            lowerRight = QuadrupedGait.FindDeep(visualRoot, "lowerleg.r");
            footRight = QuadrupedGait.FindDeep(visualRoot, "foot.r");
        }

        /// <summary>Coloca al jinete con la cadera sobre la silla y las piernas a horcajadas.</summary>
        private void SeatRider(float seat01)
        {
            if (visualRoot == null || saddle == null || model == null)
                return;
            Quaternion seatRotation = model.transform.rotation;
            Vector3 seatPosition = saddle.position + seatRotation * (Vector3.up * 0.08f) - seatRotation * hipOffset;
            Vector3 restPosition = transform.TransformPoint(visualRestLocal);
            float hop = (state == MountState.Summoning || state == MountState.Dismounting) ? Mathf.Sin(seat01 * Mathf.PI) * 0.45f : 0f;
            visualRoot.position = Vector3.Lerp(restPosition, seatPosition, seat01) + Vector3.up * hop;
            visualRoot.rotation = Quaternion.Slerp(Quaternion.LookRotation(heading, Vector3.up), seatRotation, seat01);
            if (seat01 > 0.5f)
            {
                Vector3 forward = seatRotation * Vector3.forward, right = seatRotation * Vector3.right, up = seatRotation * Vector3.up;
                PoseLeg(upperLeft, lowerLeft, footLeft, -1f, forward, right, up);
                PoseLeg(upperRight, lowerRight, footRight, 1f, forward, right, up);
            }
        }

        private static void PoseLeg(Transform upper, Transform lower, Transform foot, float side,
            Vector3 forward, Vector3 right, Vector3 up)
        {
            if (upper == null || lower == null)
                return;
            Vector3 thigh = (forward * 0.62f + right * (side * 0.5f) - up * 0.6f).normalized;
            Vector3 currentThigh = lower.position - upper.position;
            if (currentThigh.sqrMagnitude < 1e-6f)
                return;
            upper.rotation = Quaternion.FromToRotation(currentThigh.normalized, thigh) * upper.rotation;
            if (foot == null)
                return;
            Vector3 shin = (-up * 0.95f - forward * 0.12f + right * (side * 0.12f)).normalized;
            Vector3 currentShin = foot.position - lower.position;
            if (currentShin.sqrMagnitude < 1e-6f)
                return;
            lower.rotation = Quaternion.FromToRotation(currentShin.normalized, shin) * lower.rotation;
        }

        // ── Rueda de monturas ──────────────────────────────────────────

        private void OpenWheel()
        {
            wheelIds.Clear();
            foreach (string id in MountUnlocks.Unlocked)
                wheelIds.Add(id);
            if (wheelIds.Count == 0)
            {
                HudNotices.Post("Aún no tienes monturas: busca bestias salvajes y dómalas", HudNotices.Warning, 3f);
                mountKeyDownAt = -1f;
                return;
            }
            wheelOpen = true;
            wheelHover = -1;
            string currentId = IsMounted && current != null ? current.Id : MountUnlocks.Last;
            for (int i = 0; i < wheelIds.Count; i++)
                if (wheelIds[i] == currentId)
                    wheelHover = i;
            if (modifiers != null && wheelLock == 0)
                wheelLock = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack);
        }

        private void CloseWheel()
        {
            wheelOpen = false;
            if (modifiers != null && wheelLock != 0)
                modifiers.RemoveModifier(wheelLock);
            wheelLock = 0;
        }

        /// <summary>Posicion de la entrada i de n en la rueda (lienzo virtual), empezando arriba.</summary>
        public static Vector2 WheelSlot(int index, int count, Vector2 center, float radius)
        {
            if (count <= 1)
                return center;
            float angle = (-90f + index * 360f / count) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private void OnGUI()
        {
            GUI.depth = -15;
            if (!wheelOpen || HudMode.Hidden)
                return;
            MenuStyle.Begin();
            float w = MenuStyle.Width, h = MenuStyle.Height;
            Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
            int count = wheelIds.Count;
            float radius = count <= 1 ? 0f : Mathf.Lerp(120f, 175f, Mathf.Clamp01((count - 2) / 6f));

            // Seleccion con el raton: la entrada mas cercana al cursor (o en su direccion).
            Vector2 mouse = Event.current.mousePosition;
            if ((mouse - center).sqrMagnitude > 45f * 45f || count == 1)
            {
                float best = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    float d = (WheelSlot(i, count, center, radius) - mouse).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        wheelHover = i;
                    }
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                float box = radius + 150f;
                HudSkin.SoftPanel(new Rect(center.x - box, center.y - box + 10f, box * 2f, box * 2f - 20f), 0.7f);
                HudSkin.Label(new Rect(0f, center.y - radius - 112f, w, 30f), "ELIGE MONTURA", 22, HudSkin.GoldBright, TextAnchor.MiddleCenter, true);
                HudSkin.FadeLine(new Rect(center.x - 150f, center.y - radius - 80f, 300f, 1.5f), HudSkin.Gold);
                for (int i = 0; i < count; i++)
                {
                    MountDefinition def = MountCatalog.Get(wheelIds[i]);
                    Vector2 slot = WheelSlot(i, count, center, radius);
                    bool hover = i == wheelHover;
                    float d = hover ? 88f : 76f;
                    if (hover)
                        HudSkin.Circle(slot, d + 14f, new Color(HudSkin.GoldBright.r, HudSkin.GoldBright.g, HudSkin.GoldBright.b, 0.85f));
                    HudSkin.Circle(slot, d + 4f, new Color(0f, 0f, 0f, 0.85f));
                    HudSkin.Circle(slot, d, def.BodyColor);
                    HudSkin.Circle(slot + new Vector2(0f, -d * 0.12f), d * 0.55f, new Color(def.AccentColor.r, def.AccentColor.g, def.AccentColor.b, 0.55f));
                    string initial = string.IsNullOrEmpty(def.Name) ? "?" : def.Name.Substring(0, 1).ToUpperInvariant();
                    HudSkin.Label(new Rect(slot.x - 40f, slot.y - 18f, 80f, 36f), initial, hover ? 32 : 26, Color.white, TextAnchor.MiddleCenter, true);
                    HudSkin.Label(new Rect(slot.x - 110f, slot.y + d * 0.5f + 6f, 220f, 20f), def.Name, hover ? 16 : 14,
                        hover ? HudSkin.GoldBright : HudSkin.Text, TextAnchor.MiddleCenter, hover);
                    HudSkin.Label(new Rect(slot.x - 110f, slot.y + d * 0.5f + 26f, 220f, 16f),
                        def.Flies ? "Vuela · " + Mathf.RoundToInt(def.FlySpeed) + " m/s" : "Terrestre · " + Mathf.RoundToInt(def.GroundSpeed) + " m/s",
                        12, def.Flies ? new Color(0.6f, 0.85f, 1f) : HudSkin.TextDim);
                }
                if (wheelHover >= 0 && wheelHover < count)
                {
                    MountDefinition chosen = MountCatalog.Get(wheelIds[wheelHover]);
                    HudSkin.Label(new Rect(center.x - 300f, center.y + radius + 82f, 600f, 20f), chosen.Description, 14, HudSkin.TextDim);
                }
                HudSkin.Label(new Rect(0f, center.y + radius + 108f, w, 20f),
                    "Suelta " + GameInput.Label(GameAction.Mount) + " para montar la elegida", 14, HudSkin.Text);
            }
            MenuStyle.End();
        }
    }
}
