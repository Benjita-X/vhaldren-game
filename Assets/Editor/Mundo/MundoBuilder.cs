using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.World;

namespace Vhaldren.EditorTools.Mundo
{
    /// <summary>
    /// Genera la escena jugable del mapa grande (Assets/Scenes/MundoVhaldren.unity) a partir
    /// de Assets/Art/Mundo/diseno_mapa.json y del kit de modelos de Blender.
    /// Consola: Unity -batchmode -projectPath . -executeMethod Vhaldren.EditorTools.Mundo.MundoBuilder.GenerarDesdeConsola -quit
    /// </summary>
    public static partial class MundoBuilder
    {
        public const string Escena = "Assets/Scenes/MundoVhaldren.unity";
        public const string NombreEscena = "MundoVhaldren";
        private const string Base = "Assets/Scenes/Movement001.unity";
        public const string RaizMundo = "Mundo de Vhaldren";
        private const string KayKitDungeon = "Assets/ThirdParty/KayKitDungeon/Models/";
        private const int ResAltura = 2049;
        private const int ResSplat = 1024;
        private const int ResDetalle = 1024;

        private static DisenoMapa D;
        private static MundoTerreno T;
        private static MundoKit K;
        private static Terrain terreno;
        private static Transform raiz, props, enemigos, lugares, aguaRaiz, npcs;
        private static Transform jugador;
        private static System.Random rng;
        private static readonly List<Vector2> ocupados = new List<Vector2>();

        [MenuItem("Vhaldren/Mundo/Generar mapa grande")]
        public static void Generar()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Vhaldren] Sal de Play antes de generar el mapa.");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            DateTime t0 = DateTime.Now;
            // VHALDREN_DISENO permite probar el generador con otro diseño sin tocar el oficial.
            string otro = Environment.GetEnvironmentVariable("VHALDREN_DISENO");
            D = string.IsNullOrEmpty(otro) ? DisenoMapa.Cargar() : DisenoMapa.Cargar(otro);
            rng = new System.Random(2026);
            ocupados.Clear();

            K = new MundoKit();
            K.CrearPrefabs();
            K.MaterialesAgua();
            Paso("kit", t0);

            T = new MundoTerreno(D);
            Paso("diseño del relieve", t0);

            Scene scene = EditorSceneManager.OpenScene(Base, OpenSceneMode.Single);
            GameObject player = Raiz(scene, "PlayerRoot");
            GameObject camara = Raiz(scene, "MainCamera");
            if (player == null || camara == null)
                throw new InvalidOperationException("La escena base no contiene jugador o cámara.");
            if (!EditorSceneManager.SaveScene(scene, Escena))
                throw new InvalidOperationException("No se pudo crear " + Escena);
            foreach (string n in new[] { "Ground", "WallObstacle", "TestCourse" })
            {
                GameObject old = Raiz(scene, n);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }
            jugador = player.transform;
            PrepararJugador(player);

            GameObject mundo = new GameObject(RaizMundo);
            raiz = mundo.transform;
            Transform terrenoRaiz = Hijo(raiz, "Terreno");
            props = Hijo(raiz, "Naturaleza y rocas");
            lugares = Hijo(raiz, "Lugares");
            enemigos = Hijo(raiz, "Enemigos");
            aguaRaiz = Hijo(raiz, "Agua");
            npcs = Hijo(raiz, "Habitantes");

            CrearTerreno(terrenoRaiz);
            Paso("terreno", t0);
            CrearAgua();
            Puentes();
            Paso("agua y puentes", t0);
            Lugares();
            Paso("lugares", t0);
            Campamentos();
            Jefes();
            Paso("enemigos", t0);
            Monturas();
            PiedrasViaje();
            Habitantes();
            Paso("monturas, piedras y habitantes", t0);
            Acantilados();
            Paso("acantilados", t0);
            Atlas(mundo);
            MapaTextura();
            Paso("atlas y mapa", t0);
            Ambiente(camara);

