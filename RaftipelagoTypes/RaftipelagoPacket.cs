using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaftipelagoTypes
{
    [Serializable]
    public class RaftipelagoPacket : Message_NetworkBehaviour
    {
        public RaftipelagoPacket(Messages type, MonoBehaviour_Network behaviour) : base(type, behaviour)
        {
        }
    }
}
