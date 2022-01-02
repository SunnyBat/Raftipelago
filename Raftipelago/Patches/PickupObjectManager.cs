using HarmonyLib;
using Raftipelago.Data;
using Steamworks;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(PickupObjectManager), "RemovePickupItem", typeof(PickupItem_Networked), typeof(CSteamID))]
	public class HarmonyPatch_PickupObjectManager_RemovePickupItem
	{
		[HarmonyPrefix]
		public static void NeverReplace(PickupItem_Networked pickupNetwork, CSteamID pickupPlayerID)
		{
			UnityEngine.Debug.Log("RPI: " + pickupNetwork.name);
			if (pickupNetwork != null)
            {
				if (pickupNetwork.name.StartsWith("NoteBookNote"))
				{
					var noteName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, pickupNetwork.name);
					(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
						.researchInfoQue.Enqueue(new Notification_Research_Info(noteName, pickupPlayerID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				}
			}
		}
	}
}