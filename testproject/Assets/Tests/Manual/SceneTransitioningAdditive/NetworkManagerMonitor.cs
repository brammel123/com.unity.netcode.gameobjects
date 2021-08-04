using UnityEngine;
using Unity.Netcode;

public class NetworkManagerMonitor : MonoBehaviour
{
    // Start is called before the first frame update
    private void Start()
    {
        var networkManagerInstances = FindObjectsOfType<NetworkManager>();
        foreach (var instance in networkManagerInstances)
        {
            if (instance.IsListening)
            {
                if (gameObject != instance.gameObject)
                {
                    var networkManager = GetComponent<NetworkManager>();
                    Destroy(gameObject);
                }
            }
        }
    }
}
