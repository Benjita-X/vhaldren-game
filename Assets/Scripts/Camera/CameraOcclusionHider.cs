using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Survival;

namespace Vhaldren.Gameplay.CameraSystem
{
    /// <summary>
    /// Oculta los arboles, muros, pilares y rocas que tapan al personaje desde la camara y
    /// los restaura cuando dejan de taparlo. Lo oculto pasa a <c>ShadowsOnly</c>: no se ve,
    /// pero sigue proyectando sombra. Nunca toca el terreno, los enemigos ni los personajes,
    /// ni efectos creados durante la partida: solo el escenario que habia al empezar y las
    /// piezas que construye el jugador.
    /// Se comprueba cada <see cref="checkInterval"/> s la linea camara-pecho del personaje
    /// (con un margen, como un SphereCast) contra la caja de cada renderer: asi tambien se
    /// detectan las copas de los arboles, que no tienen collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraOcclusionHider : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Altura sobre el pivote del objetivo a la que se comprueba la vista (pecho).")]
        [SerializeField] private float targetHeight = 0.5f;
        [Tooltip("Margen alrededor de la linea de vista, en metros.")]
        [SerializeField, Min(0f)] private float radius = 0.35f;
        [SerializeField, Min(0.02f)] private float checkInterval = 0.1f;
        [Tooltip("Segundos que sigue oculto tras dejar de tapar: evita parpadeos en los bordes.")]
        [SerializeField, Min(0f)] private float restoreDelay = 0.25f;

        private sealed class Occluder
        {
            public GameObject Root;
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public ShadowCastingMode[] Original;
            public bool Hidden;
            public float HiddenUntil;
        }

        private readonly List<Occluder> occluders = new List<Occluder>();
        private readonly Dictionary<GameObject, Occluder> byRoot = new Dictionary<GameObject, Occluder>();
        private float nextCheck;
        private int knownBuiltPieces = -1;
        private bool scanned;

        public Transform Target => target;
        /// <summary>Objetos ocultos tras la ultima comprobacion.</summary>
        public int HiddenCount { get; private set; }
        /// <summary>Objetos de escenario que pueden ocultarse.</summary>
        public int OccluderCount => occluders.Count;

        public float RestoreDelay
        {
            get => restoreDelay;
            set => restoreDelay = float.IsNaN(value) || float.IsInfinity(value) ? 0.25f : Mathf.Max(0f, value);
        }

        public void SetTarget(Transform value)
        {
            if (value == target)
                return;
            RestoreAll();
            target = value;
            scanned = false;
        }

        /// <summary>Verdadero si el renderer esta oculto ahora mismo por tapar al objetivo.</summary>
        public bool IsHidden(Renderer renderer)
        {
            if (renderer == null)
                return false;
            foreach (Occluder occluder in occluders)
                if (occluder.Hidden && occluder.Renderers.Contains(renderer))
                    return true;
            return false;
        }

        private void OnDisable() => RestoreAll();

        private void LateUpdate()
        {
            if (target == null || Time.time < nextCheck)
                return;
            nextCheck = Time.time + Mathf.Max(0.02f, checkInterval);
            Refresh();
        }

        /// <summary>Comprueba ahora que objetos tapan al objetivo (las pruebas lo llaman directamente).</summary>
        public void Refresh()
        {
            if (target == null)
            {
                RestoreAll();
                return;
            }
            if (!scanned)
            {
                Scan();
                scanned = true;
            }
            RegisterNewBuiltPieces();

            Vector3 from = transform.position;
            Vector3 to = target.position + Vector3.up * targetHeight;
            Vector3 segment = to - from;
            float length = segment.magnitude;
            if (float.IsNaN(length) || float.IsInfinity(length) || length < 0.01f)
                return;

            Ray sight = new Ray(from, segment / length);
            float now = Time.time;
            int hidden = 0;
            for (int i = occluders.Count - 1; i >= 0; i--)
            {
                Occluder occluder = occluders[i];
                if (occluder.Root == null)
                {
                    occluders.RemoveAt(i);
                    continue;
                }
                if (Blocks(occluder, sight, length))
                {
                    occluder.HiddenUntil = now + restoreDelay;
                    if (!occluder.Hidden)
                        Hide(occluder);
                }
                else if (occluder.Hidden && now >= occluder.HiddenUntil)
                {
                    Restore(occluder);
                }
                if (occluder.Hidden)
                    hidden++;
            }
            HiddenCount = hidden;
        }

        private bool Blocks(Occluder occluder, Ray sight, float length)
        {
            foreach (Renderer renderer in occluder.Renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                Bounds bounds = renderer.bounds;
                bounds.Expand(radius * 2f);
                if (bounds.Contains(sight.origin))
                    return true;
                if (bounds.IntersectRay(sight, out float distance) && distance <= length)
                    return true;
            }
            return false;
        }

