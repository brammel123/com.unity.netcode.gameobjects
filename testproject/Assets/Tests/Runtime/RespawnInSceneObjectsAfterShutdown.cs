using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Netcode.TestHelpers.Runtime;
using Unity.Netcode;

namespace TestProject.RuntimeTests
{
    public class RespawnInSceneObjectsAfterShutdown : NetcodeIntegrationTest
    {
        public const string SceneToLoad = "InScneNetworkObject";
        private const string k_InSceneObjetName = "InSceneObject";
        protected override int NumberOfClients => 1;
        protected Scene m_SceneLoaded;

        protected bool m_SceneIsLoaded;

        internal ISceneManagerHandler OriginalClientSceneHandler;

        protected Dictionary<ulong, Scene> m_ClientIdToSceneLoaded = new Dictionary<ulong, Scene>();

        protected override void OnOneTimeSetup()
        {
            m_SceneIsLoaded = false;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            //SceneManager.LoadSceneAsync(k_SceneToLoad, LoadSceneMode.Additive);

            base.OnOneTimeSetup();
        }

        private void AssignNetworkManagerOwner(NetworkManager networkManager, Scene scene)
        {
            var sceneRelativeNetworkObjects = Object.FindObjectsOfType<NetworkObject>().Where((c) => c.gameObject.scene == scene);
            foreach (var sceneNetworkObject in sceneRelativeNetworkObjects)
            {
                sceneNetworkObject.NetworkManagerOwner = networkManager;
            }
        }

        // This is a proof of concept and is not very well designed at this time.
        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (loadSceneMode == LoadSceneMode.Additive && scene.name == SceneToLoad)
            {
                m_SceneLoaded = scene;
                if (!m_ClientIdToSceneLoaded.ContainsKey(m_ServerNetworkManager.LocalClientId))
                {
                    m_ClientIdToSceneLoaded.Add(m_ServerNetworkManager.LocalClientId, scene);
                    AssignNetworkManagerOwner(m_ServerNetworkManager, scene);
                }
                else
                {
                    Assert.False(m_ClientIdToSceneLoaded.ContainsKey(m_ClientNetworkManagers[0].LocalClientId), $"Client ID already in {nameof(m_ClientIdToSceneLoaded)}!");
                    m_ClientIdToSceneLoaded.Add(m_ClientNetworkManagers[0].LocalClientId, scene);
                    AssignNetworkManagerOwner(m_ClientNetworkManagers[0], scene);
                }
            }
        }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            DeRegisterSceneManagerHandler();
            NetcodeIntegrationTestHelpers.ClientSceneHandler.Dispose();
            NetcodeIntegrationTestHelpers.ClientSceneHandler = new ConditionalClientLoadScene(m_ServerNetworkManager, m_ClientNetworkManagers[0]);
            RegisterSceneManagerHandler();
            m_ServerNetworkManager.SceneManager.OnLoadEventCompleted += SceneManager_OnLoadEventCompleted;
            Assert.True(m_ServerNetworkManager.SceneManager.LoadScene(SceneToLoad, LoadSceneMode.Additive) == SceneEventProgressStatus.Started);
            return base.OnServerAndClientsConnected();
        }

        private void SceneManager_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (loadSceneMode == LoadSceneMode.Additive && sceneName == SceneToLoad)
            {
                m_SceneIsLoaded = true;
            }
        }

        [UnityTest]
        public IEnumerator RespawnInSceneObjectAfterShutdown()
        {
            yield return WaitForConditionOrTimeOut(() => m_SceneIsLoaded);

            // var networkObjects = s_GlobalNetworkObjects[0].Values.Where((c) => c.name.Contains(k_InSceneObjetName)).ToList();

            Debug.Break();

            m_ServerNetworkManager.Shutdown();

        }


        protected override void OnOneTimeTearDown()
        {
            if (m_SceneLoaded.IsValid() && m_SceneLoaded.isLoaded)
            {
                SceneManager.UnloadSceneAsync(m_SceneLoaded);
                m_SceneIsLoaded = false;
            }
            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
            base.OnOneTimeTearDown();
        }

    }


    internal class ConditionalClientLoadScene : IntegrationTestSceneHandler
    {
        private ISceneManagerHandler.SceneEventAction m_LoadSceneEventAction;
        public NetworkManager ServerNetworkManager;

        protected override AsyncOperation OnLoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode, ISceneManagerHandler.SceneEventAction sceneEventAction)
        {
            if (RespawnInSceneObjectsAfterShutdown.SceneToLoad == sceneName && loadSceneMode == LoadSceneMode.Additive)
            {
                m_LoadSceneEventAction = sceneEventAction;
                var asynOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
                asynOperation.completed += AsynOperation_completed;
                return asynOperation;
            }
            else
            {
                return base.OnLoadSceneAsync(sceneName, loadSceneMode, sceneEventAction);
            }

        }

        private void AsynOperation_completed(AsyncOperation obj)
        {
            if (obj.isDone)
            {
                m_LoadSceneEventAction.Invoke();
            }
        }

        protected override void OnProcessScenePlacedNetworkObject(Scene scene, NetworkObject networkObject)
        {
            var clientSceneManager = networkObject.NetworkManager.SceneManager;
            var serverSceneManager = ServerNetworkManager.SceneManager;
            // The server --should-- have this entry if it is the exact same scene being loaded for the client
            if (serverSceneManager.ScenePlacedObjects.ContainsKey(networkObject.GlobalObjectIdHash))
            {
                var serverEntries = serverSceneManager.ScenePlacedObjects[networkObject.GlobalObjectIdHash];
                var clientEntries = clientSceneManager.ScenePlacedObjects[networkObject.GlobalObjectIdHash];
                if (networkObject.NetworkManager == m_NetworkManager && networkObject.NetworkManager.IsClient)
                {
                    if (!clientEntries.ContainsKey(networkObject.gameObject.scene.handle))
                    {
                        clientEntries.Add(scene.handle, networkObject);
                    }

                    if (!serverEntries.ContainsKey(networkObject.gameObject.scene.handle))
                    {
                        serverEntries.Add(scene.handle, networkObject);
                    }
                }
                else if (networkObject.NetworkManager == ServerNetworkManager && networkObject.NetworkManager.IsServer)
                {
                    // Server already has this entry (i.e. serverEntry)
                    // Improve this
                    clientEntries.Add(serverEntries.First().Value.gameObject.scene.handle, networkObject);
                }
            }
            else
            {
                //throw Exception
            }
        }

        public ConditionalClientLoadScene(NetworkManager serverNetworkManager, NetworkManager networkManager) : base(networkManager)
        {
            ServerNetworkManager = serverNetworkManager;
            networkManager.SceneManager.SceneManagerHandler = this;
        }
    }

}
