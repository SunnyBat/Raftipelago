using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Pickup_Specific_CookingTableRecipe), "PickupSpecific", typeof(PlayerInventory))]
	public class HarmonyPatch_Pickup_Specific_CookingTableRecipe_PickupSpecific
	{
		[HarmonyPrefix]
		public static bool PickupSpecific_AlwaysReplace(PlayerInventory inventory,
			RandomDropper ___randomRecipeDropper)
		{
			List<string> itms = new List<string>();
			for (var i = 0; i < 10000; i++)
			{
				var itm = ___randomRecipeDropper.GetRandomItem();
				if (!itms.Contains(itm.UniqueName))
				{
					itms.Add(itm.UniqueName);
				}
			}
			Debug.Log(string.Join(",", itms));
			// Swallow the event entirely
			return false;
		}
	}
}