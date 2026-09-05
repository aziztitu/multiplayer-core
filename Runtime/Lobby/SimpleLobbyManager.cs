using AZUtils;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace Azeesoft.Multiplayer
{
    public class SimpleLobbyManager : SingletonNetworkBehaviour<SimpleLobbyManager>
    {
        [FormerlySerializedAs("SelectedTestGameSceneName")]
        public NetworkVariable<FixedString128Bytes> SelectedGameSceneName = new("");
    }
}
