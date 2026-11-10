using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vhaldren.EditorTools
{
    /// <summary>
    /// Guarda en PNG lo que ve la camara principal de la escena. Sirve para revisar
    /// encuadre, colores y tamano del personaje sin abrir el Editor (tambien en modo batch,
    /// siempre que no se use -nographics).
    /// </summary>
    public static class GameViewCapture
    {
        private const string ScenePath = "Assets/Scenes/Movement001.unity";
        private const string OutputFolder = "Captures";
        private const int Width = 1280;
        private const int Height = 720;

        [MenuItem("Vhaldren/Capturar vista de la camara")]
        public static void CaptureActiveScene()
        {
            Capture(SceneManager.GetActiveScene().name);
        }

        /// <summary>Entrada para -executeMethod: abre Movement001 y la captura.</summary>
        public static void CaptureMovement001()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Capture("Movement001");
        }

        private static void Capture(string name)
        {
            Camera view = Camera.main;
            if (view == null)
            {
                Debug.LogError("[Vhaldren] No hay camara principal (tag MainCamera) en la escena.");
                return;
            }

            RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previousTarget = view.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                view.targetTexture = target;
                view.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();

                Directory.CreateDirectory(OutputFolder);
                string path = Path.Combine(OutputFolder, name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("[Vhaldren] Captura guardada en " + Path.GetFullPath(path));
            }
            finally
            {
                view.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
