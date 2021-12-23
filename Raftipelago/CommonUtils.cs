using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago
{
    // We could make extensions, but I don't want to have this in IntelliSense in case it implies
    // that the function is in Raft (thus making it more reliable when updates happen) instead of
    // in our code. Because of this, extensions should not be used for Raft classes.
    public class CommonUtils
    {
        public static bool IsNote(LandmarkItem item)
        {
            var name = item?.name ?? "";
            return name.Contains("NoteBookPickup");
        }
        public static bool IsBlueprint(LandmarkItem item)
        {
            var name = item?.name ?? "";
            return name.Contains("Blueprint");
        }
        public static bool IsNoteOrBlueprint(LandmarkItem item)
        {
            return IsNote(item) || IsBlueprint(item);
        }

        public static bool IsValidNote(NoteBookNote note)
        {
            return note?.name?.Contains("NoteBookNote") ?? false;
        }

        public static bool IsValidResearchTableItem(Item_Base item)
        {
            return item.settings_recipe.CraftingCategory != CraftingCategory.Hidden
                && item.settings_recipe.CraftingCategory != CraftingCategory.Decorations
                && item.settings_recipe.CraftingCategory != CraftingCategory.CreativeMode
                && item.settings_recipe.CraftingCategory != CraftingCategory.Skin
                && !item.settings_recipe.LearnedFromBeginning;
        }
    }
}
