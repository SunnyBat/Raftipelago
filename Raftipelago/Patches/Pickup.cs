using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;

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
					PickupObjectManager.RemovePickupItemNetwork(note.networkID, ___playerNetwork.steamID);
				}
				else
				{
					PickupObjectManager.RemovePickupItem(note.networkID, ___playerNetwork.steamID);
				}
			}
			if (Semih_Network.IsHost || Semih_Network.InSinglePlayerMode)
			{
				var noteName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, note.name);
				if (noteName == "Tangaroa Next Frequency") // Special condition for victory
				{
					ComponentManager<IArchipelagoLink>.Value.SetGameCompleted(true);
				}
				else
				{
					ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(noteName);
				}
			}
			// TODO Confirm that the above will trigger on host
			return false;
		}
	}

	[HarmonyPatch(typeof(Pickup), "PickupItem", typeof(PickupItem), typeof(bool), typeof(bool))]
	public class HarmonyPatch_Pickup_PickupItem
	{
		[HarmonyPrefix]
		public static bool SometimesReplace(PickupItem pickup, bool forcePickup, bool triggerHandAnimation,
			Network_Player ___playerNetwork)
		{
			string pickupName = null;
			if (pickup.PickupName?.StartsWith("Blueprint") ?? false)
			{
				pickupName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, pickup.PickupName);
			}
			else if (pickup.name == "Pickup_Landmark_Caravan_RocketDoll") // Doesn't have PickupName set for some reason
			{
				pickupName = "Blueprint: Firework";
			}
			if (!string.IsNullOrWhiteSpace(pickupName))
			{
				if (Semih_Network.IsHost || Semih_Network.InSinglePlayerMode)
				{
					ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(pickupName);
				}
				// RemovePickupItem*() will queue notification as necessary
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
					PoolManager.ReturnObject(pickup.gameObject); // Assuming that this doesn't need any notification
				}
				return false;
			}
			else
            {
				return true;
            }
		}
	}
}