            // jugador en el inicio
            Vector3 inicio = Suelo(D.inicio.x, D.inicio.z) + Vector3.up * 1.1f;
            player.transform.position = inicio;
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            PrototypeGameSession session = mundo.AddComponent<PrototypeGameSession>();
            SerializedObject sd = new SerializedObject(session);
            sd.FindProperty("player").objectReferenceValue = player.transform;
            sd.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, Escena))
                throw new InvalidOperationException("No se pudo guardar " + Escena);
            AjustesDeCompilacion();
            AssetDatabase.SaveAssets();
            Paso("guardado", t0);
            Debug.Log("[Vhaldren] Mapa grande generado: " + Escena + " — " + D.regiones.Count + " regiones, " +
                      D.lugares.Count + " lugares, " + enemigos.childCount + " enemigos, terreno de " + T.Tam + " m (" +
                      (DateTime.Now - t0).TotalSeconds.ToString("0") + " s).");
        }

        public static void GenerarDesdeConsola()
        {
            try { Generar(); }
            catch (Exception ex)
            {
                Debug.LogError("[Vhaldren] Error generando el mapa: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void Paso(string que, DateTime t0) =>
            Debug.Log("[Vhaldren] Mapa: " + que + " listo (" + (DateTime.Now - t0).TotalSeconds.ToString("0.0") + " s)");

        // ================================================================ jugador
        private static void PrepararJugador(GameObject player)
        {
            CombatHealth hp = player.GetComponent<CombatHealth>() ?? player.AddComponent<CombatHealth>();
            SerializedObject so = new SerializedObject(hp);
            so.FindProperty("maximumHealth").floatValue = 150f;
            so.ApplyModifiedPropertiesWithoutUndo();
            hp.ResetToFull();
            CombatDamageFeedback fb = player.GetComponent<CombatDamageFeedback>() ?? player.AddComponent<CombatDamageFeedback>();
            fb.ConfigureForPlayer();
            SurvivalInventory inv = player.GetComponent<SurvivalInventory>() ?? player.AddComponent<SurvivalInventory>();
            SerializedObject si = new SerializedObject(inv);
            SerializedProperty demo = si.FindProperty("grantDemoSupplies");
            if (demo != null) { demo.boolValue = false; si.ApplyModifiedPropertiesWithoutUndo(); }
            if (player.GetComponent<SurvivalToolbelt>() == null) player.AddComponent<SurvivalToolbelt>();
            if (player.GetComponent<PlayerBuilder>() == null) player.AddComponent<PlayerBuilder>();
            if (player.GetComponent<PlayerGatherer>() == null) player.AddComponent<PlayerGatherer>();
            if (player.GetComponent<SurvivalHud>() == null) player.AddComponent<SurvivalHud>();
        }

        // ================================================================ utilidades
        private static GameObject Raiz(Scene scene, string name)
        {
            foreach (GameObject g in scene.GetRootGameObjects())
                if (g.name == name) return g;
            return null;
        }

        private static Transform Hijo(Transform parent, string name)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        /// <summary>Punto del suelo en (x,z).</summary>
        public static Vector3 Suelo(float x, float z)
        {
            float y = terreno != null ? terreno.SampleHeight(new Vector3(x, 0f, z)) + terreno.transform.position.y : 0f;
            return new Vector3(x, y, z);
        }

        private static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        private static System.Random Semilla(string id) => new System.Random(id == null ? 7 : id.GetHashCode() & 0x7fffffff);

        /// <summary>Instancia un modelo del kit apoyado en el suelo. Devuelve null si no hay modelo.</summary>
        private static GameObject Poner(Transform padre, Vector3 pos, float yaw, float escala, params string[] modelos)
        {
            GameObject prefab = K.Alguno(modelos);
            if (prefab == null) return null;
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
            Vector3 s = Suelo(pos.x, pos.z);
            g.transform.position = new Vector3(pos.x, pos.y > -9000f ? Mathf.Max(s.y, pos.y) - 0.05f : s.y - 0.05f, pos.z);
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = Vector3.one * escala;
            return g;
        }

        private static GameObject PonerEn(Transform padre, float x, float z, float yaw, float escala, params string[] modelos) =>
            Poner(padre, new Vector3(x, -10000f, z), yaw, escala, modelos);

        private static GameObject Primitiva(string nombre, PrimitiveType tipo, Transform padre, Vector3 pos, Vector3 escala, Material m, bool colisor)
        {
            GameObject g = GameObject.CreatePrimitive(tipo);
            g.name = nombre;
            g.transform.SetParent(padre, false);
            g.transform.position = pos;
            g.transform.localScale = escala;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!colisor) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        private static string NombreRegion(string id)
        {
            RegionDiseno r = D.Region(id);
            return r != null ? r.nombre : id;
        }

        private static void AjustesDeCompilacion()
        {
            List<EditorBuildSettingsScene> lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int i = lista.FindIndex(s => s.path == Escena);
            if (i < 0) lista.Insert(Math.Min(1, lista.Count), new EditorBuildSettingsScene(Escena, true));
            else lista[i] = new EditorBuildSettingsScene(Escena, true);
            EditorBuildSettings.scenes = lista.ToArray();
        }
    }
}
