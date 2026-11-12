using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Magic;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Verificacion automatica del ejecutable. Solo actua si se arranca con
    /// <c>-vhaldrenCapture carpeta</c>: guarda capturas del mundo (al inicio, la preparacion
    /// y el estallido de hechizos, los proyectiles de las seis escuelas, los remates de
    /// area, el aviso de un enemigo llenandose y su muerte), escribe un informe y cierra
    /// el juego. En una partida normal no hace nada.
    /// </summary>
    public sealed class GameAutoCapture : MonoBehaviour
    {
        private string folder;
        private string report;

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

        private void Note(string line) => File.AppendAllText(report, line + "\n");

        private IEnumerator Run()
        {
            report = Path.Combine(folder, "informe.txt");
            File.WriteAllText(report, "Vhaldren " + Application.version + " " + Screen.width + "x" + Screen.height + "\n");
            yield return new WaitForSeconds(4f);
            yield return Shot("1-inicio.png");
            Note("shaders_propios=" + !VfxLibrary.UsingFallback);
            Note(CameraReport());

            GameObject player = GameObject.Find("PlayerRoot");
            PlayerSpellbook book = player != null ? player.GetComponent<PlayerSpellbook>() : null;
            CombatDefense defense = player != null ? player.GetComponent<CombatDefense>() : null;
            CombatHealth health = player != null ? player.GetComponent<CombatHealth>() : null;
            Vhaldren.Gameplay.CameraSystem.SimpleFollowCamera follow =
                Camera.main != null ? Camera.main.GetComponent<Vhaldren.Gameplay.CameraSystem.SimpleFollowCamera>() : null;

            if (book != null)
            {
                follow?.SetZoom(0.75f, true);
                Vector3 ahead = player.transform.position + Vector3.forward * 6f;
                book.TryCast(1, ahead);
                yield return new WaitForSeconds(0.2f);
                yield return Shot("2a-anillo-preparacion.png");
                yield return new WaitForSeconds(0.2f);
                yield return Shot("2-anillo-de-llamas.png");
                Ready(book, defense);
                book.TryCast(2, ahead + Vector3.forward * 2f);
                yield return new WaitForSeconds(0.6f);
                yield return Shot("3-meteoro-avisado.png");
                yield return new WaitForSeconds(0.42f);
                yield return Shot("3b-meteoro-cayendo.png");
                yield return new WaitForSeconds(0.38f);
                yield return Shot("4-meteoro-impacto.png");
                yield return new WaitForSeconds(0.8f);

                yield return SchoolShowcase(book, defense, player.transform);
                follow?.SetZoom(1f, true);
            }

            // Vista general de un campamento del bestiario.
            if (player != null)
            {
                if (book != null)
                    book.SelectSchool(SpellSchool.Fulmancy);   // antes de entrar en combate
                player.transform.position = new Vector3(0f, 1.08f, -24f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(2.5f);
                yield return Shot("5-campamento-bandidos.png");
                yield return EnemyShowcase(book, defense, health, follow);

                // La misma vista girada (boton central / Insert-Supr) y con el zoom mas cercano.
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
                if (health != null)
                    health.ResetToFull();
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
                follow?.SetZoom(2.2f, true);
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
            Note("enemigos=" + enemies + " hechizos=" + (book != null ? book.SpellsCast : 0) +
                 " fps=" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0"));
            Application.Quit();
        }

        /// <summary>Recargas y resistencia llenas: la demostracion no depende de los tiempos.</summary>
        private static void Ready(PlayerSpellbook book, CombatDefense defense)
        {
            book.ResetCooldowns();
            if (defense != null)
                defense.RestoreStamina(1000f);
        }

        /// <summary>Un proyectil de cada escuela en abanico y el definitivo de varias.</summary>
        private IEnumerator SchoolShowcase(PlayerSpellbook book, CombatDefense defense, Transform player)
        {
            book.Unlock(SpellSchool.Umbramancy);
            book.Unlock(SpellSchool.Hemomancy);
            book.Unlock(SpellSchool.Lumancy);
            Vector3 origin = player.position;
            int cast = 0;
            for (int i = 0; i < SpellCatalog.SchoolCount; i++)
            {
                SpellSchool school = (SpellSchool)i;
                if (!book.SelectSchool(school))
                    continue;
                Ready(book, defense);
                float angle = Mathf.Lerp(-55f, 55f, i / (SpellCatalog.SchoolCount - 1f));
                Vector3 aim = origin + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 16f;
                if (book.TryCast(0, aim))
                    cast++;
            }
            yield return new WaitForSeconds(0.3f);
            yield return Shot("10-proyectiles-seis-escuelas.png");
            Note("proyectiles_en_vuelo=" + FindObjectsByType<SpellProjectile>(FindObjectsSortMode.None).Length + "/" + cast);
            yield return new WaitForSeconds(0.9f);

            Vector3 zone = origin + Vector3.forward * 7f;
            if (book.SelectSchool(SpellSchool.Fulmancy))
            {
                Ready(book, defense);
                book.TryCast(2, zone);
                yield return new WaitForSeconds(0.62f);
                yield return Shot("11-tempestad-rayos.png");
                yield return new WaitForSeconds(0.32f);
                yield return Shot("11b-tempestad-descarga.png");
                yield return new WaitForSeconds(0.6f);
            }
            if (book.SelectSchool(SpellSchool.Lumancy))
            {
                Ready(book, defense);
                book.TryCast(2, zone);
                yield return new WaitForSeconds(0.55f);
                yield return Shot("12a-juicio-avisado.png");
                yield return new WaitForSeconds(0.52f);
                yield return Shot("12-juicio-celestial.png");
                yield return new WaitForSeconds(0.6f);
            }
            if (book.SelectSchool(SpellSchool.Umbramancy))
            {
                Ready(book, defense);
                book.TryCast(2, zone);
                yield return new WaitForSeconds(0.32f);
                yield return Shot("13a-eclipse-preparacion.png");
                yield return new WaitForSeconds(0.38f);
                yield return Shot("13-eclipse.png");
                yield return new WaitForSeconds(0.6f);
            }
            if (book.SelectSchool(SpellSchool.Cryomancy))
            {
                Ready(book, defense);
                book.TryCast(1, zone);
                yield return new WaitForSeconds(0.68f);
                yield return Shot("14-prision-glacial.png");
                yield return new WaitForSeconds(0.6f);
            }
            if (book.SelectSchool(SpellSchool.Hemomancy))
            {
                Ready(book, defense);
                book.TryCast(2, zone);
                yield return new WaitForSeconds(0.98f);
                yield return Shot("15-festin-carmesi.png");
                yield return new WaitForSeconds(0.6f);
            }
        }

        /// <summary>Aviso enemigo llenandose, cadena de rayos y una muerte animada.</summary>
        private IEnumerator EnemyShowcase(PlayerSpellbook book, CombatDefense defense, CombatHealth health,
            Vhaldren.Gameplay.CameraSystem.SimpleFollowCamera follow)
        {
            follow?.SetZoom(0.8f, true);
            PrototypeEnemy attacker = null;
            float limit = Time.time + 8f;
            while (attacker == null && Time.time < limit)
            {
                if (health != null)
                    health.ResetToFull();
                foreach (PrototypeEnemy enemy in FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None))
                {
                    EnemyAttackTelegraph warning = enemy.GetComponent<EnemyAttackTelegraph>();
                    if (enemy.IsAttackTelegraphed && warning != null && warning.Fill >= 0.4f && warning.Fill <= 0.8f)
                    {
                        attacker = enemy;
                        break;
                    }
                }
                if (attacker == null)
                    yield return null;
            }
            Note("aviso_enemigo=" + (attacker != null ? attacker.name + " lleno " +
                attacker.GetComponent<EnemyAttackTelegraph>().Fill.ToString("0.00") : "ninguno"));
            if (attacker == null)
                yield break;
            yield return Shot("16-aviso-enemigo-llenandose.png");

            if (book != null)
            {
                Ready(book, defense);
                book.TryCast(1, attacker.transform.position);
                yield return new WaitForSeconds(0.22f);
                yield return Shot("17-cadena-de-rayos.png");
            }

            if (health != null)
                health.ResetToFull();
            CombatHealth victim = attacker.GetComponent<CombatHealth>();
            if (victim != null && !victim.IsDefeated)
                victim.ApplyDamage(victim.CurrentHealth + 1f);
            yield return new WaitForSeconds(0.35f);
            yield return Shot("18-muerte-cayendo.png");
            EnemyAnimationDriver driver = attacker.AnimationDriver;
            Note("muerte_animada=" + (driver != null && driver.InDeathState) + " procedural=" + attacker.UsesProceduralDeath +
                 " colision_apagada=" + !attacker.GetComponent<CharacterController>().enabled +
                 " cuerpo_visible=" + attacker.IsCorpseVisible);
            yield return new WaitForSeconds(0.65f);
            yield return Shot("19-muerte-en-el-suelo.png");
            yield return new WaitForSeconds(1.1f);
            Note("cuerpo_oculto_tras_1_8s=" + !attacker.IsCorpseVisible);
            if (health != null)
                health.ResetToFull();
            follow?.SetZoom(1f, true);
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
