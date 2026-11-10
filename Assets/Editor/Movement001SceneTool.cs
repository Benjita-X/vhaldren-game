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
    /// Solo funciona en el editor. Incluye una prueba local de apuntado y disparo hitscan, sin dano ni red.
    /// </summary>
    public static class Movement001SceneTool
    {
        private const string ScenePath = "Assets/Scenes/Movement001.unity";
        private const string MaterialsFolder = "Assets/Materials";
        private const string ModelsFolder = "Assets/Models";
        private const string CapeMeshPath = ModelsFolder + "/Movement001_CapeMesh.asset";
        private const string GroundMaterialPath = MaterialsFolder + "/Movement001_Ground.mat";
        private const string PlayerMaterialPath = MaterialsFolder + "/Movement001_Player.mat";
        private const string ArmorMaterialPath = MaterialsFolder + "/Movement001_Armor.mat";
        private const string CapeMaterialPath = MaterialsFolder + "/Movement001_Cape.mat";
        private const string SkinMaterialPath = MaterialsFolder + "/Movement001_Skin.mat";
        private const string MetalMaterialPath = MaterialsFolder + "/Movement001_Metal.mat";
        private const string LeatherMaterialPath = MaterialsFolder + "/Movement001_Leather.mat";

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
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(ModelsFolder))
                AssetDatabase.CreateFolder("Assets", "Models");

            Material groundMaterial = GetOrCreateMaterial(GroundMaterialPath, "#8F2028");
            Material playerMaterial = GetOrCreateMaterial(PlayerMaterialPath, "#151515");
            Material armorMaterial = GetOrCreateMaterial(ArmorMaterialPath, "#292D3B");
            Material capeMaterial = GetOrCreateMaterial(CapeMaterialPath, "#641D2B");
            Material skinMaterial = GetOrCreateMaterial(SkinMaterialPath, "#C6B7AA");
            Material metalMaterial = GetOrCreateMaterial(MetalMaterialPath, "#9E8755");
            Material leatherMaterial = GetOrCreateMaterial(LeatherMaterialPath, "#3A2422");
            if (groundMaterial == null || playerMaterial == null || armorMaterial == null ||
                capeMaterial == null || skinMaterial == null || metalMaterial == null || leatherMaterial == null)
            {
                Debug.LogError("[Vhaldren] No se pudieron crear los materiales de Movement001.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Suelo: la superficie superior queda a Y = 0.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(32f, 1f, 32f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

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
            LocalPlayerShooter shooter = player.AddComponent<LocalPlayerShooter>();

            GameObject visual = new GameObject("VisualRoot");
            visual.transform.SetParent(player.transform, false);
            Mesh capeMesh = GetOrCreateCapeMesh();
            if (capeMesh == null)
            {
                Debug.LogError("[Vhaldren] No se pudo crear la malla de la capa.");
                return;
            }
            BuildDarkFantasyCharacter(visual.transform, playerMaterial, armorMaterial,
                capeMaterial, skinMaterial, metalMaterial, leatherMaterial, capeMesh);
            AssignObjectReference(motor, "visualRoot", visual.transform);

            GameObject shotOrigin = new GameObject("ShotOrigin");
            shotOrigin.transform.SetParent(visual.transform, false);
            shotOrigin.transform.localPosition = new Vector3(0f, 0.18f, 0.43f);

            // Marcador de apuntado va en el suelo para que sea legible desde la cámara elevada.
            GameObject facing = CreateVisualPart(PrimitiveType.Cube, "FacingIndicator", visual.transform,
                new Vector3(0f, -0.87f, 0.72f), new Vector3(0.16f, 0.07f, 0.38f), metalMaterial);
            facing.transform.localRotation = Quaternion.identity;

            GameObject aimMarker = CreateVisualPart(PrimitiveType.Cylinder, "AimMarker", null,
                new Vector3(0f, 0.025f, 2f), new Vector3(0.26f, 0.035f, 0.26f), metalMaterial);

            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            Camera view = cameraObject.AddComponent<Camera>();
            view.fieldOfView = 55f;
            cameraObject.AddComponent<AudioListener>();
            SimpleFollowCamera follow = cameraObject.AddComponent<SimpleFollowCamera>();
            follow.SetTarget(player.transform);
            AssignObjectReference(input, "viewCamera", view);
            AssignObjectReference(shooter, "viewCamera", view);
            AssignObjectReference(shooter, "shotOrigin", shotOrigin.transform);
            AssignObjectReference(shooter, "aimMarker", aimMarker.transform);

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
                      ". Pulsa Play: WASD mueve al instante, mouse apunta y clic izquierdo dispara al cursor.");
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
            GameObject aimMarker = FindSceneRoot(scene, "AimMarker");
            Transform visualTransform = player != null
                ? player.transform.Find("VisualRoot")
                : null;
            GameObject visual = visualTransform != null ? visualTransform.gameObject : null;

            if (player == null || visual == null || cameraObject == null || ground == null || wall == null ||
                aimMarker == null)
            {
                Debug.LogError("[Vhaldren] Escena incompleta: faltan objetos principales.");
                return;
            }

            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            LocalPlayerInput input = player.GetComponent<LocalPlayerInput>();
            LocalPlayerShooter shooter = player.GetComponent<LocalPlayerShooter>();
            CharacterController controller = player.GetComponent<CharacterController>();
            SimpleFollowCamera follow = cameraObject.GetComponent<SimpleFollowCamera>();
            Camera view = cameraObject.GetComponent<Camera>();
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            Renderer visualRenderer = visual.GetComponentInChildren<Renderer>();

            bool valid = motor != null && input != null && shooter != null && controller != null &&
                         follow != null && view != null &&
                         ground.GetComponent<Collider>() != null && wall.GetComponent<Collider>() != null &&
                         groundRenderer != null && groundRenderer.sharedMaterial != null &&
                         visualRenderer != null && visualRenderer.sharedMaterial != null &&
                         controller.radius > 0f && controller.height >= controller.radius * 2f &&
                         controller.skinWidth >= 0f && controller.skinWidth < controller.radius &&
                         follow.Target == player.transform &&
                         visual.transform.parent == player.transform &&
                         visual.GetComponent<Collider>() == null &&
                         visual.transform.Find("FacingIndicator") != null &&
                         visual.transform.Find("FacingIndicator").GetComponent<Collider>() == null &&
                         visual.transform.Find("ShotOrigin") != null &&
                         aimMarker.GetComponent<Collider>() == null &&
                         visual.GetComponentsInChildren<Collider>().Length == 0 &&
                         visual.GetComponentsInChildren<Renderer>().Length >= 10;

            valid = valid && motor != null && follow != null &&
                    HasValidMovementSettings(motor) && HasValidCameraSettings(follow);

            if (valid)
            {
                SerializedObject motorSerialized = new SerializedObject(motor);
                SerializedObject inputSerialized = new SerializedObject(input);
                SerializedObject shooterSerialized = new SerializedObject(shooter);
                SerializedProperty visualProperty = motorSerialized.FindProperty("visualRoot");
                SerializedProperty instantMovementProperty = motorSerialized.FindProperty("instantHorizontalMovement");
                SerializedProperty cameraProperty = inputSerialized.FindProperty("viewCamera");
                SerializedProperty shooterCameraProperty = shooterSerialized.FindProperty("viewCamera");
                SerializedProperty shotOriginProperty = shooterSerialized.FindProperty("shotOrigin");
                SerializedProperty aimMarkerProperty = shooterSerialized.FindProperty("aimMarker");
                valid = visualProperty != null && instantMovementProperty != null &&
                        instantMovementProperty.boolValue && cameraProperty != null &&
                        visualProperty.objectReferenceValue == visual.transform &&
                        cameraProperty.objectReferenceValue == view &&
                        shooterCameraProperty != null && shooterCameraProperty.objectReferenceValue == view &&
                        shotOriginProperty != null && shotOriginProperty.objectReferenceValue ==
                            visual.transform.Find("ShotOrigin") &&
                        aimMarkerProperty != null && aimMarkerProperty.objectReferenceValue == aimMarker.transform;
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

        private static void BuildDarkFantasyCharacter(Transform root, Material cloth, Material armor,
            Material cape, Material skin, Material metal, Material leather, Mesh capeMesh)
        {
            // Escala aproximada de dos metros: los pies quedan en Y=-1 y la cabeza en Y=1.
            GameObject capeObject = new GameObject("Cape");
            capeObject.transform.SetParent(root, false);
            capeObject.AddComponent<MeshFilter>().sharedMesh = capeMesh;
            capeObject.AddComponent<MeshRenderer>().sharedMaterial = cape;
            CreateVisualPart(PrimitiveType.Cube, "CapeHem", root,
                new Vector3(0f, -0.79f, -0.45f), new Vector3(0.68f, 0.07f, 0.06f), metal);
            for (int side = -1; side <= 1; side += 2)
            {
                CreateVisualPart(PrimitiveType.Cube, side < 0 ? "CapeLeftTrim" : "CapeRightTrim", root,
                    new Vector3(side * 0.31f, -0.13f, -0.405f), new Vector3(0.035f, 1.1f, 0.035f), metal);
            }
            CreateVisualPart(PrimitiveType.Cube, "CloakSigil", root,
                new Vector3(0f, 0.08f, -0.35f), new Vector3(0.13f, 0.22f, 0.045f), metal,
                Quaternion.Euler(0f, 0f, 45f));
            CreateVisualPart(PrimitiveType.Capsule, "Body", root,
                new Vector3(0f, 0.02f, 0f), new Vector3(0.64f, 0.62f, 0.42f), cloth);
            CreateVisualPart(PrimitiveType.Sphere, "ChestArmor", root,
                new Vector3(0f, 0.16f, 0.16f), new Vector3(0.62f, 0.42f, 0.25f), armor);
            CreateVisualPart(PrimitiveType.Cube, "Belt", root,
                new Vector3(0f, -0.31f, 0.05f), new Vector3(0.58f, 0.12f, 0.46f), leather);
            CreateVisualPart(PrimitiveType.Sphere, "BeltClasp", root,
                new Vector3(0f, -0.31f, 0.29f), new Vector3(0.14f, 0.14f, 0.06f), metal);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.18f;
                CreateVisualPart(PrimitiveType.Capsule, side < 0 ? "LeftLeg" : "RightLeg", root,
                    new Vector3(x, -0.57f, 0f), new Vector3(0.2f, 0.5f, 0.22f), armor,
                    Quaternion.Euler(0f, 0f, side * -3f));
                CreateVisualPart(PrimitiveType.Cube, side < 0 ? "LeftBoot" : "RightBoot", root,
                    new Vector3(x, -0.88f, 0.09f), new Vector3(0.27f, 0.2f, 0.42f), leather);

                CreateVisualPart(PrimitiveType.Capsule, side < 0 ? "LeftArm" : "RightArm", root,
                    new Vector3(side * 0.29f, 0.02f, 0.02f), new Vector3(0.17f, 0.56f, 0.18f), cloth,
                    Quaternion.Euler(0f, 0f, side * -12f));
                CreateVisualPart(PrimitiveType.Sphere, side < 0 ? "LeftShoulder" : "RightShoulder", root,
                    new Vector3(side * 0.28f, 0.34f, 0.03f), new Vector3(0.24f, 0.26f, 0.29f), armor);
                CreateVisualPart(PrimitiveType.Sphere, side < 0 ? "LeftPauldronTrim" : "RightPauldronTrim", root,
                    new Vector3(side * 0.28f, 0.42f, 0.05f), new Vector3(0.2f, 0.08f, 0.24f), metal);
            }

            CreateVisualPart(PrimitiveType.Capsule, "Neck", root,
                new Vector3(0f, 0.48f, 0.02f), new Vector3(0.21f, 0.2f, 0.2f), skin);
            CreateVisualPart(PrimitiveType.Sphere, "Head", root,
                new Vector3(0f, 0.68f, 0.04f), new Vector3(0.37f, 0.4f, 0.34f), skin);
            CreateVisualPart(PrimitiveType.Sphere, "Hair", root,
                new Vector3(0f, 0.84f, -0.015f), new Vector3(0.4f, 0.28f, 0.37f), cloth);
            CreateVisualPart(PrimitiveType.Cube, "HairFringe", root,
                new Vector3(0f, 0.73f, 0.2f), new Vector3(0.32f, 0.1f, 0.08f), cloth,
                Quaternion.Euler(0f, 0f, -7f));
            CreateVisualPart(PrimitiveType.Cube, "Collar", root,
                new Vector3(0f, 0.47f, 0.18f), new Vector3(0.46f, 0.18f, 0.17f), leather,
                Quaternion.Euler(-18f, 0f, 0f));
            CreateVisualPart(PrimitiveType.Sphere, "Clasp", root,
                new Vector3(0f, 0.37f, 0.28f), new Vector3(0.12f, 0.14f, 0.05f), metal);
        }

        private static Mesh GetOrCreateCapeMesh()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(CapeMeshPath);
            if (existing != null)
                return existing;

            // Trapezoidal, double-sided silhouette avoids a rectangular "cube person" look.
            Mesh mesh = new Mesh { name = "Movement001_CapeMesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.23f, 0.48f, -0.30f),
                new Vector3(-0.34f, -0.78f, -0.48f),
                new Vector3(0.34f, -0.78f, -0.48f),
                new Vector3(0.23f, 0.48f, -0.30f),
                new Vector3(-0.23f, 0.48f, -0.23f),
                new Vector3(-0.34f, -0.78f, -0.41f),
                new Vector3(0.34f, -0.78f, -0.41f),
                new Vector3(0.23f, 0.48f, -0.23f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, CapeMeshPath);
            return AssetDatabase.LoadAssetAtPath<Mesh>(CapeMeshPath);
        }

        private static GameObject CreateVisualPart(PrimitiveType type, string partName, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material, Quaternion? localRotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation ?? Quaternion.identity;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            DestroyCollider(part);
            return part;
        }

        private static Material GetOrCreateMaterial(string assetPath, string htmlColor)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return null;

            if (!ColorUtility.TryParseHtmlString(htmlColor, out Color color))
                throw new InvalidOperationException("Color de material no valido: " + htmlColor);

            Material material = new Material(shader);
            string colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (material.HasProperty(colorProperty))
                material.SetColor(colorProperty, color);
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static bool HasValidMovementSettings(PlayerMotor motor)
        {
            SerializedObject serialized = new SerializedObject(motor);
            SerializedProperty gravity = serialized.FindProperty("gravity");
            return IsNonNegativeFinite(serialized.FindProperty("movementSpeed")) &&
                   IsNonNegativeFinite(serialized.FindProperty("turningSpeedDegrees")) &&
                   gravity != null && IsFinite(gravity.floatValue) &&
                   IsNonNegativeFinite(serialized.FindProperty("acceleration")) &&
                   IsNonNegativeFinite(serialized.FindProperty("braking")) &&
                   IsNonNegativeFinite(serialized.FindProperty("reversalAcceleration"));
        }

        private static bool HasValidCameraSettings(SimpleFollowCamera follow)
        {
            SerializedObject serialized = new SerializedObject(follow);
            SerializedProperty offset = serialized.FindProperty("offset");
            SerializedProperty lookOffset = serialized.FindProperty("lookOffset");
            return offset != null && lookOffset != null &&
                   IsFinite(offset.vector3Value) && IsFinite(lookOffset.vector3Value) &&
                   IsNonNegativeFinite(serialized.FindProperty("followSharpness"));
        }

        private static bool IsNonNegativeFinite(SerializedProperty property)
        {
            return property != null && IsFinite(property.floatValue) && property.floatValue >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
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
