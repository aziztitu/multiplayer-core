using AZUtils;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Azeesoft.Multiplayer
{
    public class LobbyManager : SingletonNetworkBehaviour<LobbyManager>
    {
        [FormerlySerializedAs("SelectedTestGameSceneName")]
        public NetworkVariable<FixedString128Bytes> SelectedGameSceneName = new("");
    }
}
