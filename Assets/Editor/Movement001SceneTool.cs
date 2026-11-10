using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
        private const string StoneMaterialPath = MaterialsFolder + "/Movement001_Stone.mat";
        private const string AimMaterialPath = MaterialsFolder + "/Movement001_Aim.mat";
        private const string TexturesFolder = "Assets/Textures";
        private const string GridTexturePath = TexturesFolder + "/Movement001_Grid.png";
        private const float GroundSize = 40f;
        // La textura contiene 2 x 2 casillas; cada casilla mide 2 m en el mundo.
        private const float GridTextureMeters = 4f;

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
            if (!AssetDatabase.IsValidFolder(TexturesFolder))
                AssetDatabase.CreateFolder("Assets", "Textures");

            // Todo mate: con brillo, el reflejo del cielo lavaba los colores (el suelo rojo
            // se veia rosado, casi blanco). El personaje es oscuro sobre suelo rojo, y la
            // capa ya no es roja para que no se pierda contra el suelo.
            Material groundMaterial = GetOrCreateMaterial(GroundMaterialPath, "#8F2028");
            Material playerMaterial = GetOrCreateMaterial(PlayerMaterialPath, "#151515");
            Material armorMaterial = GetOrCreateMaterial(ArmorMaterialPath, "#292D3B");
            Material capeMaterial = GetOrCreateMaterial(CapeMaterialPath, "#1F1B33");
            Material skinMaterial = GetOrCreateMaterial(SkinMaterialPath, "#C6B7AA");
            Material metalMaterial = GetOrCreateMaterial(MetalMaterialPath, "#C9A856");
            Material leatherMaterial = GetOrCreateMaterial(LeatherMaterialPath, "#3A2422");
            Material stoneMaterial = GetOrCreateMaterial(StoneMaterialPath, "#4B5060");
            Material aimMaterial = GetOrCreateMaterial(AimMaterialPath, "#FFE39A", true);
            Texture2D gridTexture = GetOrCreateGridTexture();
            if (groundMaterial == null || playerMaterial == null || armorMaterial == null ||
                capeMaterial == null || skinMaterial == null || metalMaterial == null ||
                leatherMaterial == null || stoneMaterial == null || aimMaterial == null || gridTexture == null)
            {
                Debug.LogError("[Vhaldren] No se pudieron crear los materiales de Movement001.");
                return;
            }

            // La cuadricula es la referencia de movimiento: con la camara siguiendo al
            // personaje sobre un suelo liso no se nota si avanza ni a que velocidad.
            groundMaterial.mainTexture = gridTexture;
            groundMaterial.mainTextureScale = Vector2.one * (GroundSize / GridTextureMeters);
            EditorUtility.SetDirty(groundMaterial);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Luz ambiente plana y sin cielo: colores previsibles en cualquier equipo.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.35f, 0.42f);
            RenderSettings.skybox = null;
            RenderSettings.reflectionIntensity = 0f;

            // Suelo: la superficie superior queda a Y = 0.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(GroundSize, 1f, GroundSize);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            // Obstaculo deliberado: permite comprobar colision lateral y deslizamiento.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WallObstacle";
            wall.transform.position = new Vector3(0f, 1f, 5f);
            wall.transform.localScale = new Vector3(5f, 2f, 1f);
            wall.GetComponent<Renderer>().sharedMaterial = stoneMaterial;

            BuildTestCourse(stoneMaterial);

            GameObject player = new GameObject("PlayerRoot");
            // Altura de reposo: media capsula (1 m) + skinWidth (0,08 m). Empezar mas abajo
            // deja la capsula solapada con el suelo y el primer paso se pierde.
            player.transform.position = new Vector3(0f, 1.08f, 0f);
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 2f;
            character.radius = 0.4f;
            character.center = Vector3.zero;
            // Cero: el controlador no debe descartar pasos pequenos (ver PlayerMotor.Awake).
            character.minMoveDistance = 0f;

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
                new Vector3(0f, -1.02f, 0.78f), new Vector3(0.14f, 0.04f, 0.5f), aimMaterial);
            facing.transform.localRotation = Quaternion.identity;

            // Brillante y sin sombra para que se lea de un vistazo sobre cualquier superficie.
            GameObject aimMarker = CreateVisualPart(PrimitiveType.Cylinder, "AimMarker", null,
                new Vector3(0f, 0.025f, 3f), new Vector3(0.5f, 0.012f, 0.5f), aimMaterial);
            DisableShadows(aimMarker);
            DisableShadows(facing);

            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            Camera view = cameraObject.AddComponent<Camera>();
            view.fieldOfView = 50f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.07f, 0.065f, 0.09f);
            cameraObject.AddComponent<AudioListener>();
            SimpleFollowCamera follow = cameraObject.AddComponent<SimpleFollowCamera>();
            // Mas cerca y algo mas cenital que el valor por defecto: el personaje ocupa mas pantalla.
            AssignVector(follow, "offset", new Vector3(0f, 10f, -8f));
            follow.SetTarget(player.transform);
            AssignObjectReference(input, "viewCamera", view);
            AssignObjectReference(shooter, "viewCamera", view);
            AssignObjectReference(shooter, "shotOrigin", shotOrigin.transform);
            AssignObjectReference(shooter, "aimMarker", aimMarker.transform);

            GameObject sun = new GameObject("DirectionalLight");
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.05f;
            sunLight.color = new Color(1f, 0.96f, 0.9f);
            // La sombra ancla al personaje al suelo: sin ella parece flotar.
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[Vhaldren] No se pudo guardar " + ScenePath);
                return;
            }

            EnsureSceneInBuildSettings();
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
            GameObject course = FindSceneRoot(scene, "TestCourse");
            Transform visualTransform = player != null
                ? player.transform.Find("VisualRoot")
                : null;
            GameObject visual = visualTransform != null ? visualTransform.gameObject : null;

            if (player == null || visual == null || cameraObject == null || ground == null || wall == null ||
                aimMarker == null || course == null)
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
                         controller.minMoveDistance <= 0f &&
                         course.GetComponentsInChildren<Collider>().Length >= 8 &&
                         groundRenderer.sharedMaterial.mainTexture != null &&
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
                        cameraProperty != null &&
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

        /// <summary>Sin la escena en Build Settings, una build saldria vacia.</summary>
        private static void EnsureSceneInBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene item in scenes)
            {
                if (item.path == ScenePath)
                    return;
            }

            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
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

        /// <summary>
        /// Circuito de prueba alrededor del punto de partida. Cada pieza comprueba una cosa
        /// distinta del control: deslizarse en diagonal, rodear algo redondo, pasar por un
        /// hueco estrecho, subir un escalon y subir una rampa y caer de ella.
        /// </summary>
        private static void BuildTestCourse(Material stone)
        {
            Transform course = new GameObject("TestCourse").transform;

            CreateObstacle(PrimitiveType.Cube, "DiagonalWall", course,
                new Vector3(-7f, 1f, 4f), new Vector3(6f, 2f, 0.6f), stone, Quaternion.Euler(0f, 45f, 0f));

            CreateObstacle(PrimitiveType.Cylinder, "PillarA", course,
                new Vector3(6f, 1f, 2f), new Vector3(1.2f, 1f, 1.2f), stone);
            CreateObstacle(PrimitiveType.Cylinder, "PillarB", course,
                new Vector3(8.5f, 1f, 4.5f), new Vector3(1.2f, 1f, 1.2f), stone);
            CreateObstacle(PrimitiveType.Cylinder, "PillarC", course,
                new Vector3(6f, 1f, 7f), new Vector3(1.2f, 1f, 1.2f), stone);

            // Hueco de 1,4 m entre dos bloques: el personaje (0,8 m de ancho) debe pasar.
            CreateObstacle(PrimitiveType.Cube, "GapLeft", course,
                new Vector3(-1.9f, 1f, -6f), new Vector3(2.4f, 2f, 1f), stone);
            CreateObstacle(PrimitiveType.Cube, "GapRight", course,
                new Vector3(1.9f, 1f, -6f), new Vector3(2.4f, 2f, 1f), stone);

            // Escalon de 25 cm (menos que stepOffset = 0,3): se sube caminando.
            CreateObstacle(PrimitiveType.Cube, "StepPlatform", course,
                new Vector3(-9f, 0.125f, -6f), new Vector3(5f, 0.25f, 5f), stone);

            // Rampa de 12 grados que sube hacia +Z y termina en el aire: pendiente y caida.
            CreateObstacle(PrimitiveType.Cube, "Ramp", course,
                new Vector3(9f, 0.53f, -6f), new Vector3(4f, 0.4f, 7f), stone, Quaternion.Euler(-12f, 0f, 0f));
        }

        private static GameObject CreateObstacle(PrimitiveType type, string obstacleName, Transform parent,
            Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            GameObject obstacle = GameObject.CreatePrimitive(type);
            obstacle.name = obstacleName;
            obstacle.transform.SetParent(parent, false);
            obstacle.transform.localPosition = position;
            obstacle.transform.localRotation = rotation ?? Quaternion.identity;
            obstacle.transform.localScale = scale;
            obstacle.GetComponent<Renderer>().sharedMaterial = material;
            return obstacle;
        }

        private static void DisableShadows(GameObject item)
        {
            Renderer renderer = item.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Texture2D GetOrCreateGridTexture()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
            if (existing != null)
                return existing;

            // Damero suave de 2 x 2 casillas con linea en los bordes. Es una escala de grises
            // que multiplica el color del material, asi el suelo conserva su color.
            const int size = 256;
            const int cell = size / 2;
            const int line = 3;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool lightCell = (x < cell) == (y < cell);
                    byte value = lightCell ? (byte)255 : (byte)210;
                    int cellX = x % cell;
                    int cellY = y % cell;
                    if (cellX < line || cellY < line || cellX >= cell - line || cellY >= cell - line)
                        value = 120;
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(GridTexturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(GridTexturePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(GridTexturePath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
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

        /// <summary>
        /// Crea el material o actualiza el existente: regenerar la escena aplica siempre los
        /// colores de este archivo y conserva el GUID del material.
        /// </summary>
        private static Material GetOrCreateMaterial(string assetPath, string htmlColor, bool glowing = false)
        {
            if (!ColorUtility.TryParseHtmlString(htmlColor, out Color color))
                throw new InvalidOperationException("Color de material no valido: " + htmlColor);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader == null)
                    return null;

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            string colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (material.HasProperty(colorProperty))
                material.SetColor(colorProperty, color);

            // Mate: sin brillo especular ni reflejo del entorno.
            SetFloatIfPresent(material, "_Glossiness", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0f);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_SpecularHighlights", 0f);
            SetFloatIfPresent(material, "_GlossyReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");

            if (glowing && material.HasProperty("_EmissionColor"))
            {
                // Base oscura: el color lo pone la emision y no se quema a blanco con la luz.
                if (material.HasProperty(colorProperty))
                    material.SetColor(colorProperty, new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 1f));
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void AssignVector(UnityEngine.Object target, string name, Vector3 value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
                throw new InvalidOperationException("Campo serializado no encontrado: " + name);
            property.vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
