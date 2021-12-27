using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(SaveAndLoad), "RestoreRGDGame", typeof(RGD_Game))]
	public class HarmonyPatch_SaveAndLoad_RestoreRGDGame
	{
		[HarmonyPostfix]
		public static void Postfix(RGD_Game game)
		{
			var customGameType = ComponentManager<AssemblyManager>.Value.GetAssembly(AssemblyManager.RaftipelagoTypesAssembly).GetType("RaftipelagoTypes.RGD_Game_Raftipelago");
			if (game.GetType() == customGameType)
			{
				var itemPacks = (List<int>) customGameType.GetField("Raftipelago_ItemPacks").GetValue(game);
				ComponentManager<IArchipelagoLink>.Value.SetUnlockedResourcePacks(itemPacks);
			}
			else
			{
				ComponentManager<IArchipelagoLink>.Value.SetUnlockedResourcePacks(new List<int>());
			}
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
			CommonUtils.SetUnlockedItemPacks(newGame, ComponentManager<IArchipelagoLink>.Value.GetAllUnlockedResourcePacks());
			__result = newGame;
		}
	}
}
