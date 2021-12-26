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
			if (__instance.name == "QuestInteractable_WorkBench_CaravanIsland_ZipLineHandle" || __instance.name == "QuestInteractable_WorkBench_CaravanIsland_BatteryCharger")
			{
				if (successFull)
				{
					if (Semih_Network.IsHost)
					{
						__instance.CurrentObjectStateIndex++;
					}
					typeof(QuestEventBase).GetMethod("ConsumeRequiredItems", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { player });
				}
				foreach (var objOnOff in __instance.GetComponents<QuestInteractableComponent_ObjectOnOff>())
				{
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
				var pickupName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, __instance.name);
				var localSteamId = ((Semih_Network)typeof(QuestEventBase).GetProperty("Network", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance)).LocalSteamID;
				ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(pickupName);
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research)
					.researchInfoQue.Enqueue(new Notification_Research_Info(pickupName, localSteamId, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
			}
			else
			{
				Debug.Log("Interact: " + __instance.name + " (" + successFull + ")");
				try
				{
					foreach (var obj in __instance.GetComponents<object>())
					{
						Debug.Log("TYPE: " + obj.GetType());
						if (obj.GetType() == typeof(QuestInteractableComponent_ObjectOnOff))
						{
							var dataComponents = (List<QuestInteractable_ComponentData_ObjectOnOff>)obj.GetType().GetField("dataComponents").GetValue(obj);
							foreach (var dataComp in dataComponents)
							{
								foreach (var gameObject in dataComp.gameObjects)
								{
									gameObject.SetActiveSafe(false);
								}
							}
						}
						else
						{
							foreach (var f in obj.GetType().GetRuntimeFields())
							{
								Debug.Log($"{f.Name}:" + f.GetValue(obj));
							}
							foreach (var p in obj.GetType().GetRuntimeProperties())
							{
								Debug.Log($"{p.Name}:" + p.GetValue(obj));
							}
						}
					}
				}
				catch (Exception e)
				{
					Debug.Log(e);
				}
			}
            return false;
		}
	}
}
//SendOnInteractEvent