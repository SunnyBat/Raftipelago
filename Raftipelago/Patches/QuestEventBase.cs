﻿using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using System.Collections.Generic;
using System.Reflection;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(QuestEventBase), "Interact", typeof(Network_Player), typeof(bool))]
	public class HarmonyPatch_QuestEventBase_Interact
	{
		[HarmonyPrefix]
		public static bool SometimesReplace(Network_Player player, bool successFull,
			QuestEventBase __instance)
		{
			if (__instance.name == "QuestInteractable_WorkBench_CaravanIsland_ZipLineHandle" || __instance.name == "QuestInteractable_WorkBench_CaravanIsland_BatteryCharger")
			{
				// We need to complete the quest without giving vanilla rewards, including updating the underlying GameObject's state (to hide it)
				if (successFull)
				{
					if (Raft_Network.IsHost)
					{
						__instance.CurrentObjectStateIndex++;
					}
					typeof(QuestEventBase).GetMethod("ConsumeRequiredItems", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { player });
					foreach (var objOnOff in __instance.GetComponents<QuestInteractableComponent_ObjectOnOff>())
					{
						// Grab the specific required component (eg a toolbox) and hide it
						var dataComponents = (List<QuestInteractable_ComponentData_ObjectOnOff>)objOnOff.GetType().GetField("dataComponents").GetValue(objOnOff);
						foreach (var dataComp in dataComponents)
						{
							foreach (var gameObject in dataComp.gameObjects)
							{
								gameObject.SetActiveSafe(false);
							}
						}
					}
					typeof(QuestEventBase).GetMethod("RefreshColliderState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, null);

					// Trigger notification and Archipelago location check instead
					var pickupName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, __instance.name);
					if (Raft_Network.IsHost)
					{
						ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(pickupName);
					}
					(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
						.researchInfoQue.Enqueue(new Notification_Research_Info(pickupName, ComponentManager<Raft_Network>.Value.LocalSteamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
					if (ComponentManager<ExternalData>.Value.LocationsToSuppress.Contains(pickupName))
					{
						return false;
					}
				}
			}
			else if (__instance.name == "QuestInteractable_Utopia_Door_People")
            {
				if (successFull)
				{
					ComponentManager<IArchipelagoLink>.Value.SetGameCompleted(true);
				}
				// Let game handle ending scene
            }

			return true;
		}
	}
}