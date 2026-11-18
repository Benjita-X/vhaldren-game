using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    /// <summary>
    /// Carga el mapa grande (MundoVhaldren) y comprueba lo esencial: terreno grande con
    /// relieve, jugador y enemigos sobre el suelo, jefes, lugares y que nada falle en consola
    /// al recorrerlo.
    /// </summary>
    public sealed class MundoGrandePlayModeTests
    {
        private const string Escena = "MundoVhaldren";
        private readonly List<string> errors = new List<string>();

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(condition);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Scene world = SceneManager.GetSceneByName(Escena);
            if (world.IsValid() && world.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("VaciaTrasMundoGrande");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(world);
            }
            yield return null;
        }

        private static float Suelo(Terrain t, Vector3 p) => t.SampleHeight(p) + t.transform.position.y;

        [UnityTest]
        public IEnumerator MapaGrandeCargaConRelieveYTodoSobreElSuelo()
        {
            errors.Clear();
            Application.logMessageReceived += OnLog;
            yield return SceneManager.LoadSceneAsync(Escena, LoadSceneMode.Single);
            yield return new WaitForSeconds(4f);

            Terrain terrain = Terrain.activeTerrain;
            Assert.That(terrain, Is.Not.Null, "El mapa grande necesita un Terrain.");
            Vector3 size = terrain.terrainData.size;
            Assert.That(size.x, Is.GreaterThanOrEqualTo(1500f), "El mapa debe ser grande (≥ 1,5 km).");

            // relieve: diferencia de alturas notable (montañas y valles)
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 40; i++)
                for (int j = 0; j < 40; j++)
                {
                    float h = terrain.terrainData.GetInterpolatedHeight(i / 39f, j / 39f);
                    min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                }
            Assert.That(max - min, Is.GreaterThan(60f), "El terreno no debe sentirse plano.");

            GameObject player = GameObject.Find("PlayerRoot");
            Assert.That(player, Is.Not.Null);
            float dy = player.transform.position.y - Suelo(terrain, player.transform.position);
            Assert.That(dy, Is.InRange(-0.5f, 2.5f), "El jugador debe empezar sobre el suelo.");

            PrototypeEnemy[] enemies = Object.FindObjectsByType<PrototypeEnemy>(FindObjectsSortMode.None);
            Assert.That(enemies.Length, Is.GreaterThanOrEqualTo(20), "El mapa grande tiene campamentos repartidos.");
            int bosses = 0;
            foreach (PrototypeEnemy e in enemies)
            {
                if (e.IsBoss) bosses++;
                Assert.That(e.GetComponent<CombatHealth>(), Is.Not.Null, e.name);
                float sobre = e.transform.position.y - Suelo(terrain, e.transform.position);
                Assert.That(sobre, Is.GreaterThan(-1f), e.name + " está bajo el suelo.");
            }
            Assert.That(bosses, Is.GreaterThanOrEqualTo(1), "Al menos un jefe de región.");

            GameObject lugares = GameObject.Find("Mundo de Vhaldren/Lugares");
            Assert.That(lugares, Is.Not.Null);
            Assert.That(lugares.transform.childCount, Is.GreaterThanOrEqualTo(8), "Muchos lugares que explorar.");

            // recorrer varios lugares: el jugador aterriza en el suelo y no hay errores
            CharacterController cc = player.GetComponent<CharacterController>();
            int visitados = 0;
            foreach (Transform l in lugares.transform)
            {
                if (visitados >= 5) break;
                visitados++;
                Vector3 p = l.position + new Vector3(3f, 0f, -8f);
                p.y = Suelo(terrain, p) + 1.5f;
                cc.enabled = false;
                player.transform.position = p;
                cc.enabled = true;
                yield return new WaitForSeconds(1.2f);
                float sobre = player.transform.position.y - Suelo(terrain, player.transform.position);
                Assert.That(sobre, Is.InRange(-0.6f, 2.6f), "Jugador flotando o enterrado en " + l.name);
            }
            Assert.That(errors, Is.Empty, "Errores de consola en el mapa grande: " + string.Join(" | ", errors));
        }
    }
}
