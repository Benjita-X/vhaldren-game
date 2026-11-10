using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Genera una escena minima y valida sus conexiones.
    /// Solo funciona en el editor. No agrega sistemas de combate ni red.
    /// </summary>
    public static class Movement001SceneTool
    {
        private const string ScenePath = "Assets/Scenes/Movement001.unity";

        [MenuItem("Vhaldren/Crear escena de prueba/Movimiento 001")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Vhaldren] Sal del modo Play antes de generar la escena.");
                return;
            }

            bool existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (existing && !Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "Vhaldren - Escena existente",
                    "La escena Movement001.unity ya existe. ¿Quieres reemplazarla?",
                    "Reemplazar", "Cancelar"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Suelo: la superficie superior queda a Y = 0.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(32f, 1f, 32f);

            // Obstaculo deliberado: permite comprobar colision lateral y deslizamiento.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WallObstacle";
            wall.transform.position = new Vector3(0f, 1f, 5f);
            wall.transform.localScale = new Vector3(5f, 2f, 1f);

            GameObject player = new GameObject("PlayerRoot");
            player.transform.position = new Vector3(0f, 1.01f, 0f);
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 2f;
            character.radius = 0.4f;
            character.center = Vector3.zero;

            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            LocalPlayerInput input = player.AddComponent<LocalPlayerInput>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "VisualRoot";
            visual.transform.SetParent(player.transform, false);
            DestroyCollider(visual);
            AssignObjectReference(motor, "visualRoot", visual.transform);

            // Marcador visible hacia delante: la capsula no tiene un frente reconocible.
            GameObject facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facing.name = "FacingIndicator";
            facing.transform.SetParent(visual.transform, false);
            facing.transform.localPosition = new Vector3(0f, 0.25f, 0.65f);
            facing.transform.localScale = new Vector3(0.22f, 0.22f, 0.55f);
            DestroyCollider(facing);

            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            Camera view = cameraObject.AddComponent<Camera>();
            view.fieldOfView = 55f;
            cameraObject.AddComponent<AudioListener>();
            SimpleFollowCamera follow = cameraObject.AddComponent<SimpleFollowCamera>();
            follow.SetTarget(player.transform);
            AssignObjectReference(input, "viewCamera", view);

            GameObject sun = new GameObject("DirectionalLight");
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[Vhaldren] No se pudo guardar " + ScenePath);
                return;
            }

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            ValidateScene();
            Debug.Log("[Vhaldren] Escena generada: " + ScenePath +
                      ". Pulsa Play para probar WASD y el indicador de apuntado.");
        }

        [MenuItem("Vhaldren/Validar escena 001")]
        public static void ValidateScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("[Vhaldren] Abre " + ScenePath + " para validarla.");
                return;
            }

            // No buscar globalmente: escenas aditivas pueden contener nombres repetidos.
            GameObject player = FindSceneRoot(scene, "PlayerRoot");
            GameObject cameraObject = FindSceneRoot(scene, "MainCamera");
            GameObject ground = FindSceneRoot(scene, "Ground");
            GameObject wall = FindSceneRoot(scene, "WallObstacle");
            Transform visualTransform = player != null
                ? player.transform.Find("VisualRoot")
                : null;
            GameObject visual = visualTransform != null ? visualTransform.gameObject : null;

            if (player == null || visual == null || cameraObject == null || ground == null || wall == null)
            {
                Debug.LogError("[Vhaldren] Escena incompleta: faltan objetos principales.");
                return;
            }

            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            LocalPlayerInput input = player.GetComponent<LocalPlayerInput>();
            CharacterController controller = player.GetComponent<CharacterController>();
            SimpleFollowCamera follow = cameraObject.GetComponent<SimpleFollowCamera>();
            Camera view = cameraObject.GetComponent<Camera>();

            bool valid = motor != null && input != null && controller != null &&
                         follow != null && view != null &&
                         ground.GetComponent<Collider>() != null && wall.GetComponent<Collider>() != null &&
                         follow.Target == player.transform &&
                         visual.transform.parent == player.transform &&
                         visual.GetComponent<Collider>() == null &&
                         visual.transform.Find("FacingIndicator") != null &&
                         visual.transform.Find("FacingIndicator").GetComponent<Collider>() == null;

            if (valid)
            {
                SerializedObject motorSerialized = new SerializedObject(motor);
                SerializedObject inputSerialized = new SerializedObject(input);
                SerializedProperty visualProperty = motorSerialized.FindProperty("visualRoot");
                SerializedProperty cameraProperty = inputSerialized.FindProperty("viewCamera");
                valid = visualProperty != null && cameraProperty != null &&
                        visualProperty.objectReferenceValue == visual.transform &&
                        cameraProperty.objectReferenceValue == view;
            }

            if (!valid)
                Debug.LogError("[Vhaldren] Hay conexiones o componentes incorrectos en Movement001.");
            else
                Debug.Log("[Vhaldren] Validacion estructural correcta. Falta probar compilacion y movimiento en Play.");
        }

        private static GameObject FindSceneRoot(Scene scene, string objectName)
        {
            // Se limita al nivel raiz de la escena activa, sin objetos de otras escenas.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                    return root;
            }
            return null;
        }

        private static void DestroyCollider(GameObject item)
        {
            Collider collider = item.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
        }

        private static void AssignObjectReference(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
                throw new InvalidOperationException("Campo serializado no encontrado: " + name);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
