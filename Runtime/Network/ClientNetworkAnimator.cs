using Unity.Netcode.Components;
using UnityEngine;

namespace Azeesoft.Multiplayer
{
public class ClientNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
}
