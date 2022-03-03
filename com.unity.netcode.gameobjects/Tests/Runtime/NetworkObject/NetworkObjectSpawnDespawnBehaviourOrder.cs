using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    public class NetworkObjectSpawnDespawnBehaviourOrder : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        internal const int ExpectedValue = 11111;
        private GameObject m_TestPrefab;
        private NetworkObject m_ServerSideObjectInstance;
        private DepdendentObject m_ClientSideDependentBehaviour;

        public NetworkObjectSpawnDespawnBehaviourOrder(HostOrServer hostOrServer) : base(hostOrServer) { }

        internal class ObjectDependency : NetworkBehaviour
        {
            public NetworkVariable<int> DependentValue = new NetworkVariable<int>(NetworkVariableReadPermission.Everyone);

            public override void OnNetworkSpawn()
            {
                if (IsServer)
                {
                    DependentValue.Value = ExpectedValue;
                }
            }
        }

        internal class DepdendentObject : NetworkBehaviour
        {
            private ObjectDependency m_ObjectDependency;
            public bool ValueVerified { get; internal set; }

            public override void OnNetworkSpawn()
            {
                if (!IsServer)
                {
                    m_ObjectDependency = FindObjectOfType<ObjectDependency>();
                    ValueVerified = m_ObjectDependency.DependentValue.Value == ExpectedValue;
                }
            }
        }

        protected override void OnServerAndClientsCreated()
        {
            m_TestPrefab = CreateNetworkObjectPrefab("OrderOpTest");
            m_TestPrefab.AddComponent<DepdendentObject>();
            m_TestPrefab.AddComponent<ObjectDependency>();

            base.OnServerAndClientsCreated();
        }

        protected bool ClientObjectDetected()
        {
            var clientId = m_ClientNetworkManagers[0].LocalClientId;
            if (s_GlobalNetworkObjects.ContainsKey(clientId))
            {
                if (s_GlobalNetworkObjects[clientId].ContainsKey(m_ServerSideObjectInstance.NetworkObjectId))
                {
                    m_ClientSideDependentBehaviour = s_GlobalNetworkObjects[clientId][m_ServerSideObjectInstance.NetworkObjectId].gameObject.GetComponent<DepdendentObject>();
                    return true;
                }
            }
            return false;
        }

        [UnityTest]
        public IEnumerator CheckObjectDependency()
        {
            m_ServerSideObjectInstance = SpawnObject(m_TestPrefab, m_ServerNetworkManager).GetComponent<NetworkObject>();

            yield return WaitForConditionOrTimeOut(ClientObjectDetected);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for client-side {nameof(NetworkObject)} to spawn!");
            Assert.True(m_ClientSideDependentBehaviour.ValueVerified, "Value was not verified on client-side!");
        }
    }
}
