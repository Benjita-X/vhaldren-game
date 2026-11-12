using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Inventario local de supervivencia: 36 casillas, de las que 0..8 SON la barra rapida
    /// (no hay almacenamiento duplicado). Incluye fabricacion simple y el uso del objeto
    /// equipado con clic izquierdo. Diseno propio inspirado en el genero; no es codigo de
    /// Minecraft ni de The Forest. No autoritativo para red.
    /// </summary>
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class SurvivalInventory : MonoBehaviour
    {
        // El orden importa: todo lo que va hasta Wand es herramienta no apilable.
        public enum Item { Empty, Sword, Pickaxe, Axe, Wand, Wood, Stone, Food }
        [System.Serializable] public struct Slot { public Item item; public int count; }

        public struct Recipe
        {
            public Item Result;
            public int Amount;
            public int Wood;
            public int Stone;
        }

        public static readonly Recipe[] Recipes =
        {
            new Recipe { Result = Item.Pickaxe, Amount = 1, Wood = 4, Stone = 0 },
            new Recipe { Result = Item.Axe, Amount = 1, Wood = 3, Stone = 0 },
            new Recipe { Result = Item.Sword, Amount = 1, Wood = 2, Stone = 4 },
        };

        private const int SlotCount = 36;
        private const float HotbarHeight = 70f;
        private static readonly Collider[] UseHits = new Collider[24];

        [SerializeField, Min(0.5f)] private float toolReach = 2.2f;
        [SerializeField, Min(0f)] private float swordDamage = 28f;
        [SerializeField, Min(0f)] private float swordNoise = 6f;
        [SerializeField] private bool generousStartKit = true;

        private readonly Slot[] slots = new Slot[SlotCount];
        private int selected, pending = -1;
        private float useReady;
        private int uiFrame = -1;
        private Transform heldVisual;
        private Item shownItem = Item.Empty;
        private string message = "";
        private float messageUntil;
        private float swingUntil;
        private SurvivalBuilder builder;
        private SurvivalNeeds needs;
        private CombatHealth health;

        public bool ConsumesInput => IsOpen || uiFrame == Time.frameCount;
        public bool IsOpen { get; private set; }
        public Item Equipped => slots[selected].item;
        public int SelectedIndex => selected;
        public Slot GetSlot(int index) => index >= 0 && index < SlotCount ? slots[index] : default;

        public static bool BlocksInput(GameObject player) =>
            player.TryGetComponent<SurvivalInventory>(out var bag) && bag.ConsumesInput;

        public static int Limit(Item item) =>
            item == Item.Empty ? 0 : (int)item <= (int)Item.Wand ? 1 : 64;

        public static string DisplayName(Item item)
        {
            switch (item)
            {
                case Item.Sword: return "Espada";
                case Item.Pickaxe: return "Pico";
                case Item.Axe: return "Hacha";
                case Item.Wand: return "Varita";
                case Item.Wood: return "Madera";
                case Item.Stone: return "Piedra";
                case Item.Food: return "Bayas";
                default: return "Manos";
            }
        }

        public static string Usage(Item item)
        {
            switch (item)
            {
                case Item.Sword: return "Golpe cuerpo a cuerpo";
                case Item.Pickaxe: return "Picar rocas";
                case Item.Axe: return "Talar arboles";
                case Item.Wand: return "Lanza el orbe arcano";
                case Item.Wood: return "Material de construccion";
                case Item.Stone: return "Material de construccion";
                case Item.Food: return "Comer: +hambre, +vida";
                default: return "Recoger bayas";
            }
        }

        public static Color ItemColor(Item item)
        {
            switch (item)
            {
                case Item.Sword: return new Color(0.75f, 0.8f, 0.9f);
                case Item.Pickaxe: return new Color(0.55f, 0.6f, 0.7f);
                case Item.Axe: return new Color(0.8f, 0.5f, 0.3f);
                case Item.Wand: return new Color(0.6f, 0.45f, 1f);
                case Item.Wood: return new Color(0.6f, 0.4f, 0.22f);
                case Item.Stone: return new Color(0.5f, 0.5f, 0.55f);
                case Item.Food: return new Color(0.85f, 0.15f, 0.35f);
                default: return Color.gray;
            }
        }

        public int Count(Item item)
        {
            int total = 0;
            foreach (var slot in slots)
                if (slot.item == item)
                    total += slot.count;
            return total;
        }

        /// <summary>Completa pilas compatibles y luego casillas vacias. Devuelve lo aceptado.</summary>
        public int Add(Item item, int count)
        {
            if (item == Item.Empty || count <= 0)
                return 0;
            int remaining = count;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < slots.Length && remaining > 0; i++)
                    if ((pass == 0 && slots[i].item == item) || (pass == 1 && slots[i].item == Item.Empty))
                    {
                        int moved = Mathf.Min(remaining, Limit(item) - slots[i].count);
                        if (moved <= 0)
                            continue;
                        slots[i].item = item;
                        slots[i].count += moved;
                        remaining -= moved;
                    }
            return count - remaining;
        }

        public bool CanAfford(int wood, int stone) =>
            wood >= 0 && stone >= 0 && Count(Item.Wood) >= wood && Count(Item.Stone) >= stone;

        /// <summary>Comprueba todo antes de descontar: una transaccion fallida no consume nada.</summary>
        public bool Spend(int wood, int stone)
        {
            if (!CanAfford(wood, stone))
                return false;
            Remove(Item.Wood, wood);
            Remove(Item.Stone, stone);
            return true;
        }

        public void Remove(Item item, int count)
        {
            for (int i = 0; i < slots.Length && count > 0; i++)
                if (slots[i].item == item)
                {
                    int take = Mathf.Min(count, slots[i].count);
                    slots[i].count -= take;
                    count -= take;
                    if (slots[i].count == 0)
                        slots[i] = default;
                }
        }

        /// <summary>Fabrica si hay materiales Y espacio para el resultado.</summary>
        public bool Craft(Recipe recipe)
        {
            if (!CanAfford(recipe.Wood, recipe.Stone))
            {
                ShowMessage("Faltan materiales");
                return false;
            }
            if (!HasRoomFor(recipe.Result, recipe.Amount, recipe.Wood, recipe.Stone))
            {
                ShowMessage("Inventario lleno");
                return false;
            }
            Spend(recipe.Wood, recipe.Stone);
            Add(recipe.Result, recipe.Amount);
            ShowMessage("Fabricado: " + DisplayName(recipe.Result));
            return true;
        }

        public void ShowMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            message = text;
            messageUntil = Time.time + 2.2f;
        }

        private bool HasRoomFor(Item item, int amount, int woodSpent, int stoneSpent)
        {
            // Simula el gasto: casillas que quedarian vacias tambien cuentan.
            int free = 0;
            int wood = woodSpent, stone = stoneSpent;
            foreach (var slot in slots)
            {
                if (slot.item == Item.Empty)
                    free += Limit(item);
                else if (slot.item == item)
                    free += Limit(item) - slot.count;
                else if (slot.item == Item.Wood && wood >= slot.count)
                {
                    wood -= slot.count;
                    free += Limit(item);
                }
                else if (slot.item == Item.Stone && stone >= slot.count)
                {
                    stone -= slot.count;
                    free += Limit(item);
                }
            }
            return free >= amount;
        }

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            // Kit inicial: hacha y varita para empezar; el pico y la espada se fabrican.
            Add(Item.Axe, 1);
            Add(Item.Wand, 1);
            Add(Item.Food, 3);
            if (generousStartKit)
            {
                Add(Item.Wood, 10);
                Add(Item.Stone, 4);
            }
            builder = GetComponent<SurvivalBuilder>();
            if (builder == null)
                builder = gameObject.AddComponent<SurvivalBuilder>();
            needs = GetComponent<SurvivalNeeds>();
            if (needs == null)
                needs = gameObject.AddComponent<SurvivalNeeds>();
        }

        private void OnDisable()
        {
            IsOpen = false;
            pending = -1;
        }

        private void Update()
        {
            bool building = builder != null && builder.IsBuilding;
            if (Pressed(KeyCode.I))
            {
                IsOpen = !IsOpen;
                pending = -1;
                uiFrame = Time.frameCount;
            }
            if (Pressed(KeyCode.Escape) && IsOpen)
            {
                IsOpen = false;
                pending = -1;
                uiFrame = Time.frameCount;
            }
            // En modo construccion, 1..5 eligen pieza (lo gestiona SurvivalBuilder).
            if (!building && !IsOpen)
                for (int i = 0; i < 9; i++)
                    if (Pressed((KeyCode)((int)KeyCode.Alpha1 + i)))
                        selected = i;
            UpdateHeldVisual();

            if (ConsumesInput || building || Pressed(KeyCode.B) || Pointer().y < HotbarHeight ||
                !LeftPressed() || Time.time < useReady)
                return;
            if (health != null && health.IsDefeated)
                return;
            if (TryGetComponent<PlayerActionModifiers>(out var modifiers) && !modifiers.AllowsAttack)
                return;
            UseEquipped();
        }

        private void UseEquipped()
        {
            Item item = Equipped;
            if (item == Item.Wand)
            {
                var caster = GetComponent<PlayerSpellCaster>();
                if (caster != null && !caster.TryCastProjectile(Aim()))
                    ShowMessage("Varita recargando");
                return;
            }
            if (item == Item.Food)
            {
                useReady = Time.time + 0.5f;
                bool helped = needs != null && needs.Eat(30f);
                if (health != null && health.Heal(8f) > 0f)
                    helped = true;
                if (helped)
                {
                    Remove(Item.Food, 1);
                    ShowMessage("Comes bayas (+30 hambre)");
                }
                else
                    ShowMessage("No tienes hambre");
                return;
            }
            if (item == Item.Wood || item == Item.Stone)
            {
                ShowMessage("Pulsa B para construir con " + DisplayName(item));
                return;
            }

            // Espada, pico, hacha o manos: golpe corto delante del jugador hacia el cursor.
            useReady = Time.time + (item == Item.Empty ? 0.35f : 0.45f);
            swingUntil = Time.time + 0.2f;
            Vector3 dir = Aim() - transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            Vector3 center = transform.position + dir * (toolReach * 0.55f);
            int count = Physics.OverlapSphereNonAlloc(center, toolReach * 0.6f, UseHits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            Collider best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = UseHits[i];
                if (c == null || c.transform.IsChildOf(transform))
                    continue;
                bool isResource = c.GetComponent<SurvivalResource>() != null;
                CombatHealth target = c.GetComponentInParent<CombatHealth>();
                bool isEnemy = target != null && !target.IsDefeated;
                if (!isResource && !isEnemy)
                    continue;
                Vector3 to = c.ClosestPoint(transform.position) - transform.position;
                to.y = 0f;
                // Solo delante: un arco de unos 120 grados.
                if (to.sqrMagnitude > 0.01f && Vector3.Dot(to.normalized, dir) < 0.5f)
                    continue;
                if (to.sqrMagnitude < bestDistance)
                {
                    bestDistance = to.sqrMagnitude;
                    best = c;
                }
            }

            if (best == null)
            {
                if (item != Item.Empty)
                    ShowMessage("No alcanzas nada");
                return;
            }

            SurvivalResource node = best.GetComponent<SurvivalResource>();
            if (node != null)
            {
                ShowMessage(node.Harvest(this, item));
                return;
            }

            CombatHealth victim = best.GetComponentInParent<CombatHealth>();
            if (victim == null)
                return;
            float damage = item == Item.Sword ? swordDamage : item == Item.Empty ? 4f : 10f;
            PrototypeEnemy enemy = victim.GetComponent<PrototypeEnemy>();
            if (enemy != null)
                enemy.ReceiveAttackOrigin(transform.position);
            victim.ApplyDamage(damage);
            PrototypeEnemy.EmitNoise(transform.position, swordNoise, 0.45f);
            SpellImpactFlash.Spawn(best.ClosestPoint(transform.position + Vector3.up),
                item == Item.Sword ? new Color(1f, 0.85f, 0.4f) : Color.white, 0.35f, 0.2f);
        }

        internal Vector3 Aim()
        {
            var shooter = GetComponent<LocalPlayerShooter>();
            return shooter != null && shooter.TryGetAimPoint(Pointer(), out var aim)
                ? aim : transform.position + transform.forward * 3f;
        }

        private void UpdateHeldVisual()
        {
            if (heldVisual != null)
            {
                // Pequeno giro al golpear para que se lea el uso de la herramienta.
                float swing = Time.time < swingUntil ? Mathf.Sin((swingUntil - Time.time) / 0.2f * Mathf.PI) * 70f : 0f;
                heldVisual.localRotation = Quaternion.Euler(swing, 0f, 0f);
            }
            if (shownItem == Equipped && (heldVisual != null || Equipped == Item.Empty))
                return;
            if (heldVisual != null)
                Destroy(heldVisual.gameObject);
            heldVisual = null;
            shownItem = Equipped;
            if (Equipped == Item.Empty)
                return;

            var root = new GameObject("Objeto equipado: " + DisplayName(Equipped));
            heldVisual = root.transform;
            heldVisual.SetParent(transform, false);
            heldVisual.localPosition = new Vector3(0.55f, 0.1f, 0.35f);
            Color color = ItemColor(Equipped);
            bool tool = (int)Equipped <= (int)Item.Wand;
            Color handle = new Color(0.35f, 0.22f, 0.12f);
            PrototypeVfx.CreateShape(Equipped == Item.Food ? PrimitiveType.Sphere : PrimitiveType.Cube,
                "Mango", heldVisual, heldVisual.position,
                tool ? new Vector3(0.08f, 0.75f, 0.08f) : Vector3.one * 0.25f,
                tool && Equipped != Item.Wand ? handle : color).transform.localPosition = Vector3.zero;
            Vector3 headSize = Vector3.zero;
            if (Equipped == Item.Pickaxe) headSize = new Vector3(0.6f, 0.1f, 0.1f);
            else if (Equipped == Item.Axe) headSize = new Vector3(0.3f, 0.28f, 0.08f);
            else if (Equipped == Item.Sword) headSize = new Vector3(0.12f, 0.9f, 0.04f);
            else if (Equipped == Item.Wand) headSize = Vector3.one * 0.16f;
            if (headSize != Vector3.zero)
            {
                var head = PrototypeVfx.CreateShape(Equipped == Item.Wand ? PrimitiveType.Sphere : PrimitiveType.Cube,
                    "Cabeza", heldVisual, heldVisual.position, headSize, color);
                head.transform.localPosition = Vector3.up * (Equipped == Item.Sword ? 0.75f : 0.35f) +
                    (Equipped == Item.Axe ? Vector3.right * 0.12f : Vector3.zero);
            }
        }

        // ---------- Interfaz de prototipo (OnGUI) ----------

        private void OnGUI()
        {
            if (health != null && health.IsDefeated)
                return;
            float width = Mathf.Min(Screen.width - 16f, 720f), cell = width / 9f;
            float left = (Screen.width - width) / 2f, bottom = Screen.height - 64f;
            bool building = builder != null && builder.IsBuilding;
            if (!building)
            {
                for (int i = 0; i < 9; i++)
                    DrawSlot(i, new Rect(left + i * cell, bottom, cell - 3f, 55f));
                Item eq = Equipped;
                GUI.Label(new Rect(left, bottom - 22f, width, 22f),
                    DisplayName(eq) + " — " + Usage(eq) + "   |   I mochila · B construir");
            }
            if (Time.time < messageUntil)
            {
                GUIStyle style = new GUIStyle(GUI.skin.box) { fontSize = 14 };
                GUI.Box(new Rect(Screen.width / 2f - 200f, bottom - 70f, 400f, 28f), message, style);
            }

            if (!IsOpen)
                return;
            Rect panel = new Rect(left, Mathf.Max(8f, bottom - 300f), width, 230f);
            GUI.Box(panel, "MOCHILA  ·  I / Esc cerrar  ·  clic origen y luego destino: mover, combinar o intercambiar");
            for (int i = 9; i < SlotCount; i++)
                DrawSlot(i, new Rect(left + (i - 9) % 9 * cell, panel.y + 30f + (i - 9) / 9 * 62f, cell - 3f, 58f));

            // Fabricacion: a la derecha si cabe, si no debajo del titulo de la mochila.
            Rect craft = new Rect(Mathf.Min(Screen.width - 230f, left + width + 8f), panel.y, 220f, 40f + Recipes.Length * 46f);
            if (craft.x < left + width) craft.y = Mathf.Max(8f, panel.y - craft.height - 6f);
            GUI.Box(craft, "FABRICAR");
            for (int r = 0; r < Recipes.Length; r++)
            {
                Recipe recipe = Recipes[r];
                bool canAfford = CanAfford(recipe.Wood, recipe.Stone);
                GUI.enabled = canAfford;
                string cost = recipe.Wood + " madera" + (recipe.Stone > 0 ? " + " + recipe.Stone + " piedra" : "");
                if (GUI.Button(new Rect(craft.x + 8f, craft.y + 28f + r * 46f, craft.width - 16f, 40f),
                    DisplayName(recipe.Result) + "\n" + cost))
                    Craft(recipe);
                GUI.enabled = true;
            }
        }

        private void DrawSlot(int index, Rect rect)
        {
            Slot slot = slots[index];
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = pending == index ? Color.cyan : index == selected ? Color.yellow : new Color(0.7f, 0.7f, 0.7f);
            string label = slot.item == Item.Empty ? "" : DisplayName(slot.item) +
                (Limit(slot.item) > 1 ? "\nx" + slot.count : "");
            if (GUI.Button(rect, label))
            {
                if (!IsOpen) selected = index;
                else if (pending < 0) pending = index;
                else { Transfer(pending, index); pending = -1; }
                uiFrame = Time.frameCount;
            }
            GUI.backgroundColor = old;
            if (slot.item != Item.Empty)
            {
                // Franja de color como "icono" provisional del objeto.
                Color c = GUI.color;
                GUI.color = ItemColor(slot.item);
                GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 5f), Texture2D.whiteTexture);
                GUI.color = c;
            }
            if (index < 9)
                GUI.Label(new Rect(rect.x + 4f, rect.yMax - 18f, 20f, 18f), (index + 1).ToString());
        }

        private void Transfer(int source, int target)
        {
            if (source == target)
                return;
            if (slots[source].item == slots[target].item && slots[source].item != Item.Empty)
            {
                int n = Mathf.Min(slots[source].count, Limit(slots[target].item) - slots[target].count);
                slots[target].count += n;
                slots[source].count -= n;
                if (slots[source].count == 0)
                    slots[source] = default;
            }
            else
            {
                var swap = slots[target];
                slots[target] = slots[source];
                slots[source] = swap;
            }
        }

        // ---------- Entrada ----------

        internal static Vector2 Pointer()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        internal static bool LeftPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        internal static bool RightPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        internal static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            if ((int)key >= (int)KeyCode.Alpha1 && (int)key <= (int)KeyCode.Alpha9)
                return k[(Key)((int)Key.Digit1 + (int)key - (int)KeyCode.Alpha1)].wasPressedThisFrame;
            switch (key)
            {
                case KeyCode.I: return k.iKey.wasPressedThisFrame;
                case KeyCode.B: return k.bKey.wasPressedThisFrame;
                case KeyCode.R: return k.rKey.wasPressedThisFrame;
                case KeyCode.Escape: return k.escapeKey.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }
    }
}
