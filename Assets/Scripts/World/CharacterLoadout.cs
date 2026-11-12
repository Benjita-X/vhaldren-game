using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Progression;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Armas visibles en las manos de los personajes KayKit (CC0). Los modelos de
    /// aventureros traen TODAS sus armas colgadas de los huesos <c>handslot.r</c> y
    /// <c>handslot.l</c> a la vez (el caballero llevaba tres espadas y cuatro escudos
    /// superpuestos); aquí se deja solo la de su tipo. Los esqueletos no traen armas:
    /// se cuelga una del pack de esqueletos (Resources/ArmasKayKit) con su mismo material.
    /// El jugador conserva la ballesta en la mano, que cambia de color y brillo con la
    /// calidad forjada del arma. Todo en tiempo de juego: no toca prefabs ni la escena.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterLoadout : MonoBehaviour
    {
        public const string RightSlot = "handslot.r";
        public const string LeftSlot = "handslot.l";
        public const string ResourceFolder = "ArmasKayKit/";

        /// <summary>Todas las piezas de mano que traen los modelos de aventureros de KayKit.</summary>
        private static readonly HashSet<string> KnownHandItems = new HashSet<string>
        {
            "1H_Axe", "1H_Axe_Offhand", "2H_Axe", "Barbarian_Round_Shield", "Mug",
            "1H_Sword", "1H_Sword_Offhand", "2H_Sword", "Badge_Shield", "Rectangle_Shield", "Round_Shield", "Spike_Shield",
            "1H_Wand", "2H_Staff", "Spellbook", "Spellbook_open",
            "1H_Crossbow", "2H_Crossbow", "Knife", "Knife_Offhand", "Throwable"
        };

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Transform rightSlot, leftSlot;
        private string appliedFor;
        private int appliedTier = -1;
        private Material tierMaterial;
        private PlayerEquipment equipment;
        private PrototypeEnemy enemy;
        private float nextCheck;

        /// <summary>Tipo de modelo detectado: knight, barbarian, mage, rogue, skeleton_minion, skeleton_warrior o player.</summary>
        public string ModelKind { get; private set; } = string.Empty;
        public Transform RightHandItem { get; private set; }
        public Transform LeftHandItem { get; private set; }
        public bool Equipped => RightHandItem != null || LeftHandItem != null;
        public int WeaponTier => appliedTier;

        private void Start() => Apply();

        private void OnDestroy()
        {
            if (tierMaterial != null)
                Destroy(tierMaterial);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 0.5f;
            // El arquetipo puede llegar después (oleadas, pruebas); la forja cambia la calidad.
            if (appliedFor != LoadoutKey())
                Apply();
            else if (equipment != null && equipment.TierOf(GearSlot.Weapon) != appliedTier)
                ApplyTier();
        }

        private string LoadoutKey()
        {
            string archetype = enemy != null && enemy.Archetype != null ? enemy.Archetype.Id : "-";
            return archetype + (enemy != null && enemy.IsBoss ? "+jefe" : "");
        }

        /// <summary>Busca los huesos de mano, detecta el modelo y deja solo su arma.</summary>
        public void Apply()
        {
            enemy = GetComponent<PrototypeEnemy>();
            if (equipment == null)
                equipment = GetComponent<PlayerEquipment>();
            appliedFor = LoadoutKey();
            rightSlot = FindDeep(transform, RightSlot);
            leftSlot = FindDeep(transform, LeftSlot);
            RightHandItem = null;
            LeftHandItem = null;
            if (rightSlot == null && leftSlot == null)
            {
                ModelKind = string.Empty;   // cuerpo provisional (lobo, primitivas): nada que hacer
                return;
            }

            ModelKind = DetectModel();
            string right = null, left = null, extraRight = null, extraLeft = null;
            Choose(ModelKind, enemy, ref right, ref left, ref extraRight, ref extraLeft);

            HideAllExcept(rightSlot, right);
            HideAllExcept(leftSlot, left);
            RightHandItem = Show(rightSlot, right) ?? Attach(rightSlot, extraRight);
            LeftHandItem = Show(leftSlot, left) ?? Attach(leftSlot, extraLeft);

            if (ModelKind == "player")
                ApplyTier();
        }

        /// <summary>
        /// Arma según el modelo y el papel: bárbaro con hacha (a dos manos si es bruto),
        /// caballero con espada y escudo, maga con bastón (varita y libro si sana), pícaro
        /// con ballesta o dagas, esqueletos con hoja o hacha y escudo del pack.
        /// </summary>
        private static void Choose(string kind, PrototypeEnemy enemy, ref string right, ref string left,
            ref string extraRight, ref string extraLeft)
        {
            EnemyRole role = enemy != null && enemy.Archetype != null ? enemy.Archetype.Role : EnemyRole.Melee;
            bool boss = enemy != null && enemy.IsBoss;
            switch (kind)
            {
                case "player":
                    right = "1H_Crossbow";
                    break;
                case "barbarian":
                    if (role == EnemyRole.Brute) right = "2H_Axe";
                    else { right = "1H_Axe"; left = "Barbarian_Round_Shield"; }
                    break;
                case "knight":
                    right = boss ? "2H_Sword" : "1H_Sword";
                    if (!boss)
                        left = role == EnemyRole.Elite ? "Rectangle_Shield" : "Round_Shield";
                    break;
                case "mage":
                    if (role == EnemyRole.Support) { right = "1H_Wand"; left = "Spellbook_open"; }
                    else right = "2H_Staff";
                    break;
                case "rogue":
                    if (role == EnemyRole.Ranged) right = "1H_Crossbow";
                    else { right = "Knife"; left = "Knife_Offhand"; }
                    break;
                case "skeleton_warrior":
                    extraRight = "Skeleton_Axe";
                    extraLeft = "Skeleton_Shield_Small_A";
                    break;
                case "skeleton_minion":
                    extraRight = "Skeleton_Blade";
                    break;
            }
        }

        private string DetectModel()
        {
            if (GetComponent<LocalPlayerShooter>() != null || name == "PlayerRoot")
                return "player";
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("Skeleton_Warrior_", System.StringComparison.Ordinal)) return "skeleton_warrior";
                if (n.StartsWith("Skeleton_Minion_", System.StringComparison.Ordinal)) return "skeleton_minion";
                if (n.StartsWith("Knight_", System.StringComparison.Ordinal)) return "knight";
                if (n.StartsWith("Barbarian_", System.StringComparison.Ordinal) && n != "Barbarian_Round_Shield") return "barbarian";
                if (n.StartsWith("Mage_", System.StringComparison.Ordinal)) return "mage";
                if (n.StartsWith("Rogue_", System.StringComparison.Ordinal)) return "rogue";
            }
            return "unknown";
        }

        private static void HideAllExcept(Transform slot, string keep)
        {
            if (slot == null)
                return;
            foreach (Transform child in slot)
            {
                if (KnownHandItems.Contains(child.name))
                    child.gameObject.SetActive(child.name == keep);
                else if (child.name.StartsWith("Arma: ", System.StringComparison.Ordinal))
                    child.gameObject.SetActive(false);
            }
        }

        private static Transform Show(Transform slot, string item)
        {
            if (slot == null || string.IsNullOrEmpty(item))
                return null;
            Transform found = slot.Find(item);
            if (found != null)
                found.gameObject.SetActive(true);
            return found;
        }

        /// <summary>Cuelga un arma del pack de esqueletos con el material del propio cuerpo.</summary>
        private Transform Attach(Transform slot, string resource)
        {
            if (slot == null || string.IsNullOrEmpty(resource))
                return null;
            string objectName = "Arma: " + resource;
            Transform existing = slot.Find(objectName);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing;
            }
            GameObject prefab = Resources.Load<GameObject>(ResourceFolder + resource);
            if (prefab == null)
                return null;
            // Se conserva la rotación y escala de la raíz importada (el FBX del pack viene en
            // centímetros con el eje Z arriba: 270° en X y escala 100 la dejan como las armas
            // de los aventureros, que apuntan hacia +Y desde el hueso de la mano).
            GameObject weapon = Instantiate(prefab, slot, false);
            weapon.name = objectName;
            weapon.transform.localPosition = Vector3.zero;
            Material body = BodyMaterial();
            foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>(true))
            {
                if (body != null)
                    r.sharedMaterial = body;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            // Solo visual: ninguna colisión ni capa de combate.
            foreach (Collider c in weapon.GetComponentsInChildren<Collider>(true))
                Destroy(c);
            return weapon.transform;
        }

        private Material BodyMaterial()
        {
            foreach (SkinnedMeshRenderer skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skinned.sharedMaterial != null)
                    return skinned.sharedMaterial;
            return null;
        }

        /// <summary>Color y brillo de la ballesta del jugador según la calidad forjada (0-3).</summary>
        public static void TierLook(int tier, out Color albedo, out Color emission)
        {
            switch (Mathf.Clamp(tier, 0, GearCatalog.MaxTier))
            {
                case 1: albedo = new Color(1f, 0.9f, 0.76f); emission = new Color(0.16f, 0.09f, 0.02f); break;
                case 2: albedo = new Color(0.86f, 0.95f, 1.08f); emission = new Color(0.05f, 0.16f, 0.32f); break;
                case 3: albedo = new Color(1.12f, 0.82f, 0.74f); emission = new Color(0.5f, 0.05f, 0.09f); break;
                default: albedo = Color.white; emission = Color.black; break;
            }
        }

        private void ApplyTier()
        {
            if (RightHandItem == null)
                return;
            int tier = equipment != null ? equipment.TierOf(GearSlot.Weapon) : 0;
            appliedTier = tier;
            Renderer r = RightHandItem.GetComponent<Renderer>();
            if (r == null)
                return;
            if (tierMaterial == null && r.sharedMaterial != null)
            {
                // Material propio solo para el arma del jugador (el cuerpo no cambia).
                tierMaterial = new Material(r.sharedMaterial) { name = r.sharedMaterial.name + " (arma forjada)" };
                r.sharedMaterial = tierMaterial;
            }
            if (tierMaterial == null)
                return;
            TierLook(tier, out Color albedo, out Color emission);
            tierMaterial.SetColor(ColorId, albedo);
            if (emission.maxColorComponent > 0.001f)
            {
                tierMaterial.EnableKeyword("_EMISSION");
                tierMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
                tierMaterial.DisableKeyword("_EMISSION");
            tierMaterial.SetColor(EmissionId, emission);
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
