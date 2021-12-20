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
using UnityEngine.UI;
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
			return true;
		}
	}

	[HarmonyPatch(typeof(ResearchMenuItem), "Learn")]
	public class HarmonyPatch_ResearchMenuItem_Learn
	{
		[HarmonyPrefix]
		public static bool Learn_AlwaysReplace(
			ref bool ___learned,
			ref CanvasGroup ___canvasgroup,
			ref Button ___learnButton,
			ref Text ___learnedText,
			ref CraftingMenu ___craftingMenu,
			ref List<BingoMenuItem> ___bingoMenuItems,
			Inventory_ResearchTable ___inventoryRef)
		{
			// Original function with item learn omission
			___learned = true;
			___canvasgroup.alpha = 0.5f;
			___learnButton.gameObject.SetActive(false);
			___learnedText.gameObject.SetActive(true);
			if (CanvasHelper.ActiveMenu == MenuType.Inventory)
			{
				___craftingMenu.ReselectCategory();
			}

			// Addition by Raftipelago
			var ari = (Dictionary<Item_Base, AvaialableResearchItem>)typeof(Inventory_ResearchTable).GetField("availableResearchItems").GetValue(___inventoryRef);
			// This will remove the items from the research list. If they are not researched (theoretically this will never happen),
			// this will just shrug and move on.
			var researchedItems = ___inventoryRef.GetResearchedItems(); // Pulls direct reference to list, which we can modify
			___bingoMenuItems.ForEach(itm =>
			{
				ari[itm.BingoItem].SetResearchedState(false);
				researchedItems.Remove(itm.BingoItem);
			});
			return false;
		}
	}
}
