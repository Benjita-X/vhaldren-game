using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Actualiza el Animator YA VERSIONADO de KayKit agregando solo parámetros
    /// y estados faltantes. No regenera ni sobrescribe sus estados antiguos.
    /// Se verifica al abrir Unity para que las escenas 015/016 existentes
    /// reciban las animaciones de combate sin repetir un setup destructivo.
    /// </summary>
    public static class KayKitCombatAnimationUpgrade
    {
        private readonly struct ActionClip
        {
            public readonly string Parameter;
            public readonly string State;
            public readonly string Clip;
            public readonly float Speed;
            public readonly float ExitTime;

            public ActionClip(string parameter, string state, string clip,
                float speed, float exitTime)
            {
                Parameter = parameter;
                State = state;
                Clip = clip;
                Speed = speed;
                ExitTime = exitTime;
            }
        }

        private static readonly ActionClip[] Combat =
        {
            new ActionClip("SwordAction", "Espada",
                "1H_Melee_Attack_Slice_Diagonal", 1.8f, 0.72f),
            new ActionClip("SpellAction", "Magia",
                "Spellcast_Shoot", 1.9f, 0.72f),
            new ActionClip("FrostAction", "Escarcha",
                "Spellcast_Raise", 1.9f, 0.72f),
            new ActionClip("ShieldAction", "Escudo",
                "Block", 2f, 0.72f),
            new ActionClip("WardAction", "Barrera",
                "Spellcast_Raise", 1.9f, 0.72f),
            new ActionClip("ParryAction", "Parada precisa",
                "Block_Hit", 2.6f, 0.78f),
            new ActionClip("Harvest", "Recolectar a mano",
                "PickUp", 1.65f, 0.75f),
            new ActionClip("Harvest", "Picar roca",
                "1H_Melee_Attack_Chop", 1.9f, 0.75f)
        };

        [InitializeOnLoadMethod]
        private static void ScheduleUpgrade()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    UpgradeController();
            };
        }

        [MenuItem("Vhaldren/Personaje/Revisar y completar animaciones")]
        public static void UpgradeMenu()
        {
            if (UpgradeController())
                Debug.Log("[Vhaldren] Animator KayKit con animaciones de combate y recolección.");
        }

        public static bool UpgradeController()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    KayKitCharacterSetup.ControllerPath);
            if (controller == null)
                return false;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                KayKitCharacterSetup.ModelPath);
            if (model == null)
                return false;

            Dictionary<string, AnimationClip> available =
                new Dictionary<string, AnimationClip>();
            foreach (UnityEngine.Object asset in
                AssetDatabase.LoadAllAssetsAtPath(KayKitCharacterSetup.ModelPath))
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    available[clip.name] = clip;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = machine.defaultState;
            bool changed = false;

            if (!HasParameter(controller, "HarvestStyle"))
            {
                controller.AddParameter("HarvestStyle", AnimatorControllerParameterType.Int);
                changed = true;
            }

            for (int i = 0; i < Combat.Length; i++)
            {
                ActionClip action = Combat[i];
                if (!HasParameter(controller, action.Parameter))
                {
                    controller.AddParameter(action.Parameter,
                        AnimatorControllerParameterType.Trigger);
                    changed = true;
                }

                if (HasState(machine, action.State))
                    continue;
                if (!available.TryGetValue(action.Clip, out AnimationClip clip))
                {
                    Debug.LogWarning("[Vhaldren] Falta el clip: " + action.Clip);
                    continue;
                }

                AnimatorState state = machine.AddState(action.State);
                state.motion = clip;
                state.speed = action.Speed;
                AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, action.Parameter);
                if (action.Parameter == "Harvest")
                    enter.AddCondition(AnimatorConditionMode.Equals,
                        i == Combat.Length - 1 ? 1 : 0, "HarvestStyle");
                enter.duration = 0.05f;
                enter.canTransitionToSelf = false;

                if (locomotion != null)
                {
                    AnimatorStateTransition exit = state.AddTransition(locomotion);
                    exit.hasExitTime = true;
                    exit.exitTime = action.ExitTime;
                    exit.duration = 0.08f;
                }
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Debug.Log("[Vhaldren] Animaciones nuevas agregadas sin borrar el controlador existente.");
            }
            return true;
        }

        private static bool HasParameter(AnimatorController controller, string parameter)
        {
            foreach (AnimatorControllerParameter item in controller.parameters)
                if (item.name == parameter)
                    return true;
            return false;
        }

        private static bool HasState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (child.state != null && child.state.name == name)
                    return true;
            return false;
        }
    }
}
