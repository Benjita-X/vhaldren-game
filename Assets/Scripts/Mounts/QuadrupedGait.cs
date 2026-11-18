using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vhaldren.Gameplay.Mounts
{
    /// <summary>
    /// Animacion procedural de cuadrupedos y bestias aladas, reutilizable en cualquier
    /// modelo con partes con nombre (se buscan en toda la jerarquia, todas opcionales):
    /// cuerpo, cabeza, pata_del_izq, pata_del_der, pata_tras_izq, pata_tras_der, cola,
    /// ala_izq y ala_der. El componente va en la raiz del modelo, que debe mirar hacia +Z
    /// con +Y arriba. La velocidad sale del CharacterController del padre si lo hay o del
    /// movimiento del propio Transform (o de <see cref="SpeedOverride"/>).
    /// Reposo (respira, mira alrededor, pasta), paso, trote y galope con las patas en
    /// diagonal, cabeceo y cola; con alas: plegadas en tierra, aleteo al despegar o subir
    /// y planeo. <see cref="TriggerAttack"/> lanza un mordisco o un zarpazo breve.
    /// Solo gira y desplaza un poco las partes respecto a su pose original; al desactivarse
    /// la restaura.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuadrupedGait : MonoBehaviour
    {
        public enum AttackStyle { Auto, Bite, Claw }
        public enum GaitKind { Idle, Walk, Trot, Gallop, Fly }

        public static readonly string[] PartNames =
        {
            "cuerpo", "cabeza", "pata_del_izq", "pata_del_der", "pata_tras_izq", "pata_tras_der",
            "cola", "ala_izq", "ala_der"
        };

        private struct Part
        {
            public Transform T;
            public Quaternion RestRotation;
            public Vector3 RestPosition;
            public Vector3 Right, Up, Forward;   // ejes del modelo expresados en el espacio del padre
            public float Side;                   // -1 izquierda, +1 derecha
            public bool Valid => T != null;
        }

        [Tooltip("Escala de todas las amplitudes (0,5 discreto, 1,5 exagerado).")]
        [SerializeField, Range(0.2f, 2f)] private float amplitude = 1f;
        [Tooltip("Velocidad fija en m/s. Negativa = medirla del movimiento.")]
        [SerializeField] private float speedOverride = -1f;
        [Tooltip("En vuelo: patas recogidas y alas desplegadas.")]
        [SerializeField] private bool flying;

        private Part body, head, frontLeft, frontRight, backLeft, backRight, tail, wingLeft, wingRight;
        private bool bound;
        private CharacterController controller;
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private float speed, verticalSpeed;
        private float phase;
        private float offFL, offFR = 0.5f, offBL = 0.58f, offBR = 0.08f;
        private float legLocal = 0.6f;
        private float moveWeight, trotWeight, gallopWeight, flyWeight, foldWeight = 1f;
        private float flapPhase, flapWeight, flapUntil, nextFlapBurst, takeoffFlapUntil;
        private float stretchUntil, nextStretch;
        private float attackStart = -10f, attackDuration = 0.45f;
        private AttackStyle attackStyle = AttackStyle.Bite;
        private float idleTime, grazeWeight, grazeUntil, nextGraze;
        private float seed;

        /// <summary>Velocidad horizontal medida (m/s), suavizada.</summary>
        public float Speed => speed;
        public float VerticalSpeed => verticalSpeed;
        public bool HasWings => wingLeft.Valid || wingRight.Valid;
        public bool IsAttacking => Time.time - attackStart < attackDuration;
        /// <summary>Partes encontradas (0 = modelo sin nombres reconocibles: no anima nada).</summary>
        public int PartCount { get; private set; }

        public float Amplitude
        {
            get => amplitude;
            set => amplitude = Mathf.Clamp(float.IsNaN(value) ? 1f : value, 0.2f, 2f);
        }

        /// <summary>Velocidad fija en m/s; negativa vuelve a medirla del movimiento.</summary>
        public float SpeedOverride
        {
            get => speedOverride;
            set => speedOverride = float.IsNaN(value) ? -1f : value;
        }

        /// <summary>En vuelo: al pasar a verdadero aletea con fuerza un par de segundos (despegue).</summary>
        public bool Flying
        {
            get => flying;
            set
            {
                if (value && !flying)
                    takeoffFlapUntil = Time.time + 2.2f;
                flying = value;
            }
        }

        /// <summary>Obliga a aletear mientras sea verdadero (subidas, aceleracion).</summary>
        public bool ForceFlap { get; set; }

        public GaitKind CurrentGait
        {
            get
            {
                if (flyWeight > 0.5f) return GaitKind.Fly;
                if (moveWeight < 0.3f) return GaitKind.Idle;
                if (gallopWeight > 0.5f) return GaitKind.Gallop;
                return trotWeight > 0.5f ? GaitKind.Trot : GaitKind.Walk;
            }
        }

        private void Awake()
        {
            seed = UnityEngine.Random.value * 100f;
            nextGraze = Time.time + UnityEngine.Random.Range(4f, 9f);
            nextStretch = Time.time + UnityEngine.Random.Range(8f, 16f);
            if (!bound)
                Rebind();
        }

        private void OnEnable() => hasLastPosition = false;

        private void OnDisable() => RestorePose();

        /// <summary>Busca otra vez las partes y toma su pose actual como reposo.</summary>
        public void Rebind()
        {
            RestorePose();
            body = Bind("cuerpo", 0f);
            head = Bind("cabeza", 0f);
            frontLeft = Bind("pata_del_izq", -1f);
            frontRight = Bind("pata_del_der", 1f);
            backLeft = Bind("pata_tras_izq", -1f);
            backRight = Bind("pata_tras_der", 1f);
            tail = Bind("cola", 0f);
            wingLeft = Bind("ala_izq", -1f);
            wingRight = Bind("ala_der", 1f);
            controller = GetComponentInParent<CharacterController>();
            PartCount = Count(body) + Count(head) + Count(frontLeft) + Count(frontRight) + Count(backLeft) +
                        Count(backRight) + Count(tail) + Count(wingLeft) + Count(wingRight);

            // Largo de pata: altura media de las caderas en el espacio del modelo.
            float sum = 0f;
            int legs = 0;
            AddLegHeight(frontLeft, ref sum, ref legs);
            AddLegHeight(frontRight, ref sum, ref legs);
            AddLegHeight(backLeft, ref sum, ref legs);
            AddLegHeight(backRight, ref sum, ref legs);
            if (legs > 0)
                legLocal = Mathf.Max(0.1f, sum / legs);
            else if (body.Valid)
                legLocal = Mathf.Max(0.1f, transform.InverseTransformPoint(body.T.position).y * 0.85f);
            else
                legLocal = 0.6f;
            bound = true;
        }

        private static int Count(Part p) => p.Valid ? 1 : 0;

        private void AddLegHeight(Part leg, ref float sum, ref int count)
        {
            if (!leg.Valid)
                return;
            float h = transform.InverseTransformPoint(leg.T.position).y;
            if (h > 0.02f)
            {
                sum += h;
                count++;
            }
        }

        private Part Bind(string partName, float defaultSide)
        {
            Part part = default;
            Transform t = FindDeep(transform, partName);
            if (t == null || t == transform || t.parent == null)
                return part;
            Transform parent = t.parent;
            part.T = t;
            part.RestRotation = t.localRotation;
            part.RestPosition = t.localPosition;
            part.Right = SafeAxis(parent.InverseTransformDirection(transform.right), Vector3.right);
            part.Up = SafeAxis(parent.InverseTransformDirection(transform.up), Vector3.up);
            part.Forward = SafeAxis(parent.InverseTransformDirection(transform.forward), Vector3.forward);
            float x = transform.InverseTransformPoint(t.position).x;
            part.Side = Mathf.Abs(x) > 0.001f ? Mathf.Sign(x) : (defaultSide != 0f ? defaultSide : 1f);
            return part;
        }

        private static Vector3 SafeAxis(Vector3 axis, Vector3 fallback) =>
            axis.sqrMagnitude > 1e-6f && !float.IsNaN(axis.x) ? axis.normalized : fallback;

        /// <summary>
        /// Busca un hijo por nombre en toda la jerarquia, primero lo menos profundo. Admite
        /// mayusculas distintas y sufijos de Blender ("cuerpo.001").
        /// </summary>
        public static Transform FindDeep(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;
            Queue<Transform> pending = new Queue<Transform>();
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                Transform current = pending.Dequeue();
                if (current != root && NameMatches(current.name, childName))
                    return current;
                for (int i = 0; i < current.childCount; i++)
                    pending.Enqueue(current.GetChild(i));
            }
            return null;
        }

        private static bool NameMatches(string actual, string wanted) =>
            string.Equals(actual, wanted, StringComparison.OrdinalIgnoreCase) ||
            (actual.Length > wanted.Length && actual.StartsWith(wanted, StringComparison.OrdinalIgnoreCase) &&
             (actual[wanted.Length] == '.' || actual[wanted.Length] == '_' && char.IsDigit(actual[actual.Length - 1])));

        /// <summary>
        /// Mordisco (la cabeza se lanza hacia delante) o zarpazo (se encabrita y golpea con la
        /// pata delantera). Auto elige al azar. Dura <paramref name="duration"/> s.
        /// </summary>
        public void TriggerAttack(AttackStyle style = AttackStyle.Auto, float duration = 0.45f)
        {
            attackStyle = style == AttackStyle.Auto
                ? (UnityEngine.Random.value < 0.5f ? AttackStyle.Bite : AttackStyle.Claw)
                : style;
            attackDuration = Mathf.Clamp(float.IsNaN(duration) ? 0.45f : duration, 0.2f, 1.5f);
            attackStart = Time.time;
        }

        /// <summary>Atajo sin parametros (para llamarlo desde eventos o mensajes).</summary>
        public void TriggerAttack() => TriggerAttack(AttackStyle.Auto, 0.45f);

        /// <summary>Curva del ataque: breve preparacion hacia atras, golpe y recuperacion.</summary>
        public static float AttackCurve(float t01)
        {
            if (t01 <= 0f || t01 >= 1f)
                return 0f;
            if (t01 < 0.3f)
                return -0.35f * (t01 / 0.3f);
            if (t01 < 0.55f)
                return Mathf.Lerp(-0.35f, 1f, (t01 - 0.3f) / 0.25f);
            return Mathf.Lerp(1f, 0f, (t01 - 0.55f) / 0.45f);
        }

        private void MeasureSpeed(float deltaTime)
        {
            Vector3 velocity = Vector3.zero;
            bool measured = false;
            if (controller != null && controller.enabled && controller.gameObject.activeInHierarchy)
            {
                velocity = controller.velocity;
                measured = true;
            }
            Vector3 position = transform.position;
            if (!measured && hasLastPosition)
            {
                velocity = (position - lastPosition) / deltaTime;
                measured = true;
            }
            lastPosition = position;
            hasLastPosition = true;
            if (!measured || float.IsNaN(velocity.x) || velocity.sqrMagnitude > 3600f)
                velocity = Vector3.zero;   // teletransportes: no cuentan como carrera
            float planar = new Vector2(velocity.x, velocity.z).magnitude;
            if (speedOverride >= 0f)
                planar = speedOverride;
            float blend = 1f - Mathf.Exp(-10f * deltaTime);
            speed = Mathf.Lerp(speed, planar, blend);
            verticalSpeed = Mathf.Lerp(verticalSpeed, velocity.y, blend);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || PartCount == 0)
                return;
            MeasureSpeed(dt);
            float t = Time.time;
            const float tau = Mathf.PI * 2f;

            // Tamaño: un lobo trota antes que un caballo.
            float legWorld = Mathf.Max(0.12f, legLocal * Mathf.Abs(transform.lossyScale.y));
            float sizeK = Mathf.Clamp(legWorld / 0.8f, 0.35f, 3f);
            float walkMax = 2.6f * sizeK, trotMax = 7f * sizeK;
            bool airborne = flying;
            moveWeight = Mathf.MoveTowards(moveWeight, airborne ? 0f : Mathf.InverseLerp(0.12f * sizeK, 0.8f * sizeK, speed), 3f * dt);
            trotWeight = Mathf.MoveTowards(trotWeight, Mathf.InverseLerp(walkMax * 0.75f, walkMax * 1.25f, speed), 2f * dt);
            gallopWeight = Mathf.MoveTowards(gallopWeight, Mathf.InverseLerp(trotMax * 0.85f, trotMax * 1.2f, speed), 2f * dt);
            flyWeight = Mathf.MoveTowards(flyWeight, airborne ? 1f : 0f, 1.8f * dt);

            float stride = legWorld * Mathf.Lerp(Mathf.Lerp(1.7f, 2.4f, trotWeight), 3.6f, gallopWeight);
            float frequency = speed > 0.05f ? Mathf.Clamp(speed / stride, 0.7f, 3.3f) : 0.7f;
            phase = Mathf.Repeat(phase + frequency * dt, 1000f);

            // Desfase de cada pata: paso (diagonal con algo de retraso), trote (diagonal) y galope.
            float tw = trotWeight, gw = gallopWeight;
            offFL = Mathf.MoveTowards(offFL, Mathf.Lerp(Mathf.Lerp(0f, 0f, tw), 0.52f, gw), 1.5f * dt);
            offBR = Mathf.MoveTowards(offBR, Mathf.Lerp(Mathf.Lerp(0.08f, 0f, tw), 0.1f, gw), 1.5f * dt);
            offFR = Mathf.MoveTowards(offFR, Mathf.Lerp(Mathf.Lerp(0.5f, 0.5f, tw), 0.42f, gw), 1.5f * dt);
            offBL = Mathf.MoveTowards(offBL, Mathf.Lerp(Mathf.Lerp(0.58f, 0.5f, tw), 0f, gw), 1.5f * dt);

            if (moveWeight < 0.05f && !airborne)
                idleTime += dt;
            else
                idleTime = 0f;

            float attackK = IsAttacking ? AttackCurve((t - attackStart) / attackDuration) : 0f;
            float strike = Mathf.Max(0f, attackK);
            bool bite = attackStyle == AttackStyle.Bite;

            // ── Patas ──
            float legAmp = Mathf.Lerp(Mathf.Lerp(20f, 30f, tw), 42f, gw) * amplitude * moveWeight;
            float fl = Leg(offFL, legAmp, 55f), fr = Leg(offFR, legAmp, 55f);
            float bl = Leg(offBL, legAmp, 65f), br = Leg(offBR, legAmp, 65f);
            if (!bite && attackK != 0f)
            {
                fl += -75f * strike + 20f * Mathf.Min(0f, attackK);
                fr += -25f * strike;
            }
            Rotate(frontLeft, fl, 0f, 0f);
            Rotate(frontRight, fr, 0f, 0f);
            Rotate(backLeft, bl, 0f, 0f);
            Rotate(backRight, br, 0f, 0f);

            // ── Alas ──
            float flapStroke = 0f;
            if (HasWings)
            {
                if (airborne && t > nextFlapBurst)
                {
                    flapUntil = t + UnityEngine.Random.Range(1f, 1.8f);
                    nextFlapBurst = t + UnityEngine.Random.Range(3.5f, 7f);
                }
                if (!airborne && t > nextStretch && idleTime > 3f)
                {
                    stretchUntil = t + 1.6f;
                    nextStretch = t + UnityEngine.Random.Range(9f, 18f);
                }
                bool flap = airborne && (verticalSpeed > 0.8f || ForceFlap || t < takeoffFlapUntil || t < flapUntil);
                flapWeight = Mathf.MoveTowards(flapWeight, flap ? 1f : 0f, 3f * dt);
                flapPhase = Mathf.Repeat(flapPhase + dt * Mathf.Lerp(1.1f, 2.5f, flapWeight), 1000f);
                bool spread = airborne || t < stretchUntil;
                foldWeight = Mathf.MoveTowards(foldWeight, spread ? 0f : 1f, 1.8f * dt);
                flapStroke = Mathf.Sin(tau * flapPhase);
                float flapAngle = Mathf.Lerp(-28f, 58f, 0.5f + 0.5f * flapStroke);
                float glideAngle = 6f + Mathf.Sin(tau * t * 0.45f + seed) * 4f;
                float lift = Mathf.Lerp(glideAngle, flapAngle, flapWeight) * Mathf.Lerp(1f, 0.6f, 1f - flyWeight);
                float up = Mathf.Lerp(lift, 18f + Mathf.Sin(tau * t * 0.3f) * 2f, foldWeight);
                float sweep = 72f * foldWeight;
                RotateWing(wingLeft, up, sweep);
                RotateWing(wingRight, up, sweep);
            }

            // ── Cuerpo ──
            float bobWalk = Mathf.Sin(tau * phase * 2f) * 0.018f;
            float bobTrot = Mathf.Abs(Mathf.Sin(tau * phase)) * 0.04f;
            float bobGallop = Mathf.Sin(tau * phase) * 0.07f;
            float bob = Mathf.Lerp(Mathf.Lerp(bobWalk, bobTrot, tw), bobGallop, gw) * moveWeight;
            bob += Mathf.Sin(tau * t * 0.33f + seed) * 0.012f * (1f - moveWeight) * (1f - flyWeight);
            bob -= flapStroke * 0.05f * flapWeight * flyWeight;
            float bodyPitch = Mathf.Sin(tau * phase + 1.2f) * 5f * gw * moveWeight;
            float bodyRoll = Mathf.Sin(tau * phase) * 2f * (1f - tw) * moveWeight;
            if (attackK != 0f)
                bodyPitch += bite ? 6f * attackK : -12f * strike;
            Rotate(body, bodyPitch * amplitude, 0f, bodyRoll * amplitude);
            Offset(body, Vector3.up * (bob * legLocal * amplitude));

            // ── Cabeza ──
            if (head.Valid)
            {
                float idle = (1f - moveWeight) * (1f - flyWeight);
                if (idleTime > 5f && t > nextGraze)
                {
                    grazeUntil = t + 2.5f;
                    nextGraze = t + UnityEngine.Random.Range(7f, 14f);
                }
                grazeWeight = Mathf.MoveTowards(grazeWeight, t < grazeUntil && idleTime > 0f ? 1f : 0f, 1.2f * dt);
                float nod = Mathf.Sin(tau * phase * (gw > 0.5f ? 1f : 2f) + Mathf.PI * 0.5f) * Mathf.Lerp(4f, 7f, gw) * moveWeight;
                float lookYaw = (Mathf.PerlinNoise(t * 0.23f, seed) - 0.5f) * 70f * idle * (1f - grazeWeight);
                float lookPitch = (Mathf.PerlinNoise(seed, t * 0.19f) - 0.5f) * 18f * idle;
                float pitch = nod + lookPitch + 38f * grazeWeight - 6f * gw - 8f * flyWeight;
                Vector3 thrust = Vector3.zero;
                if (attackK != 0f)
                {
                    pitch += bite ? 30f * attackK : -10f * attackK;
                    if (bite)
                        thrust = Vector3.forward * (0.2f * legLocal * strike);
                }
                Rotate(head, pitch * amplitude, lookYaw, 0f);
                Offset(head, thrust);
            }

            // ── Cola ──
            if (tail.Valid)
            {
                float swayFrequency = Mathf.Lerp(0.55f, 1.6f, moveWeight);
                float yaw = Mathf.Sin(tau * t * swayFrequency + seed) * Mathf.Lerp(16f, 7f, gw) * amplitude;
                float raise = (14f * gw + Mathf.Sin(tau * phase) * 6f * gw) * moveWeight + 5f * flyWeight;
                Rotate(tail, raise * amplitude, yaw, 0f);
            }
        }

        /// <summary>Angulo de una pata: oscila con la marcha y se recoge en vuelo.</summary>
        private float Leg(float offset, float amplitudeDegrees, float tuck) =>
            Mathf.Lerp(Mathf.Sin(Mathf.PI * 2f * (phase + offset)) * amplitudeDegrees, tuck, flyWeight);

        private static void Rotate(Part p, float pitch, float yaw, float roll)
        {
            if (!p.Valid)
                return;
            p.T.localRotation = Quaternion.AngleAxis(yaw, p.Up) * Quaternion.AngleAxis(pitch, p.Right) *
                                Quaternion.AngleAxis(roll, p.Forward) * p.RestRotation;
        }

        /// <summary>Ala: <paramref name="up"/> la levanta (eje adelante) y <paramref name="sweep"/> la pliega hacia atras.</summary>
        private static void RotateWing(Part p, float up, float sweep)
        {
            if (!p.Valid)
                return;
            p.T.localRotation = Quaternion.AngleAxis(p.Side * up, p.Forward) *
                                Quaternion.AngleAxis(p.Side * sweep, p.Up) * p.RestRotation;
        }

        /// <summary>Desplaza una parte respecto a su reposo (desplazamiento en el espacio del modelo).</summary>
        private void Offset(Part p, Vector3 modelOffset)
        {
            if (!p.Valid)
                return;
            Vector3 world = transform.TransformVector(modelOffset);
            p.T.localPosition = p.RestPosition + p.T.parent.InverseTransformVector(world);
        }

        private void RestorePose()
        {
            Restore(body); Restore(head); Restore(frontLeft); Restore(frontRight);
            Restore(backLeft); Restore(backRight); Restore(tail); Restore(wingLeft); Restore(wingRight);
        }

        private static void Restore(Part p)
        {
            if (!p.Valid)
                return;
            p.T.localRotation = p.RestRotation;
            p.T.localPosition = p.RestPosition;
        }
    }
}
