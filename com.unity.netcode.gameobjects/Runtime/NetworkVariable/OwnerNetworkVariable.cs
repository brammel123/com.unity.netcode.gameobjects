using System;

namespace Unity.Netcode
{
    [Serializable]
    public class OwnerNetworkVariable<T> : NetworkVariable<T> where T : unmanaged
    {
        public OwnerNetworkVariable()
        {
        }

        public OwnerNetworkVariable(T value) : base(value)
        {
        }

        public override bool ShouldWrite(ulong clientId, bool isServer)
        {
            return isServer
                ? m_IsDirty && clientId != m_NetworkBehaviour.OwnerClientId
                : m_IsDirty && m_NetworkBehaviour.IsOwner;
        }

        public override T Value
        {
            get => m_InternalValue;
            set
            {
                if (m_NetworkBehaviour && !m_NetworkBehaviour.IsOwner)
                {
                    throw new InvalidOperationException("Only owner can write to an OwnerNetworkVariable");
                }

                Set(value);
            }
        }
    }
}
