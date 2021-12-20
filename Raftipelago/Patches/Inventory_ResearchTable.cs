using FMODUnity;
using HarmonyLib;
using Raftipelago.Data;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;

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
					break;
				}
			}
			__instance.SortMenuItems();
			return false;
		}

		[HarmonyPrefix]
		public static bool Research_AlwaysReplace(Item_Base item, bool autoLearnRecipe,
			ref bool __result,
			Inventory_ResearchTable __instance,
			List<Item_Base> ___researchedItems,
			string ___eventRef_Research,
			ref List<ResearchMenuItem> ___menuItems,
			Dictionary<Item_Base, AvaialableResearchItem> ___availableResearchItems)
        {
			if (__instance.CanResearchItem(item))
			{
				RuntimeManager.PlayOneShot(___eventRef_Research, default(Vector3));
				___researchedItems.Add(item);
				if (item.settings_recipe.IsBlueprint)
				{
					for (int i = 0; i < ___menuItems.Count; i++)
					{
						ResearchMenuItem researchMenuItem = ___menuItems[i];
						if (researchMenuItem.GetItem().UniqueIndex == item.settings_recipe.BlueprintItem.UniqueIndex)
						{
							researchMenuItem.gameObject.SetActive(true);
							break;
						}
					}
				}
				else
				{
					if (___availableResearchItems.ContainsKey(item))
					{
						___availableResearchItems[item].SetResearchedState(true);
					}
					int num = 0;
					for (int j = 0; j < ___menuItems.Count; j++)
					{
						___menuItems[j].Research(item);
						float sortBingoPercent = ___menuItems[j].SortBingoPercent;
						if (sortBingoPercent == 1f && autoLearnRecipe)
						{
							___menuItems[j].LearnInstantly();
						}
						if (sortBingoPercent == 1f || ___menuItems[j].Learned)
						{
							num++;
						}
					}
					if (num == ___menuItems.Count && GameModeValueManager.GetCurrentGameModeValue().achievementVariables.trackAchievments)
					{
						AchievementHandler.UnlockAchievement(AchievementType.ach_researcher);
					}
				}
				__instance.SortMenuItems();
				__result = true;
			}
			else
			{
				__result = false;
			}
			return false;
		}
	}
}
