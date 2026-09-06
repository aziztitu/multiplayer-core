using System.Linq;
using Unity.Multiplayer.PlayMode;
using Unity.Netcode;
using UnityEngine;

namespace Azeesoft.Multiplayer
{
    public class AutoHostOrJoinSession : MonoBehaviour
    {
        [SerializeField] private bool autoConnect = true;

        void Start()
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsConnectedClient)
            {
                return;
            }

            if (autoConnect && Application.isEditor)
            {
                HostOrJoin();
            }
        }

        void HostOrJoin()
        {
            var networkManager = NetworkManager.Singleton;
            if (CurrentPlayer.Tags.Contains("Host"))
            {
                networkManager.StartHost();
            }
            else
            {
                networkManager.StartClient();
            }
        }
    }
}
