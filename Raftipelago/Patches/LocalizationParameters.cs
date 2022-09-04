using HarmonyLib;
using Raftipelago.Data;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(LocalizationParameters), "GetParameterValue", typeof(string))]
	public class HarmonyPatch_LocalizationParameters_GetParameterValue
	{
		[HarmonyPrefix]
		public static bool SometimesReplace(string parameter,
			ref string __result)
		{
			if (parameter == "RemoteSteamID"
				&& CommonUtils.TryGetArchipelagoPlayerIdFromSteamId(LocalizationParameters.remoteSteamID.m_SteamID, out int playerId))
			{
				__result = ComponentManager<ArchipelagoDataManager>.Value.GetPlayerName(playerId);
				if (__result == null && playerId == 0)
                {
					__result = "Server";
                }
				return false;
			}
			return true;
		}
	}
}