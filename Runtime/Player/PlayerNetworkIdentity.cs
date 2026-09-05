using AZUtils;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Azeesoft.Multiplayer
{
    public class PlayerNetworkIdentity : NetworkInstancesBehavior<PlayerNetworkIdentity>
    {
        public NetworkVariable<FixedString64Bytes> PlayerName = new("Anonymous", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        protected new void Start()
        {
            base.Start();
            Debug.Log($"Player Ready: {NetworkObject.OwnerClientId}");
        }
    }
}
