using FMODUnity;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static ResearchMenuItem;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(ResearchMenuItem), "LearnButton")]
	public class HarmonyPatch_ResearchMenuItem_LearnButton
	{
		[HarmonyPrefix]
		public static bool LearnButton_OptionalReplace(
			ref Network_Player ___localPlayer,
			ref Item_Base ___item,
			ref Inventory_ResearchTable ___inventoryRef,
			ref OnLearnedRecipe ___OnLearnedRecipeEvent,
			ref CraftingMenu ___craftingMenu,
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
				if (true) // TODO Check if we want to override with Archipelago
				{
					//RuntimeManager.PlayOneShot(eventRef_Learn, default(Vector3));
					// TODO Correct item image for notification
					(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(new Notification_Research_Info(___item.settings_Inventory.DisplayName, ___localPlayer.steamID, ___item.settings_Inventory.Sprite));
					// TODO Uncomment when ready to set as Learned in reasearch table list
					//___menuItems[i].Learn();
				}
				else
				{
					___inventoryRef.LearnItem(___item, ___localPlayer.steamID);
					if (___OnLearnedRecipeEvent != null)
					{
						___OnLearnedRecipeEvent();
					}
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
