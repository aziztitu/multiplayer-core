using Unity.Netcode;
using UnityEngine;

namespace Azeesoft.Multiplayer
{
public class AZNetworkManager : NetworkManager
{
    public static AZNetworkManager Instance => Singleton as AZNetworkManager;

    public NetworkTransport CurrentNetworkTransport => NetworkConfig.NetworkTransport;

    void Start()
    {
        if (Singleton != this)
        {
            // Prevent duplicate Network Managers
            Destroy(gameObject);
            return;
        }
    }

    void Update()
    {

    }

    public T UseTransport<T>() where T : NetworkTransport
    {
        var transport = GetComponent<T>();
        if (transport == null)
        {
            Debug.LogError($"No component of type {typeof(T)} found on this GameObject.");
            return null;
        }

        NetworkConfig.NetworkTransport = transport;

        Debug.Log($"Switched transport to {typeof(T).Name}");

        return transport;
    }
    public T GetTransport<T>() where T : NetworkTransport
    {
        var transport = GetComponent<T>();
        if (transport == null)
        {
            Debug.LogError($"No component of type {typeof(T)} found on this GameObject.");
            return null;
        }

        return transport;
    }
}
}
