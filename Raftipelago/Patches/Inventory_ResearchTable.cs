using FMODUnity;
using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Inventory_ResearchTable), "LearnItem", typeof(Item_Base), typeof(CSteamID))]
	public class HarmonyPatch_Inventory_ResearchTable_LearnItem
	{
		[HarmonyPrefix]
		public static bool LearnItem_AlwaysReplace(Item_Base item, CSteamID researcherID,
			ref bool __result,
			Inventory_ResearchTable __instance,
			ref string ___eventRef_Learn,
			ref List<ResearchMenuItem> ___menuItems)
		{
			Debug.Log("LearnItem_AlwaysReplace: " + item.UniqueName);
			for (int i = 0; i < ___menuItems.Count; i++)
			{
				if (!___menuItems[i].Learned && ___menuItems[i].GetItem().UniqueIndex == item.UniqueIndex)
				{
					// Specifically skip researching so we can spam this as necessary
					(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(new Notification_Research_Info(item.settings_Inventory.DisplayName, researcherID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
					Debug.Log("Learned item from research table: " + ___menuItems[i].name);
					ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(___menuItems[i].name); // TODO Verify this is correct name
					___menuItems[i].Learn();
					break;
				}
			}
			__instance.SortMenuItems();
			return false;
		}
	}

	[HarmonyPatch(typeof(Inventory_ResearchTable), "Research", typeof(Item_Base), typeof(bool))]
	public class HarmonyPatch_Inventory_ResearchTable_Research
	{
		[HarmonyPrefix]
		public static bool Research_AlwaysReplace(Item_Base item, bool autoLearnRecipe,
			ref bool __result,
			Inventory_ResearchTable __instance,
			List<Item_Base> ___researchedItems,
			string ___eventRef_Research,
			ref List<ResearchMenuItem> ___menuItems,
			Dictionary<Item_Base, AvaialableResearchItem> ___availableResearchItems)
		{
			Debug.Log("Research_AlwaysReplace: " + item.UniqueName);
			if (__instance.CanResearchItem(item)) // Checks for not already researched AND that at least one not-researched item accepts the item being researched
			{
				RuntimeManager.PlayOneShot(___eventRef_Research, default(Vector3));
				___researchedItems.Add(item);
				Debug.Log("NL: " + ___researchedItems.Count);
				if (item.settings_recipe.IsBlueprint)
				{
					for (int i = 0; i < ___menuItems.Count; i++)
					{
						ResearchMenuItem researchMenuItem = ___menuItems[i];
						if (researchMenuItem.GetItem().UniqueIndex == item.settings_recipe.BlueprintItem.UniqueIndex)
						{
							// TODO How to handle? Separate unlock or just ignore? If ignoring, just override CanResearchBlueprint() to always false.
							// Alternatively, we could make blueprints NOT auto-unlock items, and require they be put into the research table. If we
							// do this, we'll want to add the item with a custom recipe to the research table itself in case the blueprint is lost
							// (to prevent softlocks).
							//researchMenuItem.gameObject.SetActive(true);
							Debug.Log("Skipping blueprint " + item.UniqueName);
							break;
						}
					}
				}
				else
				{
					if (___availableResearchItems.ContainsKey(item))
					{
						Debug.Log("Successfully researched " + item.UniqueName);
						___availableResearchItems[item].SetResearchedState(true);
						for (int j = 0; j < ___menuItems.Count; j++)
						{
							___menuItems[j].Research(item);
						}
					}
					else
                    {
						Debug.Log("Unable to find research item " + item.UniqueName);
                    }
				}
				__instance.SortMenuItems();
				__result = true;
			}
			else
			{
				Debug.Log("Can't research " + item.UniqueName);
				__result = false;
			}
			return false;
		}
	}
}
