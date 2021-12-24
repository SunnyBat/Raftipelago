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
            "Battery", "Bolt", "Circuit board", "Hinge", "Receiver", "Antenna", "Smelter", // Radio Tower requirements
            "Engine", "Steering Wheel", // Balboa requirements
            "Machete", // Balboa completion requirement
            "Zipline tool", // Caravan Island completion requirement

            "NoteBookNote_Index2_Post-it", // At Radio Tower
            "NoteBookNote_Index17_Vasagatan_PostItNote_FrequencyToBalboa", // At Vasagatan
            // Balboa does not have a note
            "NoteBookNote_Index43_Landmark_CaravanIsland_FrequencyToTangaroa" // At Caravan Island
            // Tangaroa will likely have a note once further islands are added, but for now is the end of the game
        };
        private static Dictionary<string, string> RegionCorrections = new Dictionary<string, string>()
        {
            {
                "19#Landmark_Radar#Big radio tower", "RadioTower"
            },
            {
                "44#Landmark_Vasagatan", "Vasagatan"
            },
            {
                "45#Landmark_BalboaIsland", "BalboaIsland"
            },
            {
                "49#Landmark_CaravanIsland#RealDeal", "CaravanIsland"
            },
            {
                "50#Landmark_Tangaroa#", "Tangaroa"
            }
        };
        // Some location checks cannot be accessed until after specific islands are able to be
        // completed. For example, some notes on Balboa Island are locked behind the machete, which
        // is not necessary to reach the island but is necessary to complete it.
        private static Dictionary<string, string> LocationRegionFixes = new Dictionary<string, string>()
        {
            {
                "Pickup_Landmark_Blueprint_BiofuelExtractor", "BalboaIslandCompletion"
            },
            {
                "Pickup_Landmark_Blueprint_Fueltank", "BalboaIslandCompletion"
            },
            {
                "Pickup_Landmark_Blueprint_Pipes", "BalboaIslandCompletion"
            },
            {
                "Pickup_Landmark_Blueprint_EngineControls", "CaravanIslandCompletion"
            },
            {
                "Pickup_Landmark_Blueprint_MetalDetector", "CaravanIslandCompletion"
            } // TODO Balboa Island notes, Caravan Island notes
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
                    if (CommonUtils.IsValidResearchTableItem(recipe))
                    {
                        _addItem(ref currentId, recipe.settings_Inventory.DisplayName, allItemData);
                        if (!allItemNames.AddUniqueOnly(recipe.settings_Inventory.DisplayName))
                        {
                            throw new Exception(recipe.settings_Inventory.DisplayName + " is not unique");
                        }
                    }
                });

                var notebook = ComponentManager<NoteBook>.Value;
                var nbNetwork = (Semih_Network)typeof(NoteBook).GetField("network", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(notebook);
                foreach (var nbNote in nbNetwork.GetLocalPlayer().NoteBookUI.GetAllNotes())
                {
                    if (CommonUtils.IsValidNote(nbNote))
                    {
                        _addItem(ref currentId, nbNote.name, allItemData);
                        if (!allItemNames.AddUniqueOnly(nbNote.name))
                        {
                            throw new Exception(nbNote.name + " is not unique");
                        }
                    }
                }
            }
            else
            {
                craftingMenu.AllRecipes.ForEach(recipe =>
                {
                    if (!CommonUtils.IsValidResearchTableItem(recipe))
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
                    foreach (var landmarkItem in landmark.landmarkItems)
                    {
                        if (CommonUtils.IsNoteOrBlueprint(landmarkItem))
                        {
                            LocationRegionFixes.TryGetValue(landmarkItem.name, out string forcedRegionName);
                            var regionName = forcedRegionName ?? (RegionCorrections.TryGetValue(landmark.name, out string correctedRegion) ? correctedRegion : landmark.name);
                            _addLocation(ref currentId, landmarkItem.name, regionName, allLocationData);
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
                        _addLocation(ref currentId, baseItem.settings_Inventory.DisplayName, "ResearchTable", allLocationData);
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
