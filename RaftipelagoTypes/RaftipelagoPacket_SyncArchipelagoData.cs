using System;
using System.Collections.Generic;

namespace RaftipelagoTypes
{
    [Serializable]
    public class RaftipelagoPacket_SyncArchipelagoData : RaftipelagoPacket
    {
        public RaftipelagoPacket_SyncArchipelagoData(Messages type, MonoBehaviour_Network behaviour) : base(type, behaviour)
        {
            this.RaftipelagoMessage = RaftipelagoMessage.SyncAPData;
        }

        public Dictionary<int, string> PlayerIdToName { get; set; }
        public Dictionary<int, string> ItemIdToName { get; set; }
    }
}
