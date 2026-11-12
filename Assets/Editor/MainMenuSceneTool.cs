using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Genera Assets/Scenes/MainMenu.unity: camara, luz rojiza, suelo de adoquin del
    /// altar, el obelisco runico girando de fondo y el MainMenuController. Lo pone
    /// como primera escena de la compilacion.
    /// </summary>
    public static class MainMenuSceneTool
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string WorldPath = "Assets/Scenes/WorldPrototype001.unity";

        [MenuItem("Vhaldren/Crear menú principal")]
        public static void CreateMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("MainCamera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.02f, 0.035f);
            camera.fieldOfView = 40f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(4.2f, 2.6f, -7.5f);
            cameraObject.transform.LookAt(new Vector3(1.2f, 2.2f, 0f));

            GameObject sun = new GameObject("Luz de luna");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.55f, 0.65f);
            light.intensity = 0.9f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            RenderSettings.ambientLight = new Color(0.12f, 0.08f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.03f, 0.05f);
            RenderSettings.fogDensity = 0.045f;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Suelo del altar";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            Material altar = AssetDatabase.LoadAssetAtPath<Material>("Assets/WorldPrototype/Materials/Altar.mat");
            if (altar != null)
                ground.GetComponent<Renderer>().sharedMaterial = altar;

            Transform showcase = null;
            GameObject obelisk = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Generados/obelisco_runico.fbx");
            if (obelisk != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(obelisk);
                instance.name = "Obelisco runico";
                instance.transform.position = new Vector3(1.2f, 0f, 0f);
                Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/WorldPrototype/Materials/Obelisco.mat");
                Material glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/WorldPrototype/Materials/RunasBrillo.mat");
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = renderer.name.Contains("Runas") ? glow : stone;
                showcase = instance.transform;
            }
            GameObject glowLight = new GameObject("Brillo de runas");
            Light rune = glowLight.AddComponent<Light>();
            rune.type = LightType.Point;
            rune.color = new Color(1f, 0.15f, 0.2f);
            rune.range = 7f;
            rune.intensity = 2.2f;
            glowLight.transform.position = new Vector3(1.2f, 2.6f, -0.8f);

            GameObject menu = new GameObject("Menu principal");
            MainMenuController controller = menu.AddComponent<MainMenuController>();
            SerializedObject data = new SerializedObject(controller);
            data.FindProperty("showcase").objectReferenceValue = showcase;
            data.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            if (scenes.All(s => s.path != WorldPath))
                scenes.Add(new EditorBuildSettingsScene(WorldPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Vhaldren] Menú principal creado y puesto como primera escena.");
        }
    }
}
