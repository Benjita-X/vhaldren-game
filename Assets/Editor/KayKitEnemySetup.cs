using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Vhaldren.Gameplay.World;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Convierte los personajes KayKit (CC0) en modelos de enemigo animados:
    /// importacion generica, material con su textura, un controlador base
    /// (reposo/caminar, ataque, golpe) y, por modelo y estilo de ataque, un
    /// AnimatorOverrideController con los clips del propio FBX.
    /// </summary>
    public static class KayKitEnemySetup
    {
        public const string OutputFolder = "Assets/ThirdParty/KayKitEnemies";
        public const string BaseControllerPath = OutputFolder + "/EnemigoBase.controller";

        public enum AttackStyle { Melee, Heavy, Ranged, Spell }

        public sealed class ModelInfo
        {
            public readonly string Id;
            public readonly string FbxPath;
            public readonly string TexturePath;

            public ModelInfo(string id, string fbxPath, string texturePath)
            {
                Id = id;
                FbxPath = fbxPath;
                TexturePath = texturePath;
            }
        }

        public static readonly ModelInfo[] Models =
        {
            new ModelInfo("Barbarian", "Assets/ThirdParty/KayKitAdventurers/Barbarian.fbx", "Assets/ThirdParty/KayKitAdventurers/barbarian_texture.png"),
            new ModelInfo("Knight", "Assets/ThirdParty/KayKitAdventurers/Knight.fbx", "Assets/ThirdParty/KayKitAdventurers/knight_texture.png"),
            new ModelInfo("Mage", "Assets/ThirdParty/KayKitAdventurers/Mage.fbx", "Assets/ThirdParty/KayKitAdventurers/mage_texture.png"),
            new ModelInfo("Rogue", "Assets/ThirdParty/KayKitAdventurers/Rogue.fbx", "Assets/ThirdParty/KayKitAdventurers/rogue_texture.png"),
            new ModelInfo("Skeleton_Minion", "Assets/ThirdParty/KayKitSkeletons/Skeleton_Minion.fbx", "Assets/ThirdParty/KayKitSkeletons/skeleton_texture.png"),
            new ModelInfo("Skeleton_Warrior", "Assets/ThirdParty/KayKitSkeletons/Skeleton_Warrior.fbx", "Assets/ThirdParty/KayKitSkeletons/skeleton_texture.png"),
        };

        private static readonly string[] IdleNames = { "Idle_Combat", "Idle", "Idle_B" };
        private static readonly string[] HumanWalk = { "Walking_A", "Walking_B", "Running_A" };
        private static readonly string[] SkeletonWalk = { "Walking_D_Skeletons", "Skeletons_Walking", "Walking_A" };
        private static readonly string[] HitNames = { "Hit_A", "Hit_B" };

        private static string[] AttackNames(AttackStyle style)
        {
            switch (style)
            {
                case AttackStyle.Heavy: return new[] { "2H_Melee_Attack_Chop", "2H_Melee_Attack_Slice", "1H_Melee_Attack_Chop" };
                case AttackStyle.Ranged: return new[] { "1H_Ranged_Shoot", "2H_Ranged_Shoot", "Throw" };
                case AttackStyle.Spell: return new[] { "Spellcast_Shoot", "Spellcast_Raise", "Spellcasting" };
                default: return new[] { "1H_Melee_Attack_Chop", "1H_Melee_Attack_Slice_Diagonal", "Unarmed_Melee_Attack_Punch_A" };
            }
        }

        public static string PrefabPath(string modelId) => OutputFolder + "/" + modelId + "_Enemigo.prefab";

        public static string OverridePath(string modelId, AttackStyle style) =>
            OutputFolder + "/" + modelId + "_" + style + ".overrideController";

        [MenuItem("Vhaldren/Personaje/Preparar enemigos KayKit")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/ThirdParty", "KayKitEnemies");

            foreach (ModelInfo model in Models)
                ConfigureImporter(model.FbxPath);

            ModelInfo reference = Models.First(m => m.Id == "Skeleton_Warrior");
            Dictionary<string, AnimationClip> referenceClips = ClipsOf(reference.FbxPath);
            AnimatorController controller = BuildBaseController(
                Pick(referenceClips, IdleNames), Pick(referenceClips, SkeletonWalk),
                Pick(referenceClips, AttackNames(AttackStyle.Melee)), Pick(referenceClips, HitNames));

            foreach (ModelInfo model in Models)
            {
                Material material = MaterialFor(model);
                BuildPrefab(model, controller, material);
                Dictionary<string, AnimationClip> clips = ClipsOf(model.FbxPath);
                bool skeleton = model.Id.StartsWith("Skeleton", StringComparison.Ordinal);
                foreach (AttackStyle style in Enum.GetValues(typeof(AttackStyle)))
                    BuildOverride(model, style, controller, clips, skeleton);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Vhaldren] Enemigos KayKit listos: " + Models.Length + " modelos, " +
                      (Models.Length * 4) + " variantes de animacion.");
        }

        private static void ConfigureImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("No se encuentra " + path);
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                changed = true;
            }
            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool loop = IdleNames.Contains(clip.name) || HumanWalk.Contains(clip.name) || SkeletonWalk.Contains(clip.name);
                if (clip.loopTime != loop)
                {
                    clip.loopTime = loop;
                    changed = true;
                }
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, AnimationClip> ClipsOf(string fbx)
        {
            Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    clips[clip.name] = clip;
            return clips;
        }

        private static AnimationClip Pick(Dictionary<string, AnimationClip> clips, string[] names)
        {
            foreach (string name in names)
                if (clips.TryGetValue(name, out AnimationClip clip))
                    return clip;
            return null;
        }

        private static Material MaterialFor(ModelInfo model)
        {
            string path = OutputFolder + "/" + model.Id + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = model.Id };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(model.TexturePath));
            material.SetFloat("_Glossiness", 0.18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController BuildBaseController(AnimationClip idle, AnimationClip walk,
            AnimationClip attack, AnimationClip hit)
        {
            if (idle == null || walk == null || attack == null || hit == null)
                throw new InvalidOperationException("Faltan clips basicos en el modelo de referencia.");
            AssetDatabase.DeleteAsset(BaseControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(BaseControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState move = controller.CreateBlendTreeInController("Movimiento", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 2.5f);
            machine.defaultState = move;

            AddAction(machine, move, "Ataque", attack, "Attack", 0.9f);
            AddAction(machine, move, "Golpe", hit, "Hit", 0.85f);
            return controller;
        }

        private static void AddAction(AnimatorStateMachine machine, AnimatorState move, string name,
            AnimationClip clip, string trigger, float exitTime)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = clip;
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            AnimatorStateTransition back = state.AddTransition(move);
            back.hasExitTime = true;
            back.exitTime = exitTime;
            back.duration = 0.1f;
        }

        private static void BuildOverride(ModelInfo model, AttackStyle style, AnimatorController controller,
            Dictionary<string, AnimationClip> clips, bool skeleton)
        {
            string path = OverridePath(model.Id, style);
            AssetDatabase.DeleteAsset(path);
            AnimatorOverrideController overrides = new AnimatorOverrideController(controller) { name = model.Id + " " + style };
            List<KeyValuePair<AnimationClip, AnimationClip>> pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrides.GetOverrides(pairs);
            AnimationClip idle = Pick(clips, IdleNames);
            AnimationClip walk = Pick(clips, skeleton ? SkeletonWalk : HumanWalk);
            AnimationClip attack = Pick(clips, AttackNames(style));
            AnimationClip hit = Pick(clips, HitNames);
            for (int i = 0; i < pairs.Count; i++)
            {
                string original = pairs[i].Key.name;
                AnimationClip replacement =
                    IdleNames.Contains(original) ? idle :
                    SkeletonWalk.Contains(original) || HumanWalk.Contains(original) ? walk :
                    HitNames.Contains(original) ? hit : attack;
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, replacement);
            }
            overrides.ApplyOverrides(pairs);
            AssetDatabase.CreateAsset(overrides, path);
        }

        private static void BuildPrefab(ModelInfo model, AnimatorController controller, Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(model.FbxPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "Modelo texturizado";
            if (instance.GetComponent<AuthoredVisual>() == null)
                instance.AddComponent<AuthoredVisual>();
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < shared.Length; i++)
                    shared[i] = material;
                renderer.sharedMaterials = shared;
            }
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath(model.Id));
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
