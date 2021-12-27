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

        bool IsSuccessfullyConnected();

        void SetIsInWorld(bool inWorld);

        void SendChatMessage(string message);

        void LocationUnlocked(params int[] locationId);

        void LocationUnlocked(params string[] locationName);

        string GetItemNameFromId(int itemId);

        string GetPlayerAlias(int playerId);

        void ToggleDebug();

        void SetAlreadyReceivedItemIds(List<int> resourcePackIds);

        List<int> GetAllReceivedItemIds();

        void Disconnect();

        void Heartbeat();
    }
}
