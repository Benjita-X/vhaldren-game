using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.CameraSystem;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Tests
{
    /// <summary>
    /// Camara cenital de accion en escena: encuadre, limites del zoom con angulo constante,
    /// giro alrededor del personaje, terreno y objetos que tapan al personaje.
    /// </summary>
    public sealed class TopDownCameraPlayModeTests
    {
        private const float Pitch = 54f;
        private const float DefaultDistance = 22f;
        private readonly List<Object> created = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object item in created)
                if (item != null)
                    Object.Destroy(item);
            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActionViewIsTiltedWithNarrowFovAndLooksAtTheCharacter()
        {
            SimpleFollowCamera follow = CreateActionCamera(new Vector3(3f, 1.08f, -2f), out Transform target);
            Camera view = follow.GetComponent<Camera>();
            yield return null;

            Vector3 look = target.position + Vector3.up * 0.5f;
            Assert.That(view.fieldOfView, Is.EqualTo(40f).Within(0.01f));
            Assert.That(Vector3.Distance(view.transform.position, look), Is.EqualTo(DefaultDistance).Within(0.05f));
            Assert.That(PitchOf(view.transform), Is.EqualTo(Pitch).Within(0.05f));
            Assert.That(follow.PitchDegrees, Is.EqualTo(Pitch).Within(0.05f));
            Assert.That(Vector3.Angle(view.transform.forward, (look - view.transform.position).normalized), Is.LessThan(0.05f),
                "El centro de la imagen es el personaje.");
            // A 16:9, unos 28 m de ancho visible a la distancia del personaje.
            float width = 2f * DefaultDistance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * 16f / 9f;
            Assert.That(width, Is.InRange(27f, 30f));
        }

        [UnityTest]
        public IEnumerator ZoomStaysBetween14And30MetresAndKeepsTheAngle()
        {
            SimpleFollowCamera follow = CreateActionCamera(new Vector3(0f, 1.08f, 0f), out Transform target);
            Transform view = follow.transform;
            Quaternion angle = view.rotation;
            Vector3 look = target.position + Vector3.up * 0.5f;

            follow.SetZoom(0f, true);
            follow.SetTarget(target);
            yield return null;
            Assert.That(Vector3.Distance(view.position, look), Is.EqualTo(14f).Within(0.05f), "Zoom minimo: 14 m.");
            Assert.That(Quaternion.Angle(view.rotation, angle), Is.LessThan(0.01f), "El zoom no cambia el angulo.");
            Assert.That(Vector3.Angle(view.forward, (look - view.position).normalized), Is.LessThan(0.05f),
                "Acerca sobre la misma linea: el personaje sigue en el centro.");

            follow.AddZoom(100f);
            Assert.That(follow.TargetZoom * follow.DefaultDistance, Is.EqualTo(30f).Within(0.01f), "Zoom maximo: 30 m.");
            follow.SetZoom(100f, true);
            follow.SetTarget(target);
            yield return null;
            Assert.That(Vector3.Distance(view.position, look), Is.EqualTo(30f).Within(0.05f));
            Assert.That(Quaternion.Angle(view.rotation, angle), Is.LessThan(0.01f));
            Assert.That(follow.MinDistance, Is.EqualTo(14f).Within(0.01f));
            Assert.That(follow.MaxDistance, Is.EqualTo(30f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ChangingTheDefaultDistanceKeepsThePlayerZoomOtherwise()
        {
            SimpleFollowCamera follow = CreateActionCamera(new Vector3(0f, 1.08f, 0f), out Transform target);
            follow.AddZoom(0.2f);
            float chosen = follow.TargetZoom;
            // Repetir la misma configuracion (otra opcion cambiada) no deshace el zoom del jugador.
            follow.ConfigureActionView(Pitch, DefaultDistance, 14f, 30f, 40f, false);
            Assert.That(follow.TargetZoom, Is.EqualTo(chosen).Within(0.0001f));
            // Una distancia por defecto nueva si se aplica, sin saltos: se llega suavemente.
            follow.ConfigureActionView(Pitch, 26f, 14f, 30f, 40f, false);
            Assert.That(follow.TargetZoom * follow.DefaultDistance, Is.EqualTo(26f).Within(0.01f));
            float until = Time.time + 2f;
            while (Time.time < until && Mathf.Abs(follow.Distance - 26f) > 0.05f)
                yield return null;
            Assert.That(Vector3.Distance(follow.transform.position, target.position + Vector3.up * 0.5f),
                Is.EqualTo(26f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator RotationOrbitsTheCharacterOnlyWhenAllowedAndResets()
        {
            SimpleFollowCamera follow = CreateActionCamera(new Vector3(0f, 1.08f, 0f), out Transform target);
            Transform view = follow.transform;
            Vector3 look = target.position + Vector3.up * 0.5f;

            follow.AddYaw(90f);
            Assert.That(follow.TargetYaw, Is.EqualTo(0f), "Sin permiso el giro se ignora (salas de prueba).");

            follow.AllowRotation = true;
            follow.AddYaw(90f);
            follow.SetYaw(follow.TargetYaw, true);
            yield return null;
            Assert.That(view.position.x, Is.LessThan(look.x - 10f), "Girada 90 grados queda al oeste...");
            Vector3 flat = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Assert.That(Vector3.Angle(flat, Vector3.right), Is.LessThan(0.1f), "...mirando hacia +X.");
            Assert.That(Vector3.Distance(view.position, look), Is.EqualTo(DefaultDistance).Within(0.05f));
            Assert.That(PitchOf(view), Is.EqualTo(Pitch).Within(0.05f), "El giro no cambia la inclinacion.");

            follow.ResetYaw();
            float until = Time.time + 2f;
            while (Time.time < until && Mathf.Abs(follow.Yaw) > 0.01f)
                yield return null;
            Assert.That(follow.Yaw, Is.EqualTo(0f).Within(0.01f));
            Assert.That(view.position.z, Is.LessThan(look.z - 10f), "Vuelve al angulo por defecto (camara al sur).");

            follow.AllowRotation = false;
            follow.SetYaw(45f, true);
            follow.AllowRotation = false;
            Assert.That(follow.TargetYaw, Is.EqualTo(0f), "Al desactivar el giro vuelve al angulo por defecto.");
        }

        [UnityTest]
        public IEnumerator TerrainPullsTheCameraInButWallsDoNotInTheWorldMode()
        {
            SimpleFollowCamera follow = CreateActionCamera(new Vector3(0f, 1.08f, 1f), out Transform target);
            follow.ObstructionTerrainOnly = true;
            Vector3 look = target.position + Vector3.up * 0.5f;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 3f, -3f);
            wall.transform.localScale = new Vector3(6f, 6f, 0.5f);
            created.Add(wall);
            Physics.SyncTransforms();
            follow.SetTarget(target);
            Assert.That(Vector3.Distance(follow.transform.position, look), Is.EqualTo(DefaultDistance).Within(0.05f),
                "Un muro no acerca la camara: se oculta (CameraOcclusionHider).");

            // Ladera empinada detras del personaje: la camara se acerca y nunca queda dentro.
            const int resolution = 33;
            TerrainData data = new TerrainData { heightmapResolution = resolution };
            data.size = new Vector3(40f, 60f, 40f);
            float[,] heights = new float[resolution, resolution];
            for (int row = 0; row < resolution; row++)
            {
                float metresFromEdge = 40f - row * (40f / (resolution - 1));
                for (int column = 0; column < resolution; column++)
                    heights[row, column] = Mathf.Clamp01(metresFromEdge * 3f / 60f);
            }
            data.SetHeights(0, 0, heights);
            created.Add(data);
            GameObject hill = Terrain.CreateTerrainGameObject(data);
            hill.transform.position = new Vector3(-20f, 0f, -40f);
            created.Add(hill);
            Physics.SyncTransforms();
            follow.SetTarget(target);

            Vector3 cameraPosition = follow.transform.position;
            Assert.That(Vector3.Distance(cameraPosition, look), Is.LessThan(DefaultDistance - 1f),
                "El terreno acerca la camara.");
            float ground = hill.GetComponent<Terrain>().SampleHeight(cameraPosition) + hill.transform.position.y;
            Assert.That(cameraPosition.y, Is.GreaterThan(ground), "La camara nunca queda dentro del terreno.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ObjectBetweenCameraAndPlayerIsHiddenAndRestored()
        {
            SimpleFollowCamera follow = CreateActionCamera(new Vector3(0f, 1.08f, 0f), out Transform target);
            follow.ObstructionTerrainOnly = true;
            follow.SetTarget(target);

            // En la linea de vista: un muro (se oculta) y un enemigo (nunca se oculta).
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 3f, -3f);
            wall.transform.localScale = new Vector3(6f, 6f, 0.5f);
            created.Add(wall);
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            enemy.AddComponent<CombatHealth>();
            enemy.transform.position = Vector3.Lerp(follow.transform.position, target.position + Vector3.up * 0.5f, 0.45f);
            created.Add(enemy);
            GameObject aside = GameObject.CreatePrimitive(PrimitiveType.Cube);
            aside.transform.position = new Vector3(9f, 1f, 0f);
            created.Add(aside);

            CameraOcclusionHider hider = follow.gameObject.AddComponent<CameraOcclusionHider>();
            hider.RestoreDelay = 0f;
            hider.SetTarget(target);
            hider.Refresh();

            Renderer wallRenderer = wall.GetComponent<Renderer>();
            Assert.That(wallRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly),
                "El muro que tapa queda invisible pero sigue dando sombra.");
            Assert.That(hider.IsHidden(wallRenderer), Is.True);
            Assert.That(enemy.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.On),
                "Los enemigos nunca se ocultan.");
            Assert.That(aside.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.On),
                "Lo que no tapa no se toca.");

            wall.transform.position = new Vector3(14f, 3f, -3f);
            yield return null;
            hider.Refresh();
            Assert.That(wallRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On), "Al apartarse vuelve a verse.");
            Assert.That(hider.IsHidden(wallRenderer), Is.False);

            wall.transform.position = new Vector3(0f, 3f, -3f);
            hider.Refresh();
            Assert.That(wallRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));
            hider.enabled = false;
            Assert.That(wallRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On),
                "Al desactivarse restaura todo lo que oculto.");
        }

        private SimpleFollowCamera CreateActionCamera(Vector3 targetPosition, out Transform target)
        {
            GameObject targetObject = new GameObject("CameraTarget");
            targetObject.transform.position = targetPosition;
            created.Add(targetObject);
            target = targetObject.transform;

            GameObject cameraObject = new GameObject("ActionCamera");
            created.Add(cameraObject);
            cameraObject.AddComponent<Camera>();
            SimpleFollowCamera follow = cameraObject.AddComponent<SimpleFollowCamera>();
            follow.SetTarget(target);
            follow.ConfigureActionView(Pitch, DefaultDistance, 14f, 30f, 40f, true);
            return follow;
        }

        private static float PitchOf(Transform view) => Mathf.Asin(Mathf.Clamp(-view.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    /// <summary>Entrada del jugador con la camara cenital: WASD, giro con el raton y orientacion.</summary>
    public sealed class TopDownInputPlayModeTests : InputTestFixture
    {
        [Test]
        public void WasdFollowsTheCameraTurned90Degrees()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            CreatePlayerAndCamera(Vector3.zero, out GameObject player, out PlayerMotor motor,
                out LocalPlayerInput input, out SimpleFollowCamera follow);
            try
            {
                follow.SetYaw(90f, true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                InvokeUpdate(input);
                Assert.That(motor.HorizontalVelocity.x, Is.EqualTo(6f).Within(0.01f),
                    "Con la camara girada 90 grados, W avanza hacia +X.");
                Assert.That(Mathf.Abs(motor.HorizontalVelocity.z), Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(follow.gameObject);
            }
        }

        [Test]
        public void WasdWithoutCameraRotationIsUnchanged()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            CreatePlayerAndCamera(Vector3.zero, out GameObject player, out PlayerMotor motor,
                out LocalPlayerInput input, out SimpleFollowCamera follow);
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                InvokeUpdate(input);
                Assert.That(motor.HorizontalVelocity.z, Is.EqualTo(6f).Within(0.01f), "W = +Z, como siempre.");
                Assert.That(Mathf.Abs(motor.HorizontalVelocity.x), Is.LessThan(0.01f));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                InputSystem.Update();
                InvokeUpdate(input);
                Assert.That(motor.HorizontalVelocity.x, Is.EqualTo(6f).Within(0.01f), "D = +X, como siempre.");
                Assert.That(Mathf.Abs(motor.HorizontalVelocity.z), Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(follow.gameObject);
            }
        }

        [Test]
        public void MiddleMouseDragTurnsTheCameraAndHomeResetsIt()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            CreatePlayerAndCamera(Vector3.zero, out GameObject player, out PlayerMotor motor,
                out LocalPlayerInput input, out SimpleFollowCamera follow);
            try
            {
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                MouseState drag = new MouseState { position = center, delta = new Vector2(100f, 0f) }
                    .WithButton(MouseButton.Middle);
                InputSystem.QueueStateEvent(mouse, drag);
                InputSystem.Update();
                InvokeUpdate(input);
                Assert.That(follow.TargetYaw, Is.EqualTo(0f), "Sin permiso de giro (salas de prueba) no gira.");

                follow.AllowRotation = true;
                InputSystem.QueueStateEvent(mouse, drag);
                InputSystem.Update();
                InvokeUpdate(input);
                Assert.That(follow.TargetYaw, Is.GreaterThan(1f), "Arrastrar a la derecha con el boton central gira la vista.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home));
                InputSystem.Update();
                InvokeUpdate(input);
                Assert.That(follow.TargetYaw, Is.EqualTo(0f), "Inicio vuelve al angulo por defecto.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(follow.gameObject);
            }
        }

        [Test]
        public void ExplorationFacesWhereYouWalkUntilAnAttackTurnsYouToTheCursor()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            GameObject player = new GameObject("FacingModesPlayer");
            player.transform.position = new Vector3(0f, 1.08f, 6f);
            player.AddComponent<CharacterController>();
            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            LocalPlayerInput input = player.AddComponent<LocalPlayerInput>();
            // Camara fija de las salas: el centro de la pantalla cae detras del personaje.
            GameObject cameraObject = new GameObject("FacingModesCamera");
            cameraObject.transform.position = new Vector3(0f, 10f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            Camera view = cameraObject.AddComponent<Camera>();
            try
            {
                SetViewCamera(input, view);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();

                InvokeUpdate(input);
                Assert.That(input.CombatFacing, Is.False);
                Assert.That(Vector3.Angle(motor.CurrentFacing, Vector3.forward), Is.LessThan(1f),
                    "Explorando mira hacia donde camina, no al cursor que esta detras.");

                input.NotifyCombatAction();
                Assert.That(input.CombatFacing, Is.True);
                Assert.That(Vector3.Angle(motor.CurrentFacing, Vector3.back), Is.LessThan(1f),
                    "Al atacar mira al cursor al instante: el golpe sale hacia alli.");

                InvokeUpdate(input);
                Assert.That(Vector3.Angle(motor.CurrentFacing, Vector3.back), Is.LessThan(1f),
                    "En combate sigue mirando al cursor aunque camine en otra direccion.");
                Assert.That(motor.HorizontalVelocity.z, Is.EqualTo(6f).Within(0.01f),
                    "Camina hacia delante mirando atras: las piernas retroceden.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void CreatePlayerAndCamera(Vector3 position, out GameObject player, out PlayerMotor motor,
            out LocalPlayerInput input, out SimpleFollowCamera follow)
        {
            player = new GameObject("TopDownInputPlayer");
            player.transform.position = position;
            player.AddComponent<CharacterController>();
            motor = player.AddComponent<PlayerMotor>();
            input = player.AddComponent<LocalPlayerInput>();
            GameObject cameraObject = new GameObject("TopDownInputCamera");
            Camera view = cameraObject.AddComponent<Camera>();
            follow = cameraObject.AddComponent<SimpleFollowCamera>();
            follow.SetTarget(player.transform);
            follow.ConfigureActionView(54f, 22f, 14f, 30f, 40f, true);
            SetViewCamera(input, view);
        }

        private static void SetViewCamera(LocalPlayerInput input, Camera view)
        {
            typeof(LocalPlayerInput).GetField("viewCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(input, view);
        }

        private static void InvokeUpdate(LocalPlayerInput input)
        {
            typeof(LocalPlayerInput).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(input, null);
        }
    }

    /// <summary>El mundo jugable configura la camara cenital y oculta lo que tapa al personaje.</summary>
    public sealed class TopDownWorldCameraPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene world = SceneManager.GetSceneByName("WorldPrototype001");
            if (world.IsValid() && world.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("VaciaTrasCamara");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(world);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldUsesTheActionCameraFromTheOptions()
        {
            yield return SceneManager.LoadSceneAsync("WorldPrototype001", LoadSceneMode.Single);
            yield return new WaitForSeconds(1f);

            Camera view = Camera.main;
            Assert.That(view, Is.Not.Null);
            SimpleFollowCamera follow = view.GetComponent<SimpleFollowCamera>();
            Assert.That(follow, Is.Not.Null);
            GameObject player = GameObject.Find("PlayerRoot");
            Assert.That(follow.Target, Is.EqualTo(player.transform));
            Assert.That(view.fieldOfView, Is.EqualTo(PrototypeGameSession.ActionCameraFieldOfView).Within(0.01f));
            Assert.That(follow.PitchDegrees, Is.EqualTo(PrototypeGameSession.ActionCameraPitch).Within(0.05f));
            Assert.That(follow.DefaultDistance, Is.EqualTo(GameSettings.CameraDistance).Within(0.01f));
            Assert.That(follow.MinDistance, Is.EqualTo(GameSettings.CameraDistanceMin).Within(0.01f));
            Assert.That(follow.MaxDistance, Is.EqualTo(GameSettings.CameraDistanceMax).Within(0.01f));
            Assert.That(follow.AllowRotation, Is.EqualTo(GameSettings.CameraRotation));
            Assert.That(follow.LookAheadEnabled, Is.EqualTo(GameSettings.CameraLookAhead));
            Assert.That(follow.ObstructionTerrainOnly, Is.True);
            CameraOcclusionHider hider = view.GetComponent<CameraOcclusionHider>();
            Assert.That(hider, Is.Not.Null);
            Assert.That(hider.Target, Is.EqualTo(player.transform));
            Assert.That(hider.OccluderCount, Is.GreaterThan(20), "Arboles, muros, pilares y rocas del mundo.");
        }
    }
}
