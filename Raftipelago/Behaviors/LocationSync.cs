using Raftipelago.Data;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Raftipelago.Behaviors
{
    [Serializable]
    public class LocationSync : MonoBehaviour_Network
    {
        private Type _rpPacketType;
        private Type _syncItemDataType;
        private Type _syncItemDataArrayType;

        public LocationSync()
        {
            var asm = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly);
            _rpPacketType = asm.GetType("RaftipelagoTypes.RaftipelagoPacket_SyncItems");
            _syncItemDataType = asm.GetType("RaftipelagoTypes.SyncItemsData");
            _syncItemDataArrayType = _syncItemDataType.MakeArrayType();
            NetworkUpdateManager.SetIndexForBehaviour(this);
            NetworkUpdateManager.AddBehaviour(this);
        }
        public override bool Deserialize(Message_NetworkBehaviour msg, CSteamID remoteID)
        {
            if (msg.GetType() == _rpPacketType) // RaftipelagoPacket_SyncItems
            {
                Debug.Log("LS1");
                var rpPacketTypeValue = (int)_rpPacketType.GetProperty("RaftipelagoMessage").GetValue(msg);
                if (rpPacketTypeValue == 1) // SyncItems
                {
                    Debug.Log("Found packet for LocationSync");
                    var itemsToAdd = _rpPacketType.GetProperty("Items").GetValue(msg);
                    var itemsEnumerator = _syncItemDataArrayType.GetMethod("GetEnumerator").Invoke(itemsToAdd, null);
                    bool currentResult;
                    do
                    {
                        currentResult = (bool)itemsEnumerator.GetType().GetMethod("MoveNext").Invoke(itemsEnumerator, null);
                        var nextItem = itemsEnumerator.GetType().GetProperty("Current").GetValue(itemsEnumerator);
                        var itemId = (int)nextItem.GetType().GetProperty("ItemId").GetValue(nextItem);
                        var locationId = (int)nextItem.GetType().GetProperty("LocationId").GetValue(nextItem);
                        var playerId = (int)nextItem.GetType().GetProperty("PlayerId").GetValue(nextItem);
                        ComponentManager<ItemTracker>.Value.RaftItemUnlockedForCurrentWorld(itemId, locationId, playerId);
                    } while (currentResult);
                }
                return true;
            }
            return false;
        }
    }
}
