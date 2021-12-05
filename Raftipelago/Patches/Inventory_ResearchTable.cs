using FMODUnity;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Inventory_ResearchTable), "LearnItem", typeof(Item_Base), typeof(CSteamID))]
	public class HarmonyPatch_Inventory_ResearchTable_LearnItem
	{
		[HarmonyPrefix]
		public static bool LearnItem_OptionallyReplace(Item_Base item, CSteamID researcherID,
			ref bool __result,
			Inventory_ResearchTable __instance,
			ref string ___eventRef_Learn,
			ref NotificationManager ___notificationManager,
			ref List<ResearchMenuItem> ___menuItems)
		{
			for (int i = 0; i < ___menuItems.Count; i++)
			{
				if (!___menuItems[i].Learned && ___menuItems[i].GetItem().UniqueIndex == item.UniqueIndex)
				{
				}
			}
			__instance.SortMenuItems();
			return false;
		}
	}
}
