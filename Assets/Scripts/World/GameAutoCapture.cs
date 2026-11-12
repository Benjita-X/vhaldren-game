using System;
using System.Collections;
using System.IO;
using UnityEngine;
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
