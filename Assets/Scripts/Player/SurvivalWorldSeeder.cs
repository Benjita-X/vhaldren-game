using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Prepara recursos en una escena ya generada, sin regenerarla: convierte los arboles
    /// del bosque en nodos de madera y coloca rocas y arbustos de bayas cerca del inicio.
    /// Se llama una vez al entrar en Play desde PrototypeGameSession.
    /// </summary>
    public static class SurvivalWorldSeeder
    {
        private static Material rockMaterial;
        private static Material bushMaterial;

        public static void Seed(Scene scene, Vector3 playerSpawn)
        {
            if (!scene.IsValid() || Object.FindAnyObjectByType<SurvivalResource>() != null)
                return;

            int trees = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Tronco" || t.parent == null || t.parent.name != "Arbol sombrio")
                        continue;
                    if (t.GetComponent<Collider>() == null)
                        continue;
                    t.gameObject.AddComponent<SurvivalResource>()
                        .Configure(SurvivalResource.Kind.Wood, 12, 2, t.parent);
                    trees++;
                }

            // Rocas y arbustos en anillo alrededor del cruce: probar sin viajar lejos.
            var rng = new System.Random(20261111);
            int rocks = 0, bushes = 0, attempts = 0;
            while ((rocks < 10 || bushes < 8) && attempts++ < 300)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = 6f + (float)rng.NextDouble() * 16f;
                Vector3 p = new Vector3(playerSpawn.x + Mathf.Cos(a) * d, 0f, playerSpawn.z + Mathf.Sin(a) * d);
                bool rock = rocks < 10 && (bushes >= 8 || rng.NextDouble() < 0.55);
                float size = rock ? 1.1f + (float)rng.NextDouble() * 0.5f : 0.9f;
                if (!TryGround(p, out Vector3 ground) ||
                    Physics.CheckSphere(ground + Vector3.up * (size * 0.5f + 0.1f), size * 0.6f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
                if (rock)
                {
                    SpawnRock(ground, size, (float)rng.NextDouble() * 360f);
                    rocks++;
                }
                else
                {
                    SpawnBush(ground);
                    bushes++;
                }
            }
            Debug.Log("[Vhaldren] Supervivencia: " + trees + " arboles talables, " + rocks +
                " rocas y " + bushes + " arbustos de bayas.");
        }

        private static bool TryGround(Vector3 p, out Vector3 ground)
        {
            ground = p;
            if (!Physics.Raycast(p + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.normal.y < 0.8f || hit.point.y > 0.5f)
                return false;
            ground = hit.point;
            return true;
        }

        private static void SpawnRock(Vector3 ground, float size, float yaw)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Roca (pico)";
            rock.transform.position = ground + Vector3.up * size * 0.35f;
            rock.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            rock.transform.localScale = new Vector3(size * 1.3f, size * 0.8f, size);
            rock.GetComponent<Renderer>().sharedMaterial = rockMaterial != null ? rockMaterial
                : rockMaterial = Lit("Roca recolectable", new Color(0.42f, 0.42f, 0.46f));
            rock.AddComponent<SurvivalResource>().Configure(SurvivalResource.Kind.Stone, 10, 2, null);
        }

        private static void SpawnBush(Vector3 ground)
        {
            GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Arbusto de bayas";
            bush.transform.position = ground + Vector3.up * 0.45f;
            bush.transform.localScale = new Vector3(1.1f, 0.9f, 1.1f);
            bush.GetComponent<Renderer>().sharedMaterial = bushMaterial != null ? bushMaterial
                : bushMaterial = Lit("Arbusto", new Color(0.16f, 0.32f, 0.18f));
            // Bayas rojas visibles: se ve de lejos que se puede recoger.
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Combat.PrototypeVfx.CreateShape(PrimitiveType.Sphere, "Baya", bush.transform,
                    bush.transform.position + new Vector3(Mathf.Cos(a) * 0.5f, 0.15f, Mathf.Sin(a) * 0.5f),
                    Vector3.one * 0.15f, new Color(0.9f, 0.1f, 0.25f)).transform.localScale =
                    new Vector3(0.14f, 0.17f, 0.14f);
            }
            bush.AddComponent<SurvivalResource>().Configure(SurvivalResource.Kind.Berries, 6, 1, null);
        }

        private static Material Lit(string name, Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            return new Material(shader) { name = name, color = color };
        }
    }
}
