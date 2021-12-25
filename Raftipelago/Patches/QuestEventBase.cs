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
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(QuestEventBase), "Interact", typeof(Network_Player), typeof(bool))]
	public class HarmonyPatch_QuestEventBase_Interact
	{
		[HarmonyPrefix]
		public static bool Interact_SometimesReplace(Network_Player player, bool successFull,
			QuestEventBase __instance)
		{
			if (ComponentManager<ExternalData>.Value.QuestLocations.Any(kvp => kvp.Value.Contains(__instance.name)))
			{
				if (successFull)
				{
					var locationName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, __instance.name);
					ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(locationName);
					(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
						.researchInfoQue.Enqueue(new Notification_Research_Info(locationName, player.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
					if (Semih_Network.IsHost)
					{
						__instance.CurrentObjectStateIndex++;
					}
					typeof(QuestEventBase).GetMethod("ConsumeRequiredItems", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { player });
					typeof(QuestEventBase).GetMethod("RefreshColliderState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, null);
					__instance.gameObject.SetActiveSafe(false); // TODO Remove only relevant object (currently removes too much, eg removes table objects are sitting on)
				}
				else
				{
					Debug.Log($"I: {__instance.name} (Unsusccessful)");
				}
				return false;
			}
			return true;
		}
	}
}
//SendOnInteractEvent