using HarmonyLib;
using Raftipelago.Data;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Raftipelago.Patches
{
    [HarmonyPatch(typeof(ResearchMenuItem), "LearnButton")]
    public class HarmonyPatch_ResearchMenuItem_LearnButton
    {
        [HarmonyPrefix]
        public static bool SometimesReplace(
            ref Network_Player ___localPlayer,
            ref Item_Base ___item,
            ref Inventory_ResearchTable ___inventoryRef,
            ref ResearchMenuItem.OnLearnedRecipe ___OnLearnedRecipeEvent)
		{
			if (___localPlayer == null)
			{
				___localPlayer = ComponentManager<Network_Player>.Value;
			}
			if (Raft_Network.IsHost)
			{
				___inventoryRef.LearnItem(___item, ___localPlayer.steamID);
				if (___OnLearnedRecipeEvent != null)
				{
					___OnLearnedRecipeEvent();
				}
			}
			else
			{
				Message_ResearchTable_ResearchOrLearn message = new Message_ResearchTable_ResearchOrLearn(Messages.ResearchTable_Learn, ___localPlayer, ___localPlayer.steamID, ___item.UniqueIndex);
				___inventoryRef.network.SendP2P(___inventoryRef.network.HostID, message, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
			}
			return false;
        }
    }

    [HarmonyPatch(typeof(ResearchMenuItem), "Learn")]
	public class HarmonyPatch_ResearchMenuItem_Learn
	{
		[HarmonyPrefix]
		public static bool AlwaysReplace(
			ref bool ___learned,
			ref CanvasGroup ___canvasgroup,
			ref Button ___learnButton,
			ref Text ___learnedText,
			ref List<BingoMenuItem> ___bingoMenuItems,
			Inventory_ResearchTable ___inventoryRef)
		{
			// We still want to mark as learned, but we don't want to actually set the item to craftable
			___learned = true;
			___canvasgroup.alpha = 0.5f;
			___learnButton.gameObject.SetActive(false);
			___learnedText.gameObject.SetActive(true);

			// Addition by Raftipelago
			if (ComponentManager<ArchipelagoDataManager>.Value.TryGetSlotData("ExpensiveResearch", out long isExpensiveResearchEnabled) && isExpensiveResearchEnabled == 1)
			{
				var availableResearchItems = (Dictionary<Item_Base, AvaialableResearchItem>)typeof(Inventory_ResearchTable).GetField("availableResearchItems",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(___inventoryRef);
				// This will remove the items from the research list. If they are not researched (theoretically this will never happen),
				// this will just shrug and move on.
				var researchedItems = ___inventoryRef.GetResearchedItems(); // Pulls direct reference to list, which we can modify
				var unresearchedItems = new List<Item_Base>();
				___bingoMenuItems.ForEach(itm =>
				{
					availableResearchItems[itm.BingoItem].SetResearchedState(false);
					researchedItems.Remove(itm.BingoItem);
					unresearchedItems.Add(itm.BingoItem);
				});

				// Remove BingoItem from all Research Table items
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
			}

			return false;
		}
	}
}
