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
			// Swallow the event entirely
			return false;
		}
	}
}