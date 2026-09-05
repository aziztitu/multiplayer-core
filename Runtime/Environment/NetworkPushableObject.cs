using Unity.Netcode;
using UnityEngine;

namespace Azeesoft.Multiplayer
{
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkPushableObject : NetworkBehaviour
    {
        [SerializeField] private float maxVelocity = 5f;

        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.maxLinearVelocity = maxVelocity;
        }

        [Rpc(SendTo.Server)]
        public void Push_Rpc(Vector3 force, Vector3 contactPoint)
        {
            if (rb.linearVelocity.magnitude >= maxVelocity)
            {
                return;
            }

            rb.AddForceAtPosition(force, contactPoint, ForceMode.Impulse);
        }
    }
}
