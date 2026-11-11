using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Vhaldren.Gameplay.CameraSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Combat;

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
            CreateGround();

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
        public IEnumerator OneFrameMovementTapsRespondImmediatelyAndReverseDirection()
        {
            CreateGround();
            GameObject player = CreatePlayer(new Vector3(0f, 1.01f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            SetField(motor, "instantHorizontalMovement", true);
            yield return null;

            Vector3 start = player.transform.position;
            motor.Simulate(new PlayerMotionCommand(Vector3.forward, Vector3.forward), 1f / 60f);
            Assert.That(player.transform.position.z - start.z, Is.GreaterThan(0.08f),
                "Un toque de una sola actualizacion no debe sentirse como si el personaje estuviera quieto.");

            Vector3 afterForward = player.transform.position;
            motor.Simulate(new PlayerMotionCommand(Vector3.back, Vector3.forward), 1f / 60f);
            Assert.That(player.transform.position.z, Is.LessThan(afterForward.z),
                "Debe aceptar la direccion opuesta inmediatamente al cambiar de tecla.");
        }

        [UnityTest]
        public IEnumerator MouseRaySetsAimPointAndShotImpactAtCursorTarget()
        {
            CreateGround(40f);

            GameObject cameraObject = new GameObject("AimCamera");
            cameraObject.transform.position = new Vector3(0f, 10f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            createdObjects.Add(cameraObject);

            GameObject player = CreatePlayer(new Vector3(3f, 1.01f, 0f));
            LocalPlayerShooter shooter = player.AddComponent<LocalPlayerShooter>();
            SetShooterField(shooter, "viewCamera", camera);
            yield return null;

            Vector2 cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Assert.That(shooter.TryGetAimPoint(cursor, out Vector3 target), Is.True);
            Assert.That(target.x, Is.EqualTo(0f).Within(0.1f));
            Assert.That(target.z, Is.EqualTo(0f).Within(0.2f));
            Assert.That(shooter.FireAtScreenPosition(cursor), Is.True);
            Assert.That(shooter.ShotsFired, Is.EqualTo(1));
            Assert.That(Vector3.Distance(shooter.LastAimPoint, target), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(shooter.LastImpactPoint, target), Is.LessThan(0.15f));
        }

        [UnityTest]
        public IEnumerator AimUsesNearestHitEvenWhenMoreThan32CollidersOverlapTheRay()
        {
            GameObject cameraObject = new GameObject("DenseAimCamera");
            cameraObject.transform.position = new Vector3(0f, 10f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            createdObjects.Add(cameraObject);

            GameObject player = CreatePlayer(new Vector3(5f, 1f, 0f));
            LocalPlayerShooter shooter = player.AddComponent<LocalPlayerShooter>();
            SetShooterField(shooter, "viewCamera", camera);

            // Crear primero los objetos lejanos para no depender del orden del buffer fisico.
            for (int i = 0; i < 48; i++)
            {
                GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.transform.position = cameraObject.transform.position
                    + cameraObject.transform.forward * (12f - i * 0.2f);
                obstacle.transform.localScale = Vector3.one * 0.12f;
                createdObjects.Add(obstacle);
            }
            Physics.SyncTransforms();
            yield return null;

            Vector2 cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = camera.ScreenPointToRay(cursor);
            RaycastHit[] allHits = Physics.RaycastAll(ray, 100f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Assert.That(allHits.Length, Is.GreaterThan(32), "La prueba debe saturar el buffer inicial.");

            float nearestDistance = float.PositiveInfinity;
            Vector3 expected = Vector3.zero;
            foreach (RaycastHit hit in allHits)
            {
                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    expected = hit.point;
                }
            }

            Assert.That(shooter.TryGetAimPoint(cursor, out Vector3 aimPoint), Is.True);
            Assert.That(Vector3.Distance(aimPoint, expected), Is.LessThan(0.001f),
                "El resultado debe ser el mas cercano, incluso con mas de 32 colisiones.");
        }

        [UnityTest]
        public IEnumerator TestShotDamagesOnlyTheHitTrainingTarget()
        {
            CreateGround(40f);
            GameObject cameraObject = new GameObject("DamageAimCamera");
            cameraObject.transform.position = new Vector3(0f, 10f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            createdObjects.Add(cameraObject);

            GameObject player = CreatePlayer(new Vector3(3f, 1.08f, 0f));
            LocalPlayerShooter shooter = player.AddComponent<LocalPlayerShooter>();
            SetShooterField(shooter, "viewCamera", camera);

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "DamageableTarget";
            target.transform.position = new Vector3(0f, 1f, 0f);
            target.transform.localScale = new Vector3(2f, 2f, 2f);
            CombatHealth health = target.AddComponent<CombatHealth>();
            health.ResetToFull();
            createdObjects.Add(target);

            GameObject other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            other.name = "OtherDamageableTarget";
            other.transform.position = new Vector3(6f, 1f, 0f);
            CombatHealth otherHealth = other.AddComponent<CombatHealth>();
            otherHealth.ResetToFull();
            createdObjects.Add(other);

            Physics.SyncTransforms();
            yield return null;

            Vector2 cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Assert.That(shooter.TryGetAimPoint(cursor, out Vector3 point), Is.True);
            Assert.That(point.x, Is.EqualTo(0f).Within(0.1f));
            Assert.That(shooter.FireAtScreenPosition(cursor), Is.True);

            Assert.That(health.CurrentHealth, Is.EqualTo(75f).Within(0.01f),
                "El primer objetivo bajo el cursor debe recibir los 25 PV de dano de prueba.");
            Assert.That(otherHealth.CurrentHealth, Is.EqualTo(100f),
                "No se deben aplicar danos a objetivos que no reciben el disparo.");
            Assert.That(shooter.HitsLanded, Is.EqualTo(1));
            Assert.That(shooter.ShotsFired, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SkillLocksMovementAndDashWithoutStoppingGravity()
        {
            CreateGround(40f);
            GameObject player = new GameObject("PlayerWithSkillModifiers");
            player.transform.position = new Vector3(0f, 1.08f, 0f);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            PlayerActionModifiers modifiers = player.AddComponent<PlayerActionModifiers>();
            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            createdObjects.Add(player);
            SetField(motor, "instantHorizontalMovement", false);
            Physics.SyncTransforms();
            yield return null;

            for (int i = 0; i < 20; i++)
                motor.Simulate(new PlayerMotionCommand(Vector3.forward, Vector3.forward), 1f / 60f);

            float beforeLock = player.transform.position.z;
            int stun = modifiers.AddModifier(
                PlayerActionModifiers.LockedAction.Movement |
                PlayerActionModifiers.LockedAction.Dash);
            motor.Simulate(new PlayerMotionCommand(Vector3.forward, Vector3.forward, true), 1f / 60f);

            Assert.That(player.transform.position.z, Is.EqualTo(beforeLock).Within(0.002f),
                "Bloquear movimiento debe detener inmediatamente tambien la inercia.");
            Assert.That(motor.HorizontalVelocity.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(motor.DashMovedThisStep, Is.False,
                "Un aturdimiento debe poder prohibir una nueva esquiva.");
            Assert.That(modifiers.RemoveModifier(stun), Is.True);

            motor.Simulate(new PlayerMotionCommand(Vector3.forward, Vector3.forward), 1f / 60f);
            Assert.That(motor.HorizontalVelocity.z, Is.GreaterThan(0f));
            Assert.That(player.transform.position.z, Is.GreaterThan(beforeLock));
        }

        [UnityTest]
        public IEnumerator SkillCanDisableShotWithoutChangingAimOrHealth()
        {
            CreateGround(40f);
            GameObject cameraObject = new GameObject("AbilityBlockCamera");
            cameraObject.transform.position = new Vector3(0f, 10f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            createdObjects.Add(cameraObject);

            GameObject player = new GameObject("AbilityBlockPlayer");
            player.transform.position = new Vector3(3f, 1.08f, 0f);
            PlayerActionModifiers modifiers = player.AddComponent<PlayerActionModifiers>();
            LocalPlayerShooter shooter = player.AddComponent<LocalPlayerShooter>();
            SetShooterField(shooter, "viewCamera", camera);
            createdObjects.Add(player);

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0f, 1f, 0f);
            target.transform.localScale = new Vector3(2f, 2f, 2f);
            CombatHealth health = target.AddComponent<CombatHealth>();
            createdObjects.Add(target);
            Physics.SyncTransforms();
            yield return null;

            int silence = modifiers.AddModifier(PlayerActionModifiers.LockedAction.Attack);
            Vector2 cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Assert.That(shooter.TryGetAimPoint(cursor, out _), Is.True);
            Assert.That(shooter.FireAtScreenPosition(cursor), Is.False);
            Assert.That(shooter.ShotsFired, Is.Zero);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            modifiers.RemoveModifier(silence);

            Assert.That(shooter.FireAtScreenPosition(cursor), Is.True);
            Assert.That(shooter.ShotsFired, Is.EqualTo(1));
            Assert.That(health.CurrentHealth, Is.EqualTo(75f));
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

        [UnityTest]
        public IEnumerator AcceleratedMovementStartsAtHighFrameRates()
        {
            // A 1000 FPS cada paso con aceleracion mueve menos de 1 mm. El personaje
            // debe arrancar igual: el control no puede depender de los FPS del equipo.
            CreateGround();
            GameObject player = CreatePlayer(new Vector3(0f, 1.08f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            SetField(motor, "instantHorizontalMovement", false);
            Physics.SyncTransforms();
            yield return null;

            const float step = 0.001f;
            for (int i = 0; i < 50; i++)
                motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward), step);

            Vector3 start = player.transform.position;
            for (int i = 0; i < 300; i++)
                motor.Simulate(new PlayerMotionCommand(Vector3.forward, Vector3.forward), step);

            Assert.That(player.transform.position.z - start.z, Is.GreaterThan(1f),
                "Con aceleracion y FPS altos el personaje se quedaba quieto para siempre.");
            Assert.That(motor.HorizontalVelocity.z, Is.EqualTo(6f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator AcceleratedMovementKeepsSpeedAndStopsCleanly()
        {
            // Con aceleracion, la velocidad no debe degradarse por medir la posicion
            // en cada paso, y al soltar debe frenar hasta cero sin quedarse deslizando.
            CreateGround(60f);
            GameObject player = CreatePlayer(new Vector3(0f, 1.08f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            SetField(motor, "instantHorizontalMovement", false);
            yield return null;

            const float step = 1f / 240f;
            for (int i = 0; i < 240; i++)
                motor.Simulate(new PlayerMotionCommand(Vector3.right, Vector3.forward), step);

            Assert.That(motor.HorizontalVelocity.x, Is.EqualTo(6f).Within(0.01f));
            Assert.That(motor.ActualVelocity.x, Is.EqualTo(6f).Within(0.2f));

            for (int i = 0; i < 60; i++)
                motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward), step);

            Assert.That(motor.HorizontalVelocity.magnitude, Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator ShotStopsAtWallEvenWhenRayCrossesOwnBody()
        {
            // El origen del disparo esta delante del modelo; al apuntar hacia atras el rayo
            // cruza la capsula del propio jugador antes de llegar a la pared.
            CreateGround(40f);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 0.5f, 1.5f);
            wall.transform.localScale = new Vector3(4f, 1f, 0.3f);
            createdObjects.Add(wall);

            GameObject cameraObject = new GameObject("AimCamera");
            cameraObject.transform.position = new Vector3(0f, 10f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            createdObjects.Add(cameraObject);

            GameObject player = CreatePlayer(new Vector3(0f, 1.08f, 3f));
            GameObject muzzle = new GameObject("ShotOrigin");
            muzzle.transform.SetParent(player.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.18f, 0.43f);
            LocalPlayerShooter shooter = player.AddComponent<LocalPlayerShooter>();
            SetShooterField(shooter, "viewCamera", camera);
            SetShooterField(shooter, "shotOrigin", muzzle.transform);
            Physics.SyncTransforms();
            yield return null;

            Vector2 cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Assert.That(shooter.TryGetAimPoint(cursor, out Vector3 target), Is.True);
            Assert.That(target.z, Is.EqualTo(0f).Within(0.2f), "El cursor apunta al suelo, delante de la pared.");

            Assert.That(shooter.FireAtScreenPosition(cursor), Is.True);
            Assert.That(shooter.LastImpactPoint.z, Is.GreaterThan(1.5f),
                "El disparo no debe atravesar la pared que esta entre el jugador y el cursor.");
        }

        [UnityTest]
        public IEnumerator DashTrailIncludesTheFinalMovementStep()
        {
            CreateGround();
            GameObject player = CreatePlayer(new Vector3(0f, 1.08f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            TrailRenderer trail = player.AddComponent<TrailRenderer>();
            trail.emitting = false;
            DashFeedback feedback = player.AddComponent<DashFeedback>();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(DashFeedback).GetField("trail", flags).SetValue(feedback, trail);
            MethodInfo lateUpdate = typeof(DashFeedback).GetMethod("LateUpdate", flags);

            motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward, true), 0.18f);
            Assert.That(motor.IsDashing, Is.False, "La duracion restante termino en este paso.");
            Assert.That(motor.DashMovedThisStep, Is.True);
            lateUpdate.Invoke(feedback, null);
            Assert.That(trail.emitting, Is.True, "La estela debe cubrir el ultimo paso de esquiva.");

            motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward), 0.02f);
            lateUpdate.Invoke(feedback, null);
            Assert.That(trail.emitting, Is.False, "Al siguiente paso la estela debe apagarse.");
            yield break;
        }

        [UnityTest]
        public IEnumerator DashCoversItsDistanceAtAnyFrameRate()
        {
            CreateGround(60f);
            GameObject slow = CreatePlayer(new Vector3(-5f, 1.08f, 0f));
            GameObject fast = CreatePlayer(new Vector3(5f, 1.08f, 0f));
            yield return null;

            float slowDistance = DashForward(slow.GetComponent<PlayerMotor>(), 1f / 30f);
            float fastDistance = DashForward(fast.GetComponent<PlayerMotor>(), 1f / 240f);

            Assert.That(slowDistance, Is.EqualTo(4.5f).Within(0.08f), "A 30 FPS.");
            Assert.That(fastDistance, Is.EqualTo(4.5f).Within(0.08f), "A 240 FPS.");
        }

        [UnityTest]
        public IEnumerator DashFollowsMovementDirectionInsteadOfAim()
        {
            CreateGround(60f);
            GameObject player = CreatePlayer(new Vector3(0f, 1.08f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            yield return null;

            const float step = 1f / 120f;
            Settle(motor, step);
            Vector3 start = player.transform.position;
            // Camina a la derecha mientras apunta hacia delante: la esquiva va a la derecha.
            motor.Simulate(new PlayerMotionCommand(Vector3.right, Vector3.forward, true), step);
            Assert.That(motor.IsDashing, Is.True);
            for (int i = 0; i < 60; i++)
                motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward), step);

            Vector3 travelled = player.transform.position - start;
            Assert.That(travelled.x, Is.EqualTo(4.5f).Within(0.08f));
            Assert.That(Mathf.Abs(travelled.z), Is.LessThan(0.05f));
            Assert.That(motor.IsDashing, Is.False);
            Assert.That(motor.HorizontalVelocity.magnitude, Is.LessThan(0.001f),
                "Sin teclas pulsadas, al terminar la esquiva el personaje se detiene.");
        }

        [UnityTest]
        public IEnumerator DashStopsAtWalls()
        {
            CreateGround();
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, 2f);
            wall.transform.localScale = new Vector3(6f, 2f, 0.5f);
            createdObjects.Add(wall);
            GameObject player = CreatePlayer(new Vector3(0f, 1.08f, 0f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            yield return null;

            DashForward(motor, 1f / 60f);

            Assert.That(player.transform.position.z, Is.InRange(1f, 1.4f),
                "La esquiva no debe atravesar la pared ni rebotar lejos de ella.");
        }

        [UnityTest]
        public IEnumerator DashRespectsItsCooldown()
        {
            CreateGround(60f);
            GameObject player = CreatePlayer(new Vector3(0f, 1.08f, -10f));
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            yield return null;

            const float step = 1f / 120f;
            float first = DashForward(motor, step);          // 0,5 s simulados
            Assert.That(first, Is.GreaterThan(4f));
            Assert.That(motor.IsDashReady, Is.False);

            // Segunda pulsacion a mitad de la espera (1,2 s): no debe hacer nada.
            float second = DashForward(motor, step);
            Assert.That(second, Is.LessThan(0.01f));

            // Ya pasaron 1,0 s; 0,3 s mas y la espera termino.
            for (int i = 0; i < 36; i++)
                motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward), step);
            Assert.That(motor.IsDashReady, Is.True);

            float third = DashForward(motor, step);
            Assert.That(third, Is.EqualTo(4.5f).Within(0.08f));
        }

        [UnityTest]
        public IEnumerator CameraMovesInFrontOfObstaclesAndRestoresDistanceWhenClear()
        {
            GameObject target = new GameObject("CameraObstacleTarget");
            target.transform.position = new Vector3(0f, 1f, 0f);
            createdObjects.Add(target);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 5f, -4f);
            wall.transform.localScale = new Vector3(6f, 10f, 0.5f);
            createdObjects.Add(wall);

            GameObject cameraObject = new GameObject("CameraObstacleTest");
            cameraObject.AddComponent<Camera>();
            SimpleFollowCamera follow = cameraObject.AddComponent<SimpleFollowCamera>();
            createdObjects.Add(cameraObject);
            Physics.SyncTransforms();

            follow.SetTarget(target.transform);
            Quaternion originalAngle = cameraObject.transform.rotation;
            Assert.That(cameraObject.transform.position.z, Is.GreaterThan(-4f),
                "La camara debe situarse delante del obstaculo.");
            wall.SetActive(false);
            follow.SetTarget(target.transform);
            Assert.That(cameraObject.transform.position.z, Is.EqualTo(-10f).Within(0.01f),
                "La camara debe recuperar su distancia configurada al eliminar la pared.");
            Assert.That(Quaternion.Angle(originalAngle, cameraObject.transform.rotation), Is.LessThan(0.01f),
                "La oclusion no debe cambiar el angulo de la camara.");
            yield break;
        }

        [UnityTest]
        public IEnumerator CameraIgnoresItsOwnPlayerCollider()
        {
            GameObject player = CreatePlayer(new Vector3(0f, 1.08f, 0f));
            GameObject cameraObject = new GameObject("CameraSelfColliderTest");
            cameraObject.AddComponent<Camera>();
            SimpleFollowCamera follow = cameraObject.AddComponent<SimpleFollowCamera>();
            createdObjects.Add(cameraObject);
            Physics.SyncTransforms();

            follow.SetTarget(player.transform);
            Assert.That(cameraObject.transform.position.z, Is.EqualTo(-10f).Within(0.01f),
                "La capsula del jugador no debe tratarse como una pared.");
            yield break;
        }

        [UnityTest]
        public IEnumerator CameraZoomIsClampedAndKeepsTheViewAngle()
        {
            GameObject target = new GameObject("CameraTarget");
            target.transform.position = new Vector3(2f, 1f, -3f);
            createdObjects.Add(target);

            GameObject cameraObject = new GameObject("TestCamera");
            cameraObject.AddComponent<Camera>();
            SimpleFollowCamera follow = cameraObject.AddComponent<SimpleFollowCamera>();
            createdObjects.Add(cameraObject);
            follow.SetTarget(target.transform);
            Quaternion angleWithoutZoom = cameraObject.transform.rotation;

            follow.SetZoom(0.01f, true);
            follow.SetTarget(target.transform);
            yield return null;

            Assert.That(follow.Zoom, Is.EqualTo(0.6f).Within(0.0001f), "El zoom no baja del minimo.");
            // Desplazamiento (0,10,-10) y punto de mira (0,0.5,0): al 60 % queda en (0, 6.2, -6).
            Vector3 expected = target.transform.position + new Vector3(0f, 6.2f, -6f);
            Assert.That(Vector3.Distance(cameraObject.transform.position, expected), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(cameraObject.transform.rotation, angleWithoutZoom), Is.LessThan(0.01f),
                "El zoom cambia la distancia, no el angulo.");

            follow.AddZoom(10f);
            Assert.That(follow.TargetZoom, Is.EqualTo(1.5f).Within(0.0001f), "El zoom no pasa del maximo.");
            follow.AddZoom(float.NaN);
            Assert.That(follow.TargetZoom, Is.EqualTo(1.5f).Within(0.0001f));
        }

        /// <summary>Pide la esquiva hacia delante sin caminar y simula 0,5 s. Devuelve lo avanzado en Z.</summary>
        private static float DashForward(PlayerMotor motor, float step)
        {
            Settle(motor, step);
            float startZ = motor.transform.position.z;
            int steps = Mathf.RoundToInt(0.5f / step);
            for (int i = 0; i < steps; i++)
                motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward, i == 0), step);
            return motor.transform.position.z - startZ;
        }

        private static void Settle(PlayerMotor motor, float step)
        {
            // Sin esto el primer paso se gasta en asentar la capsula sobre el suelo.
            // El tiempo de asentado no cuenta para la espera: se usa un paso minimo.
            for (int i = 0; i < 5; i++)
                motor.Simulate(new PlayerMotionCommand(Vector3.zero, Vector3.forward), 0.0001f);
        }

        private GameObject CreateGround(float size = 20f)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(size, 1f, size);
            createdObjects.Add(ground);
            // Sin esto el collider sigue en su pose de creacion (cubo unitario en el origen)
            // hasta el siguiente paso de fisica, y la prueba depende de la duracion del frame.
            Physics.SyncTransforms();
            return ground;
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
            // La geometria de la prueba se crea antes que el jugador: se sincroniza aqui para
            // que sus colliders esten en su sitio aunque no haya corrido un paso de fisica.
            Physics.SyncTransforms();
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

        private static void SetShooterField(object target, string fieldName, object value)
        {
            typeof(LocalPlayerShooter)
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
