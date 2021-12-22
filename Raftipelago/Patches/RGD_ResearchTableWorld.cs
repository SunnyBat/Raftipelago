using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago.Patches
{
    [HarmonyPatch(typeof(RGD_ResearchTableWorld), "RestoreResearchTableWorld")]
    public class HarmonyPatch_RGD_ResearchTableWorld_RestoreResearchTableWorld
    {
        [HarmonyPrefix]
        public static bool RestoreResearchTableWorld_AlwaysReplace(Inventory_ResearchTable inventoryRef,
			List<int> ___researchedItems,
			List<int> ___learnedItems)
		{
			// First, we learn all items. We don't learn instantly, since that enables researched items
			if (___learnedItems != null)
			{
				List<ResearchMenuItem> menuItems = inventoryRef.GetMenuItems();
				for (int j = 0; j < menuItems.Count; j++)
				{
					if (___learnedItems.Contains(menuItems[j].GetItem().UniqueIndex))
					{
						menuItems[j].Learn();
						//menuItems[j].sprite = menuItems[j].item.settings_Inventory.Sprite;
					}
				}
			}
			// Then, we research previously-researched items
			if (___researchedItems != null)
			{
				for (int i = 0; i < ___researchedItems.Count; i++)
				{
					Item_Base itemByIndex = ItemManager.GetItemByIndex(___researchedItems[i]);
					if (itemByIndex != null)
					{
						inventoryRef.Research(itemByIndex, false);
					}
				}
			}
			// Finally, we sort.
			inventoryRef.SortMenuItems();
			return false;
        }
    }
}
