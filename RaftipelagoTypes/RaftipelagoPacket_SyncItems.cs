using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            this.RaftipelagoMessage = RaftipelagoMessage.SyncItems;
        }

        public SyncItemsData[] Items { get; set; }
    }
}
