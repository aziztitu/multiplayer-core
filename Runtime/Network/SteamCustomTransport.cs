using UnityEngine;

#if !DISABLESTEAMWORKS
using HeathenEngineering.SteamworksIntegration.API;
using Netcode.Transports;
#else
using System;
using Unity.Netcode;
#endif

namespace Azeesoft.Multiplayer
{
/// <summary>
/// Extension of SteamNetworkingSocketsTransport
/// 
/// Prevents breaking asset references when switching Platforms
/// </summary>
#if !DISABLESTEAMWORKS
public class SteamCustomTransport : SteamNetworkingSocketsTransport
#else
public class SteamCustomTransport : NetworkTransport
#endif
{
    [Header("General")]
    public ulong ApplicationId = 480;
    public bool InitializeOnStart = true;

    [Header("Misc")]
    public bool EnableDebugging = false;

    private static bool initialized = false;

    void Start()
    {
#if !DISABLESTEAMWORKS
        if (InitializeOnStart)
        {
            Initialize();
        }
#endif
    }

    void Initialize()
    {
        if (initialized)
        {
            return;
        }

#if !DISABLESTEAMWORKS

        App.isDebugging = EnableDebugging;

#if !UNITY_SERVER
        #region Client
        App.Client.Initialize(ApplicationId);
        #endregion
#else
        #region Server
        Debug.Error("Auto initialization is not supported for Server. Please do it manually.");
        #endregion
#endif

        initialized = true;
#endif
    }


#if DISABLESTEAMWORKS
    // Stub for compatibility in non-steam builds

    NotImplementedException InvalidPlatformExpection => new("Steam is not available for this platform");

    public override ulong ServerClientId => throw InvalidPlatformExpection;

    public override void DisconnectLocalClient()
    {
        throw InvalidPlatformExpection;
    }

    public override void DisconnectRemoteClient(ulong clientId)
    {
        throw InvalidPlatformExpection;
    }

    public override ulong GetCurrentRtt(ulong clientId)
    {
        throw InvalidPlatformExpection;
    }

    public override void Initialize(NetworkManager networkManager = null)
    {
        throw InvalidPlatformExpection;
    }

    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        throw InvalidPlatformExpection;
    }

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
    {
        throw InvalidPlatformExpection;
    }

    public override void Shutdown()
    {
        throw InvalidPlatformExpection;
    }

    public override bool StartClient()
    {
        throw InvalidPlatformExpection;
    }

    public override bool StartServer()
    {
        throw InvalidPlatformExpection;
    }
#endif
}
}
