using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Unity.Netcode.TestHelpers.Runtime
{
    /// <summary>
    /// The default SceneManagerHandler used for all NetcodeIntegrationTest derived children.
    /// </summary>
    internal class IntegrationTestSceneHandler : ISceneManagerHandler, IDisposable
    {
        internal CoroutineRunner CoroutineRunner;

        // Default client simulated delay time
        protected const float k_ClientLoadingSimulatedDelay = 0.02f;

        // Controls the client simulated delay time
        protected float m_ClientLoadingSimulatedDelay = k_ClientLoadingSimulatedDelay;

        public delegate bool CanClientsLoadUnloadDelegateHandler();
        public event CanClientsLoadUnloadDelegateHandler CanClientsLoad;
        public event CanClientsLoadUnloadDelegateHandler CanClientsUnload;

        internal List<Coroutine> CoroutinesRunning = new List<Coroutine>();

        protected NetworkManager m_NetworkManager;

        /// <summary>
        /// Used to control when clients should attempt to fake-load a scene
        /// Note: Unit/Integration tests that only use <see cref="NetcodeIntegrationTestHelpers"/>
        /// need to subscribe to the CanClientsLoad and CanClientsUnload events
        /// in order to control when clients can fake-load.
        /// Tests that derive from <see cref="NetcodeIntegrationTest"/> already have integrated
        /// support and you can override <see cref="NetcodeIntegrationTest.CanClientsLoad"/> and
        /// <see cref="NetcodeIntegrationTest.CanClientsUnload"/>.
        /// </summary>
        protected bool OnCanClientsLoad()
        {
            if (CanClientsLoad != null)
            {
                return CanClientsLoad.Invoke();
            }
            return true;
        }

        /// <summary>
        /// Fake-Loads a scene for a client
        /// </summary>
        internal IEnumerator ClientLoadSceneCoroutine(string sceneName, ISceneManagerHandler.SceneEventAction sceneEventAction)
        {
            yield return new WaitForSeconds(m_ClientLoadingSimulatedDelay);
            while (!OnCanClientsLoad())
            {
                yield return new WaitForSeconds(m_ClientLoadingSimulatedDelay);
            }
            sceneEventAction.Invoke();
        }

        protected bool OnCanClientsUnload()
        {
            if (CanClientsUnload != null)
            {
                return CanClientsUnload.Invoke();
            }
            return true;
        }

        /// <summary>
        /// Fake-Unloads a scene for a client
        /// </summary>
        internal IEnumerator ClientUnloadSceneCoroutine(ISceneManagerHandler.SceneEventAction sceneEventAction)
        {
            yield return new WaitForSeconds(m_ClientLoadingSimulatedDelay);
            while (!OnCanClientsUnload())
            {
                yield return new WaitForSeconds(m_ClientLoadingSimulatedDelay);
            }
            sceneEventAction.Invoke();
        }

        protected virtual AsyncOperation OnLoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode, ISceneManagerHandler.SceneEventAction sceneEventAction)
        {
            CoroutinesRunning.Add(CoroutineRunner.StartCoroutine(ClientLoadSceneCoroutine(sceneName, sceneEventAction)));
            // This is OK to return a "nothing" AsyncOperation since we are simulating client loading
            return new AsyncOperation();
        }

        public AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode, ISceneManagerHandler.SceneEventAction sceneEventAction)
        {
            return OnLoadSceneAsync(sceneName, loadSceneMode, sceneEventAction);
        }

        protected virtual AsyncOperation OnUnloadSceneAsync(Scene scene, ISceneManagerHandler.SceneEventAction sceneEventAction)
        {
            CoroutinesRunning.Add(CoroutineRunner.StartCoroutine(ClientUnloadSceneCoroutine(sceneEventAction)));
            // This is OK to return a "nothing" AsyncOperation since we are simulating client loading
            return new AsyncOperation();
        }

        public AsyncOperation UnloadSceneAsync(Scene scene, ISceneManagerHandler.SceneEventAction sceneEventAction)
        {
            return OnUnloadSceneAsync(scene, sceneEventAction);
        }



        public void Dispose()
        {
            foreach (var coroutine in CoroutinesRunning)
            {
                CoroutineRunner.StopCoroutine(coroutine);
            }
            CoroutineRunner.StopAllCoroutines();

            Object.Destroy(CoroutineRunner.gameObject);
        }

        protected bool OnNetworkObjectFilter(NetworkObject networkObject, Scene sceneToFilterBy)
        {
            // First filter by scene handle to assure it exists in the scene just loaded and by the owning NetworkManager instance
            if (networkObject.gameObject.scene.handle == sceneToFilterBy.handle && networkObject.NetworkManager == m_NetworkManager)
            {
                // Then for all non-assigned/yet to be spawned locally
                if (networkObject.IsSceneObject == null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The default scene placed NetworkObject filter
        /// </summary>
        public bool NetworkObjectFilter(NetworkObject networkObject, Scene sceneToFilterBy)
        {
            return OnNetworkObjectFilter(networkObject, sceneToFilterBy);
        }

        protected virtual void OnProcessScenePlacedNetworkObject(Scene scene, NetworkObject networkObject)
        {
            var sceneManager = networkObject.NetworkManager.SceneManager;

            if (!sceneManager.ScenePlacedObjects[networkObject.GlobalObjectIdHash].ContainsKey(networkObject.gameObject.scene.handle))
            {
                sceneManager.ScenePlacedObjects[networkObject.GlobalObjectIdHash].Add(networkObject.gameObject.scene.handle, networkObject);
            }
            else
            {
                var exitingEntryName = sceneManager.ScenePlacedObjects[networkObject.GlobalObjectIdHash][networkObject.gameObject.scene.handle] != null ?
                    sceneManager.ScenePlacedObjects[networkObject.GlobalObjectIdHash][networkObject.gameObject.scene.handle].name : "Null Entry";
                throw new Exception($"{networkObject.name} tried to registered with {nameof(sceneManager.ScenePlacedObjects)} which already contains " +
                    $"the same {nameof(NetworkObject.GlobalObjectIdHash)} value {networkObject.GlobalObjectIdHash} for {exitingEntryName}!");
            }
        }

        // Mirror the default action to throw an exception
        public void ProcessScenePlacedNetworkObject(Scene scene, NetworkObject networkObject)
        {
            OnProcessScenePlacedNetworkObject(scene, networkObject);
        }

        public IntegrationTestSceneHandler(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;

            if (CoroutineRunner == null)
            {
                CoroutineRunner = new GameObject("UnitTestSceneHandlerCoroutine").AddComponent<CoroutineRunner>();
            }
        }

        //public IntegrationTestSceneHandler()
        //{
        //    if (CoroutineRunner == null)
        //    {
        //        CoroutineRunner = new GameObject("UnitTestSceneHandlerCoroutine").AddComponent<CoroutineRunner>();
        //    }
        //}
    }
}
