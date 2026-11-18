using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Genera la aplicacion jugable de Windows (64 bits) en Builds/Windows/Vhaldren.exe.
    /// Uso por consola: Unity -batchmode -projectPath . -executeMethod
    /// Vhaldren.EditorTools.VhaldrenBuild.BuildWindows -quit
    /// </summary>
    public static class VhaldrenBuild
    {
        public const string OutputPath = "Builds/Windows/Vhaldren.exe";
        public const string VariantFolder = "Assets/Resources/VariantesShader";
        private static readonly string[] Scenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/MundoVhaldren.unity", "Assets/Scenes/WorldPrototype001.unity" };

        [MenuItem("Vhaldren/Compilar juego para Windows")]
        public static void BuildWindows()
        {
            EnsureShaderVariants();
            PlayerSettings.productName = "Vhaldren";
            PlayerSettings.companyName = "Vhaldren";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("[Vhaldren] Compilacion " + summary.result + ": " + Path.GetFullPath(OutputPath) +
                      " (" + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB, " +
                      summary.totalErrors + " errores, " + summary.totalWarnings + " avisos, " +
                      summary.totalTime.TotalSeconds.ToString("0") + " s)");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        /// <summary>
        /// El codigo crea materiales Standard transparentes (vista previa de construccion)
        /// y con emision (enemigos) en tiempo de juego. Unity elimina del ejecutable las
        /// variantes que ningun material usa, asi que se guardan dos materiales de muestra
        /// en Resources para conservarlas.
        /// </summary>
        public static void EnsureShaderVariants()
        {
            Shader standard = Shader.Find("Standard");
            if (standard == null)
                return;
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(VariantFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "VariantesShader");

            string transparentPath = VariantFolder + "/StandardTransparente.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(transparentPath) == null)
            {
                Material transparent = new Material(standard);
                transparent.SetFloat("_Mode", 3f);
                transparent.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                transparent.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                transparent.SetInt("_ZWrite", 0);
                transparent.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                transparent.renderQueue = 3000;
                AssetDatabase.CreateAsset(transparent, transparentPath);
            }

            string emissionPath = VariantFolder + "/StandardEmision.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(emissionPath) == null)
            {
                Material emission = new Material(standard);
                emission.EnableKeyword("_EMISSION");
                emission.SetColor("_EmissionColor", Color.white);
                emission.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                AssetDatabase.CreateAsset(emission, emissionPath);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
