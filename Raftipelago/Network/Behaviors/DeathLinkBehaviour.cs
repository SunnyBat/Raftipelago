using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;
using System;

namespace Raftipelago.Network.Behaviors
{
    [Serializable]
    public class DeathLinkBehaviour : MonoBehaviour_Network
    {
        private Type _rpPacketType;

        public DeathLinkBehaviour()
        {
            var asm = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly);
            _rpPacketType = asm.GetType("RaftipelagoTypes.RaftipelagoPacket_DeathLink");
            BehaviourIndex = CommonUtils.GetNetworkBehaviourUniqueIndex();
        }

        public override bool Deserialize(Message_NetworkBehaviour msg, CSteamID remoteID)
        {
            if (msg.GetType() == _rpPacketType) // RaftipelagoPacket_DeathLink
            {
                if (!Raft_Network.IsHost)
                {
                    RAPI.GetLocalPlayer().Kill();
                }
                return true;
            }
            return false;
        }
    }
}
