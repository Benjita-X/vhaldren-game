using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Vhaldren.Gameplay.CameraSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Player;

namespace Vhaldren.Tests
{
    public sealed class PlayerMotorPlayModeTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            foreach (GameObject item in createdObjects)
            {
                if (item != null)
                    Object.Destroy(item);
            }
            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CharacterControllerStopsAtWallAndSlidesAlongIt()
        {
            Time.captureDeltaTime = 1f / 60f;
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            createdObjects.Add(ground);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, 2f);
            wall.transform.localScale = new Vector3(6f, 2f, 0.5f);
            createdObjects.Add(wall);

            GameObject player = CreatePlayer(new Vector3(0f, 1f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            SetField(motor, "instantHorizontalMovement", true);

            for (int i = 0; i < 45; i++)
            {
                motor.Simulate(new PlayerMotionCommand(Vector3.forward, Vector3.forward), 1f / 60f);
                yield return null;
            }

            Assert.That(player.transform.position.z, Is.LessThan(1.55f),
                "El CharacterController no debe atravesar la pared.");
            Assert.That(Mathf.Abs(motor.HorizontalVelocity.z), Is.LessThan(0.1f),
                "La velocidad bloqueada contra la pared debe borrarse.");

            for (int i = 0; i < 15; i++)
            {
                motor.Simulate(new PlayerMotionCommand(Vector3.right, Vector3.forward), 1f / 60f);
                yield return null;
            }

            Assert.That(player.transform.position.x, Is.GreaterThan(0.5f),
                "El personaje debe poder deslizarse por el lateral de la pared.");
            Assert.That(player.transform.position.z, Is.LessThan(1.55f));
        }

        [UnityTest]
        public IEnumerator InvalidGravityAndTurnSpeedDoNotCorruptTransform()
        {
            GameObject player = CreatePlayer(new Vector3(0f, 3f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            SetField(motor, "gravity", float.NaN);
            SetField(motor, "turningSpeedDegrees", float.PositiveInfinity);
            GameObject visual = new GameObject("VisualRoot");
            visual.transform.SetParent(player.transform, false);
            SetField(motor, "visualRoot", visual.transform);

            motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.right), 1f / 60f);
            yield return null;

            float verticalSpeed = (float)typeof(PlayerMotor)
                .GetField("verticalSpeed", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(motor);
            Assert.That(float.IsNaN(verticalSpeed) || float.IsInfinity(verticalSpeed), Is.False);
            AssertFinite(player.transform.position);
            AssertFinite(visual.transform.position);
            Assert.That(Mathf.Abs(Quaternion.Dot(visual.transform.rotation, Quaternion.identity)),
                Is.GreaterThan(0.01f));
        }

        [UnityTest]
        public IEnumerator MissingCameraDoesNotDisableGravitySimulation()
        {
            Time.captureDeltaTime = 1f / 60f;
            GameObject player = CreatePlayer(new Vector3(0f, 5f, 0f));
            player.AddComponent<LocalPlayerInput>();
            float startY = player.transform.position.y;

            for (int i = 0; i < 20; i++)
                yield return null;

            Assert.That(player.transform.position.y, Is.LessThan(startY),
                "Sin cámara, la entrada local debe continuar suministrando pasos de gravedad.");
        }

        [UnityTest]
        public IEnumerator AimRotatesVisualWithoutRotatingPhysicsRoot()
        {
            GameObject player = CreatePlayer(new Vector3(0f, 1f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            GameObject visual = new GameObject("VisualRoot");
            visual.transform.SetParent(player.transform, false);
            SetField(motor, "visualRoot", visual.transform);
            SetField(motor, "turningSpeedDegrees", 1080f);
            Quaternion rootBefore = player.transform.rotation;

            motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.right), 0.1f);
            yield return null;

            Assert.That(Quaternion.Angle(player.transform.rotation, rootBefore), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(visual.transform.rotation, rootBefore), Is.EqualTo(90f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator DiagonalInputDoesNotExceedConfiguredSpeed()
        {
            GameObject player = CreatePlayer(new Vector3(0f, 2f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            SetField(motor, "instantHorizontalMovement", true);

            motor.Simulate(new PlayerMotionCommand(new Vector3(1f, 0f, 1f), Vector3.forward), 1f / 60f);
            yield return null;

            Assert.That(motor.HorizontalVelocity.magnitude, Is.LessThanOrEqualTo(6.01f));
        }

        [UnityTest]
        public IEnumerator CameraRecoversFromNonFiniteOffsetsAndSharpness()
        {
            GameObject target = new GameObject("CameraTarget");
            target.transform.position = new Vector3(2f, 1f, -3f);
            createdObjects.Add(target);

            GameObject cameraObject = new GameObject("TestCamera");
            cameraObject.AddComponent<Camera>();
            SimpleFollowCamera follow = cameraObject.AddComponent<SimpleFollowCamera>();
            SetCameraField(follow, "offset", new Vector3(float.NaN, float.PositiveInfinity, -12f));
            SetCameraField(follow, "lookOffset", new Vector3(0f, float.NaN, 0f));
            SetCameraField(follow, "followSharpness", float.NaN);
            follow.SetTarget(target.transform);

            yield return null;

            AssertFinite(cameraObject.transform.position);
            AssertFinite(cameraObject.transform.rotation);
            Assert.That(float.IsNaN(Quaternion.Dot(cameraObject.transform.rotation, cameraObject.transform.rotation)), Is.False);
            Assert.That(cameraObject.transform.position, Is.EqualTo(target.transform.position + new Vector3(0f, 10f, -10f)));
        }

        private GameObject CreatePlayer(Vector3 position)
        {
            GameObject player = new GameObject("TestPlayer");
            player.transform.position = position;
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            player.AddComponent<PlayerMotor>();
            createdObjects.Add(player);
            return player;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            typeof(PlayerMotor)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void SetCameraField(object target, string fieldName, object value)
        {
            typeof(SimpleFollowCamera)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void AssertFinite(Vector3 value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
        }

        private static void AssertFinite(Quaternion value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
            Assert.That(float.IsNaN(value.w) || float.IsInfinity(value.w), Is.False);
        }
    }
}
