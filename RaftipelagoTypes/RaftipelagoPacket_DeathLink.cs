using System;

namespace RaftipelagoTypes
{
    [Serializable]
    public class RaftipelagoPacket_DeathLink : RaftipelagoPacket
    {
        public RaftipelagoPacket_DeathLink(Messages type, MonoBehaviour_Network behaviour) : base(type, behaviour)
        {
        }
    }
}
