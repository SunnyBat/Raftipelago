using Raftipelago.Data;
using Steamworks;
using System;

namespace Raftipelago.Network.Behaviors
{
    [Serializable]
    public class ItemSyncBehaviour : MonoBehaviour_Network
    {
        private Type _rpPacketType;
        private Type _syncItemDataArrayType;

        public ItemSyncBehaviour()
        {
            var asm = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly);
            _rpPacketType = asm.GetType("RaftipelagoTypes.RaftipelagoPacket_SyncItems");
            _syncItemDataArrayType = asm.GetType("RaftipelagoTypes.SyncItemsData").MakeArrayType();
            BehaviourIndex = CommonUtils.GetNetworkBehaviourUniqueIndex();
        }
        public override bool Deserialize(Message_NetworkBehaviour msg, CSteamID remoteID)
        {
            if (msg.GetType() == _rpPacketType) // RaftipelagoPacket_SyncItems
            {
                var itemsToAdd = _rpPacketType.GetProperty("Items").GetValue(msg);
                var itemsEnumerator = _syncItemDataArrayType.GetMethod("GetEnumerator").Invoke(itemsToAdd, null);
                var enumeratorType = itemsEnumerator.GetType();
                var moveNextMethodInfo = enumeratorType.GetMethod("MoveNext");
                var currentPropertyInfo = enumeratorType.GetProperty("Current");
                bool currentResult = (bool)moveNextMethodInfo.Invoke(itemsEnumerator, null);
                while (currentResult)
                {
                    var nextItem = currentPropertyInfo.GetValue(itemsEnumerator);
                    var itemType = nextItem.GetType();
                    var itemId = (int)itemType.GetProperty("ItemId").GetValue(nextItem);
                    var locationId = (int)itemType.GetProperty("LocationId").GetValue(nextItem);
                    var playerId = (int)itemType.GetProperty("PlayerId").GetValue(nextItem);
                    ComponentManager<ItemTracker>.Value.RaftItemUnlockedForCurrentWorld(itemId, locationId, playerId);
                    currentResult = (bool)moveNextMethodInfo.Invoke(itemsEnumerator, null);
                }
                return true;
            }
            return false;
        }
    }
}
