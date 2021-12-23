using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Pickup), "PickupNoteBookNote", typeof(NoteBookNotePickup), typeof(bool))]
	public class HarmonyPatch_Pickup_PickupNoteBookNote
	{
		[HarmonyPrefix]
		public static bool PickupNoteBookNote_AlwaysReplace(NoteBookNotePickup note, bool triggerHandAnimation,
			PlayerAnimator ___playerAnimator,
			SoundManager ___soundManager,
			Network_Player ___playerNetwork)
		{
			if (triggerHandAnimation)
			{
				___playerAnimator.SetAnimation(PlayerAnimation.Trigger_GrabItem, false);
			}
			___soundManager.PlayUI_MoveItem();
			if (note.networkID != null)
			{
				if (note.networkID.stopTrackUseRPC)
				{
					PickupObjectManager.RemovePickupItemNetwork(note.networkID, ___playerNetwork.steamID);
				}
				else
				{
					PickupObjectManager.RemovePickupItem(note.networkID, ___playerNetwork.steamID);
				}
			}
			ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(note.name);
			(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
				.researchInfoQue.Enqueue(new Notification_Research_Info(note.name, ___playerNetwork.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
			return false;
		}
	}

	[HarmonyPatch(typeof(Pickup), "PickupItem", typeof(PickupItem), typeof(bool), typeof(bool))]
	public class HarmonyPatch_Pickup_PickupItem
	{
		[HarmonyPrefix]
		public static bool PickupItem_SometimesReplace(PickupItem pickup, bool forcePickup, bool triggerHandAnimation,
			Network_Player ___playerNetwork)
		{
			if (pickup.yieldHandler != null)
            {
				bool hadBlueprint = false;
				pickup.yieldHandler.Yield.ForEach(cst =>
				{
					var itemToAdd = cst?.item;
					if (itemToAdd?.settings_recipe.IsBlueprint ?? false)
					{
						Debug.Log(cst?.item?.UniqueName);
						ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(itemToAdd.UniqueName);
						(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
							.researchInfoQue.Enqueue(new Notification_Research_Info(itemToAdd.UniqueName, ___playerNetwork.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
						hadBlueprint = true;
					}
				});

				if (hadBlueprint)
				{
					PoolManager.ReturnObject(pickup.gameObject);
					return false;
				}
            }
			return true; // TODO Return true instead
		}
	}
}