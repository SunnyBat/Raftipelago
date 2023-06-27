using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(PickupObjectManager), "RemovePickupItem", typeof(PickupItem_Networked), typeof(CSteamID))]
	public class HarmonyPatch_PickupObjectManager_RemovePickupItem
	{
		[HarmonyPrefix]
		public static bool SometimesReplace(PickupItem_Networked pickupNetwork, CSteamID pickupPlayerID,
			ref bool __result)
		{
			Logger.Trace($"RemovePickupItem: {pickupNetwork.name} | {pickupPlayerID} | {pickupNetwork.CanBePickedUp()}");
			if (pickupNetwork != null
				&& (!Raft_Network.IsHost || pickupNetwork.CanBePickedUp())
				&& ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(pickupNetwork.name, out string pickupName))
			{
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
					.researchInfoQue.Enqueue(new Notification_Research_Info(pickupName, pickupPlayerID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				if (ComponentManager<ExternalData>.Value.LocationsToSuppress.Contains(pickupName))
				{
					__result = PickupObjectManager.RemovePickupItem(pickupNetwork);
					return false;
				}
			}
			Logger.Trace("RemovePickupItem not suppressing event");
			return true;
		}
	}

	[HarmonyPatch(typeof(PickupObjectManager), "RemovePickupItem", typeof(PickupItem_Networked))]
	public class HarmonyPatch_PickupObjectManager_RemovePickupItem2
	{
		[HarmonyPrefix]
		public static void NeverReplace(PickupItem_Networked pickupNetwork)
		{
			if (Raft_Network.IsHost
				&& pickupNetwork != null
				&& pickupNetwork.CanBePickedUp()
				&& ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(pickupNetwork.name, out string pickupName))
			{
				ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(pickupName);
			}
			Logger.Trace($"RemovePickupItem: {pickupNetwork.CanBePickedUp()} | {pickupNetwork.stopTrackingOnPickup} | {pickupNetwork.name}");
		}
	}
}