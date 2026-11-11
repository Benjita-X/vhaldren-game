using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    /// <summary>
    /// Recorre la escena real Movement001 con comandos al motor (sin teclado): comprueba que
    /// la escena guardada, y no solo una escena sintetica de prueba, se puede jugar.
    /// </summary>
    public sealed class Movement001ScenePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Movement001.unity";
        private const float Step = 1f / 120f;
        private const float RestHeight = 1.08f;

        private Transform player;
        private PlayerMotor motor;
        private CharacterController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True,
                "Movement001 debe estar en Build Settings para poder cargarse.");

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "PlayerRoot")
                    player = root.transform;
            }
            Assert.That(player, Is.Not.Null);

            motor = player.GetComponent<PlayerMotor>();
            controller = player.GetComponent<CharacterController>();
            // La prueba conduce el motor; la entrada real no debe simular por su cuenta.
            player.GetComponent<LocalPlayerInput>().enabled = false;
            player.GetComponent<LocalPlayerShooter>().enabled = false;
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(ScenePath);
        }

        [UnityTest]
        public IEnumerator StartsRestingOnTheGroundWithoutSinkingOrFloating()
        {
            Walk(Vector3.zero, 0.5f);

            Assert.That(player.position.y, Is.EqualTo(RestHeight).Within(0.03f));
            Assert.That(motor.IsGrounded, Is.True);
            yield break;
        }

        [UnityTest]
        public IEnumerator WalkingForwardStopsAtTheWall()
        {
            Walk(Vector3.forward, 2f);

            // Cara de la pared en z = 4,5; radio 0,4 + skinWidth 0,08.
            Assert.That(player.position.z, Is.InRange(3.85f, 4.1f));
            Assert.That(player.position.y, Is.EqualTo(RestHeight).Within(0.03f));
            yield break;
        }

        [UnityTest]
        public IEnumerator WalkingBackPassesThroughTheNarrowGap()
        {
            Walk(Vector3.back, 2f);

            Assert.That(player.position.z, Is.LessThan(-10f), "El hueco de 1,4 m debe dejar pasar al personaje.");
            Assert.That(Mathf.Abs(player.position.x), Is.LessThan(0.2f));
            yield break;
        }

        [UnityTest]
        public IEnumerator SlidesAlongTheDiagonalWallWithoutCrossingIt()
        {
            Teleport(new Vector3(-7f, RestHeight, 0f));
            float startX = player.position.x;

            // Un segundo: llega a la pared y se desliza sin alcanzar todavia su extremo.
            Walk(Vector3.forward, 1f);

            Assert.That(player.position.x, Is.LessThan(startX - 0.8f), "Debe deslizarse a lo largo de la pared.");
            // La pared sigue la recta z = -3 - x: el personaje debe quedar del lado de partida.
            Assert.That(player.position.z + player.position.x + 3f, Is.LessThan(0f));
            yield break;
        }

        [UnityTest]
        public IEnumerator ClimbsTheLowStepByWalking()
        {
            Teleport(new Vector3(-9f, RestHeight, -1f));

            Walk(Vector3.back, 0.9f);

            Assert.That(player.position.z, Is.InRange(-8f, -4.5f));
            Assert.That(player.position.y, Is.EqualTo(RestHeight + 0.25f).Within(0.04f));
            yield break;
        }

        [UnityTest]
        public IEnumerator ClimbsTheRampAndFallsBackToTheGround()
        {
            Teleport(new Vector3(9f, RestHeight, -11f));
            float highest = player.position.y;

            for (int i = 0; i < 360; i++)
            {
                motor.Simulate(new PlayerMotionCommand(Vector3.forward, Vector3.forward), Step);
                highest = Mathf.Max(highest, player.position.y);
            }

            Assert.That(highest, Is.GreaterThan(RestHeight + 1.2f), "Debe subir la rampa de 12 grados.");
            Assert.That(player.position.z, Is.GreaterThan(0f));
            Assert.That(player.position.y, Is.EqualTo(RestHeight).Within(0.03f), "Tras caer debe quedar en el suelo.");
            Assert.That(motor.IsGrounded, Is.True);
            yield break;
        }

        [UnityTest]
        public IEnumerator DashingIntoTheWallStopsAtTheWall()
        {
            Walk(Vector3.zero, 0.1f);
            motor.Simulate(new PlayerMotionCommand(Vector3.forward, Vector3.forward, true), Step);
            Walk(Vector3.forward, 1.5f);

            Assert.That(player.position.z, Is.InRange(3.85f, 4.1f));
            Assert.That(player.GetComponent<DashFeedback>(), Is.Not.Null);
            yield break;
        }

        [UnityTest]
        public IEnumerator CameraKeepsItsDistanceWhenALowWallIsBetween()
        {
            // Justo detras de la pared de 2 m: desde la camara elevada el personaje se sigue
            // viendo. La camara no debe lanzarse contra su cabeza en cada pared baja.
            Camera view = Camera.main;
            Assert.That(view, Is.Not.Null);
            Teleport(new Vector3(0f, RestHeight, 6.2f));
            for (int i = 0; i < 30; i++)
                yield return null;

            float distance = Vector3.Distance(view.transform.position, player.position);
            Assert.That(distance, Is.GreaterThan(9f),
                "La camara se acerco de golpe por una pared baja (distancia " + distance.ToString("0.0") + " m).");
        }

        [UnityTest]
        public IEnumerator AnimatedCharacterLegsFollowMovementRelativeToAim()
        {
            Animator animator = player.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null, "El jugador debe tener el personaje animado.");
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            PlayerAnimationDriver driver = player.GetComponent<PlayerAnimationDriver>();
            // Sin suavizado: la prueba mide la direccion, no la duracion de la mezcla.
            typeof(PlayerAnimationDriver)
                .GetField("dampTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(driver, 0f);

            // Camina hacia atras (-Z) mirando hacia delante (+Z): piernas hacia atras.
            for (int i = 0; i < 30; i++)
            {
                motor.Simulate(new PlayerMotionCommand(Vector3.back, Vector3.forward), Step);
                yield return null;
            }
            Assert.That(animator.GetFloat("MoveY"), Is.LessThan(-0.5f));
            Assert.That(Mathf.Abs(animator.GetFloat("MoveX")), Is.LessThan(0.2f));
        }

        private void Walk(Vector3 direction, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
                motor.Simulate(new PlayerMotionCommand(direction, Vector3.forward), Step);
        }

        private void Teleport(Vector3 position)
        {
            // El CharacterController ignora cambios directos de transform mientras esta activo.
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
            Walk(Vector3.zero, 0.25f);
        }
    }
}
