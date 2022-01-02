using Raftipelago.Data;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raftipelago.Behaviors
{
    [Serializable]
    public class ArchipelagoDataSync : MonoBehaviour_Network
    {
        private Type _rpPacketType;

        public ArchipelagoDataSync()
        {
            var asm = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly);
            _rpPacketType = asm.GetType("RaftipelagoTypes.RaftipelagoPacket_SyncArchipelagoData");
            NetworkUpdateManager.SetIndexForBehaviour(this);
            NetworkUpdateManager.AddBehaviour(this);
        }
        public override bool Deserialize(Message_NetworkBehaviour msg, CSteamID remoteID)
        {
            if (msg != null && msg.GetType() == _rpPacketType) // RaftipelagoPacket_SyncItems
            {
                Debug.Log("DS1");
                var rpPacketTypeValue = (int)_rpPacketType.GetProperty("RaftipelagoMessage").GetValue(msg);
                if (rpPacketTypeValue == 2) // SyncAPData
                {
                    Debug.Log("Found packet for DataSync");
                    var playerToName = (Dictionary<int, string>)_rpPacketType.GetProperty("PlayerIdToName").GetValue(msg);
                    var itemToName = (Dictionary<int, string>)_rpPacketType.GetProperty("ItemIdToName").GetValue(msg);
                    ComponentManager<ArchipelagoDataManager>.Value.ItemIdToName = itemToName;
                    ComponentManager<ArchipelagoDataManager>.Value.PlayerNameToId = playerToName;
                }
                return true;
            }
            return false;
        }
    }
}
