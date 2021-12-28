using HarmonyLib;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Pickup_Specific_CookingTableRecipe), "PickupSpecific", typeof(PlayerInventory))]
	public class HarmonyPatch_Pickup_Specific_CookingTableRecipe_PickupSpecific
	{
		[HarmonyPrefix]
		public static bool AlwaysReplace(PlayerInventory inventory)
		{
			// Swallow the event entirely
			return false;
		}
	}
}