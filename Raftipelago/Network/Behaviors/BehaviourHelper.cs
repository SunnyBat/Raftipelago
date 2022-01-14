using Raftipelago.Data;
using Steamworks;
using System;

namespace Raftipelago.Network.Behaviors
{
    public class BehaviourHelper
    {
        public static void SendArchipelagoData(Target toSendTo = Target.Other)
        {
            if (Semih_Network.IsHost && ComponentManager<IArchipelagoLink>.Value.IsSuccessfullyConnected() && !Semih_Network.InMenuScene)
            {
                var syncPacket = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.RaftipelagoPacket_SyncArchipelagoData")
                    .GetConstructor(new Type[] { typeof(Messages), typeof(MonoBehaviour_Network) }).Invoke(new object[] { Messages.NOTHING, ComponentManager<ArchipelagoDataSync>.Value });
                syncPacket.GetType().GetProperty("ItemIdToName").SetValue(syncPacket, ComponentManager<IArchipelagoLink>.Value.GetAllItemIds());
                syncPacket.GetType().GetProperty("PlayerIdToName").SetValue(syncPacket, ComponentManager<IArchipelagoLink>.Value.GetAllPlayerIds());
                ComponentManager<Semih_Network>.Value.RPC((Message)syncPacket, toSendTo, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);

                var itemPacket = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.RaftipelagoPacket_SyncItems")
                    .GetConstructor(new Type[] { typeof(Messages), typeof(MonoBehaviour_Network) }).Invoke(new object[] { Messages.NOTHING, ComponentManager<ItemSyncBehaviour>.Value });
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
                    itmSend.GetType().GetProperty("PlayerId").SetValue(itmSend, parsed.Item3);
                    arr.SetValue(itmSend, i);
                }
                itemPacket.GetType().GetProperty("Items").SetValue(itemPacket, arr);
                ComponentManager<Semih_Network>.Value.RPC((Message)itemPacket, toSendTo, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
            }
        }
    }
}
