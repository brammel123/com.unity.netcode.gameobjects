using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using NUnit.Framework;
using TestProject.RuntimeTests.Support;
using UnityEngine;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    public class MessageOrderingTests
    {
        private GameObject m_Prefab;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Make sure these static values are reset
            Support.SpawnRpcDespawn.ClientUpdateCount = 0;
            Support.SpawnRpcDespawn.ServerUpdateCount = 0;
            Support.SpawnRpcDespawn.ClientNetworkSpawnRpcCalled = false;
            Support.SpawnRpcDespawn.ExecuteClientRpc = false;
            yield break;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            // Shutdown and clean up both of our NetworkManager instances
            if (m_Prefab)
            {
                NetcodeIntegrationTestHelpers.Destroy();
                Object.Destroy(m_Prefab);
                m_Prefab = null;
                Support.SpawnRpcDespawn.ClientUpdateCount = 0;
                Support.SpawnRpcDespawn.ServerUpdateCount = 0;
                Support.SpawnRpcDespawn.ClientNetworkSpawnRpcCalled = false;
                Support.SpawnRpcDespawn.ExecuteClientRpc = false;
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator SpawnChangeOwnership()
        {
            const int numClients = 1;
            Assert.True(NetcodeIntegrationTestHelpers.Create(numClients, out NetworkManager server, out NetworkManager[] clients));
            m_Prefab = new GameObject("Object");
            var networkObject = m_Prefab.AddComponent<NetworkObject>();

            // Make it a prefab
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);

            var validNetworkPrefab = new NetworkPrefab();
            validNetworkPrefab.Prefab = m_Prefab;
            server.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            foreach (var client in clients)
            {
                client.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            }

            // Start the instances
            if (!NetcodeIntegrationTestHelpers.Start(true, server, clients))
            {
                Debug.LogError("Failed to start instances");
                Assert.Fail("Failed to start instances");
            }

            // [Client-Side] Wait for a connection to the server
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnected(clients, null, 512);

            // [Host-Side] Check to make sure all clients are connected
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnectedToServer(server, clients.Length + 1, null, 512);

            var serverObject = Object.Instantiate(m_Prefab, Vector3.zero, Quaternion.identity);
            NetworkObject serverNetworkObject = serverObject.GetComponent<NetworkObject>();
            serverNetworkObject.NetworkManagerOwner = server;
            serverNetworkObject.Spawn();
            serverNetworkObject.ChangeOwnership(clients[0].LocalClientId);

            // Wait until all objects have spawned.
            const int expectedNetworkObjects = numClients + 2; // +2 = one for prefab, one for server.
            const int maxFrames = 240;
            var doubleCheckTime = Time.realtimeSinceStartup + 5.0f;
            while (Object.FindObjectsOfType<NetworkObject>().Length != expectedNetworkObjects)
            {
                if (Time.frameCount > maxFrames)
                {
                    // This is here in the event a platform is running at a higher
                    // frame rate than expected
                    if (doubleCheckTime < Time.realtimeSinceStartup)
                    {
                        Assert.Fail("Did not successfully spawn all expected NetworkObjects");
                        break;
                    }
                }
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }
        }

        [UnityTest]
        public IEnumerator SpawnRpcDespawn()
        {
            // Must be 1 for this test.
            const int numClients = 1;
            Assert.True(NetcodeIntegrationTestHelpers.Create(numClients, out NetworkManager server, out NetworkManager[] clients));
            m_Prefab = new GameObject("Object");
            m_Prefab.AddComponent<SpawnRpcDespawn>();
            Support.SpawnRpcDespawn.TestStage = NetworkUpdateStage.EarlyUpdate;
            var networkObject = m_Prefab.AddComponent<NetworkObject>();

            // Make it a prefab
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);
            var handlers = new List<SpawnRpcDespawnInstanceHandler>();
            var handler = new SpawnRpcDespawnInstanceHandler(networkObject.GlobalObjectIdHash);

            foreach (var client in clients)
            {
                // TODO: Create a unique handler per client
                client.PrefabHandler.AddHandler(networkObject, handler);
            }

            var validNetworkPrefab = new NetworkPrefab();
            validNetworkPrefab.Prefab = m_Prefab;
            server.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            foreach (var client in clients)
            {
                client.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            }

            // Start the instances
            if (!NetcodeIntegrationTestHelpers.Start(true, server, clients))
            {
                Debug.LogError("Failed to start instances");
                Assert.Fail("Failed to start instances");
            }

            // [Client-Side] Wait for a connection to the server
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnected(clients, null, 512);

            // [Host-Side] Check to make sure all clients are connected
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnectedToServer(server, clients.Length + 1, null, 512);

            var serverObject = Object.Instantiate(m_Prefab, Vector3.zero, Quaternion.identity);
            NetworkObject serverNetworkObject = serverObject.GetComponent<NetworkObject>();
            serverNetworkObject.NetworkManagerOwner = server;
            serverNetworkObject.Spawn();

            SpawnRpcDespawn srdComponent = serverObject.GetComponent<SpawnRpcDespawn>();
            srdComponent.Activate();

            // Wait until all objects have spawned.
            int expectedCount = Support.SpawnRpcDespawn.ClientUpdateCount + 1;
            int maxFrames = 240 + Time.frameCount;
            var doubleCheckTime = Time.realtimeSinceStartup + 5.0f;
            while (Support.SpawnRpcDespawn.ClientUpdateCount < expectedCount && !handler.WasSpawned)
            {
                if (Time.frameCount > maxFrames)
                {
                    // This is here in the event a platform is running at a higher
                    // frame rate than expected
                    if (doubleCheckTime < Time.realtimeSinceStartup)
                    {
                        Assert.Fail("Did not successfully call all expected client RPCs");
                        break;
                    }
                }
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            Assert.AreEqual(NetworkUpdateStage.EarlyUpdate, Support.SpawnRpcDespawn.StageExecutedByReceiver);
            Assert.AreEqual(Support.SpawnRpcDespawn.ServerUpdateCount, Support.SpawnRpcDespawn.ClientUpdateCount);

            // Wait 1 tic for the GameObjet and associated components to be destroyed
            yield return new WaitForSeconds(1.0f / server.NetworkConfig.TickRate);

            Assert.True(handler.WasDestroyed);
        }

        [UnityTest]
        public IEnumerator RpcOnNetworkSpawn()
        {
            Support.SpawnRpcDespawn.ExecuteClientRpc = true;
            // Must be 1 for this test.
            const int numClients = 1;
            Assert.True(NetcodeIntegrationTestHelpers.Create(numClients, out NetworkManager server, out NetworkManager[] clients));
            m_Prefab = new GameObject("Object");
            m_Prefab.AddComponent<SpawnRpcDespawn>();
            Support.SpawnRpcDespawn.TestStage = NetworkUpdateStage.EarlyUpdate;
            var networkObject = m_Prefab.AddComponent<NetworkObject>();

            // Make it a prefab
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);
            var handlers = new List<SpawnRpcDespawnInstanceHandler>();
            var handler = new SpawnRpcDespawnInstanceHandler(networkObject.GlobalObjectIdHash);

            // We *must* always add a unique handler to both the server and the clients
            server.PrefabHandler.AddHandler(networkObject, handler);
            handlers.Add(handler);
            foreach (var client in clients)
            {
                // Create a unique SpawnRpcDespawnInstanceHandler per client
                handler = new SpawnRpcDespawnInstanceHandler(networkObject.GlobalObjectIdHash);
                handlers.Add(handler);
                client.PrefabHandler.AddHandler(networkObject, handler);
            }

            var validNetworkPrefab = new NetworkPrefab();
            validNetworkPrefab.Prefab = m_Prefab;
            server.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            foreach (var client in clients)
            {
                client.NetworkConfig.NetworkPrefabs.Add(validNetworkPrefab);
            }

            var waitForTickInterval = new WaitForSeconds(1.0f / server.NetworkConfig.TickRate);

            // Start the instances
            if (!NetcodeIntegrationTestHelpers.Start(false, server, clients))
            {
                Debug.LogError("Failed to start instances");
                Assert.Fail("Failed to start instances");
            }

            // [Client-Side] Wait for a connection to the server
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnected(clients, null, 512);

            // [Host-Side] Check to make sure all clients are connected
            yield return NetcodeIntegrationTestHelpers.WaitForClientsConnectedToServer(server, clients.Length, null, 512);

            var serverObject = Object.Instantiate(m_Prefab, Vector3.zero, Quaternion.identity);
            NetworkObject serverNetworkObject = serverObject.GetComponent<NetworkObject>();
            serverNetworkObject.NetworkManagerOwner = server;
            serverNetworkObject.Spawn();

            // Wait until all objects have spawned.
            const int maxFrames = 240;
            var doubleCheckTime = Time.realtimeSinceStartup + 5.0f;
            while (!Support.SpawnRpcDespawn.ClientNetworkSpawnRpcCalled)
            {
                if (Time.frameCount > maxFrames)
                {
                    // This is here in the event a platform is running at a higher
                    // frame rate than expected
                    if (doubleCheckTime < Time.realtimeSinceStartup)
                    {
                        Assert.Fail("Did not successfully call all expected client RPCs");
                        break;
                    }
                }
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            Assert.True(handler.WasSpawned);
            Assert.True(Support.SpawnRpcDespawn.ClientNetworkSpawnRpcCalled);

            // Despawning the server-side NetworkObject will invoke the handler's OnDestroy method
            serverNetworkObject.Despawn();
            yield return waitForTickInterval;

            var hasTimedOut = false;
            var timeOutPeriod = Time.realtimeSinceStartup + 2.0f;
            var allHandlersDestroyed = false;
            while (!allHandlersDestroyed && !hasTimedOut)
            {
                allHandlersDestroyed = true;
                foreach (var handlerInstance in handlers)
                {
                    if (!handlerInstance.WasDestroyed)
                    {
                        allHandlersDestroyed = false;
                        break;
                    }
                }
                hasTimedOut = timeOutPeriod < Time.realtimeSinceStartup;
                yield return waitForTickInterval;
            }

            Assert.False(hasTimedOut, "Timed out waiting for handlers to be destroyed");
        }
    }
}
