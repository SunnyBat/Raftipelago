using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;
using System;

namespace Raftipelago.Network.Behaviors
{
    [Serializable]
    public class ResendDataBehaviour : MonoBehaviour_Network
    {
        private Type _rpPacketType;

        public ResendDataBehaviour()
        {
            var asm = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly);
            _rpPacketType = asm.GetType("RaftipelagoTypes.RaftipelagoPacket_ResendData");
            BehaviourIndex = CommonUtils.GetNetworkBehaviourUniqueIndex(); // TODO Do we need ObjectIndex as well
        }
        public override bool Deserialize(Message_NetworkBehaviour msg, CSteamID remoteID)
        {
            if (msg.GetType() == _rpPacketType) // RaftipelagoPacket_ResendData
            {
                if (Semih_Network.IsHost && ComponentManager<IArchipelagoLink>.Value.IsSuccessfullyConnected())
                {
                    var syncPacket = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.RaftipelagoPacket_SyncArchipelagoData")
                        .GetConstructor(new Type[] { typeof(Messages), typeof(MonoBehaviour_Network) }).Invoke(new object[] { Messages.NOTHING, ComponentManager<ArchipelagoDataSync>.Value });
                    syncPacket.GetType().GetProperty("PlayerIdToName").SetValue(syncPacket, ComponentManager<IArchipelagoLink>.Value.GetAllItemIds());
                    syncPacket.GetType().GetProperty("ItemIdToName").SetValue(syncPacket, ComponentManager<IArchipelagoLink>.Value.GetAllPlayerIds());
                    ComponentManager<Semih_Network>.Value.RPC((Message)syncPacket, Target.All, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game); // TODO Target.Other instead of Target.All

                    var itemPacket = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.RaftipelagoPacket_SyncItems")
                        .GetConstructor(new Type[] { typeof(Messages), typeof(MonoBehaviour_Network) }).Invoke(new object[] { Messages.NOTHING, ComponentManager<ItemSync>.Value });
                    var allItemUniqueIdentifiers = ComponentManager<ItemTracker>.Value.GetAllReceivedItemIds();
                    var sid = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.SyncItemsData");
                    var arr = Array.CreateInstance(sid, allItemUniqueIdentifiers.Count);

                    for (int i = 0; i < allItemUniqueIdentifiers.Count; i++)
                    {
                        var uid = allItemUniqueIdentifiers[i];
                        var parsed = ComponentManager<ItemTracker>.Value.ParseUniqueIdentifier(uid);
                        var itmSend = sid.GetConstructor(new Type[] { }).Invoke(null);
                        itmSend.GetType().GetProperty("ItemId").SetValue(itmSend, parsed.Item1);
                        itmSend.GetType().GetProperty("LocationId").SetValue(itmSend, parsed.Item2);
                        // TODO playerid (info unavailable atm)
                        arr.SetValue(itmSend, i);
                    }
                    itemPacket.GetType().GetProperty("Items").SetValue(itemPacket, arr);
                    ComponentManager<Semih_Network>.Value.RPC((Message)itemPacket, Target.All, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game); // TODO Target.Other instead of Target.All
                }
                return true;
            }
            return false;
        }
    }
}
