using System;

namespace RaftipelagoTypes
{
    [Serializable]
    public class SyncItemsData
    {
        public int ItemId { get; set; }
        public int LocationId { get; set; }
        public int PlayerId { get; set; }
    }

    [Serializable]
    public class RaftipelagoPacket_SyncItems : RaftipelagoPacket
    {
        public RaftipelagoPacket_SyncItems(Messages type, MonoBehaviour_Network behaviour) : base(type, behaviour)
        {
        }

        public SyncItemsData[] Items { get; set; }
    }
}
