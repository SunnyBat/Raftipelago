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
			if (noteName == "Tangaroa Next Frequency") // Special condition for victory
            {
				ComponentManager<IArchipelagoLink>.Value.SetGameCompleted(true);
            }
			else
			{
				ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(noteName);
			}
			(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
				.researchInfoQue.Enqueue(new Notification_Research_Info(noteName, ___playerNetwork.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
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
			var needsPassthrough = true;
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
				ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(pickupName);
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
					.researchInfoQue.Enqueue(new Notification_Research_Info(pickupName, ___playerNetwork.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				needsPassthrough = false;
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