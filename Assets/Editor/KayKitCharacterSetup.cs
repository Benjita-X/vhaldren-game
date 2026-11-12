using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Prepara el personaje CC0 de KayKit (Rogue encapuchado): importacion de animaciones,
    /// material, Animator con mezcla direccional de piernas, esquivas y disparo.
    /// </summary>
    public static class KayKitCharacterSetup
    {
        public const string Folder = "Assets/ThirdParty/KayKitAdventurers/";
        public const string ModelPath = Folder + "RogueHooded.fbx";
        public const string TexturePath = Folder + "rogue_texture.png";
        public const string MaterialPath = Folder + "RogueHooded.mat";
        public const string ControllerPath = Folder + "RogueHooded.controller";
        private const string TempControllerPath = Folder + "RogueHooded.tmp.controller";

        // Altura del modelo importado (~2,44 m con capucha) llevada a la capsula de 2 m.
        private const float ModelScale = 0.82f;

        private static readonly string[] LoopingClips =
        {
            "Idle", "Running_A", "Running_B", "Running_Strafe_Left", "Running_Strafe_Right",
            "Walking_A", "Walking_B", "Walking_C", "Walking_Backwards", "1H_Ranged_Aiming"
        };

        // Armas que trae el modelo: solo se deja visible la ballesta de una mano.
        private static readonly string[] HiddenParts = { "2H_Crossbow", "Knife", "Knife_Offhand", "Throwable" };

        public static bool IsAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null;

        [MenuItem("Vhaldren/Personaje/Preparar personaje KayKit")]
        public static void Prepare()
        {
            ConfigureImporter();
            GetOrCreateMaterial();
            BuildController();
            AssetDatabase.SaveAssets();
            Debug.Log("[Vhaldren] Personaje KayKit preparado.");
        }

        /// <summary>
        /// Coloca el modelo bajo el visual del jugador y conecta el Animator.
        /// Devuelve false si el modelo no esta en el proyecto.
        /// </summary>
        public static bool TryAttach(GameObject player, Transform visualRoot, PlayerMotor motor)
        {
            if (!IsAvailable)
                return false;

            Prepare();
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.name = "KayKitModel";
            model.transform.SetParent(visualRoot, false);
            // Los pies del modelo en la base de la capsula (centro del jugador - 1 m).
            model.transform.localPosition = new Vector3(0f, -1f, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * ModelScale;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                if (Array.IndexOf(HiddenParts, renderer.name) >= 0)
                    renderer.gameObject.SetActive(false);
            }
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            // El motor manda en la posicion: la animacion nunca debe desplazar al personaje.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PlayerAnimationDriver driver = player.AddComponent<PlayerAnimationDriver>();
            SerializedObject serialized = new SerializedObject(driver);
            serialized.FindProperty("motor").objectReferenceValue = motor;
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            serialized.FindProperty("shooter").objectReferenceValue = player.GetComponent<LocalPlayerShooter>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void ConfigureImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Falta " + ModelPath);

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            bool changed = importer.animationType != ModelImporterAnimationType.Generic ||
                           importer.materialImportMode != ModelImporterMaterialImportMode.None;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool loop = Array.IndexOf(LoopingClips, clip.name) >= 0;
                if (clip.loopTime != loop)
                {
                    clip.loopTime = loop;
                    changed = true;
                }
            }

            if (!changed && importer.clipAnimations.Length > 0)
                return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static Material GetOrCreateMaterial()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = "RogueHooded" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.mainTexture = texture;
            // Mate, como el resto de la escena.
            material.SetFloat("_Glossiness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildController()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();
            // Se construye en un archivo temporal y luego se copia encima del existente:
            // asi el .meta (y su GUID) se conserva y las escenas no pierden la referencia.
            AssetDatabase.DeleteAsset(TempControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(TempControllerPath);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("Dodge", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("DodgeDir", AnimatorControllerParameterType.Int);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Harvest", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HarvestStyle", AnimatorControllerParameterType.Int);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // Piernas: mezcla direccional respecto a donde mira el personaje. Permite
            // caminar hacia atras o de lado mientras se apunta, como pide el genero.
            BlendTree tree;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomocion", out tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveY";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip(clips, "Idle"), Vector2.zero);
            tree.AddChild(Clip(clips, "Running_A"), new Vector2(0f, 1f));
            tree.AddChild(Clip(clips, "Walking_Backwards"), new Vector2(0f, -1f));
            tree.AddChild(Clip(clips, "Running_Strafe_Left"), new Vector2(-1f, 0f));
            tree.AddChild(Clip(clips, "Running_Strafe_Right"), new Vector2(1f, 0f));
            machine.defaultState = locomotion;

            string[] dodgeClips = { "Dodge_Forward", "Dodge_Backward", "Dodge_Left", "Dodge_Right" };
            for (int i = 0; i < dodgeClips.Length; i++)
            {
                AnimatorState state = machine.AddState(dodgeClips[i]);
                state.motion = Clip(clips, dodgeClips[i]);
                // La esquiva dura 0,18 s; la animacion se acelera para no quedarse atras.
                state.speed = 2.5f;
                AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
                enter.AddCondition(AnimatorConditionMode.Equals, i, "DodgeDir");
                enter.duration = 0.03f;
                enter.canTransitionToSelf = false;
                AnimatorStateTransition exit = state.AddTransition(locomotion);
                exit.hasExitTime = true;
                exit.exitTime = 0.7f;
                exit.duration = 0.1f;
            }

            AnimatorState shoot = machine.AddState("Disparo");
            shoot.motion = Clip(clips, "1H_Ranged_Shoot");
            shoot.speed = 2f;
            AnimatorStateTransition shootEnter = machine.AddAnyStateTransition(shoot);
            shootEnter.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
            shootEnter.duration = 0.03f;
            shootEnter.canTransitionToSelf = true;
            AnimatorStateTransition shootExit = shoot.AddTransition(locomotion);
            shootExit.hasExitTime = true;
            shootExit.exitTime = 0.6f;
            shootExit.duration = 0.12f;

            // KayKit ya contiene las animaciones originales PickUp y
            // 1H_Melee_Attack_Chop. Las reutilizamos: ninguna animacion nueva
            // bloquea locomocion ni traslada la capsula.
            string[] gatherClips = { "PickUp", "1H_Melee_Attack_Chop" };
            string[] gatherStates = { "Recolectar a mano", "Picar roca" };
            for (int i = 0; i < gatherClips.Length; i++)
            {
                AnimatorState gather = machine.AddState(gatherStates[i]);
                gather.motion = Clip(clips, gatherClips[i]);
                gather.speed = i == 0 ? 1.65f : 1.9f;
                AnimatorStateTransition gatherEnter = machine.AddAnyStateTransition(gather);
                gatherEnter.AddCondition(AnimatorConditionMode.If, 0f, "Harvest");
                gatherEnter.AddCondition(AnimatorConditionMode.Equals, i, "HarvestStyle");
                gatherEnter.duration = 0.05f;
                gatherEnter.canTransitionToSelf = false;

                AnimatorStateTransition gatherExit = gather.AddTransition(locomotion);
                gatherExit.hasExitTime = true;
                gatherExit.exitTime = 0.75f;
                gatherExit.duration = 0.08f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
            {
                AssetDatabase.MoveAsset(TempControllerPath, ControllerPath);
                return;
            }
            System.IO.File.Copy(TempControllerPath, ControllerPath, true);
            AssetDatabase.DeleteAsset(TempControllerPath);
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
        }

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>();
            foreach (UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (item is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    result[clip.name] = clip;
            }
            return result;
        }

        private static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string name)
        {
            if (!clips.TryGetValue(name, out AnimationClip clip))
                throw new InvalidOperationException("El modelo KayKit no trae la animacion " + name);
            return clip;
        }
    }
}
