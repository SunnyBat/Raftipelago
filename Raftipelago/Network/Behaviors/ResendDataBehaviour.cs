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
            BehaviourIndex = CommonUtils.GetNetworkBehaviourUniqueIndex();
        }

        public override bool Deserialize(Message_NetworkBehaviour msg, CSteamID remoteID)
        {
            if (msg.GetType() == _rpPacketType) // RaftipelagoPacket_ResendData
            {
                if (Raft_Network.IsHost && ComponentManager<IArchipelagoLink>.Value.IsSuccessfullyConnected())
                {
                    BehaviourHelper.SendArchipelagoData();
                }
                return true;
            }
            return false;
        }
    }
}
