using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Magic;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Verificacion automatica del ejecutable. Solo actua si se arranca con
    /// <c>-vhaldrenCapture carpeta</c>: guarda capturas del mundo (al inicio, lanzando
    /// hechizos y con la ayuda abierta), escribe un informe y cierra el juego.
    /// En una partida normal no hace nada.
    /// </summary>
    public sealed class GameAutoCapture : MonoBehaviour
    {
        private string folder;

        public static string RequestedFolder()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-vhaldrenCapture")
                    return args[i + 1];
            return null;
        }

        private void Start()
        {
            folder = RequestedFolder();
            if (string.IsNullOrEmpty(folder))
            {
                Destroy(this);
                return;
            }
            Directory.CreateDirectory(folder);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            string report = Path.Combine(folder, "informe.txt");
            File.WriteAllText(report, "Vhaldren " + Application.version + " " + Screen.width + "x" + Screen.height + "\n");
            yield return new WaitForSeconds(4f);
            yield return Shot("1-inicio.png");
            File.AppendAllText(report, CameraReport() + "\n");

            GameObject player = GameObject.Find("PlayerRoot");
            PlayerSpellbook book = player != null ? player.GetComponent<PlayerSpellbook>() : null;
            if (book != null)
            {
                Vector3 ahead = player.transform.position + Vector3.forward * 6f;
                book.TryCast(1, ahead);
                yield return new WaitForSeconds(0.45f);
                yield return Shot("2-anillo-de-llamas.png");
                book.ResetCooldowns();
                book.TryCast(2, ahead + Vector3.forward * 2f);
                yield return new WaitForSeconds(0.6f);
                yield return Shot("3-meteoro-avisado.png");
                yield return new WaitForSeconds(0.8f);
                yield return Shot("4-meteoro-impacto.png");
            }

            // Vista general de un campamento del bestiario.
            if (player != null)
            {
                player.transform.position = new Vector3(0f, 1.08f, -24f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(2.5f);
                yield return Shot("5-campamento-bandidos.png");

                // La misma vista girada (boton central / Insert-Supr) y con el zoom mas cercano.
                SimpleFollowCamera follow = FollowCamera();
                if (follow != null)
                {
                    follow.SetYaw(135f, true);
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot("5b-campamento-camara-girada.png");
                    File.AppendAllText(report, "girada: " + CameraReport() + "\n");
                    follow.SetYaw(0f, true);
                    follow.SetZoom(0f, true);
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot("5c-campamento-zoom-cercano.png");
                    File.AppendAllText(report, "zoom cercano: " + CameraReport() + "\n");
                    follow.SetZoom(1f, true);
                }

                // Detras del muro sur y de un arbol: lo que tapa al personaje se oculta.
                player.transform.position = new Vector3(10f, 1.08f, -43.5f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(1.2f);
                yield return Shot("5d-muro-oculto.png");
                File.AppendAllText(report, "tras el muro: " + CameraReport() + "\n");
                player.transform.position = new Vector3(-23.98f, 1.08f, -10f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(1.2f);
                yield return Shot("5e-arbol-oculto.png");
                File.AppendAllText(report, "tras el arbol: " + CameraReport() + "\n");
            }

            if (player != null)
            {
                player.transform.position = new Vector3(0f, 1.08f, 35.5f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(1.5f);
                yield return Shot("8-altar-obelisco.png");
                player.transform.position = new Vector3(0f, 1.08f, -24f);
                Physics.SyncTransforms();
            }

            if (player != null)
            {
                // Vista alejada desde una esquina: terreno pintado, muros y montañas.
                player.transform.position = new Vector3(-36f, 1.08f, -34f);
                Physics.SyncTransforms();
                // El zoom queda limitado al maximo de la camara (30 m en el mundo).
                SimpleFollowCamera follow = FollowCamera();
                if (follow != null)
                    follow.SetZoom(2.2f, true);
                yield return new WaitForSeconds(1.2f);
                yield return Shot("9-terreno-montanas.png");
                if (follow != null)
                    follow.SetZoom(1f, true);
                player.transform.position = new Vector3(0f, 1.08f, -24f);
                Physics.SyncTransforms();
            }

            if (DayNightCycle.Active != null)
            {
                DayNightCycle.Active.SetElapsed((3f * 24f + 14f) * DayNightCycle.DaySeconds / 24f);
                yield return new WaitForSeconds(1f);
                yield return Shot("6-luna-carmesi.png");
            }

            Vhaldren.Gameplay.Progression.PlayerEquipment gear =
                player != null ? player.GetComponent<Vhaldren.Gameplay.Progression.PlayerEquipment>() : null;
            if (gear != null)
            {
                gear.SetPanel(true);
                yield return new WaitForSeconds(0.3f);
                yield return Shot("7-forja.png");
                gear.SetPanel(false);
            }

            int enemies = FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None).Length;
            File.AppendAllText(report, "enemigos=" + enemies + " hechizos=" + (book != null ? book.SpellsCast : 0) +
                                       " fps=" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0") + "\n");
            Application.Quit();
        }

        private static SimpleFollowCamera FollowCamera() =>
            Camera.main != null ? Camera.main.GetComponent<SimpleFollowCamera>() : null;

        /// <summary>Encuadre real de la camara principal, para el informe.</summary>
        private static string CameraReport()
        {
            Camera view = Camera.main;
            SimpleFollowCamera follow = FollowCamera();
            if (view == null || follow == null)
                return "camara: sin SimpleFollowCamera";
            CameraOcclusionHider hider = view.GetComponent<CameraOcclusionHider>();
            return "camara: inclinacion=" + follow.PitchDegrees.ToString("0.0") +
                   " distancia=" + follow.Distance.ToString("0.0") +
                   " (" + follow.MinDistance.ToString("0") + "-" + follow.MaxDistance.ToString("0") + ")" +
                   " fov=" + view.fieldOfView.ToString("0") +
                   " giro=" + (follow.AllowRotation ? "si" : "no") + " " + follow.Yaw.ToString("0") +
                   " adelanto=" + (follow.LookAheadEnabled ? "si" : "no") +
                   " ocultos=" + (hider != null ? hider.HiddenCount + "/" + hider.OccluderCount : "-");
        }

        private IEnumerator Shot(string file)
        {
            // Leer el backbuffer al final del frame incluye la interfaz (OnGUI).
            yield return new WaitForEndOfFrame();
            Texture2D image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, file), image.EncodeToPNG());
            Destroy(image);
            yield return null;
        }
    }
}
