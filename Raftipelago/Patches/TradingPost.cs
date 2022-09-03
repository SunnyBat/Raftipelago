using HarmonyLib;
using System.Collections.Generic;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(TradingPost), "ResetItemsForSale")]
	public class HarmonyPatch_TradingPost_ResetItemsForSale
	{
		[HarmonyPostfix]
		public static void NeverReplace(List<SO_TradingPost_Buyable.Instance> ___buyableItems)
		{
			// Eventually we can override a Buy method to prevent the blueprint from being rewarded and instead
			// count it as an Archipelago check. But for now, this is way easier (and doesn't require AP changes)
			Logger.Debug("Removing Trading Post blueprints");
			for (int i = 0; i < ___buyableItems.Count; i++)
			{
				Logger.Trace($"Checking {___buyableItems[i]?.reward?.item?.UniqueName}");
				if (___buyableItems[i]?.reward?.item?.settings_recipe.IsBlueprint ?? false)
                {
					Logger.Trace($"Removing blueprint {___buyableItems[i].reward.item.UniqueName} from Trading Post");
					___buyableItems.RemoveAt(i);
					i--; // Decrement to check the next item in the list on next loop
                }
            }
		}
	}
}
