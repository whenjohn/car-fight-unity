using CarFight.Driving;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CarFight.Tests.Driving
{
    public sealed class VehiclePhysicsAcceptanceTests
    {
        private const float Delta = 1f / VehiclePhysicsProfile.PhysicsRate;

        private PhysicsScene physicsScene;
        private PhysicsMaterial material;
        private SimulationMode previousSimulationMode;
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            previousSimulationMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            physicsScene = Physics.defaultPhysicsScene;
            material = new PhysicsMaterial("VehiclePhysicsTest");
            VehiclePhysicsProfile.Configure(material);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
                Object.DestroyImmediate(createdObjects[index]);
            createdObjects.Clear();
            Object.DestroyImmediate(material);
            Physics.simulationMode = previousSimulationMode;
        }

        [Test]
        public void VehicleSettlesOnGroundWithoutSinking()
        {
            CreateGround();
            Rigidbody vehicle = CreateVehicle(new Vector3(0f, VehiclePhysicsProfile.CollisionRadius + 0.2f, 0f));

            Simulate(240);

            Assert.That(vehicle.position.y,
                Is.EqualTo(VehiclePhysicsProfile.CollisionRadius).Within(0.06f));
            Assert.That(Mathf.Abs(vehicle.linearVelocity.y), Is.LessThan(0.08f));
        }

        [Test]
        public void EqualMassVehiclesExchangeMomentumAndSeparate()
        {
            CreateGround();
            Rigidbody north = CreateVehicle(new Vector3(0f, VehiclePhysicsProfile.CollisionRadius, 2.5f));
            Rigidbody south = CreateVehicle(new Vector3(0f, VehiclePhysicsProfile.CollisionRadius, -2.5f));
            north.linearVelocity = Vector3.back * 10f;
            south.linearVelocity = Vector3.forward * 10f;
            bool exchanged = false;

            Physics.SyncTransforms();
            for (int step = 0; step < 120; step++)
            {
                physicsScene.Simulate(Delta);
                exchanged |= north.linearVelocity.z > 0.5f && south.linearVelocity.z < -0.5f;
            }

            Assert.That(exchanged, Is.True);
            Assert.That(Vector3.Distance(north.position, south.position),
                Is.GreaterThan(VehiclePhysicsProfile.CollisionRadius * 2f - 0.03f));
        }

        [Test]
        public void BurstSpeedVehicleCannotTunnelThroughArenaWall()
        {
            CreateGround();
            CreateWall(new Vector3(0f, 2f, -5f), new Vector3(20f, 4f, 1f));
            Rigidbody vehicle = CreateVehicle(new Vector3(0f, VehiclePhysicsProfile.CollisionRadius, 0f));
            vehicle.linearVelocity = Vector3.back * FollowController.BurstSpeed;
            float minimumZ = vehicle.position.z;

            Physics.SyncTransforms();
            for (int step = 0; step < 90; step++)
            {
                physicsScene.Simulate(Delta);
                minimumZ = Mathf.Min(minimumZ, vehicle.position.z);
            }

            Assert.That(minimumZ, Is.GreaterThan(-4.4f));
            Assert.That(vehicle.position.z, Is.GreaterThan(-4.4f));
        }

        private void Simulate(int steps)
        {
            Physics.SyncTransforms();
            for (int step = 0; step < steps; step++)
                physicsScene.Simulate(Delta);
        }

        private void CreateGround()
        {
            CreateWall(new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f));
        }

        private void CreateWall(Vector3 position, Vector3 size)
        {
            GameObject wall = new GameObject("StaticSurface");
            createdObjects.Add(wall);
            wall.transform.position = position;
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
            collider.material = material;
        }

        private Rigidbody CreateVehicle(Vector3 position)
        {
            GameObject vehicle = new GameObject("VehicleBody");
            createdObjects.Add(vehicle);
            vehicle.transform.position = position;
            SphereCollider collider = vehicle.AddComponent<SphereCollider>();
            collider.radius = VehiclePhysicsProfile.CollisionRadius;
            collider.material = material;
            Rigidbody body = vehicle.AddComponent<Rigidbody>();
            VehiclePhysicsProfile.Configure(body);
            return body;
        }
    }
}
