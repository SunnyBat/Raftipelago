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
			if (pickupNetwork != null
				&& (!Semih_Network.IsHost || pickupNetwork.CanBePickedUp())
				&& ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(pickupNetwork.name, out string pickupName))
			{
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
					.researchInfoQue.Enqueue(new Notification_Research_Info(pickupName, pickupPlayerID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				__result = PickupObjectManager.RemovePickupItem(pickupNetwork);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PickupObjectManager), "RemovePickupItem", typeof(PickupItem_Networked))]
	public class HarmonyPatch_PickupObjectManager_RemovePickupItem2
	{
		[HarmonyPrefix]
		public static void NeverReplace(PickupItem_Networked pickupNetwork)
		{
			if (pickupNetwork != null
				&& pickupNetwork.CanBePickedUp()
				&& ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(pickupNetwork.name, out string pickupName))
			{
				if (Semih_Network.IsHost)
				{
					ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(pickupName);
					if (pickupName == "Tangaroa Next Frequency") // Special condition for victory
					{
						ComponentManager<IArchipelagoLink>.Value.SetGameCompleted(true);
					}
				}
			}
		}
	}
}