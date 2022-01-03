using HarmonyLib;
using Raftipelago.Data;
using Steamworks;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(NetworkUpdateManager), "AddBehaviour", typeof(MonoBehaviour_Network))]
	public class HarmonyPatch_NetworkUpdateManager_AddBehaviour
	{
		[HarmonyPostfix]
		public static void NeverReplace(MonoBehaviour_Network behaviour)
		{
			Debug.Log("NUM: AB: " + behaviour.BehaviourIndex + " :: " + behaviour.ObjectIndex);
		}
	}

	[HarmonyPatch(typeof(NetworkUpdateManager), "Deserialize", typeof(Packet_Multiple), typeof(CSteamID))]
	public class HarmonyPatch_NetworkUpdateManager_Deserialize
	{
		[HarmonyPostfix]
		public static void NeverReplace(Packet_Multiple packet, CSteamID remoteID)
		{
			Debug.Log($"NUM: D: {packet.e} :: {packet.t} :: {packet.PacketType} :: {packet.SendType}");
			foreach (var pkt in packet.messages)
            {
				Debug.Log($"NUM: D2: {pkt.t} :: {pkt.Type}");
				var pType = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.RaftipelagoPacket_ResendData");
				if (pType == pkt.GetType())
				{
					var tst = (Message_NetworkBehaviour)pkt;
					var raftipelagoMessage = pType.GetProperty("RaftipelagoMessage").GetValue(pkt);
					Debug.Log($"NUM: D3: {tst.BehaviourIndex} :: {tst.ObjectIndex} :: {tst.o} :: {tst.t} :: {tst.Type} :: {raftipelagoMessage}");
					Debug.Log("NUM: D4: " + NetworkUpdateManager.NetworkedBehaviours.ContainsKey(tst.BehaviourIndex));
				}
			}
		}
	}
}