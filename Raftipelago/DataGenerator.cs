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
            "Machete", "Basic bow", "Stone arrow", // Balboa completion requirements
            "Zipline tool", // Caravan Island completion requirement

            "Vasagatan Frequency", // At Radio Tower
            "Balboa Island Frequency", // At Vasagatan
            "Caravan Island Frequency", // At Balboa Island
            "Tangaroa Frequency" // At Caravan Island
            // Tangaroa will likely have a note once further islands are added, but for now is the end of the game
        };

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
                var nbNetwork = (Semih_Network)typeof(NoteBook).GetField("network", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(notebook);
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
                        if (CommonUtils.IsBlueprint(landmarkItem))
                        {
                            locName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings,
                                landmarkItem.connectedBehaviourID.GetComponent<PickupItem>().PickupName);
                        }
                        else if (CommonUtils.IsNote(landmarkItem) || ComponentManager<ExternalData>.Value.QuestLocations.ContainsKey(landmarkItem.name))
                        {
                            locName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, landmarkItem.name);
                        }
                        else if (landmarkItem.name == "Pickup_Landmark_Caravan_RocketDoll") // Edge cases let's gooooo
                        {
                            locName = "Blueprint: Firework";
                        }
                        else
                        {
                            continue;
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
                        if (CommonUtils.IsBlueprint(landmarkItem) || CommonUtils.IsNote(landmarkItem) || landmarkItem.name == "Pickup_Landmark_Caravan_RocketDoll"
                            || ComponentManager<ExternalData>.Value.QuestLocations.ContainsKey(landmarkItem.name))
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
    }
}
