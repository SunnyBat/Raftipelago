using HarmonyLib;
using System.Collections.Generic;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(SaveAndLoad), "RestoreRGDGame", typeof(RGD_Game))]
	public class HarmonyPatch_SaveAndLoad_RestoreRGDGame
	{
		[HarmonyPostfix]
		public static void Postfix(RGD_Game game)
		{
			ComponentManager<ItemTracker>.Value.SetAlreadyReceivedItemData(CommonUtils.GetUnlockedItemIdentifiers(game) ?? new List<long>());
		}
    }

	[HarmonyPatch(typeof(SaveAndLoad), "CreateRGDGame")]
	public class HarmonyPatch_SaveAndLoad_CreateRGDGame
	{
		[HarmonyPostfix]
		public static void Postfix(
			ref RGD_Game __result)
		{
			var newGame = CommonUtils.CreateRaftipelagoGame(__result);
			CommonUtils.SetUnlockedItemIdentifiers(newGame, ComponentManager<ItemTracker>.Value.GetAllReceivedItemIds());
			__result = newGame;
		}
	}
}
