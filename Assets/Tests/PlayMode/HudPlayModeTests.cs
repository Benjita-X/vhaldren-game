using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.UI;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    /// <summary>
    /// HUD 022 en juego: la barra lee las recargas reales, los avisos caducan, la viñeta
    /// sigue a la vida del jugador y las armas quedan colgadas del hueso de la mano correcto.
    /// </summary>
    public sealed class HudPlayModeTests
    {
        private GameObject ground;
        private GameObject player;
        private GameObject model;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            HudNotices.Clear();
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "SueloHud";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (SpellProjectile p in Object.FindObjectsByType<SpellProjectile>(FindObjectsSortMode.None))
                Object.Destroy(p.gameObject);
            if (model != null) Object.Destroy(model);
            if (player != null) Object.Destroy(player);
            if (ground != null) Object.Destroy(ground);
            HudNotices.Clear();
            yield return null;
        }

        private ActionBarHud MakePlayer(out PlayerCombatAbilities abilities, out PlayerSpellbook book)
        {
            player = new GameObject("JugadorHud");
            player.transform.position = new Vector3(0f, 1.08f, 0f);
            CharacterController body = player.AddComponent<CharacterController>();
            body.height = 2f;
            body.radius = 0.4f;
            player.AddComponent<CombatHealth>();
            player.AddComponent<CombatDefense>();
            player.AddComponent<CombatDamageReceiver>();
            player.AddComponent<PlayerActionModifiers>();
            player.AddComponent<PlayerDamageTracker>();
            abilities = player.AddComponent<PlayerCombatAbilities>();
            book = player.AddComponent<PlayerSpellbook>();
            Physics.SyncTransforms();
            return player.AddComponent<ActionBarHud>();
        }

        [UnityTest]
        public IEnumerator BarReadsAbilityAndSpellCooldowns()
        {
            ActionBarHud bar = MakePlayer(out PlayerCombatAbilities abilities, out PlayerSpellbook book);
            yield return null;
            bar.Refresh();
            Assert.That(bar.Slot(ActionBarHud.SlotId.Sword).Ready, Is.True, "Al empezar, la espada está lista.");

            Assert.That(abilities.TrySwordSweep(), Is.True);
            yield return null;
            bar.Refresh();
            ActionBarHud.SlotState sword = bar.Slot(ActionBarHud.SlotId.Sword);
            Assert.That(sword.Ready, Is.False);
            Assert.That(sword.Remaining, Is.EqualTo(abilities.SwordCooldownRemaining).Within(0.05f));
            Assert.That(sword.Cooldown01, Is.EqualTo(abilities.SwordCooldownRemaining / abilities.SwordCooldown).Within(0.03f));
            Assert.That(sword.Key, Is.EqualTo(Vhaldren.Gameplay.Settings.GameInput.Label(Vhaldren.Gameplay.Settings.GameAction.Sword)));

            SpellDefinition first = SpellCatalog.Get(book.School, 0);
            int before = book.ChargesOf(first);
            book.ResetCooldowns();
            Assert.That(book.TryCast(0, player.transform.position + Vector3.forward * 5f), Is.True);
            yield return null;
            bar.Refresh();
            ActionBarHud.SlotState spell = bar.Slot(ActionBarHud.SlotId.Spell1);
            Assert.That(spell.Charges, Is.EqualTo(book.ChargesOf(first)), "Los puntos de carga leen el libro de hechizos.");
            Assert.That(spell.MaxCharges, Is.EqualTo(first.Charges));
            Assert.That(spell.Charges, Is.EqualTo(before - 1));
            if (spell.Charges == 0)
                Assert.That(spell.Remaining, Is.EqualTo(book.CooldownRemaining(first)).Within(0.05f));
            else
                Assert.That(spell.Ready, Is.True, "Con cargas restantes el hechizo sigue listo.");
            Assert.That(spell.Name, Is.EqualTo(first.DisplayName));
        }

        [UnityTest]
        public IEnumerator NoticesExpireInRealTime()
        {
            HudNotices.Post("Aviso de prueba");
            Assert.That(HudNotices.CountAt(Time.unscaledTime), Is.EqualTo(1));
            float until = Time.unscaledTime + HudNotices.Duration + 0.3f;
            while (Time.unscaledTime < until)
                yield return null;
            Assert.That(HudNotices.CountAt(Time.unscaledTime), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator VignetteFollowsPlayerHealth()
        {
            player = new GameObject("JugadorVineta");
            CombatHealth health = player.AddComponent<CombatHealth>();
            ScreenVignette vignette = player.AddComponent<ScreenVignette>();
            vignette.Bind(health);
            yield return null;
            Assert.That(ScreenVignette.LowHealthFactor(vignette.CurrentHealth01), Is.EqualTo(0f));
            health.ApplyDamage(health.MaximumHealth * 0.85f);
            yield return null;
            Assert.That(vignette.CurrentHealth01, Is.LessThan(ScreenVignette.LowHealthThreshold));
            Assert.That(ScreenVignette.LowHealthFactor(vignette.CurrentHealth01), Is.GreaterThan(0.3f),
                "Con menos del 30 % de vida la viñeta se vuelve roja.");
        }

#if UNITY_EDITOR
        private CharacterLoadout Equip(string assetPath, string rootName)
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(prefab, Is.Not.Null, assetPath);
            player = new GameObject(rootName);
            model = Object.Instantiate(prefab, player.transform, false);
            CharacterLoadout loadout = player.AddComponent<CharacterLoadout>();
            loadout.Apply();
            return loadout;
        }

        [UnityTest]
        public IEnumerator KnightHoldsSwordAndShieldOnHandBones()
        {
            CharacterLoadout loadout = Equip("Assets/ThirdParty/KayKitEnemies/Knight_Enemigo.prefab", "CaballeroPrueba");
            yield return null;
            Assert.That(loadout.ModelKind, Is.EqualTo("knight"));
            Assert.That(loadout.RightHandItem, Is.Not.Null);
            Assert.That(loadout.RightHandItem.name, Is.EqualTo("1H_Sword"));
            Assert.That(loadout.RightHandItem.parent.name, Is.EqualTo(CharacterLoadout.RightSlot));
            Assert.That(loadout.RightHandItem.gameObject.activeInHierarchy, Is.True);
            Assert.That(loadout.LeftHandItem, Is.Not.Null);
            Assert.That(loadout.LeftHandItem.parent.name, Is.EqualTo(CharacterLoadout.LeftSlot));
            Assert.That(loadout.LeftHandItem.name, Does.EndWith("Shield"));

            int visible = 0;
            foreach (Transform slot in new[] { loadout.RightHandItem.parent, loadout.LeftHandItem.parent })
                foreach (Transform item in slot)
                    if (item.gameObject.activeSelf)
                        visible++;
            Assert.That(visible, Is.EqualTo(2), "Solo una espada y un escudo, no las siete piezas superpuestas.");
        }

        [UnityTest]
        public IEnumerator SkeletonGetsPackWeaponWithItsOwnMaterial()
        {
            CharacterLoadout loadout = Equip("Assets/ThirdParty/KayKitEnemies/Skeleton_Minion_Enemigo.prefab", "EsqueletoPrueba");
            yield return null;
            Assert.That(loadout.ModelKind, Is.EqualTo("skeleton_minion"));
            Assert.That(loadout.RightHandItem, Is.Not.Null, "El esqueleto recibe la hoja del pack (Resources/ArmasKayKit).");
            Assert.That(loadout.RightHandItem.parent.name, Is.EqualTo(CharacterLoadout.RightSlot));
            Renderer weapon = loadout.RightHandItem.GetComponentInChildren<Renderer>();
            Renderer bodyRenderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.sharedMaterial, Is.SameAs(bodyRenderer.sharedMaterial), "Mismo atlas que el cuerpo.");
            Assert.That(loadout.RightHandItem.GetComponentInChildren<Collider>(), Is.Null, "El arma es solo visual.");
            // Tamaño en el mundo comparable a una espada de aventurero (no diminuta).
            Bounds size = weapon.bounds;
            Assert.That(size.size.magnitude, Is.InRange(0.3f, 3f), "La hoja mide como un arma de mano: " + size.size);
            Object.Destroy(player);
            Object.Destroy(model);
            yield return null;

            CharacterLoadout warrior = Equip("Assets/ThirdParty/KayKitEnemies/Skeleton_Warrior_Enemigo.prefab", "GuerreroPrueba");
            yield return null;
            Assert.That(warrior.RightHandItem.name, Does.Contain("Skeleton_Axe"));
            Assert.That(warrior.LeftHandItem.name, Does.Contain("Skeleton_Shield"));
            Assert.That(warrior.LeftHandItem.parent.name, Is.EqualTo(CharacterLoadout.LeftSlot));
        }

        [UnityTest]
        public IEnumerator BarbarianMageAndRogueKeepOneWeaponEach()
        {
            CharacterLoadout barbarian = Equip("Assets/ThirdParty/KayKitEnemies/Barbarian_Enemigo.prefab", "BarbaroPrueba");
            yield return null;
            Assert.That(barbarian.RightHandItem.name, Does.Contain("Axe"));
            Assert.That(barbarian.RightHandItem.parent.name, Is.EqualTo(CharacterLoadout.RightSlot));
            Object.Destroy(player);
            Object.Destroy(model);
            yield return null;

            CharacterLoadout mage = Equip("Assets/ThirdParty/KayKitEnemies/Mage_Enemigo.prefab", "MagaPrueba");
            yield return null;
            Assert.That(mage.RightHandItem.name, Is.EqualTo("2H_Staff"));
            Assert.That(mage.LeftHandItem, Is.Null, "La maga lleva solo el bastón.");
            Assert.That(mage.RightHandItem.parent.Find("1H_Wand").gameObject.activeSelf, Is.False);
            Object.Destroy(player);
            Object.Destroy(model);
            yield return null;

            CharacterLoadout rogue = Equip("Assets/ThirdParty/KayKitEnemies/Rogue_Enemigo.prefab", "PicaroPrueba");
            yield return null;
            Assert.That(rogue.RightHandItem.name, Is.EqualTo("Knife"));
            Assert.That(rogue.LeftHandItem.name, Is.EqualTo("Knife_Offhand"));
            Transform crossbow = rogue.RightHandItem.parent.Find("1H_Crossbow");
            Assert.That(crossbow.gameObject.activeSelf, Is.False, "Las ballestas sobrantes se ocultan.");
        }

        [UnityTest]
        public IEnumerator PlayerKeepsCrossbowInRightHand()
        {
            CharacterLoadout loadout = Equip("Assets/ThirdParty/KayKitAdventurers/RogueHooded.fbx", "PlayerRoot");
            yield return null;
            Assert.That(loadout.ModelKind, Is.EqualTo("player"));
            Assert.That(loadout.RightHandItem.name, Is.EqualTo("1H_Crossbow"));
            Assert.That(loadout.RightHandItem.parent.name, Is.EqualTo(CharacterLoadout.RightSlot));
            Assert.That(loadout.RightHandItem.parent.Find("2H_Crossbow").gameObject.activeSelf, Is.False);
            Assert.That(loadout.RightHandItem.parent.Find("Knife").gameObject.activeSelf, Is.False);
        }
#endif
    }
}
