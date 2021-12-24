using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(LocalizationParameters), "GetParameterValue", typeof(string))]
	public class HarmonyPatch_LocalizationParameters_GetParameterValue
	{
		[HarmonyPrefix]
		public static bool GetParameterValue_SometimesReplace(string parameter,
			ref string __result)
		{
			if (parameter == "RemoteSteamID"
				&& CommonUtils.TryGetArchipelagoPlayerIdFromSteamId(LocalizationParameters.remoteSteamID.m_SteamID, out int playerId))
			{
				__result = ComponentManager<IArchipelagoLink>.Value.GetPlayerAlias(playerId);
				return false;
			}
			return true;
		}
	}
}