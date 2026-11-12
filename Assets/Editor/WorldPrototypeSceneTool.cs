using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Mundo de prueba generado en una escena separada: bosque, ruinas y altar.
    /// Usa la escena de movimiento ya probada como base, sin modificarla.
    /// Assets y posiciones son deterministas: misma entrada, mismo resultado.
    /// </summary>
    public static class WorldPrototypeSceneTool
    {
        private const string Source = "Assets/Scenes/Movement001.unity";
        private const string Destination = "Assets/Scenes/WorldPrototype001.unity";
        private const string MaterialFolder = "Assets/WorldPrototype/Materials";
        private const string KayKitFolder = "Assets/ThirdParty/KayKitDungeon/Models/";
        private const string AtlasPath = "Assets/ThirdParty/KayKitDungeon/KayKitAtlas.mat";

        [MenuItem("Vhaldren/Crear mundo jugable 008 (bosque + ruinas + jefe)")]
        public static void CreateWorld()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Vhaldren] Sal de Play antes de crear el mundo.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null)
                throw new InvalidOperationException("No se encuentra Movement001.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null &&
                !Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "Vhaldren: generar mundo",
                    "WorldPrototype001 ya existe. ¿Reemplazar solamente esa escena?",
                    "Reemplazar", "Cancelar"))
                return;

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            GameObject player = GetRoot(scene, "PlayerRoot");
            GameObject camera = GetRoot(scene, "MainCamera");
            if (player == null || camera == null)
                throw new InvalidOperationException("La escena base no contiene jugador o camara.");

            // Convertir PRIMERO la escena activa en el destino. Si una importacion
            // o generacion falla, la escena Movement001 original sigue intacta.
            if (!EditorSceneManager.SaveScene(scene, Destination))
                throw new InvalidOperationException("No se pudo crear una copia segura del mundo.");

            // Borrar solo el circuito de pruebas DENTRO de la copia en memoria.
            foreach (string name in new[] { "Ground", "WallObstacle", "TestCourse" })
            {
                GameObject old = GetRoot(scene, name);
                if (old != null)
                    UnityEngine.Object.DestroyImmediate(old);
            }

            player.transform.position = new Vector3(0f, 1.08f, 0f);
            CombatHealth playerHealth = player.GetComponent<CombatHealth>();
            if (playerHealth == null)
                playerHealth = player.AddComponent<CombatHealth>();
            SetFloat(playerHealth, "maximumHealth", 150f);
            playerHealth.ResetToFull();
            CombatDamageFeedback playerFeedback = player.GetComponent<CombatDamageFeedback>();
            if (playerFeedback == null)
                playerFeedback = player.AddComponent<CombatDamageFeedback>();
            playerFeedback.ConfigureForPlayer();
            // Supervivencia 015: inventario, construccion (B), recoleccion (E) e interfaz.
            SurvivalInventory survival = player.GetComponent<SurvivalInventory>();
            if (survival == null)
                survival = player.AddComponent<SurvivalInventory>();
            // Nuevas escenas comienzan sin recursos magicos de demostracion:
            // el jugador recoge madera/fibra, fabrica picos y extrae mineral.
            SerializedObject survivalConfig = new SerializedObject(survival);
            SerializedProperty demoSupplies = survivalConfig.FindProperty("grantDemoSupplies");
            if (demoSupplies != null)
            {
                demoSupplies.boolValue = false;
                survivalConfig.ApplyModifiedPropertiesWithoutUndo();
            }
            if (player.GetComponent<SurvivalToolbelt>() == null)
                player.AddComponent<SurvivalToolbelt>();
            if (player.GetComponent<PlayerBuilder>() == null)
                player.AddComponent<PlayerBuilder>();
            if (player.GetComponent<PlayerGatherer>() == null)
                player.AddComponent<PlayerGatherer>();
            if (player.GetComponent<SurvivalHud>() == null)
                player.AddComponent<SurvivalHud>();

            // Texturas CC0 de Poly Haven con proyeccion triplanar (Assets/ThirdParty/PolyHaven).
            Material ground = TexMat("SueloBasico", "brown_mud_rocks_01", new Color(0.72f, 0.68f, 0.66f), 5f);
            Material forest = TexMat("Bosque", "forest_leaves_02", new Color(0.62f, 0.7f, 0.58f), 4f);
            Material ruins = TexMat("Ruinas", "aerial_rocks_02", new Color(0.68f, 0.68f, 0.74f), 6f);
            Material crimson = TexMat("Altar", "cobblestone_floor_13", new Color(0.78f, 0.42f, 0.45f), 3f);
            Material path = TexMat("Sendero", "cobblestone_floor_08", new Color(0.85f, 0.8f, 0.76f), 3f);
            Material stone = TexMat("Piedra", "castle_brick_07", new Color(0.8f, 0.8f, 0.84f), 2.5f);
            Material bark = TexMat("Tronco", "bark_brown_02", new Color(0.8f, 0.72f, 0.68f), 1.5f);
            Material leaves = TexMat("Hojas", "forest_leaves_02", new Color(0.45f, 0.65f, 0.42f), 2f);
            Material castleWall = TexMat("MuroCastillo", "castle_brick_broken_06", new Color(0.75f, 0.75f, 0.78f), 3f);
            Material obelisk = TexMat("Obelisco", "castle_brick_02_red", new Color(0.85f, 0.6f, 0.62f), 1.5f);
            Material wildRock = TexMat("RocaNatural", "dark_rock", new Color(0.85f, 0.85f, 0.88f), 2f);
            Material red = Mat("Enemigo", new Color(0.47f, 0.13f, 0.2f));
            Material purple = Mat("Jefe", new Color(0.42f, 0.16f, 0.52f));
            Material atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasPath);

            GameObject world = new GameObject("World008 - 92x92 - Dark Fantasy");
            Transform root = world.transform;
            Transform environment = ChildRoot(root, "Terreno y regiones");
            Transform props = ChildRoot(root, "Naturaleza y ruinas");
            Transform enemies = ChildRoot(root, "Enemigos");

            // Terreno de Unity (como en el video de LuisCanary, pero generado por codigo):
            // zona de juego plana dentro de los muros, montanas fuera y texturas pintadas
            // por region. Se llama "Suelo fisico" para que el validador lo reconozca.
            BuildTerrain(environment);
            BackdropForest(props, bark, leaves);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.16f, 0.15f, 0.19f);
            RenderSettings.fogDensity = 0.011f;

            // Limites fisicos: el usuario no puede caer al vacio durante esta prueba.
            Primitive("Muro norte", PrimitiveType.Cube, props,
                new Vector3(0f, 3f, 46f), new Vector3(92f, 6f, 1.5f), castleWall, true);
            Primitive("Muro sur", PrimitiveType.Cube, props,
                new Vector3(0f, 3f, -46f), new Vector3(92f, 6f, 1.5f), castleWall, true);
            Primitive("Muro este", PrimitiveType.Cube, props,
                new Vector3(46f, 3f, 0f), new Vector3(1.5f, 6f, 92f), castleWall, true);
            Primitive("Muro oeste", PrimitiveType.Cube, props,
                new Vector3(-46f, 3f, 0f), new Vector3(1.5f, 6f, 92f), castleWall, true);

            // Bosque de la zona oeste. Se deja un corredor central transitable.
            System.Random rng = new System.Random(20261111);
            for (int i = 0; i < 25; i++)
            {
                float x = -43f + (float)rng.NextDouble() * 26f;
                float z = -40f + (float)rng.NextDouble() * 78f;
                // Zona de entrada y ruta a la diana libre de arboles densos.
                if (x > -24f && Mathf.Abs(z) < 9f)
                    continue;
                // Dejar espacio alrededor de los guardianes para que no
                // aparezcan atrapados dentro de un tronco aleatorio.
                if ((new Vector2(x + 21f, z + 7f)).sqrMagnitude < 25f ||
                    (new Vector2(x + 34f, z - 12f)).sqrMagnitude < 25f)
                    continue;
                // Claros de los campamentos 019 (manada de lobos y cripta).
                if ((new Vector2(x + 34f, z + 30f)).sqrMagnitude < 64f ||
                    (new Vector2(x + 32f, z - 34f)).sqrMagnitude < 64f)
                    continue;
                float size = 0.75f + (float)rng.NextDouble() * 0.8f;
                Tree(props, new Vector3(x, 0f, z), size, bark, leaves);
            }

            ResourceDeposits(props, wildRock, leaves);

            // Ruinas a la derecha: posiciones predecibles, fuera del corredor principal.
            for (int i = 0; i < 16; i++)
            {
                float x = 17f + (i % 4) * 7.5f;
                float z = -36f + (i / 4) * 19f;
                if (atlas != null && i % 3 == 0)
                    KayKit("Columna antigua " + i, "pillar.obj", props,
                        new Vector3(x, 0f, z), atlas, new Vector3(1.2f, 3.2f, 1.2f));
                else
                    Primitive("Bloque en ruinas " + i, PrimitiveType.Cube, props,
                        new Vector3(x, 0.8f, z), new Vector3(1.8f, 1.6f, 1.8f),
                        stone, true);
            }

            // Arco de entrada y altar del jefe.
            for (int i = 0; i < 8; i++)
            {
                float angle = Mathf.PI * 2f * i / 8f;
                Vector3 pos = new Vector3(Mathf.Sin(angle) * 12f, 0f,
                    30f + Mathf.Cos(angle) * 12f);
                if (atlas != null)
                    KayKit("Pilar del ritual " + i, "pillar.obj", props, pos, atlas,
                        new Vector3(1.1f, 3.2f, 1.1f));
                else
                    Primitive("Pilar del ritual " + i, PrimitiveType.Cube, props,
                        pos + Vector3.up * 1.6f, new Vector3(1.1f, 3.2f, 1.1f), stone, true);
            }

            if (!GeneratedModel("Obelisco de sangre", ObeliskModelPath, props, new Vector3(0f, 0f, 41f),
                    obelisk, RuneGlow(), new Vector3(2.6f, 5.2f, 2.6f)))
                Primitive("Obelisco de sangre", PrimitiveType.Cube, props,
                    new Vector3(0f, 1.7f, 41f), new Vector3(2.8f, 3.4f, 2.8f), obelisk, true);
            if (atlas != null)
            {
                KayKit("Cofre del bosque", "chest.obj", props,
                    new Vector3(-31f, 0f, -23f), atlas, new Vector3(1.6f, 1.1f, 1f));
                KayKit("Barril ruinas", "barrel_small.obj", props,
                    new Vector3(32f, 0f, -19f), atlas, new Vector3(1f, 1f, 1f));
            }

            // Seis encuentros que pueden probarse desde ahora con el disparo existente.
            Enemy("Acechador del bosque A", enemies, player.transform, new Vector3(-21f, 1.08f, -7f), red, 55f, 8f, 3.2f, false);
            Enemy("Acechador del bosque B", enemies, player.transform, new Vector3(-34f, 1.08f, 12f), red, 55f, 8f, 3.2f, false);
            Enemy("Centinela de ruinas A", enemies, player.transform, new Vector3(21f, 1.08f, -13f), red, 75f, 11f, 2.8f, false);
            Enemy("Centinela de ruinas B", enemies, player.transform, new Vector3(32f, 1.08f, 14f), red, 75f, 11f, 2.8f, false);
            Enemy("Guardian del altar", enemies, player.transform, new Vector3(-9f, 1.08f, 24f), red, 90f, 12f, 3.0f, false);
            Enemy("JEFE - Caballero de sangre", enemies, player.transform, new Vector3(0f, 1.08f, 32f),
                purple, 320f, 18f, 2.5f, true);
            // Modelos KayKit animados (CC0) en lugar de capsulas.
            AttachModel(enemies, "Acechador del bosque A", "Rogue", KayKitEnemySetup.AttackStyle.Melee);
            AttachModel(enemies, "Acechador del bosque B", "Rogue", KayKitEnemySetup.AttackStyle.Melee);
            AttachModel(enemies, "Centinela de ruinas A", "Knight", KayKitEnemySetup.AttackStyle.Melee);
            AttachModel(enemies, "Centinela de ruinas B", "Knight", KayKitEnemySetup.AttackStyle.Melee);
            AttachModel(enemies, "Guardian del altar", "Knight", KayKitEnemySetup.AttackStyle.Heavy);
            AttachModel(enemies, "JEFE - Caballero de sangre", "Knight", KayKitEnemySetup.AttackStyle.Heavy);

            // Campamentos del bestiario 019: cada uno enseña un rol y lleva su esencia.
            foreach (Camp camp in Camps)
                ArchetypeEnemy(camp, enemies, player.transform, red);

            PrototypeGameSession session = world.AddComponent<PrototypeGameSession>();
            // Guardar la referencia real en la escena. No depender de una
            // busqueda global por nombre cuando se cargan varias escenas.
            SerializedObject sessionData = new SerializedObject(session);
            sessionData.FindProperty("player").objectReferenceValue = player.transform;
            sessionData.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, Destination))
                throw new InvalidOperationException("No se pudo guardar WorldPrototype001.");

            List<EditorBuildSettingsScene> settings =
                new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!settings.Exists(s => s.path == Destination))
                settings.Add(new EditorBuildSettingsScene(Destination, true));
            else
            {
                int i = settings.FindIndex(s => s.path == Destination);
                settings[i] = new EditorBuildSettingsScene(Destination, true);
            }
            EditorBuildSettings.scenes = settings.ToArray();
            AssetDatabase.SaveAssets();
            if (!ValidateWorldScene(false))
                throw new InvalidOperationException("El mundo generado no supero la validacion estructural.");
            Debug.Log("[Vhaldren] Mundo 008 generado: abre WorldPrototype001. " +
                "Recorre bosque, ruinas y altar, combate 6 enemigos y 5 campamentos del bestiario, y usa Q para curarte.");
        }

        [MenuItem("Vhaldren/Validar mundo jugable 008")]
        public static void ValidateWorld()
        {
            ValidateWorldScene(true);
        }

        private static bool ValidateWorldScene(bool showSuccess)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Destination)
            {
                Debug.LogError("[Vhaldren] Abre WorldPrototype001 para validar el mapa.");
                return false;
            }

            GameObject player = GetRoot(scene, "PlayerRoot");
            GameObject camera = GetRoot(scene, "MainCamera");
            GameObject world = GetRoot(scene, "World008 - 92x92 - Dark Fantasy");
            if (player == null || camera == null || world == null)
            {
                Debug.LogError("[Vhaldren] Faltan el jugador, la camara o el mundo generado.");
                return false;
            }

            PrototypeGameSession session = world.GetComponent<PrototypeGameSession>();
            Transform ground = world.transform.Find("Terreno y regiones/Suelo fisico");
            PrototypeEnemy[] enemies = world.GetComponentsInChildren<PrototypeEnemy>(true);
            CombatHealth health = player.GetComponent<CombatHealth>();
            bool valid = player.GetComponent<PlayerMotor>() != null &&
                player.GetComponent<LocalPlayerShooter>() != null &&
                player.GetComponent<PlayerBuilder>() != null &&
                player.GetComponent<PlayerGatherer>() != null &&
                player.GetComponent<CharacterController>() != null &&
                health != null && health.MaximumHealth >= 150f &&
                camera.GetComponent<Camera>() != null &&
                session != null &&
                ground != null && ground.GetComponent<Collider>() != null &&
                enemies.Length == 6 + Camps.Length;

            int bosses = 0;
            foreach (PrototypeEnemy enemy in enemies)
            {
                if (enemy == null || enemy.GetComponent<CharacterController>() == null ||
                    enemy.GetComponent<CombatHealth>() == null)
                    valid = false;
                if (enemy != null)
                {
                    if (enemy.IsBoss)
                        bosses++;
                    SerializedProperty targetProperty =
                        new SerializedObject(enemy).FindProperty("target");
                    if (targetProperty == null || targetProperty.objectReferenceValue != player.transform)
                        valid = false;
                }
            }
            valid = valid && bosses == 1;
            if (session != null)
            {
                SerializedProperty playerProperty =
                    new SerializedObject(session).FindProperty("player");
                valid = valid && playerProperty != null &&
                    playerProperty.objectReferenceValue == player.transform;
            }

            if (!valid)
                Debug.LogError("[Vhaldren] Validacion fallida: revisar vida, enemigos, suelo o referencias.");
            else if (showSuccess)
                Debug.Log("[Vhaldren] Estructura del mundo correcta: 6 enemigos, jefe, suelo y referencias.");
            return valid;
        }

        private static Transform ChildRoot(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject GetRoot(Scene scene, string name)
        {
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.name == name)
                    return candidate;
            return null;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/WorldPrototype"))
                AssetDatabase.CreateFolder("Assets", "WorldPrototype");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/WorldPrototype", "Materials");
        }

        private const string PolyHavenFolder = "Assets/ThirdParty/PolyHaven";
        private const string ObeliskModelPath = "Assets/Models/Generados/obelisco_runico.fbx";

        /// <summary>Material emisivo para runas de modelos propios hechos en Blender.</summary>
        private static Material RuneGlow()
        {
            Material glow = Mat("RunasBrillo", new Color(0.35f, 0.02f, 0.05f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1.6f, 0.12f, 0.18f));
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(glow);
            return glow;
        }

        /// <summary>
        /// Coloca un modelo propio exportado desde Blender (Tools/Blender). La piedra usa
        /// el material triplanar y los objetos llamados "Runas" el material emisivo.
        /// Devuelve false si el FBX no existe, para caer a la primitiva de siempre.
        /// </summary>
        private static bool GeneratedModel(string name, string path, Transform parent, Vector3 position,
            Material stoneMaterial, Material glowMaterial, Vector3 colliderSize)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                return false;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = name;
            instance.transform.position = position;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = renderer.name.Contains("Runas") ? glowMaterial : stoneMaterial;
            BoxCollider box = instance.AddComponent<BoxCollider>();
            box.size = colliderSize;
            box.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
            return true;
        }

        /// <summary>
        /// Material triplanar con textura de Poly Haven. Reutiliza el asset existente
        /// (mismo GUID, la escena no pierde referencias) y solo cambia shader y mapas.
        /// Si faltan las texturas, cae al color plano de siempre.
        /// </summary>
        private static Material TexMat(string name, string textureId, Color tint, float metersPerTile)
        {
            Material material = Mat(name, tint);
            Shader triplanar = Shader.Find("Vhaldren/Triplanar");
            string colorPath = PolyHavenFolder + "/" + textureId + "/color.jpg";
            string normalPath = PolyHavenFolder + "/" + textureId + "/normal.jpg";
            Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            if (triplanar == null || color == null)
                return material;
            TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            material.shader = triplanar;
            material.SetTexture("_MainTex", color);
            if (normal != null)
                material.SetTexture("_BumpMap", normal);
            material.SetColor("_Color", tint);
            material.SetFloat("_Tile", metersPerTile);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Mat(string name, Color color)
        {
            EnsureFolders();
            string assetPath = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    throw new InvalidOperationException("No hay un shader compatible.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetFloat(Component component, string field, float value)
        {
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException("No se encontro campo: " + field);
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Primitive(string name, PrimitiveType type,
            Transform parent, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            Renderer renderer = item.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            if (!collider)
            {
                Collider body = item.GetComponent<Collider>();
                if (body != null)
                    UnityEngine.Object.DestroyImmediate(body);
            }
            else
                item.isStatic = true;
            return item;
        }

        // -- Terreno ------------------------------------------------------
        private const string TerrainFolder = "Assets/WorldPrototype/Terrain";
        private const float TerrainSize = 192f;
        private const float TerrainHeight = 26f;
        private const float TerrainBase = 1f;      // metros bajo el suelo jugable
        private const float PlayableHalf = 46f;    // dentro de los muros: plano

        /// <summary>Altura del terreno en coordenadas del mundo (0 = suelo jugable).</summary>
        public static float TerrainHeightAt(float x, float z)
        {
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            if (edge <= PlayableHalf + 0.5f)
                return 0f;
            float t = Mathf.Clamp01((edge - PlayableHalf - 0.5f) / 16f);
            t = t * t * (3f - 2f * t);
            float ridges = Mathf.PerlinNoise(x * 0.028f + 13.1f, z * 0.028f + 7.7f);
            float detail = Mathf.PerlinNoise(x * 0.09f + 3.3f, z * 0.09f + 9.1f);
            return t * (5f + 15f * ridges + 2.5f * detail);
        }

        private static TerrainLayer Layer(string name, string textureId, float tile, Color tint)
        {
            string path = TerrainFolder + "/" + name + ".terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            string folder = PolyHavenFolder + "/" + textureId;
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/color.jpg");
            TextureImporter normalImporter = AssetImporter.GetAtPath(folder + "/normal.jpg") as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/normal.jpg");
            layer.tileSize = new Vector2(tile, tile);
            layer.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f);
            layer.smoothness = 0.08f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void BuildTerrain(Transform parent)
        {
            if (!AssetDatabase.IsValidFolder(TerrainFolder))
                AssetDatabase.CreateFolder("Assets/WorldPrototype", "Terrain");
            string dataPath = TerrainFolder + "/TerrenoMundo.asset";
            AssetDatabase.DeleteAsset(dataPath);
            TerrainData data = new TerrainData { heightmapResolution = 257, alphamapResolution = 256 };
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            AssetDatabase.CreateAsset(data, dataPath);

            int hr = data.heightmapResolution;
            float[,] heights = new float[hr, hr];
            for (int zi = 0; zi < hr; zi++)
                for (int xi = 0; xi < hr; xi++)
                {
                    float x = -TerrainSize * 0.5f + TerrainSize * xi / (hr - 1);
                    float z = -TerrainSize * 0.5f + TerrainSize * zi / (hr - 1);
                    heights[zi, xi] = (TerrainHeightAt(x, z) + TerrainBase) / TerrainHeight;
                }
            data.SetHeights(0, 0, heights);

            data.terrainLayers = new[]
            {
                Layer("Tierra", "brown_mud_rocks_01", 5f, new Color(0.78f, 0.74f, 0.72f)),
                Layer("Hojarasca", "forest_leaves_02", 4f, new Color(0.7f, 0.78f, 0.64f)),
                Layer("RocaRuinas", "aerial_rocks_02", 6f, new Color(0.75f, 0.75f, 0.8f)),
                Layer("Adoquin", "cobblestone_floor_08", 3f, new Color(0.9f, 0.85f, 0.8f)),
                Layer("AdoquinAltar", "cobblestone_floor_13", 3f, new Color(0.85f, 0.45f, 0.48f)),
                Layer("Montana", "dark_rock", 7f, new Color(0.7f, 0.7f, 0.75f)),
            };

            int ar = data.alphamapResolution;
            float[,,] alpha = new float[ar, ar, 6];
            for (int zi = 0; zi < ar; zi++)
                for (int xi = 0; xi < ar; xi++)
                {
                    float x = -TerrainSize * 0.5f + TerrainSize * (xi + 0.5f) / ar;
                    float z = -TerrainSize * 0.5f + TerrainSize * (zi + 0.5f) / ar;
                    // Bordes irregulares: el ruido desplaza los limites unos metros.
                    float wobble = (Mathf.PerlinNoise(x * 0.12f + 5f, z * 0.12f + 2f) - 0.5f) * 5f;
                    float[] w = new float[6];
                    float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    if (edge > PlayableHalf + 1f)
                    {
                        float slope = Mathf.Clamp01(TerrainHeightAt(x, z) / 6f);
                        w[5] = slope;
                        w[1] = 1f - slope;
                    }
                    else
                    {
                        w[0] = 1f;
                        w[1] = Band(-x - 13f + wobble, 4f);
                        w[2] = Band(x - 13f + wobble, 4f);
                        bool road = Mathf.Abs(x) < 6.5f + wobble * 0.25f && z > -39f && z < 35f;
                        bool cross = Mathf.Abs(z) < 3.5f + wobble * 0.2f && Mathf.Abs(x) < 19f;
                        bool patio = Mathf.Abs(x) < 12.5f + wobble * 0.3f && z > 17.5f && z < 43f;
                        if (patio) w[4] = 1.4f;
                        else if (road || cross) w[3] = 1.2f;
                    }
                    float sum = 0f;
                    for (int l = 0; l < 6; l++) sum += w[l];
                    for (int l = 0; l < 6; l++) alpha[zi, xi, l] = sum > 0f ? w[l] / sum : (l == 0 ? 1f : 0f);
                }
            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Suelo fisico";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = new Vector3(-TerrainSize * 0.5f, -TerrainBase, -TerrainSize * 0.5f);
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 120f;
            terrain.drawInstanced = true;
        }

        private static float Band(float distanceInside, float blend) => Mathf.Clamp01(distanceInside / blend + 0.5f);

        /// <summary>Bosque decorativo fuera de los muros (sin colision ni recoleccion).</summary>
        private static void BackdropForest(Transform parent, Material bark, Material leaves)
        {
            Transform root = ChildRoot(parent, "Bosque de fondo");
            System.Random rng = new System.Random(42);
            for (int i = 0; i < 140; i++)
            {
                float x = -88f + (float)rng.NextDouble() * 176f;
                float z = -88f + (float)rng.NextDouble() * 176f;
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < PlayableHalf + 4f)
                    continue;
                float y = TerrainHeightAt(x, z);
                if (y > 14f)
                    continue;
                float size = 0.9f + (float)rng.NextDouble() * 0.9f;
                Transform tree = ChildRoot(root, "Arbol lejano");
                Vector3 p = new Vector3(x, y, z);
                Primitive("Tronco", PrimitiveType.Cylinder, tree, p + Vector3.up * (1.75f * size),
                    new Vector3(0.7f * size, 1.75f * size, 0.7f * size), bark, false);
                Primitive("Copa", PrimitiveType.Sphere, tree, p + Vector3.up * (4.1f * size),
                    new Vector3(3.3f * size, 3.0f * size, 3.3f * size), leaves, false);
            }
        }

        private static void Surface(string name, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = Primitive(name, PrimitiveType.Cube, parent, position, scale,
                material, false);
            item.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Tree(Transform parent, Vector3 position,
            float size, Material bark, Material leaves)
        {
            Transform root = ChildRoot(parent, "Arbol sombrio");
            root.position = position;
            Primitive("Tronco", PrimitiveType.Cylinder, root,
                position + Vector3.up * (1.75f * size),
                new Vector3(0.7f * size, 1.75f * size, 0.7f * size),
                bark, true);
            Primitive("Copa", PrimitiveType.Sphere, root,
                position + Vector3.up * (4.1f * size),
                new Vector3(3.3f * size, 3.0f * size, 3.3f * size),
                leaves, false);
            root.gameObject.AddComponent<HarvestNode>().Configure(ResourceKind.Wood, 12, 3);
        }

        /// <summary>Rocas, fibra y mineral cerca de la entrada para la primera base.</summary>
        private static void ResourceDeposits(Transform parent, Material stone, Material leaves)
        {
            Material ore = TexMat("Mineral", "dark_rock", new Color(0.95f, 0.55f, 0.3f), 1.5f);
            Vector3[] rocks = { new Vector3(-8f, 0f, -6f), new Vector3(-11f, 0f, 5f), new Vector3(7f, 0f, -9f), new Vector3(10f, 0f, 7f) };
            foreach (Vector3 p in rocks)
            {
                Transform root = ChildRoot(parent, "Roca");
                root.position = p;
                Primitive("Piedra", PrimitiveType.Sphere, root, p + Vector3.up * 0.55f, new Vector3(1.8f, 1.2f, 1.5f), stone, true);
                root.gameObject.AddComponent<HarvestNode>().Configure(ResourceKind.Stone, 15, 3);
            }
            Vector3[] bushes = { new Vector3(-6f, 0f, 9f), new Vector3(5f, 0f, 11f), new Vector3(-13f, 0f, -11f) };
            foreach (Vector3 p in bushes)
            {
                Transform root = ChildRoot(parent, "Arbusto de fibra");
                root.position = p;
                Primitive("Matorral", PrimitiveType.Sphere, root, p + Vector3.up * 0.4f, new Vector3(1.3f, 0.8f, 1.3f), leaves, false);
                root.gameObject.AddComponent<HarvestNode>().Configure(ResourceKind.Fiber, 9, 3);
            }
            Vector3[] veins = { new Vector3(14f, 0f, -2f), new Vector3(15f, 0f, 4f) };
            foreach (Vector3 p in veins)
            {
                Transform root = ChildRoot(parent, "Veta de mineral");
                root.position = p;
                Primitive("Mineral", PrimitiveType.Cube, root, p + Vector3.up * 0.6f, new Vector3(1.2f, 1.2f, 1.2f), ore, true);
                root.gameObject.AddComponent<HarvestNode>().Configure(ResourceKind.Ore, 10, 2);
            }
        }

        private static void KayKit(string name, string file, Transform parent,
            Vector3 position, Material material, Vector3 colliderSize)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitFolder + file);
            if (asset == null)
                return;
            Transform wrapper = ChildRoot(parent, name);
            wrapper.position = position;
            GameObject visual = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (visual == null)
                return;
            visual.transform.SetParent(wrapper, false);
            visual.transform.localPosition = Vector3.zero;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            BoxCollider box = wrapper.gameObject.AddComponent<BoxCollider>();
            box.size = colliderSize;
            box.center = Vector3.up * (colliderSize.y * 0.5f);
        }

        private readonly struct Camp
        {
            public readonly string Name;
            public readonly string Archetype;
            public readonly Vector3 Position;

            public Camp(string name, string archetype, Vector3 position)
            {
                Name = name;
                Archetype = archetype;
                Position = position;
            }
        }

        // Sur del camino: bandidos. Oeste: lobos y cripta. Este: culto y cantera.
        private static readonly Camp[] Camps =
        {
            new Camp("Saqueador del camino A", "saqueador", new Vector3(-3f, 1.08f, -30f)),
            new Camp("Saqueador del camino B", "saqueador", new Vector3(3f, 1.08f, -31f)),
            new Camp("Ballestero renegado", "ballestero", new Vector3(0f, 1.08f, -37f)),
            new Camp("Lobo de bruma bosque A", "lobo", new Vector3(-36f, 1.08f, -29f)),
            new Camp("Lobo de bruma bosque B", "lobo", new Vector3(-32f, 1.08f, -32f)),
            new Camp("Lobo de bruma bosque C", "lobo", new Vector3(-34f, 1.08f, -26f)),
            new Camp("Esqueleto de la cripta bosque A", "esqueleto", new Vector3(-34f, 1.08f, 34f)),
            new Camp("Esqueleto de la cripta bosque B", "esqueleto", new Vector3(-30f, 1.08f, 36f)),
            new Camp("Acolito ceniciento", "acolito", new Vector3(40f, 1.08f, -24f)),
            new Camp("Sanadora del culto", "sanadora", new Vector3(42f, 1.08f, -30f)),
            new Camp("Esqueleto del culto", "esqueleto", new Vector3(37f, 1.08f, -28f)),
            new Camp("Bruto de las canteras", "bruto", new Vector3(40f, 1.08f, 30f)),
            new Camp("Centinela runico", "centinela_runico", new Vector3(36f, 1.08f, 36f)),
        };

        private static void ArchetypeEnemy(Camp camp, Transform parent, Transform player, Material material)
        {
            EnemyArchetype data = EnemyArchetypes.Find(camp.Archetype);
            if (data == null)
                throw new InvalidOperationException("Tipo de enemigo desconocido: " + camp.Archetype);
            Enemy(camp.Name, parent, player, camp.Position, material, data.Health, data.Damage, data.Speed, false);
            Transform created = parent.Find(camp.Name);
            PrototypeEnemy enemy = created.GetComponent<PrototypeEnemy>();
            SerializedObject serialized = new SerializedObject(enemy);
            serialized.FindProperty("archetypeId").stringValue = camp.Archetype;
            serialized.FindProperty("attackRange").floatValue = data.AttackRange;
            serialized.FindProperty("aggroRange").floatValue = data.Aggro;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            switch (camp.Archetype)
            {
                case "saqueador": AttachModel(parent, camp.Name, "Barbarian", KayKitEnemySetup.AttackStyle.Melee); break;
                case "ballestero": AttachModel(parent, camp.Name, "Rogue", KayKitEnemySetup.AttackStyle.Ranged); break;
                case "acolito":
                case "sanadora": AttachModel(parent, camp.Name, "Mage", KayKitEnemySetup.AttackStyle.Spell); break;
                case "bruto": AttachModel(parent, camp.Name, "Barbarian", KayKitEnemySetup.AttackStyle.Heavy); break;
                case "centinela_runico": AttachModel(parent, camp.Name, "Knight", KayKitEnemySetup.AttackStyle.Heavy); break;
                case "esqueleto":
                    AttachModel(parent, camp.Name, camp.Name.EndsWith("B") ? "Skeleton_Minion" : "Skeleton_Warrior",
                        KayKitEnemySetup.AttackStyle.Melee);
                    break;
            }
        }

        /// <summary>
        /// Coloca un personaje KayKit animado bajo "Visual enemigo" con los pies en el
        /// suelo y escala uniforme, oculta la capsula provisional y asigna los clips
        /// del estilo de ataque. Si los modelos no estan preparados, deja la capsula.
        /// </summary>
        private static void AttachModel(Transform parent, string enemyName, string modelId,
            KayKitEnemySetup.AttackStyle style)
        {
            Transform root = parent.Find(enemyName);
            Transform anchor = root != null ? root.Find("Visual enemigo") : null;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KayKitEnemySetup.PrefabPath(modelId));
            RuntimeAnimatorController overrides = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                KayKitEnemySetup.OverridePath(modelId, style));
            if (anchor == null || prefab == null)
                return;
            CharacterController body = root.GetComponent<CharacterController>();
            float halfHeight = body != null ? body.height * 0.5f : 1f;
            bool boss = body != null && body.height > 2.4f;
            float worldScale = boss ? 1.12f : 0.82f;

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            model.name = "Modelo texturizado";
            Vector3 anchorScale = anchor.localScale;
            model.transform.localPosition = new Vector3(0f, -halfHeight / Mathf.Max(0.01f, anchorScale.y), 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = new Vector3(worldScale / anchorScale.x, worldScale / anchorScale.y,
                worldScale / anchorScale.z);
            Animator animator = model.GetComponent<Animator>();
            if (animator != null && overrides != null)
                animator.runtimeAnimatorController = overrides;

            MeshRenderer placeholder = anchor.GetComponent<MeshRenderer>();
            if (placeholder != null)
                placeholder.enabled = false;
            Transform eyes = anchor.Find("Ojos");
            if (eyes != null)
                eyes.gameObject.SetActive(false);
        }

        private static void Enemy(string name, Transform parent, Transform player,
            Vector3 position, Material material, float hp, float damage, float speed, bool boss)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            // El jefe mide 2,8 m: ajustar altura inicial para no enterrarlo.
            root.transform.position = boss
                ? new Vector3(position.x, 1.48f, position.z)
                : position;
            CharacterController body = root.AddComponent<CharacterController>();
            body.height = boss ? 2.8f : 2f;
            body.radius = boss ? 0.65f : 0.42f;
            body.center = Vector3.zero;
            body.minMoveDistance = 0f;
            CombatHealth health = root.AddComponent<CombatHealth>();
            SetFloat(health, "maximumHealth", hp);
            health.ResetToFull();
            // Persistir el feedback en escenas nuevas; las escenas existentes
            // tambien quedan cubiertas por PrototypeEnemy.Awake.
            root.AddComponent<CombatDamageFeedback>().ConfigureForEnemy(boss);

            GameObject model = Primitive("Visual enemigo", PrimitiveType.Capsule, root.transform,
                position, boss ? new Vector3(1.4f, 1.5f, 1.4f) :
                new Vector3(0.84f, 1f, 0.84f), material, false);
            model.transform.localPosition = Vector3.zero;
            GameObject eyes = Primitive("Ojos", PrimitiveType.Cube, model.transform,
                position + Vector3.up * 0.4f + Vector3.forward * 0.44f,
                new Vector3(0.45f, 0.09f, 0.08f), Mat("BrilloOjos", new Color(0.94f, 0.26f, 0.22f)), false);
            eyes.transform.localPosition = new Vector3(0f, 0.45f, 0.43f);

            PrototypeEnemy enemy = root.AddComponent<PrototypeEnemy>();
            SerializedObject serialized = new SerializedObject(enemy);
            serialized.FindProperty("target").objectReferenceValue = player;
            serialized.FindProperty("modelRoot").objectReferenceValue = model;
            serialized.FindProperty("isBoss").boolValue = boss;
            serialized.FindProperty("moveSpeed").floatValue = speed;
            serialized.FindProperty("attackDamage").floatValue = damage;
            serialized.FindProperty("aggroRange").floatValue = boss ? 19f : 15f;
            serialized.FindProperty("attackRange").floatValue = boss ? 2.2f : 1.75f;
            serialized.FindProperty("respawnSeconds").floatValue = boss ? 45f : 18f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
