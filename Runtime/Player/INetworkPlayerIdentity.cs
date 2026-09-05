using System.Collections.Generic;

namespace Azeesoft.Multiplayer
{
    public interface INetworkPlayerIdentity
    {
        ulong ClientId { get; }
        string PlayerName { get; set; }
    }

    public static class NetworkPlayerIdentities
    {
        static readonly Dictionary<ulong, INetworkPlayerIdentity> instances = new();

        public static bool TryGet(ulong clientId, out INetworkPlayerIdentity identity)
        {
            return instances.TryGetValue(clientId, out identity);
        }

        public static void Register(INetworkPlayerIdentity identity)
        {
            instances[identity.ClientId] = identity;
        }

        public static void Unregister(ulong clientId)
        {
            instances.Remove(clientId);
        }
    }
}
