using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Magic;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    /// <summary>
    /// Carga el mundo jugable completo y lo deja correr: detecta errores de consola,
    /// enemigos sin tipo y componentes de magia que falten en el jugador real.
    /// </summary>
    public sealed class WorldSmokePlayModeTests
    {
        private readonly List<string> errors = new List<string>();

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(condition);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // No dejar el mundo (ni su equipo/ciclo global) activo para otras pruebas.
            Scene world = SceneManager.GetSceneByName("WorldPrototype001");
            if (world.IsValid() && world.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("VaciaTrasMundo");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(world);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldLoadsWithBestiaryAndMagicAndRunsWithoutErrors()
        {
            Application.logMessageReceived += OnLog;
            try
            {
                yield return SceneManager.LoadSceneAsync("WorldPrototype001", LoadSceneMode.Single);
                yield return new WaitForSeconds(3f);

                PrototypeEnemy[] enemies = Object.FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None);
                Assert.That(enemies.Length, Is.EqualTo(19));
                int typed = 0;
                HashSet<EnemyRole> roles = new HashSet<EnemyRole>();
                foreach (PrototypeEnemy enemy in enemies)
                {
                    Assert.That(enemy.GetComponent<EssenceSource>(), Is.Not.Null, enemy.name + " sin esencia.");
                    if (enemy.Archetype == null)
                        continue;
                    typed++;
                    roles.Add(enemy.Archetype.Role);
                    Assert.That(enemy.GetComponent<EnemyArchetypeVisual>().BuiltFor, Is.EqualTo(enemy.Archetype.Id));
                    Assert.That(enemy.GetComponent<CombatHealth>().MaximumHealth, Is.EqualTo(enemy.Archetype.Health));
                }
                Assert.That(typed, Is.EqualTo(13));
                Assert.That(roles.Count, Is.EqualTo(7), "Todos los roles del bestiario aparecen en el mapa.");

                GameObject player = GameObject.Find("PlayerRoot");
                Assert.That(player, Is.Not.Null);
                PlayerSpellbook book = player.GetComponent<PlayerSpellbook>();
                Assert.That(book, Is.Not.Null);
                Assert.That(player.GetComponent<PlayerFeeding>(), Is.Not.Null);
                Assert.That(player.GetComponent<CombatState>(), Is.Not.Null);
                Assert.That(player.GetComponent<Vhaldren.Gameplay.Progression.PlayerEquipment>(), Is.Not.Null);
                foreach (PrototypeEnemy enemy in enemies)
                {
                    Assert.That(enemy.GetComponent<Vhaldren.Gameplay.Progression.CombatLevel>(), Is.Not.Null);
                    Assert.That(enemy.GetComponent<Vhaldren.Gameplay.Progression.EnemyLoot>(), Is.Not.Null);
                }

                // Lanzar un hechizo de cada tipo en el mundo real no debe generar errores.
                Vector3 ahead = player.transform.position + Vector3.forward * 6f;
                Assert.That(book.TryCast(0, ahead), Is.True);
                yield return new WaitForSeconds(0.5f);
                Assert.That(book.TryCast(1, ahead), Is.True);
                yield return new WaitForSeconds(1.2f);
                Assert.That(book.TryCast(2, ahead), Is.True);
                yield return new WaitForSeconds(1.6f);
                Assert.That(errors, Is.Empty, "Errores en consola: " + string.Join(" | ", errors));
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
        }
    }
}
