﻿using HarmonyLib;
using Raftipelago.Data;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Pickup), "PickupNoteBookNote", typeof(NoteBookNotePickup), typeof(bool))]
	public class HarmonyPatch_Pickup_PickupNoteBookNote
	{
		[HarmonyPrefix]
		public static bool AlwaysReplace(NoteBookNotePickup note, bool triggerHandAnimation,
			PlayerAnimator ___playerAnimator,
			SoundManager ___soundManager,
			Network_Player ___playerNetwork)
		{
			Logger.Debug("PickupNoteBookNote: " + note.name);
			if (ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(note.name, out string friendlyName)
				&& ComponentManager<ExternalData>.Value.LocationsToSuppress.Contains(friendlyName))
			{
				Logger.Debug("Suppressing note");
				if (triggerHandAnimation)
				{
					___playerAnimator.SetAnimation(PlayerAnimation.Trigger_GrabItem, false);
				}
				___soundManager.PlayUI_MoveItem();
				// RemovePickupItem*() will queue notification as necessary
				if (note.networkID != null)
				{
					if (note.networkID.stopTrackUseRPC)
					{
						Logger.Debug("PickupNoteBookNote stopTrackUseRPC");
						PickupObjectManager.RemovePickupItemNetwork(note.networkID, ___playerNetwork.steamID);
					}
					else
					{
						Logger.Debug("PickupNoteBookNote vanilla");
						PickupObjectManager.RemovePickupItem(note.networkID, ___playerNetwork.steamID);
					}
				}
				return false;
			}
			Logger.Debug("Not suppressing note");
			return true;
		}
	}

	[HarmonyPatch(typeof(Pickup), "PickupItem", typeof(PickupItem), typeof(bool), typeof(bool))]
	public class HarmonyPatch_Pickup_PickupItem
	{
		[HarmonyPrefix]
		public static bool SometimesReplace(PickupItem pickup, bool forcePickup, bool triggerHandAnimation,
			Pickup __instance,
			PlayerAnimator ___playerAnimator,
			Network_Player ___playerNetwork,
			DisplayTextManager ___displayTextManager)
		{
			if (ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(pickup.name, out string pickupName))
			{
				if (!forcePickup && !pickup.canBePickedUp)
				{
					return true;
				}
				if (triggerHandAnimation)
				{
					___playerAnimator.SetAnimation(PlayerAnimation.Trigger_GrabItem, false);
				}
				if (pickup.networkID != null)
				{
					if (pickup.networkID.stopTrackUseRPC)
					{
						PickupObjectManager.RemovePickupItemNetwork(pickup.networkID, ___playerNetwork.steamID);
					}
					else
					{
						PickupObjectManager.RemovePickupItem(pickup.networkID, ___playerNetwork.steamID);
					}
				}
				else
				{
					PoolManager.ReturnObject(pickup.gameObject);
				}
				___displayTextManager.HideDisplayTexts();
			}
			return true;
		}
	}
}