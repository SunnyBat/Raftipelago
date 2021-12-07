using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago.Network
{
    public interface IArchipelagoLink
    {
        void Connect(string URL, string username, string password);

        void SetIsInWorld(bool inWorld);

        void SendChatMessage(string message);

        void LocationUnlocked(int locationId);

        void Disconnect();
    }
}
