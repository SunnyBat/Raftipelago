using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ResearchMenuItem;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(ResearchMenuItem), "LearnButton")]
	public class HarmonyPatch_ResearchMenuItem_LearnButton
	{
		[HarmonyPrefix]
		public static bool ReplaceMethod(
			ref Network_Player ___localPlayer,
			ref Item_Base ___item,
			ref Inventory_ResearchTable ___inventoryRef,
			ref OnLearnedRecipe ___OnLearnedRecipeEvent,
			ResearchMenuItem __instance)
		{
			if (___localPlayer == null)
			{
				___localPlayer = ComponentManager<Network_Player>.Value;
			}
			Message_ResearchTable_ResearchOrLearn message = new Message_ResearchTable_ResearchOrLearn(Messages.ResearchTable_Learn, ___localPlayer, ___localPlayer.steamID, ___item.UniqueIndex);
			if (Semih_Network.IsHost)
			{
				___inventoryRef.network.RPC(message, Target.Other, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
				___inventoryRef.LearnItem(___item, ___localPlayer.steamID);
				if (___OnLearnedRecipeEvent != null)
				{
					___OnLearnedRecipeEvent();
				}
			}
			else
			{
				___inventoryRef.network.SendP2P(___inventoryRef.network.HostID, message, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
			}
			return false;
		}
	}
}
