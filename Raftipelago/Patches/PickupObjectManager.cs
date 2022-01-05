using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(PickupObjectManager), "RemovePickupItem", typeof(PickupItem_Networked))]
	public class HarmonyPatch_PickupObjectManager_RemovePickupItem
	{
		[HarmonyPrefix]
		public static void NeverReplace(PickupItem_Networked pickupNetwork)
		{
			UnityEngine.Debug.Log("RPI: " + pickupNetwork.name);
			if (pickupNetwork != null
				&& pickupNetwork.CanBePickedUp()
				&& ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(pickupNetwork.name, out string pickupName))
			{
				UnityEngine.Debug.Log("RPI2: " + pickupName);
				if (Semih_Network.IsHost || Semih_Network.InSinglePlayerMode)
				{
					ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(pickupName);
					if (pickupName == "Tangaroa Next Frequency") // Special condition for victory
					{
						ComponentManager<IArchipelagoLink>.Value.SetGameCompleted(true);
					}
				}
				// TODO Display as player who unlocked
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
					.researchInfoQue.Enqueue(new Notification_Research_Info(pickupName, RAPI.GetLocalPlayer().steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
			}
		}
	}
}