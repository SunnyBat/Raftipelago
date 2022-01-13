using Raftipelago.Data;
using Steamworks;
using System;
using System.Collections.Generic;

namespace Raftipelago.Network.Behaviors
{
    [Serializable]
    public class ArchipelagoDataSync : MonoBehaviour_Network
    {
        private Type _rpPacketType;

        public ArchipelagoDataSync()
        {
            var asm = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly);
            _rpPacketType = asm.GetType("RaftipelagoTypes.RaftipelagoPacket_SyncArchipelagoData");
            UnityEngine.Debug.Log($"rpPacketType: {_rpPacketType}");
            BehaviourIndex = CommonUtils.GetNetworkBehaviourUniqueIndex();
        }
        public override bool Deserialize(Message_NetworkBehaviour msg, CSteamID remoteID)
        {
            if (msg != null && msg.GetType() == _rpPacketType) // RaftipelagoPacket_SyncItems
            {
                var playerToName = (Dictionary<int, string>)_rpPacketType.GetProperty("PlayerIdToName").GetValue(msg);
                var itemToName = (Dictionary<int, string>)_rpPacketType.GetProperty("ItemIdToName").GetValue(msg);
                UnityEngine.Debug.Log($"Sync data: {playerToName?.Count}, {itemToName?.Count}");
                ComponentManager<ArchipelagoDataManager>.Value.ItemIdToName = itemToName;
                ComponentManager<ArchipelagoDataManager>.Value.PlayerIdToName = playerToName;
                return true;
            }
            return false;
        }
    }
}
