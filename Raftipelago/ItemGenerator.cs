﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago
{
    public class ItemGenerator
    {
        private static string[] ProgressionItemList = new string[] { "ZiplineTool",
            "Battery", "Bolt", "CircuitBoard", "Hinge",
            "Placeable_EngineControls", "Placeable_MotorWheel",
            "Placeable_Reciever", "Placeable_Reciever_Antenna",
            "Placeable_SteeringWheel", "Placeable_CookingStand_Smelter", // TODO Is Placeable_CookingStand_Smelter actually the smelter?
            "Placeable_FuelTank", "Placeable_Pipe_Fuel",
            "Placeable_ZiplineBase"
        };
        private static string[] PsuedoItems = new string[]
        {
            "RadioTowerRadioTranscription" // TODO Add all coordinates
        };

        public static string GenerateRawArchipelagoItemList()
        {
            var craftingMenu = ComponentManager<CraftingMenu>.Value;
            var allItemData = new StringBuilder();
            allItemData.Append("[");
            int currentId = 47001;
            craftingMenu.AllRecipes.ForEach(recipe =>
            {
                if (recipe.settings_recipe.CraftingCategory != CraftingCategory.Hidden
                    && recipe.settings_recipe.CraftingCategory != CraftingCategory.Decorations
                    && recipe.settings_recipe.CraftingCategory != CraftingCategory.CreativeMode
                    && recipe.settings_recipe.CraftingCategory != CraftingCategory.Skin
                    && !recipe.settings_recipe.LearnedFromBeginning)
                {
                    if (allItemData.Length > 1)
                    {
                        allItemData.Append(",");
                    }
                    allItemData.Append("{");
                    allItemData.Append($"\"id\":{currentId++}");
                    allItemData.Append(",");
                    allItemData.Append($"\"count\":1");
                    allItemData.Append(",");
                    allItemData.Append($"\"progression\":{ProgressionItemList.Any(progName => progName == recipe.UniqueName).ToString().ToLowerInvariant()}");
                    allItemData.Append(",");
                    allItemData.Append($"\"name\":\"{recipe.UniqueName}\"");
                    allItemData.Append("}");
                }
            });
            PsuedoItems.Do(itm => {
                if (allItemData.Length > 1)
                {
                    allItemData.Append(",");
                }
                allItemData.Append("{");
                allItemData.Append($"\"id\":{currentId++}");
                allItemData.Append(",");
                allItemData.Append($"\"count\":1");
                allItemData.Append(",");
                allItemData.Append($"\"progression\":true");
                allItemData.Append(",");
                allItemData.Append($"\"name\":\"{itm}\"");
                allItemData.Append("}");
            });
            allItemData.Append("]");
            return allItemData.ToString();
        }
    }
}