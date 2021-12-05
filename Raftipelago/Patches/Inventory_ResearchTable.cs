using HarmonyLib;
using Raftipelago.Data;
using Steamworks;
using System.Collections.Generic;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Inventory_ResearchTable), "LearnItem", typeof(Item_Base), typeof(CSteamID))]
	public class HarmonyPatch_Inventory_ResearchTable_LearnItem
	{
		[HarmonyPrefix]
		public static bool LearnItem_OptionallyReplace(Item_Base item, CSteamID researcherID,
			ref bool __result,
			Inventory_ResearchTable __instance,
			ref string ___eventRef_Learn,
			ref NotificationManager ___notificationManager,
			ref List<ResearchMenuItem> ___menuItems)
		{
			for (int i = 0; i < ___menuItems.Count; i++)
			{
				if (!___menuItems[i].Learned && ___menuItems[i].GetItem().UniqueIndex == item.UniqueIndex)
				{
					// Specifically skip researching so we can spam this as necessary
					(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(new Notification_Research_Info(item.settings_Inventory.DisplayName, researcherID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				}
			}
			__instance.SortMenuItems();
			return false;
		}
	}
}
