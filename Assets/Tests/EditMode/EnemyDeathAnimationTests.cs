using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Vhaldren.Tests
{
    /// <summary>
    /// La muerte de los enemigos KayKit vive en los assets ya existentes (mismos GUID):
    /// un estado "Muerte" con Death_A en el controlador base y el clip de cada modelo en
    /// sus 24 variantes. Y los shaders de efectos estan en Resources para el ejecutable.
    /// </summary>
    public sealed class EnemyDeathAnimationTests
    {
        private const string Folder = "Assets/ThirdParty/KayKitEnemies/";
        private const string BasePath = Folder + "EnemigoBase.controller";
        private const string BaseGuid = "192a5c7714e25e94cbabe7c4bbf86537";
        private static readonly string[] Models = { "Barbarian", "Knight", "Mage", "Rogue", "Skeleton_Minion", "Skeleton_Warrior" };
        private static readonly string[] Styles = { "Melee", "Heavy", "Ranged", "Spell" };

        private static AnimatorState DeathState(AnimatorController controller)
        {
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
                if (child.state.name == "Muerte")
                    return child.state;
            return null;
        }

        [Test]
        public void BaseControllerKeepsItsGuidAndGainsADeathStateWithPriority()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(BasePath), Is.EqualTo(BaseGuid),
                "El controlador se modifica en el sitio: la escena conserva sus referencias.");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
            Assert.That(controller, Is.Not.Null);

            bool trigger = false;
            foreach (AnimatorControllerParameter parameter in controller.parameters)
                if (parameter.name == "Death" && parameter.type == AnimatorControllerParameterType.Trigger)
                    trigger = true;
            Assert.That(trigger, Is.True, "Falta el disparador Death.");

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            Assert.That(machine.states.Length, Is.EqualTo(4), "Movimiento, Ataque, Golpe y Muerte; nada duplicado.");
            AnimatorState death = DeathState(controller);
            Assert.That(death, Is.Not.Null);
            AnimationClip clip = death.motion as AnimationClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.name, Is.EqualTo("Death_A"));
            Assert.That(clip.isLooping, Is.False, "La caida no se repite.");
            Assert.That(death.transitions, Is.Empty, "El cuerpo se queda en el suelo hasta reaparecer.");

            AnimatorStateTransition first = machine.anyStateTransitions[0];
            Assert.That(first.destinationState, Is.EqualTo(death), "La muerte gana a golpe y ataque en el mismo fotograma.");
            Assert.That(first.conditions.Length, Is.EqualTo(1));
            Assert.That(first.conditions[0].parameter, Is.EqualTo("Death"));
            Assert.That(first.canTransitionToSelf, Is.False);
            Assert.That(machine.anyStateTransitions.Length, Is.EqualTo(3));
        }

        [Test]
        public void EveryVariantFallsWithItsOwnModelDeathClip()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
            AnimationClip reference = DeathState(controller)?.motion as AnimationClip;
            Assert.That(reference, Is.Not.Null);
            int checkedVariants = 0;
            foreach (string model in Models)
                foreach (string style in Styles)
                {
                    string path = Folder + model + "_" + style + ".overrideController";
                    AnimatorOverrideController overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                    Assert.That(overrides, Is.Not.Null, path);
                    Assert.That(overrides.runtimeAnimatorController, Is.EqualTo(controller), path);
                    AnimationClip clip = overrides[reference];
                    Assert.That(clip, Is.Not.Null, path);
                    Assert.That(clip.name, Is.EqualTo("Death_A"), path);
                    StringAssert.Contains(model + ".fbx", AssetDatabase.GetAssetPath(clip),
                        path + " debe usar la caida de su propio modelo.");
                    checkedVariants++;
                }
            Assert.That(checkedVariants, Is.EqualTo(24));
        }

        [Test]
        public void EffectShadersExistAndShipInsideResources()
        {
            foreach (string name in new[] { "Vhaldren/VfxParticula", "Vhaldren/VfxSuelo" })
            {
                Shader shader = Shader.Find(name);
                Assert.That(shader, Is.Not.Null, name);
                Assert.That(shader.isSupported, Is.True, name + " debe compilar en esta plataforma.");
                StringAssert.Contains("/Resources/", AssetDatabase.GetAssetPath(shader),
                    name + " tiene que ir en Resources para que el ejecutable lo incluya.");
            }
        }
    }
}
