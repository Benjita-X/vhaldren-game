using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Construccion modular por piezas (suelo, pared, puerta, rampa, hoguera) sobre una
    /// rejilla de 3 m, con vista previa sin colision, coste visible y motivo textual cuando
    /// no se puede colocar. Diseno propio para Vhaldren; no reproduce codigo de otros juegos.
    /// Local y no autoritativo: en cooperativo el servidor debera validar la colocacion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalBuilder : MonoBehaviour
    {
        public const float Grid = 3f;
        private const float Reach = 8f;
        private const float Shrink = 0.06f;
        private static readonly Collider[] OverlapHits = new Collider[32];
        private static readonly RaycastHit[] RayHits = new RaycastHit[16];

        private enum Snap { Cell, Edge, Free }

        private struct Box
        {
            public Vector3 Center;
            public Vector3 Size;
            public float PitchX;
            public bool Stone;
            public bool Solid;
        }

        private struct Piece
        {
            public string Name;
            public int Wood;
            public int Stone;
            public Snap Snap;
            public bool Campfire;
            public Box[] Boxes;
        }

        private static readonly Piece[] Pieces =
        {
            new Piece { Name = "Suelo", Wood = 4, Snap = Snap.Cell, Boxes = new[]
            {
                new Box { Center = new Vector3(0f, 0.125f, 0f), Size = new Vector3(3f, 0.25f, 3f), Solid = true }
            } },
            new Piece { Name = "Pared", Wood = 6, Stone = 2, Snap = Snap.Edge, Boxes = new[]
            {
                new Box { Center = new Vector3(0f, 1.2f, 0f), Size = new Vector3(3f, 2.4f, 0.25f), Solid = true }
            } },
            new Piece { Name = "Puerta", Wood = 5, Stone = 1, Snap = Snap.Edge, Boxes = new[]
            {
                new Box { Center = new Vector3(-1.05f, 1.2f, 0f), Size = new Vector3(0.9f, 2.4f, 0.25f), Solid = true },
                new Box { Center = new Vector3(1.05f, 1.2f, 0f), Size = new Vector3(0.9f, 2.4f, 0.25f), Solid = true },
                new Box { Center = new Vector3(0f, 2.15f, 0f), Size = new Vector3(1.2f, 0.5f, 0.25f), Solid = true }
            } },
            // Sube 2,4 m (la altura de una pared) en 3 m: 38,7 grados, transitable.
            new Piece { Name = "Rampa", Wood = 6, Snap = Snap.Cell, Boxes = new[]
            {
                new Box { Center = new Vector3(0f, 1.2f, 0f), Size = new Vector3(2.6f, 0.2f, 3.84f), PitchX = -38.66f, Solid = true }
            } },
            new Piece { Name = "Hoguera", Wood = 3, Stone = 3, Snap = Snap.Free, Campfire = true, Boxes = new[]
            {
                new Box { Center = new Vector3(0f, 0.15f, 0f), Size = new Vector3(1.2f, 0.3f, 1.2f), Stone = true, Solid = true },
                new Box { Center = new Vector3(0f, 0.38f, 0f), Size = new Vector3(0.9f, 0.16f, 0.16f), Solid = false },
                new Box { Center = new Vector3(0f, 0.38f, 0f), Size = new Vector3(0.16f, 0.16f, 0.9f), Solid = false }
            } },
        };

        private static Material woodMaterial;
        private static Material stoneMaterial;

        private SurvivalInventory inventory;
        private CombatHealth health;
        private Transform preview;
        private Renderer[] previewRenderers;
        private int previewPiece = -1;
        private int current;
        private float angle;
        private bool valid;
        private string reason = "";
        private Vector3 placePosition;
        private Quaternion placeRotation;

        public bool IsBuilding { get; private set; }
        public int CurrentPieceIndex => current;
        public string CurrentPieceName => Pieces[current].Name;
        public static int PieceCount => Pieces.Length;

        private void Awake()
        {
            inventory = GetComponent<SurvivalInventory>();
            health = GetComponent<CombatHealth>();
        }

        private void Update()
        {
            if (inventory == null)
                inventory = GetComponent<SurvivalInventory>();
            if (SurvivalInventory.Pressed(KeyCode.B) && (inventory == null || !inventory.IsOpen))
                IsBuilding = !IsBuilding;
            if (SurvivalInventory.Pressed(KeyCode.Escape))
                IsBuilding = false;
            if (health != null && health.IsDefeated)
                IsBuilding = false;
            if (!IsBuilding || inventory == null || inventory.ConsumesInput)
            {
                HidePreview();
                return;
            }

            for (int i = 0; i < Pieces.Length; i++)
                if (SurvivalInventory.Pressed((KeyCode)((int)KeyCode.Alpha1 + i)))
                    current = i;
            if (SurvivalInventory.Pressed(KeyCode.R))
                angle = (angle + 90f) % 360f;

            EvaluatePlacement();
            ShowPreview();

            bool overUi = SurvivalInventory.Pointer().y < 110f;
            if (!overUi && SurvivalInventory.LeftPressed())
                TryPlace();
            else if (!overUi && SurvivalInventory.RightPressed())
                TryDismantle();
        }

        private void EvaluatePlacement()
        {
            Piece piece = Pieces[current];
            Vector3 aim = inventory.Aim();
            placeRotation = Quaternion.Euler(0f, angle, 0f);
            reason = "";
            valid = true;

            // Superficie: el primer suelo (terreno o pieza construida) bajo el cursor.
            if (!TryFindSurface(aim, out Vector3 surface, out Vector3 normal, out Collider ground))
            {
                placePosition = aim;
                Fail("Sin suelo");
                return;
            }

            Vector3 p = surface;
            if (piece.Snap == Snap.Cell)
            {
                p.x = Mathf.Round(p.x / Grid) * Grid;
                p.z = Mathf.Round(p.z / Grid) * Grid;
            }
            else if (piece.Snap == Snap.Edge)
            {
                // Paredes en los bordes de las celdas: encajan con los suelos.
                bool alongX = Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(angle, 180f)) < 1f;
                if (alongX)
                {
                    p.x = Mathf.Round(p.x / Grid) * Grid;
                    p.z = Mathf.Round((p.z - Grid * 0.5f) / Grid) * Grid + Grid * 0.5f;
                }
                else
                {
                    p.z = Mathf.Round(p.z / Grid) * Grid;
                    p.x = Mathf.Round((p.x - Grid * 0.5f) / Grid) * Grid + Grid * 0.5f;
                }
                // El borde puede estar entre un suelo construido y el terreno: la pared se
                // apoya en la superficie mas alta de ambos lados para no atravesar el suelo.
                Vector3 side = placeRotation * Vector3.forward * 0.6f;
                float bestY = float.NegativeInfinity;
                foreach (Vector3 sample in new[] { p + side, p - side })
                {
                    if (TryFindSurface(new Vector3(sample.x, surface.y, sample.z), out Vector3 s2,
                        out Vector3 n2, out Collider g2) && s2.y > bestY)
                    {
                        bestY = s2.y;
                        normal = n2;
                        ground = g2;
                    }
                }
                if (bestY > float.NegativeInfinity)
                    p.y = bestY;
            }
            placePosition = p;

            Vector3 flat = p - transform.position;
            flat.y = 0f;
            if (flat.magnitude > Reach)
                Fail("Demasiado lejos");
            if (normal.y < 0.7f)
                Fail("Pendiente excesiva");
            if (!inventory.CanAfford(piece.Wood, piece.Stone))
                Fail("Faltan materiales");

            // Volumen libre: cajas algo reducidas para permitir piezas adyacentes.
            foreach (Box box in piece.Boxes)
            {
                if (!box.Solid)
                    continue;
                Quaternion rot = placeRotation * Quaternion.Euler(box.PitchX, 0f, 0f);
                Vector3 center = p + placeRotation * box.Center;
                Vector3 half = new Vector3(Mathf.Max(0.01f, box.Size.x * 0.5f - Shrink),
                    Mathf.Max(0.01f, box.Size.y * 0.5f - Shrink), Mathf.Max(0.01f, box.Size.z * 0.5f - Shrink));
                int count = Physics.OverlapBoxNonAlloc(center, half, OverlapHits, rot,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider c = OverlapHits[i];
                    if (c == null || c == ground)
                        continue;
                    Fail(c.transform.IsChildOf(transform) ? "Estas dentro" :
                        c.GetComponentInParent<CombatHealth>() != null ? "Hay alguien ahi" : "Ocupado");
                    break;
                }
            }

            // No construir al otro lado de una pared.
            Vector3 eye = transform.position + Vector3.up * 0.6f;
            Vector3 target = p + Vector3.up * 0.4f;
            Vector3 line = target - eye;
            int hits = Physics.RaycastNonAlloc(eye, line.normalized, RayHits, line.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Collider c = RayHits[i].collider;
                if (c == null || c == ground || c.transform.IsChildOf(transform) ||
                    c.GetComponentInParent<CombatHealth>() != null)
                    continue;
                Fail("Sin linea directa");
                break;
            }
        }

        private void Fail(string why)
        {
            if (valid)
                reason = why;
            valid = false;
        }

        private bool TryFindSurface(Vector3 aim, out Vector3 point, out Vector3 normal, out Collider ground)
        {
            point = aim;
            normal = Vector3.up;
            ground = null;
            int count = Physics.RaycastNonAlloc(aim + Vector3.up * 3f, Vector3.down, RayHits, 8f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = RayHits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) ||
                    c.GetComponentInParent<CombatHealth>() != null ||
                    c.GetComponent<SurvivalResource>() != null)
                    continue;
                if (RayHits[i].distance < best)
                {
                    best = RayHits[i].distance;
                    point = RayHits[i].point;
                    normal = RayHits[i].normal;
                    ground = c;
                }
            }
            return ground != null;
        }

        private void TryPlace()
        {
            Piece piece = Pieces[current];
            if (!valid)
            {
                inventory.ShowMessage("No se puede: " + reason);
                return;
            }
            if (!inventory.Spend(piece.Wood, piece.Stone))
            {
                inventory.ShowMessage("Faltan materiales");
                return;
            }
            BuildPiece(current, placePosition, placeRotation);
            SpellImpactFlash.Spawn(placePosition + Vector3.up * 0.5f, new Color(0.9f, 0.75f, 0.45f), 0.6f, 0.3f);
            // Construir hace ruido: los enemigos cercanos vienen a mirar.
            PrototypeEnemy.EmitNoise(placePosition, 10f, 0.45f);
            inventory.ShowMessage(piece.Name + " construida");
        }

        private void TryDismantle()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Ray ray = cam.ScreenPointToRay(SurvivalInventory.Pointer());
            int count = Physics.RaycastNonAlloc(ray, RayHits, 100f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            BuiltPiece target = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = RayHits[i].collider;
                if (c == null || c.transform.IsChildOf(transform))
                    continue;
                if (RayHits[i].distance < best)
                {
                    best = RayHits[i].distance;
                    target = c.GetComponentInParent<BuiltPiece>();
                }
            }
            if (target == null)
            {
                inventory.ShowMessage("Apunta a una pieza tuya para desmontarla");
                return;
            }
            Vector3 flat = target.transform.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude > Reach)
            {
                inventory.ShowMessage("Demasiado lejos para desmontar");
                return;
            }
            int wood = inventory.Add(SurvivalInventory.Item.Wood, target.WoodCost);
            int stone = inventory.Add(SurvivalInventory.Item.Stone, target.StoneCost);
            inventory.ShowMessage("Desmontado: +" + wood + " madera, +" + stone + " piedra");
            SpellImpactFlash.Spawn(target.transform.position + Vector3.up * 0.5f, Color.gray, 0.5f, 0.25f);
            Destroy(target.gameObject);
        }

        /// <summary>Crea la pieza real con colisiones. Publico para pruebas.</summary>
        public static GameObject BuildPiece(int index, Vector3 position, Quaternion rotation)
        {
            index = Mathf.Clamp(index, 0, Pieces.Length - 1);
            Piece piece = Pieces[index];
            GameObject root = new GameObject("Construido - " + piece.Name);
            root.transform.SetPositionAndRotation(position, rotation);
            foreach (Box box in piece.Boxes)
            {
                GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = piece.Name + " (parte)";
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = box.Center;
                part.transform.localRotation = Quaternion.Euler(box.PitchX, 0f, 0f);
                part.transform.localScale = box.Size;
                part.GetComponent<Renderer>().sharedMaterial = box.Stone ? StoneMaterial : WoodMaterial;
                if (!box.Solid)
                    Object.DestroyImmediate(part.GetComponent<Collider>());
            }
            if (piece.Campfire)
            {
                GameObject flame = PrototypeVfx.CreateShape(PrimitiveType.Sphere, "Fuego", root.transform,
                    position + rotation * new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 0.8f, 0.5f),
                    new Color(1f, 0.55f, 0.1f, 0.85f));
                flame.AddComponent<CampfireFlicker>();
                Light light = PrototypeVfx.CreatePointLight(root.transform, new Color(1f, 0.6f, 0.25f), 9f, 2.5f);
                light.transform.localPosition = Vector3.up * 1.1f;
                light.shadows = LightShadows.Soft;
            }
            root.AddComponent<BuiltPiece>().Initialize(index, piece.Wood, piece.Stone, piece.Campfire);
            return root;
        }

        private void ShowPreview()
        {
            if (previewPiece != current || preview == null)
                RebuildPreview();
            preview.gameObject.SetActive(true);
            preview.SetPositionAndRotation(placePosition, placeRotation);
            Color c = valid ? new Color(0.2f, 1f, 0.35f, 0.35f) : new Color(1f, 0.15f, 0.1f, 0.35f);
            foreach (Renderer r in previewRenderers)
                PrototypeVfx.SetColor(r, c);
        }

        private void RebuildPreview()
        {
            if (preview != null)
                Destroy(preview.gameObject);
            previewPiece = current;
            preview = new GameObject("Vista previa - " + Pieces[current].Name).transform;
            Box[] boxes = Pieces[current].Boxes;
            previewRenderers = new Renderer[boxes.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                // CreateShape quita el collider: la vista previa nunca se obstruye a si misma.
                GameObject part = PrototypeVfx.CreateShape(PrimitiveType.Cube, "Fantasma", preview,
                    Vector3.zero, boxes[i].Size, Color.green);
                part.transform.localPosition = boxes[i].Center;
                part.transform.localRotation = Quaternion.Euler(boxes[i].PitchX, 0f, 0f);
                previewRenderers[i] = part.GetComponent<Renderer>();
            }
        }

        private void HidePreview()
        {
            if (preview != null)
                preview.gameObject.SetActive(false);
        }

        private void OnGUI()
        {
            if (!IsBuilding)
                return;
            float width = Mathf.Min(Screen.width - 16f, 720f);
            float left = (Screen.width - width) / 2f;
            float cell = width / Pieces.Length;
            float bottom = Screen.height - 64f;
            for (int i = 0; i < Pieces.Length; i++)
            {
                Piece piece = Pieces[i];
                Color old = GUI.backgroundColor;
                bool affordable = inventory != null && inventory.CanAfford(piece.Wood, piece.Stone);
                GUI.backgroundColor = i == current ? Color.yellow : affordable ? Color.white : new Color(1f, 0.5f, 0.5f);
                string cost = piece.Wood + " mad" + (piece.Stone > 0 ? " + " + piece.Stone + " pie" : "");
                if (GUI.Button(new Rect(left + i * cell, bottom, cell - 3f, 55f), (i + 1) + "  " + piece.Name + "\n" + cost))
                    current = i;
                GUI.backgroundColor = old;
            }
            int wood = inventory != null ? inventory.Count(SurvivalInventory.Item.Wood) : 0;
            int stone = inventory != null ? inventory.Count(SurvivalInventory.Item.Stone) : 0;
            string state = valid ? "Se puede colocar" : "No se puede: " + reason;
            GUI.Label(new Rect(left, bottom - 22f, width, 22f),
                "CONSTRUIR · " + state + "   |   Madera " + wood + " · Piedra " + stone +
                "   |   clic izq colocar · clic der desmontar · R girar · 1-5 pieza · B/Esc salir");
        }

        private void OnDisable()
        {
            IsBuilding = false;
            HidePreview();
        }

        private void OnDestroy()
        {
            if (preview != null)
                Destroy(preview.gameObject);
        }

        private static Material WoodMaterial => woodMaterial != null ? woodMaterial
            : woodMaterial = CreateLit("Construccion madera", new Color(0.45f, 0.3f, 0.18f));

        private static Material StoneMaterial => stoneMaterial != null ? stoneMaterial
            : stoneMaterial = CreateLit("Construccion piedra", new Color(0.38f, 0.38f, 0.42f));

        private static Material CreateLit(string name, Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", 0.1f);
            return material;
        }
    }
}
