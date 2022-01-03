using FMODUnity;
using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Inventory_ResearchTable), "LearnItem", typeof(Item_Base), typeof(CSteamID))]
	public class HarmonyPatch_Inventory_ResearchTable_LearnItem
	{
		[HarmonyPrefix]
		public static bool AlwaysReplace(Item_Base item, CSteamID researcherID,
			ref bool __result,
			Inventory_ResearchTable __instance,
			ref List<ResearchMenuItem> ___menuItems)
		{
			for (int i = 0; i < ___menuItems.Count; i++)
			{
				var menuItemBase = ___menuItems[i].GetItem();
				if (!___menuItems[i].Learned && menuItemBase.UniqueIndex == item.UniqueIndex)
				{
					if (!CommonUtils.IsValidResearchTableItem(menuItemBase))
					{
						typeof(ResearchMenuItem).GetField("learned", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(___menuItems[i], true);
						menuItemBase.settings_recipe.Learned = true;
						(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
							.researchInfoQue.Enqueue(new Notification_Research_Info(item.settings_Inventory.DisplayName, researcherID, item.settings_Inventory.Sprite));
					}
					else
					{
						(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(new Notification_Research_Info(item.settings_Inventory.DisplayName, researcherID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
						if (Semih_Network.IsHost || Semih_Network.InSinglePlayerMode)
						{
							ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(menuItemBase.settings_Inventory.DisplayName);
						}
						___menuItems[i].Learn(); // Overridden to set item as learned and remove researches from research table
					}
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
		public static bool AlwaysReplace(Item_Base item, bool autoLearnRecipe,
			ref bool __result,
			Inventory_ResearchTable __instance,
			List<Item_Base> ___researchedItems,
			string ___eventRef_Research,
			ref List<ResearchMenuItem> ___menuItems,
			Dictionary<Item_Base, AvaialableResearchItem> ___availableResearchItems)
		{
			if (__instance.CanResearchItem(item)) // Checks for not already researched AND that at least one not-researched item accepts the item being researched
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
							// This should never happen, but log a message just in case we get one
							Debug.LogError("Skipping blueprint " + item.UniqueName + " -- This is likely a Raftipelago bug (blueprint items should never be researched by Raft, only Archipelago).");
							break;
						}
					}
				}
				else
				{
					if (___availableResearchItems.ContainsKey(item))
					{
						___availableResearchItems[item].SetResearchedState(true);
						for (int j = 0; j < ___menuItems.Count; j++)
						{
							___menuItems[j].Research(item);
						}
					}
					else
                    {
						Debug.LogError("Unable to find research item " + item.settings_Inventory.DisplayName);
                    }
				}
				__instance.SortMenuItems();
				__result = true;
			}
			else
			{
				Debug.Log("Can't research " + item.settings_Inventory.DisplayName);
				__result = false;
			}
			return false;
		}
	}
}
