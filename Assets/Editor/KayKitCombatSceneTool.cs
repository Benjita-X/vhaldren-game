using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Genera una sala jugable a partir de Movement001 SIN modificar la escena original.
    /// Los modelos CC0 se mantienen en una carpeta independiente.
    /// </summary>
    public static class KayKitCombatSceneTool
    {
        private const string SourceScene = "Assets/Scenes/Movement001.unity";
        private const string CombatScene = "Assets/Scenes/KayKitCombat001.unity";
        private const string ModelFolder = "Assets/ThirdParty/KayKitDungeon/Models/";
        private const string MaterialPath = "Assets/ThirdParty/KayKitDungeon/KayKitAtlas.mat";

        [MenuItem("Vhaldren/Crear escena de prueba/Combate KayKit 001")]
        public static void CreateCombatScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Vhaldren] Sal de Play antes de crear la sala KayKit.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
                throw new InvalidOperationException("Falta la escena Movement001. Abre el proyecto completo.");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "barrel_small.obj") == null ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(ModelFolder + "dungeon_texture.png") == null)
                throw new InvalidOperationException("Faltan los modelos o textura de KayKit. Revisa Assets/ThirdParty.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatScene) != null &&
                !Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "Vhaldren - Sala de combate",
                    "KayKitCombat001 ya existe. ¿Reemplazar la escena generada?",
                    "Reemplazar", "Cancelar"))
                return;

            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            GameObject player = null;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                if (sceneRoot.name == "PlayerRoot")
                {
                    player = sceneRoot;
                    break;
                }
            }
            if (player == null)
                throw new InvalidOperationException("Movement001 no contiene PlayerRoot.");
            if (player.GetComponent<PlayerActionModifiers>() == null)
                player.AddComponent<PlayerActionModifiers>();
            Material atlas = GetOrCreateAtlasMaterial();

            GameObject root = new GameObject("KayKit Combat 001 - CC0");
            AddModel(root.transform, "barrel_small.obj", "Barril decorativo",
                new Vector3(-5f, 0f, -0.5f), atlas, new Vector3(0.9f, 1f, 0.9f), false);
            AddModel(root.transform, "pillar.obj", "Pilar decorativo",
                new Vector3(7.5f, 0f, 1f), atlas, new Vector3(1.1f, 3.2f, 1.1f), false);
            AddModel(root.transform, "torch_lit.obj", "Antorcha",
                new Vector3(-7f, 0.5f, 1f), atlas, Vector3.zero, false);

            GameObject chest = AddModel(root.transform, "chest.obj", "Cofre",
                new Vector3(5f, 0f, -1.5f), atlas, new Vector3(1.6f, 1.1f, 1f), false);
            AddModel(chest.transform, "chest_lid.obj", "Tapa",
                Vector3.zero, atlas, Vector3.zero, false);

            AddModel(root.transform, "barrel_small.obj", "Diana izquierda",
                new Vector3(-2.5f, 0f, 1.8f), atlas, new Vector3(1f, 1f, 1f), true);
            AddModel(root.transform, "barrel_small.obj", "Diana derecha",
                new Vector3(2.5f, 0f, 1.8f), atlas, new Vector3(1f, 1f, 1f), true);

            if (!EditorSceneManager.SaveScene(scene, CombatScene))
                throw new InvalidOperationException("No se pudo guardar la escena KayKitCombat001.");

            List<EditorBuildSettingsScene> buildScenes =
                new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int buildIndex = buildScenes.FindIndex(entry => entry.path == CombatScene);
            if (buildIndex < 0)
                buildScenes.Add(new EditorBuildSettingsScene(CombatScene, true));
            else if (!buildScenes[buildIndex].enabled)
                buildScenes[buildIndex] = new EditorBuildSettingsScene(CombatScene, true);
            EditorBuildSettings.scenes = buildScenes.ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log("[Vhaldren] KayKitCombat001 creada. En Play: WASD, Espacio y clic izquierdo sobre las dianas.");
        }

        private static GameObject AddModel(Transform parent, string assetName, string objectName,
            Vector3 localPosition, Material material, Vector3 colliderSize, bool damageable)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + assetName);
            if (asset == null)
                throw new InvalidOperationException("Falta modelo KayKit: " + assetName);

            // El padre es el objetivo logico; el modelo no controla fisica ni vida.
            GameObject wrapper = new GameObject(objectName);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = localPosition;

            GameObject visual = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (visual == null)
                throw new InvalidOperationException("No se pudo instanciar: " + assetName);
            visual.name = "Visual";
            visual.transform.SetParent(wrapper.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;

            // Los colliders de los modelos importados pueden diferir por formato.
            // Una sola caja logica por prop evita duplicaciones y simplifica los impactos.
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            if (colliderSize.sqrMagnitude > 0f)
            {
                BoxCollider box = wrapper.AddComponent<BoxCollider>();
                box.size = colliderSize;
                box.center = new Vector3(0f, colliderSize.y / 2f, 0f);
            }

            if (damageable)
            {
                wrapper.AddComponent<CombatHealth>();
                wrapper.AddComponent<CombatPracticeDummy>();
            }

            return wrapper;
        }

        private static Material GetOrCreateAtlasMaterial()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ModelFolder + "dungeon_texture.png");
            if (texture == null)
                throw new InvalidOperationException("No se encontro la textura CC0.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    throw new InvalidOperationException("No se encontro un shader compatible.");

                material = new Material(shader) { name = "KayKitAtlas" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