        private void Hide(Occluder occluder)
        {
            occluder.Original = new ShadowCastingMode[occluder.Renderers.Count];
            for (int i = 0; i < occluder.Renderers.Count; i++)
            {
                Renderer renderer = occluder.Renderers[i];
                if (renderer == null)
                    continue;
                occluder.Original[i] = renderer.shadowCastingMode;
                // Sin sombra original: ocultarlo del todo, sin crear una sombra nueva.
                if (renderer.shadowCastingMode == ShadowCastingMode.Off)
                    renderer.forceRenderingOff = true;
                else
                    renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            occluder.Hidden = true;
        }

        private static void Restore(Occluder occluder)
        {
            for (int i = 0; i < occluder.Renderers.Count; i++)
            {
                Renderer renderer = occluder.Renderers[i];
                if (renderer == null || occluder.Original == null || i >= occluder.Original.Length)
                    continue;
                if (occluder.Original[i] == ShadowCastingMode.Off)
                    renderer.forceRenderingOff = false;
                else
                    renderer.shadowCastingMode = occluder.Original[i];
            }
            occluder.Hidden = false;
        }

        private void RestoreAll()
        {
            foreach (Occluder occluder in occluders)
                if (occluder.Hidden)
                    Restore(occluder);
            HiddenCount = 0;
        }

        /// <summary>Escenario presente en la escena del objetivo (no efectos posteriores).</summary>
        private void Scan()
        {
            RestoreAll();
            occluders.Clear();
            byRoot.Clear();
            knownBuiltPieces = -1;
            if (target == null || !target.gameObject.scene.IsValid())
                return;
            foreach (GameObject root in target.gameObject.scene.GetRootGameObjects())
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    Consider(renderer);
        }

        private void RegisterNewBuiltPieces()
        {
            IReadOnlyList<BuiltPiece> pieces = BuiltPiece.All;
            if (pieces.Count == knownBuiltPieces)
                return;
            knownBuiltPieces = pieces.Count;
            for (int i = 0; i < pieces.Count; i++)
            {
                BuiltPiece piece = pieces[i];
                if (piece == null || byRoot.ContainsKey(piece.gameObject))
                    continue;
                foreach (MeshRenderer renderer in piece.GetComponentsInChildren<MeshRenderer>(true))
                    Consider(renderer);
            }
        }

        private void Consider(Renderer renderer)
        {
            if (renderer == null)
                return;
            Transform node = renderer.transform;
            if (target != null && (node == target || node.IsChildOf(target)))
                return;
            // Ni enemigos, ni el jugador, ni dianas, ni personajes animados.
            if (renderer.GetComponentInParent<CombatHealth>(true) != null ||
                renderer.GetComponentInParent<CharacterController>(true) != null ||
                renderer.GetComponentInParent<Animator>(true) != null)
                return;
            // Suelos y losas grandes y planas no tapan a nadie visto desde arriba.
            Bounds bounds = renderer.bounds;
            if (bounds.size.y < 0.6f && Mathf.Max(bounds.size.x, bounds.size.z) > 25f)
                return;

            GameObject root = OccluderRoot(node);
            if (!byRoot.TryGetValue(root, out Occluder occluder))
            {
                occluder = new Occluder { Root = root };
                byRoot.Add(root, occluder);
                occluders.Add(occluder);
            }
            if (!occluder.Renderers.Contains(renderer))
                occluder.Renderers.Add(renderer);
        }

        /// <summary>
        /// Objeto completo al que pertenece un renderer, para ocultar tronco y copa juntos:
        /// el nodo recolectable o la pieza construida; si no, el antecesor con collider mas
        /// alto antes de un contenedor de escena; si no hay collider, un padre pequeño.
        /// </summary>
        private static GameObject OccluderRoot(Transform node)
        {
            HarvestNode harvest = node.GetComponentInParent<HarvestNode>(true);
            if (harvest != null)
                return harvest.gameObject;
            BuiltPiece piece = node.GetComponentInParent<BuiltPiece>(true);
            if (piece != null)
                return piece.gameObject;

            GameObject withCollider = null;
            for (Transform current = node; current != null; current = current.parent)
            {
                if (current != node && current.childCount > 8)
                    break;
                Collider body = current.GetComponent<Collider>();
                if (body != null && !body.isTrigger && !(body is TerrainCollider))
                    withCollider = current.gameObject;
            }
            if (withCollider != null)
                return withCollider;

            Transform parent = node.parent;
            return parent != null && parent.childCount <= 4 ? parent.gameObject : node.gameObject;
        }
    }
}
