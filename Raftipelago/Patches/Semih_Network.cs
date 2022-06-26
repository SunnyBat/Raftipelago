using HarmonyLib;
using Raftipelago.Network;
using Steamworks;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Raft_Network), "TryToJoinGame", typeof(CSteamID), typeof(string))]
	public class HarmonyPatch_Raft_Network_TryToJoinGame
	{
		[HarmonyPrefix]
		public static void Prefix(CSteamID hostID, string password)
		{
			ComponentManager<IArchipelagoLink>.Value?.Disconnect();
		}
	}
}
