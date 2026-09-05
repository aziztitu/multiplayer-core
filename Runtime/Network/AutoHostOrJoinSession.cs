using System.Linq;
using Unity.Multiplayer.Playmode;
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
            var mppmTags = CurrentPlayer.ReadOnlyTags();
            if (mppmTags.Contains("Host"))
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
