using System;

namespace RaftipelagoTypes
{
    [Serializable]
    public class RaftipelagoPacket_ResendData : RaftipelagoPacket
    {
        public RaftipelagoPacket_ResendData(Messages type, MonoBehaviour_Network behaviour) : base(type, behaviour)
        {
        }
    }
}
