using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaftipelagoTypes
{
    [Serializable]
    public enum RaftipelagoMessage
    {
        SyncItems = 1,
        SyncAPData = 2
    }

    [Serializable]
    public class RaftipelagoPacket : Message_NetworkBehaviour
    {
        public RaftipelagoPacket(Messages type, MonoBehaviour_Network behaviour) : base(type, behaviour)
        {
        }

        public RaftipelagoMessage RaftipelagoMessage { get; set; }
    }
}
