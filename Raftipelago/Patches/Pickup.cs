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
			var noteName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, note.name);
			ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(noteName);
			(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
				.researchInfoQue.Enqueue(new Notification_Research_Info(noteName, ___playerNetwork.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
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
			var needsPassthrough = true;
			if (pickup.PickupName?.StartsWith("Blueprint") ?? false)
			{
				var pickupName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, pickup.PickupName);
				ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(pickupName);
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
					.researchInfoQue.Enqueue(new Notification_Research_Info(pickupName, ___playerNetwork.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				needsPassthrough = false;
			}
			else if (pickup.name == "Pickup_Landmark_Caravan_RocketDoll") // Doesn't have PickupName set for some reason
			{
				var pickupName = "Blueprint: Firework";
				ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(pickupName);
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
					.researchInfoQue.Enqueue(new Notification_Research_Info(pickupName, ___playerNetwork.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				needsPassthrough = false;
			}
			else if (pickup.yieldHandler != null) // Old code, remove once new code verified working
			{
				Debug.LogWarning("Pickup: Parsing yieldHandler for " + pickup.name);
				pickup.yieldHandler.Yield.ForEach(cst =>
				{
					var itemToAdd = cst?.item;
					if (itemToAdd?.settings_recipe.IsBlueprint ?? false)
					{
						ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(itemToAdd.UniqueName);
						(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
							.researchInfoQue.Enqueue(new Notification_Research_Info(itemToAdd.UniqueName, ___playerNetwork.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
						needsPassthrough = false;
					}
				});
			}
			if (!needsPassthrough)
			{
				if (pickup.networkID != null)
				{
					PickupObjectManager.RemovePickupItem(pickup.networkID);
				}
				else
				{
					PoolManager.ReturnObject(pickup.gameObject);
				}
			}
			return needsPassthrough;
		}
	}
}