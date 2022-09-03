using System;
using System.Collections.Generic;

namespace RaftipelagoTypes
{
    public class RaftipelagoMessageTypes
    {
        public const Messages ARCHIPELAGO_DATA = (Messages)471;
        public const Messages ITEM_RECEIVED = (Messages)472;
        public const Messages DEATHLINK_RECEIVED = (Messages)473;
        public const Messages REQUEST_RESYNC = (Messages)474;
    }

    [Serializable]
    public class Message_ArchipelagoData : Message
    {
        public Dictionary<long, string> ItemIdToNameMap { get; private set; }
        public Dictionary<int, string> PlayerIdToNameMap { get; private set; }
        public Dictionary<string, object> SlotData { get; private set; }
        public Dictionary<long, int> CurrentReceivedItemIndeces { get; private set; }
        public Message_ArchipelagoData(
            Dictionary<long, string> itemIdToNameMap,
            Dictionary<int, string> playerIdToNameMap,
            Dictionary<string, object> slotData,
            Dictionary<long, int> currentReceivedItemIndeces
            ) : base(RaftipelagoMessageTypes.ARCHIPELAGO_DATA)
        {
            ItemIdToNameMap = itemIdToNameMap;
            PlayerIdToNameMap = playerIdToNameMap;
            SlotData = slotData;
            CurrentReceivedItemIndeces = currentReceivedItemIndeces;
        }
    }

    [Serializable]
    public class Message_ArchipelagoItemsReceived : Message
    {
        public List<long> ItemIds { get; private set; }
        public List<long> LocationIds { get; private set; }
        public List<int> PlayerIds { get; private set; }
        public List<int> CurrentItemIndexes { get; private set; }

        public Message_ArchipelagoItemsReceived(List<long> itemIds, List<long> locationIds, List<int> playerIds, List<int> currentItemIndexes) : base(RaftipelagoMessageTypes.ITEM_RECEIVED)
        {
            ItemIds = itemIds;
            LocationIds = locationIds;
            PlayerIds = playerIds;
            CurrentItemIndexes = currentItemIndexes;
        }
    }

    [Serializable]
    public class Message_ArchipelagoRequestResync : Message
    {
        public Message_ArchipelagoRequestResync() : base(RaftipelagoMessageTypes.REQUEST_RESYNC)
        {
        }
    }

    [Serializable]
    public class Message_ArchipelagoDeathLink : Message
    {
        public Message_ArchipelagoDeathLink() : base(RaftipelagoMessageTypes.DEATHLINK_RECEIVED)
        {
        }
    }
}
