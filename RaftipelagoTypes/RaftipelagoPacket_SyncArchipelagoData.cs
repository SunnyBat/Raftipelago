using System;
using System.Collections.Generic;

namespace RaftipelagoTypes
{
    [Serializable]
    public class RaftipelagoPacket_SyncArchipelagoData : RaftipelagoPacket
    {
        public RaftipelagoPacket_SyncArchipelagoData(Messages type, MonoBehaviour_Network behaviour) : base(type, behaviour)
        {
        }

        public Dictionary<int, string> PlayerIdToName { get; set; }
        public Dictionary<int, string> ItemIdToName { get; set; }
        public Dictionary<string, object> SlotData { get; set; }
        public List<long> AlreadyUnlockedLocations { get; set; }
    }
}
