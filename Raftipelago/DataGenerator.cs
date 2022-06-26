using Raftipelago.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Raftipelago
{
    public class DataGenerator
    {
        // We could move this to a JSON file, but the comments here are nice
        private static string[] ProgressionItemList = new string[] {
            "Battery", "Bolt", "Circuit board", "Hinge", "Receiver", "Antenna", "Smelter", // Radio Tower requirements
            "Engine", "Steering Wheel", // Balboa requirements
            "Machete", // Balboa completion requirements
            "Zipline tool", // Caravan Island completion requirement

            "Vasagatan Frequency", // At Radio Tower
            "Balboa Island Frequency", // At Vasagatan
            "Caravan Island Frequency", // At Balboa Island
            "Tangaroa Frequency", // At Caravan Island
            "Varuna Point Frequency", // At Tangaroa
            "Temperance Frequency", // At Varuna Point
            "Utopia Frequency", // At Temperance
        };

        public static string GenerateRaftipelagoFriendlyItemList(bool invert = false)
        {
            var craftingMenu = ComponentManager<CraftingMenu>.Value;
            var friendlyData = new StringBuilder();
            friendlyData.Append("{");
            var allFriendlyNames = new List<string>();
            var allUniqueNames = new List<string>();
            craftingMenu.AllRecipes.ForEach(recipe =>
            {
                if (CommonUtils.IsValidUnlockableItem(recipe) == !invert)
                {
                    var friendlyName = recipe.settings_Inventory.DisplayName;
                    var uniqueName = recipe.UniqueName;
                    _addFriendlyMapping(friendlyName, uniqueName, friendlyData);
                    if (!allUniqueNames.AddUniqueOnly(uniqueName))
                    {
                        throw new Exception($"{uniqueName} (Friendly name {friendlyName}) is not unique");
                    }
                    if (!allFriendlyNames.AddUniqueOnly(friendlyName))
                    {
                        throw new Exception($"{friendlyName} (Unique name {uniqueName}) is not unique");
                    }
                }
            });
            foreach (var kvp in ComponentManager<ExternalData>.Value.UniqueItemNameToFriendlyNameMappings)
            {
                var friendlyName = kvp.Value;
                var uniqueName = kvp.Key;
                // Don't dupe existing mappings, since we're putting this output right back into the source of these mappings
                if (allUniqueNames.AddUniqueOnly(uniqueName) && allFriendlyNames.AddUniqueOnly(friendlyName))
                {
                    _addFriendlyMapping(friendlyName, uniqueName, friendlyData);
                }
            }
            friendlyData.Append("}");
            return friendlyData.ToString();
        }

        public static string GenerateRawArchipelagoItemList(bool invert = false)
        {
            var craftingMenu = ComponentManager<CraftingMenu>.Value;
            var allItemData = new StringBuilder();
            allItemData.Append("[");
            var allItemNames = new List<string>();
            int currentId = 47001;
            if (!invert)
            {
                craftingMenu.AllRecipes.ForEach(recipe =>
                {
                    if (CommonUtils.IsValidUnlockableItem(recipe))
                    {
                        var itemName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueItemNameToFriendlyNameMappings, recipe.settings_Inventory.DisplayName);
                        _addItem(ref currentId, itemName, allItemData);
                        if (!allItemNames.AddUniqueOnly(itemName))
                        {
                            throw new Exception(itemName + " is not unique");
                        }
                    }
                });

                var notebook = ComponentManager<NoteBook>.Value;
                var nbNetwork = (Raft_Network)typeof(NoteBook).GetField("network", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(notebook);
                foreach (var nbNote in nbNetwork.GetLocalPlayer().NoteBookUI.GetAllNotes())
                {
                    if (CommonUtils.IsValidNote(nbNote) || nbNote?.name == "ThumbNailButton_CaravanIsland") // Only include valid story-related notes
                    {
                        var noteName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueItemNameToFriendlyNameMappings, nbNote.name);
                        if (ProgressionItemList.Any(progName => progName == noteName))
                        {
                            _addItem(ref currentId, noteName, allItemData);
                            if (!allItemNames.AddUniqueOnly(noteName))
                            {
                                throw new Exception(noteName + " is not unique");
                            }
                        }
                    }
                }
            }
            else
            {
                craftingMenu.AllRecipes.ForEach(recipe =>
                {
                    if (!CommonUtils.IsValidUnlockableItem(recipe))
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
            itemData.Append($"\"progression\":{ProgressionItemList.Any(progName => progName == name).ToString().ToLowerInvariant()}");
            itemData.Append(",");
            itemData.Append($"\"name\":\"{name}\"");
            itemData.Append("}");
        }

        public static string GenerateFriendlyLocationList()
        {
            var researchTableInventory = ComponentManager<Inventory_ResearchTable>.Value;
            var friendlyData = new StringBuilder();
            friendlyData.Append("{");
            var allFriendlyNames = new List<string>();
            var allUniqueNames = new List<string>();
            researchTableInventory.GetMenuItems().ForEach(rmi =>
            {
                var baseItem = rmi.GetItem();
                if (CommonUtils.IsValidResearchTableItem(baseItem))
                {
                    var friendlyName = baseItem.settings_Inventory.DisplayName;
                    var uniqueName = baseItem.UniqueName;
                    UnityEngine.Debug.Log($"{uniqueName} (Friendly name {friendlyName})");
                    _addFriendlyMapping(friendlyName, uniqueName, friendlyData);
                    if (!allUniqueNames.AddUniqueOnly(uniqueName))
                    {
                        throw new Exception($"{uniqueName} (Friendly name {friendlyName}) is not unique");
                    }
                    if (!allFriendlyNames.AddUniqueOnly(friendlyName))
                    {
                        throw new Exception($"{friendlyName} (Unique name {uniqueName}) is not unique");
                    }
                }
            });
            foreach (var kvp in ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings)
            {
                var friendlyName = kvp.Value;
                var uniqueName = kvp.Key;
                // Don't dupe existing mappings, since we're putting this output right back into the source of these mappings
                if (allUniqueNames.AddUniqueOnly(uniqueName) && allFriendlyNames.AddUniqueOnly(friendlyName))
                {
                    _addFriendlyMapping(friendlyName, uniqueName, friendlyData);
                }
            }
            friendlyData.Append("}");
            return friendlyData.ToString();
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
                    if (CommonUtils.IsValidResearchTableItem(baseItem))
                    {
                        List<string> researchItems = new List<string>();
                        var bingoMenuItems = (List<BingoMenuItem>)typeof(ResearchMenuItem).GetField("bingoMenuItems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rmi);
                        foreach (var bingoMenuItem in bingoMenuItems)
                        {
                            var baseBingoItem = (Item_Base)typeof(BingoMenuItem).GetField("bingoItem", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bingoMenuItem);
                            researchItems.Add(baseBingoItem.UniqueName);
                        }
                        _addLocation(ref currentId, baseItem.settings_Inventory.DisplayName, "ResearchTable", allLocationData, researchItems);
                    }
                });
                WorldManager.AllLandmarks.ForEach(landmark =>
                {
                    var regionName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueRegionNameToFriendlyNameMappings, landmark.name);
                    foreach (var landmarkItem in landmark.landmarkItems)
                    {
                        string locName;
                        if (!_isRaftipelagoLocation(landmarkItem))
                        {
                            continue;
                        }
                        if (ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(landmarkItem.name, out string friendlyName))
                        {
                            locName = friendlyName;
                        }
                        else
                        {
                            locName = landmarkItem.name;
                        }
                        var additionalRequirements = ComponentManager<ExternalData>.Value.AdditionalLocationCheckItemRequirements.GetValueOrDefault(locName);
                        var reqList = additionalRequirements == null ? null : new List<string>(additionalRequirements);
                        _addLocation(ref currentId, locName, regionName, allLocationData, reqList);
                    }
                });
                foreach (var questRegion in ComponentManager<ExternalData>.Value.QuestLocations.Keys)
                {
                    foreach (var questLocation in ComponentManager<ExternalData>.Value.QuestLocations[questRegion])
                    {
                        var locName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, questLocation);
                        var additionalRequirements = ComponentManager<ExternalData>.Value.AdditionalLocationCheckItemRequirements.GetValueOrDefault(locName);
                        var reqList = additionalRequirements == null ? null : new List<string>(additionalRequirements);
                        _addLocation(ref currentId, locName, questRegion, allLocationData, reqList);
                    }
                }
            }
            else
            {
                researchTableInventory.GetMenuItems().ForEach(rmi =>
                {
                    var baseItem = rmi.GetItem();
                    if (baseItem.settings_recipe.HiddenInResearchTable || baseItem.settings_recipe.LearnedViaBlueprint)
                    {
                        _addLocation(ref currentId, baseItem.settings_Inventory.DisplayName, "ResearchTable", allLocationData);
                    }
                });
                WorldManager.AllLandmarks.ForEach(landmark =>
                {
                    var regionName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueRegionNameToFriendlyNameMappings, landmark.name);
                    foreach (var landmarkItem in landmark.landmarkItems)
                    {
                        string locName;
                        if (_isRaftipelagoLocation(landmarkItem))
                        {
                            continue;
                        }
                        else
                        {
                            locName = landmarkItem.name;
                        }
                        var additionalRequirements = ComponentManager<ExternalData>.Value.AdditionalLocationCheckItemRequirements.GetValueOrDefault(locName);
                        var reqList = additionalRequirements == null ? null : new List<string>(additionalRequirements);
                        _addLocation(ref currentId, locName, regionName, allLocationData, reqList);
                    }
                });
            }
            allLocationData.Append("]");
            return allLocationData.ToString();
        }

        private static bool _isRaftipelagoLocation(LandmarkItem loc)
        {
            return ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings.TryGetValue(loc.name, out string friendlyName);
        }

        private static void _addLocation(ref int id, string name, string region, StringBuilder locData, List<string> requiredLocations = null)
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
            if (requiredLocations?.Count > 0)
            {
                locData.Append(",");
                locData.Append($"\"requiresAccessToItems\":[");
                for (int i = 0; i < requiredLocations.Count; i++)
                {
                    if (i > 0)
                    {
                        locData.Append(",");
                    }
                    locData.Append($"\"{requiredLocations[i]}\"");
                }
                locData.Append("]");
            }
            locData.Append("}");
        }

        private static void _addFriendlyMapping(string friendlyName, string uniqueName, StringBuilder itemData)
        {
            if (itemData.Length > 1)
            {
                itemData.Append(",");
            }
            itemData.Append($"\"{uniqueName}\":\"{friendlyName}\"");
        }
    }
}
