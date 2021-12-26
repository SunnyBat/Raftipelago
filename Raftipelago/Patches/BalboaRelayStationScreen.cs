using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(BalboaRelayStationScreen), "RefreshScreen")]
	public class HarmonyPatch_BalboaRelayStationScreen_RefreshScreen
	{
		// Can move this to separate tracking class if we care enough. Not too important.
		public static int previousStationCount = -1;

		[HarmonyPrefix]
		public static bool RefreshScreen_AlwaysReplace(BalboaRelayStationScreen __instance,
			ref TextMeshPro ___frequencyText,
			ref TextMeshPro ___stationsActivatedText)
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
					ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(locationName);
					var fakeSteamID = CommonUtils.GetFakeSteamIDForArchipelagoPlayerId(0); // TODO Get playerID somehow
					// TODO Not let this spam 3 times upon completion
					(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
						.researchInfoQue.Enqueue(new Notification_Research_Info(locationName, fakeSteamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				}
			}
			previousStationCount = activeStationCount;
			return false;
		}
	}
}