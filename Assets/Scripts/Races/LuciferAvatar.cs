using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Gameplay.Races
{
    /// <summary>
    /// Aspecto de Lucifer, el Lucero caído: tinte oscuro y dorado sobre el personaje,
    /// un halo roto sobre la cabeza y ALAS en la espalda animadas por código.
    /// Las alas salen del prefab <c>Resources/Lucifer/alas_lucifer</c> (hijos <c>ala_izq</c>
    /// y <c>ala_der</c> con el origen en la raíz del ala, modeladas abiertas en cruz: la
    /// izquierda hacia -X y la derecha hacia +X). Si no existe, se construyen con
    /// primitivas: nunca falla. Reposo plegado con respiración; se abren y baten al usar
    /// habilidades de alas o al esquivar (aleteo). Solo visual: no cambia el combate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LuciferAvatar : MonoBehaviour
    {
        public const string WingPrefabPath = "Lucifer/alas_lucifer";
        public const string LeftWingName = "ala_izq";
        public const string RightWingName = "ala_der";
        /// <summary>Huesos de la espalda donde se enganchan las alas (KayKit usa "chest").</summary>
        public static readonly string[] AnchorBones =
            { "chest", "Chest", "spine", "Spine", "spine.002", "Spine2", "mixamorig:Spine2", "mixamorig:Spine1" };

        public const float FoldedSweep = 78f;
        public const float FoldedDroop = 30f;
        public const float OpenSweep = 6f;
        public const float OpenDroop = -16f;
        public const float FlapDegrees = 38f;
        public const float FlapHertz = 3.4f;

        public static readonly Color BodyTint = new Color(0.5f, 0.42f, 0.47f);
        public static readonly Color CapeTint = new Color(1f, 0.72f, 0.3f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.36f);
        public static readonly Color Hellfire = new Color(1f, 0.38f, 0.08f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Mesh cubeMesh;
        private static Material featherMaterial;
        private static Material goldMaterial;

        private Transform yawSource;
        private Transform anchor;
        private Transform head;
        private Transform rig;
        private Transform left;
        private Transform right;
        private Quaternion leftRest = Quaternion.identity;
        private Quaternion rightRest = Quaternion.identity;
        private float rigTilt;
        private float rigScale = 1f;
        private CharacterController body;
        private PlayerMotor motor;
        private PlayerDamageTracker tracker;
        private bool built;
        private bool authored;

        private float openness;
        private float openUntil;
        private float openTarget = 1f;
        private float flapUntil;
        private float flapStrength;
        private float flapPhase;
        private bool wasDashing;
        private float glowBoost;
        private float restored = 0.2f;
        private float nextEmber;
        private float nextRestoreCheck;

        private LineRenderer halo;
        private Light glow;
        private readonly List<Renderer> tinted = new List<Renderer>();
        private readonly List<Color> tintRestore = new List<Color>();
        private readonly List<Transform> brokenFeathers = new List<Transform>();
        private MaterialPropertyBlock block;

        public bool Built => built;
        public bool UsingAuthoredWings => authored;
        public Transform Anchor => anchor;
        public Transform LeftWing => left;
        public Transform RightWing => right;
        public float Openness => openness;
        public bool IsFlapping => Time.time < flapUntil;
        /// <summary>Poder recuperado (0-1): las alas rotas se recomponen y brillan más.</summary>
        public float Restored => restored;
        public int Flaps { get; private set; }

        public static LuciferAvatar For(Component owner) =>
            owner != null ? owner.GetComponentInParent<LuciferAvatar>() : null;

        // ── Cálculo puro (probado en EditMode) ─────────────────────────

        /// <summary>
        /// Ángulos del ala: <paramref name="sweep"/> (hacia atrás, grados) y
        /// <paramref name="droop"/> (hacia abajo; negativo = levantada).
        /// </summary>
        public static void WingAngles(float open01, float phase, float flap01, float breath,
            out float sweep, out float droop)
        {
            float o = Mathf.Clamp01(open01);
            float f = Mathf.Clamp01(flap01);
            sweep = Mathf.Lerp(FoldedSweep, OpenSweep, o) + Mathf.Sin(breath + 0.5f) * 2f * (1f - o) - f * 8f;
            droop = Mathf.Lerp(FoldedDroop, OpenDroop, o) + Mathf.Sin(breath) * 3f * (1f - o) +
                    f * FlapDegrees * Mathf.Sin(phase);
        }

        /// <summary>Giro local de un ala: primero baja o sube (eje Z) y luego barre hacia atrás (eje Y).</summary>
        public static Quaternion WingPose(bool leftSide, float sweep, float droop)
        {
            float side = leftSide ? 1f : -1f;
            return Quaternion.Euler(0f, -side * sweep, side * droop);
        }

        /// <summary>Poder recuperado según el daño total causado (empieza con las alas rotas).</summary>
        public static float RestoredFraction(float totalDamage) =>
            Mathf.Clamp01(0.2f + Mathf.Max(0f, totalDamage) / 4000f);

        // ── API para el kit ────────────────────────────────────────────

        /// <summary>Abre las alas durante unos segundos (1 = totalmente desplegadas).</summary>
        public void Spread(float seconds, float amount = 1f)
        {
            float a = Mathf.Clamp01(amount);
            openTarget = Time.time < openUntil ? Mathf.Max(openTarget, a) : a;
            openUntil = Mathf.Max(openUntil, Time.time + Mathf.Max(0f, seconds));
        }

        /// <summary>Bate las alas con una fuerza (0-1) durante unos segundos.</summary>
        public void Flap(float strength, float seconds)
        {
            flapStrength = Time.time < flapUntil ? Mathf.Max(flapStrength, Mathf.Clamp01(strength)) : Mathf.Clamp01(strength);
            flapUntil = Mathf.Max(flapUntil, Time.time + Mathf.Max(0f, seconds));
            Flaps++;
        }

        /// <summary>Destello dorado del halo y la luz del pecho (habilidades).</summary>
        public void Flare(float amount) => glowBoost = Mathf.Max(glowBoost, Mathf.Clamp(amount, 0f, 3f));

        /// <summary>Punto aproximado de la espalda (origen de plumas y brasas).</summary>
        public Vector3 BackPoint => rig != null ? rig.position : transform.position + Vector3.up * 1.2f;

        // ── Ciclo de vida ──────────────────────────────────────────────

        private void Start() => Build();

        private void OnDestroy()
        {
            RestoreTint();
            if (rig != null)
                Destroy(rig.gameObject);
            if (halo != null)
                Destroy(halo.gameObject);
            if (glow != null)
                Destroy(glow.gameObject);
        }

        /// <summary>Construye alas, tinte, halo y luz. Seguro de llamar varias veces.</summary>
        public void Build()
        {
            if (built)
                return;
            built = true;
            body = GetComponent<CharacterController>();
            motor = GetComponent<PlayerMotor>();
            tracker = GetComponent<PlayerDamageTracker>();
            Animator animator = GetComponentInChildren<Animator>(true);
            Transform model = animator != null ? animator.transform : null;
            yawSource = FindYawSource(model);
            anchor = model != null ? FindAnyBone(model, AnchorBones) : null;
            head = model != null ? FindAnyBone(model, new[] { "head", "Head", "mixamorig:Head" }) : null;
            rigScale = body != null ? Mathf.Clamp(body.height / 2f, 0.6f, 1.6f) : 1f;

            rig = new GameObject("Alas de Lucifer — sin colision").transform;
            rig.SetParent(yawSource, false);

            GameObject prefab = Resources.Load<GameObject>(WingPrefabPath);
            if (prefab != null)
            {
                GameObject wings = Instantiate(prefab, rig, false);
                wings.name = "alas_lucifer";
                left = FindDeep(wings.transform, LeftWingName);
                right = FindDeep(wings.transform, RightWingName);
                if (left != null && right != null)
                {
                    authored = true;
                    foreach (Collider c in wings.GetComponentsInChildren<Collider>(true))
                        Destroy(c);
                }
                else
                {
                    Destroy(wings);
                    left = right = null;
                }
            }
            if (!authored)
                BuildPrimitiveWings();
            rigTilt = authored ? 0f : 35f;
            if (left != null)
                leftRest = left.localRotation;
            if (right != null)
                rightRest = right.localRotation;

            ApplyTint(model != null ? model : transform);
            BuildHalo();
            BuildGlow();
            Place();
        }

        private Transform FindYawSource(Transform model)
        {
            // El motor gira el hijo visual, no la raíz: se busca el hijo directo que contiene el modelo.
            if (model == null || model == transform)
                return transform;
            Transform current = model;
            while (current.parent != null && current.parent != transform)
                current = current.parent;
            return current.parent == transform ? current : transform;
        }

        private void LateUpdate()
        {
            if (!built || rig == null)
                return;
            float dt = Time.deltaTime;

            // Esquiva = aleteo: misma mecánica de PlayerDash, solo se escucha su estado.
            bool dashing = motor != null && motor.IsDashing;
            if (dashing && !wasDashing)
                OnDodge();
            wasDashing = dashing;

            if (Time.time >= nextRestoreCheck)
            {
                nextRestoreCheck = Time.time + 1f;
                if (tracker == null)
                    tracker = GetComponent<PlayerDamageTracker>();
                restored = RestoredFraction(tracker != null ? tracker.TotalDamage : 0f);
                for (int i = 0; i < brokenFeathers.Count; i++)
                    if (brokenFeathers[i] != null)
                        brokenFeathers[i].localScale = new Vector3(1f, Mathf.Lerp(0.3f, 1f, restored), 1f);
            }

            float targetOpen = Time.time < openUntil ? openTarget : 0f;
            openness = Mathf.MoveTowards(openness, targetOpen, dt * (targetOpen > openness ? 6f : 2.2f));
            float flap = Time.time < flapUntil ? flapStrength : 0f;
            flapPhase += dt * Mathf.PI * 2f * FlapHertz * (0.6f + 0.6f * flap);
            float breath = Time.time * 1.7f;

            Place();
            WingAngles(openness, flapPhase, flap, breath, out float sweep, out float droop);
            if (left != null)
                left.localRotation = leftRest * WingPose(true, sweep, droop);
            if (right != null)
                right.localRotation = rightRest * WingPose(false, sweep, droop + Mathf.Sin(flapPhase * 0.5f) * 1.5f);

            UpdateHaloAndGlow(dt);
            EmitEmbers(flap);
        }

        private void Place()
        {
            if (rig == null)
                return;
            Vector3 forward = yawSource != null ? yawSource.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            Quaternion facing = Quaternion.LookRotation(forward.normalized, Vector3.up);
            Vector3 basePoint;
            if (anchor != null)
                basePoint = anchor.position;
            else if (body != null && body.enabled)
                basePoint = new Vector3(transform.position.x, body.bounds.min.y + body.height * 0.72f, transform.position.z);
            else
                basePoint = transform.position + Vector3.up * 0.5f * rigScale;
            rig.position = basePoint + facing * new Vector3(0f, 0.05f, -0.2f) * rigScale;
            rig.rotation = facing * Quaternion.Euler(rigTilt, 0f, 0f);
            Vector3 parentScale = rig.parent != null ? rig.parent.lossyScale : Vector3.one;
            rig.localScale = new Vector3(rigScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                rigScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                rigScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }

        private void OnDodge()
        {
            Flap(1f, 0.38f);
            Spread(0.4f, 0.85f);
            Flare(1.2f);
            VfxHost host = VfxHost.Get();
            if (host != null)
            {
                Vector3 back = BackPoint;
                host.Glints(back, 0.5f, new Color(1f, 0.86f, 0.5f, 1f), 14, 0.14f, 0.55f, Vector3.up * 0.6f);
                host.Smoke(back, 0.35f, new Color(0.08f, 0.06f, 0.07f, 0.55f), 8, 0.35f, 0.6f, Vector3.up * 0.4f);
                host.Trail(VfxElement.Fire, back, Vector3.zero, Hellfire, 10, 0.12f, 0.5f, 0.4f, 1.2f);
            }
            if (GameFeel.Enabled)
                GameAudio.Play(GameSound.Whoosh, transform.position, 0.7f, 0.1f);
        }

        private void EmitEmbers(float flap)
        {
            if (Time.time < nextEmber || (left == null && right == null))
                return;
            nextEmber = Time.time + (flap > 0f ? 0.06f : 0.35f);
            VfxHost host = VfxHost.Get();
            if (host == null)
                return;
            Transform wing = Random.value < 0.5f ? left : right;
            if (wing == null)
                return;
            // Brasas que se desprenden de las plumas: más cuanto más poder ha recuperado.
            Vector3 tip = wing.TransformPoint(new Vector3(wing == left ? -0.8f : 0.8f, -0.3f, 0f));
            int count = flap > 0f ? 3 : (Random.value < restored ? 1 : 0);
            if (count > 0)
                host.Trail(VfxElement.Fire, tip, Vector3.up * 0.4f, Hellfire, count, 0.08f, 0.6f, 0.15f, 0.5f);
        }

        // ── Tinte oscuro y dorado ──────────────────────────────────────

        private void ApplyTint(Transform model)
        {
            if (block == null)
                block = new MaterialPropertyBlock();
            foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r == null || (rig != null && r.transform.IsChildOf(rig)))
                    continue;
                r.GetPropertyBlock(block);
                Color previous = block.HasColor(ColorId) ? block.GetColor(ColorId) :
                    r.sharedMaterial != null && r.sharedMaterial.HasProperty(ColorId)
                        ? r.sharedMaterial.GetColor(ColorId) : Color.white;
                string n = r.name.ToLowerInvariant();
                Color tint = n.Contains("cape") || n.Contains("capa") ? CapeTint : BodyTint;
                block.SetColor(ColorId, new Color(previous.r * tint.r, previous.g * tint.g, previous.b * tint.b, previous.a));
                r.SetPropertyBlock(block);
                tinted.Add(r);
                tintRestore.Add(previous);
            }
        }

        private void RestoreTint()
        {
            if (block == null)
                return;
            for (int i = 0; i < tinted.Count; i++)
            {
                if (tinted[i] == null)
                    continue;
                tinted[i].GetPropertyBlock(block);
                block.SetColor(ColorId, tintRestore[i]);
                tinted[i].SetPropertyBlock(block);
            }
            tinted.Clear();
            tintRestore.Clear();
        }

        // ── Halo roto y luz del pecho ──────────────────────────────────

        private void BuildHalo()
        {
            Material beam = VfxLibrary.AdditiveBeam;
            if (beam == null)
                return;
            GameObject go = new GameObject("Halo roto — sin colision");
            go.transform.SetParent(transform, false);
            halo = go.AddComponent<LineRenderer>();
            halo.sharedMaterial = beam;
            halo.useWorldSpace = false;
            halo.loop = false;
            const int points = 28;
            halo.positionCount = points;
            // Arco de 290°: el hueco es la corona rota del caído.
            for (int i = 0; i < points; i++)
            {
                float a = Mathf.Lerp(0f, 290f, i / (points - 1f)) * Mathf.Deg2Rad;
                halo.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.22f, 0f, Mathf.Sin(a) * 0.22f));
            }
            halo.widthMultiplier = 0.035f;
            halo.numCapVertices = 2;
            halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            halo.receiveShadows = false;
        }

        private void BuildGlow()
        {
            GameObject go = new GameObject("Luz del Lucero");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.4f;
            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.68f, 0.32f);
            glow.range = 3.2f;
            glow.intensity = 0.45f;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.Auto;
        }

        private void UpdateHaloAndGlow(float dt)
        {
            glowBoost = Mathf.MoveTowards(glowBoost, 0f, dt * 2.5f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
            if (halo != null)
            {
                Vector3 top = head != null ? head.position + Vector3.up * 0.38f * rigScale :
                    body != null && body.enabled ? new Vector3(transform.position.x, body.bounds.max.y + 0.2f, transform.position.z)
                        : transform.position + Vector3.up * 1.2f;
                halo.transform.position = top;
                halo.transform.rotation = Quaternion.Euler(8f * Mathf.Sin(Time.time * 0.7f), Time.time * 25f, 0f);
                float alpha = Mathf.Clamp01(0.35f + 0.45f * restored + 0.2f * pulse + 0.4f * glowBoost);
                Color c = Color.Lerp(Gold, Color.white, 0.25f * glowBoost);
                halo.startColor = halo.endColor = new Color(c.r, c.g, c.b, alpha);
                halo.widthMultiplier = (0.03f + 0.012f * glowBoost) * rigScale;
            }
            if (glow != null)
                glow.intensity = 0.35f + 0.25f * restored + 0.1f * pulse + 1.2f * glowBoost;
        }

        // ── Alas de sustituto con primitivas ───────────────────────────

        private void BuildPrimitiveWings()
        {
            EnsureAssets();
            Transform root = new GameObject("alas_sustituto").transform;
            root.SetParent(rig, false);
            left = BuildWing(root, true);
            right = BuildWing(root, false);
        }

        private Transform BuildWing(Transform parent, bool leftSide)
        {
            float side = leftSide ? -1f : 1f;
            Transform wing = new GameObject(leftSide ? LeftWingName : RightWingName).transform;
            wing.SetParent(parent, false);
            wing.localPosition = new Vector3(side * 0.1f, 0f, 0f);

            Vector3 elbow = new Vector3(side * 0.5f, 0.2f, 0f);
            Vector3 tip = new Vector3(side * 1.05f, 0.06f, 0f);
            Segment(wing, Vector3.zero, elbow, 0.075f, goldMaterial);
            Segment(wing, elbow, tip, 0.055f, goldMaterial);

            const int feathers = 10;
            for (int i = 0; i < feathers; i++)
            {
                float t = i / (feathers - 1f);
                Vector3 at = t < 0.5f ? Vector3.Lerp(Vector3.zero, elbow, t * 2f) : Vector3.Lerp(elbow, tip, (t - 0.5f) * 2f);
                float length = Mathf.Lerp(0.36f, 0.95f, t);
                float angle = Mathf.Lerp(10f, 72f, t);
                Transform pivot = new GameObject("Pluma " + i).transform;
                pivot.SetParent(wing, false);
                pivot.localPosition = at + new Vector3(0f, 0f, 0.012f * (i % 2));
                pivot.localRotation = Quaternion.Euler(0f, 0f, side * angle);
                Cube(pivot, new Vector3(0f, -length * 0.5f, 0f), new Vector3(0.12f, length, 0.022f), featherMaterial);
                if (i >= 7)
                    Cube(pivot, new Vector3(0f, -length, 0f), new Vector3(0.13f, 0.07f, 0.026f), goldMaterial);
                // El ala derecha está rota: le faltan plumas que vuelven al recuperar poder.
                if (!leftSide && (i == 2 || i == 5 || i == 8))
                {
                    pivot.localScale = new Vector3(1f, 0.3f, 1f);
                    brokenFeathers.Add(pivot);
                }
            }
            return wing;
        }

        private static void Segment(Transform parent, Vector3 from, Vector3 to, float thickness, Material material)
        {
            Vector3 delta = to - from;
            Transform piece = Cube(parent, (from + to) * 0.5f, new Vector3(delta.magnitude, thickness, thickness), material);
            if (piece != null && delta.sqrMagnitude > 0.0001f)
                piece.localRotation = Quaternion.FromToRotation(Vector3.right, delta.normalized);
        }

        private static Transform Cube(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            if (cubeMesh == null)
                return null;
            GameObject go = new GameObject("Pieza");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go.transform;
        }

        private static void EnsureAssets()
        {
            if (cubeMesh == null)
            {
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubeMesh = probe.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(probe);
            }
            if (featherMaterial == null)
                featherMaterial = MakeMaterial("Plumas de Lucifer", new Color(0.09f, 0.07f, 0.09f),
                    new Color(0.12f, 0.03f, 0.01f), 0.35f);
            if (goldMaterial == null)
                goldMaterial = MakeMaterial("Oro del Lucero", new Color(0.85f, 0.64f, 0.28f),
                    new Color(0.75f, 0.42f, 0.08f), 0.7f);
        }

        private static Material MakeMaterial(string name, Color color, Color emission, float smoothness)
        {
            // La plantilla de Resources garantiza que la variante con emisión entra en el ejecutable.
            Material template = Resources.Load<Material>("VariantesShader/StandardEmision");
            Material material;
            if (template != null)
                material = new Material(template);
            else
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                    shader = Shader.Find("Legacy Shaders/Diffuse");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    return null;
                material = new Material(shader);
            }
            material.name = name;
            material.color = color;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", emission);
            }
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        // ── Utilidades ─────────────────────────────────────────────────

        private static Transform FindAnyBone(Transform root, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = FindDeep(root, names[i]);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static Transform FindDeep(Transform root, string target)
        {
            if (root.name == target)
                return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, target);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
