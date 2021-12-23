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
			Item_Base ___item,
			Inventory_ResearchTable ___inventoryRef)
		{
			Debug.Log("Learn_AlwaysReplace: " + ___item.UniqueName);
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
			var availableResearchItems = (Dictionary<Item_Base, AvaialableResearchItem>)typeof(Inventory_ResearchTable).GetField("availableResearchItems",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(___inventoryRef);
			// This will remove the items from the research list. If they are not researched (theoretically this will never happen),
			// this will just shrug and move on.
			var researchedItems = ___inventoryRef.GetResearchedItems(); // Pulls direct reference to list, which we can modify
			var unresearchedItems = new List<Item_Base>();
			___bingoMenuItems.ForEach(itm =>
			{
				Debug.Log("Unresearching " + itm.BingoItem.UniqueName);
				availableResearchItems[itm.BingoItem].SetResearchedState(false);
				researchedItems.Remove(itm.BingoItem);
				unresearchedItems.Add(itm.BingoItem);
			});

			var menuItems = ___inventoryRef.GetMenuItems();
			menuItems.ForEach(mI =>
			{
				if (!mI.Learned)
				{
					var bingoItems = (List<BingoMenuItem>)typeof(ResearchMenuItem).GetField("bingoMenuItems",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(mI);
					// TODO Check bingoState, modify if bingo enabled and we remove an item
					bingoItems.ForEach(bI =>
					{
						if (unresearchedItems.Contains(bI.BingoItem))
						{
							// Revert BingoItem state for individual ingredient
							var itemImage = (Image)typeof(BingoMenuItem).GetField("itemImage",
								System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(bI);
							bI.SetBingoState(false);
							itemImage.color = new Color(1f, 1f, 1f, 0.5f);

							// Undo Bingo() if item had bingo set before
							var learnButton = (Button)typeof(ResearchMenuItem).GetField("learnButton",
								System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(mI);
							if (learnButton.interactable)
							{
								var miItemImage = (Image)typeof(ResearchMenuItem).GetField("itemImage",
									System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(mI);
								learnButton.interactable = false;
								learnButton.gameObject.SetActive(false);
								miItemImage.sprite = mI.GetItem().settings_Inventory.Sprite;
								miItemImage.color = new Color(1f, 1f, 1f, 0.5f);
							}
						}
					});
				}
			});

			return false;
		}
	}
}
