using System.Collections.Generic;

namespace Raftipelago.Network
{
    public interface IArchipelagoLink
    {
        void Connect(string URL, string username, string password);

        bool IsSuccessfullyConnected();

        void SetIsInWorld(bool inWorld);

        void SendChatMessage(string message);

        void LocationUnlocked(params long[] locationId);

        void LocationUnlocked(params string[] locationName);

        string GetItemNameFromId(long itemId);

        Dictionary<string, object> GetLastLoadedSlotData();

        string GetPlayerAlias(int playerId);

        void SetGameCompleted(bool completed);

        void Disconnect();

        void Heartbeat();

        void onUnload();

        Dictionary<int, string> GetAllPlayerIds();

        Dictionary<long, string> GetAllItemIds();

        bool IsDeathLinkEnabled();

        void SendDeathLinkPacket(string cause);

        SplitArchipelagoItemData GetAllItems();
    }

    public class SplitArchipelagoItemData
    {
        public List<long> itemIds { get; set; }
        public List<long> locationIds { get; set; }
        public List<int> playerIDs { get; set; }
        public List<int> itemIndices { get; set; }
    }
}
