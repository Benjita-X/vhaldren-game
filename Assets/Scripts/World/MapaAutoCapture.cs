using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Vhaldren.Gameplay.CameraSystem;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Verificación automática del MAPA GRANDE en el ejecutable (con -vhaldrenCapture carpeta):
    /// vista aérea del mundo, vistas inclinadas de cada región y un recorrido por los lugares
    /// con capturas, fotogramas por segundo y comprobación de que el jugador pisa el suelo.
    /// Escribe informe-mapa.txt y cierra el juego. En una partida normal no hace nada.
    /// </summary>
    public sealed class MapaAutoCapture : MonoBehaviour
    {
        private string folder;
        private readonly StringBuilder report = new StringBuilder();

        private void Start()
        {
            folder = GameAutoCapture.RequestedFolder();
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
            GameObject player = GameObject.Find("PlayerRoot");
            Camera cam = Camera.main;
            SimpleFollowCamera follow = cam != null ? cam.GetComponent<SimpleFollowCamera>() : null;
            Terrain terrain = Terrain.activeTerrain;
            report.AppendLine("Informe del mapa grande - " + DateTime.Now);
            report.AppendLine("Resolución " + Screen.width + "x" + Screen.height + ", GPU " + SystemInfo.graphicsDeviceName);
            yield return new WaitForSeconds(5f);
            yield return Medir("inicio", 2f);
            yield return Shot("01-inicio.png");
            yield return Lucifer(player, terrain);

            if (cam != null && terrain != null)
            {
                Vector3 tp = terrain.transform.position, ts = terrain.terrainData.size;
                Vector3 centro = tp + new Vector3(ts.x * 0.5f, 0f, ts.z * 0.5f);
                bool fog = RenderSettings.fog;
                float far = cam.farClipPlane, fov = cam.fieldOfView;
                if (follow != null) follow.enabled = false;
                RenderSettings.fog = false;
                cam.farClipPlane = 6000f;
                cam.fieldOfView = 55f;
                cam.transform.position = centro + new Vector3(0f, ts.x * 1.05f, -ts.z * 0.35f);
                cam.transform.LookAt(centro);
                yield return new WaitForSeconds(2f);
                yield return Shot("02-vista-aerea-completa.png");
                // vistas inclinadas desde las cuatro esquinas a media altura
                Vector3[] puntos =
                {
                    new Vector3(-0.25f, 0f, -0.25f), new Vector3(0.25f, 0f, -0.25f), new Vector3(-0.25f, 0f, 0.25f), new Vector3(0.25f, 0f, 0.25f)
                };
                for (int i = 0; i < puntos.Length; i++)
                {
                    Vector3 objetivo = centro + Vector3.Scale(puntos[i], new Vector3(ts.x, 0f, ts.z));
                    objetivo.y = terrain.SampleHeight(objetivo) + tp.y;
                    cam.transform.position = objetivo + new Vector3(0f, 230f, -260f);
                    cam.transform.LookAt(objetivo);
                    yield return new WaitForSeconds(1.5f);
                    yield return Shot("03-region-" + (i + 1) + ".png");
                }
                RenderSettings.fog = fog;
                cam.farClipPlane = far;
                cam.fieldOfView = fov;
                if (follow != null) follow.enabled = true;
            }

            // recorrido por los lugares
            GameObject lugares = GameObject.Find("Mundo de Vhaldren/Lugares");
            List<Transform> destinos = new List<Transform>();
            if (lugares != null)
                foreach (Transform t in lugares.transform)
                {
                    if (t.name.StartsWith("Puesto enemigo")) continue;
                    if (t.name == "Monturas salvajes" || t.name == "Piedras de viaje")
                    {
                        if (t.childCount > 0) destinos.Add(t.GetChild(0));
                        continue;
                    }
                    destinos.Add(t);
                }
            int n = 0;
            foreach (Transform d in destinos)
            {
                if (n >= 18 || player == null) break;
                n++;
                Teleport(player, d.position + new Vector3(2f, 0f, -9f), terrain);
                yield return new WaitForSeconds(2.5f);
                yield return Medir(d.name, 1.5f);
                float suelo = terrain != null ? terrain.SampleHeight(player.transform.position) + terrain.transform.position.y : 0f;
                report.AppendLine("   altura del jugador sobre el terreno: " + (player.transform.position.y - suelo).ToString("0.00") + " m");
                yield return Shot("10-" + n.ToString("00") + "-" + Limpio(d.name) + ".png");
            }

            report.AppendLine("Enemigos en la escena: " + FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None).Length);
            File.WriteAllText(Path.Combine(folder, "informe-mapa.txt"), report.ToString());
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        /// <summary>Lanza el kit de Lucifer cerca de un enemigo y fotografía cada habilidad.</summary>
        private IEnumerator Lucifer(GameObject player, Terrain terrain)
        {
            if (player == null) yield break;
            Vhaldren.Gameplay.Magic.PlayerSpellbook book = player.GetComponent<Vhaldren.Gameplay.Magic.PlayerSpellbook>();
            Vhaldren.Gameplay.Races.PlayerRace race = player.GetComponent<Vhaldren.Gameplay.Races.PlayerRace>();
            report.AppendLine("Linaje: " + (race != null ? race.Data.Name : "?") + " | escuela activa: " + (book != null ? book.School.ToString() : "sin grimorio"));
            PrototypeEnemy objetivo = null;
            float mejor = float.MaxValue;
            foreach (PrototypeEnemy e in FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None))
            {
                if (e.IsBoss) continue;
                float d = (e.transform.position - player.transform.position).sqrMagnitude;
                if (d < mejor) { mejor = d; objetivo = e; }
            }
            if (objetivo != null)
            {
                Teleport(player, objetivo.transform.position + new Vector3(0f, 0f, -9f), terrain);
                yield return new WaitForSeconds(1.5f);
            }
            Vector3 aim = objetivo != null ? objetivo.transform.position : player.transform.position + player.transform.forward * 8f;
            string[] nombres = { "lanza", "salto", "caida" };
            for (int slot = 0; slot < 3 && book != null; slot++)
            {
                book.ResetCooldowns();
                bool ok = book.TryCast(slot, aim);
                report.AppendLine("Habilidad " + (slot + 1) + " (" + nombres[slot] + "): " + (ok ? "lanzada" : "NO se lanzó"));
                yield return new WaitForSeconds(0.15f);
                yield return Shot("05-lucifer-" + (slot + 1) + "a-" + nombres[slot] + ".png");
                yield return new WaitForSeconds(0.55f);
                yield return Shot("05-lucifer-" + (slot + 1) + "b-" + nombres[slot] + ".png");
                yield return new WaitForSeconds(1.1f);
                yield return Shot("05-lucifer-" + (slot + 1) + "c-" + nombres[slot] + ".png");
                yield return new WaitForSeconds(0.8f);
            }
            if (race != null)
            {
                bool ok = race.TryUsePower();
                report.AppendLine("Poder R: " + (ok ? "usado" : "NO se usó"));
                yield return new WaitForSeconds(0.2f);
                yield return Shot("05-lucifer-4-estallido.png");
                yield return new WaitForSeconds(1f);
            }
        }

        private static void Teleport(GameObject player, Vector3 pos, Terrain terrain)
        {
            if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 1.2f;
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        private IEnumerator Medir(string donde, float segundos)
        {
            int frames = 0;
            float t0 = Time.unscaledTime, peor = 0f;
            while (Time.unscaledTime - t0 < segundos)
            {
                frames++;
                peor = Mathf.Max(peor, Time.unscaledDeltaTime);
                yield return null;
            }
            float fps = frames / Mathf.Max(0.01f, Time.unscaledTime - t0);
            report.AppendLine(donde + ": " + fps.ToString("0") + " fps (peor fotograma " + (peor * 1000f).ToString("0") + " ms)");
        }

        private static string Limpio(string s)
        {
            StringBuilder b = new StringBuilder();
            foreach (char ch in s)
                b.Append(ch < 128 && char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
            return b.ToString();
        }

        private IEnumerator Shot(string file)
        {
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
