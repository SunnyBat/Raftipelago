using FMODUnity;
using HarmonyLib;
using Raftipelago.Data;
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
			if (ComponentManager<ItemMapping>.Value.getArchipelagoLocationId(___item.UniqueIndex) >= 0)
			{
				//RuntimeManager.PlayOneShot(eventRef_Learn, default(Vector3));
				// TODO Correct item image for notification
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(new Notification_Research_Info(___item.settings_Inventory.DisplayName, ___localPlayer.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				// TODO Uncomment when ready to set as Learned in reasearch table list
				//___menuItems[i].Learn();
				return false;
			}
			else if (ComponentManager<ItemMapping>.Value.getArchipelagoLocationId(___item.UniqueIndex) == -1)
			{
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(new Notification_Research_Info(___item.settings_Inventory.DisplayName, ___localPlayer.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				return false;
			}
			return true;
		}
	}
}
