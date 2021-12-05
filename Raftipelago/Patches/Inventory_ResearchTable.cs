using FMODUnity;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Raftipelago.Patches
{
    [HarmonyPatch(typeof(Inventory_ResearchTable), "Research", typeof(Item_Base), typeof(bool))]
    public class HarmonyPatch_Inventory_ResearchTable_Research
    {
        [HarmonyPrefix]
        public static bool ReplaceMethod(Item_Base item, bool autoLearnRecipe,
			ref bool __result,
			Inventory_ResearchTable __instance,
			ref string ___eventRef_Research,
			ref List<Item_Base> ___researchedItems,
			ref List<ResearchMenuItem> ___menuItems,
			ref Dictionary<Item_Base, AvaialableResearchItem> ___availableResearchItems)
        {
			//Debug.Log("Called ReplaceMethod!");
			//Debug.Log($"item={item != null} autoLearnRecipe={autoLearnRecipe} instance={__instance != null} eventRef_Research={___eventRef_Research != null} researchedItems={___researchedItems != null} menuItems={___menuItems != null} availableResearchItems={___availableResearchItems != null}");
			//if (!autoLearnRecipe)
			//{
			//	Debug.Log(___availableResearchItems.Count);
			//	___availableResearchItems.Values.Do((itm) => {
			//		Debug.Log(itm.Item.name);
			//		itm.SetResearchedState(false);
			//	});
			//	__result = false;
			//	return false;
   //         }
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
							researchMenuItem.gameObject?.SetActive(true);
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

	[HarmonyPatch(typeof(Inventory_ResearchTable), "LearnItem", typeof(Item_Base), typeof(CSteamID))]
	public class HarmonyPatch_Inventory_ResearchTable_LearnItem
	{
		[HarmonyPrefix]
		public static bool ReplaceMethod(Item_Base item, CSteamID researcherID,
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
                    RuntimeManager.PlayOneShot(___eventRef_Learn, default(Vector3));
                    (___notificationManager.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(new Notification_Research_Info(item.settings_Inventory.DisplayName, researcherID, item.settings_Inventory.Sprite));
                    //___menuItems[i].Learn();
                    Debug.Log("Name: " + item.name);
					Debug.Log("UName: " + item.UniqueName);
					item.settings_recipe.Learned = !item.settings_recipe.Learned;
					__result = true;
					break;
				}
				else if (___menuItems[i].GetItem().UniqueIndex == item.UniqueIndex)
                {
					Debug.Log("Wtf");
					Debug.Log("Name: " + item.name);
					Debug.Log("UName: " + item.UniqueName);
				}
			}
			__instance.SortMenuItems();
			return false;
		}
	}

	[HarmonyPatch(typeof(Inventory_ResearchTable), "CreateMenuItems", typeof(CraftingMenu))]
	public class HarmonyPatch_Inventory_ResearchTable_CreateMenuItems
	{
		[HarmonyPrefix]
		public static void PostMethod(CraftingMenu craftingMenu)
		{
			Debug.Log("PostCreateMenuItems");
			craftingMenu.AllRecipes.Do(recipe => Debug.Log(recipe.name));
		}
	}
}
