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

            if (player != null)
                yield return HudShowcase(player, book, defense, health, follow);

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

        /// <summary>
        /// HUD 022: barra con recargas y cargas, poca vida (viñeta roja), avisos, construcción,
        /// ayuda F1, tablón, armas en las manos de cerca y la flecha de golpe fuera de pantalla.
        /// </summary>
        private IEnumerator HudShowcase(GameObject player, PlayerSpellbook book, CombatDefense defense, CombatHealth health,
            SimpleFollowCamera follow)
        {
            Transform body = player.transform;
            Vector3 safe = new Vector3(2f, 1.08f, 2f);
            body.position = safe;
            Physics.SyncTransforms();
            if (health != null) health.ResetToFull();
            yield return new WaitForSeconds(1.2f);

            // Recargas: espada, pulso, un hechizo con cargas y la poción.
            Vhaldren.Gameplay.Player.PlayerCombatAbilities abilities = player.GetComponent<Vhaldren.Gameplay.Player.PlayerCombatAbilities>();
            PrototypeGameSession session = FindAnyObjectByType<PrototypeGameSession>();
            if (book != null)
            {
                Ready(book, defense);
                book.SelectSchool(SpellSchool.Pyromancy);
                book.TryCast(0, body.position + Vector3.forward * 5f);
            }
            if (abilities != null)
            {
                abilities.TrySwordSweep();
                if (defense != null) defense.RestoreStamina(1000f);
                abilities.TryRunePulse();
            }
            if (health != null)
                health.ApplyDamage(health.MaximumHealth * 0.35f);
            session?.TryDrinkPotion();
            yield return new WaitForSeconds(1.4f);
            yield return Shot("20-hud-barra-recargas.png");
            Vhaldren.Gameplay.UI.ActionBarHud bar = player.GetComponent<Vhaldren.Gameplay.UI.ActionBarHud>();
            if (bar != null)
            {
                bar.Refresh();
                Note("barra: espada=" + bar.Slot(Vhaldren.Gameplay.UI.ActionBarHud.SlotId.Sword).Remaining.ToString("0.0") +
                     "s pulso=" + bar.Slot(Vhaldren.Gameplay.UI.ActionBarHud.SlotId.Pulse).Remaining.ToString("0.0") +
                     "s pocion=" + bar.Slot(Vhaldren.Gameplay.UI.ActionBarHud.SlotId.Potion).Remaining.ToString("0.0") +
                     "s hechizo1 cargas=" + bar.Slot(Vhaldren.Gameplay.UI.ActionBarHud.SlotId.Spell1).Charges +
                     " ancho=" + (Vhaldren.Gameplay.UI.ActionBarHud.WidthFraction(Screen.width, Screen.height) * 100f).ToString("0") + "%");
            }

            // Avisos: poder de raza y botín (máximo dos a la vez).
            Vhaldren.Gameplay.Races.PlayerRace race = player.GetComponent<Vhaldren.Gameplay.Races.PlayerRace>();
            if (race != null)
                race.TryUsePower();
            Vhaldren.Gameplay.Progression.PlayerEquipment gearForLoot = player.GetComponent<Vhaldren.Gameplay.Progression.PlayerEquipment>();
            if (gearForLoot != null)
                gearForLoot.Notify("Botín: +2 Madera +1 Piel");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("21-hud-avisos.png");

            // Poca vida: la viñeta se tiñe de rojo y late.
            if (health != null)
            {
                health.ResetToFull();
                health.ApplyDamage(health.MaximumHealth * 0.82f);
            }
            yield return new WaitForSeconds(0.5f);
            yield return Shot("22-hud-poca-vida.png");
            if (health != null) health.ResetToFull();

            // Ayuda F1 compacta.
            Vhaldren.Gameplay.Player.PlayerControlsGuide guide = player.GetComponent<Vhaldren.Gameplay.Player.PlayerControlsGuide>();
            if (guide != null)
            {
                guide.SetExpanded(true);
                yield return new WaitForSeconds(0.3f);
                yield return Shot("23-hud-ayuda-f1.png");
                guide.SetExpanded(false);
            }

            // Construcción: la fila de recursos se amplía y aparece el catálogo.
            Vhaldren.Gameplay.Survival.PlayerBuilder builder = player.GetComponent<Vhaldren.Gameplay.Survival.PlayerBuilder>();
            if (builder != null)
            {
                builder.SetBuildMode(true);
                yield return new WaitForSeconds(0.4f);
                yield return Shot("24-hud-construccion.png");
                builder.SetBuildMode(false);
            }

            // Tablón del Cruce abierto con N.
            VillageNoticeboard board = FindAnyObjectByType<VillageNoticeboard>();
            if (board != null)
            {
                body.position = board.transform.position + new Vector3(1.2f, 1.08f, 1.2f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(0.5f);
                yield return Shot("25a-hud-cerca-del-tablon.png");
                board.SetMenuOpen(true);
                yield return new WaitForSeconds(0.3f);
                yield return Shot("25-hud-tablon.png");
                board.SetMenuOpen(false);
            }

            // Armas en las manos, de cerca, en el campamento de bandidos.
            body.position = new Vector3(0f, 1.08f, -21f);
            Physics.SyncTransforms();
            if (follow != null)
                follow.SetZoom(0f, true);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("26-armas-en-las-manos.png");
            foreach (CharacterLoadout loadout in FindObjectsByType<CharacterLoadout>(FindObjectsSortMode.None))
                Note("arma: " + loadout.name + " modelo=" + loadout.ModelKind +
                     " derecha=" + (loadout.RightHandItem != null ? loadout.RightHandItem.name + "@" + loadout.RightHandItem.parent.name : "-") +
                     " izquierda=" + (loadout.LeftHandItem != null ? loadout.LeftHandItem.name + "@" + loadout.LeftHandItem.parent.name : "-"));
            if (health != null) health.ResetToFull();
            yield return Portraits();

            // Golpe desde fuera de la pantalla: un ballestero a distancia con el zoom más cercano.
            Vhaldren.Gameplay.UI.WorldHud world = FindAnyObjectByType<Vhaldren.Gameplay.UI.WorldHud>();
            PrototypeEnemy archer = null;
            foreach (PrototypeEnemy enemy in FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None))
                if (!enemy.IsDefeated && enemy.Archetype != null && enemy.Archetype.Role == EnemyRole.Ranged)
                {
                    archer = enemy;
                    break;
                }
            bool arrowShown = false;
            if (archer != null && world != null)
            {
                // El jugador se coloca al norte del ballestero: con la cámara mirando al norte,
                // el tirador queda detrás de ella (abajo, fuera de la pantalla).
                float until = Time.time + 10f;
                while (Time.time < until && !arrowShown)
                {
                    if (health != null) health.ResetToFull();
                    Vector3 away = archer.transform.position + new Vector3(0f, 0f, 8.4f);
                    if ((body.position - away).sqrMagnitude > 4f)
                    {
                        body.position = new Vector3(away.x, 1.08f, away.z);
                        Physics.SyncTransforms();
                    }
                    yield return new WaitForEndOfFrame();
                    // Se comprueba al final del fotograma, con la flecha ya dibujada.
                    if (world.Warnings.Count > 0)
                    {
                        arrowShown = true;
                        Texture2D image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(Path.Combine(folder, "27-hud-flecha-fuera-de-pantalla.png"), image.EncodeToPNG());
                        Destroy(image);
                        Note("flecha: borde=" + world.Warnings[0].EdgePoint.ToString("F0") + " angulo=" + world.Warnings[0].Angle.ToString("0"));
                    }
                    yield return null;
                }
            }
            Note("flecha_fuera_de_pantalla=" + (arrowShown ? "si" : "no") + (archer != null ? " (" + archer.name + ")" : ""));
            if (health != null) health.ResetToFull();
            if (follow != null)
                follow.SetZoom(1f, true);
            body.position = new Vector3(0f, 1.08f, -24f);
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Retratos de cerca (cámara temporal) de cada tipo de modelo con el arma en la mano:
        /// caballero, bárbaro, bruto, maga, sanadora, ballestero y los dos esqueletos.
        /// </summary>
        private IEnumerator Portraits()
        {
            string[] wanted = { "knight", "barbarian", "barbarian", "mage", "mage", "rogue", "skeleton_warrior", "skeleton_minion" };
            System.Collections.Generic.List<CharacterLoadout> picked = new System.Collections.Generic.List<CharacterLoadout>();
            CharacterLoadout[] all = FindObjectsByType<CharacterLoadout>(FindObjectsSortMode.InstanceID);
            foreach (string kind in wanted)
            {
                foreach (CharacterLoadout candidate in all)
                {
                    if (candidate.ModelKind != kind || picked.Contains(candidate))
                        continue;
                    PrototypeEnemy enemy = candidate.GetComponent<PrototypeEnemy>();
                    if (enemy == null || enemy.IsDefeated || enemy.IsBoss)
                        continue;
                    // Variantes: el bruto (hacha a dos manos) y la sanadora (varita y libro).
                    bool brute = enemy.Archetype != null && enemy.Archetype.Role == EnemyRole.Brute;
                    bool support = enemy.Archetype != null && enemy.Archetype.Role == EnemyRole.Support;
                    int sameKind = picked.FindAll(p => p.ModelKind == kind).Count;
                    if (kind == "barbarian" && brute != (sameKind == 1))
                        continue;
                    if (kind == "mage" && support != (sameKind == 1))
                        continue;
                    picked.Add(candidate);
                    break;
                }
            }
            if (picked.Count == 0)
                yield break;

            foreach (CharacterLoadout loadout in picked)
                foreach (Animator animator in loadout.GetComponentsInChildren<Animator>())
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            yield return new WaitForSeconds(0.3f);

            const int tileW = 300, tileH = 380;
            Texture2D sheet = new Texture2D(tileW * 4, tileH * 2, TextureFormat.RGB24, false);
            RenderTexture target = new RenderTexture(tileW, tileH, 24);
            GameObject rig = new GameObject("Camara de retratos");
            Camera photo = rig.AddComponent<Camera>();
            photo.fieldOfView = 32f;
            photo.nearClipPlane = 0.05f;
            photo.clearFlags = CameraClearFlags.SolidColor;
            photo.backgroundColor = new Color(0.09f, 0.07f, 0.08f);
            photo.targetTexture = target;
            photo.enabled = false;
            yield return new WaitForEndOfFrame();
            for (int i = 0; i < picked.Count && i < 8; i++)
            {
                Transform t = picked[i].transform;
                Vector3 front = t.forward;
                front.y = 0f;
                if (front.sqrMagnitude < 0.01f) front = Vector3.forward;
                front.Normalize();
                // Tres cuartos por delante y a la derecha, para ver las dos manos.
                Vector3 side = Quaternion.Euler(0f, 35f, 0f) * front;
                // Encuadre por el tamaño real del modelo (cuerpo y armas).
                Bounds bounds = new Bounds(t.position, Vector3.one * 0.5f);
                bool first = true;
                foreach (Renderer r in picked[i].GetComponentsInChildren<Renderer>())
                {
                    // Solo el cuerpo animado y las armas (no discos de aviso ni brillos del suelo).
                    bool weapon = (picked[i].RightHandItem != null && r.transform.IsChildOf(picked[i].RightHandItem)) ||
                                  (picked[i].LeftHandItem != null && r.transform.IsChildOf(picked[i].LeftHandItem));
                    if (!(r is SkinnedMeshRenderer) && !weapon || !r.enabled || !r.gameObject.activeInHierarchy)
                        continue;
                    if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                Vector3 look = bounds.center;
                float distance = Mathf.Max(2.2f, bounds.extents.y * 2f / Mathf.Tan(photo.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.62f);
                rig.transform.position = look + side * distance + Vector3.up * distance * 0.32f;
                rig.transform.LookAt(look);
                photo.Render();
                RenderTexture.active = target;
                sheet.ReadPixels(new Rect(0f, 0f, tileW, tileH), (i % 4) * tileW, (1 - i / 4) * tileH);
                RenderTexture.active = null;
                Note("retrato " + (i + 1) + ": " + t.name + " (" + picked[i].ModelKind + ") " +
                     (picked[i].RightHandItem != null ? picked[i].RightHandItem.name : "-") + " / " +
                     (picked[i].LeftHandItem != null ? picked[i].LeftHandItem.name : "-"));
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(folder, "28-armas-retratos.png"), sheet.EncodeToPNG());
            photo.targetTexture = null;
            Destroy(rig);
            Destroy(target);
            Destroy(sheet);
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
