using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using System.Reflection;
using TMPro;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(BalboaRelayStationScreen), "RefreshScreen")]
	public class HarmonyPatch_BalboaRelayStationScreen_RefreshScreen
	{
		// Can move this to separate tracking class if we care enough. Not too important.
		public static int previousStationCount = -1;

		[HarmonyPrefix]
		public static bool AlwaysReplace(BalboaRelayStationScreen __instance,
			TextMeshPro ___frequencyText,
			TextMeshPro ___stationsActivatedText)
		{
			int activeStationCount = (int)typeof(BalboaRelayStationScreen).GetMethod("GetActiveStationCount", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, null);
			if (activeStationCount == 0)
			{
				___frequencyText.gameObject.SetActiveSafe(false);
				___stationsActivatedText.gameObject.SetActiveSafe(false);
			}
			else
			{
				___frequencyText.gameObject.SetActiveSafe(false); // We're always going to use stationsActivatedText to display data
				___stationsActivatedText.gameObject.SetActiveSafe(true);
				if (activeStationCount < 3)
				{
					LocalizationParameters.itemX = activeStationCount + "/3 ";
					___stationsActivatedText.text = Helper.GetTerm("Game/Balboa/RelayStationsActive", true);
				}
				else if (previousStationCount == 2) // This will prevent duplicate messages as well as messages when first loading world if quest is already completed
				{
					___stationsActivatedText.text = "Location sent";
					var locationName = "Relay Station quest";
					if (Raft_Network.IsHost)
					{
						ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(locationName);
					}
					// TODO Use ID of player who unlocked rather than local player
					(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
						.researchInfoQue.Enqueue(new Notification_Research_Info(locationName, RAPI.GetLocalPlayer().steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				}
			}
			previousStationCount = activeStationCount;
			return false;
		}
	}
}