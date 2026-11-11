using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
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

            Material ground = Mat("SueloBasico", new Color(0.19f, 0.17f, 0.18f));
            Material forest = Mat("Bosque", new Color(0.11f, 0.18f, 0.14f));
            Material ruins = Mat("Ruinas", new Color(0.23f, 0.22f, 0.26f));
            Material crimson = Mat("Altar", new Color(0.28f, 0.075f, 0.11f));
            Material path = Mat("Sendero", new Color(0.33f, 0.26f, 0.23f));
            Material stone = Mat("Piedra", new Color(0.3f, 0.32f, 0.36f));
            Material bark = Mat("Tronco", new Color(0.2f, 0.13f, 0.12f));
            Material leaves = Mat("Hojas", new Color(0.13f, 0.24f, 0.17f));
            Material red = Mat("Enemigo", new Color(0.47f, 0.13f, 0.2f));
            Material purple = Mat("Jefe", new Color(0.42f, 0.16f, 0.52f));
            Material atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasPath);

            GameObject world = new GameObject("World008 - 92x92 - Dark Fantasy");
            Transform root = world.transform;
            Transform environment = ChildRoot(root, "Terreno y regiones");
            Transform props = ChildRoot(root, "Naturaleza y ruinas");
            Transform enemies = ChildRoot(root, "Enemigos");

            // Piso fisico continuo: sin saltos entre regiones ni numerosos colliders por baldosa.
            Primitive("Suelo fisico", PrimitiveType.Cube, environment,
                new Vector3(0f, -0.55f, 0f), new Vector3(92f, 1.1f, 92f), ground, true);

            // Capas visuales ultrafinas SIN colision: terreno continuo y zonas diferenciadas.
            Surface("Region forestal", environment, new Vector3(-29f, 0.005f, -1f),
                new Vector3(32f, 0.01f, 88f), forest);
            Surface("Region de ruinas", environment, new Vector3(29f, 0.006f, -1f),
                new Vector3(32f, 0.012f, 88f), ruins);
            Surface("Camino central", environment, new Vector3(0f, 0.008f, -2f),
                new Vector3(13f, 0.014f, 74f), path);
            Surface("Cruce de caminos", environment, new Vector3(0f, 0.01f, 0f),
                new Vector3(38f, 0.014f, 7f), path);
            Surface("Patio del altar", environment, new Vector3(0f, 0.012f, 30f),
                new Vector3(25f, 0.014f, 25f), crimson);

            // Limites fisicos: el usuario no puede caer al vacio durante esta prueba.
            Primitive("Muro norte", PrimitiveType.Cube, props,
                new Vector3(0f, 3f, 46f), new Vector3(92f, 6f, 1.5f), stone, true);
            Primitive("Muro sur", PrimitiveType.Cube, props,
                new Vector3(0f, 3f, -46f), new Vector3(92f, 6f, 1.5f), stone, true);
            Primitive("Muro este", PrimitiveType.Cube, props,
                new Vector3(46f, 3f, 0f), new Vector3(1.5f, 6f, 92f), stone, true);
            Primitive("Muro oeste", PrimitiveType.Cube, props,
                new Vector3(-46f, 3f, 0f), new Vector3(1.5f, 6f, 92f), stone, true);

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
                float size = 0.75f + (float)rng.NextDouble() * 0.8f;
                Tree(props, new Vector3(x, 0f, z), size, bark, leaves);
            }

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

            Primitive("Obelisco de sangre", PrimitiveType.Cube, props,
                new Vector3(0f, 1.7f, 41f), new Vector3(2.8f, 3.4f, 2.8f), crimson, true);
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
                "Recorre bosque, ruinas y altar, combate 6 enemigos y usa Q para curarte.");
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
                player.GetComponent<CharacterController>() != null &&
                health != null && health.MaximumHealth >= 150f &&
                camera.GetComponent<Camera>() != null &&
                session != null &&
                ground != null && ground.GetComponent<Collider>() != null &&
                enemies.Length == 6;

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
