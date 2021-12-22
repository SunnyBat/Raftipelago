using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago
{
    public class DataGenerator
    {
        private static string[] ProgressionItemList = new string[] {
            "Battery", "Bolt", "CircuitBoard", "Hinge", "Placeable_Reciever", "Placeable_Reciever_Antenna", "Placeable_CookingStand_Smelter", // Radio Tower requirements
            "Placeable_MotorWheel", "Placeable_SteeringWheel", // Balboa requirements
            "Machete", // Balboa completion requirement
            "ZiplineTool", // Caravan Island completion requirement

            "NoteBookNote_Index2_Post-it", // At Radio Tower
            "NoteBookNote_Index17_Vasagatan_PostItNote_FrequencyToBalboa", // At Vasagatan
            // Balboa does not have a note
            "NoteBookNote_Index43_Landmark_CaravanIsland_FrequencyToTangaroa" // At Caravan Island
            // Tangaroa will likely have a note once further islands are added, but for now is the end of the game
        };

        public static string GenerateRawArchipelagoItemList(bool invert = false)
        {
            var craftingMenu = ComponentManager<CraftingMenu>.Value;
            var allItemData = new StringBuilder();
            allItemData.Append("[");
            int currentId = 47001;
            if (!invert)
            {
                craftingMenu.AllRecipes.ForEach(recipe =>
                {
                    if (recipe.settings_recipe.CraftingCategory != CraftingCategory.Hidden
                        && recipe.settings_recipe.CraftingCategory != CraftingCategory.Decorations
                        && recipe.settings_recipe.CraftingCategory != CraftingCategory.CreativeMode
                        && recipe.settings_recipe.CraftingCategory != CraftingCategory.Skin
                        && !recipe.settings_recipe.LearnedFromBeginning)
                    {
                        _addItem(ref currentId, recipe.UniqueName, allItemData);
                    }
                });

                var notebook = ComponentManager<NoteBook>.Value;
                var nbNetwork = (Semih_Network)typeof(NoteBook).GetField("network", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(notebook);
                foreach (var nbNote in nbNetwork.GetLocalPlayer().NoteBookUI.GetAllNotes())
                {
                    if (CommonUtils.IsValidNote(nbNote))
                    {
                        _addItem(ref currentId, nbNote.name, allItemData);
                    }
                }
            }
            else
            {
                craftingMenu.AllRecipes.ForEach(recipe =>
                {
                    if (!(recipe.settings_recipe.CraftingCategory != CraftingCategory.Hidden
                        && recipe.settings_recipe.CraftingCategory != CraftingCategory.Decorations
                        && recipe.settings_recipe.CraftingCategory != CraftingCategory.CreativeMode
                        && recipe.settings_recipe.CraftingCategory != CraftingCategory.Skin
                        && !recipe.settings_recipe.LearnedFromBeginning))
                    {
                        _addItem(ref currentId, recipe.UniqueName, allItemData);
                    }
                });
            }
            allItemData.Append("]");
            return allItemData.ToString();
        }

        private static void _addItem(ref int id, string name, StringBuilder itemData)
        {
            if (itemData.Length > 1)
            {
                itemData.Append(",");
            }
            itemData.Append("{");
            itemData.Append($"\"id\":{id++}");
            itemData.Append(",");
            itemData.Append($"\"count\":1");
            itemData.Append(",");
            itemData.Append($"\"progression\":{ProgressionItemList.Any(progName => progName == name).ToString().ToLowerInvariant()}");
            itemData.Append(",");
            itemData.Append($"\"name\":\"{name}\"");
            itemData.Append("}");
        }

        public static string GenerateLocationList(bool invert = false)
        {
            var researchTableInventory = ComponentManager<Inventory_ResearchTable>.Value;
            var allLocationData = new StringBuilder();
            allLocationData.Append("[");
            int currentId = 48001;
            if (!invert)
            {
                researchTableInventory.GetMenuItems().ForEach(rmi =>
                {
                    var baseItem = rmi.GetItem();
                    if (!baseItem.settings_recipe.HiddenInResearchTable && !baseItem.settings_recipe.LearnedViaBlueprint)
                    {
                        _addLocation(ref currentId, baseItem.UniqueName, "ResearchTable", allLocationData);
                    }
                });
                WorldManager.AllLandmarks.ForEach(landmark =>
                {
                    foreach (var lmi in landmark.landmarkItems)
                    {
                        if (CommonUtils.IsNoteOrBlueprint(lmi))
                        {
                            _addLocation(ref currentId, lmi.name, landmark.name, allLocationData);
                        }
                    }
                });
            }
            else
            {
                researchTableInventory.GetMenuItems().ForEach(rmi =>
                {
                    var baseItem = rmi.GetItem();
                    if (baseItem.settings_recipe.HiddenInResearchTable || baseItem.settings_recipe.LearnedViaBlueprint)
                    {
                        _addLocation(ref currentId, baseItem.UniqueName, "ResearchTable", allLocationData);
                    }
                });
            }
            allLocationData.Append("]");
            return allLocationData.ToString();
        }

        private static void _addLocation(ref int id, string name, string region, StringBuilder locData)
        {
            if (locData.Length > 1)
            {
                locData.Append(",");
            }
            locData.Append("{");
            locData.Append($"\"id\":{id++}");
            locData.Append(",");
            locData.Append($"\"name\":\"{name}\"");
            locData.Append(",");
            locData.Append($"\"region\":\"{region}\"");
            locData.Append("}");
        }
    }
}
