using HarmonyLib;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Pickup_Specific_Blueprint), "PickupSpecific", typeof(PlayerInventory))]
	public class HarmonyPatch_Pickup_Specific_Blueprint_PickupSpecific
	{
		[HarmonyPrefix]
		public static bool AlwaysReplace(PlayerInventory inventory)
		{
			// Swallow the event entirely, we never want to fire it
			return false;
		}
	}
}